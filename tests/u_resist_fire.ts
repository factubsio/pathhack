// DESC: Cast resist fire, take fire damage, verify DR applied
import { testResist } from "./energy_helper";
export default async function () { await testResist("Resist fire", "fire"); }
