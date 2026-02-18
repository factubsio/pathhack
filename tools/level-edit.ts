// --- Types ---

interface TileDef {
  char: string;
  name: string;
  fg: string;
  bg?: string;
  category: "tile" | "room" | "marker";
}

interface LevelEntry {
  file: string;
  varName: string;
  id: string;
  map: string;
}

// --- Tile Registry ---

const TILES: TileDef[] = [
  { char: " ", name: "Rock",     fg: "#333",    category: "tile" },
  { char: ".", name: "Floor",    fg: "#888",    category: "tile" },
  { char: ",", name: "Grass",    fg: "#4a4",    category: "tile" },
  { char: "±", name: "Tree",     fg: "#2a6",    category: "tile" },
  { char: "#", name: "Corridor", fg: "#666",    category: "tile" },
  { char: "-", name: "HWall",    fg: "#aaa",    category: "tile" },
  { char: "|", name: "VWall",    fg: "#aaa",    category: "tile" },
  { char: "+", name: "Door",     fg: "#a80",    category: "tile" },
  { char: "<", name: "Up",       fg: "#ccc",    category: "tile" },
  { char: ">", name: "Down",     fg: "#ccc",    category: "tile" },
  { char: "~", name: "Water",    fg: "#44f",    category: "tile" },
];

const ROOM_TILES: TileDef[] = Array.from({ length: 10 }, (_, i) => ({
  char: String(i),
  name: `Room ${i}`,
  fg: `hsl(${i * 36}, 70%, 60%)`,
  category: "room" as const,
}));

const MARKER_TILES: TileDef[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ_^S".split("").map(c => ({
  char: c,
  name: `Mark ${c}`,
  fg: "#ff0",
  category: "marker" as const,
}));

const ALL_TILES = [...TILES, ...ROOM_TILES, ...MARKER_TILES];
const TILE_MAP = new Map(ALL_TILES.map(t => [t.char, t]));

function tileFor(ch: string): TileDef {
  return TILE_MAP.get(ch) ?? { char: ch, name: ch, fg: "#f0f", category: "tile" };
}

// --- Display chars ---

const DISPLAY_CHARS: Record<string, string> = {
  " ": " ", ".": "·", ",": ",", "±": "±", "#": "#",
  "-": "─", "|": "│", "+": "+", "<": "<", ">": ">", "~": "≈",
};

function displayChar(ch: string): string {
  return DISPLAY_CHARS[ch] ?? ch;
}

// --- State ---

const W = 80, H = 21;
const CELL_W = 12, CELL_H = 20;

let grid: string[][] = [];
let levels: LevelEntry[] = [];
let currentId = "";
let slotA: TileDef = TILES[1]; // Floor
let slotB: TileDef = TILES[0]; // Rock
let activeSlot: "a" | "b" = "a";
let dirty = false;
let brushSize = 1;
let hoverCell: [number, number] | null = null;
let showGrid = true;

// Circle brush masks — [dx, dy] offsets per brush size, derived from FovCalculator
// Size 1 = single cell, 3 = r1, 5 = r2, 7 = r3, 9 = r4
const BRUSH_MASKS: Record<number, [number, number][]> = (() => {
  const masks: Record<number, [number, number][]> = {};
  // radius → column extents per row (from CircleData)
  const circles: [number, number[]][] = [
    [0, [0]],           // size 1
    [1, [1, 1]],        // size 3: row 0 = ±1, row 1 = ±1
    [2, [2, 2, 1]],     // size 5
    [3, [3, 3, 2, 1]],  // size 7
    [4, [4, 4, 4, 3, 2]], // size 9
  ];
  for (const [r, extents] of circles) {
    const size = r * 2 + 1;
    const offsets: [number, number][] = [];
    for (let row = 0; row < extents.length; row++) {
      const ext = extents[row];
      for (let col = -ext; col <= ext; col++) {
        offsets.push([col, row]);
        if (row > 0) offsets.push([col, -row]);
      }
    }
    masks[size] = offsets;
  }
  return masks;
})();

function newGrid(): string[][] {
  return Array.from({ length: H }, () => Array(W).fill(" "));
}

