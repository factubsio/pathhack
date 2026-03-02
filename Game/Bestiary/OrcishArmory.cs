namespace Pathhack.Game;

public static class OrcishArmory
{
    public static readonly WeaponDef KnuckleDagger = new()
    {
        id = "orc_knuckle_dagger",
        Name = "orc knuckle dagger",
        BaseDamage = d(6),
        Style = WeaponStyle.Skewer,
        Grip = WeaponGrip.Light,
        WeaponType = WeaponTypes.OrcKnuckleDagger,
        DamageType = DamageTypes.Piercing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkGray),
        Weight = 15,
        Price = 10,
    };

    public static readonly WeaponDef Necksplitter = new()
    {
        id = "orc_necksplitter",
        Name = "orc necksplitter",
        BaseDamage = d(8),
        Style = WeaponStyle.Carve,
        Grip = WeaponGrip.Heavy,
        WeaponType = WeaponTypes.OrcNecksplitter,
        DamageType = DamageTypes.Slashing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkGray),
        Weight = 60,
        Price = 30,
    };

    public static readonly ArmorDef Breastplate = new()
    {
        id = "orcish_breastplate",
        Name = "orcish breastplate",
        ArmorType = ArmorTypes.Breastplate,
        Proficiency = Proficiencies.MediumArmor,
        ACBonus = 2,
        DexCap = 1,
        CheckPenalty = -2,
        Glyph = new(ItemClasses.Armor, ConsoleColor.DarkGray),
        Components = [new ArmorBrick(2, 1)],
        Weight = 60,
        Price = 50,
    };
}
