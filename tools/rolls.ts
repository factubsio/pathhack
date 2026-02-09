// bun tools/rolls.ts [--read-cs] [--write-cs] [--sweep]
// Simulate item generation roll distributions

import Table from 'cli-table3';

const readCs = process.argv.includes('--read-cs');
const sweep = process.argv.includes('--sweep');

interface SimResult {
  depth: number;
  mundanePct: number;
  noPotencyPct: number;
  potencyDist: { pct: number; p75: number }[];
  qualityDist: [number, number, number, number];
  empty: { pct: number; p75: number }[];
}
const writeCs = process.argv.includes('--write-cs');

function rn2(n: number): number {
  return Math.floor(Math.random() * n);
}

function rne(x: number): number {
  // Geometric distribution, 1/x chance to continue, cap at 5
  let result = 1;
  while (result < 5 && rn2(x) === 0) result++;
  return result;
}

// === CONFIG ===

interface GenConfig {
  potMundaneStart: number;
  potMundaneSlope: number;
  potMundaneFloor: number;
  potRneStart: number;
  potRneDivisor: number;
  potRneFloor: number;
  fillStart: number;
  fillSlope: number;
  fillFloor: number;
  qualStart: number;
  qualDivisor: number;
  qualFloor: number;
}

const defaultConfig: GenConfig =
{
  "potMundaneStart": 87,
  "potMundaneSlope": 2.9871723881091974,
  "potMundaneFloor": 51,
  "potRneStart": 4,
  "potRneDivisor": 5,
  "potRneFloor": 2,
  "fillStart": 69,
  "fillSlope": 1.568917336432067,
  "fillFloor": 37,
  "qualStart": 6,
  "qualDivisor": 4,
  "qualFloor": 3
};

// === ROLL FUNCTIONS (used for table generation) ===

function rollPotencyContinuous(depth: number, cfg: GenConfig): number {
  const mundaneChance = Math.max(cfg.potMundaneFloor, cfg.potMundaneStart - (depth - 1) * cfg.potMundaneSlope);
  if (rn2(100) < mundaneChance) return 0;
  const base = Math.max(cfg.potRneFloor, cfg.potRneStart - Math.floor(depth / cfg.potRneDivisor));
  return Math.min(rne(base), 3);
}

function rollQuality(depth: number, cfg: GenConfig): number {
  const base = Math.max(cfg.qualFloor, cfg.qualStart - Math.floor(depth / cfg.qualDivisor));
  return Math.min(rne(base), 4);
}

function rollFundamental(depth: number, cfg: GenConfig): { type: 'null' | 'striking', quality: number } {
  const nullChance = Math.max(20, 90 - (depth - 1) * 3);
  if (rn2(100) < nullChance) return { type: 'null', quality: 0 };
  return { type: 'striking', quality: rollQuality(depth, cfg) };
}

// === TABLE GENERATION ===

interface Tables {
  Potency: number[][];
  Fundamental: number[][];
  Fill: number[][];
  Quality: number[][];
}

function generateTables(maxDepth: number, cfg: GenConfig = defaultConfig): Tables {
  const trials = 100000;
  
  function genTable(rollFn: (depth: number) => number, minVal: number, maxVal: number): number[][] {
    const table: number[][] = [];
    for (let depth = 0; depth <= maxDepth; depth++) {
      const counts: Record<number, number> = {};
      for (let v = minVal; v <= maxVal; v++) counts[v] = 0;
      
      for (let i = 0; i < trials; i++) {
        counts[rollFn(Math.max(1, depth))]++;
      }
      
      const row: number[] = [];
      let bucket = 0;
      for (let v = minVal; v <= maxVal; v++) {
        const pct = Math.round(counts[v] / trials * 100);
        for (let j = 0; j < pct && bucket < 100; j++) {
          row.push(v);
          bucket++;
        }
      }
      while (row.length < 100) row.push(maxVal);
      table.push(row);
    }
    return table;
  }
  
  // Fill table - decreases with depth for more customization options late game
  const Fill: number[][] = [];
  for (let depth = 0; depth <= maxDepth; depth++) {
    const fillChance = Math.max(cfg.fillFloor, cfg.fillStart - Math.floor(depth * cfg.fillSlope));
    Fill.push(Array.from({ length: 100 }, (_, i) => i < fillChance ? 1 : 0));
  }
  
  return {
    Potency: genTable(d => rollPotencyContinuous(d, cfg), 0, 4),
    Fundamental: genTable(d => {
      const f = rollFundamental(d, cfg);
      return f.type === 'null' ? 0 : f.quality;
    }, 0, 4),
    Fill,
    Quality: genTable(d => rollQuality(d, cfg), 0, 4),
  };
}

