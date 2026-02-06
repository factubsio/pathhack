namespace Pathhack.UI;

public enum MenuMode { None, PickOne, PickAny }
public enum LineStyle { Text, Heading, SubHeading, Item }

public class Menu<T>
{
    readonly List<(char? Letter, string Text, T? Value, LineStyle Style, char? Category)> _items = [];
    readonly Dictionary<char, T?> _hidden = [];
    public int InitialPage { get; set; }
    
    public void Add(string line, LineStyle style = LineStyle.Text) => _items.Add((null, line, default, style, null));
    public void Add(char letter, string text, T value, char? category = null) => _items.Add((letter, text, value, LineStyle.Item, category));
    public void AddHidden(char letter, T? value) => _hidden[letter] = value;

    public List<T> Display(MenuMode mode = MenuMode.None)
    {
        var layer = Draw.Overlay;
        using var _ = layer.Activate();
        
        int maxLines = Draw.MapHeight - 3;
        int pages = (_items.Count + maxLines - 1) / maxLines;
        bool fullscreen = _items.Count > maxLines;
        
        // fullscreen uses more lines (covers status)
        if (fullscreen)
        {
            maxLines = Draw.ScreenHeight - 2; // leave 1 for prompt
            pages = (_items.Count + maxLines - 1) / maxLines;
            layer.FullScreen = true;
        }

        int page = InitialPage < 0 ? pages + InitialPage : InitialPage;
        HashSet<int> selected = [];

        // calc width once from all items
        int contentWidth = _items.Max(x => x.Style == LineStyle.Item && x.Letter.HasValue 
            ? x.Text.Length + 5  // "a  - text"
            : x.Text.Length);
        contentWidth = Math.Max(contentWidth, 30); // min width for prompt
        int menuWidth = contentWidth + 2;

        // if too wide or multi-page, go fullscreen (offx=0), else right-align
        int menuX = (menuWidth >= Draw.ScreenWidth - 10 || fullscreen)
            ? 0
            : Draw.ScreenWidth - menuWidth - 1;
        
        // fullscreen uses full width
        if (menuX == 0)
        {
            contentWidth = Draw.ScreenWidth - 2;
            menuWidth = Draw.ScreenWidth;
        }

        int startY = fullscreen ? 0 : Draw.MapRow;

        while (true)
        {
            Draw.ClearOverlay();
            Draw.Overlay.FullScreen = fullscreen;
            var pageItems = _items.Skip(page * maxLines).Take(maxLines).ToList();
            int pageOffset = page * maxLines;
            
            var lines = new List<(string Text, LineStyle Style)>();
            for (int i = 0; i < pageItems.Count; i++)
            {
                var (letter, text, _, style, _) = pageItems[i];
                if (style == LineStyle.Item && letter.HasValue)
                {
                    char sel = mode == MenuMode.PickAny && selected.Contains(pageOffset + i) ? '+' : '-';
                    lines.Add(($"{letter} {sel} {text}", style));
                }
                else
                {
                    lines.Add((text, style));
                }
            }
            
            string prompt = mode switch
            {
                MenuMode.PickAny => pages > 1 ? $"({page + 1}/{pages}) < > page, letter toggle, enter confirm" : "letter toggle, enter confirm",
                _ => pages > 1 ? $"({page + 1}/{pages}) < > page" : "(press any key)"
            };
            lines.Add((prompt, LineStyle.Text));

            int menuHeight = lines.Count + 2;
            Draw.OverlayFill(menuX, startY, menuWidth, menuHeight);

            int y = startY + 1;
            foreach (var (text, style) in lines)
            {
                if (style == LineStyle.SubHeading)
                {
                    Draw.OverlayWrite(menuX + 1, y++, text, style: CellStyle.Reverse);
                }
                else
                {
                    Draw.OverlayWrite(menuX + 1, y++, text);
                }
            }

            Draw.Blit();
            var key = Input.NextKey();
            
            // paging
            if (key.Key == ConsoleKey.RightArrow || key.KeyChar == '>' || key.Key == ConsoleKey.Spacebar)
            {
                if (pages > 1) page = (page + 1) % pages;
                else break;
                continue;
            }
            if (key.Key == ConsoleKey.LeftArrow || key.KeyChar == '<' || key.KeyChar == '^'
                || (key.Key == ConsoleKey.P && key.Modifiers == ConsoleModifiers.Control))
            {
                if (pages > 1) page = (page - 1 + pages) % pages;
                continue;
            }
            
            // select all in PickAny
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
            
            // confirm for PickAny
            if (mode == MenuMode.PickAny && (key.Key == ConsoleKey.Enter || key.KeyChar == '\n'))
            {
                return selected.Select(i => _items[i].Value!).ToList();
            }
            
            // letter selection
            char ch = key.KeyChar;
            
            // hidden hotkeys
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
            
            // category toggle in PickAny
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
            
            // any other key exits for None/PickOne
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