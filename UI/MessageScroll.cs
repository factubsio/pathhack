namespace Pathhack.UI;

public static class MessageScroll
{
    public static void Show(List<(string Text, int Round)> history)
    {
        if (history.Count == 0) return;

        using var handle = WM.CreateTransient(Draw.ScreenWidth, Draw.ScreenHeight, z: 5, opaque: true);
        var win = handle.Window;

        int visible = Draw.ScreenHeight - 2; // 1 for header, 1 for help
        int cursor = history.Count - 1;       // start at bottom

        while (true)
        {
            win.Clear();
            win.At(1, 0).Write("Message History", ConsoleColor.White, style: CellStyle.Bold);

            // Scroll window: cursor is always visible
            int scroll = Math.Clamp(cursor - visible / 2, 0, Math.Max(0, history.Count - visible));
            int end = Math.Min(scroll + visible, history.Count);

            int prevRound = -1;
            int roundParity = 0;
            for (int i = scroll; i < end; i++)
            {
                int y = 1 + (i - scroll);
                var (text, round) = history[i];

                bool newRound = round != prevRound;
                if (newRound && prevRound >= 0) roundParity ^= 1;
                prevRound = round;

                ConsoleColor fg;
                if (i == cursor)
                {
                    fg = ConsoleColor.Cyan;
                    win[0, y] = new Cell('>', ConsoleColor.Yellow);
                    win[Draw.ScreenWidth - 2, y] = new Cell('<', ConsoleColor.Yellow);
                }
                else
                {
                    fg = roundParity == 0 ? ConsoleColor.White : ConsoleColor.Gray;
                }

                string roundTag = newRound ? $"R{round,-4} " : "      ";
                win.At(1, y).Write(roundTag, ConsoleColor.DarkYellow);
                win.At(7, y).Write(text, fg);
            }

            // Scrollbar
            if (history.Count > visible)
            {
                int trackX = Draw.ScreenWidth - 1;
                int trackH = visible;
                int thumbH = Math.Max(1, trackH * visible / history.Count);
                int thumbY = (history.Count - visible) > 0
                    ? scroll * (trackH - thumbH) / (history.Count - visible)
                    : 0;
                for (int y = 0; y < trackH; y++)
                {
                    bool isThumb = y >= thumbY && y < thumbY + thumbH;
                    win[trackX, 1 + y] = new Cell('x', isThumb ? ConsoleColor.White : ConsoleColor.DarkGray, Dec: true);
                }
            }

            win.At(1, Draw.ScreenHeight - 1).Write("[j/k ↑↓] scroll  [</> PgUp/PgDn] page  [Esc] close", ConsoleColor.DarkGray);
            Draw.Blit();

            var key = Input.NextKey();
            switch (key.Key)
            {
                case ConsoleKey.K or ConsoleKey.UpArrow:
                    if (cursor > 0) cursor--;
                    break;
                case ConsoleKey.J or ConsoleKey.DownArrow:
                    if (cursor < history.Count - 1) cursor++;
                    break;
                case ConsoleKey.PageUp:
                    cursor = Math.Max(0, cursor - visible);
                    break;
                case ConsoleKey.PageDown:
                    cursor = Math.Min(history.Count - 1, cursor + visible);
                    break;
                case ConsoleKey.Home:
                    cursor = 0;
                    break;
                case ConsoleKey.End:
                    cursor = history.Count - 1;
                    break;
                case ConsoleKey.Escape or ConsoleKey.Spacebar or ConsoleKey.Enter:
                    return;
                default:
                    if (key.KeyChar == '<') cursor = Math.Max(0, cursor - visible);
                    else if (key.KeyChar == '>') cursor = Math.Min(history.Count - 1, cursor + visible);
                    break;
            }
        }
    }
}
