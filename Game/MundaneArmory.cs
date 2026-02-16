namespace Pathhack.Game;

public static class MundaneArmory
{
    public static readonly WeaponDef Dagger = new()
    {
        id = "dagger",
        Name = "dagger",
        BaseDamage = d(4),
        Profiency = Proficiencies.LightBlade,
        WeaponType = WeaponTypes.Dagger,
        DamageType = DamageTypes.Piercing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
        Price = 2,
    };

    public static readonly WeaponDef Shortsword = new()
    {
        id = "shortsword",
        Name = "shortsword",
        BaseDamage = d(6),
        Profiency = Proficiencies.LightBlade,
        WeaponType = WeaponTypes.Shortsword,
        DamageType = DamageTypes.Slashing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
        Price = 9,
    };

    public static readonly WeaponDef Longsword = new()
    {
        id = "longsword",
        Name = "longsword",
        BaseDamage = d(8),
        Profiency = Proficiencies.HeavyBlade,
        WeaponType = WeaponTypes.Longsword,
        DamageType = DamageTypes.Slashing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
        Price = 10,
    };

    public static readonly WeaponDef Scimitar = new()
    {
        id = "scimitar",
        Name = "scimitar",
        BaseDamage = d(6),
        Profiency = Proficiencies.HeavyBlade,
        WeaponType = WeaponTypes.Scimitar,
        DamageType = DamageTypes.Slashing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
        Price = 10,
    };

    public static readonly WeaponDef Rapier = new()
    {
        id = "rapier",
        Name = "rapier",
        BaseDamage = d(6),
        Profiency = Proficiencies.LightBlade,
        WeaponType = WeaponTypes.Rapier,
        DamageType = DamageTypes.Piercing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
        Price = 20,
    };

    public static readonly WeaponDef Falchion = new()
    {
        id = "falchion",
        Name = "falchion",
        BaseDamage = d(2, 4),
        Profiency = Proficiencies.HeavyBlade,
        WeaponType = WeaponTypes.Falchion,
        DamageType = DamageTypes.Slashing,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
        Price = 30,
    };

    public static readonly WeaponDef Club = new()
    {
        id = "club",
        Name = "club",
        BaseDamage = d(6),
        Profiency = Proficiencies.Club,
        WeaponType = WeaponTypes.Club,
        DamageType = DamageTypes.Blunt,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Price = 1,
    };

    public static readonly WeaponDef SpikedClub = new()
    {
        id = "spiked_club",
        Name = "spiked club",
        BaseDamage = d(8),
        Profiency = Proficiencies.Club,
        WeaponType = WeaponTypes.Club,
        DamageType = DamageTypes.Blunt,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Price = 2,
    };

    public static readonly WeaponDef Greatclub = new()
    {
        id = "greatclub",
        Name = "greatclub",
        BaseDamage = d(10),
        Profiency = Proficiencies.Club,
        WeaponType = WeaponTypes.Greatclub,
        DamageType = DamageTypes.Blunt,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Price = 10,
    };

    public static readonly WeaponDef Mace = new()
    {
        id = "mace",
        Name = "mace",
        BaseDamage = d(6),
        Profiency = Proficiencies.Club,
        WeaponType = WeaponTypes.Mace,
        DamageType = DamageTypes.Blunt,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
        Price = 10,
    };

