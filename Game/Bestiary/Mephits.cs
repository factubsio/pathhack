namespace Pathhack.Game.Bestiary;

record MephitType(
    string Name,
    ConsoleColor Color,
    DamageType BreathType,
    BreathShape BreathShape,
    DamageType? Immune
);

public static class Mephits
{
    public static readonly MonsterFamily Family = new("mephit");

    static readonly MephitType[] Types =
    [
        new("fire",      ConsoleColor.Red,        DamageTypes.Fire,     BreathShape.Cone, DamageTypes.Fire),
        new("ice",       ConsoleColor.White,       DamageTypes.Cold,     BreathShape.Cone, DamageTypes.Cold),
        new("earth",     ConsoleColor.DarkYellow,  DamageTypes.Acid,     BreathShape.Line, DamageTypes.Acid),
        new("water",     ConsoleColor.Blue,        DamageTypes.Cold,     BreathShape.Cone, null),
        new("dust",      ConsoleColor.Gray,        DamageTypes.Slashing, BreathShape.Cone, null),
        new("steam",     ConsoleColor.DarkGray,    DamageTypes.Fire,     BreathShape.Cone, null),
        new("salt",      ConsoleColor.Cyan,        DamageTypes.Piercing, BreathShape.Line, null),
        new("mud",       ConsoleColor.DarkYellow,  DamageTypes.Acid,     BreathShape.Line, null),
    ];

    static LogicBrick[] Components(MephitType type)
    {
        List<LogicBrick> c =
        [
            ElementalTraits.Instance,
            ElementalFlight.Instance,
        ];

        if (type.Immune != null)
            c.Add(EnergyResist.RampFor(type.Immune.Value).Immune);

        c.Add(new GrantAction(new BreathAttack(type.BreathShape, type.BreathType, type.Color, _ => 3, _ => d(4), type.BreathShape == BreathShape.Cone ? 2 : 4)));
        c.Add(new GrantAction(new NaturalAttack(NaturalWeapons.Slam_1d4)));
        return [.. c];
    }

    static MonsterDef Make(MephitType type) => new()
    {
        id = $"mephit_{type.Name}",
        Name = $"{type.Name} mephit",
        Family = Family,
        BrainFlags = MonFlags.NoCorpse,
        CreatureType = CreatureTypes.Outsider,
        Subtypes = ["Elemental"],
        Glyph = new('v', type.Color),
        HpPerLevel = 4,
        AC = 0,
        AttackBonus = 0,
        Unarmed = NaturalWeapons.Slam_1d4,
        LandMove = ActionCosts.LandMove20,
        Size = UnitSize.Small,
        BaseLevel = 3,
        SpawnWeight = 10,
        GroupSize = GroupSize.SmallMixed,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        Components = Components(type),
    };

    public static readonly MonsterDef[] All = [.. Types.Select(Make)];
}
