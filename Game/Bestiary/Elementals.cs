namespace Pathhack.Game.Bestiary;

/// <summary>Immune to bleed, paralyzed, poison, sleep.</summary>
public class ElementalTraits : LogicBrick
{
    public static readonly ElementalTraits Instance = new();
    public override string Id => "elemental_traits";
    public override string? PokedexDescription => "Immune to bleed, paralyzed, poison, sleep";

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        CommonQueries.Poison => true,
        CommonQueries.Bleed => true,
        CommonQueries.Sleep => true,
        CommonQueries.Paralysis => true,
        _ => null
    };
}

/// <summary>Grants flying movement mode.</summary>
public class ElementalFlight : LogicBrick
{
    public static readonly ElementalFlight Instance = new();
    public override string Id => "elemental_flight";
    public override string? PokedexDescription => "Flying";

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.TrueWhen(CreatureTags.Flying);
}

/// <summary>Tremorsense at range 6.</summary>
public class ElementalTremorsense : LogicBrick
{
    public static readonly ElementalTremorsense Instance = new();
    public override string Id => "elemental_tremorsense";
    public override string? PokedexDescription => "Tremorsense 6";

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.IntWhen("tremorsense", 6);
}

/// <summary>Persistent fire damage per round (like AcidBurnBuff).</summary>
public class FireBurnBuff : LogicBrick
{
    public static readonly FireBurnBuff Instance = new();
    public override string Id => "elemental:fire_burn";
    public override bool IsActive => true;
    public override bool IsBuff => true;
    public override string? BuffName => "Burning";
    public override StackMode StackMode => StackMode.ExtendDuration;

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        using var ctx = PHContext.Create(DungeonMaster.Mook, Target.From(unit));
        ctx.Damage.Add(new DamageRoll { Formula = d(4), Type = DamageTypes.Fire });
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "burn")}!");
        DoDamage(ctx);
    }
}

/// <summary>Speed debuff from elemental cold.</summary>
public class NumbingColdDebuff : LogicBrick
{
    public static readonly NumbingColdDebuff Instance = new();
    public override string Id => "elemental:numbing_cold";
    public override bool IsBuff => true;
    public override string? BuffName => "Numbing cold";
    public override StackMode StackMode => StackMode.ExtendDuration;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.NumWhen("speed_mult", 0.7);
}

/// <summary>Bonus to attack vs units wearing metal armor.</summary>
public class MetalMastery : LogicBrick
{
    public static readonly MetalMastery Instance = new();
    public override string Id => "elemental:metal_mastery";
    public override string? PokedexDescription => "Metal mastery (+2 AB vs metal armor)";

    protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
        var target = ctx.Target.Unit;
        if (target == null) return;
        var armor = target.Equipped.Values.FirstOrDefault(i => i.Material is "metal" or "steel" or "iron");
        if (armor != null)
            ctx.Check!.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, 2, "metal mastery"));
    }
}

/// <summary>Chance to spawn fire tile at previous position when moving.</summary>
public class MagmaTrail : LogicBrick
{
    public static readonly MagmaTrail Instance = new();
    public override string Id => "elemental:magma_trail";
    public override string? PokedexDescription => "Leaves fire in its wake";
    public override bool IsActive => true;

    protected override void OnRoundEnd(Fact fact)
    {
        if (fact.Entity is not Monster m) return;
        if (m.PrevPos == Pos.Invalid || m.PrevPos == m.Pos) return;
        if (g.Rn2(5) != 0) return;
        var area = new FirePatchArea(m, 8) { Tiles = [m.PrevPos] };
        lvl.CreateArea(area);
    }
}


/// <summary>Fire area that damages units standing in it.</summary>
public class FirePatchArea(IUnit? source, int duration) : Area(duration)
{
    public override string Name => "fire";
    public override Glyph Glyph => new('≈', ConsoleColor.Red);

    protected override void OnEnter(IUnit unit) => Burn(unit);
    protected override void OnMove(IUnit unit) => Burn(unit);

    protected override void OnTick()
    {
        foreach (var unit in Occupants) Burn(unit);
    }

    void Burn(IUnit unit)
    {
        if (unit == source || unit.HasFact(EnergyResist.Fire.Immune)) return;
        using var ctx = PHContext.Create(source, Target.From(unit));
        ctx.Damage.Add(new DamageRoll { Formula = d(4), Type = DamageTypes.Fire });
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "burn")} in the flames!");
        DoDamage(ctx);
    }
}

