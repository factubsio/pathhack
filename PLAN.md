# Pathhack Development Plan

See PLAN_DONE.md for completed features.

## Current Musings

### Activity system refactor
Activity in FoodSystem.cs is really an EatActivity wearing a trenchcoat. Needs to
be a base class so digging, lockpicking, prayer etc. can use it.

- `abstract class Activity(string name, Item? target = null)` base
- `public Item? Target => target;` exposed for rot-pause check
- `abstract int TotalTime`, `abstract bool Tick()`, `virtual bool Interruptible`
- EatActivity, CookQuickActivity, CookCarefulActivity, DigActivity as subtypes
- consumer in PlayerTurn doesn't change, just calls Tick()
- nethack doesn't save occupations, we don't have save/load yet, don't worry about it

### Digging
- Wall should be Diggable by default (add to DefaultFlags)
- Undiggable override goes on CellState (mutable per-cell, not flags)
- DigActivity targets a Pos, takes turns based on tile type
- pickaxe is a weapon with a brick that says "I'm a digger"
- monster digging is just a movement flag (dnh: `tunnels(ptr)` + `ALLOW_DIG`), park for now
- wand of digging: line vs single tile, decide later
- dig down (trapdoor/hole) is bigger scope, later

### Flying
- CreatureTags.Flying exists, never referenced
- CanMoveTo already has `// TODO: phasing, swimming, flying` comment
- Water is non-passable, flying bypasses
- Pit/web traps: flying skips trigger
- tiny feature, basically a bug fix

### Helplessness (paralysis/sleep/etc)
- each status is its own brick, all respond to `"helpless"` query
- game loop checks `Has("helpless")` once, not per-status
- message on application only, not per turn (current per-turn "You are paralyzed!" is bad)
- on removal: check if still helpless for compound message
- paralysis: uses Timed wrapper (never interrupted, independent stacking is fine)
- sleep: NO Timed wrapper, just `AddFact(SleepBuff.Instance, duration: N)` with ExtendDuration
  - damage removes the fact directly, no wrapper coupling problem
  - Timed wrapper is for stuff that can be both permanent and temporary (blind from blindfold vs poison)

---

## TODO — get to "fun loop" at CL12

two tracks: depth (warpriest to CL12) and breadth (world variety).
interleave so each run feels different while the build has real choices.

### Breadth — variety now
- [ ] throne rooms, zoos, more special room types
- [ ] wands (real ones, not dummy)
- [ ] chests (locked, trapped)
- [ ] fountains and other features

### Depth — Warpriest to CL12
- [ ] Major blessing unlocks at level X
- [ ] Stronger active ability
- [ ] class features/spells to CL12 (medium caster, should get L3 slots)
- [ ] more spells (fill out lists to L3)
- [ ] more class feats, more general feats
- [ ] fervor polish (more interesting spend choices)
- [ ] major blessing effects (unlock at ~CL10)

### World pressure — interesting decisions
- [ ] BUC (blessed/cursed) - flag exists, mechanics don't
- [ ] stoning/petrification + carry-a-cure pressure
- [ ] instadeaths: drowning, lava (water blocks movement now, add bullrush to monsters and enjoy)
- [ ] more hunger (food pressure needs more teeth)
- [ ] altars/prayer
- [ ] water/lava

### Warpriest quest path
- [ ] Shared meaty branch (1 of 9 pool, 3 per game)
- [ ] Warpriest-specific quest branch

### Second Class - Oracle (after wp feels complete)
- [ ] Oracle available at creation
- [ ] Mystery selection
- [ ] Curse applies
- [ ] First revelation

### Ongoing / as needed
- [ ] Autopickup
- [ ] door opening not always 100% success
- [ ] kicking (doors, monsters, items)
- [ ] disarm traps?
- [ ] testing, we have done some stuff, hooked logicbricks, added some harness, early days
- [ ] ancestry racial abilities
- [ ] FactSystem.cs split (UnitSystem.cs)

### Later (big systems, park until needed)
- [ ] digging
- [ ] flying
- [ ] telepathy/ESP (DetectAllBuff or whatever, infra is basically there)
- [ ] corpse effects/intrinsics (cooking items: portable stove, adventurer's kit, alchemist's alembic for distilling)
- [ ] containers (bags, boh, bohexplosion obviously)
- [ ] wishing (the wish pipeline: outsider pickup → dispatcher → sorting → deity, bureaucracy scales with player standing — debug #wish exists)
- [ ] sacrifice (temples by alignment step? anti-scum somehow, not nh sac spam)
- [ ] pets/companions (do with druid first, summoner eidolon needs ui, cavalier tbd)
- [ ] polymorph/wildshape (CurrentForm on player, hang components off it, druid-gated)
