// DESC: Cast false life lesser and verify temp HP
import { startRound, endTurn, cast, grantSpell, u, fmt, assert, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("False life, lesser", 3));

  await startRound();
  const r = await cast("False life, lesser").then(endTurn);
  fmt("cast", r);

  const thp = await u.tempHp();
  assert(thp > 0, `should have temp HP, got ${thp}`);

  testSummary();
}
