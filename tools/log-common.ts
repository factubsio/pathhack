export interface Attack { round: number; attacker: string; defender: string; weapon: string; roll: number; base_roll: number; ac: number; hit: boolean; damage?: number; hp_before?: number; hp_after?: number; }
export interface Check { round: number; key: string; dc: number; roll: number; base_roll: number; result: boolean; tag: string; }
export interface Damage { round: number; source: string; target: string; total: number; hp_before: number; hp_after: number; }
export interface Spawn { round: number; id: string; name: string; level: number; reason: string; }
export interface Death { round: number; id: string; name: string; hits: number; misses: number; dmg: number; }
export interface LevelUp { round: number; level: number; xp: number; hits: number; misses: number; dmg_taken: number; }
export interface Exp { round: number; amount: number; total: number; xl: number; dl: number; src: string; }
export interface Heal { round: number; source: string; target: string; roll: number; actual: number; }
export interface Equip { round: number; item: string; }
export interface Cast { round: number; spell: string; targeting: string; }
export interface Action { round: number; unit: string; action: string; spell: boolean; }
export interface Use { round: number; action: string; item: string; type: string; }
export interface GameEvent { round: number; text: string; tip?: string; cls?: string; }
export interface Buff { round: number; unit: string; name: string; action: string; duration?: number; stacks?: number; }

export interface TimeSeries { round: number; hp?: number; xp?: number; dl?: number; xl?: number; }

export interface LogData {
  maxRound: number;
  attacks: Attack[];
  checks: Check[];
  damages: Damage[];
  spawns: Spawn[];
  deaths: Death[];
  levelups: LevelUp[];
  exps: Exp[];
  heals: Heal[];
  equips: Equip[];
  casts: Cast[];
  actions: Action[];
  uses: Use[];
  events: GameEvent[];
  timeSeries: TimeSeries[];
  buffs: Buff[];
}

const structured = /^\[R(\d+)\] \[(\w+)\] (.+)$/;

