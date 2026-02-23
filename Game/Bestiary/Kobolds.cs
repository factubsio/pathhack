namespace Pathhack.Game.Bestiary;

public static class Kobolds
{
    public static readonly MonsterFamily Family = new("kobold");

    public static readonly MonsterDef Basic = new()
    {
        id = "kobold",
        Name = "kobold",
        Family = Family,
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('k', ConsoleColor.DarkYellow),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = -1,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = -1,
        MaxDepth = 3,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Lawful,
        GrowsInto = () => Scout!,
        Components = [
            EquipSet.OneOf(MundaneArmory.Dagger, MundaneArmory.Spear),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef Scout = new()
    {
        id = "kobold_scout",
        Name = "kobold scout",
        Family = Family,
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('k', ConsoleColor.Green),
        HpPerLevel = 6,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 0,
        MaxDepth = 4,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Lawful,
        GrowsInto = () => Warrior!,
        Components = [
            new Equip(MundaneArmory.Dagger),
            EquipSet.Roll(MundaneArmory.LeatherArmor, 50),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef Warrior = new()
    {
        id = "kobold_warrior",
        Name = "kobold warrior",
        Family = Family,
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('k', ConsoleColor.Red),
        HpPerLevel = 6,
        AC = 1,
        AttackBonus = 0,
        DamageBonus = -1,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 1,
        MaxDepth = 5,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Lawful,
        Components = [
            new Equip(MundaneArmory.Spear),
            new Equip(MundaneArmory.LeatherArmor),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef[] All = [Basic, Scout, Warrior];
}
