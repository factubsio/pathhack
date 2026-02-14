// DESC: Cast delay poison, get bitten by spider, verify poison doesn't tick
import { startRound, endTurn, cast, grantSpell, spawn, setHp, kill, findUnit, u, fmt, add, assert, testSummary } from "./monitor";

export default async function () {
  await setHp(200);
  fmt("grant spell", await grantSpell("Delay poison", 3));

  await startRound();
  const r = await cast("Delay poison").then(endTurn);
  fmt("cast", r);

  fmt("spawn spider", await spawn("giant spider", add(u.pos, [1, 0])));

  // let spider attack until we get poisoned
  let poisoned = false;
  for (let i = 0; i < 20; i++) {
    await startRound();
    const r2 = await endTurn();
    const facts = await u.facts();
    if (facts.some(f => f.includes("Spider Venom"))) { poisoned = true; fmt(`poisoned on round ${i}`, r2); break; }
  }
  assert(poisoned, "should have been poisoned by spider");

  // kill the spider so it stops generating noise
  const spider = await findUnit(x => !x.player);
  if (spider) fmt("kill spider", await kill(spider.id));

  // wait many rounds — no fort saves should fire from the suppressed affliction
  let sawFortSave = false;
  for (let i = 0; i < 100; i++) {
    await startRound();
    const r3 = await endTurn();
    const checks = r3.log?.filter(e => e.tag === "check" && (e.data as any)?.key === "fortitude_save") ?? [];
    if (checks.length > 0) { sawFortSave = true; fmt(`unexpected fort save on round ${i}`, r3); break; }
  }
  assert(!sawFortSave, "delay poison should suppress affliction ticking (no fort saves)");

  testSummary();
}
