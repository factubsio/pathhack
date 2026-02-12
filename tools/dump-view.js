// tools/dump-view.ts
async function load() {
  if (typeof DATA !== "undefined")
    return DATA;
  const resp = await fetch("/dump.json");
  return resp.json();
}
var WALL = 1;
var WALL_VIS = 4;
function isWallLike(frame, x, y) {
  return (frame.vis[y][x] & WALL_VIS) !== 0;
}
var BOX = {
  0: "─",
  1: "│",
  2: "─",
  3: "└",
  4: "│",
  5: "│",
  6: "┌",
  7: "├",
  8: "─",
  9: "┘",
  10: "─",
  11: "┴",
  12: "┐",
  13: "┤",
  14: "┬",
  15: "┼"
};
function wallChar(frame, x, y) {
  let mask = 0;
  if (y > 0 && isWallLike(frame, x, y - 1))
    mask |= 1;
  if (x < frame.width - 1 && isWallLike(frame, x + 1, y))
    mask |= 2;
  if (y < frame.height - 1 && isWallLike(frame, x, y + 1))
    mask |= 4;
  if (x > 0 && isWallLike(frame, x - 1, y))
    mask |= 8;
  return BOX[mask];
}
function monsterTip(name, m) {
  const el = document.createElement("div");
  el.className = "tooltiptext";
  const line = (text, cls) => {
    const d = document.createElement("div");
    d.textContent = text;
    if (cls)
      d.className = cls;
    el.appendChild(d);
  };
  line(name, "tip-title");
  line(`CR${m.cr} ${m.size} ${m.type}`, "tip-sub");
  line(`AC ${m.ac}  HP ${m.hp}/lvl  Spd ${m.speed}`);
  for (const a of m.attacks)
    line(`⚔ ${a}`);
  for (const a of m.naturalAttacks)
    line(`\uD83E\uDDB7 ${a}`);
  for (const a of m.abilities)
    line(`★ ${a}`);
  for (const p of m.passives)
    line(`• ${p}`);
  return el;
}
function itemTip(name, item) {
  const el = document.createElement("div");
  el.className = "tooltiptext";
  const line = (text, cls) => {
    const d = document.createElement("div");
    d.textContent = text;
    if (cls)
      d.className = cls;
    el.appendChild(d);
  };
  line(name, "tip-title");
  if (item.damage)
    line(`${item.damage} ${item.damageType} · ${item.hands}h · ${item.group ?? ""}`);
  if (item.ac != null)
    line(`AC +${item.ac}${item.dexCap != null ? ` (dex cap ${item.dexCap})` : ""}`);
  if (item.material)
    line(`${item.material} · wt ${item.weight}`);
  if (item.description)
    line(item.description, "tip-desc");
  for (const e of item.effects ?? [])
    line(`• ${e}`);
  return el;
}
function makeTip(name, data) {
  const mon = data.monsters[name];
  if (mon)
    return monsterTip(name, mon);
  const item = data.items[name];
  if (item)
    return itemTip(name, item);
  const el = document.createElement("div");
  el.className = "tooltiptext";
  el.textContent = name;
  return el;
}
function renderFrame(frame, data) {
  const map = document.getElementById("map");
  map.innerHTML = "";
  for (let y = 0;y < frame.height; y++) {
    const row = document.createElement("div");
    row.className = "row";
    for (let x = 0;x < frame.width; x++) {
      const v = frame.vis[y][x];
      if (v === 0) {
        row.appendChild(document.createTextNode(" "));
        continue;
      }
      const raw = frame.chars[y][x];
      const ch = raw === WALL ? wallChar(frame, x, y) : String.fromCharCode(raw);
      const color = frame.colors[y][x];
      const tipIdx = frame.tips[y][x];
      const dim = v === 1;
      if (tipIdx > 0) {
        const name = data.tipTable[tipIdx - 1];
        const span = document.createElement("span");
        span.className = `tip c${color}`;
        if (dim)
          span.classList.add("mem");
        span.textContent = ch;
        const tip = makeTip(name, data);
        if (tip)
          span.appendChild(tip);
        row.appendChild(span);
      } else if (color !== 7 || dim) {
        const span = document.createElement("span");
        span.className = `c${color}`;
        if (dim)
          span.classList.add("mem");
        span.textContent = ch;
        row.appendChild(span);
      } else {
        row.appendChild(document.createTextNode(ch));
      }
    }
    map.appendChild(row);
  }
  const messages = document.getElementById("messages");
  messages.innerHTML = "";
}
function renderMessages(data, upToIdx) {
  const el = document.getElementById("messages");
  el.innerHTML = "";
  for (let i = upToIdx;i >= 0; i--) {
    const msgs = data.frames[i].messages;
    for (let j = msgs.length - 1;j >= 0; j--) {
      const div = document.createElement("div");
      div.textContent = msgs[j];
      el.appendChild(div);
    }
  }
}
function renderHeader(data) {
  const header = document.getElementById("header");
  header.textContent = `${data.player} — ${data.ancestry} ${data.class} of ${data.deity} — ${data.branch}:${data.depth}`;
}
function renderPlayerState(frame) {
  const el = document.getElementById("player-state");
  const p = frame.player;
  const lines = [];
  const hpStr = p.tempHp > 0 ? `HP: ${p.hp}+${p.tempHp}/${p.maxHp}` : `HP: ${p.hp}/${p.maxHp}`;
  lines.push(`${hpStr}  AC: ${p.ac}  CL: ${p.cl}  XP: ${p.xp}  Gold: ${p.gold}`);
  lines.push(`Str:${p.str} Dex:${p.dex} Con:${p.con} Int:${p.int} Wis:${p.wis} Cha:${p.cha}`);
  if (p.hunger !== "Normal")
    lines.push(p.hunger);
  if (p.buffs.length > 0)
    lines.push(`Buffs: ${p.buffs.join(", ")}`);
  for (const slot of p.spellSlots) {
    const pips = "●".repeat(slot.current) + "○".repeat(slot.effectiveMax - slot.current);
    lines.push(`L${slot.level}: ${pips}`);
  }
  el.innerHTML = lines.map((l) => `<div>${l}</div>`).join("");
}
function renderSections(data, frame) {
  const el = document.getElementById("sections");
  el.innerHTML = "";
  const sections = [
    ["Inventory", data.inventory],
    ["Abilities", data.abilities],
    ["Feats", data.feats],
    ["Discoveries", data.discoveries.map((d) => `${d.appearance} → ${d.real}`)],
    ["Vanquished", Object.entries(data.vanquished).sort((a, b) => b[1] - a[1]).map(([n, c]) => c > 1 ? `${c} ${n}` : n)]
  ];
  for (const [title, items] of sections) {
    if (items.length === 0)
      continue;
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
async function main() {
  const data = await load();
  renderHeader(data);
  const slider = document.getElementById("slider");
  const label = document.getElementById("frame-label");
  const last = data.frames.length - 1;
  slider.max = String(last);
  const hash = parseInt(location.hash.slice(1));
  let current = hash >= 0 && hash <= last ? hash : last;
  slider.value = String(current);
  function show(idx) {
    current = idx;
    const frame = data.frames[idx];
    label.textContent = `Round ${frame.round} (${idx + 1}/${data.frames.length})`;
    renderFrame(frame, data);
    renderPlayerState(frame);
    renderMessages(data, idx);
    history.replaceState(null, "", `#${idx}`);
  }
  const playBtn = document.getElementById("play");
  let playing = false;
  let timer = null;
  function togglePlay() {
    playing = !playing;
    playBtn.textContent = playing ? "⏸" : "▶";
    if (playing) {
      if (current >= last) {
        current = 0;
        slider.value = "0";
        show(0);
      }
      timer = setInterval(() => {
        if (current >= last) {
          togglePlay();
          return;
        }
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
    if (playing)
      togglePlay();
    show(parseInt(slider.value));
  });
  show(current);
  renderSections(data, data.frames[last]);
}
main();
