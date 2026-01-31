using System.Runtime.InteropServices;
using System.Text;

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
        Prereq = p => p.Deity?.Aspects.Contains(Name) == true
            ? Availability.Now
            : Availability.Never,
        Components = [new GrantBlessingBrick(this)]
    };
}

public class GrantBlessingBrick(BlessingDef blessing) : LogicBrick
{
    public override void OnFactAdded(Fact fact)
    {
        if (fact.Entity is IUnit unit)
            blessing.ApplyMinor(unit);
    }
}

public class FireBlessingMinor() : ActionBrick("Fire Blessing")
{
    public override object? CreateData() => new CooldownTracker();

    public override ActionCost GetCost(IUnit unit, object? data, Target target) =>
        BlessingHelper.Cost(unit);

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => ((CooldownTracker)data!).CanExecute(out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        ((CooldownTracker)data!).CooldownUntil = g.CurrentRound + BlessingHelper.Cooldown(unit, BlessingHelper.MinorCooldown);

        int duration = (unit is Player p ? p.CharacterLevel : 1) + 5 + g.Rn1(1, 10);
        var weapon = unit.GetWieldedItem();

        if (weapon.Def is WeaponDef w && w.Profiency == Proficiencies.Unarmed)
            g.pline("Your fists burn with flame!");
        else if (weapon.Has("flaming"))
            g.pline("Your weapon burns brighter!");
        else
            g.pline("Your weapon bursts into flame!");

        weapon.AddFact(new FlamingBuff(g.CurrentRound + duration));
    }
}

public class FlamingBuff(int expiresAt) : LogicBrick
{
    public override bool IsBuff => true;
    public override string? BuffName => "Flaming Weapon";
    public override bool IsActive => true;

    public override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "flaming" => true,
        _ => null
    };

    public override void OnRoundEnd(Fact fact, PHContext context)
    {
        if (g.CurrentRound >= expiresAt)
        {
            fact.Remove();

            if (fact.Entity is Item item && item.Holder is { IsPlayer: true })
            {
                bool isUnarmed = item.Def is WeaponDef w && w.Profiency == Proficiencies.Unarmed;
                if (isUnarmed)
                    g.pline("The flames around your fists fade.");
                else if (item.Has("flaming"))
                    g.pline("Your weapon burns less brightly.");
                else
                    g.pline("Your weapon's flames die out.");
            }
        }
    }

    public override void OnBeforeDamageRoll(Fact fact, PHContext context)
    {
        if (context.Weapon != fact.Entity) return;
        Log.Write("on befor edamage roll");
        context.Damage.Add(new DamageRoll
        {
            Formula = d(4),
            Type = DamageTypes.Fire
        });
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
        ApplyMinor = unit =>
        {
            var fact = unit.AddFact(new WarBlessingPassive());
            unit.AddAction(new WarBlessingAction(fact));
        }
    };

    public static readonly BlessingDef Strength = new()
    {
        Id = "strength",
        Name = "Strength",
        Description = "Call upon divine might.",
        ApplyMinor = unit => unit.AddAction(BlessingHelper.MakeBlessing(
            "Strength (minor)", 
            () => "Strength buff on",
            () => "Strength buff off",
            () => u.Attributes.Str.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, 4, "Strength (minor)")),
            mod => u.Attributes.Str.Modifiers.RemoveModifier(mod),
            BlessingHelper.MinorCooldown,
            d(10) + 10
        ))
    };

    public static readonly BlessingDef Law = new()
    {
        Id = "law",
        Name = "Law",
        Description = "Invoke the armor of order.",
        ApplyMinor = unit => unit.AddAction(BlessingHelper.MakeBlessing(
            "Law (minor)",
            () => "Law buff on",
            () => "Law buff off",
            (key, _) => key switch
            {
                "ac" => new Modifier(ModifierCategory.CircumstanceBonus, 2, "law"),
                _ => null,
            },
            80,
            10))
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
        ApplyMinor = unit => {} // unit.AddAction(new MagicBlessingMinor())
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

public enum WarBlessingState { Ready, Buffed, Cooldown }

public class WarBlessingData : CooldownTracker
{
    public WarBlessingState State = WarBlessingState.Ready;
    public int BuffUntil;
    public int BonusRoll;
}

public class WarBlessingPassive : LogicBrick
{
    public override object? CreateData() => new WarBlessingData();
    public override bool IsActive => true;

    public override void OnBeforeAttackRoll(Fact fact, PHContext context)
    {
        if (context.Source != fact.Entity) return;
        var data = (WarBlessingData)fact.Data!;
        int bonus = data.State switch
        {
            WarBlessingState.Ready => 1,
            WarBlessingState.Buffed => 1 + data.BonusRoll,
            _ => 0
        };
        if (bonus > 0)
            context.Check!.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, bonus, "war"));
    }

    public override void OnRoundEnd(Fact fact, PHContext context)
    {
        var data = (WarBlessingData)fact.Data!;
        if (data.State == WarBlessingState.Buffed && g.CurrentRound >= data.BuffUntil)
        {
            data.State = WarBlessingState.Cooldown;
            if (fact.Entity is IUnit unit)
                data.CooldownUntil = g.CurrentRound + BlessingHelper.Cooldown(unit, BlessingHelper.MinorCooldown);
            if (fact.Entity is IUnit { IsPlayer: true })
                g.pline("Your focus wanes.");
        }
        else if (data.State == WarBlessingState.Cooldown && g.CurrentRound >= data.CooldownUntil)
        {
            data.State = WarBlessingState.Ready;
            if (fact.Entity is IUnit { IsPlayer: true })
                g.pline("You feel ready for battle again.");
        }
    }
}

