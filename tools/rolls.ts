// bun tools/rolls.ts
// Simulate item generation roll distributions

function rn2(n: number): number {
  return Math.floor(Math.random() * n);
}

function rne(n: number): number {
  // Geometric distribution 1 to n, favors low
  for (let i = 1; i < n; i++) {
    if (rn2(2) === 0) return i;
  }
  return n;
}

// Current implementation (advantage-based)
function rollPotencyAdvantage(depth: number): number {
  const tier = Math.floor((depth + 3) / 5);
  let roll = rne(5) - 1;
  for (let i = 0; i < tier; i++) {
    roll = Math.max(roll, rne(5) - 1);
  }
  return roll;
}

// Proposed: base + continuous depth bonus
function rollPotencyAdditive(depth: number): number {
  const mundaneChance = Math.max(20, 90 - (depth - 1) * 3);
  if (rn2(100) < mundaneChance) return 0;
  // Continuous: rne scales with depth, not tiers
  const bonusMax = Math.max(1, Math.floor(depth / 5) + 1);
  const bonus = rne(bonusMax) - 1;
  return Math.min(rne(4) + bonus, 4);
}

// Proposed v2: fully continuous
function rollPotencyContinuous(depth: number): number {
  const mundaneChance = Math.max(20, 90 - (depth - 1) * 3);
  if (rn2(100) < mundaneChance) return 0;
  // Roll base 1-4, then chance to add 1 based on depth
  let result = rne(4);
  // Each 5 depth gives 50% chance to +1
  for (let d = 5; d <= depth; d += 5) {
    if (rn2(2) === 0) result++;
  }
  return Math.min(result, 3);
}

// Simulate and count distribution
function simulate(fn: (depth: number) => number, depth: number, trials = 10000): number[] {
  const counts = [0, 0, 0, 0, 0]; // 0-4
  for (let i = 0; i < trials; i++) {
    const result = fn(depth);
    counts[Math.min(result, 4)]++;
  }
  return counts.map(c => Math.round(c / trials * 100));
}

function printTable(name: string, fn: (depth: number) => number) {
  console.log(`\n=== ${name} ===`);
  console.log("Depth |  +0  |  +1  |  +2  |  +3  |  +4  |");
  console.log("------|------|------|------|------|------|");
  for (const depth of [1, 3, 5, 7, 10, 15, 20]) {
    const pcts = simulate(fn, depth);
    const row = pcts.map(p => `${p}%`.padStart(4)).join("  | ");
    console.log(`  ${String(depth).padStart(2)}  | ${row}  |`);
  }
}

printTable("ADVANTAGE (current)", rollPotencyAdvantage);
printTable("ADDITIVE (tiered)", rollPotencyAdditive);
printTable("CONTINUOUS (proposed)", rollPotencyContinuous);

// Full item simulation
interface ItemResult {
  potency: number;
  fundamental: 'null' | 'striking';
  fundQuality: number;
  propCount: number;
  propQualities: number[];
}

function rollQuality(depth: number): number {
  let result = rne(5) - 1;
  for (let d = 5; d <= depth; d += 5) {
    if (rn2(2) === 0) result++;
  }
  return Math.min(result, 4);
}

function rollFundamental(depth: number): { type: 'null' | 'striking', quality: number } {
  const nullChance = Math.max(20, 90 - (depth - 1) * 3);
  if (rn2(100) < nullChance) return { type: 'null', quality: 0 };
  return { type: 'striking', quality: rollQuality(depth) };
}

function rollPropertyRunes(depth: number, slots: number): number[] {
  const qualities: number[] = [];
  const fillChance = Math.min(90, 70 + depth); // 70% base, +1/depth, cap 90%
  
  // Roll each slot independently
  for (let i = 0; i < slots; i++) {
    if (rn2(100) < fillChance) {
      qualities.push(rollQuality(depth));
    }
  }
  return qualities;
}

function rollFullItem(depth: number): ItemResult {
  const potency = rollPotencyContinuous(depth);
  const fund = rollFundamental(depth);
  const props = rollPropertyRunes(depth, potency);
  return {
    potency,
    fundamental: fund.type,
    fundQuality: fund.quality,
    propCount: props.length,
    propQualities: props,
  };
}

