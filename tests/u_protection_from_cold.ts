// DESC: Cast protection from cold, take cold damage, verify absorption
import { testProtection } from "./energy_helper";
export default async function () { await testProtection("Protection from cold", "cold"); }
