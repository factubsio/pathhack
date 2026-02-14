import { startRound, endTurn, cast, grantSpell, doDmg, u, fmt, assert, testSummary } from "./monitor";

export async function testResist(spell: string, type: string) {
  fmt("grant spell", await grantSpell(spell, 3));

  await startRound();
  const r = await cast(spell).then(endTurn);
  fmt("cast", r);

  const hpBefore = await u.hp();
  fmt(`do ${type} dmg`, await doDmg(await u.id(), 20, type));
  const hpAfter = await u.hp();

  assert(hpBefore - hpAfter < 20, `resist should reduce 20 ${type}: took ${hpBefore - hpAfter}`);
  assert(hpBefore - hpAfter > 0, `should still take some ${type} damage`);

  testSummary();
}

export async function testProtection(spell: string, type: string) {
  fmt("grant spell", await grantSpell(spell, 3));

  await startRound();
  const r = await cast(spell).then(endTurn);
  fmt("cast", r);

  const hpBefore = await u.hp();
  fmt(`do ${type} dmg`, await doDmg(await u.id(), 20, type));
  const hpAfter = await u.hp();

  assert(hpBefore - hpAfter < 20, `protection should absorb some ${type}: took ${hpBefore - hpAfter} of 20`);

  testSummary();
}
