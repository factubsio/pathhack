// Dungeon overview — global ordering prototype
// Run: bun tools/dungeon-view.ts

interface BranchLevel {
  visited?: boolean;
  branchTo?: string; // branchId
  label?: string; // e.g. "shop", "goblin nest"
}

interface Branch {
  id: string;
  name: string;
  entryDepthInSelf: number;
  levels: BranchLevel[];
  discovered: boolean;
  parentId?: string;
}

const branches: Record<string, Branch> = {
  dungeon: {
    id: "dungeon", name: "Dungeon", entryDepthInSelf: 1,
    levels: [
      { visited: true },                          // 1
      { visited: true },                          // 2
      { visited: true, branchTo: "meaty1" },      // 3
      { visited: true },                          // 4
      { visited: true },                          // 5
      { visited: true, branchTo: "mini1" },       // 6
      { visited: true },                          // 7
      { visited: true },                          // 8
      { visited: true, branchTo: "quest" },       // 9
      {},                                          // 10
      {},                                          // 11
      { branchTo: "meaty2" },                     // 12
      {},                                          // 13
      { branchTo: "mini2" },                      // 14
      { branchTo: "meaty3" },                     // 15
      {}, {}, {}, {}, {}, {}, {},                  // 16-22
    ],
    discovered: true,
  },
  meaty1: {
    id: "meaty1", name: "Carrion Hill", entryDepthInSelf: 2, parentId: "dungeon",
    levels: [
      { visited: true, branchTo: "cryptx" },      // 1
      { visited: true },                          // 2
      { visited: true },                          // 3
      {},                                          // 4
      {},                                          // 5
      {},                                          // 6
      {},                                          // 7
    ],
    discovered: true,
  },
  cryptx: {
    id: "cryptx", name: "Crypt of Echoes", entryDepthInSelf: 1, parentId: "meaty1",
    levels: [{ visited: true }, {}, {}],
    discovered: true,
  },
  mini1: {
    id: "mini1", name: "Scarwall", entryDepthInSelf: 3, parentId: "dungeon",
    levels: [
      { branchTo: "scar_tower" },                 // 1
      {},                                          // 2
      { visited: true },                           // 3 — entry, only visited level
      {},                                          // 4
      { branchTo: "scar_depths" },                // 5
    ],
    discovered: true,
  },
  scar_tower: {
    id: "scar_tower", name: "Scarwall Tower", entryDepthInSelf: 1, parentId: "mini1",
    levels: [{}, {}],
    discovered: true,  // known but not visited
  },
  scar_depths: {
    id: "scar_depths", name: "Scarwall Depths", entryDepthInSelf: 1, parentId: "mini1",
    levels: [{}, {}, {}, { branchTo: "the_hole" }, {}],
    discovered: true,  // known but not visited
  },
  the_hole: {
    id: "the_hole", name: "THE HOLE", entryDepthInSelf: 1, parentId: "scar_depths",
    levels: [{}, {}, {}],
    discovered: true,
  },
  quest: {
    id: "quest", name: "Sarenrae's Trial", entryDepthInSelf: 2, parentId: "dungeon",
    levels: [
      { branchTo: "dawn_crypt" },                 // 1 — not visited
      { visited: true },                          // 2 — entry
      { visited: true, branchTo: "suntemple" },   // 3
      {},                                          // 4
      {},                                          // 5
    ],
    discovered: true,
  },
  suntemple: {
    id: "suntemple", name: "Sun Temple", entryDepthInSelf: 1, parentId: "quest",
    levels: [{}, {}, {}],
    discovered: true,
  },
  dawn_crypt: {
    id: "dawn_crypt", name: "Dawn Crypt", entryDepthInSelf: 1, parentId: "quest",
    levels: [{}, {}],
    discovered: true,
  },
  meaty2: {
    id: "meaty2", name: "Gallowspire", entryDepthInSelf: 1, parentId: "dungeon",
    levels: [{}, {}, {}, {}, {}, {}, {}],
    discovered: false,
  },
  mini2: {
    id: "mini2", name: "The Pit", entryDepthInSelf: 1, parentId: "dungeon",
    levels: [{}],
    discovered: false,
  },
  meaty3: {
    id: "meaty3", name: "Viperwall", entryDepthInSelf: 1, parentId: "dungeon",
    levels: [{}, {}, {}, {}, {}, {}, {}],
    discovered: false,
  },
  meaty3: {
    id: "meaty3", name: "Viperwall", maxDepth: 7, entryDepthInSelf: 1,
    children: [],
    discovered: false,
  },
};