function simulateItems(depth: number, trials = 10000) {
  let mundane = 0;
  let hasSlots = 0;
  let hasProps = 0;
  let hasFund = 0;
  let totalProps = 0;
  let fundQualitySum = 0;
  let fundCount = 0;
  let emptySlots = 0;
  let totalSlots = 0;
  const emptyDist = [0, 0, 0, 0, 0]; // 0, 1, 2, 3, 4 empty
  
  for (let i = 0; i < trials; i++) {
    const item = rollFullItem(depth);
    if (item.potency === 0 && item.fundamental === 'null') mundane++;
    if (item.potency > 0) {
      hasSlots++;
      totalSlots += item.potency;
      const empty = item.potency - item.propCount;
      emptySlots += empty;
      emptyDist[Math.min(empty, 4)]++;
    }
    if (item.fundamental !== 'null') {
      hasFund++;
      fundQualitySum += item.fundQuality;
      fundCount++;
    }
    if (item.propCount > 0) hasProps++;
    totalProps += item.propCount;
  }
  
  return {
    mundane: Math.round(mundane / trials * 100),
    hasSlots: Math.round(hasSlots / trials * 100),
    hasFund: Math.round(hasFund / trials * 100),
    avgFundQ: fundCount > 0 ? (fundQualitySum / fundCount).toFixed(1) : "0.0",
    hasProps: Math.round(hasProps / trials * 100),
    avgSlots: hasSlots > 0 ? (totalSlots / hasSlots).toFixed(1) : "0.0",
    avgEmpty: hasSlots > 0 ? (emptySlots / hasSlots).toFixed(1) : "0.0",
    empty0: Math.round(emptyDist[0] / trials * 100),
    empty1: Math.round(emptyDist[1] / trials * 100),
    empty2: Math.round(emptyDist[2] / trials * 100),
    empty3p: Math.round((emptyDist[3] + emptyDist[4]) / trials * 100),
  };
}

console.log("\n=== FULL ITEM SIMULATION ===");
console.log("Depth | Mundane | Slots | Striking | Props | Empty distribution (of all items)");
console.log("      |         |       |          |       |  Full |  1emp |  2emp |  3+emp |");
console.log("------|---------|-------|----------|-------|-------|-------|-------|--------|");
for (const depth of [1, 3, 5, 7, 10, 15, 20]) {
  const s = simulateItems(depth);
  console.log(`  ${String(depth).padStart(2)}  |   ${String(s.mundane).padStart(3)}%  | ${String(s.hasSlots).padStart(4)}% |    ${String(s.hasFund).padStart(3)}%  | ${String(s.hasProps).padStart(4)}% | ${String(s.empty0).padStart(4)}% | ${String(s.empty1).padStart(4)}% | ${String(s.empty2).padStart(4)}% |  ${String(s.empty3p).padStart(4)}% |`);
}


// Simulate full dungeon run
function simulateDungeon(dropsPerLevel: number, maxDepth: number, runs: number = 1000) {
  let total3pEmpty = 0;
  
  for (let run = 0; run < runs; run++) {
    let run3p = 0;
    for (let depth = 1; depth <= maxDepth; depth++) {
      for (let i = 0; i < dropsPerLevel; i++) {
        const item = rollFullItem(depth);
        const empty = item.potency - item.propCount;
        if (empty >= 3) run3p++;
      }
    }
    total3pEmpty += run3p;
  }
  
  console.log(`\n=== DUNGEON SIMULATION (${runs} runs, ${dropsPerLevel} drops/level, ${maxDepth} levels) ===`);
  console.log(`Average 3+ empty weapons per run: ${(total3pEmpty / runs).toFixed(2)}`);
}

simulateDungeon(100, 20, 1000);

// === TABLE GENERATION ===

function generatePotencyTable(maxDepth: number): number[][] {
  const table: number[][] = [];
  for (let depth = 0; depth <= maxDepth; depth++) {
    const row: number[] = [];
    const counts = [0, 0, 0, 0, 0];
    const trials = 100000;
    
    for (let i = 0; i < trials; i++) {
      counts[rollPotencyContinuous(Math.max(1, depth))]++;
    }
    
    // Convert counts to cumulative d100 buckets
    let bucket = 0;
    for (let p = 0; p <= 4; p++) {
      const pct = Math.round(counts[p] / trials * 100);
      for (let j = 0; j < pct && bucket < 100; j++) {
        row.push(p);
        bucket++;
      }
    }
    // Fill remaining with highest
    while (row.length < 100) row.push(4);
    
    table.push(row);
  }
  return table;
}

