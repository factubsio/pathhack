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

## Activity System Refactor
- [x] Abstract Activity base class (name, targetItem, TotalTime, Tick, Interruptible, OnInterrupt)
- [x] EatActivity, CookQuickActivity, CookCarefulActivity as subtypes
- [x] DigActivity, DigDownActivity

## Digging
- [x] Wall diggable by default (Diggable flag), Undiggable on CellState
- [x] DigActivity targets a Pos, time based on tile type
- [x] Pickaxe as weapon with DiggerIdentity brick
- [x] Dig down (creates hole/trapdoor)
- [x] AutoDig config option

## Flying
- [x] Flying query ("flying") checked in CanMoveTo — water bypass
- [x] FlyLesserBuff (maintained L3 spell, slowed at 1 stack)
- [x] ElementalFlight brick for air/lightning elementals

## Helplessness
- [x] ParalyzedBuff queries "can_act" (not per-status "helpless")
- [x] StunnedBuff (can_act false)
- [x] FleeingBuff (monster AI)
- [x] Game loop checks Allows("can_act"), sets energy to 0

## Wands
- [x] WandDef wrapping a SpellBrickBase, charges, zap action
- [x] Magic missile, burning hands, cure light wounds, acid arrow, scorching ray

## Bottles
- [x] BottleDef wrapping a SpellBrickBase, thrown/applied
- [x] False life lesser, grease, sound burst, hold person, fireball, false life

## Spells — L2
- [x] Scorching ray, sound burst, hold person, dimension door
- [x] Resist fire/cold/shock/acid (maintained)
- [x] Delay poison (maintained, suppresses poison ticks)

## Spells — L1 additions
- [x] Command (will save or flee)
- [x] False life lesser (temp HP)
- [x] Protection from evil/good/law/chaos (maintained, +1 AC vs alignment)

## Spells — L3
- [x] Fireball, vampiric touch, fly lesser, heroism, false life
- [x] Protection from fire/cold/shock/acid (absorption pool)

## Rings & Accessories Expansion
- [x] Potency-scaling rings (protection, accurate strikes, energy resist)
- [x] Free action ring (paralysis/stun/web immunity)
- [x] Save advantage rings (reflex/fort/will)
- [x] Fast healing, teleportation curse, invisibility ring
- [x] Boots slot, gloves slot (feet/hands equip slots)
- [x] MagicBoots, MagicGloves collections

## BUC (Blessed/Uncursed/Cursed)
- [x] BUC enum on Item, RollBUC in ItemGen with bias support
- [x] Cursed items stick on equip (can't unequip)
- [x] Scroll of remove curse (uncurse equipped, blessed = uncurse all)
- [x] BUC knowledge flag, revealed on failed unequip or scroll use
- [x] Display in item names when known

## Elementals
- [x] Full elemental bestiary: air, earth, fire, water, ice, lightning, mud, magma
- [x] Three tiers each (small/medium/large)
- [x] Per-element abilities: whirlwind, bull rush, fire burn, water cone, numbing cold, spark zap, mud pool, lava puddle, magma trail
- [x] Elemental traits (immune bleed/paralysis/poison/sleep)
- [x] Element-specific immunities/resistances

## Monster Infighting
- [x] Monster.Hates() — monsters can target other monsters
- [x] AI scans for hated monsters as targets

## Rune Refactor
- [x] RuneDef → RuneBrick (runes are now LogicBricks directly, not wrapper objects)
- [x] Singleton rune instances (BonusRune.Q1-Q4, StrikingRune.Q1-Q4, ElementalRune.Flaming1-4 etc.)
- [x] Item.Fundamental and PropertyRunes now store Facts directly

## Item System Refactor
- [x] ItemDb extracted to own file
- [x] Wand appearance category
- [x] ItemKnowledge split: PropRunes, PropQuality, PropPotency (was single Props flag)
- [x] "enchanted" shown for items with unknown runes
- [x] Item.Charges for wands
- [x] VerbResponder / ItemVerb system for use-actions

## Source Generators
- [x] ItemCollectionGenerator: [GenerateAll] auto-generates All/RandomAll arrays + AppearanceIndex
- [x] BrickRegistrationGenerator: auto-registers brick instances in MasonryYard
- [x] BrickRegistrationAnalyzer: warns on missing Id, IsActive without round hooks, etc.

## Infrastructure
- [x] MasonryYard: brick/action/spell registry by Id (for monitor, serialization)
- [x] DungeonMaster: IUnit proxy for environmental damage/effects (replaces Monster.DM)
- [x] JsonBuilder: interpolated string handler for structured JSON logging
- [x] BrickStatsHook: hook call counter for profiling
- [x] Config system (.pathhackrc, NH-compatible OPTIONS= format)
- [x] PHMonitor: UDP-based test/debug monitor (spawn, cast, grant, inspect, query, kill)

## Economy
- [x] Typed shops (weapon, armor, potion, scroll, ring, general)
- [x] dNethack shopkeeper name pools per shop type
- [x] GenerateForShop per shop type
- [x] Shop display names

## Common Bricks Refactor
- [x] EnergyResist replaces SimpleDR for elemental resistance (scaling, per-type, immune tier)
- [x] FlatDR (DR2/5/10) for physical flat reduction
- [x] Thorns (fire/cold 1d4 retaliation)
- [x] MeleeDamageRider (shock 1d4)
- [x] ApplyFactOnAttackHit, GrabOnHit
- [x] CommonQueries constants (See, Paralysis, Stun, Web, Poison, Bleed, Sleep)
- [x] Query helpers: TrueWhen, FalseWhen, IntWhen, NumWhen
