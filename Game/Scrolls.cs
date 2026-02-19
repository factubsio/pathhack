namespace Pathhack.Game;

public class BookDef : ItemDef
{
    public BookDef()
    {
        Glyph = new(ItemClasses.Spellbook, ConsoleColor.White);
        AppearanceCategory = Game.AppearanceCategory.Spellbook;
    }

}

public class ScrollDef : ItemDef
{
    public ScrollDef()
    {
        Glyph = new(ItemClasses.Scroll, ConsoleColor.White);
        AppearanceCategory = Game.AppearanceCategory.Scroll;
        Stackable = true;
        Weight = 5;
    }
}

[GenerateAll("All", typeof(ScrollDef))]
public static partial class Scrolls
{
    public static readonly ScrollDef MagicMapping = new() { Name = "scroll of magic mapping", Price = 300 };
    public static readonly ScrollDef Identify = new() { Name = "scroll of identify", Price = 40 };
    public static readonly ScrollDef Teleportation = new() { Name = "scroll of teleportation", Price = 120 };
    public static readonly ScrollDef Fire = new() { Name = "scroll of fire", Price = 120 };
    public static readonly ScrollDef RemoveCurse = new() { Name = "scroll of remove curse", Price = 200 };

    public static void DoEffect(ScrollDef def, IUnit user, Func<Item?> pickItemToIdentify, BUC buc = BUC.Uncursed)
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
                    g.pline($"{toId.InvLet} - {toId.DisplayNameWeighted}.");
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
            case var _ when def == RemoveCurse:
                if (!user.IsPlayer) return;
                var cursedItems = user.Equipped.Values.Distinct().Where(i => i.BUC == BUC.Cursed).ToList();
                if (cursedItems.Count > 0)
                {
                    var toUncurse = buc == BUC.Blessed
                        ? cursedItems
                        : [cursedItems[g.Rn2(cursedItems.Count)]];
                    foreach (var item in toUncurse)
                    {
                        item.BUC = BUC.Uncursed;
                        item.Knowledge |= ItemKnowledge.BUC;
                    }
                }
                g.pline(cursedItems.Count > 0 ? "You feel a malevolent aura dissipate." : "You feel vaguely reassured.");
                break;
        }

        if (doIdentify) def.SetKnown();
    }
}
