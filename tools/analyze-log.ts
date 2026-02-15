#!/usr/bin/env bun
import { readFileSync } from "fs";

const file = process.argv[2] ?? "game.log";
const lines = readFileSync(file, "utf-8").split("\n");

const structured = /^\[R(\d+)\] \[(\w+)\] (.+)$/;

interface Attack { round: number; attacker: string; defender: string; weapon: string; roll: number; base_roll: number; ac: number; hit: boolean; damage?: number; }
interface Check { round: number; key: string; dc: number; roll: number; base_roll: number; result: boolean; tag: string; target: string; }
interface Damage { round: number; source: string; target: string; total: number; hp_before: number; hp_after: number; }
interface Spawn { round: number; id: string; name: string; level: number; }
interface Death { round: number; id: string; name: string; hits: number; misses: number; dmg: number; }

const attacks: Attack[] = [];
const checks: Check[] = [];
const damages: Damage[] = [];
const spawns: Spawn[] = [];
const deaths: Death[] = [];
const events: { round: number; text: string }[] = [];

for (const line of lines) {
  const m = line.match(structured);
  if (!m) continue;
  const [, roundStr, tag, json] = m;
  const round = parseInt(roundStr);
  try {
    const d = JSON.parse(json);
    switch (tag) {
      case "attack":
        attacks.push({ round, attacker: d.attacker, defender: d.defender, weapon: d.weapon, roll: d.roll, base_roll: d.base_roll, ac: d.ac, hit: d.hit, damage: d.damage });
        if (d.attacker === "you")
          events.push({ round, text: d.hit ? `⚔️ You hit ${d.defender} for ${d.damage} (rolled ${d.base_roll} vs AC ${d.ac}, ${d.hp_before}→${d.hp_after})` : `⚔️ You miss ${d.defender} (rolled ${d.base_roll} vs AC ${d.ac})` });
        break;
      case "check":
        checks.push({ round, key: d.key, dc: d.dc, roll: d.roll, base_roll: d.base_roll, result: d.result, tag: d.tag, target: "" });
        if (!d.result && d.key.endsWith("_save"))
          events.push({ round, text: `❌ Failed ${d.tag} save (rolled ${d.base_roll}, needed ${d.dc - (d.roll - d.base_roll)}+)` });
        break;
      case "damage":
        damages.push({ round, source: d.source, target: d.target, total: d.total, hp_before: d.hp_before, hp_after: d.hp_after });
        if (d.target === "you")
          events.push({ round, text: `💥 ${d.source} hits you for ${d.total} (${d.hp_before}→${d.hp_after})` });
        else if (d.total >= 15)
          events.push({ round, text: `💥 ${d.source} hits ${d.target} for ${d.total} (${d.hp_before}→${d.hp_after})` });
        break;
      case "spawn":
        spawns.push({ round, id: d.id, name: d.name, level: d.level });
        events.push({ round, text: `🐣 ${d.name} L${d.level} spawns` });
        break;
      case "death":
        deaths.push({ round, id: d.id, name: d.name, hits: d.hits, misses: d.misses, dmg: d.dmg });
        events.push({ round, text: `💀 ${d.name} dies (${d.hits}h/${d.misses}m, ${d.dmg} dmg taken)` });
        break;
    }
  } catch {}
}

// --- Analysis ---

function analyzeAttacks(label: string, atks: Attack[]) {
  if (atks.length === 0) return;
  const hits = atks.filter(a => a.hit);
  const baseRolls = atks.map(a => a.base_roll);
  const avgBase = baseRolls.reduce((a, b) => a + b, 0) / baseRolls.length;

  // expected hit rate: for each attack, P(hit) = (21 - needed) / 20, clamped
  let expectedHits = 0;
  for (const a of atks) {
    const needed = a.ac - (a.roll - a.base_roll); // roll - base = total mods
    const p = Math.min(1, Math.max(0.05, (21 - needed) / 20));
    expectedHits += p;
  }

  const hitRate = hits.length / atks.length;
  const expectedRate = expectedHits / atks.length;

  console.log(`\n${label} (${atks.length} attacks)`);
  console.log(`  Hit rate: ${(hitRate * 100).toFixed(1)}% (${hits.length}/${atks.length}), expected ${(expectedRate * 100).toFixed(1)}% (${expectedHits.toFixed(1)})`);
  console.log(`  Avg base d20: ${avgBase.toFixed(1)} (expected 10.5)`);

  if (hits.length > 0) {
    const dmgRolls = hits.filter(h => h.damage != null).map(h => h.damage!);
    const avgDmg = dmgRolls.reduce((a, b) => a + b, 0) / dmgRolls.length;
    console.log(`  Avg damage on hit: ${avgDmg.toFixed(1)}`);
  }

  // luck z-score
  const n = atks.length;
  const p = expectedHits / n;
  const stddev = Math.sqrt(n * p * (1 - p));
  const z = stddev > 0 ? (hits.length - expectedHits) / stddev : 0;
  const luck = z > 1 ? "🍀 lucky" : z < -1 ? "😢 unlucky" : "😐 average";
  console.log(`  Luck: z=${z.toFixed(2)} ${luck}`);
}

