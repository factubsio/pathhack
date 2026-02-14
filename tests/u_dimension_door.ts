// DESC: Cast dimension door (teleport)
import { startRound, endTurn, castAt, grantSpell, u, fmt, add, assert, testSummary } from "./monitor";

export default async function () {
  fmt("grant spell", await grantSpell("Dimension door", 3));

  const posBefore = [...u.pos] as [number, number];
  const target: [number, number] = add(u.pos, [5, 0]);

  await startRound();
  const r = await castAt("Dimension door", target).then(endTurn);
  fmt("cast", r);

  const posAfter = u.pos;
  assert(posAfter[0] !== posBefore[0] || posAfter[1] !== posBefore[1], "player should have moved");

  testSummary();
}
