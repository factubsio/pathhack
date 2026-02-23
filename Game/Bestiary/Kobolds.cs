namespace Pathhack.Game.Bestiary;

public static class Kobolds
{
    public static readonly MonsterFamily Family = new("kobold");

    static MonsterDef K(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int ac = 0, int ab = 0, int dmg = 0,
        int maxDepth = 5, ActionCost? speed = null, Func<MonsterDef>? growsInto = null)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = Family,
            CreatureType = CreatureTypes.Humanoid,
            Glyph = new('k', color),
            HpPerLevel = 6,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            LandMove = speed ?? ActionCosts.LandMove20,
            Unarmed = NaturalWeapons.Fist,
            Size = UnitSize.Small,
            BaseLevel = level,
            MaxDepth = maxDepth,
            MoralAxis = MoralAxis.Evil,
            EthicalAxis = EthicalAxis.Lawful,
            GrowsInto = growsInto,
            Components = components,
        };
    }

    public static readonly MonsterDef Basic = K("kobold", "kobold", -1, ConsoleColor.DarkYellow,
        [EquipSet.OneOf(MundaneArmory.Dagger, MundaneArmory.Spear), new GrantAction(AttackWithWeapon.Instance)],
        dmg: -1, maxDepth: 3, growsInto: () => Scout!);

    public static readonly MonsterDef Scout = K("kobold_scout", "kobold scout", 0, ConsoleColor.Green,
        [new Equip(MundaneArmory.Dagger), EquipSet.Roll(MundaneArmory.LeatherArmor, 50), new GrantAction(AttackWithWeapon.Instance)],
        ac: 1, ab: 1, maxDepth: 4, speed: ActionCosts.StandardLandMove, growsInto: () => Warrior!);

    public static readonly MonsterDef Warrior = K("kobold_warrior", "kobold warrior", 1, ConsoleColor.Red,
        [new Equip(MundaneArmory.Spear), new Equip(MundaneArmory.LeatherArmor), new GrantAction(AttackWithWeapon.Instance)],
        ac: 1, dmg: -1);

    public static readonly MonsterDef[] All = [Basic, Scout, Warrior];
}
