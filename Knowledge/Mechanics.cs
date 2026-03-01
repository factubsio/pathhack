namespace Pathhack.Knowledge;

public class MechanicsPage
{
    public required string Name;
    public required string Description;
}

public static class GeneralMechanics
{
    public static readonly MechanicsPage TurnOrder = new()
    {
        Name = "Order of play",
        Description = """
            Each round proceeds in a fixed order:

            [fg=White]1. Player phase[/]: You receive 12 energy (minus encumbrance penalty). Your start-of-round effects trigger, then you act until energy ≤ 1 or you cannot act.

            [fg=White]2. Monster phase[/]: Each monster receives 12 energy and its start-of-round effects trigger. Monsters act in initiative order until their energy runs out. Hidden monsters skip their turn.

            [fg=White]3. Swarms tick[/]: All swarms on the level update.

            [fg=White]4. Areas tick[/]: All areas (grease, fire, etc.) tick — this triggers effects on occupants.

            [fg=White]5. Round end[/]: Hunger ticks. All units' timed effects expire, end-of-round effects trigger, ability charges regenerate, and temporary HP ticks down. Corpses rot.

            [fg=White]6. Cleanup[/]: Dead units are removed. Deferred spawns resolve. Natural HP regeneration ticks. New monsters may spawn.
            """,
    };

    public static readonly MechanicsPage Energy = new()
    {
        Name = "Energy",
        Description = """
            Each round (according to Order of play) every unit has the chance to act.

            Any unit that has energy remaining continues to act until its energy drops below 1.

            Each unit completes all of its actions before the next unit's turn begins.

            The majority of combat actions cost 12 energy (attacking, item manipulation, etc).

            Movement speeds are more varied:
            A cheetah is very fast, it has a movement speed of 6: it costs 6 energy to move one tile.
            A scythe tree is  very slow, it has a movement speed of 36: it costs 36 energy to move one tile.

            Both a cheetah and a scythe tree spend 12 energy to perform their full attack, movement speed does not affect combat speed!

            Note: Encumbrance can reduce both your movement speed AND your energy income per round.
            """,
    };

    public static readonly MechanicsPage Checks = new()
    {
        Name = "Checks",
        Description = """
            When an effect requires a contested outcome, it is resolved with a d20 check.

            For example: a goblin breathes fire at you. The goblin's spell DC is 14. You roll d20 + your Reflex save modifiers. Roll+modifiers >= 14 and you save.

            Most actions and spells will have reduced effects when the target saves.

            [fg=White]Saves[/] come in three kinds: Fortitude (endurance, poison, disease), Reflex (dodging, area effects), and Will (mental effects, fear). Each automatically includes your relevant stat modifier and any active bonuses.

            [fg=White]Attack rolls[/] are also checks. The attacker builds an attack bonus, and the defender's AC is the DC.

            [fg=White]Advantage and Disadvantage[/]: A check can have advantage (roll twice, take the higher), disadvantage (roll twice, take the lower), or neither. Multiple sources cancel each other out — the net result is always one of these three states.

            [fg=White]Modifiers[/]: Checks are affected by bonuses and penalties from equipment, buffs, and other effects. Not all modifiers stack — see [fg=Cyan]Modifier Stacking[/] for details.
            """,
    };

    public static readonly MechanicsPage ModifierStacking = new()
    {
        Name = "Modifier Stacking",
        Description = """
            Bonuses and penalties are grouped into a small number of categories. Within each category, only the single best bonus and single worst penalty apply. Duplicates are ignored.

            A +2 Item bonus from armor and a +3 Item bonus from a shield: only the +3 applies.
            A +3 Item bonus and a +1 Circumstance bonus: both apply, for +4 total.

            [fg=White]Untyped[/] modifiers are the exception — they always stack with everything, including each other. A +1 untyped from a feat and a +2 untyped from a spell both apply, for +3 total.
            """,
    };

    public static readonly MechanicsPage Damage = new()
    {
        Name = "Damage",
        Description = """
            Attacks can deal multiple damage rolls of different types. A flaming sword deals slashing + fire as separate rolls. Reductions apply to each roll individually.

            [fg=White]DR and Energy Resistance[/]: Flat reduction per damage roll. DR reduces physical damage, energy resistance reduces a specific element (fire, cold, etc.).

            [fg=White]DR Bypass[/]: DR can be bypassed by specific materials or damage tags. A weapon made of silver bypasses DR/silver. A magical weapon bypasses DR/magic.

            [fg=White]Protection[/]: Absorbs a specific damage type across multiple hits. A unit with 30 fire protection absorbs up to 30 fire damage total before it runs out.

            [fg=White]Saves[/]: Some damage is halved on a successful save, or doubled on a failed one.

            After all reductions, the remaining damage is subtracted from HP. At 0 HP, the unit dies.
            """,
    };

