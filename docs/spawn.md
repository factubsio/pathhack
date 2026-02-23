# Monster Spawning

## Spawn Timing

### Initial Spawns
- On level creation, each room has 50% chance to spawn one monster
- Skipped if `level.NoInitialSpawns` is set

### Runtime Spawns
- 1/70 chance per turn to spawn a monster
- Placement priority: out of LOS > out of sight > anywhere
- Never on stairs

### Catch-Up Spawns
- When returning to a level after `turnDelta` turns away
- Expected spawns = turnDelta / 70
- Actual spawns = 30% of expected (CatchUpRate)

## Monster Selection

`PickMonster(depth, playerLevel)`:
- maxLevel = (depth + playerLevel) / 2
- Candidates: monsters where depth >= MinDepth AND BaseLevel <= maxLevel
- Weighted random by SpawnWeight

## Templates

- 10% of spawned monsters get a template (zombie, skeleton, etc.)
- Template must pass `CanApplyTo(def)` check

## Group Spawning

Triggered after placing a monster if `GroupSize != None`.

### GroupSize Values
- `None` - no group
- `Small` - 50% chance of 1-3 extras
- `SmallMixed` - same as Small, but picks from same family (±2 levels)
- `Large` - 66% chance of 1-10 extras, 33% chance of 1-3
- `LargeMixed` - same as Large, but picks from same family

### Group Size Reduction
At low player levels:
- Level < 3: count = (count + 3) / 4
- Level < 5: count = (count + 1) / 2

### Placement
Groups spawn in adjacent empty passable tiles around the leader.

## Target Distribution

Aim for ~15% of monsters to have group flags, of which ~20% should be LGROUP.

## Future: Family Bag System

Current system picks a monster def directly from the weighted pool. Families with many defs (elementals: 24, mephits: 8, dragons: 20) dominate because each def contributes its full SpawnWeight independently. A family with 8 defs at weight 10 gets 80 total weight vs a single-def family's 10.

### Proposal

Replace direct weighted pick with a two-phase system:

1. Draw a **family** from a shuffle bag
2. Pick a **monster** within that family (by SpawnWeight, filtered by level eligibility)

The bag contains N tokens per family, where N controls relative spawn frequency. Bag refills when ~70% depleted — overlap between old and new tokens keeps draws feeling random while maintaining distribution guarantees.

### Properties

- Anti-streak: a family can only appear N times per bag cycle, no matter how many defs it has
- Tunable: family frequency is token count, not def count × weight
- No linearity: 70% refill threshold prevents predictable "bag is almost empty" patterns
- Serializable: bag state is just a list of remaining family names
- Per-level bags: special levels (jungle, crypt, etc.) configure different token distributions instead of writing custom `ILevelRuntimeBehaviour` spawn logic

### Example

Default bag: `[goblin, goblin, kobold, spider, spider, snake, dragon, elemental, mephit, troll, ...]`

Jungle underneath bag: `[mephit, mephit, mephit, elemental, elemental, charau-ka, charau-ka, bandit, bandit, ...]`

Most `ILevelRuntimeBehaviour.PickMonster` overrides become bag configurations rather than code.
