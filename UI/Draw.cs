using System.Data;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text;
using Pathhack.Core;
using Pathhack.Game;
using Pathhack.Map;

namespace Pathhack.UI;

public static class TtyRec
{
    private static FileStream? _file;
    private static DateTime _startTime;

    public sealed class TtyHandle : IDisposable
    {
        public void Dispose() => Stop();
    }

    public static TtyHandle Start(string path)
    {
        _file = File.Create(path);
        _startTime = DateTime.UtcNow;
        return new();
    }

    private static int lastRoundBlitted = 0;

    public static void Write(string data)
    {
        if (_file == null) return;

        double totalSecs = lastRoundBlitted * 0.3;
        int sec = (int)totalSecs;
        int usec = (int)((totalSecs - sec) * 1_000_000);
        byte[] bytes = Encoding.UTF8.GetBytes(data);

        Span<byte> header = stackalloc byte[12];
        BitConverter.TryWriteBytes(header[0..4], sec);
        BitConverter.TryWriteBytes(header[4..8], usec);
        BitConverter.TryWriteBytes(header[8..12], bytes.Length);
        _file.Write(header);
        _file.Write(bytes);
    }

    public static void Flush()
    {
        lastRoundBlitted = g.CurrentRound + 1;
        if (_file == null) return;
    }

    public static void Stop()
    {
        _file?.Flush();
        _file?.Dispose();
        _file = null;
    }
}

[Flags]
public enum CellStyle : byte
{
    None = 0,
    Bold = 1,
    Underline = 2,
    Reverse = 4,
}

public record struct Cell(char Ch, ConsoleColor Fg = ConsoleColor.Gray, ConsoleColor Bg = ConsoleColor.Black, CellStyle Style = CellStyle.None, bool Dec = false)
{
    internal static readonly Cell Empty = new(' ');

    internal static Cell From(Glyph glyph) => new(glyph.Value, glyph.Color);
}


public class ScreenBuffer(int width, int height)
{
    public readonly int Width = width;
    public readonly int Height = height;
    readonly Cell?[] _cells = new Cell?[width * height];

    public bool FullScreen;
    public bool Ignore;

    public Cell? this[int x, int y]
    {
        get => (x >= 0 && x < Width && y >= 0 && y < Height) ? _cells[y * Width + x] : null;
        set { if (x >= 0 && x < Width && y >= 0 && y < Height) _cells[y * Width + x] = value; }
    }

    public void Clear()
    {
        Array.Clear(_cells);
        FullScreen = false;
        Ignore = false;
    }

    public void Deactivate() => Ignore = true;

    public LayerScope Activate(bool fullScreen = false)
    {
        Clear();
        FullScreen = fullScreen;
        if (fullScreen)
            Draw.Invalidate();
        return new LayerScope(this);
    }

    public void Write(int x, int y, string text, ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black, CellStyle style = CellStyle.None)
    {
        foreach (char c in text)
        {
            this[x++, y] = new Cell(c, fg, bg, style);
        }
    }

    public void Fill(int x, int y, int w, int h, Cell cell)
    {
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                this[x + dx, y + dy] = cell;
    }
}

public readonly struct LayerScope(ScreenBuffer layer) : IDisposable
{
    public void Dispose() => layer.Deactivate();
}

public static class Draw
{
    public static bool Enabled = true;

    public const int ViewWidth = 80;
    public const int ViewHeight = 21;
    public const int MsgRow = 0;
    public const int MapRow = 1;
    public const int StatusRow = MapRow + ViewHeight;
    public const int ScreenHeight = StatusRow + 2;

    public static readonly ScreenBuffer[] Layers = [
        new(ViewWidth, ScreenHeight),
        new(ViewWidth, ScreenHeight),
        new(ViewWidth, ScreenHeight),
    ];

    static readonly Cell[] _prev = new Cell[ViewWidth * ScreenHeight];

    public static ScreenBuffer Overlay => Layers[^1];

    public static void ClearOverlay() => Overlay.Clear();

