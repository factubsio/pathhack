// DESC: Spawn a training dummy and hit it until dead
import { startRound, endPlayerTurn, move, spawn, findUnit, u, fmt, add, assert, assertAttack, testSummary } from "./monitor";

export default async function () {
  fmt("spawn dummy", await spawn("dummy", add(u.pos, [1, 0])));

  let hits = 0, misses = 0;
  for (let i = 0; i < 200; i++) {
    const dummy = await findUnit(x => !x.player);
    if (!dummy) break;

    await startRound();
    const r = await move(u.dirTo(dummy.pos));
    fmt(`round ${i}`, r);
    const atk = r.log?.find(e => e.tag === "attack")?.data as any;
    if (atk?.hit) hits++; else if (atk) misses++;
    await endPlayerTurn();
  }

  const dummy = await findUnit(x => !x.player);
  assert(dummy === undefined, "dummy should be dead");
  assert(hits > 0, "should have hit at least once");
  assert(hits + misses > 0, "should have attacked");

  const hp = await u.hp();
  assert(hp === await u.hpMax(), "player should be at full HP (dummy doesn't attack)");

  testSummary();
}
