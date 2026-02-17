namespace Pathhack.UI;

/// <summary>
/// NHW_TEXT equivalent: paged rich-text display with corner-vs-fullscreen sizing.
/// No selection, no letters — just text and space-to-page.
/// </summary>
public class TextMenu
{
    readonly List<string> _lines = [];
    public int InitialPage { get; set; }

    public void Add(string line) => _lines.Add(line);
    public void Add(string line, ConsoleColor color) => _lines.Add($"[fg={color}]{line}[/]");
    public void Add() => _lines.Add("");
    public void AddHeading(string line) => _lines.Add($"[b]{line}[/]");
    public void AddSubHeading(string line) => _lines.Add($"[r]{line}[/]");

    public void Display()
    {
        if (_lines.Count == 0) return;

        // Measure natural content width (longest line, tags stripped)
        int naturalWidth = 0;
        foreach (var line in _lines)
        {
            int w = RichText.Measure(line);
            if (w > naturalWidth) naturalWidth = w;
        }
        naturalWidth += 2; // padding

        // Corner vs fullscreen: same logic as dNethack
        // offx = max(10, cols - maxcol - 1); if offx == 10 || rows overflow → fullscreen
        int offx = Math.Max(10, Draw.ScreenWidth - naturalWidth - 1);
        bool fullscreen = offx == 10 || _lines.Count >= Draw.ScreenHeight - 1;

        int contentWidth, menuX;
        if (fullscreen)
        {
            contentWidth = Draw.ScreenWidth - 2;
            menuX = 0;
        }
        else
        {
            contentWidth = naturalWidth;
            menuX = Draw.ScreenWidth - naturalWidth - 1;
        }

        int maxLines = Draw.ScreenHeight - 2; // room for "more" prompt

        using var handle = WM.CreateTransient(contentWidth, Draw.ScreenHeight, x: menuX, y: 0, z: 5, opaque: fullscreen);
        var win = handle.Window;

        // Pre-calculate page breaks
        List<int> pageStarts = [0];
        {
            int y = 0;
            for (int i = 0; i < _lines.Count; i++)
            {
                int lineH = RichText.MeasureHeight(1, y, contentWidth, _lines[i]);
                if (y + lineH >= maxLines && y > 0)
                {
                    pageStarts.Add(i);
                    y = 0;
                }
                y += lineH + 1;
            }
        }

        int page = InitialPage < 0 ? Math.Max(0, pageStarts.Count + InitialPage) : Math.Clamp(InitialPage, 0, pageStarts.Count - 1);

        while (true)
        {
            win.Clear(Cell.Empty);
            var content = win.At(1,0);
            int start = pageStarts[page];
            int end = page + 1 < pageStarts.Count ? pageStarts[page + 1] : _lines.Count;
            int y = 0;
            for (int i = start; i < end; i++)
            {
                y = RichText.Write(win, 1, y, contentWidth, _lines[i]) + 1;
            }

            bool lastPage = page >= pageStarts.Count - 1;
            string prompt = pageStarts.Count > 1
                ? $"({page + 1}/{pageStarts.Count}) {(lastPage ? "(end)" : "(more)")}"
                : "(end)";
            win.At(1, Draw.ScreenHeight - 1).Write(prompt, ConsoleColor.DarkGray);

            Draw.Blit();
            var key = Input.NextKey();
            if (key.Key == ConsoleKey.Escape) return;
            if (key.Key == ConsoleKey.Spacebar || key.Key == ConsoleKey.Enter)
            {
                if (lastPage) return;
                page++;
            }
            else if (key.KeyChar == '<' || key.Key == ConsoleKey.LeftArrow)
            {
                if (page > 0) page--;
            }
            else if (key.KeyChar == '>' || key.Key == ConsoleKey.RightArrow)
            {
                if (!lastPage) page++;
            }
        }
    }
}
