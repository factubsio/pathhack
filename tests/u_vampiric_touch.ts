// DESC: Cast vampiric touch on a goblin (spell attack + heal)
import { spawnAndCastAt, setHp, assertCheck, assertDamage, assert, u, testSummary, fmt } from "./monitor";

export default async function () {
  await setHp(10);
  const hpBefore = await u.hp();
  assert(hpBefore === 10, `hp should be 10, got ${hpBefore}`);

  const r = await spawnAndCastAt({ spell: "Vampiric touch", distance: 1 });
  fmt("cast", r);

  assertCheck(r, d => d.tag === "attack", "should have spell attack roll");
  if ((r.log?.find(e => e.tag === "check")?.data as any)?.result) {
    assertDamage(r, d => d.total > 0, "should deal damage");
    const hpAfter = await u.hp();
    assert(hpAfter > hpBefore, `should heal caster: ${hpBefore} -> ${hpAfter}`);
  }

  testSummary();
}
