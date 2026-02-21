#!/usr/bin/env bun
import { readFileSync } from "fs";
import { parseArgs } from "node:util";
import Table from "cli-table3";
import { parseLog, type Attack, type Check } from "./log-common";

const { values: args, positionals } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    s: { type: "string", multiple: true, short: "s" },
    j: { type: "boolean", short: "j", default: false },
  },
  allowPositionals: true,
  strict: false,
});

const file = positionals[0] ?? "game.log";
const sections = new Set(args.s ?? ["ttk", "hp", "recent"]);
const json = args.j!;

const { attacks, checks, damages, spawns, deaths, levelups, exps, heals, equips, casts, actions, events, timeSeries, maxRound } = parseLog(readFileSync(file, "utf-8"));

// --- Analysis helpers ---

function atkStats(atks: Attack[]) {
  if (atks.length === 0) return null;
  const hits = atks.filter(a => a.hit);
  let expectedHits = 0;
  for (const a of atks) {
    const needed = a.ac - (a.roll - a.base_roll);
    expectedHits += Math.min(1, Math.max(0.05, (21 - needed) / 20));
  }
  const avgBase = atks.reduce((s, a) => s + a.base_roll, 0) / atks.length;
  const n = atks.length, p = expectedHits / n;
  const z = Math.sqrt(n * p * (1 - p)) > 0 ? (hits.length - expectedHits) / Math.sqrt(n * p * (1 - p)) : 0;
  const avgDmg = hits.length > 0 ? hits.filter(h => h.damage != null).reduce((s, h) => s + h.damage!, 0) / hits.length : 0;
  return { n, hits: hits.length, hitPct: +(hits.length / n * 100).toFixed(1), expectedPct: +(expectedHits / n * 100).toFixed(1), avgBase: +avgBase.toFixed(1), avgDmg: +avgDmg.toFixed(1), z: +z.toFixed(2) };
}

function saveStats(svs: Check[]) {
  if (svs.length === 0) return null;
  const passes = svs.filter(s => s.result);
  let expectedPasses = 0;
  for (const s of svs) {
    const needed = s.dc - (s.roll - s.base_roll);
    expectedPasses += Math.min(1, Math.max(0.05, (21 - needed) / 20));
  }
  const avgBase = svs.reduce((s, c) => s + c.base_roll, 0) / svs.length;
  const n = svs.length, p = expectedPasses / n;
  const z = Math.sqrt(n * p * (1 - p)) > 0 ? (passes.length - expectedPasses) / Math.sqrt(n * p * (1 - p)) : 0;
  return { n, passes: passes.length, passPct: +(passes.length / n * 100).toFixed(1), expectedPct: +(expectedPasses / n * 100).toFixed(1), avgBase: +avgBase.toFixed(1), z: +z.toFixed(2) };
}

function luck(z: number) { return z > 1 ? "🍀 lucky" : z < -1 ? "😢 unlucky" : "😐 average"; }

function printAtk(label: string, atks: Attack[]) {
  const s = atkStats(atks);
  if (!s) return;
  console.log(`\n${label} (${s.n} attacks)`);
  console.log(`  Hit rate: ${s.hitPct}% (${s.hits}/${s.n}), expected ${s.expectedPct}%`);
  console.log(`  Avg base d20: ${s.avgBase} (expected 10.5)`);
  if (s.hits > 0) console.log(`  Avg damage on hit: ${s.avgDmg}`);
  console.log(`  Luck: z=${s.z} ${luck(s.z)}`);
}

function printSaves(label: string, allSaves: Check[]) {
  if (allSaves.length === 0) return;
  const tags = ["All", ...new Set(allSaves.map(s => s.tag))];
  const table = new Table({ head: ["", "n", "pass%", "exp%", "avg d20"] });
  for (const tag of tags) {
    const svs = tag === "All" ? allSaves : allSaves.filter(s => s.tag === tag);
    const s = saveStats(svs);
    if (!s) continue;
    table.push([tag, s.n, `${s.passPct}%`, `${s.expectedPct}%`, s.avgBase]);
  }
  console.log(`\n${label}`);
  console.log(table.toString());
}

// --- Computed data ---

const spawnById = new Map(spawns.map(s => [s.id, s]));

// TTK = attack rolls (hits + misses) to kill, from death record
const ttks = deaths.flatMap(d => {
  const s = spawnById.get(d.id);
  if (!s) return [];
  return [{ level: s.level, name: s.name, ttk: d.hits + d.misses }];
});

const ttkByLevel = new Map<number, number[]>();
for (const t of ttks) {
  if (!ttkByLevel.has(t.level)) ttkByLevel.set(t.level, []);
  ttkByLevel.get(t.level)!.push(t.ttk);
}

// --- Output ---

function showSection(name: string, renderText: () => void, renderJson: () => unknown) {
  if (!sections.has(name)) return;
  if (json) {
    const data = renderJson();
    console.log(JSON.stringify({ [name]: data }, null, 2));
  } else {
    renderText();
  }
}

