namespace Pathhack.Game.Bestiary;

public enum BreathShape { Cone, Line }

public class BreathWeapon(BreathShape shape, DamageType damageType, ConsoleColor color, string pool = "dragon_breath") : ActionBrick("breath weapon", tags: AbilityTags.Biological)
{
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        if (!unit.HasCharge(pool, out whyNot)) return false;
        whyNot = "can't see target";
        if (unit is not Monster m || !m.CanSeeYou) return false;
        whyNot = "out of range";
        if (unit.Pos.ChebyshevDist(target.Pos!.Value) > Range(unit)) return false;

        if (shape == BreathShape.Line)
        {
            var delta = target.Pos!.Value - unit.Pos;
            whyNot = "not in line";
            if (delta.X != 0 && delta.Y != 0 && Math.Abs(delta.X) != Math.Abs(delta.Y)) return false;
        }

        whyNot = "";
        return true;
    }

    int Range(IUnit unit) => shape == BreathShape.Cone ? 3 + unit.EffectiveLevel / 4 : 5 + unit.EffectiveLevel / 3;

    Dice BreathDamage(IUnit unit)
    {
        int lvl = unit.EffectiveLevel;
        int dice = Math.Max(1, lvl / 2);
        int faces = lvl >= 16 ? 10 : 8;
        return d(dice, faces);
    }

    static string BreathName(DamageType dt) => dt.SubCat switch
    {
        "cold" => "frost",
        "shock" => "lightning",
        _ => dt.SubCat,
    };

    public override void Execute(IUnit unit, object? data, Target target)
    {
        unit.TryUseCharge(pool);
        Pos dir = (target.Pos!.Value - unit.Pos).Signed;
        int range = Range(unit);
        string name = BreathName(damageType);

        if (shape == BreathShape.Cone)
        {
            using var cone = lvl.CollectCone(unit.Pos, dir, range);
            Draw.AnimateFlash(cone, new Glyph('≈', color));
            g.YouObserve(unit, $"{{0:The}} breathes {name}!", $"a blast of {name}");
            HitArea(unit, cone, name);
        }
        else
        {
            List<Pos> line = [];
            foreach (var pos in lvl.CollectLine(unit.Pos, dir, range))
            {
                if (!lvl[pos].IsPassable) break;
                line.Add(pos);
            }
            if (line.Count > 0)
                Draw.AnimateBeam(unit.Pos, line[^1], new Glyph('*', color));
            g.YouObserve(unit, $"{{0:The}} breathes {name}!", $"a blast of {name}");
            HitArea(unit, line, name);
        }
    }

    void HitArea(IUnit unit, IEnumerable<Pos> area, string name)
    {
        int dc = unit.GetSpellDC();
        Dice damage = BreathDamage(unit);

        foreach (var pos in area)
        {
            var victim = lvl.UnitAt(pos);
            if (victim.IsNullOrDead() || victim == unit) continue;

            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, damageType.SubCat);
            ctx.Damage = [new DamageRoll { Formula = damage, Type = damageType, HalfOnSave = true }];
            if (victim.IsPlayer) g.pline($"You are engulfed in {name}!");
            DoDamage(ctx);
        }
    }
}

/// <summary>Flat reduction of a specific damage type. Like DR but keyed on DamageType instead of bypass tag.</summary>
public class EnergyResist(DamageType type, int amount) : LogicBrick
{
    public override string? PokedexDescription => $"Resist {type.SubCat} {amount}";

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
        foreach (var roll in ctx.Damage)
            if (roll.Type == type) roll.ApplyDR(amount);
    }

    // Common presets
    public static EnergyResist Fire(int n) => new(DamageTypes.Fire, n);
    public static EnergyResist Cold(int n) => new(DamageTypes.Cold, n);
    public static EnergyResist Shock(int n) => new(DamageTypes.Shock, n);
    public static EnergyResist Acid(int n) => new(DamageTypes.Acid, n);
}

public record DragonColor(
    string Name,
    ConsoleColor GlyphColor,
    DamageType BreathType,
    BreathShape BreathShape,
    MoralAxis Moral,
    EthicalAxis Ethical
);

public record DragonAge(
    string Name,
    int LevelOffset,
    int SizeStep,    // 0=Medium, 1=Large, 2=Huge, 3=Gargantuan
    int HpPerLevel,
    int AC,
    int AB,
    int DmgBonus
);

/// <summary>Peaceful if player alignment is within 1 step on both axes.</summary>
public class AlignmentPeaceful(MoralAxis moral, EthicalAxis ethical) : LogicBrick
{
    protected override void OnSpawn(Fact fact, PHContext ctx)
    {
        if (ctx.Source is not Monster m) return;
        bool close = Math.Abs((int)moral - (int)u.MoralAxis) <= 1
                   && Math.Abs((int)ethical - (int)u.EthicalAxis) <= 1;
        m.Peaceful = close;
    }
}

