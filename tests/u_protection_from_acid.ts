// DESC: Cast protection from acid, take acid damage, verify absorption
import { testProtection } from "./energy_helper";
export default async function () { await testProtection("Protection from acid", "acid"); }