    public static void AnimateBeam(Pos from, Pos to, Glyph glyph, int delayMs = 30)
    {
        var dir = (to - from).Signed;
        
        // DEC: x = vertical, q = horizontal. Unicode for diagonals.
        (char beamChar, bool dec) = (dir.X, dir.Y) switch
        {
            (0, _) => ('x', true),
            (_, 0) => ('q', true),
            (1, 1) or (-1, -1) => ('╲', false),
            _ => ('╱', false),
        };

        using var layer = Overlay.Activate();
        
        Pos p = from + dir;
        while (p != to + dir)
        {
            Overlay[p.X, p.Y + MapRow] = new Cell(beamChar, glyph.Color, Dec: dec);
            Blit();
            Thread.Sleep(delayMs);
            p += dir;
        }

        Thread.Sleep(delayMs);
        Blit();
    }

    public static void AnimateProjectile(Pos from, Pos to, Glyph glyph, int delayMs = 100)
    {
        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        Pos p = from + new Pos(dx, dy);

        using var layer = Overlay.Activate();

        while (p != to)
        {
            Overlay[p.X, p.Y + MapRow] = new Cell(glyph.Value, glyph.Color);
            Blit();
            Thread.Sleep(delayMs);
            Overlay[p.X, p.Y + MapRow] = null;
            p += new Pos(dx, dy);
        }
        Blit();
    }

    public static void AnimateFlash(IEnumerable<Pos> positions, Glyph glyph, int delayMs = 150)
    {
        using var layer = Overlay.Activate();
        foreach (var p in positions)
            Overlay[p.X, p.Y + MapRow] = new Cell(glyph.Value, glyph.Color);
        Blit();
        Thread.Sleep(delayMs);
    }

    public static void AnimateCone(Pos origin, IEnumerable<Pos> positions, Glyph frontier, Glyph mid, Glyph trail, int delayMs = 40)
    {
        using var layer = Overlay.Activate();
        
        var rings = positions
            .GroupBy(p => p.ChebyshevDist(origin))
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();
        
        for (int i = 0; i < rings.Count; i++)
        {
            // Update previous rings to trail/mid
            for (int j = 0; j < i; j++)
            {
                var glyph = (i - j) >= 2 ? trail : mid;
                foreach (var p in rings[j])
                    Overlay[p.X, p.Y + MapRow] = new Cell(glyph.Value, glyph.Color);
            }
            
            // Draw frontier
            foreach (var p in rings[i])
                Overlay[p.X, p.Y + MapRow] = new Cell(frontier.Value, frontier.Color);
            
            Blit();
            Thread.Sleep(delayMs);
        }
        
        Thread.Sleep(delayMs * 2);
    }

    public static void OverlayWrite(int x, int y, string text, ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black, CellStyle style = CellStyle.None) =>
        Overlay.Write(x, y, text, fg, bg, style);

    public static void OverlayFill(int x, int y, int w, int h) =>
        Overlay.Fill(x, y, w, h, Cell.Empty);

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

    public static int TotalBytesWritten { get; private set; } = 0;
    public static int DamagedCellCount { get; private set; } = 0;

    public static void Blit(int row = -1)
    {
        int start = row < 0 ? 0 : row;
        int end = row < 0 ? ScreenHeight : row + 1;

        for (int y = start; y < end; y++)
        {
            for (int x = 0; x < ViewWidth; x++)
            {
                Cell? cell = null;
                for (int i = Layers.Length - 1; i >= 0 && cell == null; i--)
                {
                    if (Layers[i].Ignore) continue;
                    cell = Layers[i][x, y];

                    if (Layers[i].FullScreen)
                    {
                        break;
                    }
                }
                cell ??= Cell.Empty;

                int idx = (y * ViewWidth) + x;
                if (_prev[idx] == cell.Value) continue;
                DamagedCellCount++;
                _prev[idx] = cell.Value;

                var fg = cell.Value.Fg;
                var bg = cell.Value.Bg;
                if ((cell.Value.Style & CellStyle.Reverse) != 0)
                    (fg, bg) = (bg, fg);

                int fgCode = AnsiColor(fg);
                int bgCode = AnsiColor(bg) + 10;
                char ch = cell.Value.Ch;

                string cmd;

                if ((cell.Value.Style & CellStyle.Bold) != 0)
                    cmd = $"\x1b[{y + 1};{x + 1}H\x1b[{fgCode};{bgCode};1m{ch}\x1b[22m";
                else if (cell.Value.Dec)
                    cmd = $"\x1b[{y + 1};{x + 1}H\x1b[{fgCode};{bgCode}m\x1b(0{ch}\x1b(B";
                else
                    cmd = $"\x1b[{y + 1};{x + 1}H\x1b[{fgCode};{bgCode}m{ch}";

                TotalBytesWritten += cmd.Length;
                TtyRec.Write(cmd);
                if (Enabled) Console.Write(cmd);
            }
        }
        TtyRec.Write("\x1b[0m");
        TtyRec.Flush();
        if (Enabled) Console.Write("\x1b[0m");
    }

