using System.Data;
using System.Text;

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

    internal static Cell From(Glyph glyph) => new(glyph.Value, glyph.Color, glyph.Background ?? ConsoleColor.Black, (CellStyle)glyph.Flags);
}

public static class Draw
{
    public static bool Enabled = true;

    public static readonly int ScreenWidth = TerminalBackend.Width;
    public static readonly int ScreenHeight = TerminalBackend.Height;

    public const int MapWidth = 80;
    public const int MapHeight = 21;

    // Well-known windows
    public static readonly Window MessageWin = new(ScreenWidth, 4, x: 0, y: 0, z: 0);
    public static readonly Window MapWin = new(MapWidth, MapHeight, x: 0, y: 1, z: 0, opaque: true);
    // How many status lines fit: 2 base + up to 2 extra
    public static readonly int StatusHeight = Math.Min(4, Math.Max(2, ScreenHeight - (1 + MapHeight)));
    public static readonly Window StatusWin = new(ScreenWidth, StatusHeight, x: 0, y: 1 + MapHeight, z: 0, opaque: true);

    public static void Init()
    {
        WM.Register(MessageWin);
        WM.Register(MapWin);
        WM.Register(StatusWin);
    }

    public static void AnimateBeam(Pos from, Pos to, Glyph glyph, int delayMs = 30, bool pulse = false)
    {
        var dir = (to - from).Signed;

        (char beamChar, bool dec) = (dir.X, dir.Y) switch
        {
            (0, _) => ('x', true),
            (_, 0) => ('q', true),
            (1, 1) or (-1, -1) => ('╲', false),
            _ => ('╱', false),
        };

        using var handle = WM.CreateTransient(MapWidth, MapHeight, x: 0, y: 1, z: 10);
        var ov = handle.Window;

        if (pulse)
        {
            Pos p = from + dir;
            while (p != to + dir)
            {
                if (lvl.IsVisible(p))
                    ov[p.X, p.Y] = new Cell(beamChar, glyph.Color, Dec: dec);
                p += dir;
            }
            Blit();
            Thread.Sleep(delayMs * 3);

            p = from + dir;
            while (p != to + dir)
            {
                if (lvl.IsVisible(p))
                    ov[p.X, p.Y] = Cell.Empty;
                Blit();
                Thread.Sleep(delayMs);
                p += dir;
            }
        }
        else
        {
            Pos p = from + dir;
            while (p != to + dir)
            {
                if (lvl.IsVisible(p))
                    ov[p.X, p.Y] = new Cell(beamChar, glyph.Color, Dec: dec);
                Blit();
                Thread.Sleep(delayMs);
                p += dir;
            }
        }

        Thread.Sleep(delayMs);
        Blit();
    }

    public static void AnimateProjectile(Pos from, Pos to, Glyph glyph, int delayMs = -1, int total = 150)
    {
        if (from.X < 0 || to.X < 0) return;

        if (delayMs < 0)
        {
            int frames = to.ChebyshevDist(from);
            if (frames <= 0) return;
            delayMs = total / frames;
        }

        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        Pos p = from + new Pos(dx, dy);

        using var handle = WM.CreateTransient(MapWidth, MapHeight, x: 0, y: 1, z: 10);
        var ov = handle.Window;

        while (p != to)
        {
            if (lvl.IsVisible(p))
            {
                ov[p.X, p.Y] = new Cell(glyph.Value, glyph.Color);
                Blit();
                Thread.Sleep(delayMs);
                ov[p.X, p.Y] = null;
            }
            p += new Pos(dx, dy);
        }
        Blit();
    }

    public static void AnimateFlash(IEnumerable<Pos> positions, Glyph glyph, int delayMs = 150)
    {
        using var handle = WM.CreateTransient(MapWidth, MapHeight, x: 0, y: 1, z: 10);
        var ov = handle.Window;
        foreach (var p in positions)
            if (lvl.IsVisible(p))
                ov[p.X, p.Y] = new Cell(glyph.Value, glyph.Color);
        Blit();
        Thread.Sleep(delayMs);
    }

