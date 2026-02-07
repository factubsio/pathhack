namespace Pathhack.Game.Bestiary;

public class SnakeVenomLesser(int dc) : AfflictionBrick(dc)
{
    public static readonly SnakeVenomLesser DC12 = new(12);
    public static readonly SnakeVenomLesser DC14 = new(14);
    public static readonly SnakeVenomLesser DC16 = new(16);

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

public class SnakeVenomGreater(int dc) : AfflictionBrick(dc)
{
    public static readonly SnakeVenomGreater DC17 = new(17);
    public static readonly SnakeVenomGreater DC19 = new(19);
    public static readonly SnakeVenomGreater DC21 = new(21);

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

public static class Snakes
{
    public static readonly MonsterDef Viper = new()
    {
        id = "viper",
        Name = "viper",
        Glyph = new('S', ConsoleColor.White),
        HpPerLevel = 4,
        AC = 0,
        AttackBonus = 1,
        DamageBonus = -2,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d4,
        Size = UnitSize.Tiny,
        BaseLevel = -1,
        SpawnWeight = 2,
        MinDepth = 1,
        MaxDepth = 4,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d4)),
            SnakeVenomLesser.DC16.OnHit(),
        ],
    };

    public static readonly MonsterDef[] All = [Viper];
}
