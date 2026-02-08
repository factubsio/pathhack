#!/usr/bin/env bun

const base = 20;

console.log("Formula: current += incoming² / (incoming + current)");
let current = 0;
for (let i = 0; i < 10; i++) {
  const eff = base / (base + current);
  const added = base * eff;
  current += added;
  console.log(`App ${i + 1}: eff=${(eff*100).toFixed(0)}% +${added.toFixed(1)} = ${current.toFixed(1)}`);
}

console.log("\nMixed sources: 20, 10, 30, 20, 5");
current = 0;
for (const n of [20, 10, 30, 20, 5]) {
  const eff = n / (n + current);
  const added = n * eff;
  current += added;
  console.log(`+${n}: eff=${(eff*100).toFixed(0)}% +${added.toFixed(1)} = ${current.toFixed(1)}`);
}

console.log("\nWith 1.3x current factor:");
current = 0;
for (let i = 0; i < 10; i++) {
  const eff = base / (base + current * 1.3);
  const added = base * eff;
  current += added;
  console.log(`App ${i + 1}: eff=${(eff*100).toFixed(0)}% +${added.toFixed(1)} = ${current.toFixed(1)}`);
}

console.log("\nWith current² on denominator:");
current = 0;
for (let i = 0; i < 10; i++) {
  const eff = base / (base + current * current / base);
  const added = base * eff;
  current += added;
  console.log(`App ${i + 1}: eff=${(eff*100).toFixed(0)}% +${added.toFixed(1)} = ${current.toFixed(1)}`);
}

console.log("\nExponent scan (5 apps of 20, showing final total):");
for (let exp = 1.0; exp <= 2.0; exp += 0.1) {
  current = 0;
  for (let i = 0; i < 5; i++) {
    const eff = base / (base + Math.pow(current, exp) / Math.pow(base, exp - 1));
    const added = base * eff;
    current += added;
  }
  console.log(`exp=${exp.toFixed(1)}: ${current.toFixed(1)}`);
}

console.log("\nWith current^2.3 on denominator:");
current = 0;
for (let i = 0; i < 10; i++) {
  const exp = 2.3;
  const eff = base / (base + Math.pow(current, exp) / Math.pow(base, exp - 1));
  const added = base * eff;
  current += added;
  console.log(`App ${i + 1}: eff=${(eff*100).toFixed(0)}% +${added.toFixed(1)} = ${current.toFixed(1)}`);
}