function cloneGrid(g: string[][]): string[][] {
  return g.map(r => [...r]);
}

// --- Undo ---

const undoStack: string[][][] = [];
const UNDO_MAX = 50;

function pushUndo() {
  undoStack.push(cloneGrid(grid));
  if (undoStack.length > UNDO_MAX) undoStack.shift();
}

function undo() {
  const prev = undoStack.pop();
  if (prev) { grid = prev; dirty = true; render(); }
}

// --- Canvas ---

const canvas = document.getElementById("canvas") as HTMLCanvasElement;
const ctx = canvas.getContext("2d")!;
canvas.width = W * CELL_W;
canvas.height = H * CELL_H;

function render() {
  const src = activeTool.preview ?? grid;
  ctx.fillStyle = "#111";
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.font = `${CELL_H - 2}px Consolas, Monaco, monospace`;
  ctx.textBaseline = "middle";
  ctx.textAlign = "center";

  for (let y = 0; y < H; y++) {
    for (let x = 0; x < W; x++) {
      const ch = src[y]?.[x] ?? " ";
      const td = tileFor(ch);
      const px = x * CELL_W;
      const py = y * CELL_H;
      if (td.bg) { ctx.fillStyle = td.bg; ctx.fillRect(px, py, CELL_W, CELL_H); }
      ctx.fillStyle = td.fg;
      ctx.fillText(displayChar(ch), px + CELL_W / 2, py + CELL_H / 2);
    }
  }

  if (showGrid) {
    ctx.strokeStyle = "rgba(255,255,255,0.04)";
    ctx.lineWidth = 1;
    for (let x = 0; x <= W; x++) { ctx.beginPath(); ctx.moveTo(x * CELL_W, 0); ctx.lineTo(x * CELL_W, H * CELL_H); ctx.stroke(); }
    for (let y = 0; y <= H; y++) { ctx.beginPath(); ctx.moveTo(0, y * CELL_H); ctx.lineTo(W * CELL_W, y * CELL_H); ctx.stroke(); }
  }

  activeTool.renderOverlay(ctx);
}

// --- Grid ↔ Map string ---

function gridToMap(): string {
  return grid.map(row => row.join("")).join("\n");
}

function mapToGrid(map: string): string[][] {
  const g = newGrid();
  const lines = map.split("\n");
  for (let y = 0; y < Math.min(lines.length, H); y++)
    for (let x = 0; x < Math.min(lines[y].length, W); x++)
      g[y][x] = lines[y][x];
  return g;
}

// --- Helpers ---

function activeTile(button: number): TileDef {
  return button === 2 ? slotB : slotA;
}

function cellAt(e: MouseEvent): [number, number] | null {
  const rect = canvas.getBoundingClientRect();
  const sx = canvas.width / rect.width;
  const sy = canvas.height / rect.height;
  const x = Math.floor((e.clientX - rect.left) * sx / CELL_W);
  const y = Math.floor((e.clientY - rect.top) * sy / CELL_H);
  if (x < 0 || x >= W || y < 0 || y >= H) return null;
  return [x, y];
}

function paintBrush(x: number, y: number, ch: string, target: string[][] = grid) {
  for (const [dx, dy] of BRUSH_MASKS[brushSize]) {
    const px = x + dx, py = y + dy;
    if (px >= 0 && px < W && py >= 0 && py < H) target[py][px] = ch;
  }
}

function floodFill(sx: number, sy: number, ch: string) {
  const target = grid[sy][sx];
  if (target === ch) return;
  const stack: [number, number][] = [[sx, sy]];
  const visited = new Set<string>();
  while (stack.length > 0) {
    const [x, y] = stack.pop()!;
    const key = `${x},${y}`;
    if (visited.has(key)) continue;
    visited.add(key);
    if (x < 0 || x >= W || y < 0 || y >= H) continue;
    if (grid[y][x] !== target) continue;
    grid[y][x] = ch;
    stack.push([x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]);
  }
}

