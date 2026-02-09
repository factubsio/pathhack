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

public static class ListPicker
{
    const int ListWidth = 24;
    const int DetailX = ListWidth + 2;

    public static T? Pick<T>(IReadOnlyList<T> items, string prompt, int defaultIndex = 0) where T : class, ISelectable
    {
        if (items.Count == 0) return null;
        
        var layer = Draw.Layers[2];
        using var _ = layer.Activate(fullScreen: true);
        
        int index = Math.Clamp(defaultIndex, 0, items.Count - 1);
        while (true)
        {
            DrawPicker(layer, items, index, prompt, null, 0);
            var key = Input.NextKey();
            switch (key.Key)
            {
                case ConsoleKey.UpArrow or ConsoleKey.K:
                    index = (index - 1 + items.Count) % items.Count;
                    break;
                case ConsoleKey.DownArrow or ConsoleKey.J:
                    index = (index + 1) % items.Count;
                    break;
                case ConsoleKey.Enter:
                    if (items[index].WhyNot == null) return items[index];
                    break;
                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    public static List<T>? PickMultiple<T>(IReadOnlyList<T> items, string prompt, int count) where T : class, ISelectable
    {
        var layer = Draw.Layers[2];
        using var _ = layer.Activate(fullScreen: true);
        
        int index = 0;
        HashSet<int> selected = [];
        while (true)
        {
            DrawPicker(layer, items, index, prompt, selected, count);
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

    static void DrawPicker<T>(ScreenBuffer layer, IReadOnlyList<T> items, int cursor, string prompt, HashSet<int>? selected, int count) where T : ISelectable
    {
        layer.Clear();
        layer.FullScreen = true;
        layer.Write(2, 1, prompt, ConsoleColor.White);

        for (int i = 0; i < items.Count; i++)
        {
            var style = i == cursor ? CellStyle.Reverse : CellStyle.None;
            string prefix = selected != null ? (selected.Contains(i) ? "[+] " : "[ ] ") : "";
            ConsoleColor fg = ConsoleColor.White;
            if (items[i].WhyNot != null)
                fg = ConsoleColor.DarkYellow;
            layer.Write(2, 3 + i, prefix + items[i].Name, fg, ConsoleColor.Black, style);
        }

        // feels weird when it is mega wide, but has to be at least map wide?
        int paddedWidth = Math.Clamp(Draw.ScreenWidth - 10, Draw.MapWidth, 120);

        var current = items[cursor];
        var no = current.WhyNot;
        if (no != null)
        {
            layer.Write(DetailX, 2, no, ConsoleColor.Red);
        }
        layer.Write(DetailX, 3, current.Name, ConsoleColor.Yellow);
        if (current.Tags.Length > 0)
        {
            layer.Write(DetailX + current.Name.Length + 5, 3, '(' + string.Join(", ", current.Tags) + ')', ConsoleColor.Cyan);
        }
        if (current.Subtitle != null)
        {
            RichText.Write(layer, DetailX, 4, paddedWidth - DetailX - 2, current.Subtitle);
        }

        int descEnd = RichText.Write(layer, DetailX, 5, paddedWidth - DetailX - 2, current.Description);

        int detailY = descEnd + 2;
        foreach (var detail in current.Details)
        {
            RichText.Write(layer, DetailX, detailY++, paddedWidth - DetailX - 2, detail);
        }

        string help = selected != null
            ? $"[↑↓] select  [←→] toggle  [Enter] confirm ({selected.Count}/{count})  [Esc] back"
            : "[↑↓/jk] select  [Enter] confirm  [Esc] back";
        layer.Write(2, Draw.ScreenHeight - 2, help, ConsoleColor.DarkGray);
        Draw.Blit();
    }
}
