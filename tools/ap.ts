#!/usr/bin/env bun
import { readFileSync, writeFileSync, existsSync } from "fs";

const DB_PATH = "./docs/ap_chapters.json";

interface Chapter {
  ap: string;
  chapter: string;
  index: number;
  allocatedTo: string | null;
  themes: string[];
}

interface DB {
  meaty: string[];
  chapters: Chapter[];
}

const load = (): DB => JSON.parse(readFileSync(DB_PATH, "utf-8"));
const save = (db: DB) => writeFileSync(DB_PATH, JSON.stringify(db, null, 2));

const cmd = process.argv[2];
const args = process.argv.slice(3);

switch (cmd) {
  case "list": {
    const db = load();
    const ap = args[0];
    const chapters = ap ? db.chapters.filter(c => c.ap.toLowerCase().includes(ap.toLowerCase())) : db.chapters;
    for (const c of chapters) {
      const status = c.allocatedTo ? `[${c.allocatedTo}]` : "";
      console.log(`${c.ap} #${c.index}: ${c.chapter} ${status}`);
    }
    break;
  }
  case "free": {
    const db = load();
    const free = db.chapters.filter(c => !c.allocatedTo);
    console.log(`${free.length} unallocated chapters:\n`);
    let currentAp = "";
    for (const c of free) {
      if (c.ap !== currentAp) {
        currentAp = c.ap;
        console.log(`\n${currentAp}:`);
      }
      console.log(`  ${c.index}. ${c.chapter}`);
    }
    break;
  }
  case "allocate": {
    const query = args[0];
    const allocatedTo = args[1];
    if (!query || !allocatedTo) {
      console.log("Usage: ap allocate <chapter-query> <allocated-to>");
      process.exit(1);
    }
    const db = load();
    const matches = db.chapters.filter(c => 
      c.chapter.toLowerCase().includes(query.toLowerCase())
    );
    if (matches.length === 0) {
      console.log("No matches found");
    } else if (matches.length > 1) {
      console.log("Multiple matches, be more specific:");
      matches.forEach(c => console.log(`  ${c.ap}: ${c.chapter}`));
    } else {
      matches[0].allocatedTo = allocatedTo;
      save(db);
      console.log(`Allocated: ${matches[0].ap} - ${matches[0].chapter} → ${allocatedTo}`);
    }
    break;
  }
  case "find": {
    const query = args[0]?.toLowerCase();
    if (!query) {
      console.log("Usage: ap find <query>");
      process.exit(1);
    }
    const queryParts = query.split("/");
    const db = load();
    const matches = db.chapters.filter(c => {
      const allocSegs = c.allocatedTo?.split("/") ?? [];
      return c.chapter.toLowerCase().includes(query) ||
        c.ap.toLowerCase().includes(query) ||
        c.themes.some(t => t.toLowerCase().includes(query)) ||
        queryParts.every(qp => allocSegs.some(seg => seg.includes(qp)));
    });
    for (const c of matches) {
      const status = c.allocatedTo ? `[${c.allocatedTo}]` : "[free]";
      console.log(`${status} ${c.ap} #${c.index}: ${c.chapter}`);
    }
    break;
  }
  case "theme": {
    const query = args[0];
    const theme = args[1];
    if (!query || !theme) {
      console.log("Usage: ap theme <chapter-query> <theme>");
      process.exit(1);
    }
    const db = load();
    const matches = db.chapters.filter(c =>
      c.chapter.toLowerCase().includes(query.toLowerCase())
    );
    if (matches.length !== 1) {
      console.log(matches.length === 0 ? "No matches" : "Multiple matches, be more specific");
      matches.forEach(c => console.log(`  ${c.ap}: ${c.chapter}`));
    } else {
      if (!matches[0].themes.includes(theme)) {
        matches[0].themes.push(theme);
        save(db);
      }
      console.log(`${matches[0].chapter} themes: ${matches[0].themes.join(", ")}`);
    }
    break;
  }
  case "stats": {
    const db = load();
    const free = db.chapters.filter(c => !c.allocatedTo).length;
    const allocated = db.chapters.length - free;
    console.log(`Total: ${db.chapters.length}`);
    console.log(`Free: ${free}`);
    console.log(`Allocated: ${allocated}`);
    console.log(`Meaty APs: ${db.meaty.length}`);
    break;
  }
  case "aps": {
    const db = load();
    const aps = [...new Set(db.chapters.map(c => c.ap))];
    for (const ap of aps) {
      const chapters = db.chapters.filter(c => c.ap === ap);
      const allocated = chapters.filter(c => c.allocatedTo);
      const targets = [...new Set(allocated.map(c => c.allocatedTo).filter(Boolean))];
      const summary = targets.length ? ` → [${targets.join(", ")}]` : "";
      console.log(`${ap} [${allocated.length}/${chapters.length}]${summary}`);
    }
    break;
  }
  case "show": {
    const query = args[0];
    if (!query) {
      console.log("Usage: ap show <ap-name>");
      process.exit(1);
    }
    const db = load();
    const aps = [...new Set(db.chapters.map(c => c.ap))];
    const matches = aps.filter(ap => ap.toLowerCase().includes(query.toLowerCase()));
    if (matches.length === 0) {
      console.log("No AP found");
    } else if (matches.length > 1) {
      console.log("Multiple matches:");
      matches.forEach(ap => console.log(`  ${ap}`));
    } else {
      const apName = matches[0];
      const filename = apName.toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/_+$/, "") + ".md";
      const path = `./docs/ap/${filename}`;
      const chapters = db.chapters.filter(c => c.ap === apName);
      if (existsSync(path)) {
        let content = readFileSync(path, "utf-8");
        for (const c of chapters) {
          const status = c.allocatedTo ? ` [${c.allocatedTo}]` : " [free]";
          content = content.replace(`### ${c.chapter}`, `### ${c.chapter}${status}`);
        }
        console.log(content);
      } else {
        console.log(`No doc found at ${path}`);
      }
    }
    break;
  }
  default:
    console.log("Commands: list [ap], free, allocate <query> <to>, find <query>, theme <query> <theme>, stats, aps, show <ap>");
}
