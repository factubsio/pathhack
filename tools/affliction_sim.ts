// bun tools/affliction_sim.ts

const TRIALS = 10000;

type AfflictionConfig = {
  name: string;
  maxStage: number;
  tickInterval: number; // average turns between ticks
  saveChance: number; // 0-1, chance to succeed save
  autoCureMax?: number; // if set, rn2(autoCureMax) < turns_poisoned cures
  maxTurns: number; // sim duration
};

type SimResult = {
  turnsCured: number[]; // turn at which cured, or -1 if not
  maxStageReached: number[];
  turnsAtStage: number[][]; // [trial][stage] = turns spent
};

function rn2(n: number): number {
  return Math.floor(Math.random() * n);
}

function simulate(config: AfflictionConfig): SimResult {
  const result: SimResult = {
    turnsCured: [],
    maxStageReached: [],
    turnsAtStage: [],
  };

  for (let trial = 0; trial < TRIALS; trial++) {
    let stage = 1;
    let turn = 0;
    let nextTick = Math.floor(config.tickInterval * (0.5 + Math.random()));
    let maxStage = 1;
    const stageTime = new Array(config.maxStage + 1).fill(0);
    let curedAt = -1;

    while (turn < config.maxTurns && stage > 0 && stage <= config.maxStage) {
      stageTime[stage]++;
      turn++;

      if (turn >= nextTick) {
        // Auto-cure check (poison only)
        if (config.autoCureMax && rn2(config.autoCureMax) < turn) {
          stage = 0;
          curedAt = turn;
          break;
        }

        // Save
        const saved = Math.random() < config.saveChance;
        const critSave = saved && Math.random() < 0.05; // ~5% of saves are crit
        const critFail = !saved && Math.random() < 0.05;

        if (critSave) {
          stage = 0;
          curedAt = turn;
        } else if (saved) {
          stage--;
          if (stage === 0) curedAt = turn;
        } else if (critFail) {
          stage = Math.min(stage + 2, config.maxStage);
        } else {
          stage = Math.min(stage + 1, config.maxStage);
        }

        maxStage = Math.max(maxStage, stage);
        nextTick = turn + Math.floor(config.tickInterval * (0.5 + Math.random()));
      }
    }

    result.turnsCured.push(curedAt);
    result.maxStageReached.push(maxStage);
    result.turnsAtStage.push(stageTime);
  }

  return result;
}

function printResults(config: AfflictionConfig, result: SimResult) {
  console.log(`\n=== ${config.name} ===`);
  console.log(`Save chance: ${(config.saveChance * 100).toFixed(0)}%, Tick: ~${config.tickInterval} turns, Max stage: ${config.maxStage}`);
  if (config.autoCureMax) console.log(`Auto-cure: rn2(${config.autoCureMax}) < turns`);

  // Cure stats
  const cured = result.turnsCured.filter(t => t > 0);
  const curedPct = (cured.length / TRIALS * 100).toFixed(1);
  const avgCureTurn = cured.length ? (cured.reduce((a, b) => a + b, 0) / cured.length).toFixed(0) : 'N/A';
  console.log(`\nCured: ${curedPct}% (avg turn: ${avgCureTurn})`);

  // Cure by turn checkpoints
  const checkpoints = [50, 100, 200, 500, 1000].filter(t => t <= config.maxTurns);
  for (const cp of checkpoints) {
    const curedBy = result.turnsCured.filter(t => t > 0 && t <= cp).length;
    console.log(`  By turn ${cp}: ${(curedBy / TRIALS * 100).toFixed(1)}%`);
  }

  // Max stage distribution
  console.log(`\nMax stage reached:`);
  for (let s = 1; s <= config.maxStage; s++) {
    const count = result.maxStageReached.filter(m => m === s).length;
    console.log(`  Stage ${s}: ${(count / TRIALS * 100).toFixed(1)}%`);
  }

  // Average turns at each stage
  console.log(`\nAvg turns at stage:`);
  for (let s = 1; s <= config.maxStage; s++) {
    const total = result.turnsAtStage.reduce((sum, t) => sum + t[s], 0);
    console.log(`  Stage ${s}: ${(total / TRIALS).toFixed(1)}`);
  }
}

// === CONFIGS ===

const snakeVenom: AfflictionConfig = {
  name: "Snake Venom (bad save)",
  maxStage: 3,
  tickInterval: 15,
  saveChance: 0.45,
  autoCureMax: 600,
  maxTurns: 500,
};

const snakeVenomGood: AfflictionConfig = {
  name: "Snake Venom (good save)",
  maxStage: 3,
  tickInterval: 15,
  saveChance: 0.75,
  autoCureMax: 600,
  maxTurns: 500,
};

const spiderVenom: AfflictionConfig = {
  name: "Spider Venom (bad save)",
  maxStage: 4,
  tickInterval: 15,
  saveChance: 0.45,
  autoCureMax: 600,
  maxTurns: 500,
};

const disease: AfflictionConfig = {
  name: "Disease (bad save)",
  maxStage: 4,
  tickInterval: 200,
  saveChance: 0.45,
  autoCureMax: undefined, // no auto-cure
  maxTurns: 5000,
};

const diseaseGood: AfflictionConfig = {
  name: "Disease (good save)",
  maxStage: 4,
  tickInterval: 200,
  saveChance: 0.75,
  autoCureMax: undefined,
  maxTurns: 5000,
};

// === RUN ===

for (const config of [snakeVenom, snakeVenomGood, spiderVenom, disease, diseaseGood]) {
  printResults(config, simulate(config));
}
