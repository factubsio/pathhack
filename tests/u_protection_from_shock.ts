// DESC: Cast protection from shock, take shock damage, verify absorption
import { testProtection } from "./energy_helper";
export default async function () { await testProtection("Protection from shock", "shock"); }