showSection("ttk", () => {
  console.log("\n=== TTK BY MONSTER LEVEL (attack rolls to kill) ===");
  const allAvgs: number[] = [];
  const rows: { level: number; n: number; avg: number; med: number; max: number }[] = [];
  for (const [level, vals] of [...ttkByLevel.entries()].sort((a, b) => a[0] - b[0])) {
    vals.sort((a, b) => a - b);
    const med = vals[Math.floor(vals.length / 2)];
    const avg = vals.reduce((a, b) => a + b, 0) / vals.length;
    const max = vals[vals.length - 1];
    rows.push({ level, n: vals.length, avg, med, max });
    allAvgs.push(avg);
  }
  const maxAvg = Math.max(...allAvgs, 1);
  for (const r of rows) {
    const bar = "█".repeat(Math.max(1, Math.round(r.avg / maxAvg * 30)));
    console.log(`  L${String(r.level).padStart(2)}: n=${String(r.n).padStart(3)} avg=${String(r.avg.toFixed(1)).padStart(5)} med=${String(r.med).padStart(3)} max=${String(r.max).padStart(3)} ${bar}`);
  }
}, () => {
  const out: Record<number, { n: number; avg: number; med: number; max: number; min: number }> = {};
  for (const [level, vals] of ttkByLevel) {
    vals.sort((a, b) => a - b);
    out[level] = { n: vals.length, avg: Math.round(vals.reduce((a, b) => a + b, 0) / vals.length), med: vals[Math.floor(vals.length / 2)], max: vals[vals.length - 1], min: vals[0] };
  }
  return out;
});

showSection("events", () => {
  console.log("\n=== COMBAT FLOW ===");
  events.sort((a, b) => a.round - b.round);
  for (const e of events) console.log(`  R${e.round}: ${e.text}`);
}, () => events);

showSection("attacks", () => {
  const playerAtks = attacks.filter(a => a.attacker === "you");
  const monsterAtks = attacks.filter(a => a.attacker !== "you" && a.defender === "you");
  printAtk("Player attacks", playerAtks);
  printAtk("Monster attacks (vs you)", monsterAtks);
  const names = [...new Set(monsterAtks.map(a => a.attacker))];
  for (const name of names) printAtk(`  ${name}`, monsterAtks.filter(a => a.attacker === name));
}, () => ({ player: atkStats(attacks.filter(a => a.attacker === "you")), monsters: atkStats(attacks.filter(a => a.attacker !== "you" && a.defender === "you")) }));

showSection("saves", () => {
  const saves = checks.filter(c => c.key.endsWith("_save"));
  printSaves("Saves (vs you)", saves);
}, () => saveStats(checks.filter(c => c.key.endsWith("_save"))));

showSection("recent", () => {
  const cutoff = maxRound - 200;
  const rf = (r: number) => r >= cutoff;
  console.log(`\n=== LAST 200 ROUNDS (R${cutoff}–R${maxRound}) ===`);

  const rEvents = events.filter(e => rf(e.round));
  if (rEvents.length > 0) {
    console.log("\n--- Flow ---");
    for (const e of rEvents) console.log(`  R${e.round}: ${e.text}`);
  }

  const rPlayerAtks = attacks.filter(a => rf(a.round) && a.attacker === "you");
  printAtk("Player attacks", rPlayerAtks);

  const rSaves = checks.filter(c => rf(c.round) && c.key.endsWith("_save"));
  printSaves("Saves (vs you)", rSaves);
}, () => {
  const cutoff = maxRound - 200;
  const rf = (r: number) => r >= cutoff;
  return {
    attacks: atkStats(attacks.filter(a => rf(a.round) && a.attacker === "you")),
    monsters: atkStats(attacks.filter(a => rf(a.round) && a.attacker !== "you" && a.defender === "you")),
    saves: saveStats(checks.filter(c => rf(c.round) && c.key.endsWith("_save"))),
  };
});

showSection("hp", () => {
  const pts = timeSeries.filter(t => t.hp !== undefined);
  if (pts.length === 0) { console.log("\n=== HP OVER TIME === (no data)"); return; }
  const maxHp = Math.max(...pts.map(p => p.hp!));
  const width = 60;
  console.log(`\n=== HP OVER TIME (max ${maxHp}) ===`);
  // bucket into ~40 rows by round range
  const rows = 40;
  const bucketSize = Math.max(1, Math.ceil(maxRound / rows));
  for (let b = 0; b < rows; b++) {
    const lo = b * bucketSize, hi = (b + 1) * bucketSize;
    const inBucket = pts.filter(p => p.round >= lo && p.round < hi);
    if (inBucket.length === 0) continue;
    const minHpB = Math.min(...inBucket.map(p => p.hp!));
    const maxHpB = Math.max(...inBucket.map(p => p.hp!));
    const barMin = Math.round(minHpB / maxHp * width);
    const barMax = Math.round(maxHpB / maxHp * width);
    const bar = " ".repeat(barMin) + "█".repeat(Math.max(1, barMax - barMin));
    console.log(`  R${String(lo).padStart(5)}–${String(hi).padStart(5)}: ${bar} ${minHpB}–${maxHpB}`);
  }
}, () => timeSeries.filter(t => t.hp !== undefined).map(t => ({ round: t.round, hp: t.hp })));

for (const raw of ["damages", "spawns", "deaths", "levelups", "exps", "heals", "equips", "casts", "actions"] as const) {
  const data: Record<string, unknown[]> = { damages, spawns, deaths, levelups, exps, heals, equips, casts, actions };
  showSection(raw, () => {
    console.log(`\n=== ${raw.toUpperCase()} ===`);
    for (const d of data[raw]) console.log(`  ${JSON.stringify(d)}`);
  }, () => data[raw]);
}
