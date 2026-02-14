// DESC: Cast burning hands at a goblin
import { spawnAndCastAt, assertCheck, assertDamage, assertPline, testSummary, u, fmt } from "./monitor";

export default async function () {
  const r = await spawnAndCastAt({ spell: "Burning hands" });
  fmt("cast", r);

  assertCheck(r, d => d.key === "reflex_save", "should be reflex save");
  assertDamage(r, d => d.rolls.some(r => r.type === "fire"), "should deal fire damage");
  assertDamage(r, d => d.total > 0, "should deal nonzero damage");

  testSummary();
}
