namespace Pathhack.UI;

public interface ISelectable
{
    string Name { get; }
    string? Subtitle => null;
    string Description { get; }
    IEnumerable<string> Details => [];
    public string? WhyNot { get; }
    string[] Tags => [];
}

public record class SimpleSelectable(string Name, string Description) : ISelectable
{
    public string? WhyNot => null;
}

public delegate bool ListPickerDrawCallback<T>(WindowWriter writer, T item, bool own);

public static class ListPicker
{
    const int ListWidth = 24;
    const int DetailX = ListWidth + 2;

    public static T? Pick<T>(IReadOnlyList<T> items, string prompt, int defaultIndex = 0, ListPickerDrawCallback<T>? custom = null) where T : class, ISelectable
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
            if (filter != null)
                visible = items.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            else
                visible = items;

            if (visible.Count > 0)
                index = Math.Clamp(index, 0, visible.Count - 1);

            DrawPicker(win, visible, index, filter != null ? $"{prompt} [/{filter}{(typing ? "▌" : "")}]" : prompt, null, 0, custom);
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
                case ConsoleKey.Enter or ConsoleKey.RightArrow or ConsoleKey.L:
                    if (visible.Count > 0 && visible[index].WhyNot == null)
                    {
                        if (custom != null)
                        {
                            int paddedWidth = Math.Clamp(Draw.ScreenWidth - 10, Draw.MapWidth, 120);
                            win.At(DetailX - 2, 0).Write("------", fg: ConsoleColor.Yellow);
                            win.At(DetailX - 2, 1).WriteVertical("||||||", fg: ConsoleColor.Yellow);
                            Draw.Blit();
                            var rhs = win.At(DetailX, 2, paddedWidth - DetailX - 2, Draw.ScreenHeight - 4);
                            bool exit = custom(rhs, visible[index], true);
                            if (exit) return null;
                        }
                        else return visible[index];
                    }
                    break;
                case ConsoleKey.Escape:
                    return null;
                default:
                    if (key.KeyChar == '/') { filter = ""; typing = true; continue; }
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

    static void DrawPicker<T>(Window win, IReadOnlyList<T> items, int cursor, string prompt, HashSet<int>? selected, int count, ListPickerDrawCallback<T>? custom = null) where T : ISelectable
    {
        win.Clear();
        win.At(2, 1).Write(prompt, ConsoleColor.White);

        int maxVisible = Draw.ScreenHeight - 7;
        int scroll = 0;
        if (items.Count > maxVisible)
        {
            scroll = cursor - maxVisible / 2;
            scroll = Math.Clamp(scroll, 0, items.Count - maxVisible);
        }
        int end = Math.Min(scroll + maxVisible, items.Count);

        for (int i = scroll; i < end; i++)
        {
            var style = i == cursor ? CellStyle.Reverse : CellStyle.None;
            string prefix = selected != null ? (selected.Contains(i) ? "[+] " : "[ ] ") : "";
            ConsoleColor fg = ConsoleColor.White;
            if (items[i].WhyNot != null)
                fg = ConsoleColor.DarkYellow;
            win.At(2, 3 + i - scroll).Write(prefix + items[i].Name, fg, ConsoleColor.Black, style);
        }

        if (items.Count > maxVisible)
        {
            int trackX = ListWidth;
            int trackH = end - scroll;
            int thumbH = Math.Max(1, trackH * maxVisible / items.Count);
            int thumbY = trackH > thumbH ? scroll * (trackH - thumbH) / (items.Count - maxVisible) : 0;
            for (int y = 0; y < trackH; y++)
            {
                bool isThumb = y >= thumbY && y < thumbY + thumbH;
                win[trackX, 3 + y] = new Cell('x', isThumb ? ConsoleColor.White : ConsoleColor.DarkGray, Dec: true);
            }
        }

        int paddedWidth = Math.Clamp(Draw.ScreenWidth - 10, Draw.MapWidth, 120);

        if (items.Count == 0)
        {
            win.At(DetailX, 3).Write("No matches", ConsoleColor.DarkGray);
        }
        else if (custom != null)
        {
            var rhs = win.At(DetailX, 2, paddedWidth - DetailX - 2, Draw.ScreenHeight - 4);
            custom(rhs, items[cursor], false);
        }
        else
        {
            var current = items[cursor];
            var no = current.WhyNot;
            if (no != null)
            {
                win.At(DetailX, 2).Write(no, ConsoleColor.Red);
            }
            win.At(DetailX, 3).Write(current.Name, ConsoleColor.Yellow);
            if (current.Tags.Length > 0)
            {
                win.At(DetailX + current.Name.Length + 5, 3).Write('(' + string.Join(", ", current.Tags) + ')', ConsoleColor.Cyan);
            }
            if (current.Subtitle != null)
            {
                RichText.Write(win, DetailX, 4, paddedWidth - DetailX - 2, current.Subtitle);
            }

            int descEnd = RichText.Write(win, DetailX, 5, paddedWidth - DetailX - 2, current.Description);

            int detailY = descEnd + 2;
            foreach (var detail in current.Details)
            {
                RichText.Write(win, DetailX, detailY++, paddedWidth - DetailX - 2, detail);
            }
        }

        string help = selected != null
            ? $"[↑↓] select  [←→] toggle  [Enter] confirm ({selected.Count}/{count})  [Esc] back"
            : "[↑↓/jk] select  [Enter] confirm  [/] search  [Esc] back";
        win.At(2, Draw.ScreenHeight - 2).Write(help, ConsoleColor.DarkGray);
        Draw.Blit();
    }
}
