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
    protected override void OnFactAdded(Fact fact)
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

        weapon.AddFact(DamageRiderBuff.FlamingD4, duration: duration);
    }
}

public class DamageRiderBuff(string name, DamageType type, int faces) : LogicBrick
{
    public override StackMode StackMode => StackMode.Extend;

    public static readonly DamageRiderBuff UnholyD4 = new("Unholy Weapon", DamageTypes.Unholy, 4);
    public static readonly DamageRiderBuff UnholyD8 = new("Unholy Weapon", DamageTypes.Unholy, 8);
    public static readonly DamageRiderBuff HolyD4 = new("Holy Weapon", DamageTypes.Holy, 4);
    public static readonly DamageRiderBuff HolyD8 = new("Holy Weapon", DamageTypes.Holy, 8);
    public static readonly DamageRiderBuff FreezeD4 = new("Freezing Weapon", DamageTypes.Cold, 4);
    public static readonly DamageRiderBuff FreezeD8 = new("Freezing Weapon", DamageTypes.Cold, 8);
    public static readonly DamageRiderBuff ShockD4 = new("Shocking Weapon", DamageTypes.Shock, 4);
    public static readonly DamageRiderBuff ShockD8 = new("Shocking Weapon", DamageTypes.Shock, 8);
    public static readonly DamageRiderBuff FlamingD4 = new("Flaming Weapon", DamageTypes.Fire, 4);
    public static readonly DamageRiderBuff FlamingD8 = new("Flaming Weapon", DamageTypes.Fire, 8);

    private string OnStr(IUnit unit, string weapon) => type.SubCat switch
    {
        "fire" => $"Flames surround {unit:own} {weapon}.",
        "cold" => $"Icicles swirl round {unit:own} {weapon}.",
        "shock" => $"{unit:Own} {weapon} start to crackle.",
        "holy" => $"{unit:Own} {weapon} glow gold.",
        "unholy" => $"{unit:Own} {weapon} glow black.",
        _ => "??",
    };

    private string OffStr(IUnit unit, string weapon) => type.SubCat switch
    {
        "fire" => $"The flames surrounding {unit:own} {weapon} die out.",
        "cold" => $"Icicles around {unit:own} {weapon} start melting.",
        "shock" => $"{unit:Own} {weapon} stops crackling.",
        "holy" => $"{unit:Own} {weapon} stops glowing gold.",
        "unholy" => $"{unit:Own} {weapon} stops glowing black.",
        _ => "??",
    };

    private string Key => type.SubCat switch
    {
        "fire" => "flaming",
        "cold" => "freeze",
        "shock" => "shock",
        "holy" => "holy",
        "unholy" => "unholy",
        _ => "___",
    };

    public override bool IsBuff => true;
    public override string? BuffName => name;
    public override bool IsActive => true;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key == Key ? true : null;

    protected override void OnFactAdded(Fact fact)
    {
        if (fact.Entity is Item item && item.Holder?.IsPlayer == true)
        {
            bool isUnarmed = item.Def is WeaponDef w && w.Profiency == Proficiencies.Unarmed;
            string weaponName = isUnarmed ? "fists" : item.Def.Name;
            if (item.Has(Key))
                g.pline($"{item.Holder:Own} {weaponName} seems more energised.");
            else
                g.pline(OnStr(u, weaponName));
        }
    }

    protected override void OnFactRemoved(Fact fact)
    {
        if (fact.Entity is Item item && item.Holder is { IsPlayer: true })
        {
            bool isUnarmed = item.Def is WeaponDef w && w.Profiency == Proficiencies.Unarmed;
            string weaponName = isUnarmed ? "fists" : item.Def.Name;
            if (item.Has(Key))
                g.pline($"{item.Holder:Own} {weaponName} seems slightly less energised.");
            else
                g.pline(OffStr(u, weaponName));
        }
    }

    protected override void OnBeforeDamageRoll(Fact fact, PHContext context)
    {
        if (context.Weapon != fact.Entity) return;

        context.Damage.Add(new DamageRoll
        {
            Formula = d(faces),
            Type = type,
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

public class WarBlessingPassive : LogicBrick<WarBlessingData>
{
    public override bool IsActive => true;

    protected override void OnBeforeAttackRoll(Fact fact, PHContext context)
    {
        if (context.Source != fact.Entity) return;
        var data = X(fact);
        int bonus = data.State switch
        {
            WarBlessingState.Ready => 1,
            WarBlessingState.Buffed => 1 + data.BonusRoll,
            _ => 0
        };
        if (bonus > 0)
            context.Check!.Modifiers.Untyped(bonus, "war");
    }

    protected override void OnRoundEnd(Fact fact, PHContext context)
    {
        var data = X(fact);
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

public class StrengthBlessingMinor() : ActionBrick("Strength (minor)")
{
    public override object? CreateData() => new CooldownTracker();
    public override ActionCost GetCost(IUnit unit, object? data, Target target) => BlessingHelper.Cost(unit);
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => ((CooldownTracker)data!).CanExecute(out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        ((CooldownTracker)data!).CooldownUntil = g.CurrentRound + BlessingHelper.Cooldown(unit, BlessingHelper.MinorCooldown);
        g.pline("Divine might surges through you!");
        unit.AddFact(StrengthBuff.Instance, duration: d(10).Roll() + 10);
    }
}

public class StrengthBuff : LogicBrick
{
    public static readonly StrengthBuff Instance = new();
    public override bool IsBuff => true;
    public override string? BuffName => "Strength";

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
      key == "stat/Str" ? new Modifier(ModifierCategory.CircumstanceBonus, 4, "Strength Blessing") : null;
}

public class LawBlessingMinor() : ActionBrick("Law (minor)")
{
    public override object? CreateData() => new CooldownTracker();
    public override ActionCost GetCost(IUnit unit, object? data, Target target) => BlessingHelper.Cost(unit);
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => ((CooldownTracker)data!).CanExecute(out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        ((CooldownTracker)data!).CooldownUntil = g.CurrentRound + BlessingHelper.Cooldown(unit, 80);
        g.pline("Law buff on");
        unit.AddFact(LawBuff.Instance, duration: 10);
    }
}

public class LawBuff : LogicBrick
{
    public static readonly LawBuff Instance = new();
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
    public override bool IsBuff => true;
    public override string? BuffName => "Luck";

    protected override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (context.Source != fact.Entity) return;
        var data = X(fact);
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
            victim.AddFact(BlindBuff.Instance.Timed(), duration: BlindDuration);
            if (unit.IsPlayer)
                g.pline("{0:The} is blinded!", victim);
        }
    }
}
