// DESC: Cast acid arrow at a goblin (spell attack + acid + DoT save)
import { spawnAndCastAt, assertCheck, assertDamage, testSummary, fmt } from "./monitor";

export default async function () {
  const r = await spawnAndCastAt({ spell: "Acid Arrow", distance: 5 });
  fmt("cast", r);

  assertCheck(r, d => d.tag === "attack", "should have spell attack roll");
  if ((r.log?.find(e => e.tag === "check")?.data as any)?.result) {
    assertDamage(r, d => d.rolls.some(r => r.type === "acid"), "should deal acid damage");
    const checks = r.log?.filter(e => e.tag === "check") ?? [];
    if (checks.length > 1) {
      assertCheck(r, d => d.key === "fortitude_save", "DoT save should be fort");
    }
  }

  testSummary();
}
