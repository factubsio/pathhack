namespace Pathhack.Game.Bestiary;

public class SnakeVenomLesser(int dc) : AfflictionBrick(dc, "poison")
{
    public override AbilityTags Tags => AbilityTags.Biological;
    public static readonly SnakeVenomLesser DC10 = new(10);
    public static readonly SnakeVenomLesser DC12 = new(12);
    public static readonly SnakeVenomLesser DC14 = new(14);

    public override string AfflictionName => "Snake Venom";
    public override int MaxStage => 3;
    public override DiceFormula TickInterval => d(10) + 10;
    public override int? AutoCureMax => 600;

    protected override void DoPeriodicEffect(IUnit unit, int stage)
    {
        using var ctx = PHContext.Create(unit, Target.From(unit));
        ctx.Damage.Add(new DamageRoll { Formula = d(4), Type = DamageTypes.Poison });
        DoDamage(ctx);
        
        if (stage == 1)
            g.pline($"{unit:The} {VTense(unit, "feel")} poisoned!");
    }

    protected override object? DoQuery(int stage, string key, string? arg) =>
        key == "stat/Str" && stage >= 2 ? new Modifier(ModifierCategory.StatusPenalty, -2, "snake venom") : null;
}

public class SnakeVenomGreater(int dc) : AfflictionBrick(dc, "poison")
{
    public override AbilityTags Tags => AbilityTags.Biological;
    public static readonly SnakeVenomGreater DC13 = new(13);
    public static readonly SnakeVenomGreater DC14 = new(14);
    public static readonly SnakeVenomGreater DC15 = new(15);
    public static readonly SnakeVenomGreater DC17 = new(17);

    public override string AfflictionName => "Virulent Snake Venom";
    public override int MaxStage => 5;
    public override DiceFormula TickInterval => d(10) + 10;
    public override int? AutoCureMax => 800;

    protected override void DoPeriodicEffect(IUnit unit, int stage)
    {
        using var ctx = PHContext.Create(unit, Target.From(unit));
        DiceFormula formula = stage >= 5 ? d(2, 6) : stage >= 3 ? d(8) : d(6);
        ctx.Damage.Add(new DamageRoll { Formula = formula, Type = DamageTypes.Poison });
        DoDamage(ctx);
        
        if (stage == 1)
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
        g.pline($"{attacker:The} {VTense(attacker, "grab")} {target:the}!");
    }
}

public class Constrict(DiceFormula damage) : LogicBrick
{
    public static readonly Constrict Small = new(d(6));
    public static readonly Constrict Medium = new(d(8));
    public static readonly Constrict Large = new(d(10) + 7);
    
    public override string? PokedexDescription => $"Constrict {damage}";
    public override bool IsActive => true;
    
    protected override void OnRoundStart(Fact fact)
    {
        var unit = fact.Entity as IUnit;
        if (unit?.Grabbing is not { } victim) return;

        using var dmgCtx = PHContext.Create(unit, Target.From(victim));
        dmgCtx.Damage.Add(new DamageRoll { Formula = damage, Type = DamageTypes.Blunt });
        g.pline($"{unit:The} {VTense(unit, "crush")} {victim:the}!");
        DoDamage(dmgCtx);
    }
}

