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
        bool doIdentify = user.IsPlayer;

        switch (def)
        {
            case var _ when def == MagicMapping:
                if (!user.IsPlayer) return;

                g.DoMapLevel();
                break;
            case var _ when def == Identify:
                if (!user.IsPlayer) return;

                def.SetKnown();
                g.pline("This is an identify scroll.");
                if (pickItemToIdentify() is { } toId)
                {
                    toId.Def.SetKnown();
                    toId.Knowledge |= ItemKnowledge.Props | ItemKnowledge.BUC;
                    g.pline($"{toId.InvLet} - {toId.DisplayName}.");
                }
                break;
                
            case var _ when def == Teleportation:
                var dest = lvl.FindLocation(p => lvl[p].IsPassable && lvl.NoUnit(p));
                if (dest == null)
                {
                    g.YouObserveSelf(user, "You feel disoriented for a moment.", $"{user:The} seems to shudder in place.");
                }
                else
                {
                    g.YouObserveSelf(user, "You are suddenly somewhere else!", $"{user:The} disappears!");
                    lvl.MoveUnit(user, dest.Value);
                }
                break;
            case var _ when def == Fire:
                g.YouObserve(user, $"A pillar of fire erupts around {user:the}!");
                foreach (var n in user.Pos.Neighbours(true))
                {
                    if (lvl.UnitAt(n) is { } victim)
                    {
                        using var ctx = PHContext.Create(user, Target.From(victim));
                        ctx.Damage.Add(new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Fire });
                        DoDamage(ctx);
                    }
                }
                break;
        }

        if (doIdentify) def.SetKnown();
    }
}