    public static readonly WeaponDef Flail = new()
    {
        id = "flail",
        Name = "flail",
        BaseDamage = d(6),
        Profiency = Proficiencies.Flail,
        WeaponType = WeaponTypes.Flail,
        DamageType = DamageTypes.Blunt,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Gray),
        Price = 8,
    };

    public static readonly WeaponDef Quarterstaff = new()
    {
        id = "quarterstaff",
        Name = "quarterstaff",
        BaseDamage = d(6),
        Profiency = Proficiencies.Staff,
        WeaponType = WeaponTypes.Quarterstaff,
        DamageType = DamageTypes.Blunt,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Price = 1,
    };

    public static readonly WeaponDef BoStaff = new()
    {
        id = "bo_staff",
        Name = "bo staff",
        BaseDamage = d(8),
        Profiency = Proficiencies.Staff,
        WeaponType = WeaponTypes.BoStaff,
        DamageType = DamageTypes.Blunt,
        Hands = 2,
        Range = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Price = 2,
    };

    public static readonly WeaponDef Spear = new()
    {
        id = "spear",
        Name = "spear",
        BaseDamage = d(6),
        Profiency = Proficiencies.Polearm,
        WeaponType = WeaponTypes.Spear,
        DamageType = DamageTypes.Piercing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Launcher = "hand",
        MeleeVerb = "thrusts",
        Range = 4,
        Price = 1,
    };

    public static readonly WeaponDef Scythe = new()
    {
        id = "scythe",
        Name = "scythe",
        BaseDamage = d(10),
        Profiency = Proficiencies.Polearm,
        WeaponType = WeaponTypes.Scythe,
        DamageType = DamageTypes.Slashing,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Gray),
        Price = 20,
    };

    public static readonly WeaponDef Whip = new()
    {
        id = "whip",
        Name = "whip",
        BaseDamage = d(4),
        Profiency = Proficiencies.Whip,
        WeaponType = WeaponTypes.Whip,
        DamageType = DamageTypes.Slashing,
        Range = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Price = 1,
    };

    public static readonly WeaponDef SpikedChain = new()
    {
        id = "spiked_chain",
        Name = "spiked chain",
        BaseDamage = d(8),
        Profiency = Proficiencies.Whip,
        WeaponType = WeaponTypes.SpikedChain,
        DamageType = DamageTypes.Piercing,
        Range = 2,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkGray),
        Price = 30,
    };

    public static readonly WeaponDef Longbow = new()
    {
        id = "longbow",
        Name = "longbow",
        BaseDamage = d(8),
        Profiency = Proficiencies.Bow,
        WeaponType = WeaponTypes.Longbow,
        DamageType = DamageTypes.Piercing,
        Hands = 2,
        Range = 10,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Price = 60,
    };

    public static readonly WeaponDef Dart = new()
    {
        id = "dart",
        Name = "dart",
        BaseDamage = d(4),
        Profiency = Proficiencies.Thrown,
        WeaponType = WeaponTypes.Dart,
        DamageType = DamageTypes.Piercing,
        Range = 4,
        Stackable = true,
        Launcher = "hand",
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
        Price = 1,
    };

    public static readonly WeaponDef Bola = new()
    {
        id = "bola",
        Name = "bola",
        BaseDamage = d(6),
        Profiency = Proficiencies.Thrown,
        WeaponType = WeaponTypes.Bola,
        DamageType = DamageTypes.Blunt,
        Launcher = "hand",
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Price = 5,
    };

    public static readonly WeaponDef Hatchet = new()
    {
        id = "hatchet",
        Name = "hatchet",
        BaseDamage = d(6),
        Profiency = Proficiencies.Axe,
        WeaponType = WeaponTypes.Hatchet,
        DamageType = DamageTypes.Slashing,
        Launcher = "hand",
        Stackable = true,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Gray),
        Price = 4,
    };

    public static readonly WeaponDef Battleaxe = new()
    {
        id = "battleaxe",
        Name = "battleaxe",
        BaseDamage = d(8),
        Profiency = Proficiencies.Axe,
        WeaponType = WeaponTypes.Axe,
        DamageType = DamageTypes.Slashing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Gray),
        Price = 10,
    };

    public static readonly WeaponDef DwarvenWaraxe = new()
    {
        id = "dwarven_waraxe",
        Name = "dwarven waraxe",
        BaseDamage = d(10),
        Profiency = Proficiencies.Axe,
        WeaponType = WeaponTypes.Axe,
        DamageType = DamageTypes.Slashing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.White),
        Price = 30,
    };

    public static readonly WeaponDef Greataxe = new()
    {
        id = "greataxe",
        Name = "greataxe",
        BaseDamage = d(12),
        Profiency = Proficiencies.Axe,
        WeaponType = WeaponTypes.Greataxe,
        DamageType = DamageTypes.Slashing,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Gray),
        Price = 20,
    };

    public static readonly WeaponDef Pickaxe = new()
    {
        id = "pickaxe",
        Name = "pickaxe",
        BaseDamage = d(6),
        Profiency = Proficiencies.Axe,
        WeaponType = WeaponTypes.Pick,
        DamageType = DamageTypes.Piercing,
        MeleeVerb = "strikes",
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Gray),
        Price = 14,
        Components = [DiggerIdentity.Instance, DiggerVerb.Instance],
    };

    public static readonly WeaponDef Gandasa = new()
    {
        id = "gandasa",
        Name = "gandasa",
        BaseDamage = d(2, 4),
        Profiency = Proficiencies.Axe,
        WeaponType = WeaponTypes.Greataxe,
        DamageType = DamageTypes.Slashing,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkYellow),
        Price = 15,
    };

    public static readonly ArmorDef LeatherArmor = new()
    {
        id = "leather_armor",
        Name = "leather armor",
        Proficiency = Proficiencies.LightArmor,
        ACBonus = 1,
        DexCap = 4,
        Glyph = new(ItemClasses.Armor, ConsoleColor.DarkYellow),
        Components = [new ArmorBrick(1, 4)],
        Price = 20,
    };

    public static readonly ArmorDef ChainShirt = new()
    {
        id = "chain_shirt",
        Name = "chain shirt",
        Proficiency = Proficiencies.LightArmor,
        ACBonus = 2,
        DexCap = 3,
        CheckPenalty = -1,
        Glyph = new(ItemClasses.Armor, ConsoleColor.Gray),
        Components = [new ArmorBrick(2, 3)],
        Price = 50,
    };

    public static readonly ArmorDef HideArmor = new()
    {
        id = "hide_armor",
        Name = "hide armor",
        Proficiency = Proficiencies.MediumArmor,
        ACBonus = 3,
        DexCap = 2,
        CheckPenalty = -2,
        Glyph = new(ItemClasses.Armor, ConsoleColor.DarkYellow),
        Components = [new ArmorBrick(3, 2)],
        Price = 20,
    };

    public static readonly ArmorDef Breastplate = new()
    {
        id = "breastplate",
        Name = "breastplate",
        Proficiency = Proficiencies.MediumArmor,
        ACBonus = 4,
        DexCap = 1,
        CheckPenalty = -2,
        Glyph = new(ItemClasses.Armor, ConsoleColor.Gray),
        Components = [new ArmorBrick(4, 1)],
        Price = 80,
    };

    public static readonly ArmorDef SplintMail = new()
    {
        id = "splint_mail",
        Name = "splint mail",
        Proficiency = Proficiencies.HeavyArmor,
        ACBonus = 4,
        DexCap = 1,
        CheckPenalty = -3,
        Glyph = new(ItemClasses.Armor, ConsoleColor.DarkGray),
        Components = [new ArmorBrick(4, 1)],
        Price = 130,
    };

    public static readonly ArmorDef FullPlate = new()
    {
        id = "full_plate",
        Name = "full plate",
        Proficiency = Proficiencies.HeavyArmor,
        ACBonus = 6,
        DexCap = 0,
        CheckPenalty = -3,
        Glyph = new(ItemClasses.Armor, ConsoleColor.White),
        Components = [new ArmorBrick(6, 0)],
        Price = 300,
    };

    public static readonly ItemDef RingOfKnives = new()
    {
        id = "ring_of_knives",
        Name = "ring of knives",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Cyan),
        DefaultEquipSlot = ItemSlots.Ring,
        Components = [new GrantProficiency(Proficiencies.LightBlade, ProficiencyLevel.Trained, requiresEquipped: true)],
        Price = 400,
    };

    public static readonly WeaponDef[] AllWeapons = [Dagger, Shortsword, Longsword, Scimitar, Rapier, Falchion, Club, SpikedClub, Greatclub, Mace, Flail, Quarterstaff, BoStaff, Spear, Scythe, Whip, SpikedChain, Longbow, Dart, Bola, Hatchet, Battleaxe, DwarvenWaraxe, Greataxe, Gandasa, Pickaxe];
    public static readonly ArmorDef[] AllArmors = [LeatherArmor, ChainShirt, HideArmor, Breastplate, SplintMail, FullPlate];
}
