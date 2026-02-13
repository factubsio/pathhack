namespace Pathhack.Game;

public static class Gems
{
    public static readonly WeaponDef Rock = new()
    {
        id = "rock",
        Name = "rock",
        BaseDamage = d(6),
        Profiency = Proficiencies.Thrown,
        WeaponType = WeaponTypes.Rock,
        DamageType = DamageTypes.Blunt,
        Launcher = "hand",
        Stackable = true,
        Glyph = new(ItemClasses.Gem, ConsoleColor.Gray),
        Price = 0,
    };
}