public class WarBlessingAction(Fact fact) : ActionBrick("War Blessing")
{
    const int BuffDuration = 20;

    WarBlessingData Data => (WarBlessingData)fact.Data!;

    public override ActionCost GetCost(IUnit unit, object? data, Target target) =>
        BlessingHelper.Cost(unit);

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => Data.CanExecute(out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        Data.BonusRoll = 1 + g.Rn2(2);
        Data.BuffUntil = g.CurrentRound + BuffDuration;
        Data.State = WarBlessingState.Buffed;
        g.pline("To war!");
    }
}

// Strength Blessing - +4 STR for duration

public class CooldownTracker
{
    public int CooldownUntil;

    public bool CanExecute(out string whyNot)
    {
        int remaining = CooldownUntil - g.CurrentRound;
        whyNot = $"{remaining} rounds left";
        return remaining <= 0;
    }
}

public class AddBuffAction(string name, Func<string> on, Func<int, LogicBrick> makeFact, DiceFormula cd, DiceFormula dur) : ActionBrick(name)
{
    public override object? CreateData() => new CooldownTracker();

    public override ActionCost GetCost(IUnit unit, object? data, Target target) =>
        BlessingHelper.Cost(unit);

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => ((CooldownTracker)data!).CanExecute(out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        ((CooldownTracker)data!).CooldownUntil = g.CurrentRound + cd.Roll();

        if (unit is not Player p) return;

        g.pline(on());

        unit.AddFact(makeFact(g.CurrentRound + dur.Roll()));
    }
}

public class BuffBrick(int expiresAt, string buffName, Action? onEnd, Func<string, string?, object?>? query, Func<string> off) : LogicBrick
{
    public override bool IsBuff => true;
    public override string? BuffName => buffName;
    public override bool IsActive => true;

    public override object? OnQuery(Fact fact, string key, string? arg) => query?.Invoke(key, arg);

    public override void OnRoundEnd(Fact fact, PHContext context)
    {
        if (g.CurrentRound >= expiresAt)
        {
            fact.Remove();
            g.pline(off());
            onEnd?.Invoke();
        }
    }
}

public static class BlessingHelper
{
    public static readonly DiceFormula MinorCooldown = d(50) + 50;

    public static ActionCost Cost(IUnit unit) => unit.Has("blessing_free_action") ? ActionCosts.Free : ActionCosts.OneAction;

    public static int Cooldown(IUnit unit, DiceFormula cd)
    {
        int rolled = cd.Roll();
        if (unit.Has("blessing_cooldown_reduction"))
            rolled = rolled * 3 / 4;
        return Math.Max(10, rolled);
    }

    public static ActionBrick MakeBlessing(string name, Func<string> on, Func<string> off, Action onAdd, Action onRemove, DiceFormula cd, DiceFormula dur)
    {
        return new AddBuffAction(
                name,
                on,
                expiresAt => { onAdd(); return new BuffBrick(expiresAt, name, onRemove, null, off); },
                cd,
                dur);
    }

    public static ActionBrick MakeBlessing<T>(string name, Func<string> on, Func<string> off, Func<T> onAdd, Action<T> onRemove, DiceFormula cd, DiceFormula dur)
    {
        return new AddBuffAction(
                name,
                on,
                expiresAt =>
                {
                    var val = onAdd();
                    return new BuffBrick(expiresAt, name, () => onRemove(val), null, off);
                },
                cd,
                dur);
    }

    public static ActionBrick MakeBlessing(string name, Func<string> on, Func<string> off, Func<string, string?, object?> query, DiceFormula cd, DiceFormula dur)
    {
        return new AddBuffAction(
                name,
                on,
                expiresAt => new BuffBrick(expiresAt, name, null, query, off),
                cd,
                dur);
    }
}

