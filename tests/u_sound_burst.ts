// DESC: Cast sound burst on a goblin (pos AoE + fort save + stun)
import { startRound, endTurn, castAt, spawn, grantSpell, u, fmt, add, assertCheck, assertDamage, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("Sound burst", 3));

  const target: [number, number] = add(u.pos, [4, 0]);
  fmt("spawn goblin", await spawn("goblin", target));

  await startRound();
  const r = await castAt("Sound burst", target).then(endTurn);
  fmt("cast", r);

  assertCheck(r, d => d.key === "fortitude_save", "should be fort save");
  assertDamage(r, d => d.total > 0, "should deal damage");

  testSummary();
}
