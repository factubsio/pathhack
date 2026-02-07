# DC and Bonus Guidelines

## Core Formula

Base DC/AC = 12 + level/2

| Level | Base DC |
|-------|---------|
| 1     | 13      |
| 5     | 14-15   |
| 10    | 17      |
| 15    | 19-20   |
| 20    | 22      |

This is an upper bound for on-level challenges, not a hard cap. Mooks can be lower, bosses can be +1-2 higher.

## Expected Bonuses by Tier

| Level | Prof (typical) | Stat (typical) | Item | Status | Circ |
|-------|----------------|----------------|------|--------|------|
| 1-4   | +2 (T)         | +4             | +1   | +1     | +1   |
| 5-9   | +4 (E)         | +5             | +2   | +2     | +2   |
| 10-14 | +6 (M)         | +6             | +3   | +3     | +3   |
| 15-20 | +8 (L)         | +7             | +4   | +4     | +4   |

These are guidelines, not hard caps. Artifacts, wishes, or lucky rolls can exceed. Typed bonuses (item/status/circumstance) don't stack - best of each type wins. Untyped stacks but should be rare.

## Expected Player Saves (Level 20, DC 22)

**Good save (master prof, main stat):**
- Prof +6, Stat +7, Item +4, Status +2 = +19
- Succeeds on 3 (90%)
- With circumstance +4: succeeds on -1, only fails nat 1

**Bad save (trained prof, dump stat):**
- Prof +2, Stat +2, Item +4, Status +2 = +10
- Succeeds on 12 (45%)

Spread of ~9 points between good and bad saves. Buffs shift both equally, spread comes from prof tier + stat investment.

## Status vs Circumstance

- **Status**: Reliable but has cooldown/cost. Passive +1-2 always, big +4 for important fights.
- **Circumstance**: Situational - positioning, terrain, vs specific enemy types. No free flanking in this game, so can be slightly more generous with values.

## Monster AC

AC = base + dodge + item

- **Base**: From stat block, represents natural armor/training
- **Dodge**: From stat block, represents agility
- **Item**: From spawned equipment

Touch attacks ignore item. Flat-footed ignores dodge.

Target distribution: 50% at baseline DC, 25% higher, 25% lower (from equipment variance).

Spell DCs are baked into stat block, not affected by equipment.

## Design Principles

1. **Bounded within tier** - level cancels out vs same-level enemies, tier bonuses are the real progression
2. **Typed bonuses don't stack** - prevents runaway optimization
3. **Munchkin ceiling exists** - fully optimized player can auto-pass good saves (except nat 1), that's the reward
4. **Bad saves hurt** - 45% pass rate on weak save is intentional, specialize or suffer
5. **Circumstance is earned** - no free flanking, bonuses come from build choices or tactical play
