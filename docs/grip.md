# Weapon Proficiency: Style + Grip

Two-axis proficiency system. You're proficient if you match on EITHER axis.

## Axes

**Style** (technique): Simple, Impact, Carve, Skewer, Staff, Thrown, Firearm, Flail, Exotic
**Grip** (handling): Light, Heavy, Great, Polearm

Exotic is a poison pill on BOTH axes — exotic weapons can ONLY be used via specific WeaponType proficiency.

## Proficiency Resolution

Proficient if ANY of:
1. Trained in the weapon's **style** (except exotic)
2. Trained in the weapon's **grip** (except exotic)
3. Trained in the specific **WeaponType**

## Weapon Mapping

| Weapon | Style | Grip |
|---|---|---|
| dagger | Simple | Light |
| shortsword | Carve | Light |
| rapier | Skewer | Light |
| hatchet | Simple | Light |
| club | Simple | Light |
| longsword | Carve | Heavy |
| scimitar | Carve | Heavy |
| falchion | Carve | Heavy |
| mace | Impact | Heavy |
| flail | Flail | Heavy |
| pick | Skewer | Heavy |
| axe | Carve | Heavy |
| spiked chain | Flail | Heavy |
| greatsword | Carve | Great |
| greatclub | Impact | Great |
| greataxe | Carve | Great |
| earth breaker | Impact | Great |
| quarterstaff | Staff | Great |
| bo staff | Staff | Polearm |
| spear | Simple | Polearm |
| scythe | Carve | Polearm |
| halberd | Carve | Polearm |
| glaive | Carve | Polearm |
| lucerne hammer | Impact | Polearm |
| ranseur | Skewer | Polearm |
| bardiche | Carve | Polearm |
| dart | Thrown | Light |
| bola | Thrown | Light |
| rock | Thrown | Light |
| whip | Exotic | Light |
| longbow | Exotic | Great |
| shortbow | Exotic | Light |
| blowgun | Exotic | Light |

Firearms will be Style=Firearm, Grip varies by weapon (pistol=Light, musket=Great, etc).

## Notes

- Hatchet is Carve/Light. Throwability is a weapon property, not a proficiency concern.
- AltProficiency exists for weapons that genuinely straddle two categories.
- Favored weapon (warpriest sacred weapon) still keys off specific WeaponType.
- Separate from favored weapon, weapon gen will have a 1-in-5 reroll toward favored type to reduce scarcity.
- Exotic poisons both axes — the only way to use an exotic weapon is specific WeaponType training.
