// DESC: Cast resist acid, take acid damage, verify DR applied
import { testResist } from "./energy_helper";
export default async function () { await testResist("Resist acid", "acid"); }
