using System.Reflection.Metadata;
using Pathhack.Core;
using Pathhack.Game;
using Pathhack.Map;

namespace Pathhack.UI;

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

    public static void Blit()
    {
        for (int y = 0; y < ScreenHeight; y++)
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

                int idx = y * ViewWidth + x;
                if (_prev[idx] == cell.Value) continue;
                _prev[idx] = cell.Value;

                var fg = cell.Value.Fg;
                var bg = cell.Value.Bg;
                if ((cell.Value.Style & CellStyle.Reverse) != 0)
                    (fg, bg) = (bg, fg);

                int fgCode = AnsiColor(fg);
                int bgCode = AnsiColor(bg) + 10;
                char ch = cell.Value.Ch;

                if ((cell.Value.Style & CellStyle.Bold) != 0)
                    Console.Write($"\x1b[{y + 1};{x + 1}H\x1b[{fgCode};{bgCode};1m{ch}\x1b[22m");
                else if (cell.Value.Dec)
                    Console.Write($"\x1b[{y + 1};{x + 1}H\x1b[{fgCode};{bgCode}m\x1b(0{ch}\x1b(B");
                else
                    Console.Write($"\x1b[{y + 1};{x + 1}H\x1b[{fgCode};{bgCode}m{ch}");
            }
        }
        Console.Write("\x1b[0m");
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
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                Pos p = new(x, y);
                bool visible = level.IsVisible(p);

                if (visible)
                {
                    IUnit? UnitAt = level.Units.FirstOrDefault(c => c.Pos == p);
                    if (UnitAt != null)
                    {
                        var glyph = UnitAt.Glyph;
                        Layers[0][x, y + MapRow] = Cell.From(UnitAt.Glyph);
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

    static ConsoleColor TileColor(TileType t) => t switch
    {
        TileType.Floor => ConsoleColor.Gray,
        TileType.Wall => ConsoleColor.Gray,
        TileType.Rock => ConsoleColor.Gray,
        TileType.Corridor => ConsoleColor.Gray,
        TileType.Door => ConsoleColor.DarkYellow,
        TileType.StairsUp => ConsoleColor.Gray,
        TileType.StairsDown => ConsoleColor.Gray,
        _ => ConsoleColor.Gray,
    };

    static Cell TileCellInner(Level level, Pos p, TileType t, DoorState door, bool isMemory, ConsoleColor memoryColor = default)
    {
        ConsoleColor fg = isMemory ? memoryColor : TileColor(t);
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
            TileType.Door => door == DoorState.Open ? 'a' : door == DoorState.Broken ? '~' : '+',
            TileType.StairsUp => '<',
            TileType.StairsDown => '>',
            TileType.BranchUp => '<',
            TileType.BranchDown => '>',
            _ => '?',
        };
        return new(ch, fg, Dec: dec);
    }

    public static void DrawCurrent(Pos? cursor = null)
    {
        if (g.CurrentLevel is { } level)
        {
            Perf.Start();
            DrawMessages();
            Perf.Stop("DrawMessages");
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

    static void DrawMessages()
    {
        if (g.Messages.Count == 0)
        {
            Layers[0].Write(0, MsgRow, new string(' ', ViewWidth));
            return;
        }

        const string more = "--More--";
        string all = string.Join(" ", g.Messages);
        g.Messages.Clear();

        while (all.Length > ViewWidth)
        {
            int cut = ViewWidth - more.Length - 1;
            int space = all.LastIndexOf(' ', cut);
            if (space <= 0) space = cut;

            Layers[0].Write(0, MsgRow, all[..space] + " " + more);
            Blit();
            Console.ReadKey(true);

            all = all[(space + 1)..];
        }

        g.Messages.Add(all);
        Layers[0].Write(0, MsgRow, all.PadRight(ViewWidth));
        Blit();
    }

    public static void ClearMessages() => g.Messages.Clear();

    static void DrawStatus(Level level)
    {
        Layers[0].Write(0, StatusRow, $"{level.Branch.Name}:{level.Depth} R:{g.CurrentRound} E:{u.Energy}".PadRight(ViewWidth));
        int nextLvl = u.CharacterLevel + 1;
        int needed = Progression.XpForLevel(nextLvl) - Progression.XpForLevel(u.CharacterLevel);
        int progress = u.XP - Progression.XpForLevel(u.CharacterLevel);
        string xpStr = Progression.HasPendingLevelUp(u) ? $"XP:{progress}/{needed}*" : $"XP:{progress}/{needed} ";
        Layers[0].Write(0, StatusRow + 1, $"HP:{u.HP.Current}/{u.HP.Max} AC:{u.GetAC()} CL:{u.CharacterLevel} {xpStr}".PadRight(ViewWidth));
    }
}
