namespace Pathhack.Game.Bestiary;

/// <summary>Vetoes respawn_from_corpse when not adjacent to water.</summary>
public class FloodCondition : LogicBrick
{
    public static readonly FloodCondition Instance = new();
    public override string Id => "troll:flood";

    static bool NearWater(IUnit unit) =>
        unit.Pos.Neighbours().Any(p => lvl.InBounds(p) && lvl[p].Type == TileType.Water);

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "respawn_from_corpse" && !NearWater(fact.Entity as IUnit ?? u) ? false : null;
}

/// <summary>Rips off armor for a burst heal, permanently loses AC.</summary>
public class ShedArmorAction() : ActionBrick("Shed Armor", tags: AbilityTags.Beneficial)
{
    public static readonly ShedArmorAction Instance = new();

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        if (unit.HP.Current > unit.HP.Max * 4 / 10) return "healthy";
        if (unit.HasFact(ShedArmorDebuff.Instance)) return "already shed";
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        int missing = unit.HP.Max - unit.HP.Current;
        int heal = missing * 7 / 10;
        g.DoHeal(unit, unit, heal);
        unit.AddFact(ShedArmorDebuff.Instance);
        g.YouObserve(unit, $"{unit:The} roars in pain then rips off its armor, it looks haler... too hale, where are those arms coming from???", "a roar of pain and anger");
    }
}

public class ShedArmorDebuff : LogicBrick
{
    public static readonly ShedArmorDebuff Instance = new();
    public override string Id => "troll:shed_armor";
    public override bool IsBuff => true;
    public override string? BuffName => "Shed Armor";
    public override BuffPriority BuffPriority => BuffPriority.Moderate;

    protected override void OnBeforeDefendRoll(Fact fact, PHContext ctx) => ctx.Check!.Modifiers.Untyped(-5, "shed");
    protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx) => ctx.Check!.Modifiers.Untyped(1, "shedrage");
}

public static class Trolls
{
    // Common outfits
    public static readonly EquipSet TrollWeapons = EquipSet.Weighted(
        (3, null),
        (3, MundaneArmory.Club),
        (2, MundaneArmory.SpikedClub),
        (1, MundaneArmory.Greatclub),
        (1, MundaneArmory.Battleaxe)
    );

    public static readonly EquipSet TrollArmor = EquipSet.Weighted(
        (3, null),
        (4, MundaneArmory.LeatherArmor),
        (2, MundaneArmory.HideArmor),
        (1, MundaneArmory.Breastplate)
    );

    public static readonly EquipSet CasterWeapon = EquipSet.OneOf(MundaneArmory.Club, MundaneArmory.Quarterstaff);

