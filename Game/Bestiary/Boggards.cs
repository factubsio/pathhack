namespace Pathhack.Game.Bestiary;

public class TonguePull(int cd) : CooldownAction("Tongue Pull", TargetingType.None, _ => cd, tags: AbilityTags.Harmful)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var basePlan = base.CanExecute(unit, data, target);
        if (!basePlan) return basePlan;
        if (target.Unit == null) return false;

        if (!unit.Pos.IsCompassFrom(target.Unit.Pos)) return "not compass";
        var dist = unit.Pos.ChebyshevDist(target.Unit.Pos);
        if (dist <= 1 || dist > 3) return "too close/far";

        var dir = (target.Unit.Pos - unit.Pos).Signed;
        for (int i = 1; i < dist; i++)
        {
            if (lvl.UnitAt(unit.Pos + dir * i) != null) return "blocked";
        }

        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit == null) return;

        int dc = unit.GetSpellDC();
        using var ctx = PHContext.Create(unit, target);
        if (!CheckReflex(ctx, dc, "tongue pull"))
        {
            g.YouObserve(unit, $"{unit:The} grabs {target.Unit:the} with its sticky tongue!", "a slurping sound");
            Pos adj = unit.Pos + (target.Unit!.Pos - unit.Pos).Signed;
            lvl.MoveUnit(target.Unit, adj, true);
        }
    }
}

public class Croak(int cd) : CooldownAction("Croak", TargetingType.None, _ => cd, tags: AbilityTags.Harmful)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var basePlan = base.CanExecute(unit, data, target);
        if (!basePlan) return basePlan;
        foreach (var dir in Pos.AllDirs)
        {
            var tgt = lvl.UnitAt(unit.Pos + dir);
            if (tgt != null && !tgt.IsDead && !tgt.Has("confused"))
                return true;
        }
        return "no valid targets";
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        int dc = unit.GetSpellDC();
        g.YouObserve(unit, $"{unit:The} croaks loudly, and confusingly!", "a loud croak");
        foreach (var dir in Pos.AllDirs)
        {
            var tgt = lvl.UnitAt(unit.Pos + dir);
            if (tgt.IsNullOrDead() || tgt == unit) continue;

            using var ctx = PHContext.Create(unit, Target.From(tgt));
            if (!CheckWill(ctx, dc, "croak"))
                tgt.AddFact(ConfusedBuff.Instance, duration: 3);
        }
    }
}

public static class Boggards
{
    static readonly EquipSet LightWeapon = EquipSet.OneOf(MundaneArmory.Club, MundaneArmory.Spear);
    static readonly EquipSet MediumWeapon = EquipSet.OneOf(MundaneArmory.Spear, MundaneArmory.Mace, MundaneArmory.Flail);
    static readonly EquipSet HeavyWeapon = EquipSet.OneOf(MundaneArmory.Flail, MundaneArmory.Battleaxe, MundaneArmory.Greatclub);
    static readonly EquipSet LightArmor = EquipSet.Roll(MundaneArmory.LeatherArmor, 50);
    static readonly EquipSet MediumArmor = EquipSet.OneOf(MundaneArmory.LeatherArmor, MundaneArmory.HideArmor);

    static MonsterDef B(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 8, int ac = 0, int ab = 0, int dmg = 0,
        GroupSize group = GroupSize.SmallMixed, int spawnWeight = 10,
        MonFlags flags = MonFlags.None, WeaponDef? unarmed = null,
        ActionCost? speed = null)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = "boggard",
            CreatureType = CreatureTypes.Humanoid,
            Subtypes = [CreatureSubtypes.Amphibious],
            Glyph = new('@', color),
            HpPerLevel = hp,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            Unarmed = unarmed ?? NaturalWeapons.Bite_1d3,
            Size = UnitSize.Medium,
            BaseLevel = level,
            MinDepth = level,
            MaxDepth = 12,
            GroupSize = group,
            SpawnWeight = spawnWeight,
            MoralAxis = MoralAxis.Evil,
            EthicalAxis = EthicalAxis.Chaotic,
            BrainFlags = flags,
            LandMove = speed ?? ActionCosts.LandMove25,
            Components = components,
        };
    }

    // --- Mundane ---

    public static readonly MonsterDef Boggard = B("boggard", "boggard", 4, ConsoleColor.DarkGreen,
        components: [
            LightWeapon,
            LightArmor,
            new EquipSet(
                new Outfit(3, new OutfitItem(MundaneArmory.Dart, Count: d(2) + 2)),
                new Outfit(3, new OutfitItem(MundaneArmory.Spear)),
                new Outfit(4)
            ),
            new GrantAction(new TonguePull(30)),
            new GrantAction(new Croak(40)),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Hunter = B("boggard_hunter", "boggard hunter", 5, ConsoleColor.Gray,
        speed: 10,
        components: [
            new Equip(MundaneArmory.Blowgun),
            new Equip(MundaneQuivers.BlowgunDarts),
            LightWeapon,
            LightArmor,
            new GrantAction(new Croak(36)),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Warrior = B("boggard_warrior", "boggard warrior", 6, ConsoleColor.DarkGreen,
        components: [
            MediumWeapon,
            MediumArmor,
            new GrantAction(new TonguePull(24)),
            new GrantAction(new Croak(32)),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Stalker = B("boggard_stalker", "boggard stalker", 7, ConsoleColor.DarkGray,
        speed: 10,
        components: [
            LightWeapon,
            LightArmor,
            new GrantAction(new Croak(32)),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    // --- Casters ---

    public static readonly MonsterDef Mudcroaker = B("boggard_mudcroaker", "boggard mudcroaker", 7, ConsoleColor.DarkCyan,
        flags: MonFlags.PrefersCasting,
        components: [
            new Equip(MundaneArmory.Club),
            new GrantPool("spell_l1", 2, 20),
            new GrantSpell(BasicLevel1Spells.Grease),
            new GrantSpell(BasicLevel1Spells.FalseLifeLesser),
            new GrantAction(new TonguePull(22)),
            new GrantAction(new Croak(28)),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Swampseer = B("boggard_swampseer", "boggard swampseer", 9, ConsoleColor.Cyan,
        flags: MonFlags.PrefersCasting,
        components: [
            new Equip(MundaneArmory.Quarterstaff),
            new GrantPool("spell_l1", 1, 15),
            new GrantPool("spell_l2", 1, 20),
            new GrantSpell(BasicLevel1Spells.BurningHands),
            new GrantSpell(BasicLevel1Spells.FalseLifeLesser),
            new GrantSpell(BasicLevel2Spells.AcidArrow),
            new GrantAction(new TonguePull(20)),
            new GrantAction(new Croak(24)),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    // --- Leaders ---

    public static readonly MonsterDef Champion = B("boggard_champion", "boggard champion", 9, ConsoleColor.Blue,
        spawnWeight: 5,
        components: [
            HeavyWeapon,
            MediumArmor,
            new GrantAction(new TonguePull(18)),
            new GrantAction(new Croak(24)),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Sovereign = B("boggard_sovereign", "boggard sovereign", 11, ConsoleColor.Magenta,
        spawnWeight: 3, unarmed: NaturalWeapons.Bite_1d6,
        components: [
            HeavyWeapon,
            MediumArmor,
            new GrantAction(new TonguePull(16)),
            new GrantAction(new Croak(20)),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef[] All = [
        Boggard, Hunter, Warrior, Stalker,
        Mudcroaker, Swampseer,
        Champion, Sovereign,
    ];
}
