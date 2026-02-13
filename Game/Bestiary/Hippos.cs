namespace Pathhack.Game.Bestiary;

// Hippo family - attracted by cooking smells
// Base: Level 5 Large, AC 21, 85 HP, jaws 2d8+8, foot 1d10+8, Trample
//
// hippo: level 3, Medium, Beast
// hungry hippo: level 5 (base), Beast
// hungry hungry hippo: level 7, Beast
// hungrier hippo: level 9, Beast
// The Hungriest Hippo: level 12 unique, UNDEAD - ate itself to death and kept going
//
// Urgathoa followers: hippos are tame/peaceful
// Global HungryHippoCounter increases spawn chance

public static class Hippos
{
    public static readonly MonsterDef Hippo = new()
    {
        id = "hippo",
        Name = "hippo",
        Family = "hippo",
        Glyph = new('q', ConsoleColor.Gray),
        HpPerLevel = 8,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 2,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d8,
        Size = UnitSize.Medium,
        BaseLevel = 3,
        MinDepth = 3,
        MaxDepth = 8,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        GrowsInto = () => HungryHippo!,
    };

    public static readonly MonsterDef HungryHippo = new()
    {
        id = "hungry_hippo",
        Name = "hungry hippo",
        Family = "hippo",
        Glyph = new('q', ConsoleColor.DarkYellow),
        HpPerLevel = 10,
        AC = 1,
        AttackBonus = 0,
        DamageBonus = 3,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Bite_2d8,
        Size = UnitSize.Large,
        BaseLevel = 5,
        MinDepth = 5,
        MaxDepth = 10,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        GrowsInto = () => HungryHungryHippo!,
    };

    public static readonly MonsterDef HungryHungryHippo = new()
    {
        id = "hungry_hungry_hippo",
        Name = "hungry hungry hippo",
        Family = "hippo",
        Glyph = new('q', ConsoleColor.Yellow),
        HpPerLevel = 12,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 4,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_2d8,
        Size = UnitSize.Large,
        BaseLevel = 7,
        MinDepth = 7,
        MaxDepth = 12,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        GrowsInto = () => HungrierHippo!,
    };

    public static readonly MonsterDef HungrierHippo = new()
    {
        id = "hungrier_hippo",
        Name = "even hungrier hippo",
        Family = "hippo",
        Glyph = new('q', ConsoleColor.Red),
        HpPerLevel = 14,
        AC = 2,
        AttackBonus = 1,
        DamageBonus = 5,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Bite_2d10,
        Size = UnitSize.Large,
        BaseLevel = 9,
        MinDepth = 9,
        MaxDepth = 15,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
    };

    public static readonly MonsterDef HungriestHippo = new()
    {
        id = "hungriest_hippo",
        Name = "The Hungriest Hippo",
        Family = "hippo",
        Glyph = new('q', ConsoleColor.Magenta),
        HpPerLevel = 16,
        AC = 2,
        AttackBonus = 2,
        DamageBonus = 6,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_2d10,
        Size = UnitSize.Large,
        BaseLevel = 12,
        SpawnWeight = 0, // unique
        MinDepth = 12,
        MaxDepth = 99,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        CreatureType = CreatureTypes.Beast, // TODO: Undead
        IsUnique = true,
    };

    public static readonly MonsterDef[] All = [Hippo, HungryHippo, HungryHungryHippo, HungrierHippo, HungriestHippo];
}
