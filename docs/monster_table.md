# Monster Table Source Gen

## Shape

- `monsters.tsv` — single source of truth for numbers
- Source gen reads TSV, emits `new MonsterDef { ... }` literals
- Partial class: gen side has stats + `All` array, hand-written side has components
- Convention: `{PascalId}Components` property maps components to each monster
- AOT safe, no reflection, no runtime parsing

## TSV columns

id, name, family, level, hp, ac, ab, dmg, size, speed, glyph, color

Maybe: bite, claw (natural weapon ids) — valid to parse from TSV since it's just a lookup.

## Red line

TSV handles: stats, natural weapons, size, speed, glyph, color.

Code handles: components, spell lists, special bricks, actions, outfits.

The temptation is to keep pulling things into the TSV (spell lists, equipment tables).
Don't. The TSV is for numbers you'd want to see in a grid and tweak in bulk.
If it references code objects by name, it belongs in code.

## When

When we do the next balance pass across 3+ families. Not before.
