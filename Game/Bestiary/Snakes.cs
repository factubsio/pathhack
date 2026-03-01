namespace Pathhack.Game.Bestiary;

public class SnakeVenomLesser(int dc) : AfflictionBrick(dc, "poison")
{
    public override string Id => $"snake:venom_lesser+{DC}";
    public override AbilityTags Tags => AbilityTags.Biological;
    public static readonly SnakeVenomLesser DC10 = new(10);
    public static readonly SnakeVenomLesser DC12 = new(12);
    public static readonly SnakeVenomLesser DC14 = new(14);

    public override string AfflictionName => "Snake Venom";
    public override int MaxStage => 3;
    public override DiceFormula TickInterval => d(10) + 10;
    public override int? AutoCureMax => 600;

    protected override void DoPeriodicEffect(Fact fact, IUnit unit, int stage)
    {
        using var ctx = PHContext.Create(unit, Target.From(unit));
        ctx.Damage.Add(new DamageRoll { Formula = d(4), Type = DamageTypes.Poison });
        DoDamage(ctx);

        if (stage == 1 && unit.IsPlayer) //FIXME YouObserveSelf?
            g.pline($"{unit:The} {VTense(unit, "feel")} poisoned!");
    }

    protected override object? DoQuery(int stage, string key, string? arg) =>
        key == "stat/Str" && stage >= 2 ? new Modifier(ModifierCategory.StatusPenalty, -2, "snake venom") : null;
}

public class SnakeVenomGreater(int dc) : AfflictionBrick(dc, "poison")
{
    public override string Id => $"snake:venom_greater+{DC}";
    public override AbilityTags Tags => AbilityTags.Biological;
    public static readonly SnakeVenomGreater DC13 = new(13);
    public static readonly SnakeVenomGreater DC14 = new(14);
    public static readonly SnakeVenomGreater DC15 = new(15);
    public static readonly SnakeVenomGreater DC17 = new(17);

    public override string AfflictionName => "Virulent Snake Venom";
    public override int MaxStage => 5;
    public override DiceFormula TickInterval => d(10) + 10;
    public override int? AutoCureMax => 800;

    protected override void DoPeriodicEffect(Fact fact, IUnit unit, int stage)
    {
        using var ctx = PHContext.Create(unit, Target.From(unit));
        DiceFormula formula = stage >= 5 ? d(2, 6) : stage >= 3 ? d(8) : d(6);
        ctx.Damage.Add(new DamageRoll { Formula = formula, Type = DamageTypes.Poison });
        DoDamage(ctx);

        if (stage == 1 && unit.IsPlayer) //FIXME YouObserveSelf?
            g.pline($"{unit:The} {VTense(unit, "feel")} badly poisoned!");
    }

    protected override object? DoQuery(int stage, string key, string? arg) => key switch
    {
        "stat/Str" => new Modifier(ModifierCategory.StatusPenalty, stage >= 4 ? -4 : -2, "virulent venom"),
        "stat/Con" when stage >= 2 => new Modifier(ModifierCategory.StatusPenalty, stage >= 4 ? -4 : -2, "virulent venom"),
        "stat/Dex" when stage >= 3 => new Modifier(ModifierCategory.StatusPenalty, -2, "virulent venom"),
        _ => null
    };
}

public class GrabOnHit : LogicBrick
{
    public static readonly GrabOnHit Instance = new();
    public override string Id => "snake:grab";
    public override string? PokedexDescription => "Grabs on hit";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Melee || !ctx.Check!.Result) return;
        var attacker = ctx.Source!;
        var target = ctx.Target.Unit;
        if (target == null) return;
        if (attacker.Grabbing != null || target.GrabbedBy != null) return;

        attacker.Grabbing = target;
        target.GrabbedBy = attacker;
        g.YouObserve(attacker, $"{attacker:The} {VTense(attacker, "grab")} {target:the}!");
    }
}

public class Constrict(Dice damage, DamageType type) : LogicBrick
{
    public override string Id => $"snake:constrict+{damage.Serialize()}/{type.SubCat}";
    public static readonly Constrict Small = new(d(6), DamageTypes.Blunt);
    public static readonly Constrict Medium = new(d(8), DamageTypes.Blunt);
    public static readonly Constrict Large = new(d(10) + 7, DamageTypes.Blunt);
    public static readonly Constrict Heavy = new(d(2, 8), DamageTypes.Blunt);
    public static readonly Constrict Crushing = new(d(4, 8), DamageTypes.Blunt);

    public static readonly Constrict SmallAcid = new(d(6), DamageTypes.Acid);
    public static readonly Constrict MediumAcid = new(d(8), DamageTypes.Acid);
    public static readonly Constrict LargeAcid = new(d(10) + 7, DamageTypes.Acid);
    public static readonly Constrict HeavyAcid = new(d(2, 8), DamageTypes.Acid);
    public static readonly Constrict CrushingAcid = new(d(4, 8), DamageTypes.Acid);

