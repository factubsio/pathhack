namespace Pathhack.UI;

public static class RuneForge
{
    public static void Open(Inventory inv)
    {
        var weapons = inv.Where(i => i.Def is WeaponDef).ToList();
        if (weapons.Count == 0) { g.pline("You have no weapons."); return; }
        ListPicker.Pick(weapons, "Forge", custom: (w, item, own) =>
        {
            int index = own ? 0 : -1;
            int runeScroll = 0;
            List<Item> availableRunes = [];

            void RefreshRunes(int slot)
            {
                RuneSlot slotType = slot == 0 ? RuneSlot.Fundamental : RuneSlot.Property;
                availableRunes = inv.Where(i => i.Def is RuneItemDef rd && rd.Rune.Slot == slotType).ToList();
                runeScroll = 0;
            }

            RefreshRunes(index);
            Render(ref w, item, index, availableRunes, runeScroll);
            if (!own) return false;

            Draw.Blit();

            while (true)
            {
                var key = Input.NextKey();
                switch (key.Key)
                {
                    case ConsoleKey.Escape:
                        return false;
                    case ConsoleKey.L:
                    case ConsoleKey.RightArrow:
                        index = Math.Clamp(index + 1, 0, 3);
                        RefreshRunes(index);
                        break;
                    case ConsoleKey.H:
                    case ConsoleKey.LeftArrow:
                        index = Math.Clamp(index - 1, 0, 3);
                        RefreshRunes(index);
                        break;
                    case ConsoleKey.J:
                    case ConsoleKey.DownArrow:
                        if (availableRunes.Count > 0)
                            runeScroll = Math.Min(runeScroll + 1, availableRunes.Count - 1);
                        break;
                    case ConsoleKey.K:
                    case ConsoleKey.UpArrow:
                        runeScroll = Math.Max(runeScroll - 1, 0);
                        break;
                    case ConsoleKey.Enter:
                        if (availableRunes.Count > 0)
                        {
                            int ci = Math.Clamp(runeScroll, 0, availableRunes.Count - 1);
                            var runeItem = availableRunes[ci];
                            var rd = (RuneItemDef)runeItem.Def;
                            ItemGen.ApplyRune(item, rd.Rune, fundamental: index == 0);
                            inv.Remove(runeItem);
                            RefreshRunes(index);
                        }
                        break;
                    default: break;
                }

                Render(ref w, item, index, availableRunes, runeScroll);
                Draw.Blit();
            }
        });
    }

    static void Render(ref WindowWriter w, Item item, int selected, List<Item> availableRunes, int runeScroll)
    {
        w.SetCursor(0, 0);
        w.Write(item.DisplayName, ConsoleColor.Yellow);
        w.NewLine();
        w.NewLine();

        for (int i = 0; i < 4; i++)
        {
            int sx = 1 + i * 12 + (i > 0 ? 2 : 0);
            bool isSel = i == selected;
            ConsoleColor outline = isSel ? ConsoleColor.Blue : ConsoleColor.Gray;
            bool drawRune = false;
            bool blocked = false;

            if (i == 0)
            {
                if (item.Fundamental == null) { }
                else if (item.Fundamental.Brick is NullFundamental)
                {
                    blocked = true;
                    drawRune = true;
                }
                else
                {
                    drawRune = true;
                }
            }
            else
            {
                int pi = i - 1;
                if (pi >= item.PropertySlots)
                    outline = ConsoleColor.DarkGray;
                else if (pi < item.PropertyRunes.Count)
                    drawRune = true;
            }

            w.SetCursor(sx, 3);
            w.WriteMulti(RuneArt.RuneEmpty, outline);

            if (drawRune)
            {
                w.SetCursor(sx, 3);
                if (blocked)
                {
                    w.WriteMulti(RuneArt.RunePlain, ConsoleColor.DarkRed, transparent: true);
                    w.SetCursor(sx, 3);
                    w.WriteMulti(RuneArt.RuneDetails[3], ConsoleColor.DarkRed, transparent: true, overwrite: 'X');
                }
                else
                {
                    var (detailColor, detailChar, quality) = GetRuneVisuals(item, i);
                    w.WriteMulti(RuneArt.RunePlain, ConsoleColor.Yellow, transparent: true);
                    w.SetCursor(sx, 3);
                    w.WriteMulti(RuneArt.RuneDetails[quality - 1], detailColor, transparent: true, overwrite: detailChar);
                }
            }

            w.SetCursor(sx, 12);
            if (i == 0)
                w.Write("Fundamental", ConsoleColor.DarkGray);
            else if (i == 1)
                w.Write("Property", ConsoleColor.DarkGray);
        }

        // carousel
        const int carouselX = 20, carouselY = 14, carouselW = 30;
        for (int row = 0; row < 5; row++)
        {
            w.SetCursor(carouselX, carouselY + row);
            w.Write(new string(' ', carouselW));
        }

        bool slotEmpty = selected == 0
            ? item.Fundamental == null
            : selected - 1 >= item.PropertyRunes.Count && selected - 1 < item.PropertySlots;

        if (slotEmpty && availableRunes.Count > 0)
        {
            int center = Math.Clamp(runeScroll, 0, availableRunes.Count - 1);
            int top = center - 1;

            if (top > 0) { w.SetCursor(carouselX + carouselW / 2, carouselY); w.Write("▲", ConsoleColor.DarkGray); }
            for (int j = 0; j < 3; j++)
            {
                int ri = top + j;
                if (ri < 0 || ri >= availableRunes.Count) continue;
                var rd = (RuneItemDef)availableRunes[ri].Def;
                w.SetCursor(carouselX, carouselY + 1 + j);
                bool isMid = ri == center;
                if (isMid)
                    w.Write(rd.Rune.QualifiedName, ConsoleColor.Black, ConsoleColor.White);
                else
                    w.Write(rd.Rune.QualifiedName, ConsoleColor.White);
            }
            if (top + 3 < availableRunes.Count) { w.SetCursor(carouselX + carouselW / 2, carouselY + 4); w.Write("▼", ConsoleColor.DarkGray); }
        }

        w.NewLine();
    }

    static (ConsoleColor color, char glyph, int quality) GetRuneVisuals(Item item, int slot)
    {
        RuneBrick rune = slot == 0
            ? (RuneBrick)item.Fundamental!.Brick
            : (RuneBrick)item.PropertyRunes[slot - 1].Brick;

        var (color, glyph) = rune switch
        {
            StrikingRune => (ConsoleColor.White, '+'),
            ElementalRune er => er.Category switch
            {
                0 => (ConsoleColor.Red, '≈'),
                1 => (ConsoleColor.Cyan, '≈'),
                _ => (ConsoleColor.Yellow, '*'),
            },
            _ => (ConsoleColor.Gray, '*'),
        };
        return (color, glyph, rune.Quality);
    }
}
