#!/usr/bin/env bun

import { spawn, sleep, Glob } from "bun";
import { connect, disconnect, end, setTestName, _didFail } from "../tests/monitor";

function timeout(ms: number): Promise<never> {
  return new Promise((_, reject) => setTimeout(() => reject(new Error("timeout")), ms));
}

const root = `${import.meta.dir}/..`;
const name = process.argv[2];

let names: string[];
if (name) {
  names = [name];
} else {
  const glob = new Glob("u_*.ts");
  names = [];
  for await (const f of glob.scan(`${root}/tests`))
    names.push(f.replace(/\.ts$/, ""));
  names.sort();
}

let passed = 0, failed = 0, failures: string[] = [];

for (const n of names) {
  const server = spawn(["./bin/Debug/net9.0/pathhack", "--debug-server"], {
    cwd: root,
    stdout: "ignore",
    stderr: "inherit",
  });

  let ok = false;
  try {
    await sleep(100);
    for (let i = 0; ; i++) {
      try { await Promise.race([connect(4777, "127.0.0.1", "auto"), timeout(500)]); break; }
      catch { disconnect(); if (i >= 50) throw new Error("server did not start"); await sleep(100); }
    }
    const test = await import(`../tests/${n}.ts`);
    setTestName(n);
    await Promise.race([test.default(), timeout(10000)]);
    await end();
    ok = !_didFail;
  } catch (e) {
    if (!names.length || names.length === 1) console.error(e);
  }

  await Promise.race([server.exited, sleep(3000)]);
  server.kill();

  if (ok) passed++;
  else { failed++; failures.push(n); }
}

if (names.length > 1) {
  console.log(`\n${passed} passed, ${failed} failed out of ${names.length}`);
  if (failures.length) console.log(`failures: ${failures.join(", ")}`);
}

process.exit(0);
