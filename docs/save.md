(warnign: slop ahead, as most of the docs are, iiwii in this brave new world)
# Save/Load Design

## Approach

Manual binary (BinaryWriter/BinaryReader) first. Migrate to source generator later — the generator just automates what we're already doing by hand. Method signatures don't change.

AOT-safe. No reflection. No 3p libraries.

## What Gets Saved

### Player
- Stats, HP, XP, level, gold, hunger
- Inventory (items, equipment slots)
- Facts (brick id + fact.Data)
- TakenFeats, spell lists, charge pools
- Position, current level id

### Level (per visited level)
- Tile grid (type, flags)
- CellState per cell (features, messages)
- Traps (position → TrapType + PlayerSeen + depth)
- Items on ground (position → item list)
- Rooms (type, bounds, resident ref)
- Stairs/branch portal positions + targets
- Corpses (item + position + rot timer)
- Areas (ground fx)
- Monster list
- LastExitTurn

### Monsters (per level)
- Def reference (by id, not the whole def)
- HP, position, energy, inventory, equipment
- Facts + fact.Data
- AI state (peaceful, asleep, target, known traps)
- Grab state (grabbing/grabbed by — entity id cross-refs)

### Items (in inventories and on ground)
- Def reference (by id)
- Count, BUC, identified, potency, runes
- CorpseOf + RotTimer
- Facts + fact.Data

### Global
- Seed
- CurrentRound
- Branches (resolved structure — branch ids, depths, template placements)
- Which levels have been visited
- ActiveEntities
- ID game state (discovered appearances)

### Unvisited Levels
Not saved. Regenerated from seed + DungeonResolver output.

## Cross-References

Entities (units, items) already have unique uint ids via `Entity<TDef>`. Serialize references as ids, build lookup table on load, patch up pointers.

## Fact Serialization

### Brick Identity

Central registry: `Dictionary<string, LogicBrick>` populated at startup. Every brick instance gets a unique string id and is pre-registered — including parameterized ones (QueryBrick etc.) since they're all statically created in feat/item/monster definitions.

On save: write brick id + fact.Data.
On load: look up brick by id, reattach to fact.

Eagerly instantiate all bricks at startup. No lazy factories needed — bricks are tiny stateless objects, even 10k is sub-millisecond.

No static init ordering concerns as long as registry population is one explicit method call, not scattered across static initializers.

### Fact Data

LogicBrick data is typed through `LogicBrick<T> where T : class, new()`. This means:
- We can constrain T (e.g. `T : IGameSavable`) to force serialization support
- Source generator can enumerate closed `LogicBrick<T>` types and emit serialization

ActionBrick has a parallel untyped `CreateData` path, but only two data types exist: `CooldownTracker` and `DataFlag`. These are on reusable base classes, not per-ability. Small, contained problem.

Most actions return null from CreateData — stateless.

### Primitives in Data

For `LogicBrick<T>` where T is a primitive-wrapping type (ScalarData<int> etc.), serialization is trivial — just write the value.

## Future: Source Generator

The source generator would:
1. Scan for all `LogicBrick` subclasses and their `Instance` fields
2. Build the central registry automatically
3. Emit Save/Load methods for all fact data types
4. Emit Save/Load for entities, levels, etc.

Manual-first means we can ship save/load before the generator exists.
