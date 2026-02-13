namespace Pathhack.Game.Bestiary;

public class TKAttackBonus(int bonus) : LogicBrick
{
    public static readonly TKAttackBonus Instance = new(2);

    protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
        if (ctx.Weapon?.Def.id == "tk_projectile")
            ctx.Check!.Modifiers.Untyped(bonus, "telekinesis");
    }
}

public class DazeImmunity : LogicBrick
{
    public static readonly DazeImmunity Instance = new();
    public override string? PokedexDescription => "Immune to daze";
    public override bool IsActive => true;
    public override string? BuffName => "Daze Immunity";
}

public class DazedBuff : LogicBrick
{
    public static readonly DazedBuff Instance = new();
    public override bool IsActive => true;
    public override string? BuffName => "Dazed";

    protected override void OnRoundStart(Fact fact)
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

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        if (!unit.HasCharge(pool, out var whyNot)) return new(false, whyNot);
        if (unit is not Monster m || !m.CanSeeYou) return new(false, "can't see target");
        if (unit.Pos.ChebyshevDist(upos) > range) return new(false, "out of range");
        if (u.HasFact(DazeImmunity.Instance)) return new(false, "target immune");
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        if (target.Unit is not { } tgt) return;

        unit.TryUseCharge(pool);
        string msg = $"{unit:The} dazes {tgt:the}";

        // You are immune even regardless of save, otherwise it's a bit overly painful
        u.AddFact(DazeImmunity.Instance, 4);

        using var ctx = PHContext.Create(unit, Target.From(u));
        if (CheckWill(ctx, dc, "daze"))
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

public class TelekineticProjectile(int range, Dice damage, string pool) : ActionBrick("Telekinetic Projectile")
{
    internal static readonly TelekineticProjectile Minor = new(6, d(6), Resource);
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
        Price = -1,
    };
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        if (!unit.HasCharge(pool, out var whyNot)) return new(false, whyNot);
        if (unit is not Monster m || !m.CanSeeYou) return new(false, "can't see target");
        if (FindLaunchPos(unit) == null) return new(false, "no valid position");
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

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        unit.TryUseCharge(pool);
        var from = FindLaunchPos(unit)!.Value;
        var dir = (upos - from).Signed;
        var item = Item.Create(TKProjectile);

        g.pline($"{unit:The} hurls a telekinetic bolt!");
        var landed = DoThrow(unit, item, dir, from);
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
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 1,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Family = "derro",
        CreatureType = CreatureTypes.Humanoid,
        GroupSize = GroupSize.SmallMixed,
        Components = [
            new EquipSet(new Outfit(1, new OutfitItem(MundaneArmory.Dagger))),
            new GrantAction(AttackWithWeapon.Instance),
            new GrantPool(TelekineticProjectile.Resource, 2, 30),
            new GrantAction(TelekineticProjectile.Minor),
            TKAttackBonus.Instance,
        ],
    };

    public static readonly MonsterDef Stalker = new()
    {
        id = "derro_stalker",
        Name = "derro stalker",
        Glyph = new('h', ConsoleColor.DarkCyan),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 1,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 2,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Family = "derro",
        CreatureType = CreatureTypes.Humanoid,
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
        HpPerLevel = 7,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 2,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 3,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Family = "derro",
        CreatureType = CreatureTypes.Humanoid,
        Components = [
            new EquipSet(new Outfit(1, new OutfitItem(MundaneArmory.SpikedClub))),
            new GrantAction(AttackWithWeapon.Instance),
            new ApplyFactOnAttackHit(SilencedBuff.Instance.Timed(), duration: 3),
        ],
    };

    public static readonly MonsterDef Magister = new()
    {
        id = "derro_magister",
        Name = "derro magister",
        Glyph = new('h', ConsoleColor.Magenta),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 4,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Family = "derro",
        CreatureType = CreatureTypes.Humanoid,
        Components = [
            new EquipSet(new Outfit(1, new OutfitItem(MundaneArmory.Quarterstaff))),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef[] All = [Punk, Stalker, Strangler, Magister];
}