    public static void AnimateCone(Pos origin, IEnumerable<Pos> positions, Glyph frontier, Glyph mid, Glyph trail, int delayMs = 40)
    {
        using var handle = WM.CreateTransient(MapWidth, MapHeight, x: 0, y: 1, z: 10);
        var ov = handle.Window;

        var rings = positions
            .GroupBy(p => p.ChebyshevDist(origin))
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();

        for (int i = 0; i < rings.Count; i++)
        {
            for (int j = 0; j < i; j++)
            {
                var g2 = (i - j) >= 2 ? trail : mid;
                foreach (var p in rings[j])
                    if (lvl.IsVisible(p))
                        ov[p.X, p.Y] = new Cell(g2.Value, g2.Color);
            }

            foreach (var p in rings[i])
                if (lvl.IsVisible(p))
                    ov[p.X, p.Y] = new Cell(frontier.Value, frontier.Color);

            Blit();
            Thread.Sleep(delayMs);
        }

        Thread.Sleep(delayMs * 2);
    }

    public static int TotalBytesWritten => TerminalBackend.BytesWritten;
    public static int DamagedCellCount => TerminalBackend.DamagedCells;

    public static void Blit()
    {
        WM.Blit();
        TtyRec.Flush();
    }

    public static void ResetRoundStats() => TerminalBackend.ResetStats();

    public static void Invalidate() => Compositor.Invalidate();

