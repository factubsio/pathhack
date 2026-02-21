// --- Types ---

interface Cell {
  char: string;
  fg: string;
  bg: string;
}

// --- Constants ---

const W = 80, H = 21;
const CELL_W = 10, CELL_H = 20;

const PALETTE: { name: string; hex: string }[] = [
  { name: "Black",       hex: "#000000" },
  { name: "DarkRed",     hex: "#aa0000" },
  { name: "DarkGreen",   hex: "#00aa00" },
  { name: "DarkYellow",  hex: "#aa5500" },
  { name: "DarkBlue",    hex: "#0000aa" },
  { name: "DarkMagenta", hex: "#aa00aa" },
  { name: "DarkCyan",    hex: "#00aaaa" },
  { name: "Gray",        hex: "#aaaaaa" },
  { name: "DarkGray",    hex: "#555555" },
  { name: "Red",         hex: "#ff5555" },
  { name: "Green",       hex: "#55ff55" },
  { name: "Yellow",      hex: "#ffff55" },
  { name: "Blue",        hex: "#5555ff" },
  { name: "Magenta",     hex: "#ff55ff" },
  { name: "Cyan",        hex: "#55ffff" },
  { name: "White",       hex: "#ffffff" },
];

// --- State ---

let grid: Cell[][] = [];
let fgColor = PALETTE[15].hex; // white
let bgColor = PALETTE[0].hex;  // black
let brushChar = "*";
let viewW = W, viewH = H;
let brushSize = 1;
let autoLine = false;

