// DESC: Cast light and verify light radius increases
import { startRound, endTurn, cast, grantSpell, query, fmt, assert, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("Light", 3));

  const before = await query("light_radius");
  const lrBefore = (before.result as any)?.value as number ?? 0;

  await startRound();
  const r = await cast("Light").then(endTurn);
  fmt("cast", r);

  const after = await query("light_radius");
  const lrAfter = (after.result as any)?.value as number ?? 0;

  assert(lrAfter > lrBefore, `light radius should increase: ${lrBefore} -> ${lrAfter}`);

  testSummary();
}
