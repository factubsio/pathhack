namespace Pathhack.Game.Bestiary;

public class FireBreath(int radius, Dice damage, int dc, string pool = "fire_breath") : ActionBrick("Goblin Fire Breath")
{
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        if (!unit.HasCharge(pool, out whyNot)) return false;
        whyNot = "can't see target";
        if (unit is not Monster m || !m.CanSeeYou) return false;
        whyNot = "out of range";
        if (unit.Pos.ChebyshevDist(target.Pos!.Value) > radius) return false;
        whyNot = "";
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        unit.TryUseCharge(pool);
        Pos dir = (target.Pos!.Value - unit.Pos).Signed;
        using var cone = lvl.CollectCone(unit.Pos, dir, radius);

        Draw.AnimateFlash(cone, new Glyph('≈', ConsoleColor.Red));
        g.YouObserve(unit, "{0:The} breathes fire!", "a whoosh of flames");

        foreach (var pos in cone)
        {
            var victim = lvl.UnitAt(pos);
            if (victim.IsNullOrDead() || victim == unit) continue;

            using var ctx = PHContext.Create(unit, Target.From(victim));
            
            CreateAndDoCheck(ctx, "reflex_save", dc, "fire");

            var dmg = new DamageRoll { Formula = damage, Type = DamageTypes.Fire, HalfOnSave = true };
            ctx.Damage = [dmg];
            DoDamage(ctx);
        }
    }
}

public class WarChant(string pool = "war_chant") : ActionBrick("Goblin War Chant")
{
    const int Duration = 10;
    const int Range = 6;

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) =>
        unit.HasCharge(pool, out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        unit.TryUseCharge(pool);

        g.YouObserve(unit, "{0:The} sings a war chant!", "a war chant");

        string? family = (unit as Monster)?.Def.Family;
        if (family == null) return;

        foreach (var ally in lvl.LiveUnits)
        {
            if (ally is not Monster m) continue;
            if (m.Def.Family != family) continue;
            if (unit.Pos.ChebyshevDist(ally.Pos) > Range) continue;
            m.AddFact(WarChantBuff.Instance, duration: Duration);
        }
    }
}

public class WarChantBuff : LogicBrick
{
    public static readonly WarChantBuff Instance = new();
    public override bool IsBuff => true;
    public override string? BuffName => "War Chant";
    public override bool IsActive => true;

    protected override void OnBeforeAttackRoll(Fact fact, PHContext context)
    {
        if (context.Source != fact.Entity) return;
        if (context.Weapon == null) return;
        context.Check!.Modifiers.Mod(ModifierCategory.CircumstanceBonus, 2, "war chant");
    }

    protected override void OnBeforeDamageRoll(Fact fact, PHContext context)
    {
        if (context.Source != fact.Entity) return;
        if (context.Weapon == null) return;
        context.Damage[0].Modifiers.Mod(ModifierCategory.CircumstanceBonus, 1, "war chant");
    }
}

public static class Goblins
{
    public static readonly MonsterDef Warrior = new()
    {
        id = "goblin_warrior",
        Name = "goblin warrior",
        Glyph = new('g', ConsoleColor.Green),
        HpPerLevel = 5,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = -1,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = -1,
        SpawnWeight = 4,
        MinDepth = 1,
        MaxDepth = 3,
        Family = "goblin",
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        GrowsInto = () => Basic!,
        Components = [
            new Equip(NaturalWeapons.DogSlicer),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef Chef = new()
    {
        id = "goblin_chef",
        Name = "goblin chef",
        Glyph = new('g', ConsoleColor.Yellow),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 1,
        SpawnWeight = 1,
        MinDepth = 1,
        MaxDepth = 4,
        Family = "goblin",
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef Pyro = new()
    {
        id = "goblin_pyro",
        Name = "goblin pyro",
        Glyph = new('g', ConsoleColor.Red),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 1,
        SpawnWeight = 1,
        MinDepth = 1,
        MaxDepth = 4,
        Family = "goblin",
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            new GrantPool("fire_breath", 2, 50),
            new GrantAction(new FireBreath(2, d(6), 12)),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef WarChanter = new()
    {
        id = "goblin_war_chanter",
        Name = "goblin war chanter",
        Glyph = new('g', ConsoleColor.Magenta),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 1,
        SpawnWeight = 1,
        MinDepth = 1,
        MaxDepth = 4,
        Family = "goblin",
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            new GrantPool("war_chant", 1, 10),
            new GrantAction(new WarChant()),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef MediumBoss = new()
    {
        id = "medium_boss_goblin",
        Name = "medium boss goblin",
        Glyph = new('g', ConsoleColor.DarkYellow),
        HpPerLevel = 8,
        AC = 2,
        AttackBonus = 2,
        DamageBonus = 2,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 3,
        SpawnWeight = 0,
        MinDepth = 2,
        MaxDepth = 5,
        Family = "goblin",
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            new Equip(NaturalWeapons.DogSlicer),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef Basic = new()
    {
        id = "goblin",
        Name = "goblin",
        Glyph = new('g', ConsoleColor.White),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = -1,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 1,
        SpawnWeight = 3,
        MinDepth = 1,
        MaxDepth = 3,
        Family = "goblin",
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            new Equip(NaturalWeapons.DogSlicer),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef[] All = [Warrior, Chef, Pyro, WarChanter, MediumBoss, Basic];
}
