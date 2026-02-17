namespace Pathhack.UI;

public class Snapshot
{
    public int Round;
    public int Width;
    public int Height;
    public int[][] Chars = [];
    public int[][] Colors = [];
    public int[][] Vis = [];
    public string?[][] Tips = [];
    public string[] Messages = [];

    // Player state
    public int Hp, MaxHp, TempHp, AC, CL, XP;
    public long Gold;
    public int Str, Dex, Con, Int, Wis, Cha;
    public string Hunger = "";
    public string[] Buffs = [];
    public List<SpellSlotState> SpellSlots = [];
}

public record struct SpellSlotState(int Level, int Current, int Max, int EffectiveMax, int Ticks, int RegenRate);

public static class BlackBox
{
    const int Capacity = 100;
    static readonly Snapshot[] _buffer = new Snapshot[Capacity];
    static int _head;
    static int _count;
    static int _lastMessageIndex;

    public static void Record()
    {
        Level level = g.CurrentLevel!;
        int w = level.Width;
        int h = level.Height;

        int[][] chars = new int[h][];
        int[][] colors = new int[h][];
        int[][] vis = new int[h][];
        string?[][] tips = new string?[h][];

        for (int y = 0; y < h; y++)
        {
            chars[y] = new int[w];
            colors[y] = new int[w];
            vis[y] = new int[w];
            tips[y] = new string?[w];

            for (int x = 0; x < w; x++)
            {
                Pos p = new(x, y);
                var (cell, v) = Dump.ResolveCellFov(level, p);
                if (Dump.IsWallLike(level, p))
                    v |= 4;
                chars[y][x] = cell.Ch;
                colors[y][x] = (int)cell.Fg;
                vis[y][x] = v;
                tips[y][x] = (v & 2) != 0 ? Dump.DescribeCell(level, p) : null;
            }
        }

        // Messages since last snapshot
        var messages = g.MessageHistory;
        int from = Math.Max(0, _lastMessageIndex);
        string[] newMessages = from < messages.Count
            ? messages.GetRange(from, messages.Count - from).Select(m => m.Text).ToArray()
            : [];
        _lastMessageIndex = messages.Count;

        // Spell slots
        List<SpellSlotState> slots = [];
        for (int lvl = 1; lvl <= 9; lvl++)
        {
            var pool = u.GetPool($"spell_l{lvl}");
            if (pool == null) continue;
            slots.Add(new(lvl, pool.Current, pool.Max, pool.EffectiveMax, pool.Ticks, (int)pool.RegenRate.Average()));
        }

        HungerState hunger = Hunger.GetState(u.Nutrition);

        Snapshot snap = new()
        {
            Round = g.CurrentRound,
            Width = w,
            Height = h,
            Chars = chars,
            Colors = colors,
            Vis = vis,
            Tips = tips,
            Messages = newMessages,
            Hp = u.HP.Current,
            MaxHp = u.HP.Max,
            TempHp = u.TempHp,
            AC = u.GetAC(),
            CL = u.CharacterLevel,
            XP = u.XP,
            Gold = u.Gold,
            Str = u.Str, Dex = u.Dex, Con = u.Con,
            Int = u.Int, Wis = u.Wis, Cha = u.Cha,
            Hunger = Hunger.GetLabel(hunger),
            Buffs = [.. u.LiveFacts
                .Where(f => f.Brick.BuffName != null)
                .Select(f => f.DisplayName)],
            SpellSlots = slots,
        };

        _buffer[_head] = snap;
        _head = (_head + 1) % Capacity;
        if (_count < Capacity) _count++;
    }

    public static Snapshot[] Drain()
    {
        Snapshot[] result = new Snapshot[_count];
        int start = _count < Capacity ? 0 : _head;
        for (int i = 0; i < _count; i++)
            result[i] = _buffer[(start + i) % Capacity];
        return result;
    }

    public static void Reset()
    {
        _head = 0;
        _count = 0;
        _lastMessageIndex = 0;
        Array.Clear(_buffer);
    }
}
