namespace Pathhack.Game;

public class BlessingDef
{
    public required string Id;
    public required string Name;
    public required string Description;
    public required Action<IUnit> ApplyMinor;
    // public required Action<IUnit> ApplyMajor; // later

    public FeatDef ToFeat() => new()
    {
        id = $"blessing_{Id}",
        Name = $"{Name} Blessing",
        Description = Description,
        Type = FeatType.Class,
        CheckWhyNot = () => u.Deity?.Aspects.Contains(Name) == true ? null : "",
        Components = [new GrantBlessingBrick(this)]
    };
}

public class GrantBlessingBrick(BlessingDef blessing) : LogicBrick
{
    public override string Id => $"grant_blessing+{blessing.Name}";
    protected override void OnFactAdded(Fact fact)
    {
        if (fact.Entity is IUnit unit)
            blessing.ApplyMinor(unit);
    }
}

public static class Blessings
{
    public static readonly BlessingDef Fire = new()
    {
        Id = "fire",
        Name = "Fire",
        Description = "Imbue your weapon with flames.",
        ApplyMinor = unit => unit.AddAction(new FireBlessingMinor())
    };

    public static readonly BlessingDef War = new()
    {
        Id = "war",
        Name = "War",
        Description = "Channel the fury of battle.",
        ApplyMinor = unit => unit.AddAction(new WarBlessingAction()),
    };

    public static readonly BlessingDef Strength = new()
    {
        Id = "strength",
        Name = "Strength",
        Description = "Call upon divine might.",
        ApplyMinor = unit => unit.AddAction(new StrengthBlessingMinor())
    };

    public static readonly BlessingDef Law = new()
    {
        Id = "law",
        Name = "Law",
        Description = "Invoke the armor of order.",
        ApplyMinor = unit => unit.AddAction(new LawBlessingMinor())
    };

    public static readonly BlessingDef Healing = new()
    {
        Id = "healing",
        Name = "Healing",
        Description = "Channel restorative energy.",
        ApplyMinor = unit => unit.AddAction(new HealingBlessingMinor())
    };

    public static readonly BlessingDef Magic = new()
    {
        Id = "magic",
        Name = "Magic",
        Description = "Hurl a bolt of arcane force.",
        ApplyMinor = unit => { } // unit.AddAction(new MagicBlessingMinor())
    };

    public static readonly BlessingDef Sun = new()
    {
        Id = "sun",
        Name = "Sun",
        Description = "Radiate holy light that burns the unholy.",
        ApplyMinor = unit =>
        {
            unit.AddFact(LogicHelpers.ModifierBrick("light_radius", ModifierCategory.UntypedStackable, 1, "sun"));
            unit.AddAction(new SunBlessingMinor());
        }
    };

    public static readonly BlessingDef Luck = new()
    {
        Id = "luck",
        Name = "Luck",
        Description = "Fortune favors you.",
        ApplyMinor = unit => unit.AddFact(new LuckBlessingPassive())
    };

    public static readonly BlessingDef Darkness = new()
    {
        Id = "darkness",
        Name = "Darkness",
        Description = "Blind enemies with a cone of shadow.",
        ApplyMinor = unit => unit.AddAction(new DarknessBlessingMinor())
    };

    public static readonly BlessingDef[] All = [Fire, War, Strength, Law, Healing, Magic, Sun, Luck, Darkness];
}

public abstract class BlessingAction(string name, TargetingType target = TargetingType.None, Func<IUnit, int>? cooldown = null) : CooldownAction(name, target, cooldown ?? MinorCooldown)
{
    public override ActionCost GetCost(IUnit unit, object? data, Target tgt) => unit.Has("blessing_free_action") ? ActionCosts.Free : ActionCosts.OneAction;

    public static Func<IUnit, int> Cooldown(DiceFormula cd)
    {
        return u =>
        {
            int rolled = cd.Roll();
            if (u.Has("blessing_cooldown_reduction"))
                rolled = rolled * 3 / 4;
            return Math.Max(10, rolled);
        };
    }

    public static Func<IUnit, int> MinorCooldown = Cooldown(d(50) + 50);

}

public class FireBlessingMinor() : BlessingAction("Fire Blessing")
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        int duration = (unit is Player p ? p.CharacterLevel : 1) + 5 + g.Rn1(1, 10);
        var weapon = unit.GetWieldedItem();
        weapon?.AddFact(WeaponDamageRider.FlamingD4, duration: duration);
    }
}

public enum WarBlessingState { Ready, Buffed, Cooldown }

public class WarBlessingBuff : LogicBrick
{
    public override string Id => "blessing:war";
    internal static readonly WarBlessingBuff Instance = new();

    protected override void OnBeforeAttackRoll(Fact fact, PHContext context)
    {
        if (context.Source != fact.Entity) return;
        int adjacent = context.Source.Pos.Neighbours().Count(x => lvl.UnitAt(x) != null);
        int bonus = Math.Max(adjacent, 4);
        context.Check!.Modifiers.Mod(ModifierCategory.CircumstanceBonus, bonus, "war");
    }
}

