namespace Pathhack.Game;

public class ScrollDef : ItemDef
{
    public ScrollDef()
    {
        Glyph = new(ItemClasses.Scroll, ConsoleColor.White);
        AppearanceCategory = Game.AppearanceCategory.Scroll;
        Stackable = true;
    }
}

public static class Scrolls
{
    public static readonly ScrollDef MagicMapping = new() { Name = "scroll of magic mapping" };
    public static readonly ScrollDef Identify = new() { Name = "scroll of identify" };
    public static readonly ScrollDef Teleportation = new() { Name = "scroll of teleportation" };
    public static readonly ScrollDef Fire = new() { Name = "scroll of fire" };

    public static readonly ScrollDef[] All = [MagicMapping, Identify, Teleportation, Fire];

    static Scrolls()
    {
        for (int i = 0; i < All.Length; i++)
            All[i].AppearanceIndex = i;
    }

    public static void DoEffect(ScrollDef def, IUnit user, Func<Item?> pickItemToIdentify)
    {
        switch (def)
        {
            case var _ when def == MagicMapping:
                g.DoMapLevel();
                ItemDb.Instance.Identify(def);
                break;
            case var _ when def == Identify:
                ItemDb.Instance.Identify(def);
                g.pline("This is an identify scroll.");
                if (pickItemToIdentify() is { } toId)
                {
                    ItemDb.Instance.Identify(toId.Def);
                    g.pline($"{toId.InvLet} - {toId.DisplayName}.");
                }
                break;
            case var _ when def == Teleportation:
                DoRandomTeleport(user);
                ItemDb.Instance.Identify(def);
                break;
            case var _ when def == Fire:
                DoScrollFire(user);
                ItemDb.Instance.Identify(def);
                break;
        }
    }

    static void DoRandomTeleport(IUnit unit)
    {
        var dest = lvl.FindLocation(p => lvl[p].IsPassable && lvl.NoUnit(p));
        if (dest == null)
        {
            g.pline("You feel disoriented for a moment.");
            return;
        }
        lvl.MoveUnit(unit, dest.Value);
        g.pline("You are suddenly somewhere else!");
    }

    static void DoScrollFire(IUnit user)
    {
        g.pline("A pillar of fire erupts around you!");
        foreach (var dir in Pos.AllDirs)
        {
            var target = user.Pos + dir;
            if (lvl.UnitAt(target) is { } victim)
            {
                using var ctx = PHContext.Create(user, Target.From(victim));
                ctx.Damage.Add(new DamageRoll { Formula = d(6, 6), Type = DamageTypes.Fire });
                DoDamage(ctx);
            }
        }
    }
}