interface OrderEntry {
  key: number[];
  branchId: string;
  name: string;
  entryDepth: number; // the parent's entryDepthInSelf — where "we" sit in the parent
}

// Process a branch recursively: emit itself at its entry depth, recurse into children
function processBranch(branchId: string, parentKey: number[]): OrderEntry[] {
  const b = branches[branchId];
  if (!b.discovered) return [];

  const result: OrderEntry[] = [];

  // This branch sits at its own entry depth
  result.push({ key: [...parentKey, b.entryDepthInSelf], branchId: b.id, name: b.name, entryDepth: b.entryDepthInSelf });

  // Recurse into children derived from levels
  for (let i = 0; i < b.levels.length; i++) {
    const lvl = b.levels[i];
    if (lvl.branchTo && branches[lvl.branchTo]?.discovered) {
      const atDepth = i + 1; // 1-based
      result.push(...processBranch(lvl.branchTo, [...parentKey, atDepth]));
    }
  }

  return result;
}

// Dungeon is just a branch entered from "outside"
const entries = processBranch("dungeon", []);

// Sort lexicographically
entries.sort((a, b) => {
  for (let i = 0; i < Math.max(a.key.length, b.key.length); i++) {
    const ak = a.key[i] ?? -1;
    const bk = b.key[i] ?? -1;
    if (ak !== bk) return ak - bk;
  }
  return 0;
});

const lines: string[] = [];

for (let i = 0; i < entries.length; i++) {
  const prev = entries[i - 1] ?? null;
  const curr = entries[i];
  const next = entries[i + 1] ?? null;

  // Build padding with │ for active parent levels
  let pad = "";
  for (let d = 1; d < curr.key.length - 1; d++) {
    const prefix = curr.key.slice(0, d - 1);
    const hasMore = entries.slice(i + 1).some(e =>
      e.key.length >= d + 1 && prefix.every((v, k) => v === e.key[k])
    );
    pad += hasMore ? "│   " : "    ";
  }

  // Scan forward for a sibling: same length, same prefix
  const hasSibling = () => {
    const prefix = curr.key.slice(0, -1);
    for (let j = i + 1; j < entries.length; j++) {
      const ok = entries[j].key;
      if (ok.length === curr.key.length && prefix.every((v, k) => v === ok[k])) return true;
    }
    return false;
  };

  if (prev == null) {
    lines.push(`${pad}\x1b[1m${curr.name}\x1b[0m`);
  } else if (hasSibling()) {
    lines.push(`${pad}├─ ${curr.name}`);
  } else if (next != null && curr.key.length > next.key.length && curr.key[next.key.length - 1] < next.key[next.key.length - 1]) {
    lines.push(`${pad}┌─ ${curr.name}`);
  } else {
    lines.push(`${pad}└─ ${curr.name}`);
  }
}

// Post-process: upgrade corners to T-junctions if a vertical char is directly below
const verticals = new Set(["│", "├", "┌", "└"]);
for (let i = 0; i < lines.length - 1; i++) {
  const chars = [...lines[i]];
  const below = [...lines[i + 1]];
  let changed = false;
  for (let c = 0; c < chars.length && c < below.length; c++) {
    if ((chars[c] === "┌" || chars[c] === "└") && verticals.has(below[c])) {
      chars[c] = "├";
      changed = true;
    }
  }
  if (changed) lines[i] = chars.join("");
}

for (const line of lines) console.log(line);

// ── Branch Slice View ──

const SLICE_WIDTH = 24;
const LEFT_PAD = 20;

function renderSlice(label: string, dim: boolean, leftAnnot?: string, rightAnnot?: string): string[] {
  const inner = SLICE_WIDTH - 2;
  const lp = " ".repeat(LEFT_PAD);
  const top = `${lp}  ╱${"─".repeat(inner)}╱`;
  const mid = `${lp} ╱ ${label.padEnd(inner - 1)}╱`;
  const bot = `${lp}╱${"─".repeat(inner)}╱`;

  const color = dim ? "\x1b[90m" : "\x1b[0m";
  const reset = "\x1b[0m";

  const lines = [
    `${color}${top}${reset}`,
    `${color}${mid}${reset}`,
    `${color}${bot}${reset}`,
  ];

  // Overlay left annotation on the middle line (replace padding, keep ╱ aligned)
  if (leftAnnot) {
    const la = leftAnnot.padStart(LEFT_PAD - 1);
    lines[1] = `${color}${la}  ╱ ${label.padEnd(inner - 1)}╱${reset}`;
  }
  if (rightAnnot) {
    lines[1] += `  ${rightAnnot}`;
  }

  return lines;
}