/// <summary>Mud area that heavily slows movement.</summary>
public class MudSlowArea(IUnit? source, int dc, int duration) : Area(duration)
{
    public override string Name => "mud";
    public override Glyph Glyph => new('~', ConsoleColor.DarkYellow);
    public override bool IsDifficultTerrain => true;

    protected override void OnEnter(IUnit unit) => TrySlow(unit);
    protected override void OnMove(IUnit unit) => TrySlow(unit);

    void TrySlow(IUnit unit)
    {
        if (unit.IsCreature(subtype: "earth") || unit.IsCreature(subtype: "mud")) return;
        using var ctx = PHContext.Create(source, Target.From(unit));
        if (!CheckReflex(ctx, dc, "mud"))
        {
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "get")} mired in mud!");
            unit.Energy -= unit.LandMove.Value;
        }
    }
}

// --- Active abilities ---

/// <summary>AoE damage to units within range. Low energy cost.</summary>
public class Whirlwind(int range) : LogicBrick
{
    public static readonly Whirlwind Range1 = new(1);
    public static readonly Whirlwind Range2 = new(2);
    public static readonly Whirlwind Range3 = new(3);

    public override string Id => $"elemental:whirlwind/{range}";
    public override bool IsActive => true;
    public override string? PokedexDescription => $"Whirlwind (range {range})";

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        using var area = lvl.CollectCircle(unit.Pos, range);
        foreach (var pos in area)
        {
            var victim = lvl.UnitAt(pos);
            if (victim == null || victim == unit) continue;

            using var ctx = PHContext.Create(unit, Target.From(victim));
            ctx.Damage.Add(new DamageRoll { Formula = d(4), Type = DamageTypes.Blunt });
            g.YouObserve(victim, $"{victim:The} {VTense(victim, "get")} battered by the whirlwind!");
            DoDamage(ctx);
        }
    }
}

/// <summary>Bull rush: shove target (and self) in a direction.</summary>
public class BullRush(int distance) : CooldownAction("bull rush", TargetingType.Unit, _ => 8, 1)
{
    public static readonly BullRush Push1 = new(1);
    public static readonly BullRush Push2 = new(2);
    public static readonly BullRush Push3 = new(3);

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        if (target.Unit == null) return "no target";
        if (!unit.IsAdjacent(target)) return "not adjacent";
        return true;
    }

    protected override void Execute(IUnit unit, Target target)
    {
        var tgt = target.Unit!;
        Pos dir = (tgt.Pos - unit.Pos).Signed;

        g.YouObserve(unit, $"{unit:The} {VTense(unit, "rush")} {tgt:the}!");

        int pushed = 0;
        for (int i = 0; i < distance; i++)
        {
            Pos nextTgt = tgt.Pos + dir;
            if (!lvl[nextTgt].IsPassable)
            {
                using var ctx = PHContext.Create(unit, Target.From(tgt));
                ctx.Damage.Add(new DamageRoll { Formula = d(6), Type = DamageTypes.Blunt });
                g.YouObserve(tgt, $"{tgt:The} {VTense(tgt, "slam")} into solid rock!");
                DoDamage(ctx);
                break;
            }
            lvl.MoveUnit(tgt, nextTgt, true);
            lvl.MoveUnit(unit, unit.Pos + dir, true);
            pushed++;
        }
    }
}

/// <summary>Water cone: blunt + cold damage in a cone.</summary>
public class WaterCone() : CooldownAction("water blast", TargetingType.Direction, _ => 10, maxRange: 3)
{
    public static readonly WaterCone Instance = new();

    protected override void Execute(IUnit unit, Target target)
    {
        Pos dir = target.Pos!.Value;
        using var cone = lvl.CollectCone(unit.Pos, dir, 3);
        Draw.AnimateFlash(cone, new Glyph('≈', ConsoleColor.Blue));
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "unleash")} a wave of water!", "a rushing torrent");

        int dc = unit.GetSpellDC();
        foreach (var pos in cone)
        {
            var victim = lvl.UnitAt(pos);
            if (victim == null || victim == unit) continue;

            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "water");
            ctx.Damage.Add(new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Blunt, HalfOnSave = true });
            ctx.Damage.Add(new DamageRoll { Formula = d(6), Type = DamageTypes.Cold, HalfOnSave = true });
            DoDamage(ctx);
        }
    }
}

/// <summary>AoE fort save or lose energy (skip actions).</summary>
public class NumbingCold() : CooldownAction("numbing cold", TargetingType.None, _ => 10)
{
    public static readonly NumbingCold Instance = new();