    static MonsterDef Troll(string id, string name, int level,
        WeaponDef bite, WeaponDef claw, RegenBrick regen,
        LogicBrick[]? extra = null, UnitSize size = UnitSize.Large,
        ActionCost? speed = null, int ac = 0, int ab = 0, int dmg = 0,
        ConsoleColor color = ConsoleColor.Green)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = "troll",
            CreatureType = CreatureTypes.Humanoid,
            Subtypes = ["giant"],
            Glyph = new('T', color),
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
                .. extra ?? [],
                new GrantAction(AttackWithWeapon.Instance),
                new GrantAction(new FullAttack("troll", bite, claw, claw)),
            ],
        };
    }

    // === Unique ===

    public static readonly MonsterDef FloodTroll = Troll("flood_troll", "flood troll", 3,
        NaturalWeapons.Bite_1d3, NaturalWeapons.Claw_1d2, RegenBrick.FireOrAcid,
        // TODO: swimming (should not drown when respawning on water)
        extra: [FloodCondition.Instance, TrollWeapons, TrollArmor],
        color: ConsoleColor.Cyan);

    public static readonly MonsterDef IceTroll = Troll("ice_troll", "ice troll", 4,
        NaturalWeapons.Bite_1d3, NaturalWeapons.Claw_1d3, RegenBrick.FireOrAcid,
        extra: [
            EnergyResist.Cold.Immune,  // placeholder for immunity
            EquipSet.WithCount(MundaneArmory.Hatchet, d(2) + 1),
            TrollWeapons,
            TrollArmor,
        ], ac: 0,
        color: ConsoleColor.White);

    public static readonly MonsterDef MossTroll = Troll("moss_troll", "moss troll", 5,
        NaturalWeapons.Bite_1d4, NaturalWeapons.Claw_1d3, RegenBrick.Fire,
        extra: [TrollWeapons, TrollArmor],
        color: ConsoleColor.DarkGreen);

    public static readonly MonsterDef CavernTroll = Troll("cavern_troll", "cavern troll", 6,
        NaturalWeapons.Bite_1d6, NaturalWeapons.Claw_1d3, RegenBrick.Acid,
        extra: [EquipSet.WithCount(Gems.Rock, d(3) + 2), TrollWeapons, TrollArmor],
        color: ConsoleColor.DarkGray);

    public static readonly MonsterDef Warleader = Troll("troll_warleader", "troll warleader", 10,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d4, RegenBrick.FireOrAcid,
        // TODO: fear roar action (will save or frightened)
        extra: [new GrantAction(ShedArmorAction.Instance), new Equip(MundaneArmory.Greatclub), new Equip(MundaneArmory.HideArmor)],
        ac: 0, ab: 0, dmg: 0,
        color: ConsoleColor.Red);

    public static readonly MonsterDef JotundTroll = Troll("jotund_troll", "jotund troll", 15,
        NaturalWeapons.Bite_2d10, NaturalWeapons.Claw_1d8, RegenBrick.FireOrAcid,
        // TODO: multi-bite FullAttack with primaryCount param
        // TODO: swallow whole action
        // TODO: confusion roar action
        extra: [],
        size: UnitSize.Huge, ac: 0, ab: 0, dmg: 0,
        color: ConsoleColor.Magenta);

    // === Casters ===

    public static readonly MonsterDef TrollAcolyte = Troll("troll_acolyte", "troll acolyte", 6,
        NaturalWeapons.Bite_1d6, NaturalWeapons.Claw_1d4, RegenBrick.FireOrAcid,
        extra: [
            CasterWeapon,
            new GrantPool("spell_l1", 2, 20),
            new GrantSpell(BasicLevel1Spells.MagicMissile),
        ],
        color: ConsoleColor.DarkYellow);

    public static readonly MonsterDef TrollFury = Troll("troll_fury", "troll fury", 8,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d6, RegenBrick.FireOrAcid,
        extra: [
            CasterWeapon,
            new GrantPool("spell_l1", 2, 15),
            new GrantPool("spell_l2", 1, 25),
            new GrantSpell(BasicLevel1Spells.BurningHands),
            new GrantSpell(BasicLevel2Spells.AcidArrow),
        ], ac: 0, ab: 0,
        color: ConsoleColor.Yellow);

    public static readonly MonsterDef ElderMatron = Troll("troll_elder_matron", "troll elder matron", 10,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d6, RegenBrick.FireOrAcid,
        extra: [
            CasterWeapon,
            new GrantPool("spell_l1", 3, 12),
            new GrantPool("spell_l2", 2, 20),
            new GrantSpell(BasicLevel1Spells.CureLightWounds),
            new GrantSpell(BasicLevel1Spells.MagicMissile),
            new GrantSpell(BasicLevel1Spells.BurningHands),
            new GrantSpell(BasicLevel2Spells.AcidArrow),
        ], ac: 0, ab: 0, dmg: 0,
        color: ConsoleColor.DarkMagenta);

    // === Soldiers ===

    public static readonly MonsterDef SewerTroll = Troll("sewer_troll", "sewer troll", 3,
        NaturalWeapons.Bite_1d3, NaturalWeapons.Claw_1d2, RegenBrick.FireOrAcid,
        extra: [TrollWeapons, TrollArmor],
        color: ConsoleColor.DarkCyan);

    public static readonly MonsterDef TrollBrute = Troll("troll_brute", "troll brute", 7,
        NaturalWeapons.Bite_1d6, NaturalWeapons.Claw_1d3, RegenBrick.FireOrAcid,
        extra: [TrollWeapons, TrollArmor],
        ab: 0, dmg: 0,
        color: ConsoleColor.Green);

    public static readonly MonsterDef TrollBerserker = Troll("troll_berserker", "troll berserker", 9,
        NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d4, RegenBrick.FireOrAcid,
        extra: [TrollWeapons, TrollArmor],
        ab: 0, dmg: 0,
        color: ConsoleColor.DarkRed);

    public static readonly MonsterDef TrollRender = Troll("troll_render", "troll render", 12,
        NaturalWeapons.Bite_2d6, NaturalWeapons.Claw_1d8, RegenBrick.FireOrAcid,
        extra: [TrollWeapons, TrollArmor],
        ac: 0, ab: 0, dmg: 0,
        color: ConsoleColor.Gray);

    public static readonly MonsterDef MountainTroll = Troll("mountain_troll", "mountain troll", 14,
        NaturalWeapons.Bite_2d10, NaturalWeapons.Claw_1d8, RegenBrick.FireOrAcid,
        extra: [TrollWeapons, TrollArmor],
        size: UnitSize.Huge, ac: 0, ab: 0, dmg: 0,
        color: ConsoleColor.DarkYellow);

    public static readonly MonsterDef[] All = [
        FloodTroll, IceTroll, MossTroll, CavernTroll, Warleader, JotundTroll,
        TrollAcolyte, TrollFury, ElderMatron,
        SewerTroll, TrollBrute, TrollBerserker, TrollRender, MountainTroll,
    ];
}
