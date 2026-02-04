namespace Pathhack.Game.Bestiary;

public class TKAttackBonus(int bonus) : LogicBrick
{
    protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
        if (ctx.Weapon?.Def.id == "tk_projectile")
            ctx.Check!.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, bonus, "telekinesis"));
    }
}

public class DazeImmunity : LogicBrick
{
    public static readonly DazeImmunity Instance = new();
    public override bool IsActive => true;
    public override string? BuffName => "Daze Immunity";
}

public class DazedBuff : LogicBrick
{
    public static readonly DazedBuff Instance = new();
    public override bool IsActive => true;
    public override string? BuffName => "Dazed";

    protected override void OnRoundStart(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not IUnit unit) return;
        unit.Energy = 0;
        g.pline($"{unit:The} {VTense(unit, "is")} dazed!");
        fact.Remove();
    }
}

public class DazeAction(int range, int dc, string pool) : ActionBrick("Daze")
{
    public const string Resource = "daze";

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        if (!unit.HasCharge(pool, out whyNot)) return false;
        whyNot = "can't see target";
        if (unit is not Monster m || !m.CanSeeYou) return false;
        whyNot = "out of range";
        if (unit.Pos.ChebyshevDist(upos) > range) return false;
        whyNot = "target immune";
        if (u.HasFact<DazeImmunity>()) return false;
        whyNot = "";
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        if (target.Unit is not {} tgt) return;

        unit.TryUseCharge(pool);
        string msg = $"{unit:The} dazes {tgt:the}";

        // You are immune even regardless of save, otherwise it's a bit overly painful
        u.AddFact(DazeImmunity.Instance, 4);

        using var ctx = PHContext.Create(unit, Target.From(u));
        if (CreateAndDoCheck(ctx, "will_save", dc, "daze"))
        {
            g.pline($"{msg}, but {tgt:the} resists.");
            return;
        }
        else
        {
            g.pline($"{msg}!");
        }
        u.AddFact(DazedBuff.Instance);
    }
}

public class SilencedBuff : LogicBrick
{
    public static readonly SilencedBuff Instance = new();
    public override bool IsBuff => true;
    public override string? BuffName => "Silenced";
    public override StackMode StackMode => StackMode.Stack;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "can_speak" => false,
        _ => null
    };
}

public class TimedSilence : LogicBrick
{
    public static readonly TimedSilence Instance = new();
    public override bool IsActive => true;

    protected override void OnFactAdded(Fact fact) =>
        fact.Entity.AddFact(SilencedBuff.Instance);

    protected override void OnFactRemoved(Fact fact) =>
        fact.Entity.RemoveStack<SilencedBuff>();
}

public class TelekineticProjectile(int range, Dice damage, string pool) : ActionBrick("Telekinetic Projectile")
{
    public const string Resource = "tk_projectile";

    readonly WeaponDef TKProjectile = new()
    {
        id = "tk_projectile",
        Name = "telekinetic projectile",
        BaseDamage = damage,
        Profiency = Proficiencies.Unarmed,
        DamageType = DamageTypes.Blunt,
        Glyph = new('*', ConsoleColor.Cyan),
        Launcher = "tk",
    };

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        if (!unit.HasCharge(pool, out whyNot)) return false;
        whyNot = "can't see target";
        if (unit is not Monster m || !m.CanSeeYou) return false;
        whyNot = "no valid position";
        if (FindLaunchPos(unit) == null) return false;
        whyNot = "";
        return true;
    }

    Pos? FindLaunchPos(IUnit unit)
    {
        // Walk each 8-dir ray from player, find tile >1 from player, <=range from self
        // Start far (more damage) and work closer
        foreach (var dir in Pos.AllDirs.Shuffled())
        {
            // Skip rays that pass through self
            if ((unit.Pos - upos).Signed == dir) continue;
            for (int dist = 4; dist >= 2; dist--)
            {
                Pos p = upos + dir * dist;
                if (p.ChebyshevDist(unit.Pos) > range) continue;
                if (!lvl.InBounds(p)) continue;
                if (!lvl.HasLOS(p)) continue;
                return p;
            }
        }
        return null;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        unit.TryUseCharge(pool);
        var from = FindLaunchPos(unit)!.Value;
        var dir = (upos - from).Signed;
        var item = Item.Create(TKProjectile);
        
        g.pline($"{unit:The} hurls a telekinetic bolt!");
        var landed = g.DoThrow(unit, item, dir, from);
        lvl.RemoveItem(item, landed);
    }
}

public static class Derro
{
    public static readonly MonsterDef Punk = new()
    {
        id = "derro_punk",
        Name = "derro punk",
        Glyph = new('h', ConsoleColor.Cyan),
        HP = 10,
        AC = 13,
        AttackBonus = 3,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        CR = 1,
        Components = [
            new EquipSet(new Outfit(1, new OutfitItem(MundaneArmory.Dagger))),
            new GrantAction(AttackWithWeapon.Instance),
            new GrantPool(TelekineticProjectile.Resource, 2, 30),
            new GrantAction(new TelekineticProjectile(6, d(6), TelekineticProjectile.Resource)),
            new TKAttackBonus(2),
        ],
    };

    public static readonly MonsterDef Stalker = new()
    {
        id = "derro_stalker",
        Name = "derro stalker",
        Glyph = new('h', ConsoleColor.DarkCyan),
        HP = 14,
        AC = 14,
        AttackBonus = 4,
        DamageBonus = 1,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        CR = 2,
        Components = [
            new EquipSet(new Outfit(1, new OutfitItem(MundaneArmory.Club))),
            new GrantAction(AttackWithWeapon.Instance),
            new GrantPool(DazeAction.Resource, 1, 20),
            new GrantAction(new DazeAction(4, 12, DazeAction.Resource)),
        ],
    };

    public static readonly MonsterDef Strangler = new()
    {
        id = "derro_strangler",
        Name = "derro strangler",
        Glyph = new('h', ConsoleColor.Blue),
        HP = 18,
        AC = 15,
        AttackBonus = 5,
        DamageBonus = 2,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        CR = 3,
        Components = [
            new EquipSet(new Outfit(1, new OutfitItem(MundaneArmory.SpikedClub))),
            new GrantAction(AttackWithWeapon.Instance),
            new ApplyFactOnAttackHit(TimedSilence.Instance, duration: 3),
        ],
    };

    public static readonly MonsterDef Magister = new()
    {
        id = "derro_magister",
        Name = "derro magister",
        Glyph = new('h', ConsoleColor.Magenta),
        HP = 12,
        AC = 13,
        AttackBonus = 3,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        CR = 4,
        Components = [
            new EquipSet(new Outfit(1, new OutfitItem(MundaneArmory.Quarterstaff))),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef[] All = [Punk, Stalker, Strangler, Magister];
}
