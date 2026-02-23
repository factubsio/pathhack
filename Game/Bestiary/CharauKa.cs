namespace Pathhack.Game.Bestiary;

public class ChargeLowAc : LogicBrick
{
    public override string Id => "charge_low_ac";
    public override StackMode StackMode => StackMode.ExtendDuration;
    public static readonly ChargeLowAc Instance = new();

    protected override void OnBeforeDefendRoll(Fact fact, PHContext context) => context.Check!.Modifiers.Untyped(-2, "charge");
}
public class AerialCharge() : CooldownAction("charau_ka:aerial_charge", TargetingType.Unit, _ => 4, 3)
{
    public static readonly AerialCharge Instance = new();
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;

        if (target.Unit is not {} tgt) return "no unit";
        if (!tgt.Pos.IsCompassFrom(unit.Pos)) return "not in line";

        var dist = tgt.Pos.ChebyshevDist(unit.Pos);
        if (dist > MaxRange) return "too far";
        if (dist == 1) return "too near";

        Pos[] candidates = [.. tgt.Pos.Neighbours().Where(x => lvl.InBounds(x) && lvl.UnitAt(x) == null && lvl.CanMoveTo(unit.Pos, x, unit))];
        if (candidates.Length == 0) return "no landing spot";
        return new(true, Plan: candidates.Pick());
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (plan is not Pos {} landingSpot) return;
        lvl.MoveUnit(unit, landingSpot, true);

        g.YouObserve(unit, $"{unit:The} charges {target.Unit!:the} through the air!", "a whoosh, a stomping crash");
        DoWeaponAttack(unit, target.Unit!, unit.GetWieldedItem(), attackBonus: 2);
        unit.AddFact(ChargeLowAc.Instance, 2);

        unit.Energy -= ActionCosts.OneAction.Value;
    }
}

public class ShriekingFrenzyBuff : LogicBrick
{
    public static readonly ShriekingFrenzyBuff Instance = new();
    public override string Id => "charau_ka:shrieking_frenzy";
    public override bool IsActive => true;
    public override bool IsBuff => true;
    public override string? BuffName => "Shrieking Frenzy";
    public override StackMode StackMode => StackMode.Reject;

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit {} unit) return;
        if (unit.CannotAct) return;
        unit.Energy += 3;
    }

    protected override void OnFactRemoved(Fact fact)
    {
        if (fact.Entity is IUnit unit && !unit.IsDead)
        {
            if (!unit.Has(CommonQueries.DazeImmune))
            {
                g.YouObserve(unit, $"{unit:The} staggers, exhausted.");
                g.Defer(() =>
                {
                    unit.AddFact(DazedBuff.Instance, 2);
                });
            }
        }
    }
}

public class ShriekingFrenzyPassive : LogicBrick
{
    public static readonly ShriekingFrenzyPassive Instance = new();
    public override string Id => "charau_ka:shrieking_frenzy_passive";
    public override bool IsActive => true;

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        if (unit.HasFact(ShriekingFrenzyBuff.Instance)) return;
        if (unit.HP.Current == unit.HP.Max) return;
        if (unit.CannotAct) return;

        unit.AddFact(ShriekingFrenzyBuff.Instance, 3);
        g.YouObserve(unit, $"{unit:The} begins shrieking wildly!", "a terrible shrieking");
    }
}

public static class CharauKa
{
    static readonly EquipSet BasicWeapons = EquipSet.Weighted(
        (2, null),
        (4, MundaneArmory.Club),
        (1, MundaneArmory.SpikedClub),
        (1, MundaneArmory.Greatclub)
    );

    static MonsterDef CK(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 5, int ac = 0, int ab = 0, int dmg = 0,
        GroupSize group = GroupSize.SmallMixed, int spawnWeight = 10,
        MonFlags flags = MonFlags.None, Func<MonsterDef>? growsInto = null,
        UnitSize size = UnitSize.Small, int minDepth = 3, int maxDepth = 10)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = "charau-ka",
            CreatureType = CreatureTypes.Humanoid,
            Subtypes = [],
            Glyph = new('Y', color),
            HpPerLevel = hp,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            Unarmed = NaturalWeapons.Bite_1d3,
            Size = size,
            BaseLevel = level,
            MinDepth = minDepth,
            MaxDepth = maxDepth,
            GroupSize = group,
            SpawnWeight = spawnWeight,
            MoralAxis = MoralAxis.Evil,
            EthicalAxis = EthicalAxis.Chaotic,
            BrainFlags = flags,
            Components = components,
            GrowsInto = growsInto,
        };
    }

    public static readonly MonsterDef Mook = CK("charau_ka", "charau-ka", 3, ConsoleColor.White,
        growsInto: () => Savage!,
        components: [
            EquipSet.Roll(Gems.Rock, 40, d(3) + 1),
            BasicWeapons,
            EquipSet.Roll(MundaneArmory.HideArmor, 50),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Warrior = CK("charau_ka_warrior", "charau-ka warrior", 4, ConsoleColor.Red,
        growsInto: () => Butcher!,
        components: [
            BasicWeapons,
            EquipSet.Roll(MundaneArmory.Spear, 50, d(3)),
            new Equip(MundaneArmory.HideArmor),
            ShriekingFrenzyPassive.Instance,
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Savage = CK("charau_ka_savage", "charau-ka savage", 5, ConsoleColor.Red,
        growsInto: () => Butcher!,
        components: [
            new Equip(MundaneArmory.Greatclub),
            new Equip(MundaneArmory.HideArmor),
            ShriekingFrenzyPassive.Instance,
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Tracker = CK("charau_ka_tracker", "charau-ka tracker", 5, ConsoleColor.Green,
        components: [
            EquipSet.Roll(MundaneArmory.Spear, 100, d(4)),
            new Equip(MundaneArmory.Club),
            EquipSet.Roll(MundaneArmory.HideArmor, 50),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Acolyte = CK("charau_ka_acolyte", "charau-ka acolyte", 6, ConsoleColor.DarkYellow,
        flags: MonFlags.PrefersCasting,
        components: [
            ..GrantPool.StandardLevel1Caster,
            new GrantSpell(BasicLevel1Spells.Grease),
            new GrantSpell(BasicLevel1Spells.MagicMissile),
            new Equip(MundaneArmory.Club),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Butcher = CK("charau_ka_butcher", "charau-ka butcher", 7, ConsoleColor.Blue,
        components: [
            new Equip(MundaneArmory.Greatclub),
            new Equip(MundaneArmory.HideArmor),
            ShriekingFrenzyPassive.Instance,
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef HighPriest = CK("charau_ka_high_priest", "charau-ka high priest", 8, ConsoleColor.Magenta,
        flags: MonFlags.PrefersCasting,
        components: [
            new Equip(MundaneArmory.Club),
            new Equip(MundaneArmory.HideArmor),
            new GrantAction(AttackWithWeapon.Instance),
            // TODO: spells, channel negative energy, madness aura
        ]);

    public static readonly MonsterDef Derhii = CK("derhii", "derhii", 10, ConsoleColor.Blue,
        size: UnitSize.Large,
        group: GroupSize.Small,
        components: [
            new Equip(MundaneArmory.Falchion),
            new QueryBrick(CreatureTags.Flying, true),
            new GrantAction(AerialCharge.Instance),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef[] All = [
        Mook, Warrior, Savage, Tracker, Acolyte, Butcher, Derhii,
    ];
}
