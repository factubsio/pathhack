namespace Pathhack.Game;

public static class MundaneArmory
{
    public static readonly WeaponDef Dagger = new()
    {
        id = "dagger",
        Name = "dagger",
        BaseDamage = d(4),
        Profiency = Proficiencies.Dagger,
        DamageType = DamageTypes.Piercing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
    };

    public static readonly WeaponDef Longsword = new()
    {
        id = "longsword",
        Name = "longsword",
        BaseDamage = d(8),
        Profiency = Proficiencies.Longsword,
        DamageType = DamageTypes.Slashing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
    };

    public static readonly WeaponDef Mace = new()
    {
        id = "mace",
        Name = "mace",
        BaseDamage = d(6),
        Profiency = Proficiencies.Mace,
        DamageType = DamageTypes.Blunt,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
    };

    public static readonly WeaponDef Club = new()
    {
        id = "club",
        Name = "club",
        BaseDamage = d(6),
        Profiency = Proficiencies.Club,
        DamageType = DamageTypes.Blunt,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
    };

    public static readonly WeaponDef SpikedClub = new()
    {
        id = "spiked_club",
        Name = "spiked club",
        BaseDamage = d(8),
        Profiency = Proficiencies.Club,
        DamageType = DamageTypes.Blunt,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
    };

    public static readonly WeaponDef Quarterstaff = new()
    {
        id = "quarterstaff",
        Name = "quarterstaff",
        BaseDamage = d(6),
        Profiency = Proficiencies.Club,
        DamageType = DamageTypes.Blunt,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
    };

    public static readonly WeaponDef Scimitar = new()
    {
        id = "scimitar",
        Name = "scimitar",
        BaseDamage = d(6),
        Profiency = Proficiencies.Scimitar,
        DamageType = DamageTypes.Slashing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
    };

    public static readonly WeaponDef Rapier = new()
    {
        id = "rapier",
        Name = "rapier",
        BaseDamage = d(6),
        Profiency = Proficiencies.Rapier,
        DamageType = DamageTypes.Piercing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
    };

    public static readonly WeaponDef Whip = new()
    {
        id = "whip",
        Name = "whip",
        BaseDamage = d(4),
        Profiency = Proficiencies.Whip,
        DamageType = DamageTypes.Slashing,
        Range = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
    };

    public static readonly WeaponDef SpikedChain = new()
    {
        id = "spiked_chain",
        Name = "spiked chain",
        BaseDamage = d(8),
        Profiency = Proficiencies.SpikedChain,
        DamageType = DamageTypes.Piercing,
        Range = 2,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkGray),
    };

    public static readonly WeaponDef Scythe = new()
    {
        id = "scythe",
        Name = "scythe",
        BaseDamage = d(10),
        Profiency = Proficiencies.Scythe,
        DamageType = DamageTypes.Slashing,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Gray),
    };

    public static readonly WeaponDef Falchion = new()
    {
        id = "falchion",
        Name = "falchion",
        BaseDamage = d(2, 4),
        Profiency = Proficiencies.Falchion,
        DamageType = DamageTypes.Slashing,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
    };

    public static readonly WeaponDef Spear = new()
    {
        id = "spear",
        Name = "spear",
        BaseDamage = d(6),
        Profiency = Proficiencies.Spear,
        DamageType = DamageTypes.Piercing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Launcher = "hand",
        MeleeVerb = "thrusts",
    };

    public static readonly WeaponDef Dart = new()
    {
        id = "dart",
        Name = "dart",
        BaseDamage = d(4),
        Profiency = Proficiencies.Dart,
        DamageType = DamageTypes.Piercing,
        Range = 4,
        Stackable = true,
        Launcher = "hand",
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
    };

    public static readonly ArmorDef LeatherArmor = new()
    {
        id = "leather_armor",
        Name = "leather armor",
        ACBonus = 1,
        DexCap = 4,
        Glyph = new(ItemClasses.Armor, ConsoleColor.DarkYellow),
        Components = [new ArmorBrick(1, 4)],
    };

    public static readonly ArmorDef ChainShirt = new()
    {
        id = "chain_shirt",
        Name = "chain shirt",
        ACBonus = 2,
        DexCap = 2,
        CheckPenalty = -1,
        Glyph = new(ItemClasses.Armor, ConsoleColor.Gray),
        Components = [new ArmorBrick(2, 2)],
    };

    public static readonly ItemDef RingOfKnives = new()
    {
        id = "ring_of_knives",
        Name = "ring of knives",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Cyan),
        DefaultEquipSlot = ItemSlots.Ring,
        Components = [new GrantProficiency(Proficiencies.Dagger, ProficiencyLevel.Trained, requiresEquipped: true)],
    };

    public static readonly WeaponDef[] AllWeapons = [Dagger, Longsword, Scimitar, Rapier, Whip, SpikedChain, Scythe, Falchion];
}
