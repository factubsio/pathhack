// DESC: Walk up to a goblin and melee it
import { startRound, endPlayerTurn, move, spawn, findUnit, u, fmt, add, assert, assertAttack, assertPline, testSummary } from "./monitor";

export default async function () {
  fmt("spawn goblin", await spawn("goblin", add(u.pos, [3, 0])));

  let lastHit: Awaited<ReturnType<typeof move>> | undefined;
  for (let i = 0; i < 10; i++) {
    const goblin = await findUnit(x => !x.player);
    if (!goblin) break;

    await startRound();
    const r = await move(u.dirTo(goblin.pos));
    fmt(`round ${i}`, r);
    if (r.log?.some(e => e.tag === "attack")) lastHit = r;
    await endPlayerTurn();
  }

  const goblin = await findUnit(x => !x.player);
  assert(goblin === undefined, "goblin should be dead");
  assert(lastHit !== undefined, "should have landed at least one attack");
  assertAttack(lastHit!, d => d.hit && d.damage! > 0, "final hit should deal damage");

  testSummary();
}
