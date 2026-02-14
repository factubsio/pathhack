// DESC: Cast hold person on a goblin (humanoid)
import { startRound, endTurn, cast, spawn, grantSpell, findUnit, u, fmt, add, assert, assertCheck, assertPline, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("Hold person", 3));
  fmt("spawn goblin", await spawn("goblin", add(u.pos, [2, 0])));

  const goblin = await findUnit(x => !x.player);
  assert(goblin !== undefined, "goblin should exist");

  await startRound();
  const r = await cast("Hold person", goblin!.id).then(endTurn);
  fmt("cast", r);

  assertCheck(r, d => d.key === "will_save", "should be will save");

  testSummary();
}
