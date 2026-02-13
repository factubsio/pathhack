namespace Pathhack.Game.Bestiary;

public static class Trolls
{
    static MonsterDef Troll(string id, string name, int level,
        WeaponDef bite, WeaponDef claw, RegenBrick regen,
        LogicBrick[]? extra = null, UnitSize size = UnitSize.Large,
        ActionCost? speed = null, int ac = 0, int ab = 0, int dmg = 0)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = "troll",
            CreatureType = CreatureTypes.Humanoid,
            Subtypes = ["giant"],
            Glyph = new('T', ConsoleColor.Green),
            HpPerLevel = 8,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            Size = size,
            BaseLevel = level,
            MinDepth = level,
            Unarmed = bite,
            LandMove = speed ?? ActionCosts.LandMove25,
            MoralAxis = MoralAxis.Evil,
            EthicalAxis = EthicalAxis.Chaotic,
            Components = [
                regen,
                new GrantAction(AttackWithWeapon.Instance),
                new GrantAction(new FullAttack("troll", bite, claw, claw)),
                .. extra ?? [],
            ],
        };
    }

    // === Unique ===

    static readonly MonsterDef FloodTroll = Troll("flood_troll", "flood troll", 3,
        NaturalWeapons.Bite_1d6, NaturalWeapons.Claw_1d4, RegenBrick.FireOrAcid,
        // TODO: FloodRegenBrick — regen only when adjacent to water tile
        // replace RegenBrick.FireOrAcid with it
        extra: []);

    static readonly MonsterDef IceTroll = Troll("ice_troll", "ice troll", 4,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d4, RegenBrick.FireOrAcid,
        // TODO: cold immunity brick (negate cold damage OnBeforeDamageIncomingRoll)
        extra: [EnergyResist.Cold(99)],  // placeholder for immunity
        ac: 1);

    static readonly MonsterDef MossTroll = Troll("moss_troll", "moss troll", 5,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d6, RegenBrick.Fire);

    static readonly MonsterDef CavernTroll = Troll("cavern_troll", "cavern troll", 6,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d6, RegenBrick.Acid,
        // TODO: rock throw action
        extra: []);

    static readonly MonsterDef Warleader = Troll("troll_warleader", "troll warleader", 10,
        NaturalWeapons.Bite_2d6, NaturalWeapons.Claw_1d8, RegenBrick.FireOrAcid,
        // TODO: shed armor action (apply -6 AC buff, heal 70% missing HP)
        // TODO: fear roar action (will save or frightened)
        extra: [new Equip(MundaneArmory.HideArmor), new Equip(MundaneArmory.Greatclub)],
        ac: 2, ab: 2, dmg: 2);

    static readonly MonsterDef JotundTroll = Troll("jotund_troll", "jotund troll", 15,
        NaturalWeapons.Bite_2d8, NaturalWeapons.Claw_1d8, RegenBrick.FireOrAcid,
        // TODO: multi-bite FullAttack with primaryCount param
        // TODO: swallow whole action
        // TODO: confusion roar action
        extra: [],
        size: UnitSize.Huge, ac: 3, ab: 3, dmg: 4);

    // === Casters ===

    static readonly MonsterDef TrollAcolyte = Troll("troll_acolyte", "troll acolyte", 6,
        NaturalWeapons.Bite_1d6, NaturalWeapons.Claw_1d4, RegenBrick.FireOrAcid,
        extra: [
            new GrantPool("spell_l1", 2, 20),
            new GrantSpell(BasicLevel1Spells.CureLightWounds),
        ]);

    static readonly MonsterDef TrollFury = Troll("troll_fury", "troll fury", 8,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d6, RegenBrick.FireOrAcid,
        extra: [
            new GrantPool("spell_l1", 2, 15),
            new GrantPool("spell_l2", 1, 25),
            new GrantSpell(BasicLevel1Spells.BurningHands),
            new GrantSpell(BasicLevel1Spells.AcidArrow),
        ], ac: 1, ab: 1);

    static readonly MonsterDef ElderMatron = Troll("troll_elder_matron", "troll elder matron", 10,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d6, RegenBrick.FireOrAcid,
        extra: [
            new GrantPool("spell_l1", 3, 12),
            new GrantPool("spell_l2", 2, 20),
            new GrantSpell(BasicLevel1Spells.CureLightWounds),
            new GrantSpell(BasicLevel1Spells.BurningHands),
            new GrantSpell(BasicLevel1Spells.AcidArrow),
        ], ac: 1, ab: 1, dmg: 1);

    // === Soldiers ===

    static readonly MonsterDef SewerTroll = Troll("sewer_troll", "sewer troll", 3,
        NaturalWeapons.Bite_1d6, NaturalWeapons.Claw_1d4, RegenBrick.FireOrAcid,
        extra: [EquipSet.OneOf(MundaneArmory.Club)]);

    static readonly MonsterDef TrollBrute = Troll("troll_brute", "troll brute", 7,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d6, RegenBrick.FireOrAcid,
        extra: [new Equip(MundaneArmory.SpikedClub), new Equip(MundaneArmory.HideArmor)],
        ab: 1, dmg: 1);

    static readonly MonsterDef TrollBerserker = Troll("troll_berserker", "troll berserker", 9,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d6, RegenBrick.FireOrAcid,
        extra: [new Equip(MundaneArmory.Greatclub), new Equip(MundaneArmory.HideArmor)],
        ab: 2, dmg: 2);

    static readonly MonsterDef TrollRender = Troll("troll_render", "troll render", 12,
        NaturalWeapons.Bite_2d6, NaturalWeapons.Claw_1d8, RegenBrick.FireOrAcid,
        extra: [new Equip(MundaneArmory.Greatclub), new Equip(MundaneArmory.Breastplate)],
        ac: 2, ab: 2, dmg: 3);

    static readonly MonsterDef MountainTroll = Troll("mountain_troll", "mountain troll", 14,
        NaturalWeapons.Bite_2d8, NaturalWeapons.Claw_1d8, RegenBrick.FireOrAcid,
        extra: [new Equip(MundaneArmory.Greatclub), new Equip(MundaneArmory.Breastplate)],
        size: UnitSize.Huge, ac: 3, ab: 3, dmg: 4);

    public static readonly MonsterDef[] All = [
        FloodTroll, IceTroll, MossTroll, CavernTroll, Warleader, JotundTroll,
        TrollAcolyte, TrollFury, ElderMatron,
        SewerTroll, TrollBrute, TrollBerserker, TrollRender, MountainTroll,
    ];
}
