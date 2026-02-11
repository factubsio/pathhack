# Pathhack Development Plan

## Movement & Display
- [x] Player @ on screen
- [x] Move with hjkl/arrows
- [x] Multiple rooms, corridors
- [x] Doors (open/close)
- [x] Multiple levels, stairs
- [x] Branch stairs distinct (no branches yet though)

## First Combat
- [x] One monster (goblin), wanders
- [x] Bump to attack, HP, death
- [x] Monster attacks back
- [x] Player death, game over

## Stats Foundation
- [x] Str/Dex/Con/Int/Wis/Cha
- [x] Stats affect to-hit, damage, AC

## First Item
- [x] Weapon on ground
- [x] Pick up, inventory, drop
- [x] Equip weapon, affects damage

## Warpriest Foundation
- [x] Character creation screen
- [x] Pick class (warpriest only)
- [x] Deity selection (3-4 deities)
- [x] Sacred weapon - weapon damage scales with level

## Breadth - Monsters
- [x] Second monster (skeleton)
- [x] Third monster (rat)
- [x] Monsters vary by depth

## Breadth - Items
- [x] Armor, equip, affects AC
- [x] Second weapon type (dagger)

## Gameplay
- [x] [monster movement brain](Game/movement.md)

## Warpriest - Blessings
- [x] [Minor blessing](Game/Classes/blessings.md) - passive or small active
- [x] Blessing UI on status line

## Info
- [x] Character sheet (ctrl-x)
- [x] Pokedex for items
- [x] Pokedex for monsters

## Breadth - Dungeon
- [x] More monsters
- [x] Special rooms - goblin outpost.

## Progression
- [x] Exp gain
- [x] Level up screen - choices:
- [x] Pick one blessing (war, fire, strength)

## Breadth - Dungeon
- [x] Special levels, templates
- [x] More monsters
- [x] Traps (one so far, pit, but more should be easy, not generated though)

## Spells
- [x] Spell slots (level 1 only)
- [x] Slot regen over time
- [x] Cast command, spell menu
- [x] One spell (cure light wounds) [we also done burning hands and magic missile]

## Economy
- [x] Gold (silver crests)
- [x] Shops (shopkeeper, pricing, theft)

## Breadth - Consumables
- [x] Potion of healing, quaff (8 potions: healing, speed, paralysis, antivenom, omen, panacea, false life, lesser invisibility)
- [x] Scroll of teleport, read (4 scrolls: magic mapping, identify, teleportation, fire)

## Warpriest - Fervor
- [x] Fervor pool (Wis-based)
- [x] Spend fervor to swift-cast self buff spell
- [x] Fervor regen

## FOV & Memory
- [x] FOV/LOS
- [x] Remembered tiles

## Breadth - More Monsters
- [x] Fourth monster
- [x] Fifth monster
- [x] Monster abilities (fire breath, war chant, pounce, constrict, web spit, daze, TK projectile, etc.)

## Warpriest - Sacred Weapon Enhancement
- [x] Spend fervor to enhance weapon (+1, or flaming, etc.)
- [x] Duration-based buff

## ID Game
- [x] Unidentified appearances (potions, scrolls, rings, amulets, boots, gloves, cloaks)
- [x] ID on use
- [x] Knowledge screen (discoveries)

## Breadth - More Items
- [x] Ring (featherstep, spiritsight, grim, wild, ram)
- [x] Third armor type (6 armors: leather, chain shirt, hide, breastplate, splint, full plate)

## XP & Leveling
- [x] XP from kills
- [x] Level up
- [x] HP/stat increases
- [x] New spell slot levels at appropriate levels (MediumCaster table, pools granted via progression)

## Win Path
- [x] Get mcguffin from branch
- [x] take to final level (unlock door with it)
- [x] invoke on a specific tile
- [x] WINNER IS YOU

## More Level Gen
- [x] Branches (DungeonResolver, branch templates, crypt, trunau, end shrine)

## Features
- [x] CellState has feature type, rendered
- [x] CellState has "message", displayed when move over
- [x] Feature memory and farlook

## Monster tagging
- [x] Monster templates (skeleton, zombie)
- [x] Tag monsters so spells can affect (CreatureTypes, sun blessing vs undead)

## Food
- [x] Hunger system (satiated/normal/hungry/weak/fainting)
- [x] Eating (corpses, cooking quick/careful)
- [x] HUNGRY HUNGRY HIPPOS
- [x] Food poisoning

## Rune System
- [x] Weapon potency, striking, property runes (flaming/frost/shock)
- [x] Item generation tables by depth

## Afflictions
- [x] Affliction system (staged, ticking, saves)
- [x] Snake venom, spider venom, filth fever, food poisoning

## General Feats
- [x] Fleet, Toughness, Blind Fight, Feather Step, Trap Sense, Evasion, Power Attack, Reckless Attack

## Ancestries
- [x] They exist, they can be picked, there is no mechanical effect

## LORE DUMPS
- [x] Much lore is dumped, uses rich text and full screen to make people ignore it more

## MISC (done)
- [x] ground fx (grease, vomit)
- [x] launchers (bow/thrown weapon support — thrown works, bows have no ammo system yet)
- [x] generate traps
- [x] shift move sucks still

---

## TODO

### Warpriest - Major Blessing
- [ ] Major blessing unlocks at level X
- [ ] Stronger active ability

### Warpriest - Quest Path
- [ ] Class features/spells to CL11
- [ ] Shared meaty branch (1 of 9 pool, 3 per game)
- [ ] Warpriest-specific quest branch

### Second Class - Oracle
- [ ] Oracle available at creation
- [ ] Mystery selection
- [ ] Curse applies
- [ ] First revelation

### Level Gen
- [ ] Caves
- [ ] More rooms
- [ ] More inclusions
- [ ] More templates

### Iterate
- [ ] More blessings, mysteries, monsters, items, spells...
- [ ] Autopickup

### MISC
- [ ] door opening not always 100% success
- [ ] kicking (doors, monsters, items)
- [ ] chests (locked, trapped)
- [ ] altars/prayer
- [ ] fountains and other features
- [ ] water/lava
- [ ] MORE traps
- [ ] disarm traps?
- [ ] testing, we have done some stuff, hooked logicbricks, added some harness, early days
- [ ] wands (real ones, not dummy)
- [ ] ancestry racial abilities
- [ ] FactSystem.cs split (UnitSystem.cs)
- [ ] digging
- [ ] flying
- [ ] BUC (blessed/cursed) - flag exists, mechanics don't
- [ ] resistances (energy res is just DR for elements, immunity only from ancestry/artifacts)
- [ ] telepathy/ESP (DetectAllBuff or whatever, infra is basically there)
- [ ] stoning/petrification + carry-a-cure pressure
- [ ] instadeaths: drowning, lava (water blocks movement now, add bullrush to monsters and enjoy)
- [ ] corpse effects/intrinsics (cooking items: portable stove, adventurer's kit, alchemist's alembic for distilling)
- [ ] containers (bags, boh, bohexplosion obviously)
- [ ] wishing (the wish pipeline: outsider pickup → dispatcher → sorting → deity, bureaucracy scales with player standing)
- [ ] sacrifice (temples by alignment step? anti-scum somehow, not nh sac spam)
- [ ] throne rooms, zoos, more special room types
- [ ] pets/companions (do with druid first, summoner eidolon needs ui, cavalier tbd)
- [ ] polymorph/wildshape (CurrentForm on player, hang components off it, druid-gated)