// Circle brush masks — [dx, dy] offsets per brush size
const BRUSH_MASKS: Record<number, [number, number][]> = (() => {
  const masks: Record<number, [number, number][]> = {};
  const circles: [number, number[]][] = [
    [0, [0]], [1, [1, 1]], [2, [2, 2, 1]], [3, [3, 3, 2, 1]], [4, [4, 4, 4, 3, 2]],
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

function newGrid(): Cell[][] {
  return Array.from({ length: H }, () =>
    Array.from({ length: W }, () => ({ char: " ", fg: PALETTE[15].hex, bg: PALETTE[0].hex }))
  );
}

function cloneGrid(g: Cell[][]): Cell[][] {
  return g.map(r => r.map(c => ({ ...c })));
}

// --- Undo ---

const undoStack: Cell[][][] = [];
const UNDO_MAX = 50;

function pushUndo() {
  undoStack.push(cloneGrid(grid));
  if (undoStack.length > UNDO_MAX) undoStack.shift();
  scheduleHashSave();
}

function undo() {
  const prev = undoStack.pop();
  if (prev) { grid = prev; scheduleHashSave(); render(); }
}

// --- Canvas ---

const canvas = document.getElementById("canvas") as HTMLCanvasElement;
const ctx = canvas.getContext("2d")!;
canvas.width = W * CELL_W;
canvas.height = H * CELL_H;

function render() {
  ctx.fillStyle = "#111";
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.font = `${CELL_H - 2}px Consolas, Monaco, monospace`;
  ctx.textBaseline = "middle";
  ctx.textAlign = "center";

  for (let y = 0; y < H; y++) {
    for (let x = 0; x < W; x++) {
      const cell = grid[y][x];
      const px = x * CELL_W, py = y * CELL_H;
      if (cell.bg !== PALETTE[0].hex) {
        ctx.fillStyle = cell.bg;
        ctx.fillRect(px, py, CELL_W, CELL_H);
      }
      if (cell.char !== " ") {
        ctx.fillStyle = cell.fg;
        ctx.fillText(cell.char, px + CELL_W / 2, py + CELL_H / 2);
      }
    }
  }

  // grid lines
  ctx.strokeStyle = "rgba(255,255,255,0.04)";
  ctx.lineWidth = 1;
  for (let x = 0; x <= W; x++) { ctx.beginPath(); ctx.moveTo(x * CELL_W, 0); ctx.lineTo(x * CELL_W, H * CELL_H); ctx.stroke(); }
  for (let y = 0; y <= H; y++) { ctx.beginPath(); ctx.moveTo(0, y * CELL_H); ctx.lineTo(W * CELL_W, y * CELL_H); ctx.stroke(); }

  // dim outside working area
  if (viewW < W || viewH < H) {
    ctx.fillStyle = "rgba(0,0,0,0.6)";
    if (viewW < W) ctx.fillRect(viewW * CELL_W, 0, (W - viewW) * CELL_W, viewH * CELL_H);
    if (viewH < H) ctx.fillRect(0, viewH * CELL_H, W * CELL_W, (H - viewH) * CELL_H);
  }

  // tool overlay
  activeTool.renderOverlay(ctx);
}

function cellAt(e: MouseEvent): [number, number] | null {
  const rect = canvas.getBoundingClientRect();
  const sx = canvas.width / rect.width, sy = canvas.height / rect.height;
  const x = Math.floor((e.clientX - rect.left) * sx / CELL_W);
  const y = Math.floor((e.clientY - rect.top) * sy / CELL_H);
  if (x < 0 || x >= W || y < 0 || y >= H) return null;
  return [x, y];
}

function stamp(x: number, y: number) {
  for (const [dx, dy] of BRUSH_MASKS[brushSize]) {
    const px = x + dx, py = y + dy;
    if (px >= 0 && px < W && py >= 0 && py < H)
      grid[py][px] = { char: brushChar, fg: fgColor, bg: bgColor };
  }
}

function stampErase(x: number, y: number) {
  for (const [dx, dy] of BRUSH_MASKS[brushSize]) {
    const px = x + dx, py = y + dy;
    if (px >= 0 && px < W && py >= 0 && py < H)
      grid[py][px] = { char: " ", fg: DEFAULT_FG, bg: DEFAULT_BG };
  }
}

// --- Line rasterization (Bresenham) ---

function autoCharForLine(x: number, y: number, x0: number, y0: number, x1: number, y1: number): string {
  const dx = x1 - x0, dy = y1 - y0;
  if (dx === 0 && dy === 0) return brushChar;

  const adx = Math.abs(dx), ady = Math.abs(dy);

  // true diagonal
  if (adx === ady) return (dx > 0) === (dy > 0) ? "\\" : "/";

  if (adx >= ady) {
    // horizontal-ish: compute where line crosses this column (cell center = 0.5)
    const t = dx === 0 ? 0.5 : (x - x0) / dx;
    const trueY = (y0 + 0.5) + t * dy;
    const frac = trueY - y; // 0=top of cell, 1=bottom
    if (frac < 0.33) return "▔";
    if (frac > 0.67) return "▁";
    return "─";
  } else {
    const t = dy === 0 ? 0.5 : (y - y0) / dy;
    const trueX = (x0 + 0.5) + t * dx;
    const frac = trueX - x; // 0=left of cell, 1=right
    if (frac < 0.33) return "▏";
    if (frac > 0.67) return "▕";
    return "│";
  }
}

function stampCell(x: number, y: number, ch: string, target: Cell[][]) {
  if (x >= 0 && x < W && y >= 0 && y < H)
    target[y][x] = { char: ch, fg: fgColor, bg: bgColor };
}

function plotLine(x0: number, y0: number, x1: number, y1: number, target: Cell[][]) {
  let cx = x0, cy = y0;
  let dx = Math.abs(x1 - x0), dy = -Math.abs(y1 - y0);
  let sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
  let err = dx + dy;
  while (true) {
    const ch = autoLine ? autoCharForLine(cx, cy, x0, y0, x1, y1) : brushChar;
    stampCell(cx, cy, ch, target);
    if (cx === x1 && cy === y1) break;
    const e2 = 2 * err;
    if (e2 >= dy) { err += dy; cx += sx; }
    if (e2 <= dx) { err += dx; cy += sy; }
  }
}

// --- Quadratic bezier rasterization ---

function plotBezier(x0: number, y0: number, cx: number, cy: number, x1: number, y1: number, target: Cell[][]) {
  const steps = Math.max(60, (Math.abs(x1 - x0) + Math.abs(y1 - y0)) * 2);
  let prevX = -1, prevY = -1;
  for (let i = 0; i <= steps; i++) {
    const t = i / steps;
    const u = 1 - t;
    const bx = Math.round(u * u * x0 + 2 * u * t * cx + t * t * x1);
    const by = Math.round(u * u * y0 + 2 * u * t * cy + t * t * y1);
    if (bx === prevX && by === prevY) continue;
    if (autoLine) {
      // local tangent for char selection
      const dt = 1 / steps;
      const tx = 2 * ((1 - t) * (cx - x0) + t * (x1 - cx));
      const ty = 2 * ((1 - t) * (cy - y0) + t * (y1 - cy));
      // use tangent as a synthetic line direction
      const ch = autoCharForLine(bx, by, bx - tx, by - ty, bx + tx, by + ty);
      stampCell(bx, by, ch, target);
    } else {
      stampCell(bx, by, brushChar, target);
    }
    prevX = bx; prevY = by;
  }
}

// --- Tool System ---

abstract class ArtTool {
  abstract readonly name: string;
  abstract readonly key: string;
  preview: Cell[][] | null = null;

  onDown(_x: number, _y: number): void {}
  onDrag(_x: number, _y: number): void {}
  onUp(_x: number, _y: number): void {}
  onClick(_x: number, _y: number): void {}
  onHover(_x: number, _y: number): void {}
  onHoverEnd(): void {}
  renderOverlay(_ctx: CanvasRenderingContext2D): void {}
  cancel(): void { this.preview = null; }
  finish(): void {}
  get isDragTool(): boolean { return true; }
  get isClickTool(): boolean { return false; }
}

class BrushTool extends ArtTool {
  readonly name = "Brush";
  readonly key = "brush";

  onDown(x: number, y: number) { pushUndo(); stamp(x, y); }
  onDrag(x: number, y: number) { stamp(x, y); }
}

class EraserTool extends ArtTool {
  readonly name = "Eraser";
  readonly key = "eraser";

  onDown(x: number, y: number) { pushUndo(); stampErase(x, y); }
  onDrag(x: number, y: number) { stampErase(x, y); }
}

class PolyLineTool extends ArtTool {
  readonly name = "Polyline";
  readonly key = "polyline";
  private points: [number, number][] = [];
  private hover: [number, number] | null = null;
  get isDragTool() { return false; }
  get isClickTool() { return true; }

  onClick(x: number, y: number) {
    if (this.points.length === 0) pushUndo();
    this.points.push([x, y]);
    if (this.points.length >= 2) {
      const [px, py] = this.points[this.points.length - 2];
      plotLine(px, py, x, y, grid);
    }
    this.preview = null;
  }

  onHover(x: number, y: number) { this.hover = [x, y]; }
  onHoverEnd() { this.hover = null; }

  renderOverlay(ctx: CanvasRenderingContext2D) {
    if (this.points.length === 0 || !this.hover) return;
    const [lx, ly] = this.points[this.points.length - 1];
    const [hx, hy] = this.hover;
    ctx.globalAlpha = 0.4;
    ctx.fillStyle = fgColor;
    const temp = cloneGrid(grid);
    plotLine(lx, ly, hx, hy, temp);
    for (let y = 0; y < H; y++)
      for (let x = 0; x < W; x++)
        if (temp[y][x].char !== grid[y][x].char || temp[y][x].fg !== grid[y][x].fg)
          ctx.fillText(temp[y][x].char, x * CELL_W + CELL_W / 2, y * CELL_H + CELL_H / 2);
    ctx.globalAlpha = 1.0;

    ctx.fillStyle = "#e94560";
    for (const [px, py] of this.points)
      ctx.fillRect(px * CELL_W, py * CELL_H, CELL_W, CELL_H);
  }

  cancel() { this.points = []; this.preview = null; }
  finish() { this.points = []; this.preview = null; }
  get active() { return this.points.length > 0; }
}

class CurveTool extends ArtTool {
  readonly name = "Curve";
  readonly key = "curve";
  private points: [number, number][] = [];
  private hover: [number, number] | null = null;
  get isDragTool() { return false; }
  get isClickTool() { return true; }

  onClick(x: number, y: number) {
    if (this.points.length === 0) pushUndo();
    this.points.push([x, y]);
    if (this.points.length === 3) {
      const [p0, p1, p2] = this.points;
      plotBezier(p0[0], p0[1], p1[0], p1[1], p2[0], p2[1], grid);
      this.points = [];
      this.preview = null;
    }
  }

  onHover(x: number, y: number) { this.hover = [x, y]; }
  onHoverEnd() { this.hover = null; }

  renderOverlay(ctx: CanvasRenderingContext2D) {
    if (this.points.length === 0) return;
    const pts = [...this.points];
    if (this.hover) pts.push(this.hover);

    ctx.fillStyle = "#e94560";
    for (const [px, py] of this.points)
      ctx.fillRect(px * CELL_W, py * CELL_H, CELL_W, CELL_H);

    if (pts.length >= 2) {
      ctx.globalAlpha = 0.4;
      ctx.fillStyle = fgColor;
      const temp = cloneGrid(grid);
      if (pts.length === 2)
        plotLine(pts[0][0], pts[0][1], pts[1][0], pts[1][1], temp);
      else
        plotBezier(pts[0][0], pts[0][1], pts[1][0], pts[1][1], pts[2][0], pts[2][1], temp);
      for (let y = 0; y < H; y++)
        for (let x = 0; x < W; x++)
          if (temp[y][x].char !== grid[y][x].char || temp[y][x].fg !== grid[y][x].fg)
            ctx.fillText(temp[y][x].char, x * CELL_W + CELL_W / 2, y * CELL_H + CELL_H / 2);
      ctx.globalAlpha = 1.0;
    }
  }

  cancel() { this.points = []; this.preview = null; }
  get active() { return this.points.length > 0; }
}

class SelectTool extends ArtTool {
  readonly name = "Select";
  readonly key = "select";
  private sel: { x: number; y: number; w: number; h: number } | null = null;
  private dragStart: [number, number] | null = null;
  private mode: "selecting" | "moving" | null = null;
  private clip: Cell[][] | null = null;
  private moveOrigin: { x: number; y: number } | null = null;

  private inSel(x: number, y: number): boolean {
    if (!this.sel) return false;
    return x >= this.sel.x && x < this.sel.x + this.sel.w && y >= this.sel.y && y < this.sel.y + this.sel.h;
  }

  onDown(x: number, y: number) {
    if (this.sel && this.inSel(x, y)) {
      this.mode = "moving";
      this.dragStart = [x, y];
      if (!this.clip) {
        pushUndo();
        this.clip = [];
        for (let dy = 0; dy < this.sel.h; dy++) {
          const row: Cell[] = [];
          for (let dx = 0; dx < this.sel.w; dx++) {
            const gx = this.sel.x + dx, gy = this.sel.y + dy;
            row.push({ ...grid[gy][gx] });
            grid[gy][gx] = { char: " ", fg: PALETTE[15].hex, bg: PALETTE[0].hex };
          }
          this.clip.push(row);
        }
      }
      this.moveOrigin = { x: this.sel.x, y: this.sel.y };
    } else {
      this.commit();
      this.mode = "selecting";
      this.dragStart = [x, y];
      this.sel = null;
    }
  }

  onDrag(x: number, y: number) {
    if (!this.dragStart) return;
    if (this.mode === "selecting") {
      const [sx, sy] = this.dragStart;
      const minX = Math.max(0, Math.min(sx, x)), minY = Math.max(0, Math.min(sy, y));
      const maxX = Math.min(W - 1, Math.max(sx, x)), maxY = Math.min(H - 1, Math.max(sy, y));
      this.sel = { x: minX, y: minY, w: maxX - minX + 1, h: maxY - minY + 1 };
    } else if (this.mode === "moving" && this.sel && this.moveOrigin) {
      const [sx, sy] = this.dragStart;
      this.sel.x = Math.max(0, Math.min(W - this.sel.w, this.moveOrigin.x + (x - sx)));
      this.sel.y = Math.max(0, Math.min(H - this.sel.h, this.moveOrigin.y + (y - sy)));
    }
  }

  onUp() {
    if (this.mode === "moving") {
      this.moveOrigin = { x: this.sel!.x, y: this.sel!.y };
      this.flush();
    }
    this.dragStart = null;
    this.mode = null;
  }

  private flush() {
    if (!this.clip || !this.sel) return;
    for (let dy = 0; dy < this.sel.h; dy++)
      for (let dx = 0; dx < this.sel.w; dx++) {
        const gx = this.sel.x + dx, gy = this.sel.y + dy;
        if (gx < W && gy < H) grid[gy][gx] = { ...this.clip[dy][dx] };
      }
    this.clip = null;
    scheduleHashSave();
  }

  private commit() {
    this.flush();
    this.sel = null;
    this.clip = null;
    this.moveOrigin = null;
  }

  cancel() {
    this.commit();
    this.dragStart = null;
    this.mode = null;
    this.preview = null;
  }

  selectRect(x: number, y: number, w: number, h: number) {
    this.commit();
    this.sel = { x, y, w, h };
  }

  get selection() { return this.sel; }

  centerSelection() {
    if (!this.sel) return;
    if (!this.clip) {
      pushUndo();
      this.clip = [];
      for (let dy = 0; dy < this.sel.h; dy++) {
        const row: Cell[] = [];
        for (let dx = 0; dx < this.sel.w; dx++) {
          const gx = this.sel.x + dx, gy = this.sel.y + dy;
          row.push({ ...grid[gy][gx] });
          grid[gy][gx] = { char: " ", fg: PALETTE[15].hex, bg: PALETTE[0].hex };
        }
        this.clip.push(row);
      }
    }
    this.sel.x = Math.round((viewW - this.sel.w) / 2);
    this.sel.y = Math.round((viewH - this.sel.h) / 2);
    this.moveOrigin = { x: this.sel.x, y: this.sel.y };
    this.flush();
  }

  renderOverlay(ctx: CanvasRenderingContext2D) {
    if (!this.sel) return;
    if (this.clip) {
      for (let dy = 0; dy < this.sel.h; dy++)
        for (let dx = 0; dx < this.sel.w; dx++) {
          const c = this.clip[dy][dx];
          const px = (this.sel.x + dx) * CELL_W, py = (this.sel.y + dy) * CELL_H;
          if (c.bg !== PALETTE[0].hex) { ctx.fillStyle = c.bg; ctx.fillRect(px, py, CELL_W, CELL_H); }
          if (c.char !== " ") { ctx.fillStyle = c.fg; ctx.fillText(c.char, px + CELL_W / 2, py + CELL_H / 2); }
        }
    }
    ctx.strokeStyle = "#e94560";
    ctx.lineWidth = 2;
    ctx.setLineDash([4, 4]);
    ctx.strokeRect(this.sel.x * CELL_W, this.sel.y * CELL_H, this.sel.w * CELL_W, this.sel.h * CELL_H);
    ctx.setLineDash([]);
  }
}

// --- Tool Registry ---

const brushTool = new BrushTool();
const eraserTool = new EraserTool();
const polyLineTool = new PolyLineTool();
const curveTool = new CurveTool();
const selectTool = new SelectTool();
const tools: ArtTool[] = [brushTool, eraserTool, polyLineTool, curveTool, selectTool];
const toolMap = new Map(tools.map(t => [t.key, t]));
let activeTool: ArtTool = brushTool;

function setTool(key: string) {
  activeTool.cancel();
  const t = toolMap.get(key);
  if (!t) return;
  activeTool = t;
  document.querySelectorAll<HTMLButtonElement>("#toolbar button[data-tool]").forEach(b =>
    b.classList.toggle("active", b.dataset.tool === key));
  render();
}

document.querySelectorAll<HTMLButtonElement>("#toolbar button[data-tool]").forEach(b =>
  b.addEventListener("click", () => setTool(b.dataset.tool!)));

// --- Mouse ---

canvas.addEventListener("contextmenu", e => e.preventDefault());

let mouseDown = false;
let dragged = false;
let dragStartCell: [number, number] | null = null;
let hoverCell: [number, number] | null = null;

canvas.addEventListener("mousedown", e => {
  const cell = cellAt(e);
  if (!cell) return;
  mouseDown = true;
  dragged = false;
  dragStartCell = cell;
  if (activeTool.isDragTool) {
    activeTool.onDown(cell[0], cell[1]);
    render();
  }
});

canvas.addEventListener("mousemove", e => {
  const cell = cellAt(e);
  const info = document.getElementById("cursor-info")!;
  if (cell) {
    hoverCell = cell;
    info.textContent = `${cell[0]},${cell[1]}`;
    activeTool.onHover(cell[0], cell[1]);
  } else {
    hoverCell = null;
    info.textContent = "";
    activeTool.onHoverEnd();
  }

  if (mouseDown && cell) {
    if (!dragged && dragStartCell && (cell[0] !== dragStartCell[0] || cell[1] !== dragStartCell[1]))
      dragged = true;
    if (activeTool.isDragTool) activeTool.onDrag(cell[0], cell[1]);
  }
  render();
});

window.addEventListener("mouseup", e => {
  if (!mouseDown) return;
  mouseDown = false;
  const cell = cellAt(e);
  if (!cell) return;

  if (activeTool.isDragTool) activeTool.onUp(cell[0], cell[1]);
  if (!dragged && activeTool.isClickTool) activeTool.onClick(cell[0], cell[1]);
  render();
});

// --- Keyboard ---

window.addEventListener("keydown", e => {
  if (document.activeElement instanceof HTMLInputElement) return;

  if ((e.key === "z" || e.key === "Z") && (e.ctrlKey || e.metaKey)) { undo(); e.preventDefault(); return; }
  if ((e.key === "c" || e.key === "C") && (e.ctrlKey || e.metaKey)) { document.getElementById("btn-copy")!.click(); e.preventDefault(); return; }
  if ((e.key === "v" || e.key === "V") && (e.ctrlKey || e.metaKey)) { document.getElementById("btn-paste")!.click(); e.preventDefault(); return; }
  if (e.key === "Escape") { activeTool.cancel(); render(); return; }

  // finish polyline on Enter/Return
  if (e.key === "Enter") {
    activeTool.finish();
    render();
    e.preventDefault();
    return;
  }

  const toolKeys: Record<string, string> = { "b": "brush", "e": "eraser", "l": "polyline", "c": "curve", "s": "select" };
  if (toolKeys[e.key]) { setTool(toolKeys[e.key]); e.preventDefault(); }
  if (e.key === "a") { autoSelectContent(); e.preventDefault(); }
  if (e.key === "q" && hoverCell) {
    const c = grid[hoverCell[1]][hoverCell[0]];
    brushChar = c.char;
    charInput.value = brushChar;
    updateCharPalette();
    e.preventDefault();
  }
  if (e.key === "[") { brushSize = Math.max(1, brushSize - 2); updateBrushLabel(); e.preventDefault(); }
  if (e.key === "]") { brushSize = Math.min(9, brushSize + 2); updateBrushLabel(); e.preventDefault(); }
});

// --- Palette UI ---

const CHAR_PALETTE = [
  // blocks
  "█", "▀", "▄", "▌", "▐", "▔", "▁", "▏", "▕",
  // box drawing
  "─", "│", "┌", "┐", "└", "┘", "├", "┤", "┬", "┴", "┼",
  // diagonals & shapes
  "/", "\\", "▶", "◆", "◇", ">", "<", "^", "v",
  // misc useful
  "─", "═", "║", "●", "○", "·",
];

function buildPalette() {
  // char palette
  const charGrid = document.getElementById("char-palette")!;
  for (const ch of CHAR_PALETTE) {
    const swatch = document.createElement("div");
    swatch.className = "char-swatch";
    swatch.textContent = ch;
    swatch.title = ch;
    swatch.addEventListener("click", () => {
      brushChar = ch;
      charInput.value = ch;
      updateCharPalette();
    });
    charGrid.appendChild(swatch);
  }

  // color palette
  const fgGrid = document.getElementById("fg-colors")!;
  const bgGrid = document.getElementById("bg-colors")!;

  for (const target of [{ el: fgGrid, isFg: true }, { el: bgGrid, isFg: false }]) {
    for (const c of PALETTE) {
      const swatch = document.createElement("div");
      swatch.className = "color-swatch";
      swatch.style.background = c.hex;
      swatch.title = c.name;
      swatch.addEventListener("click", () => {
        if (target.isFg) fgColor = c.hex; else bgColor = c.hex;
        updateColorDisplay();
      });
      target.el.appendChild(swatch);
    }
  }
  updateColorDisplay();
  updateCharPalette();
}

function updateCharPalette() {
  document.querySelectorAll<HTMLElement>(".char-swatch").forEach(s =>
    s.classList.toggle("sel", s.textContent === brushChar));
}

function updateColorDisplay() {
  const fgEl = document.getElementById("fg-preview") as HTMLElement;
  const bgEl = document.getElementById("bg-preview") as HTMLElement;
  fgEl.style.background = fgColor;
  bgEl.style.background = bgColor;

  document.querySelectorAll<HTMLElement>("#fg-colors .color-swatch").forEach(s =>
    s.classList.toggle("sel", s.style.background === fgColor || rgbToHex(s.style.background) === fgColor));
  document.querySelectorAll<HTMLElement>("#bg-colors .color-swatch").forEach(s =>
    s.classList.toggle("sel", s.style.background === bgColor || rgbToHex(s.style.background) === bgColor));
}

function rgbToHex(rgb: string): string {
  const m = rgb.match(/rgb\((\d+),\s*(\d+),\s*(\d+)\)/);
  if (!m) return rgb;
  return "#" + [m[1], m[2], m[3]].map(x => parseInt(x).toString(16).padStart(2, "0")).join("");
}

// --- Swap colors ---

document.getElementById("swap-colors")!.addEventListener("click", () => {
  [fgColor, bgColor] = [bgColor, fgColor];
  updateColorDisplay();
});

// --- Brush char input ---

const charInput = document.getElementById("brush-char") as HTMLInputElement;
charInput.value = brushChar;
charInput.addEventListener("input", () => {
  const v = charInput.value;
  if (v.length > 0) brushChar = v[v.length - 1];
  charInput.value = brushChar;
  updateCharPalette();
});

// --- View size inputs ---

const viewWInput = document.getElementById("view-w") as HTMLInputElement;
const viewHInput = document.getElementById("view-h") as HTMLInputElement;

function syncViewInputs() { viewWInput.value = String(viewW); viewHInput.value = String(viewH); }
function updateBrushLabel() { document.getElementById("brush-size")!.textContent = `${brushSize}×${brushSize}`; }

function updateAutoLineLabel() {
  const btn = document.getElementById("btn-autoline")!;
  btn.textContent = `Auto-Line: ${autoLine ? "On" : "Off"}`;
  btn.classList.toggle("active", autoLine);
}

document.getElementById("btn-autoline")!.addEventListener("click", () => {
  autoLine = !autoLine;
  updateAutoLineLabel();
});

viewWInput.addEventListener("input", () => {
  viewW = Math.max(1, Math.min(W, parseInt(viewWInput.value) || W));
  scheduleHashSave(); render();
});
viewHInput.addEventListener("input", () => {
  viewH = Math.max(1, Math.min(H, parseInt(viewHInput.value) || H));
  scheduleHashSave(); render();
});

// --- Copy ---

document.getElementById("btn-copy")!.addEventListener("click", () => {
  let lines = grid.slice(0, viewH).map(row => row.slice(0, viewW).map(c => c.char).join("").trimEnd());
  while (lines.length > 0 && lines[lines.length - 1] === "") lines.pop();
  const text = lines.join("\n");
  navigator.clipboard.writeText(text);
  setStatus("Copied to clipboard");
});

// --- Paste ---

document.getElementById("btn-paste")!.addEventListener("click", async () => {
  const text = await navigator.clipboard.readText();
  if (!text) { setStatus("Clipboard empty"); return; }
  pushUndo();
  grid = newGrid();
  const lines = text.split("\n");
  for (let y = 0; y < Math.min(lines.length, H); y++)
    for (let x = 0; x < Math.min(lines[y].length, W); x++)
      if (lines[y][x] !== " ") grid[y][x] = { char: lines[y][x], fg: fgColor, bg: bgColor };
  render();
  setStatus("Pasted from clipboard");
});

// --- Auto-select content ---

function autoSelectContent() {
  let minX = viewW, minY = viewH, maxX = -1, maxY = -1;
  for (let y = 0; y < viewH; y++)
    for (let x = 0; x < viewW; x++) {
      const c = grid[y][x];
      if (c.char !== " " || c.fg !== DEFAULT_FG || c.bg !== DEFAULT_BG) {
        minX = Math.min(minX, x); minY = Math.min(minY, y);
        maxX = Math.max(maxX, x); maxY = Math.max(maxY, y);
      }
    }
  if (maxX < 0) { setStatus("Nothing to select"); return; }
  setTool("select");
  selectTool.selectRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
  render();
}

document.getElementById("btn-autosel")!.addEventListener("click", autoSelectContent);

document.getElementById("btn-center")!.addEventListener("click", () => {
  selectTool.centerSelection();
  render();
});

// --- Clear ---

document.getElementById("btn-clear")!.addEventListener("click", () => {
  pushUndo();
  const sel = selectTool.selection;
  if (sel) {
    for (let dy = 0; dy < sel.h; dy++)
      for (let dx = 0; dx < sel.w; dx++)
        grid[sel.y + dy][sel.x + dx] = { char: " ", fg: DEFAULT_FG, bg: DEFAULT_BG };
  } else {
    grid = newGrid();
  }
  render();
  setStatus(sel ? "Cleared selection" : "Cleared");
});

// --- Hash persistence ---

const DEFAULT_FG = PALETTE[15].hex;
const DEFAULT_BG = PALETTE[0].hex;

interface SaveState {
  cells: [number, number, string, string, string][]; // [x, y, char, fg, bg] for non-default
  fg: string;
  bg: string;
  brush: string;
}

function saveToHash() {
  const cells: SaveState["cells"] = [];
  for (let y = 0; y < H; y++)
    for (let x = 0; x < W; x++) {
      const c = grid[y][x];
      if (c.char !== " " || c.fg !== DEFAULT_FG || c.bg !== DEFAULT_BG)
        cells.push([x, y, c.char, c.fg, c.bg]);
    }
  const state: SaveState = { cells, fg: fgColor, bg: bgColor, brush: brushChar, vw: viewW, vh: viewH };
  history.replaceState(null, "", "#" + btoa(JSON.stringify(state)));
}

function loadFromHash(): boolean {
  if (!location.hash || location.hash.length < 2) return false;
  try {
    const state: SaveState = JSON.parse(atob(location.hash.slice(1)));
    grid = newGrid();
    for (const [x, y, ch, fg, bg] of state.cells)
      if (x < W && y < H) grid[y][x] = { char: ch, fg, bg };
    fgColor = state.fg ?? DEFAULT_FG;
    bgColor = state.bg ?? DEFAULT_BG;
    brushChar = state.brush ?? "*";
    viewW = state.vw ?? W;
    viewH = state.vh ?? H;
    return true;
  } catch { return false; }
}

let hashTimer: ReturnType<typeof setTimeout> | null = null;
function scheduleHashSave() {
  if (hashTimer) clearTimeout(hashTimer);
  hashTimer = setTimeout(saveToHash, 500);
}

// --- Status ---

function setStatus(msg: string) { document.getElementById("status")!.textContent = msg; }

// --- Init ---

if (!loadFromHash()) grid = newGrid();
charInput.value = brushChar;
syncViewInputs();
buildPalette();
render();