    public static void DrawLevel(Level level)
    {
        Area?[,] areaMap = new Area?[level.Width, level.Height];
        foreach (var area in level.AllAreas.OrderBy(a => a.ZOrder))
            foreach (var p in area.Tiles)
                areaMap[p.X, p.Y] = area;

        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                Pos p = new(x, y);

                if (level.UnitAt(p) is Monster m && !m.IsPlayer && !m.HiddenFromRender)
                {
                    switch (m.Perception)
                    {
                        case PlayerPerception.Visible:
                            MapWin[x, y] = Cell.From(m.Glyph);
                            continue;
                        case PlayerPerception.Detected:
                        case PlayerPerception.Warned:
                            MapWin[x, y] = Cell.From(m.Glyph);
                            continue;
                        case PlayerPerception.Unease:
                            int warnLevel = Math.Clamp(m.EffectiveLevel / 4, 1, 5);
                            MapWin[x, y] = new((char)('0' + warnLevel), ConsoleColor.Magenta);
                            continue;
                        case PlayerPerception.Guess:
                            MapWin[x, y] = new('?', ConsoleColor.DarkMagenta);
                            continue;
                    }
                }

                bool visible = level.IsVisible(p);

                if (visible || p == upos)
                {
                    IUnit? unit = level.UnitAt(p);
                    if (unit != null)
                    {
                        MapWin[x, y] = Cell.From(unit.Glyph);
                    }
                    else
                    {
                        var area = areaMap[x, y];
                        if (!level[p].IsStructural && area != null)
                        {
                            MapWin[x, y] = Cell.From(area.Glyph);
                        }
                        else if (level[p].IsStairs)
                        {
                            var items = level.ItemsAt(p);
                            var cell = TileCell(level, p);
                            if (items.Count > 0)
                                cell = cell with { Style = CellStyle.Reverse };
                            MapWin[x, y] = cell;
                        }
                        else
                        {
                            var items = level.ItemsAt(p);
                            if (items.Count > 0 && level[p].Type != TileType.Water)
                            {
                                var top = items[^1];
                                MapWin[x, y] = Cell.From(top.Glyph);
                            }
                            else if (level.GetState(p)?.Feature is {} feature && !feature.Hidden)
                            {
                                MapWin[x, y] = feature.Glyph is { } g ? Cell.From(g) : new('_', ConsoleColor.DarkGreen);
                            }
                            else if (level.Traps.TryGetValue(p, out var trap) && trap.PlayerSeen)
                            {
                                MapWin[x, y] = Cell.From(trap.Glyph);
                            }
                            else
                            {
                                MapWin[x, y] = TileCell(level, p);
                            }
                        }
                    }
                }
                else if (level.WasSeen(p) && level.GetMemory(p) is { } mem)
                {
                    ConsoleColor col = ConsoleColor.DarkBlue;
                    if (mem.Tile.Type == TileType.Wall) col = level.WallColor ?? ConsoleColor.Gray;
                    else if (mem.Tile.Type == TileType.Tree) col = ConsoleColor.DarkGreen;
                    else if (mem.Tile.Type == TileType.Grass) col = ConsoleColor.DarkGreen;
                    Cell cell;
                    if (mem.TopItem is { } item && !mem.Tile.IsStairs && mem.Tile.Type != TileType.Water)
                        cell = new(item.Glyph.Value, item.Glyph.Color);
                    else if (mem.Trap is { } trap)
                        cell = new(trap.Glyph.Value, trap.Glyph.Color);
                    else if (mem.Feature is { } feature && !feature.Hidden && feature.Glyph is { } fg)
                        cell = new(fg.Value, fg.Color);
                    else
                    {
                        cell = MemoryTileCell(level, p, mem, col);
                        if (mem.Tile.IsStairs && mem.TopItem != null)
                            cell = cell with { Style = CellStyle.Reverse };
                    }
                    MapWin[x, y] = cell;
                }
                else
                {
                    MapWin[x, y] = Cell.Empty;
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
        bool realColor = mem.Tile.IsStairs || mem.Tile.Type == TileType.Door;
        return TileCellInner(level, p, mem.Tile.Type, mem.Door, isMemory: !realColor, memoryColor);
    }

    static ConsoleColor Dim(ConsoleColor c) => c switch
    {
        ConsoleColor.Gray => ConsoleColor.DarkGray,
        ConsoleColor.White => ConsoleColor.Gray,
        ConsoleColor.Red => ConsoleColor.DarkRed,
        ConsoleColor.Green => ConsoleColor.DarkGreen,
        ConsoleColor.Blue => ConsoleColor.DarkBlue,
        ConsoleColor.Yellow => ConsoleColor.DarkYellow,
        ConsoleColor.Cyan => ConsoleColor.DarkCyan,
        ConsoleColor.Magenta => ConsoleColor.DarkMagenta,
        ConsoleColor.DarkGray => ConsoleColor.Black,
        _ => c,
    };

    internal static ConsoleColor TileColor(Level level, TileType t, DoorState door) => t switch
    {
        TileType.Floor => level.FloorColor ?? ConsoleColor.Gray,
        TileType.Wall => level.WallColor ?? ConsoleColor.Gray,
        TileType.Rock => ConsoleColor.Gray,
        TileType.Corridor => level.FloorColor ?? ConsoleColor.Gray,
        TileType.Door => door switch
        {
            DoorState.Closed => ConsoleColor.DarkYellow,
            DoorState.Open => ConsoleColor.DarkYellow,
            _ => ConsoleColor.Gray,
        },
        TileType.StairsUp => ConsoleColor.Gray,
        TileType.StairsDown => ConsoleColor.Gray,
        TileType.Grass => ConsoleColor.Green,
        TileType.Tree => ConsoleColor.DarkGreen,
        TileType.Pool => ConsoleColor.Blue,
        TileType.Water => ConsoleColor.Blue,
        _ => ConsoleColor.Gray,
    };

    static Cell TileCellInner(Level level, Pos p, TileType t, DoorState door, bool isMemory, ConsoleColor memoryColor = default)
    {
        ConsoleColor fg = isMemory ? memoryColor : TileColor(level, t, door);
        if (t == TileType.BranchUp)
            fg = level.BranchUpTarget?.Branch.Color ?? ConsoleColor.Cyan;
        else if (t == TileType.BranchDown)
            fg = level.BranchDownTarget?.Branch.Color ?? ConsoleColor.Cyan;
        bool dec = t == TileType.Wall || t == TileType.Floor || t == TileType.Grass || (t == TileType.Door && door != DoorState.Closed);
        char ch = t switch
        {
            TileType.Floor => '~',
            TileType.Wall => level[p].WallCh != '\0' ? level[p].WallCh : '0',
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
            TileType.Grass => '~',
            TileType.Tree => '±',
            TileType.Pool => '~',
            TileType.Water => '≈',
            _ => '?',
        };
        return new(ch, fg, Dec: dec);
    }

    private static string TopLine = "";
    private static TopLineState TopLineState = TopLineState.Empty;

    public static bool More(bool save, int col, int row)
    {
        if (save) SaveTopLine();
        if (!PHMonitor.Active)
        {
            MessageWin.At(col, row).Write("--more--");
            Blit();
            while (Input.NextKey().Key != ConsoleKey.Spacebar)
                ;
        }
        TopLine = "";
        TopLineState = TopLineState.Empty;
        return false;
    }

    private static int MessageWidth => ScreenWidth - 8;
    const string messageDelimit = "   ";

    private static bool CanAppendMessage(string msg) => TopLine.Length + messageDelimit.Length + msg.Length < MessageWidth;

    private static void SaveTopLine()
    {
        if (TopLine.Length > 0) g.MessageHistory.Add((TopLine, g.CurrentRound));
    }

    public static void ClearTopLine()
    {
        SaveTopLine();
        TopLine = "";
        TopLineState = TopLineState.Empty;
        MessageWin.Clear();
        Blit();
    }

    internal static void RenderTopLine(string? v = null)
    {
        v ??= TopLine;

        int row = 0;
        int col = 0;
        string remaining = v;
        MessageWin.Clear();
        var writer = MessageWin.At(0, 0);
        while (remaining.Length > 0)
        {
            int len = Math.Min(ScreenWidth, remaining.Length);
            if (len < remaining.Length)
            {
                int space = remaining.LastIndexOf(' ', len);
                if (space > 0) len = space;
            }
            writer.Write(remaining[..len].PadRight(ScreenWidth));
            remaining = remaining[len..].TrimStart();
            col = len;
            row++;
            writer.NewLine(false);
        }

        if (row > 1)
        {
            Blit();
            More(v == null, col, row);
            DrawCurrent();
        }
        else
        {
            Blit();
        }
    }

    internal static void DrawMessage(string msg)
    {
        if (TopLineState != TopLineState.Empty && CanAppendMessage(msg))
        {
            TopLine += messageDelimit + msg;
        }
        else
        {
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
                var cell = MapWin[c.X, c.Y];
                if (cell.HasValue)
                    MapWin[c.X, c.Y] = cell.Value with { Style = CellStyle.Reverse };
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
        string quiverState = "";
        if (u.Quiver?.Def is QuiverDef)
            quiverState = $" Q:{u.Quiver.Charges}/{u.Quiver.MaxCharges}";

        // Line 0: location, gold, round, energy, quiver
        string statusLine = $"{level.Branch.Name}:{level.EffectiveDepth} $:{u.Gold} R:{g.CurrentRound} E:{u.Energy}{quiverState}";
        StatusWin.At(0, 0).Write(statusLine.PadRight(ScreenWidth));

        // Line 1: HP, AC, CL, XP
        int nextLvl = u.CharacterLevel + 1;
        int needed = Progression.XpForLevel(nextLvl) - Progression.XpForLevel(u.CharacterLevel);
        int progress = u.XP - Progression.XpForLevel(u.CharacterLevel);
        string xpStr = $"XP:{progress}/{needed}";
        string hpStr = u.TempHp > 0 ? $"HP:{u.HP.Current}+{u.TempHp}/{u.HP.Max}" : $"HP:{u.HP.Current}/{u.HP.Max}";
        string prefix = $"{hpStr} AC:{u.GetAC()} CL:{u.CharacterLevel} ";
        StatusWin.At(0, 1).Write(prefix.PadRight(ScreenWidth));
        bool pendingLvl = Progression.HasPendingLevelUp(u);
        StatusWin.At(prefix.Length, 1).Write(xpStr, style: pendingLvl ? CellStyle.Reverse : CellStyle.None);
        DrawSpellPips();

        if (StatusHeight >= 3)
            DrawStatLine(2);

        if (StatusHeight >= 4)
            DrawStatusEffects(3);
    }

    static void DrawStatusEffects(int row)
    {
        StatusWin.At(0, row).Write("".PadRight(ScreenWidth));

        List<(string text, ConsoleColor color, int priority, int? remaining)> entries = [];

        // Hunger
        var hunger = Hunger.GetState(u.Nutrition);
        if (hunger != HungerState.Normal)
        {
            string label = Hunger.GetLabel(hunger);
            var (color, pri) = hunger switch
            {
                HungerState.Satiated => (ConsoleColor.Green, (int)StatusDisplay.Low),
                HungerState.Hungry => (ConsoleColor.Yellow, (int)StatusDisplay.Moderate),
                HungerState.Weak => (ConsoleColor.Red, (int)StatusDisplay.Severe),
                HungerState.Fainting => (ConsoleColor.Red, (int)StatusDisplay.Critical),
                _ => (ConsoleColor.Gray, (int)StatusDisplay.Low),
            };
            entries.Add((label, color, pri, null));
        }

        // Buffs from player and inventory
        foreach (var fact in u.LiveFacts.Where(f => f.Brick.IsBuff && f.Brick.StatusDisplayPriority != StatusDisplay.None))
            entries.Add(BuffEntry(fact));

        // Sort: priority asc, then remaining duration asc (expiring soon first)
        entries.Sort((a, b) =>
        {
            int c = a.priority.CompareTo(b.priority);
            if (c != 0) return c;
            // nulls (permanent) sort after timed
            if (a.remaining == null && b.remaining == null) return 0;
            if (a.remaining == null) return 1;
            if (b.remaining == null) return -1;
            return a.remaining.Value.CompareTo(b.remaining.Value);
        });

        int col = 0;
        int shown = 0;
        int total = entries.Count;
        foreach (var (text, color, _, _) in entries)
        {
            int needed = (shown > 0 ? 2 : 0) + text.Length; // "  " separator
            // Reserve space for overflow indicator
            int overflow = total - shown - 1;
            int reserveLen = overflow > 0 ? 2 + $"…+{overflow}".Length : 0;
            if (col + needed + reserveLen > ScreenWidth && overflow > 0)
            {
                string trunc = $"…+{total - shown}";
                StatusWin.At(col + 2, row).Write(trunc, ConsoleColor.DarkGray);
                break;
            }
            if (shown > 0) col += 2; // gap
            StatusWin.At(col, row).Write(text, color);
            col += text.Length;
            shown++;
        }
    }

    static (string text, ConsoleColor color, int priority, int? remaining) BuffEntry(Fact fact)
    {
        string name = fact.Brick.BuffName ?? fact.Brick.GetType().Name;
        int? rem = fact.RemainingRounds;

        // Build display text
        string text = name;
        if (fact.Brick.DisplayMode.HasFlag(FactDisplayMode.Stacks) && fact.Stacks > 1)
            text += $"[{fact.Stacks}]";

        var color = fact.Brick.StatusDisplayPriority switch
        {
            StatusDisplay.Critical => ConsoleColor.Red,
            StatusDisplay.Severe => ConsoleColor.Red,
            StatusDisplay.Moderate => ConsoleColor.Yellow,
            StatusDisplay.Affliction => ConsoleColor.DarkYellow,
            StatusDisplay.Buff => ConsoleColor.Green,
            StatusDisplay.Low => ConsoleColor.DarkGray,
            _ => ConsoleColor.Gray,
        };

        return (text, color, (int)fact.Brick.StatusDisplayPriority, rem);
    }

    static void DrawStatLine(int row)
    {
        string stats = $"Str:{u.Str} Dex:{u.Dex} Con:{u.Con} Int:{u.Int} Wis:{u.Wis} Cha:{u.Cha}";
        StatusWin.At(0, row).Write(stats.PadRight(ScreenWidth));

        int col = stats.Length;

        // Encumbrance
        if (u.Encumbrance != Encumbrance.Unencumbered)
        {
            string encStr = u.Encumbrance switch
            {
                Encumbrance.Burdened => " Burdn",
                Encumbrance.Stressed => " Stres",
                Encumbrance.Strained => " Strai",
                Encumbrance.Overtaxed => " Overt",
                Encumbrance.Overloaded => " Overl",
                _ => "",
            };
            var (fg, style) = u.Encumbrance switch
            {
                Encumbrance.Burdened => (ConsoleColor.Yellow, CellStyle.None),
                Encumbrance.Stressed => (ConsoleColor.DarkYellow, CellStyle.None),
                Encumbrance.Strained => (ConsoleColor.Red, CellStyle.None),
                _ => (ConsoleColor.Red, CellStyle.Reverse),
            };
            StatusWin.At(col, row).Write(encStr, fg, style: style);
        }
    }

    static void DrawSpellPips()
    {
        const int width = 6;
        for (int lvl = 1; lvl <= 9; lvl++)
        {
            var pool = u.GetPool($"spell_l{lvl}");
            if (pool == null) continue;

            int left = 36 + width * (lvl - 1);
            StatusWin.At(left, 0).Write($"l{lvl}");

            int available = pool.EffectiveMax;
            for (int i = 0; i < pool.Max; i++)
            {
                ConsoleColor fg = ConsoleColor.Gray;
                char ch;
                if (i >= available)
                {
                    ch = '○';
                    fg = ConsoleColor.Red;
                }
                else if (i < pool.Current)
                    ch = '●';
                else if (i == pool.Current && pool.Ticks >= pool.NextRegen / 2)
                    ch = '◐';
                else
                    ch = '○';

                StatusWin.At(left + 3 + i, 1).Write(ch.ToString(), fg);
            }
        }
    }
}

internal enum TopLineState
{
    Empty,
    PresentMustShow,
    PresentCanClear
}
