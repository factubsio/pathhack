// DESC: Cast cure light wounds on self (heal pipeline)
import { startRound, endTurn, cast, grantSpell, setHp, u, fmt, assert, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("Cure light wounds", 3));
  await setHp(10);

  await startRound();
  const r = await cast("Cure light wounds", [0, 0]).then(endTurn);
  fmt("cast", r);

  const hpAfter = await u.hp();
  assert(hpAfter > 10, `should heal: 10 -> ${hpAfter}`);

  testSummary();
}
