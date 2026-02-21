using System.Runtime.InteropServices;

namespace Pathhack.UI;

public class Window(int width, int height, int x = 0, int y = 0, int z = 0, bool opaque = false)
{
    public int X = x;
    public int Y = y;
    public int Z = z;
    public int Width = width;
    public int Height = height;
    public bool Opaque = opaque;

    public bool Visible = true;

    readonly Cell?[] _cells = new Cell?[width * height];

    public Cell? this[int lx, int ly]
    {
        get => (lx >= 0 && lx < Width && ly >= 0 && ly < Height) ? _cells[ly * Width + lx] : null;
        set
        {
            if (lx >= 0 && lx < Width && ly >= 0 && ly < Height)
            {
                _cells[ly * Width + lx] = value;
                if (value.HasValue) Visible = true;
            }
        }
    }

    public void Clear(Cell? cell = null)
    {
        Array.Fill(_cells, cell);
        Visible = false;
    }

    public WindowWriter At(int x = 0, int y = 0, int? w = null, int? h = null) => new(this, new(x, y), w ?? Width - x, h ?? Height - y);
}

public struct WindowWriter(Window window, Pos origin, int width, int height)
{
    private Pos Cursor = origin;
    public void SetCursor(int x, int y) => Cursor = new(Origin.X + x, Origin.Y + y);
    readonly Pos Origin = origin;
    public readonly int Width = width;
    public readonly int Height = height;

    private void Write(string text, ConsoleColor fg, ConsoleColor bg, CellStyle style, Pos advance, char overwrite = '\0', bool transparent = false)
    {
        foreach (char c in text)
        {
            if ((!transparent || c != ' ') && Cursor.X - Origin.X < Width && Cursor.Y - Origin.Y < Height)
                window[Cursor.X, Cursor.Y] = new Cell(overwrite == '\0' ? c : overwrite, fg, bg, style);
            Cursor += advance;
        }
    }

    public void Write(string text, ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black, CellStyle style = CellStyle.None, bool transparent = false) => Write(text, fg, bg, style, Pos.E, '\0', transparent);
    public void WriteVertical(string text, ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black, CellStyle style = CellStyle.None, bool transparent = false) => Write(text, fg, bg, style, Pos.S, '\0', transparent);
    public void NewLine(bool cr = true)
    {
        if (cr)
        {
            Cursor = new(Origin.X, Cursor.Y + 1);
        }
        else
        {
            Cursor += Pos.S;
        }
    }

    public readonly void Clear() => Fill(Width, Height, Cell.Empty);

    public readonly void Fill(int w, int h, Cell cell)
    {
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                if (Cursor.X + dx - Origin.X < Width && Cursor.Y + dy - Origin.Y < Height)
                    window[Cursor.X + dx, Cursor.Y + dy] = cell;
    }

    public void WriteMulti(string text, ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black, CellStyle style = CellStyle.None, char overwrite = '\0', bool transparent = false)
    {
        int beginX = Cursor.X;
        var lines = text.Split('\n');
        int start = 0, end = lines.Length - 1;
        if (start <= end && lines[start].Trim().Length == 0) start++;
        if (start <= end && lines[end].Trim().Length == 0) end--;
        for (int i = start; i <= end; i++)
        {
            Write(lines[i], fg, bg, style, Pos.E, overwrite, transparent);
            Cursor = new(beginX, Cursor.Y + 1);
        }
    }
}

public static class WindowDecorations
{
    public static WindowWriter WithBorder(this Window window, ConsoleColor color = ConsoleColor.Gray)
    {
        for (int x = 1; x < window.Width - 1; x++)
        {
            window[x, 0] = new Cell('q', color, Dec: true);
            window[x, window.Height - 1] = new Cell('q', color, Dec: true);
        }
        for (int y = 1; y < window.Height - 1; y++)
        {
            window[0, y] = new Cell('x', color, Dec: true);
            window[window.Width - 1, y] = new Cell('x', color, Dec: true);
        }
        window[0, 0] = new Cell('l', color, Dec: true);
        window[window.Width - 1, 0] = new Cell('k', color, Dec: true);
        window[0, window.Height - 1] = new Cell('m', color, Dec: true);
        window[window.Width - 1, window.Height - 1] = new Cell('j', color, Dec: true);

        return window.At(1, 1, window.Width - 2, window.Height - 2);
    }
}

public static class WM
{
    static readonly List<Window> _windows = [];

    public static void Register(Window window)
    {
        _windows.Add(window);
        _windows.Sort((a, b) => a.Z.CompareTo(b.Z));
    }

    public static void Unregister(Window window) => _windows.Remove(window);

    public static WindowHandle CreateTransient(int width, int height, int x = 0, int y = 0, int z = 0, bool opaque = false)
    {
        Window window = new(width, height, x, y, z, opaque);
        Register(window);
        return new(window);
    }

    public static void Blit() => TerminalBackend.Blit(_windows);
}

public readonly struct WindowHandle(Window window) : IDisposable
{
    public Window Window => window;
    public void Dispose() => WM.Unregister(window);
}
