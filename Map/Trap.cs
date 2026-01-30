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
}

public abstract class Trap(TrapType type, int depth, int detectDC, int escapeDC, int escapeBonus)
{
    public TrapType Type => type;
    public bool PlayerSeen;

    private readonly int boost = depth;

    public virtual int DetectDC => detectDC + (int)(boost * boost * 0.2);
    public virtual int EscapeDC => escapeDC + (int)(boost * boost * 0.1);
    public virtual int EscapeBonus => escapeBonus;

    public abstract bool Trigger(IUnit? unit, Item? item);
    public virtual bool TryEscape(IUnit unit) => true;
    public abstract Glyph Glyph { get; }

}

public class PitTrap(int depth) : Trap(TrapType.Pit, depth, 15, 15, 4)
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
        ctx.Damage.Add(new DamageRoll { Formula = d(1, 6), Type = DamageTypes.Blunt });
        if (IsSpiked)
        {
            ctx.Damage.Add(new DamageRoll { Formula = d(1, 6), Type = DamageTypes.Piercing });
        }
        if (CreateAndDoCheck(ctx, "reflex_save", 15, "pit trap"))
            ctx.Damage[0].Halve();
        DoDamage(ctx);
        unit.TrappedIn = this;
        unit.EscapeAttempts = 0;
        return true;
    }

    public override bool TryEscape(IUnit unit)
    {
        g.pline($"{unit:The} {VTense(unit, "climb")} out of the pit.");
        return true;
    }
}
