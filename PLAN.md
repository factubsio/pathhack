# Pathhack Development Plan

See PLAN_DONE.md for completed features.

## Current Musings

### Autopickup
Config exists (`autopickup`, `pickup_classes` in .pathhackrc), AutoDig works.
Autopickup itself isn't wired into the move loop yet — just the config parsing.

---

## TODO — get to "fun loop" at CL12

two tracks: depth (warpriest to CL12) and breadth (world variety).
interleave so each run feels different while the build has real choices.

### Breadth — variety now
- [ ] throne rooms, zoos, more special room types
- [ ] chests (locked, trapped)
- [ ] fountains and other features

### Depth — Warpriest to CL12
- [ ] Major blessing unlocks at level X
- [ ] Stronger active ability
- [ ] class features/spells to CL12 (L1-L3 done, need more variety + L4?)
- [ ] more class feats, more general feats
- [ ] fervor polish (more interesting spend choices)
- [ ] major blessing effects (unlock at ~CL10)

### World pressure — interesting decisions
- [ ] stoning/petrification + carry-a-cure pressure
- [ ] instadeaths: drowning, lava (water blocks movement now, add bullrush to monsters and enjoy — bull rush exists on earth elementals now)
- [ ] more hunger (food pressure needs more teeth)
- [ ] altars/prayer
- [ ] water/lava (water passability done for flying, lava tiles don't exist yet)

### Warpriest quest path
- [ ] Shared meaty branch (1 of 9 pool, 3 per game)
- [ ] Warpriest-specific quest branch

### Second Class - Oracle (after wp feels complete)
- [ ] Oracle available at creation
- [ ] Mystery selection
- [ ] Curse applies
- [ ] First revelation

### Ongoing / as needed
- [ ] autopickup (config done, not wired into move loop)
- [ ] door opening not always 100% success
- [ ] kicking (doors, monsters, items)
- [ ] disarm traps?
- [ ] testing — monitor harness exists, spell tests written, need more coverage
- [ ] ancestry racial abilities
- [ ] FactSystem.cs split (UnitSystem.cs)
- [ ] flying: trap bypass not implemented yet (movement done, water done)
- [ ] BUC: mechanics beyond cursed-stick and remove curse (blessed potions, cursed scrolls, etc.)

### Later (big systems, park until needed)
- [ ] telepathy/ESP (DetectAllBuff or whatever, infra is basically there)
- [ ] corpse effects/intrinsics (cooking items: portable stove, adventurer's kit, alchemist's alembic for distilling)
- [ ] alchemy (dipping)
- [ ] containers (bags, boh, bohexplosion obviously)
- [ ] wishing (the wish pipeline: outsider pickup → dispatcher → sorting → deity, bureaucracy scales with player standing — debug #wish exists)
- [ ] sacrifice (temples by alignment step? anti-scum somehow, not nh sac spam)
- [ ] pets/companions (do with druid first, summoner eidolon needs ui, cavalier tbd)
- [ ] polymorph/wildshape (CurrentForm on player, hang components off it, druid-gated)
