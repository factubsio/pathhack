namespace Pathhack.UI;

static class ShopServicesUI
{
    enum FilterMode { None, IdentifyDef, IdentifyProps }

    public static void Show(ShopState shop)
    {
        List<Item> items = [..u.Inventory
            .OrderBy(i => ItemClasses.Order.IndexOf(i.Def.Class))
            .ThenBy(i => i.InvLet)];
        if (items.Count == 0)
        {
            g.pline("You have nothing in your inventory.");
            return;
        }

        FilterMode mode = FilterMode.None;
        WindowWriter? rhs = null;

        ListPicker.Pick(items, $"{shop.Shopkeeper:The}'s Services", listWidth: 32, custom: (w, item, own) =>
        {
            rhs = w;
            RenderRHS(w, shop, item, mode);
            return false;
        },
        keyHandler: (item, key) =>
        {
            if (key.KeyChar == 'i' && item != null) { DoIdentifyDef(shop, item, rhs); return true; }
            if (key.KeyChar == 'p' && item != null) { DoIdentifyProps(shop, item, rhs); return true; }
            if (key.KeyChar == 'I') { mode = mode == FilterMode.IdentifyDef ? FilterMode.None : FilterMode.IdentifyDef; return true; }
            if (key.KeyChar == 'P') { mode = mode == FilterMode.IdentifyProps ? FilterMode.None : FilterMode.IdentifyProps; return true; }
            if (key.KeyChar == 'A') { mode = FilterMode.None; return true; }
            return false;
        },
        groupBy: i => Input.ClassDisplayName(i.Def.Class),
        emptyKeyDispatch: true,
        extraFilter: i => mode switch
        {
            FilterMode.IdentifyDef => ShopServices.CanIdentifyDef(i),
            FilterMode.IdentifyProps => ShopServices.CanIdentifyProps(i),
            _ => true,
        });
    }

    static void ShowResult(WindowWriter? w, string msg)
    {
        if (w is not { } rhs) return;
        rhs.SetCursor(0, 0);
        rhs.Clear();
        rhs.SetCursor(0, 0);
        rhs.Write(msg, ConsoleColor.Yellow);
        rhs.SetCursor(0, 2);
        rhs.Write("[any key]", ConsoleColor.DarkGray);
        Draw.Blit();
        Input.NextKey();
    }

    static void RenderRHS(WindowWriter w, ShopState shop, Item? item, FilterMode mode)
    {
        int y = 0;
        w.SetCursor(0, y++);
        w.Write($"Gold: {u.Gold.Crests()}", ConsoleColor.Yellow);
        y++;

        if (item != null)
        {
            w.SetCursor(0, y++);
            w.Write("Services:", ConsoleColor.White);
            y++;

            bool canDef = ShopServices.CanIdentifyDef(item);
            w.SetCursor(2, y++);
            if (canDef)
            {
                int price = ShopServices.AdjustedDefPrice(shop, item.Def);
                ConsoleColor priceColor = u.Gold >= price ? ConsoleColor.Green : ConsoleColor.Red;
                w.Write("[i] ", ConsoleColor.Cyan);
                w.Write($"Identify — {price.Crests()}", priceColor);
            }
            else
            {
                w.Write("[i] Identify — ", ConsoleColor.DarkGray);
                w.Write("already known", ConsoleColor.DarkGray);
            }

            bool canProps = ShopServices.CanIdentifyProps(item);
            w.SetCursor(2, y++);
            if (canProps)
            {
                ConsoleColor priceColor = u.Gold >= ShopServices.IdentifyPropsPrice ? ConsoleColor.Green : ConsoleColor.Red;
                w.Write("[p] ", ConsoleColor.Cyan);
                w.Write($"Identify properties — {ShopServices.IdentifyPropsPrice.Crests()}", priceColor);
            }
            else
            {
                w.Write("[p] Identify properties — ", ConsoleColor.DarkGray);
                w.Write("fully known", ConsoleColor.DarkGray);
            }

            y++;
        }
        w.SetCursor(0, y++);
        w.Write($"[I] Filter: identify eligible{(mode == FilterMode.IdentifyDef ? " (active)" : "")}", ConsoleColor.DarkGray);
        w.SetCursor(0, y++);
        w.Write($"[P] Filter: props eligible{(mode == FilterMode.IdentifyProps ? " (active)" : "")}", ConsoleColor.DarkGray);
        if (mode != FilterMode.None)
        {
            w.SetCursor(0, y++);
            w.Write("[A] Clear filter", ConsoleColor.DarkGray);
        }
    }

    static void DoIdentifyDef(ShopState shop, Item item, WindowWriter? w)
    {
        if (!ShopServices.CanIdentifyDef(item)) return;
        int price = ShopServices.AdjustedDefPrice(shop, item.Def);
        if (u.Gold < price) return;

        u.Gold -= price;
        item.Def.SetKnown();
        ShowResult(w, $"Identified: {item.DisplayName}");
    }

    static void DoIdentifyProps(ShopState shop, Item item, WindowWriter? w)
    {
        if (!ShopServices.CanIdentifyProps(item)) return;
        if (u.Gold < ShopServices.IdentifyPropsPrice) return;

        u.Gold -= ShopServices.IdentifyPropsPrice;
        item.Knowledge |= item.Def.RelevantKnowledge;
        ShowResult(w, $"Appraised: {item.DisplayName}");
    }
}