function drawRect(x0: number, y0: number, x1: number, y1: number, ch: string, filled: boolean, target: string[][]) {
  const minX = Math.max(0, Math.min(x0, x1)), maxX = Math.min(W - 1, Math.max(x0, x1));
  const minY = Math.max(0, Math.min(y0, y1)), maxY = Math.min(H - 1, Math.max(y0, y1));
  for (let y = minY; y <= maxY; y++)
    for (let x = minX; x <= maxX; x++)
      if (filled || y === minY || y === maxY || x === minX || x === maxX)
        target[y][x] = ch;
}

function drawOval(x0: number, y0: number, x1: number, y1: number, ch: string, filled: boolean, target: string[][]) {
  const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
  const rx = Math.abs(x1 - x0) / 2, ry = Math.abs(y1 - y0) / 2;
  if (rx < 0.5 || ry < 0.5) return;
  for (let y = Math.max(0, Math.min(y0, y1)); y <= Math.min(H - 1, Math.max(y0, y1)); y++)
    for (let x = Math.max(0, Math.min(x0, x1)); x <= Math.min(W - 1, Math.max(x0, x1)); x++) {
      const dx = (x - cx) / rx, dy = (y - cy) / ry;
      const d = dx * dx + dy * dy;
      if (filled) {
        if (d <= 1.0) target[y][x] = ch;
      } else if (d <= 1.0) {
        const border = [(x-1-cx)/rx, (x+1-cx)/rx].some(ddx => ddx*ddx + dy*dy > 1.0)
          || [(y-1-cy)/ry, (y+1-cy)/ry].some(ddy => dx*dx + ddy*ddy > 1.0);
        if (border) target[y][x] = ch;
      }
    }
}

// --- Tool System ---

abstract class EditorTool {
  abstract readonly name: string;
  abstract readonly key: string;
  preview: string[][] | null = null;

  onDown(_x: number, _y: number, _button: number): void {}
  onDrag(_x: number, _y: number, _button: number): void {}
  onUp(_x: number, _y: number, _button: number): void {}
  renderOverlay(_ctx: CanvasRenderingContext2D): void {}

  protected brushPreview(ctx: CanvasRenderingContext2D) {
    if (!hoverCell) return;
    const [hx, hy] = hoverCell;
    const td = activeSlot === "a" ? slotA : slotB;
    ctx.globalAlpha = 0.4;
    for (const [dx, dy] of BRUSH_MASKS[brushSize]) {
      const px = hx + dx, py = hy + dy;
      if (px >= 0 && px < W && py >= 0 && py < H) {
        ctx.fillStyle = "#111";
        ctx.fillRect(px * CELL_W, py * CELL_H, CELL_W, CELL_H);
        ctx.fillStyle = td.fg;
        ctx.fillText(displayChar(td.char), px * CELL_W + CELL_W / 2, py * CELL_H + CELL_H / 2);
      }
    }
    ctx.globalAlpha = 1.0;
  }
}

class BrushTool extends EditorTool {
  readonly name = "Brush";
  readonly key = "brush";

  onDown(x: number, y: number, button: number) {
    pushUndo();
    paintBrush(x, y, activeTile(button).char);
    dirty = true;
  }
  onDrag(x: number, y: number, button: number) {
    paintBrush(x, y, activeTile(button).char);
    dirty = true;
  }
  renderOverlay(ctx: CanvasRenderingContext2D) { this.brushPreview(ctx); }
}

class EraserTool extends EditorTool {
  readonly name = "Eraser";
  readonly key = "eraser";

  onDown(x: number, y: number) {
    pushUndo();
    paintBrush(x, y, " ");
    dirty = true;
  }
  onDrag(x: number, y: number) {
    paintBrush(x, y, " ");
    dirty = true;
  }
  renderOverlay(ctx: CanvasRenderingContext2D) {
    if (!hoverCell) return;
    const [hx, hy] = hoverCell;
    ctx.globalAlpha = 0.4;
    for (const [dx, dy] of BRUSH_MASKS[brushSize]) {
      const px = hx + dx, py = hy + dy;
      if (px >= 0 && px < W && py >= 0 && py < H) {
        ctx.fillStyle = "#111";
        ctx.fillRect(px * CELL_W, py * CELL_H, CELL_W, CELL_H);
      }
    }
    ctx.globalAlpha = 1.0;
  }
}

