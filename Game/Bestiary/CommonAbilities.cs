namespace Pathhack.Game.Bestiary;

public enum BreathShape { Cone, Line }

public class BreathAttack(
    BreathShape shape,
    DamageType damageType,
    ConsoleColor color,
    Func<IUnit, int> cooldown,
    Func<int, Dice> scaling,
    int maxRange
) : CooldownAction("breath weapon", TargetingType.Direction, cooldown, maxRange, tags: AbilityTags.Biological)
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

    public static void CollectBreath(BreathShape shape, IUnit source, Pos dir, int range, ConsoleColor color, string name, Action<Pos, IUnit?> action, Action<IEnumerable<Pos>>? afterAll = null)
    {
        if (shape == BreathShape.Cone)
        {
            using var cone = lvl.CollectCone(source.Pos, dir, range);
            Draw.AnimateFlash(cone, new Glyph('≈', color));
            g.YouObserve(source, $"{source:The} breathes {name}!", $"a blast of {name}");
            foreach (var pos in cone)
                action(pos, lvl.UnitAt(pos));
            afterAll?.Invoke(cone);
        }
        else
        {
            List<Pos> line = [];
            foreach (var pos in lvl.CollectLine(source.Pos, dir, range))
            {
                if (!lvl[pos].IsPassable) break;
                line.Add(pos);
            }
            if (line.Count > 0)
                Draw.AnimateBeam(source.Pos, line[^1], new Glyph('*', color));
            g.YouObserve(source, $"{source:The} breathes {name}!", $"a blast of {name}");
            foreach (var pos in line)
                action(pos, lvl.UnitAt(pos));
            afterAll?.Invoke(line);
        }
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        Dice damage = scaling(unit.EffectiveLevel);
        string name = BreathName(damageType);
        int dc = unit.GetSpellDC();

        CollectBreath(shape, unit, dir, MaxRange, color, name, (pos, victim) =>
        {
            if (victim == null || victim == unit) return;

            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, damageType.SubCat);
            ctx.Damage.Add(new DamageRoll { Formula = damage, Type = damageType, HalfOnSave = true });
            if (victim.IsPlayer) g.pline($"You are engulfed in {name}!");
            DoDamage(ctx);
        });
    }
}
