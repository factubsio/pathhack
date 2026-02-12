interface SpellSlot {
  level: number;
  current: number;
  max: number;
  effectiveMax: number;
  ticks: number;
  regenRate: number;
}

interface PlayerState {
  hp: number;
  maxHp: number;
  tempHp: number;
  ac: number;
  cl: number;
  xp: number;
  gold: number;
  str: number; dex: number; con: number;
  int: number; wis: number; cha: number;
  hunger: string;
  buffs: string[];
  spellSlots: SpellSlot[];
}

interface Frame {
  round: number;
  width: number;
  height: number;
  chars: number[][];
  colors: number[][];
  tips: number[][];
  tipTable: string[];
  messages: string[];
  player: PlayerState;
}

interface Discovery {
  appearance: string;
  real: string;
}

interface MonsterEntry {
  cr: number;
  size: string;
  type: string;
  ac: number;
  hp: number;
  speed: number;
  attacks: string[];
  naturalAttacks: string[];
  abilities: string[];
  passives: string[];
}

interface ItemEntry {
  type: string;
  weight: number;
  material?: string;
  description?: string;
  effects?: string[];
  damage?: string;
  damageType?: string;
  hands?: number;
  group?: string;
  ac?: number;
  dexCap?: number;
  proficiency?: string;
}

interface DumpData {
  player: string;
  class: string;
  deity: string;
  ancestry: string;
  level: number;
  branch: string;
  depth: number;
  inventory: string[];
  abilities: string[];
  feats: string[];
  discoveries: Discovery[];
  vanquished: Record<string, number>;
  monsters: Record<string, MonsterEntry>;
  items: Record<string, ItemEntry>;
  frames: Frame[];
}

async function load(): Promise<DumpData> {
  const resp = await fetch("/dump.json");
  return resp.json();
}

const WALL = 1; // SOH, marker for walls
const DOOR_CLOSED = "+".charCodeAt(0);

function isWallLike(ch: number): boolean {
  return ch === WALL || ch === DOOR_CLOSED;
}

// bitmask: N=1 E=2 S=4 W=8
const BOX: Record<number, string> = {
  0b0000: "─", // isolated
  0b0001: "│", // N
  0b0010: "─", // E
  0b0011: "└", // N+E
  0b0100: "│", // S
  0b0101: "│", // N+S
  0b0110: "┌", // S+E
  0b0111: "├", // N+S+E
  0b1000: "─", // W
  0b1001: "┘", // N+W
  0b1010: "─", // E+W
  0b1011: "┴", // N+E+W
  0b1100: "┐", // S+W
  0b1101: "┤", // N+S+W
  0b1110: "┬", // S+E+W
  0b1111: "┼", // all
};

function wallChar(frame: Frame, x: number, y: number): string {
  const c = frame.chars;
  let mask = 0;
  if (y > 0 && isWallLike(c[y - 1][x])) mask |= 1;
  if (x < frame.width - 1 && isWallLike(c[y][x + 1])) mask |= 2;
  if (y < frame.height - 1 && isWallLike(c[y + 1][x])) mask |= 4;
  if (x > 0 && isWallLike(c[y][x - 1])) mask |= 8;
  return BOX[mask];
}

function monsterTip(name: string, m: MonsterEntry): HTMLElement {
  const el = document.createElement("div");
  el.className = "tooltiptext";
  const line = (text: string, cls?: string) => {
    const d = document.createElement("div");
    d.textContent = text;
    if (cls) d.className = cls;
    el.appendChild(d);
  };
  line(name, "tip-title");
  line(`CR${m.cr} ${m.size} ${m.type}`, "tip-sub");
  line(`AC ${m.ac}  HP ${m.hp}/lvl  Spd ${m.speed}`);
  for (const a of m.attacks) line(`⚔ ${a}`);
  for (const a of m.naturalAttacks) line(`🦷 ${a}`);
  for (const a of m.abilities) line(`★ ${a}`);
  for (const p of m.passives) line(`• ${p}`);
  return el;
}

function itemTip(name: string, item: ItemEntry): HTMLElement {
  const el = document.createElement("div");
  el.className = "tooltiptext";
  const line = (text: string, cls?: string) => {
    const d = document.createElement("div");
    d.textContent = text;
    if (cls) d.className = cls;
    el.appendChild(d);
  };
  line(name, "tip-title");
  if (item.damage) line(`${item.damage} ${item.damageType} · ${item.hands}h · ${item.group ?? ""}`);
  if (item.ac != null) line(`AC +${item.ac}${item.dexCap != null ? ` (dex cap ${item.dexCap})` : ""}`);
  if (item.material) line(`${item.material} · wt ${item.weight}`);
  if (item.description) line(item.description, "tip-desc");
  for (const e of item.effects ?? []) line(`• ${e}`);
  return el;
}