class FloodTool extends EditorTool {
  readonly name = "Flood";
  readonly key = "flood";

  onDown(x: number, y: number, button: number) {
    pushUndo();
    floodFill(x, y, activeTile(button).char);
    dirty = true;
  }
}

abstract class DragShapeTool extends EditorTool {
  private dragStart: [number, number] | null = null;

  abstract drawShape(x0: number, y0: number, x1: number, y1: number, ch: string, target: string[][]): void;

  onDown(x: number, y: number) {
    pushUndo();
    this.dragStart = [x, y];
    this.preview = cloneGrid(grid);
  }
  onDrag(x: number, y: number, button: number) {
    if (!this.dragStart) return;
    this.preview = cloneGrid(grid);
    this.drawShape(this.dragStart[0], this.dragStart[1], x, y, activeTile(button).char, this.preview);
  }
  onUp(x: number, y: number, button: number) {
    if (!this.dragStart) return;
    this.drawShape(this.dragStart[0], this.dragStart[1], x, y, activeTile(button).char, grid);
    dirty = true;
    this.dragStart = null;
    this.preview = null;
  }
}

class RectTool extends DragShapeTool {
  readonly name = "Rect";
  readonly key = "rect";
  drawShape(x0: number, y0: number, x1: number, y1: number, ch: string, target: string[][]) {
    drawRect(x0, y0, x1, y1, ch, false, target);
  }
}

class FillRectTool extends DragShapeTool {
  readonly name = "FillRect";
  readonly key = "fillrect";
  drawShape(x0: number, y0: number, x1: number, y1: number, ch: string, target: string[][]) {
    drawRect(x0, y0, x1, y1, ch, true, target);
  }
}

class OvalTool extends DragShapeTool {
  readonly name = "Oval";
  readonly key = "oval";
  drawShape(x0: number, y0: number, x1: number, y1: number, ch: string, target: string[][]) {
    drawOval(x0, y0, x1, y1, ch, false, target);
  }
}

class FillOvalTool extends DragShapeTool {
  readonly name = "FillOval";
  readonly key = "filloval";
  drawShape(x0: number, y0: number, x1: number, y1: number, ch: string, target: string[][]) {
    drawOval(x0, y0, x1, y1, ch, true, target);
  }
}

// --- Tool Registry ---

const tools: EditorTool[] = [
  new BrushTool(),
  new RectTool(),
  new FillRectTool(),
  new OvalTool(),
  new FillOvalTool(),
  new FloodTool(),
  new EraserTool(),
];

const toolMap = new Map(tools.map(t => [t.key, t]));
let activeTool: EditorTool = tools[0];

function setTool(key: string) {
  const t = toolMap.get(key);
  if (!t) return;
  activeTool = t;
  document.querySelectorAll<HTMLButtonElement>("#toolbar button[data-tool]").forEach(b => {
    b.classList.toggle("active", b.dataset.tool === key);
  });
}

// --- Mouse handlers ---

canvas.addEventListener("contextmenu", e => e.preventDefault());
canvas.addEventListener("mouseleave", () => { hoverCell = null; render(); });

let mouseDown = false;
let mouseButton = 0;

canvas.addEventListener("mousedown", e => {
  const cell = cellAt(e);
  if (!cell) return;
  mouseDown = true;
  mouseButton = e.button;
  activeTool.onDown(cell[0], cell[1], e.button);
  render();
});

canvas.addEventListener("mousemove", e => {
  const cell = cellAt(e);
  const info = document.getElementById("cursor-info")!;
  if (cell) {
    const [x, y] = cell;
    const ch = grid[y][x];
    const td = tileFor(ch);
    info.textContent = `${x},${y} [${ch}] ${td.name}`;
  } else {
    info.textContent = "";
  }

  const prevHover = hoverCell;
  hoverCell = cell;

  if (mouseDown && cell) {
    activeTool.onDrag(cell[0], cell[1], mouseButton);
  }

  if (hoverCell !== prevHover || mouseDown) render();
});

