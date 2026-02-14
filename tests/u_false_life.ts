// DESC: Cast false life (L3) and verify temp HP is greater than lesser version
import { startRound, endTurn, cast, grantSpell, u, fmt, assert, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("False life", 3));

  await startRound();
  const r = await cast("False life").then(endTurn);
  fmt("cast", r);

  const thp = await u.tempHp();
  assert(thp > 0, `should have temp HP, got ${thp}`);

  testSummary();
}