    public static void ResetRoundStats()
    {
        TotalBytesWritten = 0;
        DamagedCellCount = 0;
    }

    internal static void Invalidate()
    {
        for (int i = 0; i < _prev.Length; i++)
            _prev[i] = new();
    }


    static bool IsWall(Level level, Pos p)
    {
        if (!level.InBounds(p)) return false;
        return level[p].Type is TileType.Wall or TileType.Door;
        // TODO: perf - room border iteration too slow
        // if (level[p].Type == TileType.Door) return true;
        // foreach (var room in level.Rooms)
        //     if (room.Border.Contains(p)) return true;
        // return false;
    }

    static char WallChar(Level level, Pos p)
    {
        bool n = IsWall(level, p + Pos.N);
        bool s = IsWall(level, p + Pos.S);
        bool e = IsWall(level, p + Pos.E);
        bool w = IsWall(level, p + Pos.W);

        return (n, s, e, w) switch
        {
            (false, false, false, false) => '0', // solid block (DEC)
            (true,  true,  false, false) => 'x', // vertical
            (false, false, true,  true)  => 'q', // horizontal
            (false, true,  true,  false) => 'l', // top-left
            (false, true,  false, true)  => 'k', // top-right
            (true,  false, true,  false) => 'm', // bottom-left
            (true,  false, false, true)  => 'j', // bottom-right
            (true,  true,  true,  false) => 't', // T-right
            (true,  true,  false, true)  => 'u', // T-left
            (false, true,  true,  true)  => 'w', // T-down
            (true,  false, true,  true)  => 'v', // T-up
            (true,  true,  true,  true)  => 'n', // cross
            (true,  false, false, false) => 'x',
            (false, true,  false, false) => 'x',
            (false, false, true,  false) => 'q',
            (false, false, false, true)  => 'q',
        };
    }

