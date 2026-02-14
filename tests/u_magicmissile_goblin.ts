// DESC: Cast magic missile at a goblin
import { spawnAndCastAt, assertDamage, assertPline, testSummary, fmt, E } from "./monitor";

export default async function () {
  const r = await spawnAndCastAt({ spell: "Magic missile", distance: 5 });
  fmt("cast", r);

  assertDamage(r, d => d.total > 0, "should deal nonzero damage");
  assertDamage(r, d => d.rolls.length >= 1, "should have at least one missile");

  testSummary();
}