// Healing Blessing - heal 1d6+level
public class HealingBlessingMinor() : ActionBrick("Healing Blessing")
{
    const int Cooldown = 150;

    public override object? CreateData() => new CooldownTracker();

    public override ActionCost GetCost(IUnit unit, object? data, Target target) =>
        BlessingHelper.Cost(unit);

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => ((CooldownTracker)data!).CanExecute(out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        ((CooldownTracker)data!).CooldownUntil = g.CurrentRound + Cooldown;

        int level = unit is Player p ? p.CharacterLevel : 1;
        g.DoHeal(unit, unit, d(6) + level);

        if (unit.IsPlayer)
            g.pline("Healing energy washes over you.");
    }
}

public class MagicBlessingMinor() : ActionBrick("Magic Blessing", TargetingType.Direction)
{
    const int Cooldown = 100;

    public override object? CreateData() => new CooldownTracker();

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => ((CooldownTracker)data!).CanExecute(out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        ((CooldownTracker)data!).CooldownUntil = g.CurrentRound + Cooldown;

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
public class SunBlessingMinor() : ActionBrick("Sun Blessing")
{
    const int radius = 4;

    public override object? CreateData() => new CooldownTracker();

    public override ActionCost GetCost(IUnit unit, object? data, Target target) =>
        BlessingHelper.Cost(unit);

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => ((CooldownTracker)data!).CanExecute(out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        ((CooldownTracker)data!).CooldownUntil = g.CurrentRound + BlessingHelper.Cooldown(unit, BlessingHelper.MinorCooldown);

        Log.Verbose("sun", "Sun blessing radius={0} from {1}", radius, unit.Pos);
        
        using TileBitset result = lvl.CollectCircle(upos, radius, false);
        Draw.AnimateCone(unit.Pos, result,
            new Glyph('*', ConsoleColor.Yellow),
            new Glyph('*', ConsoleColor.DarkYellow),
            new Glyph('.', ConsoleColor.DarkYellow));

        g.FlashLit(result);

        foreach (var tgt in result.Select(lvl.UnitAt))
        {
            if (tgt.IsNullOrDead()) continue;

            Log.Verbose("sun", $"Sun hits {tgt} at {tgt.Pos}");
            var formula = tgt.Has("undead") ? d(6) : d(2);
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

public class LuckBlessingPassive : LogicBrick
{
    public override object? CreateData() => new LuckBlessingData();
    public override bool IsBuff => true;
    public override string? BuffName => "Luck";

    public override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (context.Source != fact.Entity) return;
        var data = (LuckBlessingData)fact.Data!;
        if (g.CurrentRound < data.NextAvailable) return;

        data.NextAvailable = g.CurrentRound + 6 + d(12).Roll();
        context.Check!.Advantage++;

        if (fact.Entity is IUnit { IsPlayer: true })
            g.pline("Luck is on your side!");
    }
}

// Darkness Blessing - cone blind
public class DarknessBlessingMinor() : ActionBrick("Darkness Blessing", TargetingType.Direction)
{
    const int Radius = 4;
    const int BlindDuration = 10;

    public override object? CreateData() => new CooldownTracker();

    public override ActionCost GetCost(IUnit unit, object? data, Target target) =>
        BlessingHelper.Cost(unit);

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => ((CooldownTracker)data!).CanExecute(out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        ((CooldownTracker)data!).CooldownUntil = g.CurrentRound + BlessingHelper.Cooldown(unit, BlessingHelper.MinorCooldown);

        using var cone = lvl.CollectCone(unit.Pos, target.Pos!.Value, Radius);

        Draw.AnimateFlash(cone, new Glyph('*', ConsoleColor.DarkGray));
        g.pline("Shadows billow forth!");

        foreach (var pos in cone)
        {
            var victim = lvl.UnitAt(pos);
            if (victim.IsNullOrDead() || victim == unit) continue;
            victim.AddFact(new BlindBuff(g.CurrentRound + BlindDuration));
            if (unit.IsPlayer)
                g.pline("{0:The} is blinded!", victim);
        }
    }
}

public class BlindBuff(int expiresAt) : LogicBrick
{
    public override bool IsBuff => true;
    public override string? BuffName => "Blind";
    public override bool IsActive => true;

    public override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (context.Source != fact.Entity) return;
        context.Check!.Disadvantage++;
    }

    public override void OnRoundEnd(Fact fact, PHContext context)
    {
        if (g.CurrentRound >= expiresAt)
        {
            fact.Remove();
            if (fact.Entity is IUnit { IsPlayer: true })
                g.pline("You can see again.");
        }
    }
}