    public static void DrawLevel(Level level)
    {
        Area?[,] areaMap = new Area?[level.Width, level.Height];
        foreach (var area in level.Areas.OrderBy(a => a.ZOrder))
            foreach (var p in area.Tiles)
                areaMap[p.X, p.Y] = area;

        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                Pos p = new(x, y);
                bool visible = level.IsVisible(p);

                if (visible || p == upos)
                {
                    IUnit? unit = level.UnitAt(p);
                    if (unit != null)
                    {
                        Layers[0][x, y + MapRow] = Cell.From(unit.Glyph);
                    }
                    else
                    {
                        var area = areaMap[x, y];
                        if (!level[p].IsStructural && area != null)
                        {
                            Layers[0][x, y + MapRow] = Cell.From(area.Glyph);
                        }
                        else
                        {
                            var items = level.ItemsAt(p);
                            if (items.Count > 0)
                            {
                                var top = items[^1];
                                Layers[0][x, y + MapRow] = Cell.From(top.Def.Glyph);
                            }
                            else if (level.GetState(p)?.Feature is {} feature && feature.Id[0] != '_')
                            {
                                Layers[0][x, y + MapRow] = new('_', ConsoleColor.DarkGreen);
                            }
                            else if (level.Traps.TryGetValue(p, out var trap) && trap.PlayerSeen)
                            {
                                Layers[0][x, y + MapRow] = Cell.From(trap.Glyph);
                            }
                            else
                            {
                                Layers[0][x, y + MapRow] = TileCell(level, p);
                            }
                        }
                    }
                }
                else if (level.WasSeen(p) && level.GetMemory(p) is { } mem)
                {
                    if (mem.TopItem is { } item)
                    {
                        Layers[0][x, y + MapRow] = Cell.From(item.Def.Glyph);
                    }
                    else
                    {
                        ConsoleColor col = ConsoleColor.DarkBlue;
                        if (mem.Tile.IsStructural) col = ConsoleColor.Gray;
                        Layers[0][x, y + MapRow] = MemoryTileCell(level, p, mem, col);
                    }
                }
                else
                {
                    Layers[0][x, y + MapRow] = Cell.Empty;
                }
            }
        }
    }

    static Cell TileCell(Level level, Pos p)
    {
        Tile tile = level[p];
        DoorState door = level.GetState(p)?.Door ?? DoorState.Closed;
        return TileCellInner(level, p, tile.Type, door, isMemory: false);
    }

    static Cell MemoryTileCell(Level level, Pos p, TileMemory mem, ConsoleColor memoryColor)
    {
        return TileCellInner(level, p, mem.Tile.Type, mem.Door, isMemory: true, memoryColor);
    }

    static ConsoleColor TileColor(TileType t, DoorState door) => t switch
    {
        TileType.Floor => ConsoleColor.Gray,
        TileType.Wall => ConsoleColor.Gray,
        TileType.Rock => ConsoleColor.Gray,
        TileType.Corridor => ConsoleColor.Gray,
        TileType.Door => door switch
        {
            DoorState.Closed => ConsoleColor.DarkYellow,
            DoorState.Open => ConsoleColor.DarkYellow,
            _ => ConsoleColor.Gray,
        },
        TileType.StairsUp => ConsoleColor.Gray,
        TileType.StairsDown => ConsoleColor.Gray,
        TileType.Grass => ConsoleColor.Green,
        _ => ConsoleColor.Gray,
    };

    static Cell TileCellInner(Level level, Pos p, TileType t, DoorState door, bool isMemory, ConsoleColor memoryColor = default)
    {
        ConsoleColor fg = isMemory ? memoryColor : TileColor(t, door);
        if (t == TileType.BranchUp)
            fg = level.BranchUpTarget?.Branch.Color ?? ConsoleColor.Cyan;
        else if (t == TileType.BranchDown)
            fg = level.BranchDownTarget?.Branch.Color ?? ConsoleColor.Cyan;
        bool dec = t == TileType.Wall || t == TileType.Floor || (t == TileType.Door && door != DoorState.Closed);
        char ch = t switch
        {
            TileType.Floor => '~',
            TileType.Wall => WallChar(level, p),
            TileType.Rock => ' ',
            TileType.Corridor => '#',
            TileType.Door => door switch
            {
                DoorState.Broken => '~',
                DoorState.Open => 'a',
                _ => '+',
            },
            TileType.StairsUp => '<',
            TileType.StairsDown => '>',
            TileType.BranchUp => '<',
            TileType.BranchDown => '>',
            TileType.Grass => ',',
            _ => '?',
        };
        return new(ch, fg, Dec: dec);
    }

    private static string TopLine = "";
    private static TopLineState TopLineState = TopLineState.Empty;

    private static bool More(bool save, int col, int row)
    {
        if (save) SaveTopLine();
        Layers[0].Write(col, row, "--more--");
        Blit(0);
        Input.NextKey();
        TopLine = "";
        TopLineState = TopLineState.Empty;
        return false; //return true if skip rest (esc?)
    }

    private static int MessageViewWidth => ViewWidth; //Console.WindowWidth; - this doens't work with layers very well, need maybe tty style windows but bleh
    private static int messageWidth => MessageViewWidth - 8;
    const string messageDelimit = "   ";

    private static bool CanAppendMessage(string msg) => TopLine.Length + messageDelimit.Length + msg.Length < messageWidth;

    private static void SaveTopLine()
    {
        if (TopLine.Length > 0) g.MessageHistory.Add(TopLine);
    }
    
    public static void ClearTopLine()
    {
        SaveTopLine();
        TopLine = "";
        TopLineState = TopLineState.Empty;
        for (int x = 0; x < MessageViewWidth; x++)
            Layers[0][x, 0] = null;
        Blit(0);
    }

    internal static void RenderTopLine(string? v = null)
    {
        v ??= TopLine;

        int row = 0;
        int col = 0;
        string remaining = v;
        while (remaining.Length > 0)
        {
            int len = Math.Min(MessageViewWidth, remaining.Length);
            if (len < remaining.Length)
            {
                int space = remaining.LastIndexOf(' ', len);
                if (space > 0) len = space;
            }
            Layers[0].Write(0, row, remaining[..len].PadRight(MessageViewWidth));
            remaining = remaining[len..].TrimStart();
            col = len;
            row++;
        }

        if (row > 1)
        {
            Blit();
            More(v == null, col, row);
            DrawCurrent();
        }
        else
        {
            Blit(0);
        }
    }

    internal static void DrawMessage(string msg)
    {
        // Can we append to current line?
        if (TopLineState != TopLineState.Empty && CanAppendMessage(msg))
        {
            TopLine += messageDelimit + msg;
        }
        else
        {
            // Need fresh line - if unread content, prompt to flush it
            if (TopLineState == TopLineState.PresentMustShow) More(true, TopLine.Length, 0);

            TopLine = msg;
        }

        RenderTopLine();

        TopLineState = TopLineState.PresentMustShow;
    }


    public static void DrawCurrent(Pos? cursor = null)
    {
        if (g.CurrentLevel is { } level)
        {
            Perf.Start();
            DrawLevel(level);
            Perf.Stop("DrawLevel");
            
            if (cursor is { } c && level.InBounds(c))
            {
                var cell = Layers[0][c.X, c.Y + MapRow];
                if (cell.HasValue)
                    Layers[0][c.X, c.Y + MapRow] = cell.Value with { Style = CellStyle.Reverse };
            }
            
            Perf.Start();
            DrawStatus(level);
            Perf.Stop("DrawStatus");
            Perf.Start();
            Blit();
            Perf.Stop("Blit");
        }
    }

    static void DrawStatus(Level level)
    {
        Layers[0].Write(0, StatusRow, $"{level.Branch.Name}:{level.Depth} R:{g.CurrentRound} E:{u.Energy}".PadRight(ViewWidth));
        int nextLvl = u.CharacterLevel + 1;
        int needed = Progression.XpForLevel(nextLvl) - Progression.XpForLevel(u.CharacterLevel);
        int progress = u.XP - Progression.XpForLevel(u.CharacterLevel);
        string xpStr = Progression.HasPendingLevelUp(u) ? $"XP:{progress}/{needed}*" : $"XP:{progress}/{needed} ";
        Layers[0].Write(0, StatusRow + 1, $"HP:{u.HP.Current}/{u.HP.Max} AC:{u.GetAC()} CL:{u.CharacterLevel} {xpStr}".PadRight(ViewWidth));
        DrawSpellPips();
    }

    static void DrawSpellPips()
    {
        // const int maxSlots = 5;
        const int width = 6;
        StringBuilder sb = new();
        for (int lvl = 1; lvl <= 9; lvl++)
        {
            var pool = u.GetPool($"spell_l{lvl}");
            if (pool == null) continue;

            int left = 36 + width * (lvl - 1);

            Layers[0].Write(left, StatusRow, $"l{lvl}");
            sb.Clear();

            for (int i = 0; i < pool.Max; i++)
            {
                if (i < pool.Current)
                    sb.Append('●');
                else if (i == pool.Current && pool.Ticks >= pool.RegenRate / 2)
                    sb.Append('◐');
                else
                    sb.Append('○');
            }
            Layers[0].Write(left, StatusRow + 1, sb.ToString());
        }
    }

}

internal enum TopLineState
{
    Empty,
    PresentMustShow,
    PresentCanClear
}