// DESC: Fly lesser: base stack has -4 speed, second stack removes penalty
import { grantFact, query, fmt, assert, testSummary } from "./monitor";

export default async function () {
  const before = await query("speed_bonus");
  const base_ = (before.result as any)?.value as number;

  // 1 stack: should have -4 penalty
  fmt("grant fly", await grantFact("spb:fly"));
  const at1 = await query("speed_bonus");
  const val1 = (at1.result as any)?.value as number;
  assert(val1 === base_ - 4, `1 stack should be ${base_ - 4}, got ${val1}`);

  // 2 stacks: penalty should be gone
  fmt("grant fly 2", await grantFact("spb:fly"));
  const at2 = await query("speed_bonus");
  const val2 = (at2.result as any)?.value as number;
  assert(val2 === base_, `2 stacks should be ${base_}, got ${val2}`);

  testSummary();
}
