using System.Text;

namespace Pathhack.UI;

public static class Compositor
{
    static readonly Cell[] _prev = new Cell[TerminalBackend.Width * TerminalBackend.Height];

    public static void Composite(List<Window> windows, CellEncoder encoder)
    {
        for (int sy = 0; sy < TerminalBackend.Height; sy++)
        {
            for (int sx = 0; sx < TerminalBackend.Width; sx++)
            {
                Cell? cell = null;
                for (int i = windows.Count - 1; i >= 0 && cell == null; i--)
                {
                    Window w = windows[i];
                    if (!w.Visible) continue;
                    int lx = sx - w.X;
                    int ly = sy - w.Y;
                    if (lx >= 0 && lx < w.Width && ly >= 0 && ly < w.Height)
                    {
                        cell = w[lx, ly];
                        if (w.Opaque) break;
                    }
                }

                int idx = sy * TerminalBackend.Width + sx;
                Cell resolved = cell ?? Cell.Empty;
                if (_prev[idx] == resolved) continue;
                _prev[idx] = resolved;
                encoder.Emit(sx, sy, resolved);
            }
        }
    }

    public static void Invalidate()
    {
        for (int i = 0; i < _prev.Length; i++)
            _prev[i] = new();
    }
}

public static class TerminalBackend
{
    public static readonly int Width = Console.WindowWidth;
    public static readonly int Height = Console.WindowHeight;

    static readonly CellEncoder _encoder = new();
    public static int BytesWritten { get; private set; }
    public static int DamagedCells { get; private set; }

    public static void Blit(List<Window> windows)
    {
        _encoder.Reset();
        Compositor.Composite(windows, _encoder);
        _encoder.Finish();
        _encoder.CopyTo(Console.Out);
        BytesWritten += _encoder.Length;
        DamagedCells += _encoder.CellCount;
    }

    public static void ResetStats() { BytesWritten = 0; DamagedCells = 0; }
}

public static class DebugBackend
{
    public static void Render(Window window)
    {
        CellEncoder enc = new();
        for (int ly = 0; ly < window.Height; ly++)
            for (int lx = 0; lx < window.Width; lx++)
                enc.Emit(window.X + lx, window.Y + ly, window[lx, ly] ?? Cell.Empty);
        enc.Finish();
        enc.CopyTo(Console.Out);
    }
}

public class CellEncoder
{
    readonly StringBuilder _buf = new();
    int _lastX = -1, _lastY = -1;
    int _lastFg = -1, _lastBg = -1;
    bool _inDec;
    bool _inBold;
    bool _inReverse;

    public int Length => _buf.Length;
    public int CellCount { get; private set; }

    public void CopyTo(TextWriter writer)
    {
        foreach (var chunk in _buf.GetChunks())
            writer.Write(chunk.Span);
    }

    public void Reset()
    {
        _buf.Clear();
        CellCount = 0;
        _lastX = _lastY = -1;
        _lastFg = _lastBg = -1;
        _inDec = false;
        _inBold = false;
        _inReverse = false;
    }

    public void Emit(int x, int y, Cell cell)
    {
        int fgCode = AnsiColor(cell.Fg);
        int bgCode = AnsiColor(cell.Bg) + 10;

        // Cursor positioning — skip if we're already at the right spot
        if (y != _lastY || x != _lastX)
            _buf.Append($"\x1b[{y + 1};{x + 1}H");

        // Color — skip if unchanged
        if (fgCode != _lastFg || bgCode != _lastBg)
        {
            _buf.Append($"\x1b[{fgCode};{bgCode}m");
            _lastFg = fgCode;
            _lastBg = bgCode;
        }

        // Bold
        bool wantBold = cell.Style.HasFlag(CellStyle.Bold);
        if (wantBold && !_inBold) { _buf.Append("\x1b[1m"); _inBold = true; }
        else if (!wantBold && _inBold) { _buf.Append("\x1b[22m"); _inBold = false; }

        // Reverse
        bool wantReverse = cell.Style.HasFlag(CellStyle.Reverse);
        if (wantReverse && !_inReverse) { _buf.Append("\x1b[7m"); _inReverse = true; }
        else if (!wantReverse && _inReverse) { _buf.Append("\x1b[27m"); _inReverse = false; }

        // DEC line drawing mode
        if (cell.Dec && !_inDec) { _buf.Append("\x1b(0"); _inDec = true; }
        else if (!cell.Dec && _inDec) { _buf.Append("\x1b(B"); _inDec = false; }

        _buf.Append(cell.Ch);
        _lastX = x + 1;
        _lastY = y;
        CellCount++;
    }

    public void Finish()
    {
        if (_inDec) _buf.Append("\x1b(B");
        _buf.Append("\x1b[0m");
        _lastX = _lastY = -1;
        _lastFg = _lastBg = -1;
        _inDec = _inBold = _inReverse = false;
    }

    static int AnsiColor(ConsoleColor c) => c switch
    {
        ConsoleColor.Black => 30,
        ConsoleColor.DarkRed => 31,
        ConsoleColor.DarkGreen => 32,
        ConsoleColor.DarkYellow => 33,
        ConsoleColor.DarkBlue => 34,
        ConsoleColor.DarkMagenta => 35,
        ConsoleColor.DarkCyan => 36,
        ConsoleColor.Gray => 37,
        ConsoleColor.DarkGray => 90,
        ConsoleColor.Red => 91,
        ConsoleColor.Green => 92,
        ConsoleColor.Yellow => 93,
        ConsoleColor.Blue => 94,
        ConsoleColor.Magenta => 95,
        ConsoleColor.Cyan => 96,
        ConsoleColor.White => 97,
        _ => 37,
    };
}