export function parseLog(content: string): LogData {
  const lines = content.split("\n");

  const attacks: Attack[] = [];
  const checks: Check[] = [];
  const damages: Damage[] = [];
  const spawns: Spawn[] = [];
  const deaths: Death[] = [];
  const levelups: LevelUp[] = [];
  const exps: Exp[] = [];
  const heals: Heal[] = [];
  const equips: Equip[] = [];
  const casts: Cast[] = [];
  const actions: Action[] = [];
  const uses: Use[] = [];
  const buffs: Buff[] = [];
  const events: GameEvent[] = [];

  // track player HP over time
  let hp: number | undefined;
  let xp = 0, dl = 0, xl = 0;
  let lastRound = 0;
  const tsMap = new Map<number, TimeSeries>();

  function ts(round: number): TimeSeries {
    let t = tsMap.get(round);
    if (!t) { t = { round }; tsMap.set(round, t); }
    return t;
  }

  for (const line of lines) {
    const m = line.match(structured);
    if (!m) continue;
    const [, roundStr, tag, json] = m;
    const round = parseInt(roundStr);
    if (round > lastRound) lastRound = round;
    try {
      const d = JSON.parse(json);
      switch (tag) {
        case "attack":
          attacks.push({ round, attacker: d.attacker, defender: d.defender, weapon: d.weapon, roll: d.roll, base_roll: d.base_roll, ac: d.ac, hit: d.hit, damage: d.damage, hp_before: d.hp_before, hp_after: d.hp_after });
          {
            const mods = (d.check_mods ?? []).map((m: any) => `${m.value >= 0 ? "+" : ""}${m.value} ${m.why || m.cat}`).join(", ");
            const rolls = (d.rolls ?? []).map((r: any) => `${r.formula}=${r.rolled} ${r.type}${r.dr ? ` (DR ${r.dr})` : ""}`).join(", ");
            const tip = `${d.weapon}\nBase roll: ${d.base_roll}\nMods: ${mods || "none"}\nTotal: ${d.roll} vs AC ${d.ac}` + (d.hit ? `\nDamage: ${rolls}` : "");
            const isPlayer = d.attacker === "you" || d.defender === "you";
            const atk = d.attacker === "you" ? "You" : d.attacker;
            const def = d.defender === "you" ? "you" : d.defender;
            const verb = atk === "You" ? (d.hit ? "hit" : "miss") : (d.hit ? "hits" : "misses");
            const text = d.hit
              ? `⚔️ ${atk} ${verb} ${def} for ${d.damage} (${d.roll}(${d.base_roll}) vs AC ${d.ac}, ${d.hp_before}→${d.hp_after})`
              : `⚔️ ${atk} ${verb} ${def} (${d.roll}(${d.base_roll}) vs AC ${d.ac})`;
            events.push({ round, tip, text, cls: isPlayer ? undefined : "mvm" });
          }
          if (d.defender === "you" && d.hit) { hp = d.hp_after; ts(round).hp = hp; }
          break;
        case "check":
          checks.push({ round, key: d.key, dc: d.dc, roll: d.roll, base_roll: d.base_roll, result: d.result, tag: d.tag });
          if (d.key.endsWith("_save")) {
            const icon = d.result ? "✅" : "❌";
            const who = d.target === "you" ? "You" : (d.target ?? "?");
            const vb = who === "You" ? (d.result ? "pass" : "fail") : (d.result ? "passes" : "fails");
            const mods = (d.mods ?? []).map((m: any) => `${m.value >= 0 ? "+" : ""}${m.value} ${m.why || m.cat}`).join(", ");
            const tip = `${d.key} DC ${d.dc}\nBase roll: ${d.base_roll}\nMods: ${mods || "none"}\nTotal: ${d.roll}`;
            events.push({ round, tip, text: `${icon} ${who} ${vb} ${d.tag} save (rolled ${d.base_roll}, needed ${d.dc - (d.roll - d.base_roll)}+)` });
          }
          break;
        case "damage":
          damages.push({ round, source: d.source, target: d.target, total: d.total, hp_before: d.hp_before, hp_after: d.hp_after });
          const dmgTip = (d.rolls ?? []).map((r: any) => {
            let s = `${r.formula}=${r.rolled} ${r.type}`;
            if (r.dr) s += ` DR:${r.dr}`;
            if (r.prot) s += ` Prot:${r.prot}`;
            if (r.halved) s += ` (halved)`;
            return s;
          }).join("\n") + (d.saved ? "\nSaved" : "") + (d.temp_hp_absorbed ? `\nTemp HP absorbed: ${d.temp_hp_absorbed}` : "");
          if (d.target === "you") {
            hp = d.hp_after;
            ts(round).hp = hp;
            events.push({ round, tip: dmgTip, text: `💥 ${d.source === "you" ? "You take" : `${d.source} hits you for`} ${d.total} (${d.hp_before}→${d.hp_after})` });
          } else {
            const isPlayer = d.source === "you" || d.target === "you";
            const src = d.source === "you" ? "You hit" : d.source + " hits";
            events.push({ round, tip: dmgTip, cls: isPlayer ? undefined : "mvm", text: `💥 ${src} ${d.target} for ${d.total} (${d.hp_before}→${d.hp_after})` });
          }
          break;
        case "spawn":
          spawns.push({ round, id: d.id, name: d.name, level: d.level, reason: d.reason });
          events.push({ round, text: `🐣 ${d.name} L${d.level} spawns` });
          break;
        case "death":
          deaths.push({ round, id: d.id, name: d.name, hits: d.hits, misses: d.misses, dmg: d.dmg });
          events.push({ round, text: `💀 ${d.name} dies (${d.hits}h/${d.misses}m, ${d.dmg} dmg taken)` });
          break;
        case "levelup":
          levelups.push({ round, level: d.level, xp: d.xp, hits: d.hits, misses: d.misses, dmg_taken: d.dmg_taken });
          xl = d.level;
          ts(round).xl = xl;
          events.push({ round, text: `⬆️ Level up to ${d.level}` });
          break;
        case "exp":
          exps.push({ round, amount: d.amount, total: d.total, xl: d.xl, dl: d.dl, src: d.src });
          xp = d.total; dl = d.dl; xl = d.xl;
          const t = ts(round);
          t.xp = xp; t.dl = dl; t.xl = xl;
          break;
        case "heal":
          heals.push({ round, source: d.source, target: d.target, roll: d.roll, actual: d.actual });
          if (d.target === "you") {
            // we don't have hp_after in heal log, estimate from last known + actual
            if (hp !== undefined) hp += d.actual;
            if (hp !== undefined) ts(round).hp = hp;
            events.push({ round, text: `💚 Healed for ${d.actual}` });
          }
          break;
        case "equip":
        case "unequip":
          equips.push({ round, item: d.item });
          break;
        case "cast":
          casts.push({ round, spell: d.spell, targeting: d.targeting });
          events.push({ round, text: `✨ Cast ${d.spell}` });
          break;
        case "action":
          actions.push({ round, unit: d.unit, action: d.action, spell: d.spell });
          break;
        case "use":
          uses.push({ round, action: d.action, item: d.item, type: d.type });
          const actIcons: Record<string, string> = { quaff: "🍻", read: "👀", throw: "🎯" };
          const typeIcons: Record<string, string> = { potion: "🧪", scroll: "📜", bottle: "🍾" };
          events.push({ round, text: `${actIcons[d.action] ?? "❓"}${typeIcons[d.type] ?? "📦"} ${d.action} ${d.item}` });
          break;
        case "buff":
          buffs.push({ round, unit: d.unit, name: d.name, action: d.action, duration: d.duration, stacks: d.stacks });
          if (d.unit === "you") {
            const icon = d.action === "add" ? "🟢" : "🔴";
            const dur = d.duration ? ` (${d.duration}r)` : "";
            events.push({ round, text: `${icon} ${d.action} ${d.name}${dur}`, cls: "buff-event" });
          }
          break;
      }
    } catch {}
  }

  // build sorted time series, forward-fill gaps
  const timeSeries: TimeSeries[] = [];
  const maxRound = lastRound;
  let lastHp = hp, lastXp = 0, lastDl = 0, lastXl = 0;

  // first pass: collect all rounds that have data
  const sortedTs = [...tsMap.entries()].sort((a, b) => a[0] - b[0]);
  for (const [round, t] of sortedTs) {
    if (t.hp !== undefined) lastHp = t.hp;
    if (t.xp !== undefined) lastXp = t.xp;
    if (t.dl !== undefined) lastDl = t.dl;
    if (t.xl !== undefined) lastXl = t.xl;
    timeSeries.push({ round, hp: lastHp, xp: lastXp, dl: lastDl, xl: lastXl });
  }

  // ensure graph extends to current round
  if (timeSeries.length === 0 || timeSeries[timeSeries.length - 1].round < maxRound)
    timeSeries.push({ round: maxRound, hp: lastHp, xp: lastXp, dl: lastDl, xl: lastXl });

  events.sort((a, b) => a.round - b.round);

  return { maxRound, attacks, checks, damages, spawns, deaths, levelups, exps, heals, equips, casts, actions, uses, events, timeSeries, buffs };
}
