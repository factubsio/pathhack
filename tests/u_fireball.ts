// DESC: Cast fireball at a goblin cluster
import { startRound, endTurn, castAt, spawn, grantSpell, findUnit, u, fmt, add, assert, assertCheck, assertDamage, assertPline, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("Fireball", 3));

  const target: [number, number] = add(u.pos, [5, 0]);
  fmt("spawn 1", await spawn("goblin", target));
  fmt("spawn 2", await spawn("goblin", add(target, [0, 1])));
  fmt("spawn 3", await spawn("goblin", add(target, [0, -1])));

  await startRound();
  const r = await castAt("Fireball", target).then(endTurn);
  fmt("cast", r);

  assertCheck(r, d => d.key === "reflex_save", "should be reflex save");
  assertDamage(r, d => d.rolls.some(r => r.type === "fire"), "should deal fire damage");

  // verify multiple targets hit
  const damages = r.log?.filter(e => e.tag === "damage") ?? [];
  assert(damages.length >= 2, `should hit multiple targets, got ${damages.length}`);

  // caster should be unharmed
  const hp = await u.hp();
  assert(hp === await u.hpMax(), "caster should not be hit by own fireball");

  testSummary();
}