function parseTablesFromCS(): Tables | null {
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

function writeTablesToCS(tables: Tables) {
  function formatTable(name: string, table: number[][]): string {
    const lines: string[] = [];
    lines.push(`    public static readonly byte[][] ${name} = [`);
    for (let d = 0; d < table.length; d++) {
      lines.push(`        [${table[d].join(',')}], // depth ${d}`);
    }
    lines.push(`    ];`);
    return lines.join('\n');
  }
  
  const lines: string[] = [];
  lines.push('// AUTO-GENERATED - do not edit manually');
  lines.push('// Regenerate with: bun tools/rolls.ts --write-cs');
  lines.push('');
  lines.push('namespace Pathhack.Game;');
  lines.push('');
  lines.push('public static class ItemGenTables');
  lines.push('{');
  lines.push(formatTable('Potency', tables.Potency));
  lines.push('');
  lines.push(formatTable('Fundamental', tables.Fundamental));
  lines.push('');
  lines.push(formatTable('Fill', tables.Fill));
  lines.push('');
  lines.push(formatTable('Quality', tables.Quality));
  lines.push('}');
  
  const output = lines.join('\n');
  const scriptDir = import.meta.dir;
  const projectRoot = require('path').resolve(scriptDir, '..');
  const outPath = require('path').join(projectRoot, 'Game/ItemGenTables.cs');
  require('fs').writeFileSync(outPath, output);
  console.log(`Wrote ${outPath}`);
}

// === SIMULATION ===

function runSimulation(tables: Tables): SimResult[] {
  function rollFromTable(table: number[][], depth: number): number {
    const d = Math.min(depth, table.length - 1);
    return table[d][rn2(100)];
  }
  
  function rollItem(depth: number) {
    const potency = rollFromTable(tables.Potency, depth);
    const fund = rollFromTable(tables.Fundamental, depth);
    let propCount = 0;
    const propQualities: number[] = [];
    for (let i = 0; i < potency; i++) {
      if (rollFromTable(tables.Fill, depth) === 1) {
        propCount++;
        propQualities.push(rollFromTable(tables.Quality, depth));
      }
    }
    return { potency, fund, propCount, propQualities };
  }
  
  const dropsPerLevel = 10;
  const runs = 1000;
  
  // Track cumulative counts across depths for P75
  const cumulativeFull: number[][] = [];
  const cumulative1emp: number[][] = [];
  const cumulative2emp: number[][] = [];
  const cumulative3pemp: number[][] = [];
  const cumulativePot1: number[][] = [];
  const cumulativePot2: number[][] = [];
  const cumulativePot3: number[][] = [];
  
  for (let run = 0; run < runs; run++) {
    cumulativeFull[run] = [];
    cumulative1emp[run] = [];
    cumulative2emp[run] = [];
    cumulative3pemp[run] = [];
    cumulativePot1[run] = [];
    cumulativePot2[run] = [];
    cumulativePot3[run] = [];
    let totalFull = 0, total1 = 0, total2 = 0, total3p = 0;
    let totalPot1 = 0, totalPot2 = 0, totalPot3 = 0;
    
    for (let depth = 1; depth <= 20; depth++) {
      for (let i = 0; i < dropsPerLevel; i++) {
        const item = rollItem(depth);
        if (item.potency > 0) {
          if (item.potency === 1) totalPot1++;
          else if (item.potency === 2) totalPot2++;
          else if (item.potency === 3) totalPot3++;
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
      cumulativePot1[run][depth] = totalPot1;
      cumulativePot2[run][depth] = totalPot2;
      cumulativePot3[run][depth] = totalPot3;
    }
  }
  
  const p75 = (arr: number[][], depth: number): number => {
    const vals = arr.map(r => r[depth]).sort((a, b) => a - b);
    return vals[Math.floor(runs * 0.75)];
  };
  
  const results: SimResult[] = [];
  
  for (const depth of [1, 3, 5, 7, 10, 15, 20]) {
    const trials = 100000;
    let mundane = 0, noPotency = 0;
    const emptyDist = [0, 0, 0, 0, 0];
    const potencyDist = [0, 0, 0, 0];
    const qualityDist = [0, 0, 0, 0, 0];
    
    for (let i = 0; i < trials; i++) {
      const item = rollItem(depth);
      if (item.potency === 0) noPotency++;
      if (item.potency === 0 && item.fund === 0) mundane++;
      if (item.potency > 0) {
        potencyDist[item.potency]++;
        const empty = item.potency - item.propCount;
        emptyDist[Math.min(empty, 4)]++;
      }
      for (const q of item.propQualities) {
        qualityDist[q]++;
      }
    }
    
    const pct = (n: number, total = trials) => n / total * 100;
    const totalQ = qualityDist[1] + qualityDist[2] + qualityDist[3] + qualityDist[4];
    
    results.push({
      depth,
      mundanePct: pct(mundane),
      noPotencyPct: pct(noPotency),
      potencyDist: [
        { pct: pct(potencyDist[1]), p75: p75(cumulativePot1, depth) },
        { pct: pct(potencyDist[2]), p75: p75(cumulativePot2, depth) },
        { pct: pct(potencyDist[3]), p75: p75(cumulativePot3, depth) },
      ],
      qualityDist: totalQ > 0 
        ? [pct(qualityDist[1], totalQ), pct(qualityDist[2], totalQ), pct(qualityDist[3], totalQ), pct(qualityDist[4], totalQ)]
        : [0, 0, 0, 0],
      empty: [
        { pct: pct(emptyDist[0]), p75: p75(cumulativeFull, depth) },
        { pct: pct(emptyDist[1]), p75: p75(cumulative1emp, depth) },
        { pct: pct(emptyDist[2]), p75: p75(cumulative2emp, depth) },
        { pct: pct(emptyDist[3] + emptyDist[4]), p75: p75(cumulative3pemp, depth) },
      ],
    });
  }
  
  return results;
}

function displayResults(results: SimResult[]) {
  const f = (n: number) => n >= 10 ? Math.round(n).toString() : n.toFixed(1);
  const col = (e: { pct: number; p75: number }) => `${f(e.pct)}/${e.p75}`;
  
  const table = new Table();
  table.push(
    [{ content: 'Depth', rowSpan: 2 }, { content: 'Mundane', rowSpan: 2 }, { content: 'Pot=0', rowSpan: 2 }, { content: 'Potency', colSpan: 3 }, { content: 'Quality', colSpan: 4 }, { content: 'Full', rowSpan: 2 }, { content: '1emp', rowSpan: 2 }, { content: '2emp', rowSpan: 2 }, { content: '3+emp', rowSpan: 2 }],
    ['1', '2', '3', '1', '2', '3', '4'],
  );
  
  for (const r of results) {
    table.push([
      r.depth,
      f(r.mundanePct),
      f(r.noPotencyPct),
      col(r.potencyDist[0]),
      col(r.potencyDist[1]),
      col(r.potencyDist[2]),
      f(r.qualityDist[0]),
      f(r.qualityDist[1]),
      f(r.qualityDist[2]),
      f(r.qualityDist[3]),
      col(r.empty[0]),
      col(r.empty[1]),
      col(r.empty[2]),
      col(r.empty[3]),
    ]);
  }
  
  console.log('\n=== ITEM GENERATION SIMULATION ===');
  console.log(table.toString());
}

function randomConfig(): GenConfig {
  const r = (min: number, max: number) => min + Math.random() * (max - min);
  const ri = (min: number, max: number) => Math.floor(r(min, max + 1));
  return {
    potMundaneStart: ri(80, 95),
    potMundaneSlope: r(0.5, 3),
    potMundaneFloor: ri(30, 60),
    potRneStart: ri(3, 5),
    potRneDivisor: ri(4, 10),
    potRneFloor: ri(2, 3),
    fillStart: ri(60, 90),
    fillSlope: r(0.5, 3),
    fillFloor: ri(20, 60),
    qualStart: ri(4, 7),
    qualDivisor: ri(3, 8),
    qualFloor: ri(2, 4),
  };
}

function runSweep() {
  const N = 100;
  
  // Targets at d20
  const targets = {
    emp3: 4,
    mundane: 50,
    qual34: 15, // Q3 + Q4 combined %
    full: 30,
  };
  
  const results: { cfg: GenConfig; d20: SimResult; score: number; breakdown: string }[] = [];
  
  for (let i = 0; i < N; i++) {
    const cfg = randomConfig();
    const tables = generateTables(20, cfg);
    const sim = runSimulation(tables);
    const d20 = sim.find(r => r.depth === 20)!;
    
    const emp3Err = Math.abs(d20.empty[3].p75 - targets.emp3);
    const mundaneErr = Math.abs(d20.mundanePct - targets.mundane) / 10;
    const qual34 = d20.qualityDist[2] + d20.qualityDist[3];
    const qual34Err = Math.abs(qual34 - targets.qual34) / 20;
    const fullErr = Math.abs(d20.empty[0].p75 - targets.full) / 5;
    
    const score = emp3Err + mundaneErr + qual34Err + fullErr;
    const breakdown = `e3:${emp3Err.toFixed(1)} m:${mundaneErr.toFixed(1)} q:${qual34Err.toFixed(1)} f:${fullErr.toFixed(1)}`;
    
    results.push({ cfg, d20, score, breakdown });
    process.stdout.write(`\r${i + 1}/${N}`);
  }
  console.log();
  
  results.sort((a, b) => a.score - b.score);
  
  const table = new Table({
    head: ['Score', 'Full', '1emp', '2emp', '3+emp', 'Mundane', 'Q3+Q4', 'Breakdown'],
  });
  
  for (const { cfg, d20, score, breakdown } of results.slice(0, 10)) {
    const qual34 = d20.qualityDist[2] + d20.qualityDist[3];
    table.push([
      score.toFixed(1),
      d20.empty[0].p75,
      d20.empty[1].p75,
      d20.empty[2].p75,
      d20.empty[3].p75,
      d20.mundanePct.toFixed(0),
      qual34.toFixed(0),
      breakdown,
    ]);
  }
  
  console.log('\n=== RANDOM SEARCH (targets: 3+emp=4, mundane=50%, Q3+Q4=15%, full=30) ===');
  console.log(table.toString());
  
  console.log('\n=== TOP 4 FULL TABLES ===');
  for (let i = 0; i < 4; i++) {
    const { cfg } = results[i];
    console.log(`\n--- Config ${i + 1}: fill:${cfg.fillStart}/${cfg.fillSlope.toFixed(1)}/${cfg.fillFloor} ---`);
    const tables = generateTables(20, cfg);
    displayResults(runSimulation(tables));
  }
  
  console.log('\nBest config:');
  console.log(JSON.stringify(results[0].cfg, null, 2));
}

// === MAIN ===

if (sweep) {
  runSweep();
  process.exit(0);
}

let tables: Tables;

if (readCs) {
  console.log("Reading tables from C#...");
  const parsed = parseTablesFromCS();
  if (!parsed) {
    console.error("Failed to read tables. Run without --read-cs first.");
    process.exit(1);
  }
  tables = parsed;
} else {
  console.log("Generating tables...");
  tables = generateTables(20);
}

displayResults(runSimulation(tables));

if (writeCs) {
  writeTablesToCS(tables);
}
