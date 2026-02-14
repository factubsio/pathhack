// DESC: Cast protection from evil, verify AC bonus against evil attacker
import { startRound, endTurn, cast, grantSpell, spawn, u, fmt, add, assertAttack, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("Protection from Evil", 3));

  await startRound();
  const r = await cast("Protection from Evil").then(endTurn);
  fmt("cast", r);

  // spawn evil goblin adjacent so it attacks us
  fmt("spawn goblin", await spawn("goblin", add(u.pos, [1, 0])));

  await startRound();
  const r2 = await endTurn();
  fmt("goblin turn", r2);

  // goblin is CE, should trigger the +1 circumstance AC bonus
  assertAttack(r2, d => d.check_mods.some(m => m.why.includes("Prot")), "should have protection AC bonus vs evil");

  testSummary();
}