function generateFundamentalTable(maxDepth: number): number[][] {
  const table: number[][] = [];
  for (let depth = 0; depth <= maxDepth; depth++) {
    const row: number[] = [];
    const counts: Record<number, number> = { 0: 0, 1: 0, 2: 0, 3: 0, 4: 0 };
    const trials = 100000;
    
    for (let i = 0; i < trials; i++) {
      const f = rollFundamental(Math.max(1, depth));
      if (f.type === 'null') counts[0]++;
      else counts[f.quality]++;
    }
    
    let bucket = 0;
    for (let q = 0; q <= 4; q++) {
      const pct = Math.round(counts[q] / trials * 100);
      for (let j = 0; j < pct && bucket < 100; j++) {
        row.push(q);
        bucket++;
      }
    }
    while (row.length < 100) row.push(4);
    
    table.push(row);
  }
  return table;
}

function generateFillTable(maxDepth: number): number[][] {
  const table: number[][] = [];
  for (let depth = 0; depth <= maxDepth; depth++) {
    const row: number[] = [];
    const fillChance = Math.min(90, 70 + Math.max(1, depth));
    
    for (let i = 0; i < 100; i++) {
      row.push(i < fillChance ? 1 : 0);
    }
    table.push(row);
  }
  return table;
}

function generateQualityTable(maxDepth: number): number[][] {
  const table: number[][] = [];
  for (let depth = 0; depth <= maxDepth; depth++) {
    const row: number[] = [];
    const counts = [0, 0, 0, 0, 0]; // 0 unused, 1-4 quality
    const trials = 100000;
    
    for (let i = 0; i < trials; i++) {
      counts[rollQuality(Math.max(1, depth))]++;
    }
    
    let bucket = 0;
    for (let q = 1; q <= 4; q++) {
      const pct = Math.round(counts[q] / trials * 100);
      for (let j = 0; j < pct && bucket < 100; j++) {
        row.push(q);
        bucket++;
      }
    }
    while (row.length < 100) row.push(4);
    
    table.push(row);
  }
  return table;
}

function formatTable(name: string, table: number[][]): string {
  const lines: string[] = [];
  lines.push(`    public static readonly byte[][] ${name} = [`);
  for (let d = 0; d < table.length; d++) {
    lines.push(`        [${table[d].join(',')}], // depth ${d}`);
  }
  lines.push(`    ];`);
  return lines.join('\n');
}

if (process.argv.includes('--generate')) {
  const maxDepth = 20;
  const lines: string[] = [];
  
  lines.push('// AUTO-GENERATED - do not edit manually');
  lines.push('// Regenerate with: bun tools/rolls.ts --generate');
  lines.push('');
  lines.push('namespace Pathhack.Game;');
  lines.push('');
  lines.push('public static class ItemGenTables');
  lines.push('{');
  lines.push(formatTable('Potency', generatePotencyTable(maxDepth)));
  lines.push('');
  lines.push(formatTable('Fundamental', generateFundamentalTable(maxDepth)));
  lines.push('');
  lines.push(formatTable('Fill', generateFillTable(maxDepth)));
  lines.push('');
  lines.push(formatTable('Quality', generateQualityTable(maxDepth)));
  lines.push('}');
  
  const output = lines.join('\n');
  const scriptDir = import.meta.dir;
  const projectRoot = require('path').resolve(scriptDir, '..');
  const outPath = require('path').join(projectRoot, 'Game/ItemGenTables.cs');
  require('fs').writeFileSync(outPath, output);
  console.log(`Wrote ${outPath}`);
}

// === TABLE-BASED SIMULATION ===

function parseTablesFromCS(): { Potency: number[][], Fundamental: number[][], Fill: number[][], Quality: number[][] } | null {
  const scriptDir = import.meta.dir;
  const projectRoot = require('path').resolve(scriptDir, '..');
  const filePath = require('path').join(projectRoot, 'Game/ItemGenTables.cs');
  
  try {
    const content = require('fs').readFileSync(filePath, 'utf-8');
    
    const parseTable = (name: string): number[][] => {
      const regex = new RegExp(`public static readonly byte\\[\\]\\[\\] ${name} = \\[([\\s\\S]*?)\\];`, 'm');
      const match = content.match(regex);
      if (!match) throw new Error(`Table ${name} not found`);
      
      const rows: number[][] = [];
      const rowRegex = /\[([0-9,]+)\]/g;
      let rowMatch;
      while ((rowMatch = rowRegex.exec(match[1])) !== null) {
        rows.push(rowMatch[1].split(',').map(Number));
      }
      return rows;
    };
    
    return {
      Potency: parseTable('Potency'),
      Fundamental: parseTable('Fundamental'),
      Fill: parseTable('Fill'),
      Quality: parseTable('Quality'),
    };
  } catch (e) {
    console.error(`Failed to parse tables: ${e}`);
    return null;
  }
}

