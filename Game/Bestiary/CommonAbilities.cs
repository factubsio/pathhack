namespace Pathhack.Game.Bestiary;

public enum BreathShape { Cone, Line, Burst }

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

    public static void CollectBreath(BreathShape shape, IUnit source, Pos dir, int range, ConsoleColor color, string name, Action<IUnit> onHit, Action<Pos>? onTile = null, string verb = "breathe")
    {
        if (shape == BreathShape.Cone)
        {
            using var cone = lvl.CollectCone(source.Pos, dir, range);
            Draw.AnimateFlash(cone, new Glyph('≈', color));
            g.YouObserve(source, $"{source:The} {VTense(source, verb)} {name}!", $"a blast of {name}");
            foreach (var pos in cone)
            {
                onTile?.Invoke(pos);
                var victim = lvl.UnitAt(pos);
                if (victim != null) onHit(victim);
            }
        }
        else if (shape == BreathShape.Burst)
        {
            using var area = lvl.CollectCircle(source.Pos, range, andCenter: false);
            Draw.AnimateFlash(area, new Glyph('*', color));
            foreach (var pos in area)
            {
                onTile?.Invoke(pos);
                var victim = lvl.UnitAt(pos);
                if (victim != null) onHit(victim);
            }
        }
        else
        {
            g.YouObserve(source, $"{source:The} {VTense(source, verb)} {name}!", $"a blast of {name}");
            Beam.Cast(source.Pos, dir, "breath", new('*', color), range,
                BeamFlags.None, victim =>
                {
                    onHit(victim);
                    return BeamHit.Continue;
                }, onTile: onTile);
        }
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        Dice damage = scaling(unit.EffectiveLevel);
        string name = BreathName(damageType);
        int dc = unit.GetSpellDC();

        CollectBreath(shape, unit, dir, MaxRange, color, name, victim =>
        {
            if (victim == unit) return;

            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, damageType.SubCat);
            ctx.Damage.Add(new DamageRoll { Formula = damage, Type = damageType, HalfOnSave = true });
            if (victim.IsPlayer) g.pline($"You are engulfed in {name}!");
            DoDamage(ctx);
        }, AreaSystem.OnTile(damageType));
    }
}