    protected override void Execute(IUnit unit, Target target)
    {
        g.YouObserve(unit, $"A wave of numbing cold radiates from {unit:the}!", "a bone-chilling cold");
        int dc = unit.GetSpellDC();

        foreach (var pos in unit.Pos.Neighbours())
        {
            var victim = lvl.UnitAt(pos);
            if (victim == null || victim == unit) continue;

            using var ctx = PHContext.Create(unit, Target.From(victim));
            if (!CheckFort(ctx, dc, "cold"))
            {
                g.YouObserve(victim, $"{victim:The} {VTense(victim, "seize")} up from the cold!");
                victim.Energy -= ActionCosts.OneAction.Value;
            }
        }
    }
}

/// <summary>Ranged shock beam.</summary>
public class SparkZap() : CooldownAction("spark zap", TargetingType.Direction, _ => 8, maxRange: 6)
{
    public static readonly SparkZap Instance = new();

    protected override void Execute(IUnit unit, Target target)
    {
        Pos dir = target.Pos!.Value;
        List<Pos> line = [];
        foreach (var pos in lvl.CollectLine(unit.Pos, dir, 6))
        {
            if (!lvl[pos].IsPassable) break;
            line.Add(pos);
        }
        if (line.Count > 0)
            Draw.AnimateBeam(unit.Pos, line[^1], new Glyph('*', ConsoleColor.Yellow), pulse: true);
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "zap")} a bolt of lightning!", "a crack of thunder");

        int dc = unit.GetSpellDC();
        foreach (var pos in line)
        {
            var victim = lvl.UnitAt(pos);
            if (victim == null || victim == unit) continue;

            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "shock");
            ctx.Damage.Add(new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Shock, HalfOnSave = true });
            DoDamage(ctx);
        }
    }
}

/// <summary>Create a mud slow area around self.</summary>
public class MudPool() : CooldownAction("mud pool", TargetingType.None, _ => 12)
{
    public static readonly MudPool Instance = new();

    protected override void Execute(IUnit unit, Target target)
    {
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "ooze")} mud everywhere!", "a squelching sound");
        using var tiles = lvl.CollectCircle(unit.Pos, 1, andCenter: true);
        var area = new MudSlowArea(unit, unit.GetSpellDC(), 8) { Tiles = [.. tiles] };
        lvl.CreateArea(area);
    }
}

/// <summary>Create fire area at target position.</summary>
public class LavaPuddle(int radius) : CooldownAction("lava puddle", TargetingType.None, _ => 10)
{
    public static readonly LavaPuddle Small = new(1);
    public static readonly LavaPuddle Large = new(2);

    protected override void Execute(IUnit unit, Target target)
    {
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "spew")} molten rock!", "a hiss of lava");
        using var tiles = lvl.CollectCircle(unit.Pos, radius, andCenter: true);
        Draw.AnimateFlash(tiles, new Glyph('≈', ConsoleColor.Red));
        var area = new FirePatchArea(unit, 6) { Tiles = [.. tiles] };
        lvl.CreateArea(area);
    }
}

record ElementType(
    string Name,
    ConsoleColor Color,
    DamageType? Immune,
    DamageType? Weakness
);

record ElementalTier(
    string Prefix,
    int LevelOffset,
    UnitSize Size,
    int HpPerLevel,
    int AC,
    int AB,
    int DmgBonus,
    char Glyph
);

public static class Elementals
{
    static readonly ElementType[] Types =
    [
        new("air",       ConsoleColor.Cyan,       null,              null),
        new("earth",     ConsoleColor.DarkYellow,  null,              null),
        new("fire",      ConsoleColor.Red,         DamageTypes.Fire,  DamageTypes.Cold),
        new("water",     ConsoleColor.Blue,        null,              null),
        new("ice",       ConsoleColor.White,       DamageTypes.Cold,  DamageTypes.Fire),
        new("lightning", ConsoleColor.Yellow,       DamageTypes.Shock, null),
        new("mud",       ConsoleColor.Gray,         DamageTypes.Acid,  null),
        new("magma",     ConsoleColor.DarkRed,     DamageTypes.Fire,  DamageTypes.Cold),
    ];

    static readonly ElementalTier[] Tiers =
    [
        new("small",   0, UnitSize.Small,   4, 0, 0, -1, 'v'),
        new("medium",  4, UnitSize.Medium,  5, 1, 1,  1, 'E'),
        new("large",   9, UnitSize.Large,   6, 2, 1,  3, 'E'),
    ];