if (process.argv.includes('--simulate')) {
  const tables = parseTablesFromCS();
  if (!tables) {
    console.error('Run --generate first');
    process.exit(1);
  }
  
  function rollFromTable(table: number[][], depth: number): number {
    const d = Math.min(depth, table.length - 1);
    return table[d][rn2(100)];
  }
  
  function rollItemFromTables(depth: number) {
    const potency = rollFromTable(tables.Potency, depth);
    const fund = rollFromTable(tables.Fundamental, depth);
    
    let propCount = 0;
    for (let i = 0; i < potency; i++) {
      if (rollFromTable(tables.Fill, depth) === 1) propCount++;
    }
    
    return { potency, fund, propCount };
  }
  
  console.log("\n=== SIMULATION FROM C# TABLES ===");
  console.log("Depth | Mundane |   Potency 1/2/3   | Striking | Props | Empty distribution (% / P75 cumulative count)");
  console.log("      |         |                   |          |       |    Full    |    1emp    |    2emp    |   3+emp    |");
  console.log("------|---------|-------------------|----------|-------|------------|------------|------------|------------|");
  
  const dropsPerLevel = 10;
  const runs = 1000;
  
  // Track cumulative counts across depths for P75
  const cumulativeFull: number[][] = [];
  const cumulative1emp: number[][] = [];
  const cumulative2emp: number[][] = [];
  const cumulative3pemp: number[][] = [];
  
  for (let run = 0; run < runs; run++) {
    cumulativeFull[run] = [];
    cumulative1emp[run] = [];
    cumulative2emp[run] = [];
    cumulative3pemp[run] = [];
    let totalFull = 0, total1 = 0, total2 = 0, total3p = 0;
    
    for (let depth = 1; depth <= 20; depth++) {
      for (let i = 0; i < dropsPerLevel; i++) {
        const item = rollItemFromTables(depth);
        if (item.potency > 0) {
          const empty = item.potency - item.propCount;
          if (empty === 0) totalFull++;
          else if (empty === 1) total1++;
          else if (empty === 2) total2++;
          else total3p++;
        }
      }
      cumulativeFull[run][depth] = totalFull;
      cumulative1emp[run][depth] = total1;
      cumulative2emp[run][depth] = total2;
      cumulative3pemp[run][depth] = total3p;
    }
  }
  
  const p75 = (arr: number[][], depth: number): number => {
    const vals = arr.map(r => r[depth]).sort((a, b) => a - b);
    return vals[Math.floor(runs * 0.75)];
  };
  
  for (const depth of [1, 3, 5, 7, 10, 15, 20]) {
    const trials = 10000;
    let mundane = 0, hasSlots = 0, hasFund = 0, hasProps = 0;
    const emptyDist = [0, 0, 0, 0, 0];
    const potencyDist = [0, 0, 0, 0]; // 0, 1, 2, 3
    
    for (let i = 0; i < trials; i++) {
      const item = rollItemFromTables(depth);
      if (item.potency === 0 && item.fund === 0) mundane++;
      if (item.potency > 0) {
        hasSlots++;
        potencyDist[item.potency]++;
        const empty = item.potency - item.propCount;
        emptyDist[Math.min(empty, 4)]++;
      }
      if (item.fund > 0) hasFund++;
      if (item.propCount > 0) hasProps++;
    }
    
    const pct = (n: number) => (n / trials * 100).toFixed(1).padStart(5);
    const p75Full = p75(cumulativeFull, depth);
    const p751emp = p75(cumulative1emp, depth);
    const p752emp = p75(cumulative2emp, depth);
    const p753pemp = p75(cumulative3pemp, depth);
    
    const col = (n: number, p: number) => `${pct(n)}%/${String(p).padStart(2)}`;
    const pot = `${pct(potencyDist[1])}/${pct(potencyDist[2])}/${pct(potencyDist[3])}`;
    console.log(`  ${String(depth).padStart(2)}  |  ${pct(mundane)}% | ${pot} |   ${pct(hasFund)}% | ${pct(hasProps)}% | ${col(emptyDist[0], p75Full)} | ${col(emptyDist[1], p751emp)} | ${col(emptyDist[2], p752emp)} | ${col(emptyDist[3] + emptyDist[4], p753pemp)} |`);
  }
}
