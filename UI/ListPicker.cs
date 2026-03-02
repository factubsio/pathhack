namespace Pathhack.UI;

public interface ISelectable
{
    string Name { get; }
    string? Subtitle => null;
    string Description { get; }
    IEnumerable<string> Details => [];
    public string? WhyNot { get; }
    string[] Tags => [];
    ConsoleColor? ListColor => null;
}

public record class SimpleSelectable(string Name, string Description) : ISelectable
{
    public string? WhyNot => null;
}

public delegate bool ListPickerDrawCallback<T>(WindowWriter writer, T item, bool own);

public static class ListPicker
{
    const int DefaultListWidth = 24;

    public static T? Pick<T>(IReadOnlyList<T> items, string prompt, int defaultIndex = 0, ListPickerDrawCallback<T>? custom = null, Func<T, ConsoleKeyInfo, bool>? keyHandler = null, int listWidth = DefaultListWidth, Func<T, string>? groupBy = null, Func<T, bool>? extraFilter = null, bool emptyKeyDispatch = false) where T : class, ISelectable
    {
        if (items.Count == 0) return null;

        using var handle = WM.CreateTransient(Draw.ScreenWidth, Draw.ScreenHeight, z: 5, opaque: true);
        var win = handle.Window;

        int index = Math.Clamp(defaultIndex, 0, items.Count - 1);
        string? filter = null;
        bool typing = false;
        IReadOnlyList<T> visible = items;

        while (true)
        {
            if (filter != null || extraFilter != null)
            {
                IEnumerable<T> filtered = items;
                if (filter != null)
                    filtered = filtered.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
                if (extraFilter != null)
                    filtered = filtered.Where(extraFilter);
                visible = filtered.ToList();
            }
            else
                visible = items;

            if (visible.Count > 0)
                index = Math.Clamp(index, 0, visible.Count - 1);

            DrawPicker(win, visible, index, filter != null ? $"{prompt} [/{filter}{(typing ? "▌" : "")}]" : prompt, null, 0, custom, listWidth: listWidth, groupBy: groupBy);
            var key = Input.NextKey();

            if (typing)
            {
                if (key.Key == ConsoleKey.Escape) { filter = null; typing = false; index = 0; continue; }
                if (key.Key == ConsoleKey.Enter) { typing = false; continue; }
                if (key.Key == ConsoleKey.Backspace) { if (filter!.Length > 0) filter = filter[..^1]; continue; }
                if (key.KeyChar >= ' ' && key.KeyChar <= '~') { filter += key.KeyChar; index = 0; continue; }
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.UpArrow or ConsoleKey.K:
                    index = (index - 1 + visible.Count) % visible.Count;
                    break;
                case ConsoleKey.DownArrow or ConsoleKey.J:
                    index = (index + 1) % visible.Count;
                    break;
                case ConsoleKey.Enter or ConsoleKey.RightArrow:
                    if (visible.Count > 0 && visible[index].WhyNot == null)
                    {
                        if (custom != null)
                        {
                            int detailX = listWidth + 2;
                            int paddedWidth = Math.Clamp(Draw.ScreenWidth - 10, Draw.MapWidth, 120);
                            win.At(detailX - 2, 0).Write("------", fg: ConsoleColor.Yellow);
                            win.At(detailX - 2, 1).WriteVertical("||||||", fg: ConsoleColor.Yellow);
                            Draw.Blit();
                            var rhs = win.At(detailX, 2, paddedWidth - detailX - 2, Draw.ScreenHeight - 4);
                            bool exit = custom(rhs, visible[index], true);
                            if (exit) return null;
                        }
                        else return visible[index];
                    }
                    break;
                case ConsoleKey.Escape:
                case ConsoleKey.Q when !typing:
                    return null;
                default:
                    if (key.KeyChar == '/') { filter = ""; typing = true; continue; }
                    if (keyHandler != null && (visible.Count > 0 ? keyHandler(visible[index], key) : emptyKeyDispatch && keyHandler(null!, key))) break;
                    break;
            }
        }
    }

