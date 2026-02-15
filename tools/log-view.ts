import type { LogData, Attack, Check, Death, Spawn, GameEvent } from "./log-common";

declare const uPlot: any;

let data: LogData;
let cursorRound = 0;
let windowSize = 200;

async function main() {
  data = await (await fetch("/data.json")).json();
  cursorRound = data.maxRound;

  const windowInput = document.getElementById("window-size") as HTMLInputElement;
  windowInput.addEventListener("change", () => {
    windowSize = parseInt(windowInput.value) || 200;
    updatePanels();
  });

  buildChart();
  updatePanels();
}

function buildChart() {
  const container = document.getElementById("chart-container")!;
  const ts = data.timeSeries;
  if (ts.length === 0) return;

  const rounds = ts.map(t => t.round);
  const hp = ts.map(t => t.hp ?? null);
  const xp = ts.map(t => t.xp ?? null);
  const dl = ts.map(t => t.dl ?? null);
  const xl = ts.map(t => t.xl ?? null);

  const opts: any = {
    width: container.clientWidth - 24,
    height: 250,
    cursor: {
      drag: { x: false, y: false },
    },
    select: { show: false },
    scales: {
      x: { time: false },
      hp: { auto: true, range: (u: any, min: number, max: number) => [0, max * 1.1] },
      xp: { auto: true },
      dl: { auto: true, range: (u: any, min: number, max: number) => [0, Math.max(max, 1)] },
      xl: { auto: true, range: (u: any, min: number, max: number) => [0, Math.max(max, 1)] },
    },
    axes: [
      { label: "Round", stroke: "#888", grid: { stroke: "#2a2a4a" } },
      { scale: "hp", label: "HP", stroke: "#e94560", grid: { stroke: "#2a2a4a" }, side: 3 },
      { scale: "xp", label: "XP", stroke: "#4ecdc4", grid: { show: false }, side: 1 },
      { scale: "dl", label: "DL", stroke: "#f9a825", grid: { show: false }, side: 1 },
      { scale: "xl", label: "CL", stroke: "#a29bfe", grid: { show: false }, side: 1 },
    ],
    series: [
      { label: "Round" },
      { label: "HP", stroke: "#e94560", width: 2, scale: "hp", fill: "rgba(233,69,96,0.1)" },
      { label: "XP", stroke: "#4ecdc4", width: 1, scale: "xp" },
      { label: "DL", stroke: "#f9a825", width: 1, scale: "dl", paths: (u: any, si: number) => stepPath(u, si) },
      { label: "CL", stroke: "#a29bfe", width: 1, scale: "xl", paths: (u: any, si: number) => stepPath(u, si) },
    ],
    hooks: {
      draw: [(u: any) => {
        const ctx = u.ctx;
        const left = u.valToPos(cursorRound - windowSize, "x", true);
        const right = u.valToPos(cursorRound, "x", true);
        const top = u.bbox.top;
        const height = u.bbox.height;

        // window shading
        ctx.save();
        ctx.fillStyle = "rgba(78,205,196,0.12)";
        ctx.fillRect(left, top, right - left, height);

        // cursor line (bold)
        ctx.strokeStyle = "#e94560";
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(right, top);
        ctx.lineTo(right, top + height);
        ctx.stroke();

        // window left edge (subtle)
        ctx.strokeStyle = "rgba(78,205,196,0.4)";
        ctx.lineWidth = 1;
        ctx.setLineDash([4, 4]);
        ctx.beginPath();
        ctx.moveTo(left, top);
        ctx.lineTo(left, top + height);
        ctx.stroke();
        ctx.restore();
      }],
    },
  };

  const plotData = [rounds, hp, xp, dl, xl];
  const plot = new uPlot(opts, plotData, container);

  // click/drag to scrub cursor
  const over = plot.over;
  let dragging = false;

  function scrubTo(e: MouseEvent) {
    const rect = over.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const round = plot.posToVal(x, "x");
    // snap to nearest data point
    let best = 0;
    let bestDist = Infinity;
    for (let i = 0; i < rounds.length; i++) {
      const dist = Math.abs(rounds[i] - round);
      if (dist < bestDist) { bestDist = dist; best = i; }
    }
    cursorRound = rounds[best];
    plot.redraw();
    updatePanels();
  }

  over.addEventListener("mousedown", (e: MouseEvent) => { dragging = true; scrubTo(e); });
  window.addEventListener("mousemove", (e: MouseEvent) => { if (dragging) scrubTo(e); });
  window.addEventListener("mouseup", () => { dragging = false; });

  // handle resize
  window.addEventListener("resize", () => {
    plot.setSize({ width: container.clientWidth - 24, height: 250 });
  });
}

function stepPath(u: any, si: number) {
  const s = u.series[si];
  const xdata = u.data[0];
  const ydata = u.data[si];
  const scaleX = "x";
  const scaleY = s.scale;

  const stroke = new Path2D();
  let started = false;

  for (let i = 0; i < xdata.length; i++) {
    if (ydata[i] == null) continue;
    const x = u.valToPos(xdata[i], scaleX, true);
    const y = u.valToPos(ydata[i], scaleY, true);
    if (!started) { stroke.moveTo(x, y); started = true; }
    else { stroke.lineTo(x, y); }
    if (i + 1 < xdata.length && ydata[i + 1] != null) {
      const nx = u.valToPos(xdata[i + 1], scaleX, true);
      stroke.lineTo(nx, y);
    }
  }

  return { stroke, fill: null, clip: null, band: null, gaps: null, flags: 0 };
}