public class WarBlessingAction() : BlessingAction("War Blessing")
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        unit.AddFact(WarBlessingBuff.Instance, 20);
        g.pline("To war!");
    }
}

public class StrengthBlessingMinor() : BlessingAction("Strength (minor)")
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        g.pline("Divine might surges through you!");
        unit.AddFact(StrengthBuff.Plus4.Timed(), duration: d(10).Roll() + 10);
    }
}

public class StrengthBuff(int mod) : LogicBrick
{
    public override string Id => $"blessing:str+{mod}";
    public static readonly StrengthBuff Plus2 = new(2);
    public static readonly StrengthBuff Plus4 = new(4);

    public override bool IsBuff => true;
    public override string? BuffName => "Strength";

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
      key == "stat/Str" ? new Modifier(ModifierCategory.CircumstanceBonus, mod, "Strength Blessing") : null;
}

public class LawBlessingMinor() : BlessingAction("Law Blessing (minor)")
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        g.pline("Law buff on");
        unit.AddFact(LawBuff.Instance, duration: 10);
    }
}

public class LawBuff : LogicBrick
{
    public static readonly LawBuff Instance = new();
    public override string Id => "blessing:law";
    public override bool IsBuff => true;
    public override string? BuffName => "Law";

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "ac" => new Modifier(ModifierCategory.CircumstanceBonus, 2, "law"),
        _ => null
    };

    protected override void OnFactRemoved(Fact fact)
    {
        if (fact.Entity is IUnit { IsPlayer: true })
            g.pline("Law buff off");
    }
}

// Healing Blessing - heal 1d6+level
public class HealingBlessingMinor() : BlessingAction("Healing Blessing", TargetingType.None, Cooldown(150))
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        int level = unit is Player p ? p.CharacterLevel : 1;
        g.DoHeal(unit, unit, d(6) + level);

        if (unit.IsPlayer)
            g.pline("Healing energy washes over you.");
    }
}

public class MagicBlessingMinor() : BlessingAction("Magic Blessing", TargetingType.Direction, Cooldown(100))
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        var range = g.RnRange(3, 6);

        foreach (var tgt in lvl.CollectLine(unit.Pos, target.Pos!.Value, range, lvl.UnitAt))
        {
            if (tgt == null) continue;
            using var ctx = PHContext.Create(unit, Target.From(tgt));
            ctx.Damage = [
                new() { Formula = d(2, 6), Type = DamageTypes.Magic }
            ];
            DoDamage(ctx);
        }
    }
}

// Sun Blessing - passive +1 light radius, active AoE burn
public class SunBlessingMinor() : BlessingAction("Sun Blessing")
{
    const int radius = 4;

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Log.Verbose("sun", "Sun blessing radius={0} from {1}", radius, unit.Pos);

        using TileBitset result = lvl.CollectCircle(upos, radius, false);
        Draw.AnimateCone(unit.Pos, result,
            new Glyph('*', ConsoleColor.Yellow),
            new Glyph('*', ConsoleColor.DarkYellow),
            new Glyph('.', ConsoleColor.DarkYellow));

        FlashLit(result);

        foreach (var tgt in result.Select(lvl.UnitAt))
        {
            if (tgt.IsNullOrDead()) continue;

            Log.Verbose("sun", $"Sun hits {tgt} at {tgt.Pos}");
            var formula = tgt.IsCreature(CreatureTypes.Undead) ? d(6) : d(2);
            using var ctx = PHContext.Create(unit, Target.From(tgt));
            ctx.Damage = [new() { Formula = formula, Type = DamageTypes.Fire }];
            DoDamage(ctx);
        }
    }
}

// Luck Blessing - advantage every N rounds
public class LuckBlessingData
{
    public int NextAvailable;
}

public class LuckBlessingPassive : LogicBrick<LuckBlessingData>
{
    public override string Id => "blessing:luck";
    public override bool IsBuff => true;
    public override string? BuffName => "Luck";

    protected override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (!context.IsCheckingOwnerOf(fact)) return;
        var data = X(fact);
        if (g.CurrentRound < data.NextAvailable) return;

        data.NextAvailable = g.CurrentRound + 6 + d(12).Roll();
        context.Check!.Advantage++;
    }
}

// Darkness Blessing - cone blind
public class DarknessBlessingMinor() : BlessingAction("Darkness Blessing", TargetingType.Direction)
{
    const int Radius = 4;
    const int BlindDuration = 10;

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        using var cone = lvl.CollectCone(unit.Pos, target.Pos!.Value, Radius);

        Draw.AnimateFlash(cone, new Glyph('*', ConsoleColor.DarkGray));
        g.pline("Shadows billow forth!");

        foreach (var pos in cone)
        {
            var victim = lvl.UnitAt(pos);
            if (victim.IsNullOrDead() || victim == unit) continue;
            victim.AddFact(BlindBuff.Instance.Timed(), duration: BlindDuration);
            if (unit.IsPlayer)
                g.pline("{0:The} is blinded!", victim);
        }
    }
}
