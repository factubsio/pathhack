namespace Pathhack.Game.Bestiary;

public class SeedpodBurst(int range, int cd)
    : CooldownAction("Seedpod", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    static readonly WeaponDef Seedpod = new()
    {
        id = "seedpod",
        Name = "seedpod",
        BaseDamage = d(4),
        Style = WeaponStyle.Exotic,
        Grip = WeaponGrip.Exotic,
        DamageType = DamageTypes.Blunt,
        Glyph = new('♭', ConsoleColor.Green),
        Launcher = "hand",
        Weight = -1,
        Price = -1,
    };

    public override string? PokedexDescription => "Seedpod that silences in a burst";

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        var item = Item.Create(Seedpod);
        g.YouObserve(unit, $"{unit:The} hurls a seedpod!", "a wet pop");
        Pos landing = DoThrow(unit, item, dir, AttackType.Thrown);

        int dc = unit.GetSpellDC();
        var dm = DungeonMaster.As(unit).At(landing);
        BreathAttack.CollectBreath(BreathShape.Burst, dm, Pos.Zero, 1, ConsoleColor.Green, "spores", victim =>
        {
            if (victim == unit) return;
            using var ctx = PHContext.Create(unit, Target.From(victim));
            if (!CheckFort(ctx, dc, "seedpod spores"))
                victim.AddFact(SilencedBuff.Instance.Timed(), unit, 3);
        }, glyphChar: '⛬');
    }

    public static readonly SeedpodBurst Instance = new(6, 6);
}

public class SeedSprayCone()
    : CooldownAction("Seed Spray", TargetingType.Direction, _ => 8, maxRange: 3, tags: AbilityTags.Harmful)
{
    public static readonly SeedSprayCone Instance = new();

    public override string? PokedexDescription => "Cone of piercing seeds";

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        if (unit is not Monster m || !m.CanSeeYou) return "can't see target";
        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();
        Dice damage = d(2, 4);

        BreathAttack.CollectBreath(BreathShape.Cone, unit, dir, MaxRange, ConsoleColor.Yellow, "seeds", victim =>
        {
            if (victim == unit) return;
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "seed spray");
            ctx.Damage.Add(new DamageRoll { Formula = damage, Type = DamageTypes.Piercing, HalfOnSave = true });
            DoDamage(ctx);
        }, verb: "spray");
    }
}

public class WaterJetBeam(int range, int cd)
    : CooldownAction("Water Jet", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    public override string? PokedexDescription => "Beam that blinds";

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC() + 2;

        g.YouObserve(unit, $"{unit:The} shoots a jet of water!", "a rushing splash");
        Beam.Cast(unit.Pos, dir, "water jet", new('~', ConsoleColor.Cyan), MaxRange, BeamFlags.None, victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "water jet");
            ctx.Damage.Add(new DamageRoll { Formula = d(3, 6), Type = DamageTypes.Blunt, HalfOnSave = true });
            DoDamage(ctx);
            if (!ctx.Check!.Result)
                victim.AddFact(BlindBuff.Instance.Timed(), unit, 2);
            return BeamHit.Continue;
        });
    }

    public static readonly WaterJetBeam Instance = new(5, 6);
}

public class FilamentLash(int cd)
    : CooldownAction("Filament", TargetingType.Unit, _ => cd, maxRange: 2, tags: AbilityTags.Harmful)
{
    public override string? PokedexDescription => "Reach 2 lash that degrades equipment";

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit is not { } victim) return;
        g.YouObserve(unit, $"{unit:The} lashes {victim:the} with a filament!", "a wet snap");

        using var ctx = PHContext.Create(unit, Target.From(victim));
        ctx.AttackType = AttackType.Melee;
        ctx.Weapon = Item.Create(NaturalWeapons.Tendril_1d4);
        if (!DoAttackRoll(ctx)) return;

        ctx.Damage.Add(new DamageRoll { Formula = d(4), Type = DamageTypes.Blunt });
        DoDamage(ctx);

        // degrade a random equipped item
        int roll = g.Rn2(3);
        Item? equip = roll switch
        {
            0 => victim.Equipped.GetValueOrDefault(ItemSlots.MainHandSlot),
            1 => victim.Equipped.GetValueOrDefault(ItemSlots.OffHandSlot),
            _ => victim.Equipped.GetValueOrDefault(ItemSlots.BodySlot),
        };
        if (equip is not { Def: WeaponDef or ArmorDef }) return;
        var result = equip.TryDegrade();
        Item.PrintDegrade(victim, equip, result);
    }

    public static readonly FilamentLash Instance = new(4);
}

public class NeedleShot(int range, int cd)
    : CooldownAction("Needle", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    static readonly WeaponDef Needle = new()
    {
        id = "cactus_needle",
        Name = "cactus needle",
        BaseDamage = d(6),
        Style = WeaponStyle.Exotic,
        Grip = WeaponGrip.Exotic,
        DamageType = DamageTypes.Piercing,
        Glyph = new('/', ConsoleColor.Green),
        Launcher = "hand",
        Weight = -1,
        Price = -1,
        Components = [BleedOnHit.S1],
    };

    public override string? PokedexDescription => "Fires a bleeding needle";

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        var item = Item.Create(Needle);
        g.YouObserve(unit, $"{unit:The} fires a needle!", "a sharp hiss");
        DoThrow(unit, item, dir, AttackType.Thrown);
    }

    public static readonly NeedleShot Instance = new(6, 4);
}