function updateWindowLabel() {
  const label = document.getElementById("window-label")!;
  const lo = Math.max(0, cursorRound - windowSize);
  label.textContent = `R${lo}–R${cursorRound}`;
}

function inWindow(round: number): boolean {
  return round >= cursorRound - windowSize && round <= cursorRound;
}

function updatePanels() {
  updateWindowLabel();
  updateEvents();
  updateAttacks();
  updateSaves();
  updateTTK();
}

function updateEvents() {
  const el = document.getElementById("events-content")!;
  const filtered = data.events.filter(e => inWindow(e.round));
  // show most recent at top
  const reversed = [...filtered].reverse();
  el.innerHTML = reversed.map(e =>
    `<div class="log-line"><span class="round">R${e.round}</span>${e.text}</div>`
  ).join("");
}

function updateAttacks() {
  const el = document.getElementById("attacks-content")!;
  const atks = data.attacks.filter(a => inWindow(a.round) && a.attacker === "you");
  if (atks.length === 0) { el.innerHTML = "<em>No attacks</em>"; return; }

  const hits = atks.filter(a => a.hit);
  let expectedHits = 0;
  for (const a of atks) {
    const needed = a.ac - (a.roll - a.base_roll);
    expectedHits += Math.min(1, Math.max(0.05, (21 - needed) / 20));
  }
  const avgDmg = hits.length > 0 ? hits.filter(h => h.damage != null).reduce((s, h) => s + h.damage!, 0) / hits.length : 0;
  const avgBase = atks.reduce((s, a) => s + a.base_roll, 0) / atks.length;

  el.innerHTML = `
    <table>
      <tr><th></th><th>Value</th></tr>
      <tr><td>Attacks</td><td>${atks.length}</td></tr>
      <tr><td>Hit rate</td><td>${(hits.length / atks.length * 100).toFixed(1)}% (${hits.length}/${atks.length})</td></tr>
      <tr><td>Expected</td><td>${(expectedHits / atks.length * 100).toFixed(1)}%</td></tr>
      <tr><td>Avg dmg/hit</td><td>${avgDmg.toFixed(1)}</td></tr>
      <tr><td>Avg d20</td><td>${avgBase.toFixed(1)}</td></tr>
    </table>`;
}

function updateSaves() {
  const el = document.getElementById("saves-content")!;
  const saves = data.checks.filter(c => inWindow(c.round) && c.key.endsWith("_save"));
  if (saves.length === 0) { el.innerHTML = "<em>No saves</em>"; return; }

  const tags = ["All", ...new Set(saves.map(s => s.tag))];
  let html = "<table><tr><th></th><th>n</th><th>pass%</th><th>exp%</th><th>avg d20</th></tr>";

  for (const tag of tags) {
    const svs = tag === "All" ? saves : saves.filter(s => s.tag === tag);
    const passes = svs.filter(s => s.result);
    let expected = 0;
    for (const s of svs) {
      const needed = s.dc - (s.roll - s.base_roll);
      expected += Math.min(1, Math.max(0.05, (21 - needed) / 20));
    }
    const avgBase = svs.reduce((s, c) => s + c.base_roll, 0) / svs.length;
    html += `<tr><td>${tag}</td><td>${svs.length}</td><td>${(passes.length / svs.length * 100).toFixed(1)}%</td><td>${(expected / svs.length * 100).toFixed(1)}%</td><td>${avgBase.toFixed(1)}</td></tr>`;
  }
  html += "</table>";
  el.innerHTML = html;
}

function updateTTK() {
  const el = document.getElementById("ttk-content")!;
  const spawnById = new Map(data.spawns.map(s => [s.id, s]));
  const windowDeaths = data.deaths.filter(d => {
    const s = spawnById.get(d.id);
    return s && inWindow(d.round);
  });

  if (windowDeaths.length === 0) { el.innerHTML = "<em>No kills</em>"; return; }

  const byLevel = new Map<number, number[]>();
  for (const d of windowDeaths) {
    const s = spawnById.get(d.id)!;
    const rolls = d.hits + d.misses;
    if (!byLevel.has(s.level)) byLevel.set(s.level, []);
    byLevel.get(s.level)!.push(rolls);
  }

  let html = "<table><tr><th>Lvl</th><th>n</th><th>avg</th><th>med</th><th>max</th><th></th></tr>";
  const allAvgs: number[] = [];
  const rows: { level: number; n: number; avg: number; med: number; max: number }[] = [];

  for (const [level, vals] of [...byLevel.entries()].sort((a, b) => a[0] - b[0])) {
    vals.sort((a, b) => a - b);
    const avg = vals.reduce((a, b) => a + b, 0) / vals.length;
    rows.push({ level, n: vals.length, avg, med: vals[Math.floor(vals.length / 2)], max: vals[vals.length - 1] });
    allAvgs.push(avg);
  }

  const maxAvg = Math.max(...allAvgs, 1);
  for (const r of rows) {
    const barW = Math.max(1, Math.round(r.avg / maxAvg * 100));
    html += `<tr><td>L${r.level}</td><td>${r.n}</td><td>${r.avg.toFixed(1)}</td><td>${r.med}</td><td>${r.max}</td><td><div style="background:#e94560;height:10px;width:${barW}px"></div></td></tr>`;
  }
  html += "</table>";
  el.innerHTML = html;
}

main();
