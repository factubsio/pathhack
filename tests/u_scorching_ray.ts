// DESC: Cast scorching ray at a goblin (spell attack roll)
import { spawnAndCastAt, assertCheck, assertDamage, testSummary, fmt } from "./monitor";

export default async function () {
  const r = await spawnAndCastAt({ spell: "Scorching ray", distance: 5 });
  fmt("cast", r);

  assertCheck(r, d => d.tag === "attack", "should have spell attack roll");
  if ((r.log?.find(e => e.tag === "check")?.data as any)?.result) {
    assertDamage(r, d => d.rolls.some(r => r.type === "fire"), "should deal fire damage");
  }

  testSummary();
}
