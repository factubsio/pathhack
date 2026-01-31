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
- [ ] More rooms
- [ ] Inclusions
- [x] Traps (one so far, pit, but more should be easy, not generated though)

## Spells
- [ ] Spell slots (level 1 only)
- [ ] Slot regen over time
- [ ] Cast command, spell menu
- [ ] One spell (cure light wounds)

## Economy
- [ ] Gold
- [ ] Shops

## Breadth - Consumables
- [ ] Potion of healing, quaff
- [ ] Scroll of teleport, read

## Warpriest - Fervor
- [ ] Fervor pool (Wis-based)
- [ ] Spend fervor to swift-cast self buff spell
- [ ] Fervor regen (slow or per-level)

## FOV & Memory
- [x] FOV/LOS
- [x] Remembered tiles

## Breadth - More Monsters
- [ ] Fourth monster
- [ ] Fifth monster
- [ ] Monster abilities (one that hits hard, one that's fast)

## Warpriest - Sacred Weapon Enhancement
- [ ] Spend fervor to enhance weapon (+1, or flaming, etc.)
- [ ] Duration-based buff

## ID Game
- [ ] Unidentified appearances
- [ ] ID on use
- [ ] Knowledge screen

## Breadth - More Items
- [ ] Wand (fire)
- [ ] Ring (protection)
- [ ] Third armor type

## Warpriest - Major Blessing
- [ ] Major blessing unlocks at level X
- [ ] Stronger active ability

## XP & Leveling
- [x] XP from kills
- [x] Level up
- [x] HP/stat increases
- [ ] New spell slot levels at appropriate levels

## Second Class - Oracle
- [ ] Oracle available at creation
- [ ] Mystery selection
- [ ] Curse applies
- [ ] First revelation

## Win Path
- [x] Get mcguffin from branch
- [x] take to final level (unlock door with it)
- [x] invoke on a specific tile
- [x] WINNER IS YOU

## More Level Gen
- [ ] Branches, caves?

## Iterate
- [ ] More blessings, mysteries, monsters, items, spells...
- [ ] Autopickup

## More More Level Gen
- [ ] More inclusions
- [ ] More templates

## Features
- [x] CellState has feature type, rendered
- [x] CellState has "message", displayed when move over
- [ ] Feature memory and farlook

## Monster tagging
- [ ] Monster templates, like zombie
- [ ] Tag monsters so spells can affect (sun blessing vs undead)

## LORE DUMPS
- [x] Much lore is dumped, uses rich text and full screen to make people ignore it more

## MISC
- [ ] door opening not always 100% success
- [ ] kicking (doors, monsters, items)
- [ ] chests (locked, trapped)
- [ ] altars/parayer
- [ ] fountains and other features
- [ ] water/lava
- [ ] ground fx (grease) ?
- [ ] launchers - add to PHContext?
- [ ] MORE traps
- [ ] generate traps
- [ ] disarm traps?
- [ ] shift move sucks still

