// DESC: Cast shield and verify AC bonus
import { startRound, endTurn, cast, grantSpell, query, u, fmt, assert, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("Shield", 3));

  const before = await query("ac");
  fmt("ac before", before);
  const acBefore = (before.result as any)?.value as number;

  await startRound();
  const r = await cast("Shield").then(endTurn);
  fmt("cast", r);

  const after = await query("ac");
  fmt("ac after", after);
  const acAfter = (after.result as any)?.value as number;

  assert(acAfter > acBefore, `AC should increase: ${acBefore} -> ${acAfter}`);

  testSummary();
}