window.addEventListener("mouseup", e => {
  if (!mouseDown) return;
  mouseDown = false;
  const cell = cellAt(e);
  if (cell) activeTool.onUp(cell[0], cell[1], e.button);
  render();
});

// --- Keyboard ---

window.addEventListener("keydown", e => {
  const help = document.getElementById("help-overlay")!;
  if (help.style.display === "flex") { help.style.display = "none"; e.preventDefault(); return; }

  // don't intercept when typing in an input
  if (document.activeElement instanceof HTMLInputElement) return;

  if (e.key === "?") { help.style.display = "flex"; e.preventDefault(); return; }

  if (e.key === "x" || e.key === "X") { [slotA, slotB] = [slotB, slotA]; updateAB(); updatePalette(); e.preventDefault(); }
  if ((e.key === "z" || e.key === "Z") && (e.ctrlKey || e.metaKey)) { undo(); e.preventDefault(); }
  if ((e.key === "s" || e.key === "S") && (e.ctrlKey || e.metaKey)) { document.getElementById("btn-save")!.click(); e.preventDefault(); }

  const toolKeys: Record<string, string> = {
    "1": "brush", "2": "rect", "3": "fillrect", "4": "oval", "5": "filloval", "6": "flood", "7": "eraser",
    "b": "brush", "r": "rect", "f": "flood", "e": "eraser", "o": "oval",
  };
  if (toolKeys[e.key]) { setTool(toolKeys[e.key]); e.preventDefault(); }

  if (e.key === "[") { brushSize = Math.max(1, brushSize - 2); updateBrushLabel(); e.preventDefault(); }
  if (e.key === "]") { brushSize = Math.min(9, brushSize + 2); updateBrushLabel(); e.preventDefault(); }
  if (e.key === "g" || e.key === "G") { showGrid = !showGrid; render(); e.preventDefault(); }

  const quickTiles: Record<string, string> = { ".": ".", ",": ",", "#": "#", " ": " " };
  if (quickTiles[e.key] !== undefined) {
    const td = ALL_TILES.find(t => t.char === quickTiles[e.key]);
    if (td) { if (activeSlot === "a") slotA = td; else slotB = td; updateAB(); updatePalette(); }
    e.preventDefault();
  }
});

// --- Tool buttons ---

document.querySelectorAll<HTMLButtonElement>("#toolbar button[data-tool]").forEach(b => {
  b.addEventListener("click", () => setTool(b.dataset.tool!));
});

// --- A/B display ---

function updateAB() {
  const aBox = document.getElementById("ab-a")!;
  const bBox = document.getElementById("ab-b")!;
  aBox.textContent = displayChar(slotA.char);
  aBox.style.color = slotA.fg;
  bBox.textContent = displayChar(slotB.char);
  bBox.style.color = slotB.fg;
  aBox.classList.toggle("sel", activeSlot === "a");
  bBox.classList.toggle("sel", activeSlot === "b");
}

document.getElementById("ab-a")!.addEventListener("click", () => { activeSlot = "a"; updateAB(); });
document.getElementById("ab-b")!.addEventListener("click", () => { activeSlot = "b"; updateAB(); });

// --- Palette ---

function buildPalette() {
  const sidebar = document.getElementById("sidebar")!;
  sidebar.innerHTML = "";

  const sections: [string, TileDef[]][] = [
    ["Tiles", TILES],
    ["Rooms", ROOM_TILES],
    ["Markers", MARKER_TILES],
  ];

  for (const [title, items] of sections) {
    const h = document.createElement("h4");
    h.textContent = title;
    sidebar.appendChild(h);

    for (const td of items) {
      const div = document.createElement("div");
      div.className = "pal-item";
      div.innerHTML = `<span class="swatch" style="color:${td.fg}">${displayChar(td.char)}</span><span class="label">${td.name}</span>`;

      div.addEventListener("click", () => {
        if (activeSlot === "a") slotA = td; else slotB = td;
        if (td.category === "room" && !["rect", "fillrect", "oval", "filloval"].includes(activeTool.key))
          setTool("fillrect");
        updateAB(); updatePalette();
      });
      div.addEventListener("contextmenu", e => {
        e.preventDefault();
        if (activeSlot === "a") slotB = td; else slotA = td;
        updateAB(); updatePalette();
      });

      sidebar.appendChild(div);
    }
  }
  updatePalette();
}

