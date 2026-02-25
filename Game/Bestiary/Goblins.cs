namespace Pathhack.Game.Bestiary;

public class FireBreath(int radius, Dice damage, int dc, string pool = "fire_breath") : ActionBrick("Goblin Fire Breath")
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        if (!unit.HasCharge(pool, out var whyNot)) return new(false, whyNot);
        if (unit is not Monster m || !m.CanSeeYou) return new(false, "can't see target");
        if (unit.Pos.ChebyshevDist(target.Pos!.Value) > radius) return new(false, "out of range");
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        unit.TryUseCharge(pool);
        Pos dir = (target.Pos!.Value - unit.Pos).Signed;
        using var cone = lvl.CollectCone(unit.Pos, dir, radius);

        Draw.AnimateFlash(cone, new Glyph('≈', ConsoleColor.Red));
        g.YouObserve(unit, $"{unit:The} breathes fire!", "a whoosh of flames");

        foreach (var pos in cone)
        {
            var victim = lvl.UnitAt(pos);
            if (victim.IsNullOrDead() || victim == unit) continue;

            using var ctx = PHContext.Create(unit, Target.From(victim));

            CheckReflex(ctx, dc, "fire");

            var dmg = new DamageRoll { Formula = damage, Type = DamageTypes.Fire, HalfOnSave = true };
            ctx.Damage = [dmg];
            DoDamage(ctx);
        }
        AreaSystem.AffectTiles(lvl, DamageTypes.Fire, cone);
    }
}

public class WarChant(string pool = "war_chant") : ActionBrick("Goblin War Chant")
{
    const int Duration = 10;
    const int Range = 6;

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) =>
        unit.HasCharge(pool, out var whyNot) ? true : new ActionPlan(false, whyNot);

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        unit.TryUseCharge(pool);

        g.YouObserve(unit, $"{unit:The} sings a war chant!", "a war chant");

        MonsterFamily? family = (unit as Monster)?.Def.Family;
        if (family == null) return;

        foreach (var ally in lvl.LiveUnits)
        {
            if (ally is not Monster m) continue;
            if (m.Def.Family != family) continue;
            if (unit.Pos.ChebyshevDist(ally.Pos) > Range) continue;
            m.AddFact(WarChantBuff.Instance, unit, duration: Duration);
        }
    }
}

public class WarChantBuff : LogicBrick
{
    public static readonly WarChantBuff Instance = new();
    public override string Id => "goblin:war_chant";
    public override bool IsBuff => true;
    public override string? BuffName => "War Chant";
    public override bool IsActive => true;

    protected override void OnBeforeAttackRoll(Fact fact, PHContext context)
    {
        if (context.Source != fact.Entity) return;
        if (context.Weapon == null) return;
        context.Check!.Modifiers.Mod(ModifierCategory.CircumstanceBonus, 1, "war chant");
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
    public static readonly MonsterFamily Family = new("goblin");

    public static readonly WeaponDef DogSlicer = new()
    {
        Name = "dogslicer",
        BaseDamage = d(6),
        DamageType = DamageTypes.Slashing,
        WeaponType = WeaponTypes.Shortbow,
        Style = WeaponStyle.Carve, Grip = WeaponGrip.Light,
        MeleeVerb = "swing",
        Price = -1,
    };

    static MonsterDef G(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 6, int ac = 0, int ab = 0, int dmg = 0,
        int maxDepth = 4, int spawnWeight = 10, Func<MonsterDef>? growsInto = null)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = Family,
            CreatureType = CreatureTypes.Humanoid,
            Glyph = new('g', color),
            HpPerLevel = hp,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            LandMove = ActionCosts.LandMove20,
            Unarmed = NaturalWeapons.Fist,
            Size = UnitSize.Small,
            BaseLevel = level,
            MaxDepth = maxDepth,
            SpawnWeight = spawnWeight,
            MoralAxis = MoralAxis.Evil,
            EthicalAxis = EthicalAxis.Chaotic,
            GrowsInto = growsInto,
            Components = components,
        };
    }

    public static readonly MonsterDef Warrior = G("goblin_warrior", "goblin warrior", -1, ConsoleColor.Green,
        [new Equip(DogSlicer), new GrantAction(AttackWithWeapon.Instance)],
        hp: 5, dmg: -1, maxDepth: 3, growsInto: () => Basic!);

    public static readonly MonsterDef Chef = G("goblin_chef", "goblin chef", 1, ConsoleColor.Yellow,
        [new GrantAction(AttackWithWeapon.Instance)]);

    public static readonly MonsterDef Pyro = G("goblin_pyro", "goblin pyro", 1, ConsoleColor.Red,
        [new GrantPool("fire_breath", 2, 50), new GrantAction(new FireBreath(2, d(6), 12)), new GrantAction(AttackWithWeapon.Instance)]);

    public static readonly MonsterDef WarChanter = G("goblin_war_chanter", "goblin war chanter", 1, ConsoleColor.Magenta,
        [new GrantPool("war_chant", 1, 30), new GrantAction(new WarChant()), new GrantAction(AttackWithWeapon.Instance)]);

    public static readonly MonsterDef MediumBoss = G("medium_boss_goblin", "medium boss goblin", 3, ConsoleColor.DarkYellow,
        [new Equip(DogSlicer), new GrantAction(AttackWithWeapon.Instance)],
        hp: 8, ac: 2, ab: 2, dmg: 2, maxDepth: 5, spawnWeight: 0);

    public static readonly MonsterDef Basic = G("goblin", "goblin", 1, ConsoleColor.White,
        [new Equip(DogSlicer), new GrantAction(AttackWithWeapon.Instance)],
        dmg: -1, maxDepth: 3);

    public static readonly MonsterDef[] All = [Warrior, Chef, Pyro, WarChanter, MediumBoss, Basic];
}