public class SnapdragonInspire : LogicBrick
{
    public static readonly SnapdragonInspire Instance = new();
    public override string Id => "leshy:inspire";
    public override bool IsActive => true;
    public override string? PokedexDescription => "Inspires nearby plants (attack advantage)";

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not Monster unit || unit.CannotAct) return;

        foreach (var mon in lvl.LiveUnits.OfType<Monster>())
        {
            if (mon == unit || mon.IsDead) continue;
            if (!mon.IsCreature(CreatureTypes.Plant)) continue;
            if (!unit.Pos.InRange(mon.Pos, 3)) continue;
            mon.AddFact(InspireBuff.Instance, unit, 2);
        }
    }

    class InspireBuff : LogicBrick
    {
        public static readonly InspireBuff Instance = new();
        public override string Id => "leshy:inspire_buff";
        public override StackMode StackMode => StackMode.ExtendDuration;
        public override bool IsBuff => true;
        public override string? BuffName => Id;

        protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx) => ctx.Check!.Advantage++;
    }
}

public class BloomLasso(int range, int cd)
    : CooldownAction("Bloom Lasso", TargetingType.Unit, _ => cd, maxRange: range, tags: AbilityTags.Harmful | AbilityTags.Mental)
{
    public override string? PokedexDescription => "Ranged confusion";

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit is not { } victim) return;
        g.YouObserve(unit, $"{unit:The} lashes {victim:the} with a bloom lasso!", "a rustling snap");
        if (victim.Has(CommonQueries.ConfusionImmune)) return;
        int dc = unit.GetSpellDC();
        using var ctx = PHContext.Create(unit, Target.From(victim));
        if (!CheckWill(ctx, dc, "bloom lasso"))
            victim.AddFact(ConfusedBuff.Instance, unit, (d(6) + 4).Roll());
    }

    public static readonly BloomLasso Instance = new(5, 6);
}

public static class Leshys
{
    public static readonly MonsterFamily Family = new("leshy");

    static MonsterDef L(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 8, int ac = 0, int ab = 0,
        ActionCost? speed = null, WeaponDef? unarmed = null,
        int spawnWeight = 10, GroupSize group = GroupSize.SmallMixed) => new()
    {
        id = $"leshy_{id}",
        Name = name,
        Family = Family,
        CreatureType = CreatureTypes.Plant,
        Glyph = new('{', color),
        HpPerLevel = hp,
        AC = ac,
        AttackBonus = ab,
        Unarmed = unarmed ?? NaturalWeapons.Slam_1d4,
        LandMove = speed ?? ActionCosts.LandMove15,
        Size = UnitSize.Small,
        BaseLevel = level,
        SpawnWeight = spawnWeight,
        GroupSize = group,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        Components = [..components, new GrantAction(AttackWithWeapon.Instance)],
    };

    // --- Level 2: mooks ---

    public static readonly MonsterDef Leaf = L("leaf", "leaf leshy", 2, ConsoleColor.White,
    [
        new GrantAction(SeedpodBurst.Instance),
    ]);

    public static readonly MonsterDef Sunflower = L("sunflower", "sunflower leshy", 2, ConsoleColor.Yellow,
    [
        new GrantAction(SeedSprayCone.Instance),
    ]);

    // --- Level 3 ---

    public static readonly MonsterDef Gourd = L("gourd", "gourd leshy", 3, ConsoleColor.Green,
    [
        new ApplyFactOnAttackHit(ProneBuff.Instance.Timed(), 1),
    ], unarmed: NaturalWeapons.Slam_1d6);

    public static readonly MonsterDef Cactus = L("cactus", "cactus leshy", 3, ConsoleColor.Red,
    [
        Thorns.Piercing_1d4,
        new GrantAction(NeedleShot.Instance),
    ], unarmed: NaturalWeapons.Slam_1d6);

    // --- Level 4 ---

    public static readonly MonsterDef Seaweed = L("seaweed", "seaweed leshy", 4, ConsoleColor.DarkCyan,
    [
        new GrantAction(WaterJetBeam.Instance),
    ], unarmed: NaturalWeapons.Slam_2d6);

    // --- Level 5 ---

    public static readonly MonsterDef Lichen = L("lichen", "lichen leshy", 5, ConsoleColor.Gray,
    [
        CorrosionOnBeingHit.Chance10,
        new GrantAction(FilamentLash.Instance),
    ], unarmed: NaturalWeapons.Slam_2d6);

    // --- Level 6 ---

    public static readonly MonsterDef Flytrap = L("flytrap", "flytrap leshy", 6, ConsoleColor.DarkGreen,
    [
        GrabOnHit.Instance,
        Constrict.SmallAcid,
        // TODO: engulf
    ], unarmed: NaturalWeapons.Bite_2d6);

    public static readonly MonsterDef Snapdragon = L("snapdragon", "snapdragon leshy", 6, ConsoleColor.DarkYellow,
    [
        SnapdragonInspire.Instance,
        new GrantAction(BloomLasso.Instance),
    ], unarmed: NaturalWeapons.Slam_2d6);

    // --- Level 8: boss ---

    public static readonly MonsterDef Lotus = L("lotus", "lotus leshy", 8, ConsoleColor.Magenta,
    [
        DazeAura.Instance,
        new GrantAction(new FullAttack("lotus", NaturalWeapons.Slam_3d6, NaturalWeapons.Slam_2d6, NaturalWeapons.Slam_2d6)),
    ], unarmed: NaturalWeapons.Slam_3d6, spawnWeight: 5);

    public static readonly MonsterDef[] All =
    [
        Leaf, Sunflower,
        Gourd, Cactus,
        Seaweed,
        Lichen,
        Flytrap, Snapdragon,
        Lotus,
    ];
}
