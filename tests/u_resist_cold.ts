// DESC: Cast resist cold, take cold damage, verify DR applied
import { testResist } from "./energy_helper";
export default async function () { await testResist("Resist cold", "cold"); }
