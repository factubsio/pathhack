const syllables = ["KEN", "TYR", "ISA", "SOL", "FEHU", "GEBO", "JERA", "DAGAZ"];

const colors = [
    "ConsoleColor.Red", "ConsoleColor.Green", "ConsoleColor.Blue",
    "ConsoleColor.Cyan", "ConsoleColor.Magenta", "ConsoleColor.Yellow",
    "ConsoleColor.White", "ConsoleColor.DarkCyan", "ConsoleColor.DarkYellow",
    "ConsoleColor.DarkMagenta",
];

const combos: string[] = [];
for (let i = 0; i < syllables.length; i++)
  for (let j = i + 1; j < syllables.length; j++)
    for (let k = j + 1; k < syllables.length; k++)
      combos.push(`${syllables[i]}-${syllables[j]}-${syllables[k]}`);

for (const c of combos) {
  const color = colors[combos.indexOf(c) % colors.length];
  console.log(`            new("rune scribed ${c}", ${color}),`);
}