function makeTip(name: string, data: DumpData): HTMLElement | null {
  const mon = data.monsters[name];
  if (mon) return monsterTip(name, mon);
  const item = data.items[name];
  if (item) return itemTip(name, item);
  const el = document.createElement("div");
  el.className = "tooltiptext";
  el.textContent = name;
  return el;
}

function renderFrame(frame: Frame, data: DumpData): void {
  const map = document.getElementById("map")!;
  map.innerHTML = "";

  for (let y = 0; y < frame.height; y++) {
    const row = document.createElement("div");
    row.className = "row";

    for (let x = 0; x < frame.width; x++) {
      const raw = frame.chars[y][x];
      const ch = raw === WALL ? wallChar(frame, x, y) : String.fromCharCode(raw);
      const color = frame.colors[y][x];
      const tipIdx = frame.tips[y][x];

      if (tipIdx > 0) {
        const name = frame.tipTable[tipIdx - 1];
        const span = document.createElement("span");
        span.className = `tip c${color}`;
        span.textContent = ch;
        const tip = makeTip(name, data);
        if (tip) span.appendChild(tip);
        row.appendChild(span);
      } else if (color !== 7) { // 7 = Gray, default
        const span = document.createElement("span");
        span.className = `c${color}`;
        span.textContent = ch;
        row.appendChild(span);
      } else {
        row.appendChild(document.createTextNode(ch));
      }
    }
    map.appendChild(row);
  }

  const messages = document.getElementById("messages")!;
  messages.innerHTML = "";
  for (const msg of frame.messages) {
    const div = document.createElement("div");
    div.textContent = msg;
    messages.appendChild(div);
  }
}

function renderHeader(data: DumpData): void {
  const header = document.getElementById("header")!;
  header.textContent = `${data.player} — ${data.ancestry} ${data.class} of ${data.deity} — ${data.branch}:${data.depth}`;
}

function renderPlayerState(frame: Frame): void {
  const el = document.getElementById("player-state")!;
  const p = frame.player;
  const lines: string[] = [];

  const hpStr = p.tempHp > 0 ? `HP: ${p.hp}+${p.tempHp}/${p.maxHp}` : `HP: ${p.hp}/${p.maxHp}`;
  lines.push(`${hpStr}  AC: ${p.ac}  CL: ${p.cl}  XP: ${p.xp}  Gold: ${p.gold}`);
  lines.push(`Str:${p.str} Dex:${p.dex} Con:${p.con} Int:${p.int} Wis:${p.wis} Cha:${p.cha}`);
  if (p.hunger !== "Normal") lines.push(p.hunger);
  if (p.buffs.length > 0) lines.push(`Buffs: ${p.buffs.join(", ")}`);

  for (const slot of p.spellSlots) {
    const pips = "●".repeat(slot.current) + "○".repeat(slot.effectiveMax - slot.current);
    lines.push(`L${slot.level}: ${pips}`);
  }

  el.innerHTML = lines.map(l => `<div>${l}</div>`).join("");
}

function renderSections(data: DumpData, frame: Frame): void {
  const el = document.getElementById("sections")!;
  el.innerHTML = "";

  const sections: [string, string[]][] = [
    ["Inventory", data.inventory],
    ["Abilities", data.abilities],
    ["Feats", data.feats],
    ["Discoveries", data.discoveries.map(d => `${d.appearance} → ${d.real}`)],
    ["Vanquished", Object.entries(data.vanquished).sort((a, b) => b[1] - a[1]).map(([n, c]) => c > 1 ? `${c} ${n}` : n)],
  ];

  for (const [title, items] of sections) {
    if (items.length === 0) continue;
    const h = document.createElement("h3");
    h.textContent = title;
    el.appendChild(h);
    for (const item of items) {
      const div = document.createElement("div");
      div.textContent = item;
      el.appendChild(div);
    }
  }
}

async function main(): Promise<void> {
  const data = await load();
  renderHeader(data);
  const frame = data.frames[0];
  renderFrame(frame, data);
  renderPlayerState(frame);
  renderSections(data, frame);
}

main();