function updatePalette() {
  document.querySelectorAll<HTMLDivElement>(".pal-item").forEach(div => {
    const swatch = div.querySelector(".swatch")!;
    const ch = swatch.textContent ?? "";
    const td = ALL_TILES.find(t => displayChar(t.char) === ch);
    div.classList.toggle("sel-a", td === slotA);
    div.classList.toggle("sel-b", td === slotB);
  });
}

// --- File operations ---

const levelInput = document.getElementById("level-input") as HTMLInputElement;
const levelDropdown = document.getElementById("level-dropdown")!;

function showDropdown(filter = "") {
  const f = filter.toLowerCase();
  const filtered = f ? levels.filter(l => l.id.toLowerCase().includes(f)) : levels;
  levelDropdown.innerHTML = filtered.map(l =>
    `<div class="dd-item${l.id === currentId ? " active" : ""}" data-id="${l.id}">${l.id}</div>`
  ).join("");
  levelDropdown.style.display = filtered.length ? "block" : "none";
  levelDropdown.querySelectorAll<HTMLDivElement>(".dd-item").forEach(div => {
    div.addEventListener("mousedown", e => {
      e.preventDefault(); // keep focus on input
      const id = div.dataset.id!;
      if (id === currentId) { hideDropdown(); return; }
      if (dirty && !confirm("Unsaved changes. Continue?")) return;
      loadLevel(id);
      hideDropdown();
    });
  });
}

function hideDropdown() { levelDropdown.style.display = "none"; }

levelInput.addEventListener("focus", () => showDropdown(levelInput.value));
levelInput.addEventListener("input", () => showDropdown(levelInput.value));
levelInput.addEventListener("blur", () => setTimeout(hideDropdown, 150));
levelInput.addEventListener("keydown", e => {
  if (e.key === "Escape") { hideDropdown(); levelInput.blur(); }
  if (e.key === "Enter") {
    const first = levelDropdown.querySelector<HTMLDivElement>(".dd-item");
    if (first) first.dispatchEvent(new MouseEvent("mousedown"));
  }
});

// close dropdown on outside click
document.addEventListener("mousedown", e => {
  if (!(e.target as HTMLElement).closest("#level-picker")) hideDropdown();
});

document.getElementById("btn-new")!.addEventListener("click", async () => {
  const resp = await fetch("/api/new", { method: "POST" });
  if (!resp.ok) { setStatus("Failed to create level"); return; }
  const { id } = await resp.json();
  await loadLevels();
  loadLevel(id);
  setStatus(`Created ${id}`);
});

async function loadLevels() {
  levels = await (await fetch("/api/levels")).json();
  if (levels.length > 0 && !currentId) loadLevel(levels[0].id);
}

function loadLevel(id: string) {
  const entry = levels.find(l => l.id === id);
  if (!entry) return;
  currentId = id;
  levelInput.value = id;
  grid = mapToGrid(entry.map);
  dirty = false;
  undoStack.length = 0;
  render();
  setStatus(`Loaded ${id}`);
}

document.getElementById("btn-save")!.addEventListener("click", async () => {
  const map = gridToMap();
  const resp = await fetch("/api/save", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ id: currentId, map }),
  });
  if (resp.ok) {
    dirty = false;
    setStatus(`Saved ${currentId}`);
    const entry = levels.find(l => l.id === currentId);
    if (entry) entry.map = map;
  } else {
    setStatus(`Save failed: ${resp.statusText}`);
  }
});

// --- Status ---

function setStatus(msg: string) { document.getElementById("status")!.textContent = msg; }
function updateBrushLabel() { const el = document.getElementById("brush-size"); if (el) el.textContent = `${brushSize}×${brushSize}`; }

// --- Init ---

grid = newGrid();
buildPalette();
updateAB();
render();
loadLevels();
