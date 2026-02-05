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
        Glyph = new('o', ConsoleColor.DarkGreen),
        HP = 18,
        AC = 14,
        AttackBonus = 7,
        DamageBonus = 3,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        CR = 0,
        SpawnWeight = 100,
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
        Glyph = new('o', ConsoleColor.Green),
        HP = 23,
        AC = 18,
        AttackBonus = 7,
        DamageBonus = 4,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        CR = 1,
        SpawnWeight = 80,
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
        Glyph = new('o', ConsoleColor.Yellow),
        HP = 32,
        AC = 19,
        AttackBonus = 10,
        DamageBonus = 4,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        CR = 2,
        SpawnWeight = 60,
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
        Glyph = new('o', ConsoleColor.Red),
        HP = 75,
        AC = 19,
        AttackBonus = 14,
        DamageBonus = 9,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        CR = 4,
        SpawnWeight = 40,
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
        Glyph = new('o', ConsoleColor.DarkYellow),
        HP = 65,
        AC = 20,
        AttackBonus = 14,
        DamageBonus = 9,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        CR = 4,
        SpawnWeight = 30,
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
        Glyph = new('o', ConsoleColor.Magenta),
        HP = 78,
        AC = 21,
        AttackBonus = 13,
        DamageBonus = 3,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        CR = 5,
        SpawnWeight = 25,
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
        Glyph = new('o', ConsoleColor.White),
        HP = 175,
        AC = 30,
        AttackBonus = 24,
        DamageBonus = 13,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        CR = 10,
        SpawnWeight = 10,
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