public static class Snakes
{
    public static readonly MonsterDef Viper = new()
    {
        id = "viper",
        Name = "viper",
        Family = "snake",
        StartingRot = Foods.RotSpoiled,
        Glyph = new('S', ConsoleColor.White),
        HpPerLevel = 4,
        AC = 0,
        AttackBonus = 1,
        DamageBonus = -2,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Bite_1d4,
        Size = UnitSize.Tiny,
        BaseLevel = -1,
        SpawnWeight = 2,
        MinDepth = 1,
        MaxDepth = 4,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        GrowsInto = () => GiantViper!,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d4)),
            SnakeVenomLesser.DC10.OnHit(),
        ],
    };

    public static readonly MonsterDef SeaSnake = new()
    {
        id = "sea_snake",
        Name = "sea snake",
        Family = "snake",
        StartingRot = Foods.RotSpoiled,
        Glyph = new('S', ConsoleColor.Cyan),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 1,
        DamageBonus = -1,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Bite_1d4,
        Size = UnitSize.Small,
        BaseLevel = 1,
        SpawnWeight = 2,
        MinDepth = 1,
        MaxDepth = 6,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d4)),
            SnakeVenomLesser.DC10.OnHit(),
        ],
    };

    public static readonly MonsterDef GiantViper = new()
    {
        id = "giant_viper",
        Name = "giant viper",
        Family = "snake",
        StartingRot = Foods.RotSpoiled,
        Glyph = new('S', ConsoleColor.Green),
        HpPerLevel = 8,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 1,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Bite_1d6,
        Size = UnitSize.Medium,
        BaseLevel = 3,
        SpawnWeight = 2,
        MinDepth = 3,
        MaxDepth = 8,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d6)),
            SnakeVenomLesser.DC12.OnHit(),
        ],
    };

    public static readonly MonsterDef PrinceCobra = new()
    {
        id = "prince_cobra",
        Name = "prince cobra",
        Family = "snake",
        StartingRot = Foods.RotSpoiled,
        Glyph = new('S', ConsoleColor.Yellow),
        HpPerLevel = 8,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 2,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Bite_1d6,
        Size = UnitSize.Medium,
        BaseLevel = 5,
        SpawnWeight = 2,
        MinDepth = 5,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        GrowsInto = () => CrownPrinceCobra!,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d6)),
            SnakeVenomGreater.DC13.OnHit(),
        ],
    };

    public static readonly MonsterDef CrownPrinceCobra = new()
    {
        id = "crown_prince_cobra",
        Name = "crown prince cobra",
        Family = "snake",
        StartingRot = Foods.RotSpoiled,
        Glyph = new('S', ConsoleColor.Yellow),
        HpPerLevel = 8,
        AC = 1,
        AttackBonus = 0,
        DamageBonus = 3,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Bite_1d8,
        Size = UnitSize.Medium,
        BaseLevel = 6,
        SpawnWeight = 2,
        MinDepth = 6,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        GrowsInto = () => KingCobra!,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d8)),
            SnakeVenomGreater.DC14.OnHit(),
        ],
    };

    public static readonly MonsterDef QueenConsortCobra = new()
    {
        id = "queen_consort_cobra",
        Name = "queen consort cobra",
        Family = "snake",
        StartingRot = Foods.RotSpoiled,
        Glyph = new('S', ConsoleColor.DarkYellow),
        HpPerLevel = 10,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 4,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Bite_1d8,
        Size = UnitSize.Large,
        BaseLevel = 7,
        SpawnWeight = 1,
        MinDepth = 7,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d8)),
            SnakeVenomGreater.DC14.OnHit(),
        ],
    };

    public static readonly MonsterDef KingCobra = new()
    {
        id = "king_cobra",
        Name = "king cobra",
        Family = "snake",
        StartingRot = Foods.RotSpoiled,
        Glyph = new('S', ConsoleColor.Red),
        HpPerLevel = 10,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 5,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Bite_2d6,
        Size = UnitSize.Large,
        BaseLevel = 8,
        SpawnWeight = 1,
        MinDepth = 8,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        GrowsInto = () => EmperorCobra!,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d6)),
            SnakeVenomGreater.DC15.OnHit(),
        ],
    };

    public static readonly MonsterDef GiantAnaconda = new()
    {
        id = "giant_anaconda",
        Name = "giant anaconda",
        Family = "snake",
        StartingRot = Foods.RotSpoiled,
        Glyph = new('S', ConsoleColor.Magenta),
        HpPerLevel = 12,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 7,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_2d10,
        Size = UnitSize.Huge,
        BaseLevel = 9,
        SpawnWeight = 1,
        MinDepth = 10,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d10)),
            GrabOnHit.Instance,
            Constrict.Large,
        ],
    };

    public static readonly MonsterDef EmperorCobra = new()
    {
        id = "emperor_cobra",
        Name = "emperor cobra",
        Family = "snake",
        StartingRot = Foods.RotSpoiled,
        Glyph = new('S', ConsoleColor.Magenta),
        HpPerLevel = 10,
        AC = 2,
        AttackBonus = 2,
        DamageBonus = 6,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Bite_2d6,
        Size = UnitSize.Large,
        BaseLevel = 10,
        SpawnWeight = 1,
        MinDepth = 12,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d6)),
            SnakeVenomGreater.DC17.OnHit(),
            // TODO: FlareHood fear aura
        ],
    };

    public static readonly MonsterDef[] All = [
        Viper, SeaSnake, GiantViper,
        PrinceCobra, CrownPrinceCobra, QueenConsortCobra, KingCobra,
        GiantAnaconda, EmperorCobra,
    ];
}
