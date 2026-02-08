// bun tools/hp.ts
// Warpriest HP progression

const hpPerLevel = 8;
const baseHp = 12;
const baseCon = 12;

// Attribute boosts at 5, 11, 16 - assume Con each time
const boostLevels = [5, 11, 16];

console.log("Level |  HP  | Con | Δ");
console.log("------|------|-----|----");

let prevHp = 0;
for (let level = 1; level <= 20; level++) {
  const boosts = boostLevels.filter(l => l <= level).length;
  const con = baseCon + boosts * 2;
  const conMod = Math.floor((con - 10) / 2);
  
  const baseMax = baseHp + hpPerLevel * level;
  const hp = baseMax + level * conMod;
  const delta = hp - prevHp;
  prevHp = hp;
  
  console.log(`  ${String(level).padStart(2)}  | ${String(hp).padStart(4)} |  ${con}  | +${delta}`);
}
