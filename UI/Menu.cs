namespace Pathhack.UI;

public enum MenuMode { None, PickOne, PickAny }
public enum LineStyle { Text, Heading, SubHeading, Item }

public class Menu<T>
{
    readonly List<(char? Letter, string Text, T? Value, LineStyle Style, char? Category, ConsoleColor? Color)> _items = [];
    readonly Dictionary<char, T?> _hidden = [];
    public int InitialPage { get; set; }
    
    public void Add(string line, LineStyle style = LineStyle.Text, ConsoleColor? color = null) => _items.Add((null, line, default, style, null, color));
    public void Add(char letter, string text, T value, char? category = null) => _items.Add((letter, text, value, LineStyle.Item, category, null));
    public void AddHidden(char letter, T? value) => _hidden[letter] = value;

    public List<T> Display(MenuMode mode = MenuMode.None)
    {
        int contentWidth = _items.Max(x => x.Style == LineStyle.Item && x.Letter.HasValue 
            ? x.Text.Length + 5
            : x.Text.Length);
        contentWidth = Math.Max(contentWidth, 30);
        int menuWidth = contentWidth + 2;

        // dNethack-style: fullscreen driven by width only, paging handles height
        int offx = Math.Max(10, Draw.ScreenWidth - menuWidth - 1);
        bool fullscreen = offx == 10 || _items.Count >= Draw.ScreenHeight - 1;

        int menuX;
        if (fullscreen)
        {
            menuX = 0;
            contentWidth = Draw.ScreenWidth - 2;
            menuWidth = Draw.ScreenWidth;
        }
        else
        {
            menuX = Draw.ScreenWidth - menuWidth - 1;
        }

        int winY = 0;
        int winH = Draw.ScreenHeight;
        int maxLines = winH - 3;
        int pages = (_items.Count + maxLines - 1) / maxLines;

        using var handle = WM.CreateTransient(Draw.ScreenWidth, winH, x: 0, y: winY, z: 5, opaque: fullscreen);
        var win = handle.Window;

        int page = InitialPage < 0 ? pages + InitialPage : InitialPage;
        HashSet<int> selected = [];

        while (true)
        {
            win.Clear();
            int firstPageSize = InitialPage < 0 ? (_items.Count - 1) % maxLines + 1 : maxLines;
            int skip = page == 0 ? 0 : firstPageSize + (page - 1) * maxLines;
            int take = page == 0 ? firstPageSize : maxLines;
            var pageItems = _items.Skip(skip).Take(take).ToList();
            int pageOffset = skip;
            
            var lines = new List<(string Text, LineStyle Style, ConsoleColor? Color)>();
            for (int i = 0; i < pageItems.Count; i++)
            {
                var (letter, text, _, style, _, color) = pageItems[i];
                if (style == LineStyle.Item && letter.HasValue)
                {
                    char sel = mode == MenuMode.PickAny && selected.Contains(pageOffset + i) ? '+' : '-';
                    lines.Add(($"{letter} {sel} {text}", style, color));
                }
                else
                {
                    lines.Add((text, style, color));
                }
            }
            
            string prompt = mode switch
            {
                MenuMode.PickAny => pages > 1 ? $"({page + 1}/{pages}) < > page, letter toggle, enter confirm" : "letter toggle, enter confirm",
                _ => pages > 1 ? $"({page + 1}/{pages}) < > page" : "(press any key)"
            };
            lines.Add((prompt, LineStyle.Text, null));

            int menuHeight = lines.Count + 1;
            if (InitialPage < 0 && page == 0)
                menuHeight = Math.Max(menuHeight, maxLines + 2);

            win.At(menuX, 0).Fill(menuWidth, menuHeight, Cell.Empty);

            int y = 0;
            foreach (var (text, style, color) in lines)
            {
                CellStyle cs = style == LineStyle.SubHeading ? CellStyle.Reverse : CellStyle.None;
                win.At(menuX + 1, y++).Write(text, fg: color ?? ConsoleColor.Gray, style: cs);
            }

            Draw.Blit();
            var key = Input.NextKey();
            
            if (key.Key == ConsoleKey.RightArrow || key.KeyChar == '>')
            {
                if (pages > 1) page = (page + 1) % pages;
                else break;
                continue;
            }
            if (key.Key == ConsoleKey.Spacebar)
            {
                if (page < pages - 1) { page++; continue; }
                else break;
            }
            if (key.Key == ConsoleKey.LeftArrow || key.KeyChar == '<' || key.KeyChar == '^'
                || (key.Key == ConsoleKey.P && key.Modifiers == ConsoleModifiers.Control))
            {
                if (pages > 1) page = (page - 1 + pages) % pages;
                continue;
            }
            
            if (mode == MenuMode.PickAny && (key.KeyChar == '.' || key.KeyChar == ','))
            {
                var selectable = _items.Select((item, i) => (item, i)).Where(x => x.item.Value != null).ToList();
                bool allSelected = selectable.All(x => selected.Contains(x.i));
                foreach (var (_, i) in selectable)
                {
                    if (allSelected) selected.Remove(i);
                    else selected.Add(i);
                }
                continue;
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                return [];
            }
            
            if (mode == MenuMode.PickAny && (key.Key == ConsoleKey.Enter || key.KeyChar == '\n'))
            {
                return selected.Select(i => _items[i].Value!).ToList();
            }
            
            char ch = key.KeyChar;
            
            if (_hidden.TryGetValue(ch, out var hiddenValue))
            {
                return [hiddenValue!];
            }
            
            int idx = _items.FindIndex(x => x.Letter == ch);
            if (idx >= 0 && _items[idx].Value != null)
            {
                if (mode == MenuMode.PickOne)
                {
                    return [_items[idx].Value!];
                }
                if (mode == MenuMode.PickAny)
                {
                    if (!selected.Remove(idx)) selected.Add(idx);
                    continue;
                }
            }
            
            if (mode == MenuMode.PickAny)
            {
                var catItems = _items.Select((item, i) => (item, i))
                    .Where(x => x.item.Category == ch && x.item.Value != null)
                    .ToList();
                if (catItems.Count > 0)
                {
                    bool allSelected = catItems.All(x => selected.Contains(x.i));
                    foreach (var (_, i) in catItems)
                    {
                        if (allSelected) selected.Remove(i);
                        else selected.Add(i);
                    }
                    continue;
                }
            }
            
            if (mode != MenuMode.PickAny) break;
        }
        return [];
    }
}

public class Menu : Menu<object>
{
    public void Add(char letter, string text) => Add(letter, text, text);
    public void Display() => Display(MenuMode.None);
}
