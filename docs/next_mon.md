# Next Monsters — Stream of Thought

## What We Have (Mechanics Inventory)

### Existing Creature Types
humanoid, beast, undead, construct, elemental, outsider, aberration, dragon, fey, plant, ooze, giant
— most of these are DEFINED but only humanoid, beast, plant, construct are actually used by monsters.

### Existing Tags
fire, cold, air, earth, water, acid, incorporeal, swarm, aquatic, flying, mindless
— flying and incorporeal are defined but NOT mechanically referenced yet.

### Existing Subtypes
demon, devil, angel, shapechanger, goblinoid, orc, elf, dwarf, giant
— none used by actual monsters yet.

### Existing Damage Types
phys: slashing, piercing, blunt
elem: fire, cold, shock, acid
magic, force
spirit: holy, unholy
poison

### Existing DR System
SimpleDR with ramp (5/10/15/20 by level) — slashing, blunt, piercing, silver, adamantine, cold_iron, good, evil, chaotic, neutral
ComplexDR for multi-bypass (or/and)
ProtectionBrick for absorb-shield style protection

### Existing Monster Abilities (reusable)
- AttackWithWeapon — melee with equipped weapon
- NaturalAttack — melee with natural weapon
- FullAttack — multi-attack sequence (cats)
- Pounce — leap + full attack (cats)
- QuickBite — fast cheap attack (cheetah)
- FireBreath — cone AoE, reflex save
- WarChant — buff allies in range
- WebSpit — ranged web trap
- Daze — will save or lose turn
- TelekineticProjectile — ranged attack from nearby tile
- GrabOnHit — grab on successful melee
- Constrict — auto-damage grabbed target each round
- Thorns — damage attacker on hit
- PhaseShift — 50% miss chance
- Ferocity — survive first lethal blow at 1 HP

### Existing Status Effects
- Blind, Prone, Silenced, Paralyzed, Nauseated, Dazed
- Affliction system (staged, ticking, saves): snake venom, spider venom, filth fever, food poisoning

### Existing Infrastructure
- ApplyFactOnAttackHit — apply any buff/debuff on hit
- ApplyAfflictionOnHit — affliction with save on hit
- GrantPool/GrantAction — resource pools with regen
- CooldownAction — actions with cooldown timers
- Timed() wrapper — duration-based facts
- WhenEquipped wrapper — only active when equipped
- QueryBrick — simple query responses
- SimpleDR/ComplexDR — damage reduction
- WeaponDamageRider — add elemental damage to weapon attacks
- Equip/EquipSet — spawn with equipment
- SayOnDeath/DropOnDeath — death triggers
- GrowsInto — monster evolution at depth

### What's Easy (no new systems needed)
- Any monster that uses existing natural attacks + existing abilities
- Monsters with DR (skeletons already use it)
- Monsters with elemental damage riders on attacks
- Monsters with afflictions on hit (venom pattern)
- Monsters with breath weapons (FireBreath is parameterized)
- Monsters with daze/silence/paralyze on hit
- Monsters with grab + constrict
- Monsters with pounce/full attack
- Monsters with equipment (orc pattern)
- Monsters with charge pools + cooldown actions
- Stationary monsters (thorn bush pattern)
- Group spawning monsters

### What Needs Small Additions
- Cold/acid/shock breath (FireBreath is fire-only in animation, needs parameterization)
- Energy resistance (ProtectionBrick exists but as a buff, need permanent version or just OnQuery for "resist_fire" etc)
- Regeneration (need a regen brick that heals each round, stopped by specific damage types)
- Level drain / XP drain (new mechanic, but small — just subtract XP or effective level)
- Steal items (new action — take random item from inventory)
- Engulf (new action — swallow target, damage per round until escape)

### What Needs Bigger Work
- Incorporeal (attacks pass through, need magic/force/ghost touch to hit — needs CanMoveTo changes too)
- Spellcasting monsters (need monster spell lists, AI to pick spells)
mcw: spellcasting is just actionbricks, ther eisn't massive difference other than MAYBE SILENCE? if we can re-use player spells that's great, some may be harder tha nothers
- Gaze attacks (passive — trigger on seeing, not on hit)
- Shapechanging (doppelganger — change appearance)
mcw: this is much later
- Summoning (spawn allies mid-combat)
- Aura effects (affect all units in radius each round)

---

## Monster Proposals by Tier

### Tier 1: Levels 0-3 (early dungeon, fill variety)


