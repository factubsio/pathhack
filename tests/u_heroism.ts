// DESC: Cast heroism and verify attack/save bonuses
import { startRound, endTurn, cast, grantSpell, query, fmt, assert, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("Heroism", 3));

  const before = await query("attack_bonus");
  const atkBefore = (before.result as any)?.value as number;

  await startRound();
  const r = await cast("Heroism").then(endTurn);
  fmt("cast", r);

  const after = await query("attack_bonus");
  const atkAfter = (after.result as any)?.value as number;

  assert(atkAfter > atkBefore, `attack bonus should increase: ${atkBefore} -> ${atkAfter}`);

  testSummary();
}
