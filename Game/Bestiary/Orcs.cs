namespace Pathhack.Game.Bestiary;

public static class Orcs
{
    public static readonly MonsterFamily Family = new("orc");

    static readonly LogicBrick[] CommonArmor = [
        EquipSet.Roll(OrcishArmory.ChainMail, 40),
        EquipSet.Roll(MundaneArmory.LeatherArmor, 40),
    ];

    static readonly LogicBrick[] Common = [
        ..CommonArmor,
        new GrantAction(AttackWithWeapon.Instance),
        new Ferocity(),
    ];

    static MonsterDef O(string id, string name, int level, ConsoleColor color,
        LogicBrick[] extra, int ac = 0, int ab = 0, int dmg = 0)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = Family,
            CreatureType = CreatureTypes.Humanoid,
            Glyph = new('o', color),
            HpPerLevel = 8,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            LandMove = ActionCosts.LandMove25,
            Unarmed = NaturalWeapons.Fist,
            Size = UnitSize.Medium,
            BaseLevel = level,
            MoralAxis = MoralAxis.Evil,
            EthicalAxis = EthicalAxis.Chaotic,
            Components = [.. Common, .. extra],
        };
    }

    public static readonly MonsterDef OrcScrapper = O("orc_scrapper", "orc scrapper", 0, ConsoleColor.DarkGreen,
        [new Equip(OrcishArmory.KnuckleDagger), EquipSet.Roll(MundaneArmory.Spear, 30)],
        dmg: 3);

    public static readonly MonsterDef OrcVeteran = O("orc_veteran", "orc veteran", 1, ConsoleColor.Green,
        [new Equip(OrcishArmory.Necksplitter), EquipSet.Roll(MundaneArmory.Shortsword, 50), EquipSet.Roll(MundaneArmory.Spear, 30)],
        ac: 2, dmg: 4);

    public static readonly MonsterDef OrcCommander = O("orc_commander", "orc commander", 2, ConsoleColor.Yellow,
        [new Equip(MundaneArmory.Greatclub), EquipSet.Roll(MundaneArmory.Spear, 50)],
        ac: 2, ab: 2, dmg: 4);

    public static readonly MonsterDef OrcRampager = O("orc_rampager", "orc rampager", 4, ConsoleColor.Red,
        [new Equip(OrcishArmory.Necksplitter), EquipSet.Roll(MundaneArmory.Longbow, 60)],
        ac: 1, ab: 2, dmg: 9);

    public static readonly MonsterDef OrcGamekeeper = O("orc_gamekeeper", "orc gamekeeper", 4, ConsoleColor.DarkYellow,
        [new Equip(MundaneArmory.Whip), EquipSet.Roll(MundaneArmory.Bola, 80)],
        ac: 2, ab: 2, dmg: 9);

    public static readonly MonsterDef OrcDoomsayer = O("orc_doomsayer", "orc doomsayer", 5, ConsoleColor.Magenta,
        [new Equip(MundaneArmory.Flail)],
        ac: 2, ab: 1, dmg: 3);

    public static readonly MonsterDef OrcVeteranMaster = O("orc_veteran_master", "orc veteran master", 10, ConsoleColor.DarkMagenta,
        [new Equip(MundaneArmory.BoStaff), EquipSet.Roll(MundaneArmory.Longbow, 70)],
        ac: 2, ab: 2, dmg: 13);
}