public static class Dragons
{
    static readonly DragonColor[] Colors =
    [
        // Chromatic
        new("black",  ConsoleColor.DarkGray,   DamageTypes.Acid,  BreathShape.Line, MoralAxis.Evil,    EthicalAxis.Chaotic),
        new("blue",   ConsoleColor.Blue,        DamageTypes.Shock, BreathShape.Line, MoralAxis.Evil,    EthicalAxis.Lawful),
        new("green",  ConsoleColor.DarkGreen,   DamageTypes.Acid,  BreathShape.Cone, MoralAxis.Evil,    EthicalAxis.Lawful),
        new("red",    ConsoleColor.Red,          DamageTypes.Fire,  BreathShape.Cone, MoralAxis.Evil,    EthicalAxis.Chaotic),
        new("white",  ConsoleColor.White,        DamageTypes.Cold,  BreathShape.Cone, MoralAxis.Evil,    EthicalAxis.Chaotic),
        // Metallic
        new("brass",  ConsoleColor.DarkYellow,  DamageTypes.Fire,  BreathShape.Line, MoralAxis.Good,    EthicalAxis.Chaotic),
        new("bronze", ConsoleColor.DarkYellow,  DamageTypes.Shock, BreathShape.Line, MoralAxis.Good,    EthicalAxis.Lawful),
        new("copper", ConsoleColor.Yellow,       DamageTypes.Acid,  BreathShape.Line, MoralAxis.Good,    EthicalAxis.Chaotic),
        new("gold",   ConsoleColor.Yellow,       DamageTypes.Fire,  BreathShape.Cone, MoralAxis.Good,    EthicalAxis.Lawful),
        new("silver", ConsoleColor.Gray,         DamageTypes.Cold,  BreathShape.Cone, MoralAxis.Good,    EthicalAxis.Lawful),
    ];

    // Base levels per color (compressed from 1e CR to our 1-20 range)
    static readonly Dictionary<string, int> BaseLevels = new()
    {
        ["white"] = 2, ["black"] = 3, ["brass"] = 3, ["green"] = 4, ["copper"] = 4,
        ["blue"] = 5, ["bronze"] = 5, ["red"] = 6, ["silver"] = 6, ["gold"] = 7,
    };

    static readonly DragonAge[] Ages =
    [
        new("wyrmling", 0,  0, 6,  0, 0, 0),
        new("young",    4,  1, 8,  1, 1, 2),
    ];

    static UnitSize SizeFromStep(int step) => step switch
    {
        0 => UnitSize.Medium,
        1 => UnitSize.Large,
        2 => UnitSize.Huge,
        _ => UnitSize.Gargantuan,
    };

    static MonsterDef MakeDragon(DragonColor color, DragonAge age)
    {
        int baseLevel = BaseLevels[color.Name];
        int level = baseLevel + age.LevelOffset;
        UnitSize size = SizeFromStep(age.SizeStep);
        bool isWyrmling = age.Name == "wyrmling";
        string name = isWyrmling ? $"{color.Name} wyrmling" : $"{age.Name} {color.Name} dragon";
        char glyph = 'D';
        int resist = 5 + level / 3;

        WeaponDef bite = size switch
        {
            >= UnitSize.Huge => NaturalWeapons.Bite_2d8,
            >= UnitSize.Large => NaturalWeapons.Bite_1d8,
            _ => NaturalWeapons.Bite_1d6,
        };

        WeaponDef claw = size switch
        {
            >= UnitSize.Huge => NaturalWeapons.Claw_1d6,
            >= UnitSize.Large => NaturalWeapons.Claw_1d4,
            _ => NaturalWeapons.Claw_1d3,
        };

        BreathWeapon breath = new(color.BreathShape, color.BreathType, color.GlyphColor);

        return new MonsterDef
        {
            id = $"dragon_{color.Name}_{age.Name}",
            Name = name,
            Family = "dragon",
            CreatureType = CreatureTypes.Dragon,
            Glyph = new(glyph, color.GlyphColor),
            HpPerLevel = age.HpPerLevel,
            AC = age.AC,
            AttackBonus = age.AB,
            DamageBonus = age.DmgBonus,
            LandMove = ActionCosts.StandardLandMove,
            Unarmed = bite,
            Size = size,
            BaseLevel = level,
            SpawnWeight = 100,
            MinDepth = Math.Max(1, level + 2),
            MoralAxis = color.Moral,
            EthicalAxis = color.Ethical,
            Components =
            [
                new AlignmentPeaceful(color.Moral, color.Ethical),
                new GrantPool("dragon_breath", 1, 12),
                new GrantAction(breath),
                new GrantAction(new FullAttack("dragon", bite, claw, claw)),
                new EnergyResist(color.BreathType, resist),
                new QueryBrick(CreatureTags.Flying, true),
            ],
        };
    }

    public static readonly MonsterDef[] All = [.. Colors.SelectMany(c => Ages.Select(a => MakeDragon(c, a)))];
}