function analyzeSaves(label: string, svs: Check[]) {
  if (svs.length === 0) return;
  const passes = svs.filter(s => s.result);
  const baseRolls = svs.map(s => s.base_roll);
  const avgBase = baseRolls.reduce((a, b) => a + b, 0) / baseRolls.length;

  let expectedPasses = 0;
  for (const s of svs) {
    const mods = s.roll - s.base_roll;
    const needed = s.dc - mods;
    const p = Math.min(1, Math.max(0.05, (21 - needed) / 20));
    expectedPasses += p;
  }

  console.log(`\n${label} (${svs.length} saves)`);
  console.log(`  Pass rate: ${(passes.length / svs.length * 100).toFixed(1)}% (${passes.length}/${svs.length}), expected ${(expectedPasses / svs.length * 100).toFixed(1)}% (${expectedPasses.toFixed(1)})`);
  console.log(`  Avg base d20: ${avgBase.toFixed(1)} (expected 10.5)`);

  const n = svs.length;
  const p = expectedPasses / n;
  const stddev = Math.sqrt(n * p * (1 - p));
  const z = stddev > 0 ? (passes.length - expectedPasses) / stddev : 0;
  const luck = z > 1 ? "🍀 lucky" : z < -1 ? "😢 unlucky" : "😐 average";
  console.log(`  Luck: z=${z.toFixed(2)} ${luck}`);
}

// --- Output ---

console.log("=== COMBAT FLOW ===");
events.sort((a, b) => a.round - b.round);
for (const e of events) console.log(`  R${e.round}: ${e.text}`);

console.log("\n=== ATTACK ANALYSIS ===");

const playerAtks = attacks.filter(a => a.attacker === "you");
const monsterAtks = attacks.filter(a => a.attacker !== "you");
analyzeAttacks("Player attacks", playerAtks);
analyzeAttacks("Monster attacks (vs you)", monsterAtks.filter(a => a.defender === "you"));

// per-monster breakdown for monsters that attacked player
const monsterNames = [...new Set(monsterAtks.filter(a => a.defender === "you").map(a => a.attacker))];
for (const name of monsterNames)
  analyzeAttacks(`  ${name}`, monsterAtks.filter(a => a.attacker === name && a.defender === "you"));

console.log("\n=== SAVE ANALYSIS ===");

const saves = checks.filter(c => c.key.endsWith("_save"));
const playerSaves = saves; // saves are always on the target, which is the player in most cases
analyzeSaves("All saves (vs you)", playerSaves);

// per-tag breakdown
const saveTags = [...new Set(playerSaves.map(s => s.tag))];
for (const tag of saveTags)
  analyzeSaves(`  ${tag}`, playerSaves.filter(s => s.tag === tag));

// overall d20 luck
const allBaseRolls = [...attacks.filter(a => a.attacker === "you").map(a => a.base_roll), ...playerSaves.map(s => s.base_roll)];
if (allBaseRolls.length > 0) {
  const avg = allBaseRolls.reduce((a, b) => a + b, 0) / allBaseRolls.length;
  console.log(`\n=== OVERALL PLAYER d20 LUCK ===`);
  console.log(`  ${allBaseRolls.length} rolls, avg ${avg.toFixed(1)} (expected 10.5)`);
  const diff = avg - 10.5;
  const se = Math.sqrt(33.25 / allBaseRolls.length); // var of uniform 1-20 = 33.25
  const z = diff / se;
  const luck = z > 1.5 ? "🍀 lucky" : z < -1.5 ? "😢 unlucky" : "😐 average";
  console.log(`  z=${z.toFixed(2)} ${luck}`);
}

// --- Recent analysis (last 100 rounds) ---
const maxRound = Math.max(...attacks.map(a => a.round), ...checks.map(c => c.round), 0);
const recentCutoff = maxRound - 100;

function runAnalysis(label: string, roundFilter: (r: number) => boolean) {
  const rAtks = attacks.filter(a => roundFilter(a.round));
  const rSaves = checks.filter(c => roundFilter(c.round) && c.key.endsWith("_save"));
  const rEvents = events.filter(e => roundFilter(e.round));

  console.log(`\n${"=".repeat(50)}`);
  console.log(`=== ${label} ===`);

  if (rEvents.length > 0) {
    console.log("\n--- Flow ---");
    for (const e of rEvents) console.log(`  R${e.round}: ${e.text}`);
  }

  const rPlayerAtks = rAtks.filter(a => a.attacker === "you");
  const rMonsterAtks = rAtks.filter(a => a.attacker !== "you" && a.defender === "you");
  analyzeAttacks("Player attacks", rPlayerAtks);
  analyzeAttacks("Monster attacks (vs you)", rMonsterAtks);

  const rMonsterNames = [...new Set(rMonsterAtks.map(a => a.attacker))];
  for (const name of rMonsterNames)
    analyzeAttacks(`  ${name}`, rMonsterAtks.filter(a => a.attacker === name));

  analyzeSaves("Saves (vs you)", rSaves);
  const rSaveTags = [...new Set(rSaves.map(s => s.tag))];
  for (const tag of rSaveTags)
    analyzeSaves(`  ${tag}`, rSaves.filter(s => s.tag === tag));

  const rAllBase = [...rPlayerAtks.map(a => a.base_roll), ...rSaves.map(s => s.base_roll)];
  if (rAllBase.length > 0) {
    const avg = rAllBase.reduce((a, b) => a + b, 0) / rAllBase.length;
    const se = Math.sqrt(33.25 / rAllBase.length);
    const z = (avg - 10.5) / se;
    const luck = z > 1.5 ? "🍀 lucky" : z < -1.5 ? "😢 unlucky" : "😐 average";
    console.log(`\n  Overall d20: ${rAllBase.length} rolls, avg ${avg.toFixed(1)}, z=${z.toFixed(2)} ${luck}`);
  }
}

runAnalysis("LAST 100 ROUNDS", r => r >= recentCutoff);