    public static readonly MechanicsPage Encumbrance = new()
    {
        Name = "Encumbrance",
        Description = """
            Every item has weight. Your total carried weight determines your encumbrance tier, based on your Strength and Constitution.

            Any encumbrance above Unencumbered increases movement cost by 20%.

            [fg=White]Unencumbered[/]: No penalty.
            [fg=White]Burdened[/]: -1 attack, +20% movement cost.
            [fg=White]Stressed[/]: -6 energy/round, -2 attack, +20% movement cost.
            [fg=White]Strained[/]: -9 energy/round, -2 attack, +20% movement cost.
            [fg=White]Overtaxed[/]: -10 energy/round, -3 attack, +20% movement cost.
            [fg=White]Overloaded[/]: -11 energy/round, -3 attack, +20% movement cost.
            """,
    };

    public static readonly MechanicsPage BUC = new()
    {
        Name = "Blessed / Uncursed / Cursed",
        Description = """
            Items have a BUC status: Blessed, Uncursed, or Cursed. BUC is hidden until identified.

            [fg=White]Cursed[/] items (usually) weld to you when equipped — they cannot be removed without a remove curse effect. Cursed consumables may have reduced or harmful effects.

            [fg=White]Blessed[/] consumables often have enhanced effects.

            [fg=White]Uncursed[/] is the default. Most items behave normally at this status.
            """,
    };

    public static readonly MechanicsPage Awareness = new()
    {
        Name = "Awareness",
        Description = """
            Awareness determines whether a unit can target another in combat. The same rules apply to you and to monsters.

            [fg=White]Visible[/]: Line of sight, not blind, target not invisible (or viewer has see invisible), area is lit (or viewer has darkvision). Can target normally.

            [fg=White]Detected[/]: Tremorsense reveals creatures within range regardless of sight or stealth, including hidden creatures. Can target normally.

            [fg=White]Warned[/]: A type-specific sense alerts you to the creature's presence and location.

            [fg=White]Unease[/]: A general warning. Something threatening is nearby but not exactly identifiable.

            A unit attacking something it cannot detect has disadvantage. A unit attacking something that cannot detect it has advantage.
            """,
    };

    public static readonly MechanicsPage Spells = new()
    {
        Name = "Spells",
        Description = """
            Spells come in 5 levels.

            A level 1 spell requires an available level 1 slot to cast.

            Casting a spell costs 12 energy.

            A caster has a pool of slots for every spell level they can cast. Each pool refills over time.

            [fg=White]Maintained spells[/] lock a slot instead of consuming it. The slot is unavailable while the spell is active. A maintained spell can be dismissed at any time to free the slot.

            Maintained spells require concentration — if the caster cannot speak, they must pass a Will save each round or lose the spell.

            Monsters use the same spell lists and follow the same system.
            """,
    };

    public static readonly MechanicsPage Consumables = new()
    {
        Name = "Consumables",
        Description = """
            [fg=White]Potions[/] are single-use. Quaff a potion to apply its effect to yourself. Throw a potion at a creature to apply its effect to them.

            [fg=White]Scrolls[/] are single-use. Reading a scroll triggers its effect.

            [fg=White]Bottles[/] are regular spells brewed into a flask. Throwing a bottle triggers the spell on impact. Bottles cannot contain directional spells.

            [fg=White]Wands[/] are regular spells stored in a wand. Zapping a wand casts the spell in a direction. Wands have limited charges that do not recharge. Wands can only contain directional spells.

            Alchemical flasks (acid, alchemist's fire, etc.) only appear as bottles.
            """,
    };

    public static readonly MechanicsPage Runes = new()
    {
        Name = "Runes and Forging",
        Description = """
            Weapons and armor have a potency (+0 to +3), adding a bonus to attack or AC.

            Each point of potency adds a property rune slot. Slots may be found empty or already filled.

            Property slots (depending on potency) can be filled with modifiers that add elemental damage and other on-hit effects.

            Weapons have a fundamental slot. Fundamental runes fit here and provide base damage upgrades.

            Runes are found as loot. Apply them at a rune forge, with the `#forge` command.
            """,
    };

    public static readonly MechanicsPage Progression = new()
    {
        Name = "Progression",
        Description = """
            Characters level from 1 to 20.

            On level up, you choose a feat. Feats come in three types:

            [fg=White]Class feats[/] (levels 2, 4, 6, 8, 10, 12, 14, 18, 20): specific to your class.

            [fg=White]General feats[/] (levels 3, 9, 15, 19): available to all characters.

            [fg=White]Ancestry feats[/] (levels 7, 13, 17): from your ancestry.

            Levelling also grants new spell choices and additional spell slots for casters.
            """,
    };

    public static readonly MechanicsPage FavouredWeapon = new()
    {
        Name = "Favoured Weapon",
        Description = """
            Warpriests use a mechanic called 'favoured weapon'.

            Each deity has a favoured weapon, warpriests get benefits from using it.

            For example, Sarenrae favours scimitars. A warpriest of Sarenrae gets benefits when using scimitars.

            You will always start with a +1 favoured weapon (with empty fundamental and property slots)

            Your damage dice are slightly increased with your favoured weapon.

            Your [fg=Cyan]fervour: enhance weapon[/] can only apply to your favoured weapon.

            (Note: Irori is the fisticuffs god so you just get real good at punching instead)
            """,
    };

    public static readonly MechanicsPage[] All = [TurnOrder, Energy, Checks, ModifierStacking, Damage, Encumbrance, BUC, Awareness, Spells, Consumables, Runes, Progression, FavouredWeapon];
}
