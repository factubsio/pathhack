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
  vis: number[][];
  tips: number[][];
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
  tipTable: string[];
  frames: Frame[];
}

declare const DATA: DumpData | undefined;

async function load(): Promise<DumpData> {
  if (typeof DATA !== "undefined") return DATA;
  const resp = await fetch("/dump.json");
  return resp.json();
}

const WALL = 1; // SOH, marker for walls
const WALL_VIS = 4; // bit in vis array
const DOOR_CLOSED = "+".charCodeAt(0);

function isWallLike(frame: Frame, x: number, y: number): boolean {
  return (frame.vis[y][x] & WALL_VIS) !== 0;
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
  let mask = 0;
  if (y > 0 && isWallLike(frame, x, y - 1)) mask |= 1;
  if (x < frame.width - 1 && isWallLike(frame, x + 1, y)) mask |= 2;
  if (y < frame.height - 1 && isWallLike(frame, x, y + 1)) mask |= 4;
  if (x > 0 && isWallLike(frame, x - 1, y)) mask |= 8;
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
      const v = frame.vis[y][x];
      if (v === 0) { row.appendChild(document.createTextNode(" ")); continue; }

      const raw = frame.chars[y][x];
      const ch = raw === WALL ? wallChar(frame, x, y) : String.fromCharCode(raw);
      const color = frame.colors[y][x];
      const tipIdx = frame.tips[y][x];
      const dim = v === 1;

      if (tipIdx > 0) {
        const name = data.tipTable[tipIdx - 1];
        const span = document.createElement("span");
        span.className = `tip c${color}`;
        if (dim) span.classList.add("mem");
        span.textContent = ch;
        const tip = makeTip(name, data);
        if (tip) span.appendChild(tip);
        row.appendChild(span);
      } else if (color !== 7 || dim) {
        const span = document.createElement("span");
        span.className = `c${color}`;
        if (dim) span.classList.add("mem");
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
}

function renderMessages(data: DumpData, upToIdx: number): void {
  const el = document.getElementById("messages")!;
  el.innerHTML = "";
  for (let i = upToIdx; i >= 0; i--) {
    const msgs = data.frames[i].messages;
    for (let j = msgs.length - 1; j >= 0; j--) {
      const div = document.createElement("div");
      div.textContent = msgs[j];
      el.appendChild(div);
    }
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

  const slider = document.getElementById("slider") as HTMLInputElement;
  const label = document.getElementById("frame-label")!;
  const last = data.frames.length - 1;
  slider.max = String(last);

  // Read initial frame from hash
  const hash = parseInt(location.hash.slice(1));
  let current = (hash >= 0 && hash <= last) ? hash : last;
  slider.value = String(current);

  function show(idx: number): void {
    current = idx;
    const frame = data.frames[idx];
    label.textContent = `Round ${frame.round} (${idx + 1}/${data.frames.length})`;
    renderFrame(frame, data);
    renderPlayerState(frame);
    renderMessages(data, idx);
    history.replaceState(null, "", `#${idx}`);
  }

  const playBtn = document.getElementById("play")!;
  let playing = false;
  let timer: ReturnType<typeof setInterval> | null = null;

  function togglePlay(): void {
    playing = !playing;
    playBtn.textContent = playing ? "⏸" : "▶";
    if (playing) {
      if (current >= last) { current = 0; slider.value = "0"; show(0); }
      timer = setInterval(() => {
        if (current >= last) { togglePlay(); return; }
        current++;
        slider.value = String(current);
        show(current);
      }, 200);
    } else if (timer) {
      clearInterval(timer);
      timer = null;
    }
  }

  playBtn.addEventListener("click", togglePlay);
  slider.addEventListener("input", () => {
    if (playing) togglePlay();
    show(parseInt(slider.value));
  });
  show(current);
  renderSections(data, data.frames[last]);
}

main();
