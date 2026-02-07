// bun tools/balance.ts

// === CONFIGURABLE FORMULAS ===

const levelDC = (level: number) => 12 + Math.floor((level + 1) / 2);

// Player bonuses by level (interpolated from tier table)
const profBonus = (level: number, good: boolean) => {
  if (level <= 4) return good ? 2 : 2;
  if (level <= 9) return good ? 4 : 2;
  if (level <= 14) return good ? 6 : 2;
  return good ? 8 : 2;
};

const statBonus = (level: number, good: boolean) => {
  if (level <= 4) return good ? 4 : 0;
  if (level <= 9) return good ? 5 : 2;
  if (level <= 14) return good ? 6 : 2;
  return good ? 7 : 2;
};

const itemBonus = (level: number) => {
  if (level <= 4) return 1;
  if (level <= 9) return 2;
  if (level <= 14) return 3;
  return 4;
};

const statusBonus = (level: number, buffed: boolean) => {
  if (!buffed) return 0;
  if (level <= 4) return 1;
  if (level <= 9) return 2;
  if (level <= 14) return 3;
  return 4;
};

const circBonus = (level: number, buffed: boolean) => {
  if (!buffed) return 0;
  if (level <= 4) return 1;
  if (level <= 9) return 2;
  if (level <= 14) return 3;
  return 4;
};

// Player AC = 10 + prof + dexCap + item + status + circ
const dexCap = 2;
const playerAC = (level: number, buffed: boolean) =>
  10 + profBonus(level, true) + dexCap + itemBonus(level) + statusBonus(level, buffed) + circBonus(level, buffed);

// Player save bonus
const saveBonus = (level: number, good: boolean, buffed: boolean) =>
  profBonus(level, good) + statBonus(level, good) + itemBonus(level) + statusBonus(level, buffed) + circBonus(level, buffed);

// Monster AB = LevelDC + delta
const monsterAB = (level: number, delta: number) => levelDC(level) + delta;

// === CALCULATIONS ===

const passRate = (bonus: number, dc: number) => Math.min(100, Math.max(5, (21 - (dc - bonus)) * 5));
const hitRate = (ab: number, ac: number) => Math.min(100, Math.max(5, (ab - ac + 21) * 5));

// need = roll required to pass (dc - bonus)
// in-range: 2-19 (nat 1 always fails, nat 20 always passes)
const formatMargin = (bonus: number, dc: number): string => {
  const need = dc - bonus;
  
  // ANSI colors
  const blue = "\x1b[34m";
  const white = "\x1b[37m";
  const red = "\x1b[31m";
  const redRev = "\x1b[31;7m";
  const reset = "\x1b[0m";
  
  const colorize = (s: string, margin: number, inRange: boolean) => {
    const color = inRange
      ? (margin >= 5 ? blue : white)
      : (margin <= 4 ? red : redRev);
    return `${color}${s}${reset}`;
  };
  
  if (need <= 1) {
    // auto-pass, show headroom past need=1
    const margin = 1 - need;
    return colorize(`}${margin}`.padStart(3), margin, margin <= 4);
  }
  if (need >= 20) {
    // impossible, show how far past nat-20
    const margin = need - 20;
    return colorize(`${margin}{`.padStart(3), margin, false);
  }
  // in range: 2-19
  const fromFail = need - 1;    // distance from auto-fail (need=1)
  const fromPass = 20 - need;   // distance from auto-pass (need=20)
  
  if (fromFail <= 1 && fromPass <= 1) return colorize("__", 1, true).padStart(3);
  if (fromFail < fromPass) {
    return colorize(`[${fromFail}`.padStart(3), fromFail, true);
  } else {
    return colorize(`${fromPass}]`.padStart(3), fromPass, true);
  }
};

// === OUTPUT ===

console.log("=== SAVE TABLE ===");
console.log("Level | DC | Good | Good+ | Bad | Bad+ |");
console.log("------|-----|------|-------|-----|------|");
for (let lvl = 1; lvl <= 20; lvl += 2) {
  const dc = levelDC(lvl);
  const good = formatMargin(saveBonus(lvl, true, false), dc);
  const goodBuf = formatMargin(saveBonus(lvl, true, true), dc);
  const bad = formatMargin(saveBonus(lvl, false, false), dc);
  const badBuf = formatMargin(saveBonus(lvl, false, true), dc);
  console.log(`  ${String(lvl).padStart(2)}  |  ${dc} | ${good} |  ${goodBuf} | ${bad} |  ${badBuf} |`);
}

console.log("\n=== HIT TABLE (monster vs player AC) ===");
console.log("Level | DC | AC | AC+ | d=0 | d=0+ | d=-5 | d=-5+ |");
console.log("------|-----|-----|-----|-----|------|------|-------|");
for (let lvl = 1; lvl <= 20; lvl += 2) {
  const dc = levelDC(lvl);
  const acUnbuffed = playerAC(lvl, false);
  const acBuffed = playerAC(lvl, true);
  const ab0 = monsterAB(lvl, 0);
  const abM5 = monsterAB(lvl, -5);
  // For hits, we want formatMargin(attacker bonus, defender AC)
  const hit0 = formatMargin(ab0, acUnbuffed);
  const hit0Buf = formatMargin(ab0, acBuffed);
  const hitM5 = formatMargin(abM5, acUnbuffed);
  const hitM5Buf = formatMargin(abM5, acBuffed);
  console.log(`  ${String(lvl).padStart(2)}  |  ${dc} |  ${acUnbuffed} |  ${acBuffed} | ${hit0} |  ${hit0Buf} |  ${hitM5} |   ${hitM5Buf} |`);
}