    public static List<T>? PickMultiple<T>(IReadOnlyList<T> items, string prompt, int count) where T : class, ISelectable
    {
        using var handle = WM.CreateTransient(Draw.ScreenWidth, Draw.ScreenHeight, z: 5, opaque: true);
        var win = handle.Window;

        int index = 0;
        HashSet<int> selected = [];
        while (true)
        {
            DrawPicker(win, items, index, prompt, selected, count);
            var key = Input.NextKey();
            switch (key.Key)
            {
                case ConsoleKey.UpArrow or ConsoleKey.K:
                    index = (index - 1 + items.Count) % items.Count;
                    break;
                case ConsoleKey.DownArrow or ConsoleKey.J:
                    index = (index + 1) % items.Count;
                    break;
                case ConsoleKey.RightArrow or ConsoleKey.L or ConsoleKey.LeftArrow or ConsoleKey.H:
                    if (!selected.Remove(index))
                        if (selected.Count < count)
                            selected.Add(index);
                    break;
                case ConsoleKey.Enter when selected.Count == count:
                    return selected.Select(i => items[i]).ToList();
                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    static void DrawPicker<T>(Window win, IReadOnlyList<T> items, int cursor, string prompt, HashSet<int>? selected, int count, ListPickerDrawCallback<T>? custom = null, int listWidth = DefaultListWidth, Func<T, string>? groupBy = null) where T : ISelectable
    {
        int detailX = listWidth + 2;
        win.Clear();
        win.At(2, 1).Write(prompt, ConsoleColor.White);

        // Count display rows (items + group headers) to compute scroll
        int totalRows = items.Count;
        if (groupBy != null)
        {
            string? lastGroup = null;
            for (int i = 0; i < items.Count; i++)
            {
                string grp = groupBy(items[i]);
                if (grp != lastGroup) { totalRows++; lastGroup = grp; }
            }
        }

        int maxVisible = Draw.ScreenHeight - 7;

        // Find the display row of the cursor item
        int cursorRow = 0;
        {
            string? lastGroup = null;
            for (int i = 0; i < items.Count && i <= cursor; i++)
            {
                if (groupBy != null)
                {
                    string grp = groupBy(items[i]);
                    if (grp != lastGroup) { if (i <= cursor) cursorRow++; lastGroup = grp; }
                }
                if (i < cursor) cursorRow++;
            }
        }

        int scroll = 0;
        if (totalRows > maxVisible)
        {
            scroll = cursorRow - maxVisible / 2;
            scroll = Math.Clamp(scroll, 0, totalRows - maxVisible);
        }

        // Render rows
        int y = 3;
        int row = 0;
        string? prevGroup = null;
        for (int i = 0; i < items.Count && y < 3 + maxVisible; i++)
        {
            if (groupBy != null)
            {
                string grp = groupBy(items[i]);
                if (grp != prevGroup)
                {
                    prevGroup = grp;
                    if (row >= scroll && y < 3 + maxVisible)
                    {
                        win.At(2, y).Write(grp, ConsoleColor.DarkCyan);
                        y++;
                    }
                    row++;
                }
            }

            if (row >= scroll && y < 3 + maxVisible)
            {
                var style = i == cursor ? CellStyle.Reverse : CellStyle.None;
                string prefix = selected != null ? (selected.Contains(i) ? "[+] " : "[ ] ") : "";
                ConsoleColor fg = ConsoleColor.White;
                if (items[i].WhyNot != null)
                    fg = ConsoleColor.DarkYellow;
                else if (items[i].ListColor is { } lc)
                    fg = lc;
                string label = prefix + items[i].Name;
                if (label.Length > listWidth - 2)
                    label = label[..(listWidth - 3)] + "…";
                win.At(2, y).Write(label, fg, ConsoleColor.Black, style);
                y++;
            }
            row++;
        }

        if (totalRows > maxVisible)
        {
            int trackX = listWidth;
            int trackH = Math.Min(maxVisible, totalRows - scroll);
            int thumbH = Math.Max(1, trackH * maxVisible / totalRows);
            int thumbY = trackH > thumbH ? scroll * (trackH - thumbH) / (totalRows - maxVisible) : 0;
            for (int sy = 0; sy < trackH; sy++)
            {
                bool isThumb = sy >= thumbY && sy < thumbY + thumbH;
                win[trackX, 3 + sy] = new Cell('x', isThumb ? ConsoleColor.White : ConsoleColor.DarkGray, Dec: true);
            }
        }

        int paddedWidth = Math.Clamp(Draw.ScreenWidth - 10, Draw.MapWidth, 120);

        if (items.Count == 0)
        {
            win.At(detailX, 3).Write("No matches", ConsoleColor.DarkGray);
            if (custom != null)
            {
                var rhs = win.At(detailX, 5, paddedWidth - detailX - 2, Draw.ScreenHeight - 7);
                custom(rhs, default!, false);
            }
        }
        else if (custom != null)
        {
            var rhs = win.At(detailX, 2, paddedWidth - detailX - 2, Draw.ScreenHeight - 4);
            custom(rhs, items[cursor], false);
        }
        else
        {
            var current = items[cursor];
            var no = current.WhyNot;
            if (no != null)
            {
                win.At(detailX, 2).Write(no, ConsoleColor.Red);
            }
            win.At(detailX, 3).Write(current.Name, ConsoleColor.Yellow);
            if (current.Tags.Length > 0)
            {
                win.At(detailX + current.Name.Length + 5, 3).Write('(' + string.Join(", ", current.Tags) + ')', ConsoleColor.Cyan);
            }
            if (current.Subtitle != null)
            {
                RichText.Write(win, detailX, 4, paddedWidth - detailX - 2, current.Subtitle);
            }

            int descEnd = RichText.Write(win, detailX, 5, paddedWidth - detailX - 2, current.Description);

            int detailY = descEnd + 2;
            foreach (var detail in current.Details)
            {
                RichText.Write(win, detailX, detailY++, paddedWidth - detailX - 2, detail);
            }
        }

        string help = selected != null
            ? $"[↑↓] select  [←→] toggle  [Enter] confirm ({selected.Count}/{count})  [Esc] back"
            : "[↑↓/jk] select  [Enter] confirm  [/] search  [Esc] back";
        win.At(2, Draw.ScreenHeight - 2).Write(help, ConsoleColor.DarkGray);
        Draw.Blit();
    }
}