## Glyphs In Use
s = spiders, S = snakes, o = orcs, f = cats, g = goblins, m = gremlins
q = hippos, h = derro, k = kobolds, r = rat, { = plant, @ = player

mcw: note goblins should and will eventually be `o`, mitflits and gremlins will become g, freeing up `m`

Available (nethack convention where applicable):
b = bat, B = blob/ooze, d = dog/canine, D = dragon, e = elemental
E = eel, F = fungus/mold, G = gnome/giant?, H = giant, i = imp
j = jelly, J = jabberwock, l = leprechaun, L = lich, n = nymph
O = ogre, p = piercer, P = pudding, R = rust monster, t = trapper/mimic
T = troll, u = umber hulk, U = unicorn, v = vortex, V = vampire
w = worm, W = wraith/wight, x = xan, X = xorn, y = yeti
z = zombie, Z = ghoul, ' = golem, & = demon/devil, ; = eel/aquatic

---

## NEW SYSTEM: Regeneration (needed for trolls, vampires, some undead)

This is the one "hard" system that unlocks the most monsters. A LogicBrick that:
- Heals N HP per round
- Stopped by specific damage types (fire for trolls, fire+acid for some)
- Track "took fire this round" via OnDamageTaken, clear on OnRoundEnd
- If regen stopped, monster stays dead; if not, revives after N rounds
mcw: agreed, I think this is one we wshould try to do now we have corpses (we may have to force forpse for troll?)

This is a GATE. Trolls, vampires, and several other monsters need it.
Implementation: ~30 lines. One brick, parameterized.

---

## NEW SYSTEM: Energy Resistance (permanent, not buff)

ProtectionBrick exists as a consumable absorb-shield. Need a permanent version:
- OnQuery for "resist_fire" etc returning int
- OnBeforeDamageIncomingRoll: reduce matching damage by resist amount
- Simple LogicBrick, parameterized by type and amount
mcw: don't know why we need a permanent? if it's flat reductonow e use DR?

This is also a gate — elementals, dragons, demons all need it.
Implementation: ~15 lines.

---

## NEW SYSTEM: Drain Life (for wights, wraiths, vampires)

On hit: fort save or lose max HP (or XP, or effective level — TBD).
The nethack approach (level drain) is brutal. PF2e uses "drained" condition.
For us: probably "drained N" status that reduces max HP by N * some_amount.
Stacks. Cured by restoration or similar.
mcw: yeah let's noodle cos nh style exp drain is important i think, keep player honest

This is the undead gate. Wights, wraiths, vampires, specters all use it.
Implementation: ~40 lines (new affliction-like brick + DrainedBuff).

---

## PROPOSALS — Tier by Tier

### TIER 1: Levels 0-3 (fill early variety)

#### Bat family (glyph: b)
PF: bat, giant bat, vampire bat
dNH: bat (L0), giant bat (L2), vampire bat (L5)

- **bat** L0, Tiny, beast, fast (LandMove 6), low damage (d3), Flying tag
  - EASY: just a MonsterDef. Flying tag exists but isn't mechanically checked yet.
  - Even without flying mechanics, fast + low damage = annoying early pest. Good.
  - When flying IS implemented, bats retroactively become more interesting.

- **giant bat** L2, Small, beast, fast, d6 bite
  - Grows from bat.

- **vampire bat** L5, Small, beast, fast, d6 bite + drain (needs drain system)
  - LATER, after drain system exists.

  mcw: good test for flying

Verdict: bat + giant bat are trivial to add NOW. vampire bat waits for drain.

#### Fungus/Mold family (glyph: F)
PF: yellow musk creeper, violet fungus, phantom fungus
dNH: brown mold (passive cold), red mold (passive fire), yellow mold (passive stun), green mold (passive acid), shrieker (alerts monsters)

- **brown mold** L1, Small, plant, Stationary, passive cold damage when hit (Thorns pattern but cold)
  - TRIVIAL: copy thorn bush, change damage type to cold.

- **yellow mold** L1, Small, plant, Stationary, passive: fort save or nauseated when adjacent
  - EASY: needs an aura-on-adjacent check. Could do in OnRoundStart — check if player adjacent, apply Nauseated.
  - Actually: could be a "stench" brick. Reusable for ghasts, troglodytes, etc.

- **shrieker** L1, Small, plant, Stationary, no attack, but when it sees player it "shrieks" — wakes up sleeping monsters, alerts wandering ones
  - MEDIUM: needs a "noise" concept. Could just set ApparentPlayerPos on all monsters in range.
  - Very nethack. Very good. Teaches player about noise/stealth.

- **violet fungus** L3, Small, plant, Stationary, d4 touch + poison (use existing affliction system)
  - EASY: stationary + NaturalAttack + AfflictionOnHit with a new "fungal rot" affliction.

Verdict: brown mold is trivial. Yellow mold needs a small "adjacent aura" pattern. Shrieker needs noise concept (small but new). Violet fungus easy.
mcw: these are nh not pf?

#### Ooze family (glyph: j for jelly, P for pudding, B for blob)
PF: gray ooze (acid + grab + constrict), sewer ooze, slime mold
dNH: acid blob (passive acid), gray ooze (rust), ochre jelly (acid engulf), gelatinous cube (paralyze engulf)

- **slime** L1, Small, ooze, Mindless, slow (LandMove 18+), d4 acid touch
  - TRIVIAL: basic monster with acid damage type. CreatureType = Ooze.
  - DR piercing+slashing (oozes resist cuts). Max damage from phys (swarm-like).

- **gray ooze** L4, Medium, ooze, Mindless, slow, d6+acid, grab+constrict
  - EASY: GrabOnHit + Constrict already exist. Add acid damage rider.
  - Special: acid damages metal equipment. This is the "rust" equivalent.
  - MEDIUM: equipment damage is a new mechanic. Could start simple: % chance to degrade armor AC by 1 on hit.
  mcw: let's assume we will have dmg but we don't NEED it to gate this guy?

- **gelatinous cube** L5, Large, ooze, Mindless, slow, paralyze on touch, engulf
  - HARDER: engulf is a new action type. But paralyze-on-hit is just ApplyFactOnAttackHit(ParalyzedBuff).
  - Could start WITHOUT engulf — just paralyze on hit + acid damage. Still scary.
  - Transparent: hard to see until adjacent. Could use a "stealth" equivalent for monsters.
  mcw: yeah we want englufl and transparent , will have to put on list of features

mcw: again confused not pf? 

Verdict: slime is trivial. Gray ooze easy if we skip equipment damage (add later). Gelatinous cube medium — skip engulf for now, paralyze+acid is enough.

#### Bugbear (glyph: H or h)
PF: bugbear warrior (L2), bugbear thug, bugbear tormentor
dNH: bugbear (L3)

- **bugbear** L3, Medium, humanoid (goblinoid), equipped with flail or morningstar
  - TRIVIAL: just an orc-pattern humanoid. Bigger than goblins, smaller than ogres.
  - Could share 'g' glyph (goblinoid) or use 'H' for large humanoids.

#if bugbear is a large or a medium? do we want a bugbear family?

Verdict: trivial, good filler for L3 gap.

### TIER 2: Levels 4-7 (mid dungeon, introduce new threat types)

#### Ogre family (glyph: O)
PF: ogre warrior (L3), ogre glutton (L4), ogre boss (L7)
dNH: ogre (L5), ogre lord (L7), ogre king (L9)

- **ogre** L4, Large, giant, equipped with greatclub, high damage, low AC, slow
  - EASY: orc pattern but Large, CreatureType = Giant. First giant-type monster.
  - Subtypes = [Giant]. Uses existing equipment system.

- **ogre brute** L6, Large, giant, higher stats, maybe throws rocks
  - EASY if melee only. Thrown rocks need launcher system (exists for bows).

- **ogre boss** L8, Large, giant, group leader, war chant equivalent?
  - EASY: copy war chant pattern from goblins.

  mcw: we need more in here, maybe casters? rock throwers?

Verdict: ogre family is trivial to add. Fills the L4-8 gap perfectly.

#### Troll family (glyph: T) — NEEDS REGEN SYSTEM
PF: forest troll (L5), ice troll, cavern troll, jotund troll
dNH: troll (L7), ice troll (L9), rock troll (L10), water troll (L11)

- **troll** L6, Large, giant, claw/claw/bite full attack, REGENERATION (stopped by fire+acid)
  - NEEDS: Regeneration brick. This is the poster child.
  - Once regen exists: FullAttack + Regen brick. Done.
  - DR: none, but high HP regen makes them tanky.
  - Weakness to fire is the TEACHING MOMENT — player learns fire matters.

- **ice troll** L8, Large, giant, cold damage on claw, regen stopped by fire+acid
  - Same as troll + cold damage rider + cold resistance.

- **troll berserker** L10, Large, giant, higher damage, regen
  - Stat bump.

Verdict: troll family is EASY once regen exists. Regen is the gate. Regen unlocks trolls, vampires, and several other monsters. HIGH PRIORITY system.

#### Ghoul/Ghast family (glyph: Z)
PF: ghast (L2), ghoul soldier (L3), ghoul stalker (L5)
dNH: ghoul (L3) — paralyze on hit

- **ghoul** L3, Medium, undead, claw/claw/bite, PARALYZE on hit (fort save)
  - EASY: ApplyFactOnAttackHit(ParalyzedBuff.Instance.Timed(), duration: 3) with a fort save wrapper.
  - Actually: need a "save-or-apply" pattern. ApplyAfflictionOnHit does this for afflictions.
  - For simple status: new brick "SaveOrApplyOnHit(buff, saveKey, dc, duration)"
  - OR: just make a GhoulParalysis affliction. Simpler, more consistent.

  mcw: i think weneed to make paralyze self stacking with extend, not using timed wrapper, van't imagine we will evenr have a perma source of it

- **ghast** L5, Medium, undead, same as ghoul but stronger + stench aura
  - Stench: adjacent creatures must save or be nauseated. Same pattern as yellow mold aura.
  - Reusable: troglodyte, otyugh, etc.

- **ghoul stalker** L7, Medium, undead, faster, sneak attack equivalent
  - Bonus damage when target is paralyzed/helpless. New brick but small.

Verdict: ghoul is easy (paralyze on hit). Ghast needs stench aura (shared with mold). Good undead entry point — doesn't need drain system.

#### Wight family (glyph: W) — NEEDS DRAIN SYSTEM
PF: wight (L3), cairn wight (L4), hunter wight (L7), prowler wight (L9)
dNH: barrow wight (L3) — drain + spells

- **wight** L5, Medium, undead, equipped with weapon, DRAIN LIFE on hit
  - NEEDS: Drain system. Fort save or drained 1.
  - Once drain exists: just ApplyDrainOnHit brick.
  - Wights use weapons (like orcs) — equipment system works.

- **cairn wight** L7, Medium, undead, drain + funereal dirge (fear aura, cooldown action)
  - Dirge: will save or frightened. New "FrightenedBuff" needed (penalty to attacks/saves).
  - MEDIUM: frightened is a new status but simple.

Verdict: wights need drain system. Once that exists, they're easy. Cairn wight also needs frightened status.
#mcw: we wan tto do wider in the family, and ideally differentiate a LITTLE bit within family if we can, if not is ok

#### Wraith (glyph: W)
PF: wraith (L6)
dNH: wraith (L6) — drain + incorporeal

- **wraith** L7, Medium, undead, incorporeal, drain on touch
  - NEEDS: drain system + incorporeal
  - Incorporeal is HARD: need magic/force/ghost touch to hit, can move through walls
  - Could do a "partial incorporeal" — 50% miss chance (like phase spider) + drain
  - That's actually fine for now. Full incorporeal later.

Verdict: wraith with phase-shift + drain is doable once drain exists. Full incorporeal is later.
mcw: sure, i consider him a wight really

### TIER 3: Levels 8-12 (late-mid, serious threats)

#### Vampire family (glyph: V) — NEEDS DRAIN + REGEN
PF: vampire servant (L2), vampire count (L6), vampire mastermind (L9)
dNH: vampire (L10), vampire lord (L15)

- **vampire spawn** L7, Medium, undead, fast, claw + grab + drain blood
  - Drain blood: on grabbed target, heal self + drain target. Needs drain + regen.
  - DR 5/silver (already exists!)
  - Fast (LandMove 8 or so)

- **vampire** L10, Medium, undead, fast, claw + grab + drain + dominate gaze
  - Dominate gaze: will save or confused/charmed. New mechanic.
  - HARDER: gaze attacks are a new pattern (passive, triggers on LOS)

- **vampire lord** L14, Medium, undead, all of above + spellcasting
  - LATER: needs monster spellcasting.

Verdict: vampire spawn is doable once drain+regen exist. Full vampire needs gaze. Lord needs spells. Progression path.
#mcw: ondeath to bat then res? (once?) (chance?)

#### Golem family (glyph: ')
PF: wood golem (L6), flesh golem (L8), clay golem (L10), stone golem (L11), iron golem (L13)
dNH: paper golem (L3), straw golem (L3), rope golem (L4), leather golem (L6), wood golem (L7), flesh golem (L9), clay golem (L11), stone golem (L14)

- **wood golem** L6, Medium, construct, Mindless, DR/adamantine, immune to most magic
  - EASY: SimpleDR.Adamantine + QueryBrick("mindless", true)
  - "Immune to most magic" — could be high magic resistance or specific immunity
  - For now: just high DR + mindless. Magic immunity is a bigger system.

- **flesh golem** L8, Large, construct, DR/adamantine, healed by electricity, vulnerable to fire
  - MEDIUM: "healed by electricity" is a new mechanic (OnBeforeDamageIncomingRoll, convert shock to healing)
  - Vulnerability to fire: double fire damage (zombie template already does this!)

- **stone golem** L11, Large, construct, high DR/adamantine, slow, very high damage
  - EASY once wood golem pattern exists. Just bigger numbers.

- **iron golem** L13, Large, construct, highest DR, poison breath, immune to most
  - Poison breath: parameterized breath weapon (like FireBreath but poison)

mcw: i am sure there are a million more pf golems than this

Verdict: wood golem is easy. Flesh golem needs "healed by element" (small new brick). Stone/iron are stat bumps. Golems showcase the DR system beautifully.

#### Giant family (glyph: H)
PF: hill giant (L7), stone giant (L8), frost giant (L9), fire giant (L10), cloud giant (L11), storm giant (L13)
dNH: similar progression

- **hill giant** L7, Huge, giant, equipped with greatclub, throws rocks
  - EASY: ogre pattern but bigger. Rock throwing needs ranged attack (launcher exists).

- **stone giant** L9, Huge, giant, higher AC (stone skin = DR?), throws rocks
  - EASY: stat bump + DR.

- **frost giant** L10, Huge, giant, cold damage on weapon, cold resistance
  - NEEDS: energy resistance (small system). WeaponDamageRider for cold.

- **fire giant** L11, Huge, giant, fire damage, fire resistance
  - Same pattern as frost but fire.

mcw: where cloud and storm? rock throwing?

Verdict: hill giant is trivial. Stone giant easy. Frost/fire need energy resistance system (small gate).

### TIER 4: Levels 13-20 (endgame, big threats)

These are further out but worth noting what they need:

#### Demon/Devil families (glyph: &)
- **dretch** L3, Small, outsider/demon, stinking cloud ability
- **babau** L6, Medium, outsider/demon, sneak attack, acid blood (thorns but acid)
- **vrock** L9, Large, outsider/demon, screech (stun), spores
- **hezrou** L11, Large, outsider/demon, stench + grab + high damage
- **marilith** L17, Large, outsider/demon, 6 weapon attacks (FullAttack on steroids)

- **lemure** L1, Small, outsider/devil, mindless, regen (stopped by good/silver)
- **imp** L3, Tiny, outsider/devil, flying, poison sting, invisible
- **bearded devil** L5, Medium, outsider/devil, glaive, infernal wound (bleed)
- **bone devil** L9, Large, outsider/devil, fear aura, sting + poison

Most demons/devils need: energy resistance, DR/good or DR/silver, some need flying.
Higher ones need spellcasting.

Verdict: dretch and lemure are easy entry points. Babau needs acid-blood (Thorns variant). Most others need energy resistance at minimum.


#mcw: defer, we will do a demon and devil pass later

#### Lich (glyph: L)
- **lich** L12+, Medium, undead, spellcaster, phylactery
  - NEEDS: monster spellcasting system (BIG gate)
  - Phylactery: respawns after death unless phylactery destroyed (quest mechanic)
  - This is endgame content. Park it.

mcw: template? or?

#### Dragon family (glyph: D)
- Way too early. Need breath weapons (have fire, need others), flying, energy resistance.
- Young dragons could appear L10+ as rare encounters.
- Park for now.

mcw: why too early? pf has tons of dragon stages?

### ADVENTURER PARTIES (glyph: @)

These are "player character" monsters — stereotypes of PF classes. They appear as hostile adventuring parties. Key design:
- Use @ glyph with different colors
- CreatureType = Humanoid
- Equipped with class-appropriate gear
- Have 1-2 signature abilities from their class
- NEGATIVE modifiers to HP/damage because they appear in groups of 2-4
- Spawn as SmallMixed groups with "adventurer" family

Proposed party members:

- **sellsword** L4, Medium, humanoid, equipped longsword+shield, Power Attack equivalent
  - The "fighter". High AC, moderate damage. Boring but solid.
  - TRIVIAL: orc pattern with better equipment.

- **hedge wizard** L4, Medium, humanoid, equipped staff, casts magic missile + one other
  - NEEDS: monster spellcasting (even simple version — just 1-2 hardcoded spells as actions)
  - Could fake it: GrantAction(MagicMissileAction) where MagicMissileAction is a CooldownAction
  - Actually this IS how we'd do it. Monster "spells" are just actions with cooldowns.

- **cutpurse** L4, Medium, humanoid, equipped daggers, fast, steal items
  - NEEDS: steal item action (take random item from player inventory)
  - MEDIUM: new action, but straightforward.

- **zealot** L4, Medium, humanoid, equipped mace, heals allies (CooldownAction)
  - Heal allies action: pick lowest HP ally in range, heal them.
  - MEDIUM: new action pattern but uses existing DoHeal.

- **wandering monk** L5, Medium, humanoid, unarmed, fast, full attack (fist/fist/kick)
  - EASY: FullAttack with fist weapons. Fast movement.

- **bounty hunter** L6, Medium, humanoid, equipped crossbow + net
  - Net = web equivalent. Crossbow = ranged weapon.
  - EASY: existing systems.

mcw: these are not pf calsses? you can use 1e if y9ou want

These would spawn as mixed groups: 2-4 members, random composition from the pool.
They'd be a recurring threat type that teaches the player about class abilities.

The key balance lever: they have LOW HP (HpPerLevel = 4-5) and NEGATIVE damage bonuses.
They're glass cannons in groups. Kill the healer first, etc.

mcw: glass cannon with NEGATIVE damage bonus??? (i.e. they ar enot glass cannons they are more a troop)

---

## PRIORITY ORDER (what to implement when)

### Wave 1: No new systems needed (can do RIGHT NOW)
1. **bat** + **giant bat** — new family, fills L0-2, fast flyer archetype
2. **brown mold** — stationary cold damage, trivial (thorn bush clone)
3. **bugbear** — L3 humanoid filler, trivial
4. **ogre** + **ogre brute** — new family, fills L4-6, first Giant type
5. **slime** — first ooze, L1, slow + acid, trivial
6. **gray ooze** — L4, grab+constrict+acid, uses existing mechanics
7. **wood golem** — L6, first real construct, DR/adamantine showcase
8. **hill giant** — L7, first true giant, rock throwing

mcw: where antoch thingies? and cyclopses

That's 8 new monsters, 4 new families, using ZERO new systems.

### Wave 2: Small new systems (energy resistance, regen, stench aura)
9. **Regeneration brick** — ~30 lines, unlocks trolls + vampires
10. **Energy Resistance brick** — ~15 lines, unlocks elementals + giants + demons
11. **Stench/Aura brick** — ~20 lines, unlocks ghasts + troglodytes + yellow mold
12. **troll** + **ice troll** — regen showcase
13. **ghoul** + **ghast** — paralyze + stench, first scary undead
14. **frost giant** + **fire giant** — energy resistance showcase
15. **yellow mold** — stench aura
16. **violet fungus** — stationary + poison
17. **flesh golem** — healed-by-element variant

### Wave 3: Drain system (unlocks wights, wraiths, vampires)
18. **Drain Life system** — ~40 lines
19. **wight** — drain on hit, equipped undead
20. **wraith** — drain + phase shift (fake incorporeal)
21. **vampire spawn** — drain + regen + fast + DR/silver
22. **cairn wight** — drain + fear dirge

### Wave 4: Adventurer parties + misc
23. **Monster "spell" actions** — magic missile, heal ally, etc. as CooldownActions
24. **sellsword** + **hedge wizard** + **cutpurse** + **zealot**
25. **wandering monk** + **bounty hunter**
26. **shrieker** — noise concept
27. **stone golem** + **iron golem** — stat bumps

### Wave 5: Bigger systems (later)
28. Incorporeal (full)
29. Monster spellcasting (real)
30. Gaze attacks (passive LOS trigger)
31. Demons/devils (need energy resist + DR/alignment)
32. Dragons (need breath variety + flying + energy resist)
33. Lich (needs spellcasting + phylactery)

mcw: is gaze trivial?

---

## NOTES ON SPECIFIC DESIGN DECISIONS

### Ooze immunities
PF oozes are immune to: critical hits, mental, precision, visual.
For us: Mindless tag (already exists, blocks daze etc), immune to crits (new query "crit_immune"?),
and "no vision" (motion sense only — interesting but complex).
Start simple: just Mindless + DR slash/pierce + slow.

mcw: let's not say crit  but precision damage, more correct
mcw: no vision probably simple

### Undead immunities
All undead should be immune to: poison, disease, sleep, bleed.
We partially have this via templates (zombie/skeleton set CreatureType = Undead).
Need a base "undead immunities" brick that all non-template undead get.
Could be a simple QueryBrick that responds to "immune_poison", "immune_disease", etc.
OR: check CreatureType == Undead in the affliction/status application code.
The latter is cleaner — one check, all undead benefit.

mcw: absolutely not the latter, bricks

### Troll regeneration specifics
dNH: trolls revive from 0 HP unless killed by fire/acid.
PF: trolls have fast healing that's suppressed by fire/acid for 1 round.
For us: regen brick heals X per round. If took fire or acid damage this round, no regen.
If troll reaches 0 HP and regen is active, it revives next round at 1 HP.
If troll reaches 0 HP and regen is suppressed, it stays dead.
This means: you MUST use fire/acid to finish trolls. Classic.
mcw: correct, good , we also need some fire res OR acid res trolls

### Adventurer party balance
These need to feel like a real threat but not be individually overwhelming.
Key: they coordinate. The zealot heals, the sellsword tanks, the wizard nukes.
If you kill the zealot first, the rest crumble. If you ignore the wizard, you get nuked.
This teaches tactical thinking — target priority matters.
Balance: each member is ~60% of a normal monster at their level.
But 3-4 of them together are 180-240% of a single monster. Dangerous in aggregate.
mcw: they need to have goal and prupose, eithe ryou or they are on their own little quest, they need to chat to each other with comedy party stuff
mcw: we want to have THEMED aprties, like from delicion in dungeon, or pathfinder wotr or km games, bg possibly, etc.

### Glyph philosophy
Follow nethack conventions where possible. Players who know nethack will have intuition.
New families get new glyphs. Color differentiates within family.
Capital = bigger/scarier version (s/S for spiders/snakes already does this).
mcw: in general capital is not really scarier, but is bigger. a H is agiant humanoid, a h is a small humanoid, but it's not a rule at all


---

## MORE SPECIFIC MONSTERS (continued research)

### Owlbear (glyph: Y? or use beast glyph)
PF: owlbear (L4) — talon grab + beak gnaw + bloodcurdling screech
dNH: owlbear (L5) — claw/claw/hug

mcw: how many owlbears can we do? do they need a grab?

For us: L5, Large, beast, talon grab + beak full attack + screech (fear aura, cooldown)
- Grab already exists (GrabOnHit)
- Screech = war chant pattern but applies FrightenedBuff instead of attack bonus
- NEEDS: FrightenedBuff (penalty to attacks/saves, wears off). Small new status.
- Once frightened exists: owlbear is easy AND frightened is reusable (cairn wight dirge, dragon presence, etc.)

Verdict: owlbear is a good reason to implement FrightenedBuff. One status unlocks many monsters.

### Rust Monster (glyph: R)
PF: rust monster (L3) — antenna rusts metal equipment
dNH: rust monster (L5) — touch rusts metal

For us: L4, Medium, aberration, fast, antenna attack that degrades metal equipment
- Equipment degradation is a NEW SYSTEM. But it can start simple:
  - On hit: if target has metal armor equipped, reduce its AC bonus by 1 (permanently)
  - If AC bonus reaches 0, armor is destroyed
  - Same for weapons: reduce damage bonus by 1
- This is the "equipment pressure" monster. Forces player to think about what they're wearing.
- MEDIUM complexity: need to track item degradation state.

Verdict: medium. Equipment degradation is a new concept but very impactful. Could be simplified to "% chance to destroy one equipped metal item" for v1.
mcw: not for a while because equip degradation is a whole thing to balance

### Mimic (glyph: t for trapper/mimic)
PF: mimic (L4) — disguises as object, grab + swallow
dNH: small/large/giant mimic (L7/8/9) — stick to you, disguised as item

For us: L5, Medium, aberration, disguised as item/chest/door
- Disguise: appears as an item on the ground until player tries to pick it up or steps adjacent
- Then: surprise attack with grab
- MEDIUM: needs "disguised" state where monster renders as an item glyph
- Could be simpler: just a monster that starts asleep and wakes when adjacent (already have IsAsleep)
- Render as item glyph when asleep, monster glyph when awake
- That's actually pretty easy! Just override glyph based on IsAsleep.

Verdict: surprisingly easy if we use the IsAsleep trick. The "looks like an item" rendering is the only new bit.
mcw: yeah this will be good, we coujld even do it as a trap with a custom glyph???? not sure

### Troglodyte (glyph: T or use lizard glyph)
PF: troglodyte (L1) — stench aura, club
dNH: no direct equivalent

For us: L2, Medium, humanoid, equipped with club, stench aura (nauseated)
- Stench aura: same system as yellow mold / ghast
- EASY once stench exists.
- Good early humanoid variety. Reptilian, lives in caves.

Verdict: easy once stench aura exists. Good L2 filler.
mcw: would prefer in family stuff for time being, he needs friends, might be a mook wiht trolls?

### Darkmantle (no direct glyph — maybe 'e' for eye-like?)
PF: darkmantle (L1) — drops from ceiling, engulf head, darkness
dNH: no direct equivalent

For us: L2, Small, aberration, drops from ceiling (surprise attack), grab + constrict
- "Drops from ceiling" = starts asleep on ceiling, wakes when player adjacent, free grab attempt
- Darkness ability: extinguish light in area (if we have light system)
- Without light system: just the drop + grab + constrict. Still interesting.

Verdict: easy. Grab + constrict exist. The "ceiling ambush" is just IsAsleep + first-strike grab.
mcw: neat, not sure how he'd attack/be attackable? he'd need to move around with the player, let's think more on it if we want

---

## GOLARION-SPECIFIC FLAVOR MONSTERS

These aren't from dNH but are distinctly Pathfinder/Golarion:

### Cacodaemon (glyph: &)
Tiny outsider/demon, flying, bite + soul lock (on kill, traps soul in gem)
- L1 demon. Flying + bite + poison.
- Soul lock is flavor — drop a "soul gem" item on kill. Could be valuable to sell.
- Good entry-level demon.
mcw: what is point? he doesn't have soul lock?

### Sinspawn (glyph: could use 'a' for abomination)
Medium aberration, created by Thassilonian sin magic. Each type tied to a sin.
- Wrathspawn: L2, melee focused, rage-like damage bonus
- Envyspawn: L2, steals buffs (complex, later)
- For now: wrathspawn as a simple L2 aberration with bonus damage. Distinctly Golarion.
mcw: let's think mroe hard, `U` is for ¬unknown abonination by the way`

### Attic Whisperer (glyph: z or W)
Tiny undead, L4. Looks like a child's skeleton with cobwebs. Steals voice (silence) and breath (fatigue).
- Silence on hit: already have SilencedBuff!
- Breath steal: could be a new "fatigued" debuff (reduced speed, penalty)
- Very creepy, very Golarion. Good mid-level undead that isn't just "hits hard."
mcw: breath is slow death, can be

### Vargouille (glyph: b or special)
Small outsider, L5. Flying head. Shriek (paralyze) + kiss (transforms victim into vargouille over time).
- Shriek = screech/fear pattern
- Kiss transformation = long-term affliction (flavor, doesn't need to actually transform player)
- Flying + paralyze shriek is mechanically interesting.

mcw: delayed death is fine

---

## SUMMARY: THE THREE GATES

Looking at everything, there are three small systems that unlock the most monsters:

1. **Regeneration** (~30 lines) → trolls, vampires, some demons, some undead
2. **Energy Resistance** (~15 lines) → elementals, giants, dragons, demons, golems
3. **FrightenedBuff** (~15 lines) → owlbear, cairn wight, dragons, demons, many bosses

Total: ~60 lines of infrastructure unlocks probably 30+ monsters.

After those three, the next gates are:
4. **Drain Life** (~40 lines) → wights, wraiths, vampires, specters
5. **Stench Aura** (~20 lines) → ghasts, troglodytes, otyughs, yellow mold
6. **Equipment Degradation** (~30 lines) → rust monster, gray ooze acid, black pudding

And the BIG gates (park for later):
7. Monster spellcasting → liches, demon lords, adventurer wizards
8. Incorporeal → ghosts, shadows, specters (full version)
9. Gaze attacks → beholders, medusa, basilisk, cockatrice


---

## DEPTH CURVE ANALYSIS (what player encounters when)

Current monster count by base level:
- L-1: 5 (goblin warrior, grimple, kobold, mitflit, viper, very drunk jinkin)
- L0: 5 (kobold scout, orc scrapper, pugwampi, rat, scarlet spider)
- L1: 10 (derro punk, giant crab spider, goblin, goblin chef, goblin pyro, goblin war chanter, jinkin, kobold warrior, orb weaver, orc veteran, sea snake, thorn bush, training dummy)
- L2: 5 (cheetah, derro stalker, giant spider, leopard, nuglub, orc commander)
- L3: 6 (derro strangler, giant viper, hippo, lion, medium boss goblin, panther)
- L4: 5 (Asar, derro magister, giant black widow, orc gamekeeper, orc rampager, tiger)
- L5: 4 (hungry hippo, orc doomsayer, phase spider, prince cobra)
- L6: 2 (crown prince cobra, ogre spider)
- L7: 3 (hungry hungry hippo, queen consort cobra, smilodon)
- L8: 3 (giant tarantula, king cobra, shopkeeper)
- L9: 2 (even hungrier hippo, giant anaconda)
- L10: 2 (emperor cobra, orc veteran master)
- L11: 1 (goliath spider)
- L12: 1 (The Hungriest Hippo)

PROBLEMS:
- L0-1: decent variety (15+ monsters)
- L2-3: OK (11 monsters)
- L4-5: thinning (9 monsters), mostly single-family continuations
- L6-7: THIN (5 monsters), almost all are just "bigger version of earlier monster"
- L8-10: VERY THIN (7 monsters), dominated by snake/hippo/spider continuations
- L11-12: BARREN (2 monsters)

The player at depth 8+ is fighting the same families they saw at depth 2, just bigger.
No new TYPES of threat appear. No new mechanics to learn. This is the core problem.

### What each wave adds to the curve:

**Wave 1 (no new systems):**
- L0: bat (new family, new movement archetype)
- L1: brown mold (stationary hazard, cold damage — new damage type encounter)
- L1: slime (first ooze, acid, slow — new creature type)
- L2: giant bat (bat continuation)
- L3: bugbear (humanoid filler, bridges goblin→ogre)
- L4: ogre (first Giant type, Large, high damage)
- L4: gray ooze (grab+constrict+acid — ooze with teeth)
- L5: mimic (surprise! disguised monster — new encounter type)
- L6: ogre brute (ogre continuation)
- L6: wood golem (first real construct, DR/adamantine — teaches DR)
- L7: hill giant (Huge, rock throwing — first ranged giant)

This alone transforms L4-7 from "more snakes and spiders" to "ogres, oozes, golems, giants."

**Wave 2 (regen + energy resist + stench + frightened):**
- L2: troglodyte (stench aura — new mechanic encounter)
- L3: ghoul (paralyze on hit — SCARY new threat type)
- L3: violet fungus (stationary + poison)
- L5: ghast (ghoul + stench — compound threat)
- L5: owlbear (grab + screech/fear — iconic)
- L6: troll (REGENERATION — paradigm shift, must use fire)
- L8: ice troll (cold variant)
- L8: flesh golem (healed by electricity — puzzle monster)
- L9: stone giant (DR + rocks)
- L10: frost giant (cold damage + cold resist)
- L10: fire giant (fire damage + fire resist)
- L11: stone golem (high DR, slow, devastating)

This fills L5-11 with genuinely new threat types that require different tactics.

**Wave 3 (drain):**
- L5: wight (drain life — permanent resource loss, terrifying)
- L7: wraith (drain + phase shift — can't just melee it)
- L7: vampire spawn (drain + regen + fast + DR/silver — multi-layered threat)
- L8: cairn wight (drain + fear dirge — area denial)

This adds the "oh shit" undead tier that makes the mid-dungeon genuinely dangerous.

### Post-waves depth curve:
- L0-1: 17+ monsters, good variety
- L2-3: 14+ monsters, multiple threat types
- L4-5: 16+ monsters, ogres, oozes, ghouls, wights, owlbear, ghast
- L6-7: 12+ monsters, trolls, golems, wraiths, vampires, hill giant
- L8-10: 12+ monsters, ice troll, flesh golem, giants, stone golem, cairn wight
- L11-12: 4+ monsters (still thin but better)
- L13+: empty (future work)

That's a MUCH healthier curve. The player encounters genuinely new mechanics every 2-3 levels.

---

## FINAL THOUGHT: What makes a monster INTERESTING vs just a stat block?

The best monsters in nethack/dNH aren't the ones with the highest damage. They're the ones that change how you play:

- **Rust monster**: you take off your armor before fighting it
- **Floating eye**: you don't melee it (or you use ranged/magic)
- **Cockatrice**: you wear gloves, you don't eat the corpse
- **Troll**: you MUST have fire/acid ready
- **Leprechaun**: you stash your gold before exploring
- **Gelatinous cube**: you don't let it touch you (paralyze = death)
- **Mimic**: you're suspicious of every item on the ground

Each of these teaches the player a RULE. "When you see X, do Y." The game becomes a vocabulary of threats and responses.

Our current monsters mostly teach one rule: "hit it until it dies, maybe use fire on the pyro."
The proposed additions each teach a new rule:
- Mold: "don't melee the stationary thing, use ranged"
- Ooze: "it's slow, kite it; don't let it grab you"
- Troll: "bring fire or it won't stay dead"
- Ghoul: "don't let it touch you or you're paralyzed"
- Wight: "every hit costs you permanent HP"
- Golem: "your normal weapons bounce off, need adamantine"
- Rust monster: "take off your metal gear"
- Mimic: "that chest might eat you"
- Owlbear: "it screams and you panic, fight through the fear"

This is the real value of monster variety — not filling a spreadsheet, but building a vocabulary of tactical decisions.
