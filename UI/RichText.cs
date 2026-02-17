namespace Pathhack.UI;

public static class RichText
{
    record struct State(ConsoleColor Fg, ConsoleColor Bg, CellStyle Style);

    public static int Write(Window buf, int x, int y, int maxWidth, string text, ConsoleColor defaultFg = ConsoleColor.Gray, ConsoleColor defaultBg = ConsoleColor.Black)
    {
        Stack<State> stack = [];
        var fg = defaultFg;
        var bg = defaultBg;
        var style = CellStyle.None;
        int cx = x;
        int cy = y;
        int lineEnd = x + maxWidth;

        var tokens = Tokenize(text);
        foreach (var token in tokens)
        {
            if (token.IsTag)
            {
                TryApplyTag(token.Text, stack, ref fg, ref bg, ref style);
                continue;
            }

            if (token.Text == "\n")
            {
                cx = x;
                cy++;
                continue;
            }

            bool isSpace = token.Text.Length > 0 && char.IsWhiteSpace(token.Text[0]);
            bool isWord = !isSpace;
            
            if (isWord && cx > x && cx + token.Text.Length > lineEnd)
            {
                cx = x;
                cy++;
            }

            if (isSpace && cx == x)
                continue; // trim leading whitespace on line

            foreach (char c in token.Text)
            {
                if (cx >= lineEnd)
                {
                    cx = x;
                    cy++;
                }
                if (char.IsWhiteSpace(c) && cx == x)
                    continue; // trim leading whitespace on line
                buf[cx, cy] = new Cell(c, fg, bg, style);
                cx++;
            }
        }
        return cy;
    }

    record struct Token(string Text, bool IsTag);

    static List<Token> Tokenize(string text)
    {
        List<Token> tokens = [];
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '[')
            {
                int close = text.IndexOf(']', i);
                if (close > i)
                {
                    tokens.Add(new(text[(i + 1)..close], true));
                    i = close + 1;
                    continue;
                }
            }

            if (text[i] == '\n')
            {
                tokens.Add(new("\n", false));
                i++;
                continue;
            }

            if (char.IsWhiteSpace(text[i]))
            {
                tokens.Add(new(text[i].ToString(), false));
                i++;
                continue;
            }

            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '[')
                i++;
            tokens.Add(new(text[start..i], false));
        }
        return tokens;
    }

    static bool TryApplyTag(string tag, Stack<State> stack, ref ConsoleColor fg, ref ConsoleColor bg, ref CellStyle style)
    {
        if (tag.StartsWith('/'))
        {
            if (stack.Count > 0)
            {
                var prev = stack.Pop();
                fg = prev.Fg;
                bg = prev.Bg;
                style = prev.Style;
            }
            return true;
        }

        stack.Push(new(fg, bg, style));

        if (tag.StartsWith("fg=") && TryParseColor(tag[3..], out var fgc))
        {
            fg = fgc;
            return true;
        }
        if (tag.StartsWith("bg=") && TryParseColor(tag[3..], out var bgc))
        {
            bg = bgc;
            return true;
        }
        if (tag == "b") { style |= CellStyle.Bold; return true; }
        if (tag == "r") { style |= CellStyle.Reverse; return true; }

        stack.Pop();
        return false;
    }

    static bool TryParseColor(string name, out ConsoleColor color)
    {
        color = name.ToLowerInvariant() switch
        {
            "black" => ConsoleColor.Black,
            "darkblue" => ConsoleColor.DarkBlue,
            "darkgreen" => ConsoleColor.DarkGreen,
            "darkcyan" => ConsoleColor.DarkCyan,
            "darkred" => ConsoleColor.DarkRed,
            "darkmagenta" => ConsoleColor.DarkMagenta,
            "darkyellow" => ConsoleColor.DarkYellow,
            "gray" => ConsoleColor.Gray,
            "darkgray" => ConsoleColor.DarkGray,
            "blue" => ConsoleColor.Blue,
            "green" => ConsoleColor.Green,
            "cyan" => ConsoleColor.Cyan,
            "red" => ConsoleColor.Red,
            "magenta" => ConsoleColor.Magenta,
            "yellow" => ConsoleColor.Yellow,
            "white" => ConsoleColor.White,
            _ => ConsoleColor.Gray
        };
        return name.ToLowerInvariant() is "black" or "darkblue" or "darkgreen" or "darkcyan" or "darkred" or "darkmagenta" or "darkyellow" or "gray" or "darkgray" or "blue" or "green" or "cyan" or "red" or "magenta" or "yellow" or "white";
    }

    /// <summary>Returns visible character count of the longest sub-line (tags stripped, newline-aware).</summary>
    public static int Measure(string text)
    {
        int max = 0, cur = 0;
        foreach (var token in Tokenize(text))
        {
            if (token.IsTag) continue;
            if (token.Text == "\n") { if (cur > max) max = cur; cur = 0; continue; }
            cur += token.Text.Length;
        }
        return Math.Max(max, cur);
    }

    /// <summary>Returns how many rows Write() would consume, without actually writing.</summary>
    public static int MeasureHeight(int x, int startY, int maxWidth, string text)
    {
        int cx = x, cy = startY;
        int lineEnd = x + maxWidth;
        foreach (var token in Tokenize(text))
        {
            if (token.IsTag) continue;
            if (token.Text == "\n") { cx = x; cy++; continue; }
            bool isWord = token.Text.Length > 0 && !char.IsWhiteSpace(token.Text[0]);
            if (isWord && cx > x && cx + token.Text.Length > lineEnd) { cx = x; cy++; }
            if (char.IsWhiteSpace(token.Text[0]) && cx == x) continue;
            foreach (char c in token.Text)
            {
                if (cx >= lineEnd) { cx = x; cy++; }
                if (char.IsWhiteSpace(c) && cx == x) continue;
                cx++;
            }
        }
        return cy - startY + 1;
    }
}
