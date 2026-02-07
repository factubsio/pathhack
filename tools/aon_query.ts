#!/usr/bin/env bun

import { readdir, readFile } from "fs/promises";

const AON_DATA = `${import.meta.dir}/../../aon/aon_data`;

const [verb, category, query] = Bun.argv.slice(2);

if (!verb || !category || !query) {
  console.error("Usage:");
  console.error("  aon_query index creatures snake      # list folders");
  console.error("  aon_query find creatures viper       # search names");
  console.error("  aon_query show creatures snake/viper # show content");
  process.exit(1);
}

const categoryMap: Record<string, string[]> = {
  creatures: ["creatures_by_family", "creatures_by_trait", "creatures_by_level"],
  feats: ["feats_by_trait"],
  spells: ["spells_by_level", "spells_by_tradition"],
};

const dirs = categoryMap[category];
if (!dirs) {
  console.error(`Unknown category: ${category}`);
  console.error(`Valid: ${Object.keys(categoryMap).join(", ")}`);
  process.exit(1);
}

const matches = (name: string, query: string) => {
  const n = name.toLowerCase();
  const q = query.toLowerCase().replace(/s$/, "");
  return n === q || n.startsWith(q + " ") || n.endsWith(" " + q) || n.includes(" " + q + " ") || n.startsWith(q);
};

if (verb === "index") {
  for (const dir of dirs) {
    const base = `${AON_DATA}/${dir}`;
    for (const entry of await readdir(base, { withFileTypes: true })) {
      if (!entry.isDirectory()) continue;
      if (!matches(entry.name, query)) continue;
      const files = (await readdir(`${base}/${entry.name}`)).filter(f => f.endsWith(".txt"));
      console.log(`${dir}/${entry.name}: ${files.map(f => f.replace(".txt", "")).join(", ")}`);
    }
  }
} else if (verb === "find") {
  for (const dir of dirs) {
    const base = `${AON_DATA}/${dir}`;
    for (const entry of await readdir(base, { withFileTypes: true })) {
      if (!entry.isDirectory()) continue;
      const familyPath = `${base}/${entry.name}`;
      const files = (await readdir(familyPath)).filter(f => f.endsWith(".txt"));
      const matched = files.filter(f => matches(f.replace(".txt", ""), query));
      if (matched.length > 0) {
        console.log(`${dir}/${entry.name}: ${matched.map(f => f.replace(".txt", "")).join(", ")}`);
      }
    }
  }
} else if (verb === "show") {
  const [folder, name] = query.split("/");
  if (!folder || !name) {
    console.error("show requires folder/name format");
    process.exit(1);
  }
  for (const dir of dirs) {
    const base = `${AON_DATA}/${dir}`;
    for (const entry of await readdir(base, { withFileTypes: true })) {
      if (!entry.isDirectory()) continue;
      if (!matches(entry.name, folder)) continue;
      const familyPath = `${base}/${entry.name}`;
      for (const file of await readdir(familyPath)) {
        if (!file.endsWith(".txt")) continue;
        if (!matches(file.replace(".txt", ""), name)) continue;
        console.log(`\n=== ${entry.name}: ${file.replace(".txt", "")} ===\n`);
        console.log(await readFile(`${familyPath}/${file}`, "utf-8"));
      }
    }
  }
} else {
  console.error(`Unknown verb: ${verb}`);
  console.error("Valid: index, find, show");
  process.exit(1);
}
