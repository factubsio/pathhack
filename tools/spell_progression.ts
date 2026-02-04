// bun run tools/spell_progression.ts

const FULL_UNLOCK = [1, 4, 7, 10, 13];
const FULL_CAP = [5, 5, 4, 3, 2];
const FULL_GAINS = [3, 4, 3, 2, 1];

const PARTIAL_UNLOCK = [1, 5, 9, 13, 17];
const PARTIAL_CAP = [4, 3, 3, 2, 1];
const PARTIAL_GAINS = [2, 2, 2, 1, 0];

function freeLevels(unlocks: number[]): number[] {
  const free: number[] = [];
  for (let cl = 2; cl <= 19; cl++) {
    if (!unlocks.includes(cl)) free.push(cl);
  }
  return free;
}

function* combinations<T>(arr: T[], k: number): Generator<T[]> {
  if (k === 0) { yield []; return; }
  if (arr.length < k) return;
  const [first, ...rest] = arr;
  for (const combo of combinations(rest, k - 1)) yield [first, ...combo];
  yield* combinations(rest, k);
}

function scoreAssignment(assignment: number[][], unlocks: number[]): number {
  let score = 0;
  for (let sl = 0; sl < 5; sl++) {
    const cls = assignment[sl].sort((a, b) => a - b);
    if (cls.length <= 1) continue;
    const start = unlocks[sl];
    const end = 20;
    const idealSpacing = (end - start) / (cls.length + 1);
    for (let i = 0; i < cls.length; i++) {
      const ideal = start + idealSpacing * (i + 1);
      score += Math.abs(cls[i] - ideal);
    }
  }
  return score;
}

function* allAssignments(levels: number[], unlocks: number[], gains: number[], sl: number, current: number[][]): Generator<number[][]> {
  if (sl < 0) {
    yield current.map(a => [...a]);
    return;
  }
  
  const need = gains[sl];
  if (need === 0) {
    current[sl] = [];
    yield* allAssignments(levels, unlocks, gains, sl - 1, current);
    return;
  }
  
  const minCL = unlocks[sl] + 1;
  const valid = levels.filter(l => l >= minCL);
  
  for (const combo of combinations(valid, need)) {
    const remaining = levels.filter(l => !combo.includes(l));
    current[sl] = combo;
    yield* allAssignments(remaining, unlocks, gains, sl - 1, current);
  }
}

function findBest(name: string, unlocks: number[], gains: number[]) {
  console.log(`\n=== ${name} ===`);
  const free = freeLevels(unlocks);
  const totalGains = gains.reduce((a, b) => a + b, 0);
  console.log(`Free levels: ${free.join(', ')} (${free.length})`);
  console.log(`Gains needed: ${gains.join('+')} = ${totalGains}`);
  
  let best: { skip: number; assignment: number[][]; score: number } | null = null;
  let count = 0;
  
  for (const skip of free) {
    const levels = free.filter(l => l !== skip);
    if (levels.length < totalGains) continue;
    
    for (const assignment of allAssignments(levels, unlocks, gains, 4, [[], [], [], [], []])) {
      count++;
      const score = scoreAssignment(assignment, unlocks);
      if (!best || score < best.score) {
        best = { skip, assignment: assignment.map(a => [...a]), score };
      }
    }
  }
  
  console.log(`Checked ${count} assignments`);
  
  if (best) {
    console.log(`\nBest: skip CL ${best.skip} (score: ${best.score.toFixed(1)})`);
    for (let sl = 0; sl < 5; sl++) {
      console.log(`  L${sl + 1}: gains at ${best.assignment[sl].sort((a,b)=>a-b).join(', ') || '(none)'}`);
    }
    printTable(best.assignment, unlocks);
  }
}

function printTable(assignment: number[][], unlocks: number[]) {
  const startSlots = [2, 1, 1, 1, 1];
  const prog: number[][] = [];
  
  for (let cl = 0; cl <= 20; cl++) {
    prog[cl] = [0, 0, 0, 0, 0];
    for (let sl = 0; sl < 5; sl++) {
      if (cl >= unlocks[sl]) {
        prog[cl][sl] = startSlots[sl];
        for (const gainCL of assignment[sl]) {
          if (gainCL <= cl) prog[cl][sl]++;
        }
      }
    }
  }
  
  console.log('\nCL    L1  L2  L3  L4  L5   Σ');
  for (let cl = 1; cl <= 20; cl++) {
    const row = prog[cl].map(n => n === 0 ? ' -' : n.toString().padStart(2));
    const total = prog[cl].reduce((a, b) => a + b, 0);
    let mark = '';
    const unlockIdx = unlocks.indexOf(cl);
    if (unlockIdx >= 0) mark = ` <- L${unlockIdx + 1}`;
    for (let sl = 0; sl < 5; sl++) {
      if (assignment[sl].includes(cl)) mark += ` +L${sl + 1}`;
    }
    console.log(`${cl.toString().padStart(2)}    ${row.join('  ')}   ${total.toString().padStart(2)}${mark}`);
  }
}

findBest('Full Caster', FULL_UNLOCK, FULL_GAINS);
findBest('Partial Caster', PARTIAL_UNLOCK, PARTIAL_GAINS);