    static WeaponDef Slam(UnitSize size) => size switch
    {
        UnitSize.Small  => NaturalWeapons.Slam_1d4,
        UnitSize.Medium => NaturalWeapons.Slam_1d6,
        _               => NaturalWeapons.Slam_2d6,
    };

    static ActionCost SpeedFor(string element) => element switch
    {
        "air" or "lightning" => 9,
        "fire"               => 9,
        "ice"                => ActionCosts.LandMove25,
        "water"              => ActionCosts.StandardLandMove,
        _                    => ActionCosts.LandMove20, // earth, magma, mud
    };

    static LogicBrick[] Components(ElementType elem, ElementalTier tier)
    {
        List<LogicBrick> c =
        [
            ElementalTraits.Instance,
        ];

        if (elem.Immune != null)
            c.Add(EnergyResist.RampFor(elem.Immune.Value).Immune);

        // TODO: vulnerability when that brick exists
        // if (elem.Weakness != null) ...

        if (elem.Name is "air" or "lightning")
            c.Add(ElementalFlight.Instance);

        if (elem.Name is "earth" or "mud" or "magma")
            c.Add(ElementalTremorsense.Instance);

        switch (elem.Name)
        {
            case "air":
                c.Add(tier.Size switch
                {
                    UnitSize.Small  => Whirlwind.Range1,
                    UnitSize.Medium => Whirlwind.Range2,
                    _               => Whirlwind.Range3,
                });
                break;

            case "earth":
                c.Add(tier.Size switch
                {
                    UnitSize.Small  => FlatDR.DR2,
                    UnitSize.Medium => FlatDR.DR5,
                    _               => FlatDR.DR10,
                });
                if (tier.Size >= UnitSize.Medium)
                    c.Add(new GrantAction(tier.Size >= UnitSize.Large ? BullRush.Push2 : BullRush.Push1));
                break;

            case "fire":
                c.Add(new ApplyFactOnAttackHit(FireBurnBuff.Instance.Timed(), 3));
                break;

            case "water":
                c.Add(Thorns.Cold_1d4);
                if (tier.Size >= UnitSize.Medium)
                    c.Add(new GrantAction(WaterCone.Instance));
                break;

            case "ice":
                c.Add(new ApplyFactOnAttackHit(NumbingColdDebuff.Instance.Timed(), 3));
                if (tier.Size >= UnitSize.Medium)
                    c.Add(new GrantAction(NumbingCold.Instance));
                break;

            case "lightning":
                c.Add(MeleeDamageRider.Shock_1d4);
                c.Add(MetalMastery.Instance);
                if (tier.Size >= UnitSize.Medium)
                    c.Add(new GrantAction(SparkZap.Instance));
                break;

            case "mud":
                c.Add(GrabOnHit.Instance);
                if (tier.Size >= UnitSize.Medium)
                    c.Add(new GrantAction(MudPool.Instance));
                break;

            case "magma":
                c.Add(Thorns.Fire_1d4);
                c.Add(MagmaTrail.Instance);
                if (tier.Size >= UnitSize.Medium)
                    c.Add(new GrantAction(tier.Size >= UnitSize.Large ? LavaPuddle.Large : LavaPuddle.Small));
                break;
        }

        // Basic attack last so active abilities get AI priority
        c.Add(new GrantAction(new NaturalAttack(Slam(tier.Size))));
        return [.. c];
    }

    static MonsterDef Make(ElementType elem, ElementalTier tier)
    {
        int level = 1 + tier.LevelOffset;
        string name = $"{tier.Prefix} {elem.Name} elemental";

        return new MonsterDef
        {
            id = $"elemental_{elem.Name}_{tier.Prefix}",
            Name = name,
            BrainFlags = MonFlags.NoCorpse,
            Family = "elemental",
            CreatureType = CreatureTypes.Elemental,
            Subtypes = [elem.Name],
            Glyph = new(tier.Glyph, elem.Color),
            HpPerLevel = tier.HpPerLevel,
            AC = tier.AC,
            AttackBonus = tier.AB,
            DamageBonus = tier.DmgBonus,
            LandMove = SpeedFor(elem.Name),
            Unarmed = Slam(tier.Size),
            Size = tier.Size,
            BaseLevel = level,
            MinDepth = Math.Max(1, level),
            SpawnWeight = 8,
            GroupSize = GroupSize.None,
            MoralAxis = MoralAxis.Neutral,
            EthicalAxis = EthicalAxis.Neutral,
            Components = Components(elem, tier),
        };
    }

    public static readonly MonsterDef[] All = [.. Types.SelectMany(e => Tiers.Select(t => Make(e, t)))];
}