interface SliceModel {
  label: string;
  dim: boolean;
  leftAnnot?: string;
  rightAnnot?: string;
}

function buildSliceModel(branchId: string): SliceModel[] {
  const b = branches[branchId];
  const parentName = b.parentId ? branches[b.parentId]?.name ?? "surface" : "surface";

  let firstVisited = -1, lastVisited = -1;
  for (let i = 0; i < b.levels.length; i++) {
    if (b.levels[i].visited) {
      if (firstVisited === -1) firstVisited = i;
      lastVisited = i;
    }
  }

  // No visited levels: just the entry level dimmed
  if (firstVisited === -1) {
    return [{
      label: `${b.entryDepthInSelf}`,
      dim: true,
      leftAnnot: `${parentName} ←`,
    }];
  }

  const model: SliceModel[] = [];

  // Phantom above
  if (firstVisited > 0) {
    model.push({ label: `${firstVisited} ?`, dim: true });
  }

  // Visited range
  for (let i = firstVisited; i <= lastVisited; i++) {
    const depth = i + 1;
    const lvl = b.levels[i];
    const visited = lvl.visited ?? false;
    const isEntry = depth === b.entryDepthInSelf;

    let label = `${depth}`;
    if (lvl.label) label += ` [${lvl.label}]`;

    model.push({
      label,
      dim: !visited,
      leftAnnot: isEntry ? `${parentName} ←` : undefined,
      rightAnnot: (visited && lvl.branchTo && branches[lvl.branchTo]?.discovered)
        ? `→ ${branches[lvl.branchTo].name}` : undefined,
    });
  }

  // Phantom below
  if (lastVisited < b.levels.length - 1) {
    model.push({ label: `${lastVisited + 2} ?`, dim: true });
  }

  return model;
}

function renderBranchSlice(branchId: string): string[] {
  const b = branches[branchId];
  const out: string[] = [];
  out.push(`\x1b[1m═══ ${b.name} ═══\x1b[0m`);
  out.push("");

  const model = buildSliceModel(branchId);
  for (let i = 0; i < model.length; i++) {
    const s = model[i];
    const sliceLines = renderSlice(s.label, s.dim, s.leftAnnot, s.rightAnnot);
    if (i > 0) sliceLines.shift();
    out.push(...sliceLines);
  }

  return out;
}

// ── Interactive Mode ──

function renderOverviewLines(cursor: number): string[] {
  return lines.map((line, i) => i === cursor ? `\x1b[7m${line}\x1b[0m` : line);
}

function stripAnsi(s: string): string {
  return s.replace(/\x1b\[[0-9;]*m/g, "");
}

function sideBySide(left: string[], right: string[], gap: number = 4): string[] {
  const leftWidth = 40;
  const height = Math.max(left.length, right.length);
  const result: string[] = [];
  const spacer = " ".repeat(gap);
  for (let i = 0; i < height; i++) {
    const l = left[i] ?? "";
    const visLen = stripAnsi(l).length;
    const padded = l + " ".repeat(Math.max(0, leftWidth - visLen));
    const r = right[i] ?? "";
    result.push(`${padded}${spacer}${r}`);
  }
  return result;
}

async function interactive() {
  let cursor = 0;
  process.stdin.setRawMode(true);
  process.stdout.write("\x1b[?25l"); // hide cursor

  const draw = () => {
    const selectedBranch = entries[cursor]?.branchId ?? "dungeon";
    const left = renderOverviewLines(cursor);
    const right = renderBranchSlice(selectedBranch);
    const combined = sideBySide(left, right);

    process.stdout.write("\x1b[H\x1b[2J"); // clear
    for (const line of combined) process.stdout.write(line + "\n");
    process.stdout.write("\n\x1b[90m(j/k navigate, q quit)\x1b[0m");
  };

  draw();

  for await (const chunk of Bun.stdin.stream()) {
    const key = new TextDecoder().decode(chunk);

    if (key === "q" || key === "\x03") break; // q or ctrl-c
    if (key === "j" || key === "\x1b[B") {
      cursor = Math.min(cursor + 1, entries.length - 1);
    } else if (key === "k" || key === "\x1b[A") {
      cursor = Math.max(cursor - 1, 0);
    }
    draw();
  }

  process.stdin.setRawMode(false);
  process.stdout.write("\x1b[?25h\x1b[0m"); // show cursor, reset
  process.exit(0);
}

interactive();
