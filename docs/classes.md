# Class Design

## Design Principles

- FLAVOUR FLAVOUR FLAVOUR
- I don't care if there are broken combos, it is nh
- Class feats shouldn't have drawbacks - PrC feats can have tradeoffs (deliberate specialization).
- class defiens progression, though we should aim for <=2 choices at any level up cos menu menu menu blahhhh

## Casting Depth

max spell level = 5, we'll compress 9th level to 5th but it will be all a bit hand wavy, it's ok, we will manage

ALL CASTERS CAN CAST SPELL LEVEL 5 

we won't limit max LEVEL like pf, but if you were full caster in pf you'll get more higher level slots in pathhack, earlier

(note: big/medium/little are full, 2/3 ,paladin-likes from 1e)

BIG CASTERS IN PF = MORE LEVEL 5 SLOTS (EARLIER?)
MEDIUM CASTERS IN PF = FEWER LEVEL 5 SLOTS (LATER?)
LITTLE CASTERS IN PF = ONE LEVEL 5 SLOT? (LEVEL 20????)

I dunno if we even care about little casters from 1e?

Currently cos ui width max 5 slots per level - FUNCTION FOLLOW FORM :-(

our classes:
- BIG (9-level equivalent): Oracle, Sorcerer, Druid
- MEDIUM (6-level equivalent): Magus, Alchemist, Warpriest
- LOL NOMAGIC4U: Swashbuckler, Monk, Kineticist (own systems), bit conceredn swashhy will be left behind... bug GUNS?

non-pf people will be tres confused that monk is the archer but iiwii yolo

## Base Classes (9)

### Warpriest
Divine martial. Blessings, fervor (cooldown-based), sacred weapon. caster. Deity choice at level 1 determines favored weapon and blessing options.

bit of archery-ness if favoured weapon is bow like thing (or desna weeb throwing starknives?)

### Oracle
Divine caster. Mystery + curse at level 1. Spells half-choice, half-random (mystery forces some on you). curse/mystery progress with CL

### Sorcerer
Arcane full caster. Bloodline at level 1. Spells fully random per bloodline - no choice, chaos magic. Bloodline gives bonus abilities (?)

have to do some fun bloodlines to make this good - also see quests, 100% bloodline (category) based quest

### Magus
Arcane martial. Spellstrike toggle system:
- OFF: n% chance to cast chosen spell on hit, no slot burn
- ON: 100% cast on hit, burns slot

it's like a wp but wp is a bit more "control" while magus is more stabby stabby?

NO RANGED SPELL STRIKE FUCK THAT

Arcane pool for weapon enhancement, spell recall, arcana fuel

arcanas will be neat little actives, like blessings but maybe more trickery-ey?

### Druid
Nature full caster. Order choice at level 1:
- **Primal**: MORE SPELL STUFF
- **Wild**: PET???? (to do later?)
- **Feral**: MORE WILDSHAPE STUFF

also note these three words pretty mean the same thing 

Also wildshape. help

Also pets. help

### Swashbuckler
Martial. Panache/grit resource economy. Styles available. Gunslinger/crossbow master as early PrC option for ranged.

how do we do reactions? It isn't really possible and keep a decent ui/flow so maybe make it more chance based?

ammo will be "joyful" to implement - I was thinking we let launchers can always launch ammo at like +0 (maybe even -N) but quivered is gooder? need to noodle

### Monk
Martial. Ki pool. Focus at level 1:
- **PUNCH**: unarmed techniques (stunning fist, knockout)
- **SHOOT**: ki archery (zen archer, ki arrow, pinning shot)
- **Sensei**: stronger ki abilities, more pool

don't nerf the other options on pickup, druid we will probably penalise the non-order paths (a bit?) but monk should just be more tools for the given pick. don't think should be damage based stuff, but utility (basically are you more flexible in punch or bow, and sensei is many more "support" stuff)

need to raid the chained,unchained,2e kitchen for cool ki powers

### Kineticist
Elemental. Burn system, blasts, infusions. No spell slots - own resource. Element choice at level 1.

metakinesis is kinda a tedium to play, but how tf do we burn without it? need to finda  decent way to use this

burn can be spell slots in reverse - you build it up and reduce max hp (like in tt) but we'll just decrease over time

maybe decrease faster the higher it is (reasonably fun system to have class feats interact with)

burn also gives bonuses like tt

NO FUCKING DEADLY EARTH

### Alchemist
Grenadier base (combat-focused). Bombs, mutagen, extracts (mixed arcane/divine list), discoveries.

bombs - do we need ammo/potions?

definitely more potion interactions (+benefits, %chance to skip bads, throw for bigger effects?)

extracts are just spells but we pull from a subset of different lists

## Later (needs systems)

### Summoner
Needs pet AI
**Eidolon**: customizable outsider

this is going to be some ui joy to implement

do we want to fold in necro class fantasy here, cos it's kinda same thing but maybe like increase N rather than make big boy bigger?

some of the actives will be pretty neat like swap places and stuff

### Occultist
Needs implement system, mental focus pool.

no idea if it will be fun or anyone will know wtf is going on , but I adored my necrocultist when I played him for a decent while so I add here and to heck with everything

### Cavalier
Needs mount system

orders are cool

what does he do that the other martials don't, because he don't get no spells

tbd honestly

## As PrC Only

- Paladin (smite, auras, lay on hands)
- Barbarian (rage powers)
- Gunslinger (on swashbuckler)
- Assassin/Rogue (sneak attack, poison)
- Ranger (favored enemy/terrain)
- Hunter (teamwork, animal focus (eh))
- Witch (hexes, patron) - how do patron work here? maybe only allow for some classes I dunno
- Lichdom - meme or broken, probably both
- Necromancer - if we don't do it as summoner maybe as prc, but hard to see how it fits in, maybe as prc for summoner but then you lose your eidolon I dunno - I guess can style it as KILL YOUR EIDOLON AND RES IT but eidolon is STRICTLY an outsider so it's a bit gross lorewise and for that reason I think it should be out
- Arcane Archer
- Hellknight
- Bladebound (sentient weapon for magus) - black blade is cool, maybe fiddly lol we shojuld do finnean (NO)
- etc.

## Spell Acquisition

- **Warpriest**: choice from list, limited by deity
- **Oracle**: half choice, half random (mystery-forced)
- **Sorcerer**: fully random per bloodline
- **Druid**: choice from nature list
- **Magus**: choice from arcane list
- **Alchemist**: choice from mixed arcane/divine

maybe too much choice not enough spellbooks? not enough randos?