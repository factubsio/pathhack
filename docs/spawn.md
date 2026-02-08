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