    public override string? PokedexDescription => $"Constrict {damage}";
    public override bool IsActive => true;

    protected override void OnRoundStart(Fact fact)
    {
        var unit = fact.Entity as IUnit;
        if (unit?.Grabbing is not { } victim) return;

        using var dmgCtx = PHContext.Create(unit, Target.From(victim));
        dmgCtx.Damage.Add(new DamageRoll { Formula = damage, Type = type });
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "crush")} {victim:the}!");
        DoDamage(dmgCtx);
    }
}

public static class Snakes
{
    public static readonly MonsterFamily Family = new("snake");

    static MonsterDef S(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 8, int ac = 0, int ab = 0, int dmg = 0,
        UnitSize size = UnitSize.Medium, int maxDepth = 99,
        WeaponDef? unarmed = null, ActionCost? speed = null,
        Func<MonsterDef>? growsInto = null)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = Family,
            CreatureType = CreatureTypes.Beast,
            Glyph = new('S', color),
            HpPerLevel = hp,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            LandMove = speed ?? ActionCosts.LandMove25,
            Unarmed = unarmed ?? NaturalWeapons.Bite_1d6,
            Size = size,
            BaseLevel = level,
            MaxDepth = maxDepth,
            StartingRot = Foods.RotSpoiled,
            MoralAxis = MoralAxis.Neutral,
            EthicalAxis = EthicalAxis.Neutral,
            GrowsInto = growsInto,
            Components = components,
        };
    }

    public static readonly MonsterDef Viper = S("viper", "viper", -1, ConsoleColor.White,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d4)), SnakeVenomLesser.DC10.OnHit()],
        hp: 4, ab: 1, dmg: -2, size: UnitSize.Tiny, maxDepth: 4,
        unarmed: NaturalWeapons.Bite_1d4, growsInto: () => GiantViper!);

    public static readonly MonsterDef SeaSnake = S("sea_snake", "sea snake", 1, ConsoleColor.Cyan,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d4)), SnakeVenomLesser.DC10.OnHit()],
        hp: 6, ab: 1, dmg: -1, size: UnitSize.Small, maxDepth: 6,
        unarmed: NaturalWeapons.Bite_1d4);

    public static readonly MonsterDef GiantViper = S("giant_viper", "giant viper", 3, ConsoleColor.Green,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d6)), SnakeVenomLesser.DC12.OnHit()],
        dmg: 1, maxDepth: 8);

    public static readonly MonsterDef PrinceCobra = S("prince_cobra", "prince cobra", 5, ConsoleColor.Yellow,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d6)), SnakeVenomGreater.DC13.OnHit()],
        dmg: 2, growsInto: () => CrownPrinceCobra!);

    public static readonly MonsterDef CrownPrinceCobra = S("crown_prince_cobra", "crown prince cobra", 6, ConsoleColor.Yellow,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d8)), SnakeVenomGreater.DC14.OnHit()],
        ac: 1, dmg: 3, unarmed: NaturalWeapons.Bite_1d8, growsInto: () => KingCobra!);

    public static readonly MonsterDef QueenConsortCobra = S("queen_consort_cobra", "queen consort cobra", 7, ConsoleColor.DarkYellow,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d8)), SnakeVenomGreater.DC14.OnHit()],
        hp: 10, ac: 1, ab: 1, dmg: 4, size: UnitSize.Large, unarmed: NaturalWeapons.Bite_1d8);

    public static readonly MonsterDef KingCobra = S("king_cobra", "king cobra", 8, ConsoleColor.Red,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d6)), SnakeVenomGreater.DC15.OnHit()],
        hp: 10, ac: 1, ab: 1, dmg: 5, size: UnitSize.Large, unarmed: NaturalWeapons.Bite_2d6,
        growsInto: () => EmperorCobra!);

    public static readonly MonsterDef GiantAnaconda = S("giant_anaconda", "giant anaconda", 9, ConsoleColor.Magenta,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d10)), GrabOnHit.Instance, Constrict.Large],
        hp: 12, ac: 1, ab: 1, dmg: 7, size: UnitSize.Huge,
        speed: ActionCosts.StandardLandMove, unarmed: NaturalWeapons.Bite_2d10);

    public static readonly MonsterDef EmperorCobra = S("emperor_cobra", "emperor cobra", 10, ConsoleColor.Magenta,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d6)), SnakeVenomGreater.DC17.OnHit()],
        hp: 10, ac: 2, ab: 2, dmg: 6, size: UnitSize.Large, unarmed: NaturalWeapons.Bite_2d6);

    public static readonly MonsterDef[] All = [
        Viper, SeaSnake, GiantViper,
        PrinceCobra, CrownPrinceCobra, QueenConsortCobra, KingCobra,
        GiantAnaconda, EmperorCobra,
    ];
}
