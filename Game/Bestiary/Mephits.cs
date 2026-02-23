namespace Pathhack.Game.Bestiary;

public class MephitBreath(BreathShape shape, DamageType damageType, ConsoleColor color) : CooldownAction("breath weapon", TargetingType.Direction, _ => 3, maxRange: shape == BreathShape.Cone ? 2 : 4, tags: AbilityTags.Biological)
{

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        if (unit is not Monster m || !m.CanSeeYou) return "can't see target";
        return true;
    }

    static string BreathName(DamageType dt) => dt.SubCat switch
    {
        "cold" => "frost",
        "shock" => "lightning",
        "slashing" => "grit",
        "piercing" => "salt crystals",
        _ => dt.SubCat,
    };

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        string name = BreathName(damageType);

        if (shape == BreathShape.Cone)
        {
            using var cone = lvl.CollectCone(unit.Pos, dir, MaxRange);
            Draw.AnimateFlash(cone, new Glyph('≈', color));
            g.YouObserve(unit, $"{unit:The} breathes {name}!", $"a puff of {name}");
            HitArea(unit, cone);
        }
        else
        {
            List<Pos> line = [];
            foreach (var pos in lvl.CollectLine(unit.Pos, dir, MaxRange))
            {
                if (!lvl[pos].IsPassable) break;
                line.Add(pos);
            }
            if (line.Count > 0)
                Draw.AnimateBeam(unit.Pos, line[^1], new Glyph('*', color));
            g.YouObserve(unit, $"{unit:The} breathes {name}!", $"a puff of {name}");
            HitArea(unit, line);
        }
    }

    void HitArea(IUnit unit, IEnumerable<Pos> area)
    {
        int dc = unit.GetSpellDC();
        foreach (var pos in area)
        {
            var victim = lvl.UnitAt(pos);
            if (victim.IsNullOrDead() || victim == unit) continue;

            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, damageType.SubCat);
            ctx.Damage.Add(new DamageRoll { Formula = d(4), Type = damageType, HalfOnSave = true });
            DoDamage(ctx);
        }
    }
}

record MephitType(
    string Name,
    ConsoleColor Color,
    DamageType BreathType,
    BreathShape BreathShape,
    DamageType? Immune
);

public static class Mephits
{
    static readonly MephitType[] Types =
    [
        new("fire",      ConsoleColor.Red,        DamageTypes.Fire,     BreathShape.Cone, DamageTypes.Fire),
        new("ice",       ConsoleColor.White,       DamageTypes.Cold,     BreathShape.Cone, DamageTypes.Cold),
        new("earth",     ConsoleColor.DarkYellow,  DamageTypes.Acid,     BreathShape.Line, DamageTypes.Acid),
        new("water",     ConsoleColor.Blue,        DamageTypes.Cold,     BreathShape.Cone, null),
        new("dust",      ConsoleColor.Gray,        DamageTypes.Slashing, BreathShape.Cone, null),
        new("steam",     ConsoleColor.DarkGray,    DamageTypes.Fire,     BreathShape.Cone, null),
        new("salt",      ConsoleColor.Cyan,        DamageTypes.Piercing, BreathShape.Line, null),
        new("mud",       ConsoleColor.DarkYellow,  DamageTypes.Acid,     BreathShape.Line, null),
    ];

    static LogicBrick[] Components(MephitType type)
    {
        List<LogicBrick> c =
        [
            ElementalTraits.Instance,
            ElementalFlight.Instance,
        ];

        if (type.Immune != null)
            c.Add(EnergyResist.RampFor(type.Immune.Value).Immune);

        c.Add(new GrantAction(new MephitBreath(type.BreathShape, type.BreathType, type.Color)));
        c.Add(new GrantAction(new NaturalAttack(NaturalWeapons.Slam_1d4)));
        return [.. c];
    }

    static MonsterDef Make(MephitType type) => new()
    {
        id = $"mephit_{type.Name}",
        Name = $"{type.Name} mephit",
        Family = "mephit",
        BrainFlags = MonFlags.NoCorpse,
        CreatureType = CreatureTypes.Outsider,
        Subtypes = ["Elemental"],
        Glyph = new('v', type.Color),
        HpPerLevel = 4,
        AC = 0,
        AttackBonus = 0,
        Unarmed = NaturalWeapons.Slam_1d4,
        LandMove = ActionCosts.LandMove20,
        Size = UnitSize.Small,
        BaseLevel = 3,
        MinDepth = 2,
        SpawnWeight = 10,
        GroupSize = GroupSize.SmallMixed,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        Components = Components(type),
    };

    public static readonly MonsterDef[] All = [.. Types.Select(Make)];
}
