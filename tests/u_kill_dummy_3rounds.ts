// DESC: Spawn a training dummy and expect it dead in 3 rounds (should fail)
import { startRound, endPlayerTurn, move, spawn, findUnit, u, fmt, add, assert, testSummary } from "./monitor";

export default async function () {
  fmt("spawn dummy", await spawn("dummy", add(u.pos, [1, 0])));

  for (let i = 0; i < 3; i++) {
    const dummy = await findUnit(x => !x.player);
    if (!dummy) break;

    await startRound();
    const r = await move(u.dirTo(dummy.pos));
    fmt(`round ${i}`, r);
    await endPlayerTurn();
  }

  const dummy = await findUnit(x => !x.player);
  assert(dummy === undefined, "dummy should be dead after 3 rounds");

  testSummary();
}
