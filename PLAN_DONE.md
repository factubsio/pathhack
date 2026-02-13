# Pathhack — Done

Completed features, moved from PLAN.md to reduce noise.

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
- [x] Monster movement brain

## Warpriest - Blessings
- [x] Minor blessing - passive or small active
- [x] Blessing UI on status line

## Info
- [x] Character sheet (ctrl-x)
- [x] Pokedex for items
- [x] Pokedex for monsters

## Breadth - Dungeon
- [x] More monsters
- [x] Special rooms - goblin outpost
- [x] Special levels, templates
- [x] Traps (pit, web, trapdoor, hole)

## Progression
- [x] Exp gain
- [x] Level up screen - choices
- [x] Pick one blessing (war, fire, strength)

## Spells
- [x] Spell slots (level 1 only)
- [x] Slot regen over time
- [x] Cast command, spell menu
- [x] Spells: cure light wounds, burning hands, magic missile, grease, acid arrow, light, shield
- [x] Maintained spells (light, shield — slot locking, dismiss)
- [x] Spell attacks (acid arrow)

## Economy
- [x] Gold (silver crests)
- [x] Shops (shopkeeper, pricing, theft)

## Breadth - Consumables
- [x] Potions (8: healing, speed, paralysis, antivenom, omen, panacea, false life, lesser invisibility)
- [x] Scrolls (4: magic mapping, identify, teleportation, fire)

## Warpriest - Fervor
- [x] Fervor pool (Wis-based)
- [x] Spend fervor to swift-cast self buff spell
- [x] Fervor regen

## Warpriest - Sacred Weapon Enhancement
- [x] Spend fervor to enhance weapon (+1, or flaming, etc.)
- [x] Duration-based buff

## FOV & Memory
- [x] FOV/LOS
- [x] Remembered tiles

## Breadth - More Monsters
- [x] Monster abilities (fire breath, war chant, pounce, constrict, web spit, daze, TK projectile, etc.)

## ID Game
- [x] Unidentified appearances (potions, scrolls, rings, amulets, boots, gloves, cloaks)
- [x] ID on use
- [x] Knowledge screen (discoveries)

## Breadth - More Items
- [x] Ring (featherstep, spiritsight, grim, wild, ram)
- [x] 6 armors (leather, chain shirt, hide, breastplate, splint, full plate)
- [x] Axes (hatchet, battleaxe, dwarven waraxe, greataxe, gandasa)
- [x] Rocks (throwable)

## XP & Leveling
- [x] XP from kills
- [x] Level up
- [x] HP/stat increases
- [x] New spell slot levels at appropriate levels (MediumCaster table)

## Win Path
- [x] Get mcguffin from branch
- [x] Take to final level (unlock door with it)
- [x] Invoke on a specific tile
- [x] WINNER IS YOU

## More Level Gen
- [x] Branches (DungeonResolver, branch templates, crypt, trunau, end shrine)
- [x] Dungeon overview UI
- [x] Caves, more rooms, more inclusions, more templates
- [x] Rivers and water tiles
- [x] Mini vaults

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

## Resistances
- [x] Energy resist (DR for elements, immunity only from ancestry/artifacts)

## MISC
- [x] ground fx (grease, vomit)
- [x] launchers (thrown works, bows have no ammo system yet)
- [x] generate traps
- [x] shift move sucks still
- [x] lore dumps
