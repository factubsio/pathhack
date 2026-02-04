namespace Pathhack.Game;


public class BlindFight : LogicBrick
{
    protected override object? OnQuery(Fact fact, string key, string? arg)
    {
        return key switch
        {
            "blind_fight" => true,
            "tremorsense" => fact.Entity.EffectiveLevel >= 10 ? 2 : null,
            _ => null,
        };
    }
}

public class DebilitatingStrikes : LogicBrick
{
    protected override void OnDamageDone(Fact fact, PHContext context)
    {
        // roll for effect todo: we need the effects
        // lower chance of better effects may be a bit lame compared to better chance of lower effects since it is too feast/famine?
    }
}

public class Evasion : LogicBrick
{
    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext context)
    {
        // no check, no evade
        if (context.Check == null) return;

        // if the check failed, or this wasn't even a reflex save, don't do anything
        if (context.Check.Result == false || context.Check.Key != "reflex_save") return;

        foreach (var dmg in context.Damage)
        {
            if (dmg.HalfOnSave) dmg.Negate();
        }
    }
}

public class TrapSense : LogicBrick
{
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
      key == "trap_sense" ? true : null;

    protected override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (context.Check!.Tag == "trap")
            context.Check.Modifiers.AddModifier(new(ModifierCategory.CircumstanceBonus, 4, "Trap Sense"));
    }
}

public class FeatherStep : LogicBrick
{
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
      key == "ignore_difficult_terrain" ? true : null;
}
public class Toughness : LogicBrick
{
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
      key == "max_hp" ? new Modifier(ModifierCategory.UntypedStackable, fact.Entity.EffectiveLevel * 6, "Toughness") : null;
}
public class FleetBrick : LogicBrick
{
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "speed_bonus" ? new Modifier(ModifierCategory.CircumstanceBonus, fact.Entity.EffectiveLevel >= 10 ? 3 : 2, "fleet") : null;
}

public static class GeneralFeats
{
    public static readonly FeatDef Fleet = new()
    {
        id = "fleet",
        Name = "Fleet",
        Description = "You move a little faster. Note: this does not affect the time taken to perform combat or item actions.",
        Type = FeatType.General,
        Level = 1,
        Components = [new FleetBrick()],
    };

    public static readonly FeatDef Toughness = new()
    {
        id = "toughness",
        Name = "Toughness",
        Description = "You gain extra HP per level (retroactively applied).",
        Type = FeatType.General,
        Level = 1,
        Components = [new Toughness()],
    };

    public static readonly FeatDef BlindFight = new()
    {
        id = "blindfight",
        Name = "Blind Fight",
        Description = "You take no penalties for attacking unseen targets, neither from invisible enemies nor you being blind. At level 10 you gain Tremorsense with a 20 foot range.",
        Type = FeatType.General,
        Level = 1,
        Components = [new BlindFight()],
    };

    public static readonly FeatDef FeatherStep = new()
    {
        id = "featherstep",
        Name = "Feather Step",
        Description = "You ignore the effects of difficult terrain.",
        Type = FeatType.General,
        Level = 1,
        Components = [new FeatherStep()],
    };

    public static readonly FeatDef TrapSense = new()
    {
        id = "trap_sense",
        Name = "Trap Sense",
        Description = "You are better at finding hidden things and avoid the negative effects of traps more easily.",
        Type = FeatType.General,
        Level = 1,
        Components = [new TrapSense()],
    };

    public static readonly FeatDef Evasion = new()
    {
        id = "evasion",
        Name = "Evasion",
        Description = "You can avoid even magical and unusual attacks with great agility. If you makes a successful Reflex saving throw against an attack that normally deals half damage on a successful save, you instead take no damage.",
        Type = FeatType.General,
        Level = 1,
        Components = [new Evasion()],
    };

    public static readonly FeatDef DebilitatingStrikes = new()
    {
        id = "debilitating_strikes",
        Name = "Debilitating Strikes",
        Description = "Whenever you strike a creature with a weapon you have a chance to debilitate your target, randomly chosing (Bewildered, Disorientated, Hampered). These penalties do not stack with themselves.",
        Type = FeatType.General,
        Level = 1,
        Components = [new DebilitatingStrikes()],
    };

    public static readonly FeatDef[] All = [Fleet, Toughness, BlindFight, FeatherStep, TrapSense, Evasion, DebilitatingStrikes];
}