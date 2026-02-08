namespace Pathhack.Map;

[Flags]
public enum TrapType
{
    None = 0,
    Pit = 1 << 0,
    Arrow = 1 << 1,
    Poison = 1 << 2,
    Alarm = 1 << 3,
    Fire = 1 << 4,
    Web = 1 << 5,
}

public abstract class Trap(TrapType type, int depth, int detectDelta, int escapeDelta, int escapeBonus)
{
    public TrapType Type => type;
    public bool PlayerSeen;

    public static int BaseDC(int depth) => 12 + depth / 3;

    public virtual int DetectDC => BaseDC(depth) + detectDelta;
    public virtual int EscapeDC => BaseDC(depth) + escapeDelta;
    public virtual int EscapeBonus => escapeBonus;

    public abstract bool Trigger(IUnit? unit, Item? item);
    public virtual bool TryEscape(IUnit unit) => true;
    public abstract Glyph Glyph { get; }

}

public class PitTrap(int depth) : Trap(TrapType.Pit, depth, 0, 0, 4)
{
    public override Glyph Glyph => new('^', ConsoleColor.Blue);
    private readonly bool IsSpiked = g.Rn2(4) == 0;
    private string Name => IsSpiked ? "spiked pit" : "pit";

    public override bool Trigger(IUnit? unit, Item? item)
    {
        if (unit == null) return false;

        if (unit.IsAwareOf(this) && g.Rn2(3) == 0)
        {
            g.pline($"{unit:The} {VTense(unit, "avoid")} a pit.");
            return false;
        }

        g.pline($"{unit:The} {VTense(unit, "fall")} into a {Name}!");
        using var ctx = PHContext.Create(Monster.DM, Target.From(unit));
        ctx.Damage.Add(new DamageRoll { Formula = d(1, 6), Type = DamageTypes.Blunt, HalfOnSave = true });
        if (IsSpiked)
        {
            ctx.Damage.Add(new DamageRoll { Formula = d(1, 6), Type = DamageTypes.Piercing });
        }
        CreateAndDoCheck(ctx, "reflex_save", 15, "trap");
        DoDamage(ctx);
        unit.TrappedIn = this;
        unit.EscapeAttempts = 0;
        return true;
    }

    public override bool TryEscape(IUnit unit)
    {
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "climb")} out of the pit.");
        return true;
    }
}

public class WebTrap(int depth) : Trap(TrapType.Web, depth, -2, 0, 4)
{
    public override Glyph Glyph => new('"', ConsoleColor.Gray);

    public override bool Trigger(IUnit? unit, Item? item)
    {
        if (unit == null) return false;
        if (unit.Has("web_immunity")) return false;

        if (unit.IsAwareOf(this) && g.Rn2(3) == 0)
        {
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "avoid")} a web.");
            return false;
        }

        g.YouObserve(unit, $"{0:The} {VTense(unit, "get")} caught in a web!");
        unit.TrappedIn = this;
        unit.EscapeAttempts = 0;
        return true;
    }

    public override bool TryEscape(IUnit unit)
    {
        var bonus = unit.EscapeAttempts * EscapeBonus;
        using var ctx = PHContext.Create(unit, Target.From(unit));
        ctx.Check = new Check { DC = EscapeDC };
        ctx.Check.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, bonus, "struggling"));
        if (CreateAndDoCheck(ctx, "athletics", EscapeDC, "web"))
        {
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "break")} free of the web!");
            if (g.Rn2(3) == 0)
            {
                // This is a bit poop, we should probably know our own position?
                lvl.Traps.Remove(unit.Pos);
            }
            return true;
        }
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "struggle")} in the web.");
        return false;
    }
}
