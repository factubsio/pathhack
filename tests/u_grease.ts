// DESC: Cast grease on a goblin (area + reflex check)
import { startRound, endTurn, castAt, spawn, grantSpell, findUnit, u, fmt, add, assert, assertCheck, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("Grease", 3));

  const target: [number, number] = add(u.pos, [3, 0]);
  fmt("spawn goblin", await spawn("goblin", target));

  await startRound();
  const r = await castAt("Grease", target).then(endTurn);
  fmt("cast", r);

  assertCheck(r, d => d.key === "reflex_save", "should force reflex save on occupant");

  testSummary();
}
