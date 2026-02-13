namespace Pathhack.Game.Bestiary;

public static class Orcs
{
    static readonly LogicBrick[] CommonArmor = [
        EquipSet.Roll(OrcishArmory.ChainMail, 40),
        EquipSet.Roll(MundaneArmory.LeatherArmor, 40),
    ];

    static readonly LogicBrick[] Common = [
        ..CommonArmor,
        new GrantAction(AttackWithWeapon.Instance),
        new Ferocity(),
    ];

    public static readonly MonsterDef OrcScrapper = new()
    {
        id = "orc_scrapper",
        Name = "orc scrapper",
        Family = "orc",
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('o', ConsoleColor.DarkGreen),
        HpPerLevel = 8,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 3,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        BaseLevel = 0,
        MinDepth = 1,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            ..Common,
            new Equip(OrcishArmory.KnuckleDagger),
            EquipSet.Roll(MundaneArmory.Spear, 30),
        ],
    };

    public static readonly MonsterDef OrcVeteran = new()
    {
        id = "orc_veteran",
        Name = "orc veteran",
        Family = "orc",
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('o', ConsoleColor.Green),
        HpPerLevel = 9,
        AC = 2,
        AttackBonus = 0,
        DamageBonus = 4,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        BaseLevel = 1,
        MinDepth = 2,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            ..Common,
            new Equip(OrcishArmory.Necksplitter),
            EquipSet.Roll(MundaneArmory.Shortsword, 50),
            EquipSet.Roll(MundaneArmory.Spear, 30),
        ],
    };

    public static readonly MonsterDef OrcCommander = new()
    {
        id = "orc_commander",
        Name = "orc commander",
        Family = "orc",
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('o', ConsoleColor.Yellow),
        HpPerLevel = 10,
        AC = 2,
        AttackBonus = 2,
        DamageBonus = 4,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        BaseLevel = 2,
        MinDepth = 4,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            ..Common,
            new Equip(MundaneArmory.Greatclub),
            EquipSet.Roll(MundaneArmory.Spear, 50),
        ],
    };

    public static readonly MonsterDef OrcRampager = new()
    {
        id = "orc_rampager",
        Name = "orc rampager",
        Family = "orc",
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('o', ConsoleColor.Red),
        HpPerLevel = 10,
        AC = 1,
        AttackBonus = 2,
        DamageBonus = 9,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        BaseLevel = 4,
        MinDepth = 6,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            ..Common,
            new Equip(OrcishArmory.Necksplitter),
            EquipSet.Roll(MundaneArmory.Longbow, 60),
        ],
    };

    public static readonly MonsterDef OrcGamekeeper = new()
    {
        id = "orc_gamekeeper",
        Name = "orc gamekeeper",
        Family = "orc",
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('o', ConsoleColor.DarkYellow),
        HpPerLevel = 9,
        AC = 2,
        AttackBonus = 2,
        DamageBonus = 9,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        BaseLevel = 4,
        MinDepth = 6,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            ..Common,
            new Equip(MundaneArmory.Whip),
            EquipSet.Roll(MundaneArmory.Bola, 80),
        ],
    };

    public static readonly MonsterDef OrcDoomsayer = new()
    {
        id = "orc_doomsayer",
        Name = "orc doomsayer",
        Family = "orc",
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('o', ConsoleColor.Magenta),
        HpPerLevel = 8,
        AC = 2,
        AttackBonus = 1,
        DamageBonus = 3,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        BaseLevel = 5,
        MinDepth = 8,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            ..Common,
            new Equip(MundaneArmory.Flail),
        ],
    };

    public static readonly MonsterDef OrcVeteranMaster = new()
    {
        id = "orc_veteran_master",
        Name = "orc veteran master",
        Family = "orc",
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('o', ConsoleColor.DarkMagenta),
        HpPerLevel = 10,
        AC = 2,
        AttackBonus = 2,
        DamageBonus = 13,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        BaseLevel = 10,
        MinDepth = 12,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            ..Common,
            new Equip(MundaneArmory.BoStaff),
            EquipSet.Roll(MundaneArmory.Longbow, 70),
        ],
    };
}
