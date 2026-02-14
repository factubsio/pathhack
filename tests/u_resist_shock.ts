// DESC: Cast resist shock, take shock damage, verify DR applied
import { testResist } from "./energy_helper";
export default async function () { await testResist("Resist shock", "shock"); }
