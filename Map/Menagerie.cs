namespace Pathhack.Map;

public class StasisBuff : LogicBrick
{
    public static readonly StasisBuff Instance = new();
    public override string Id => "stasis";

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.FalseWhen("can_act");
}

/// Scans cardinal directions only — place the stasis monster on a cardinal from the trap.
public class ReleaseTrap() : Trap(TrapType.Release, 0, 99, 0, 0)
{
    public override Glyph Glyph => new('_', ConsoleColor.Cyan);

    public override bool Trigger(IUnit? unit, Item? item)
    {
        if (unit is not { IsPlayer: true }) return false;

        // scan nearby for a monster in stasis (BF: closest first, cardinal only)
        Pos pos = unit.Pos;
        for (int dist = 1; dist <= 4; dist++)
        {
            foreach (var dir in Pos.CardinalDirs)
            {
                Pos check = pos + dir * dist;
                if (!lvl.InBounds(check)) continue;
                if (lvl.UnitAt(check) is Monster m && m.HasFact(StasisBuff.Instance))
                {
                    m.RemoveStack(StasisBuff.Instance);
                    m.IsAsleep = false;
                    g.pline($"{m:The} stirs to life!");
                    return true;
                }
            }
        }
        return false;
    }
}

public static class Menagerie
{
    public static void Generate(LevelGenContext ctx, MonsterDef[] monsters)
    {
        Level level = ctx.level;
        const int cellW = 7, cellH = 7;
        int cols = level.Width / cellW;
        int rows = level.Height / cellH;

        // fill with floor and light everything
        for (int y = 0; y < level.Height; y++)
        for (int x = 0; x < level.Width; x++)
        {
            Pos p = new(x, y);
            level.Set(p, TileType.Floor);
            level.SetLit(p);
        }

        // draw horizontal walls
        for (int row = 0; row <= rows; row++)
        {
            int y = row * cellH;
            if (y >= level.Height) break;
            for (int x = 0; x < level.Width; x++)
                level.Set(new(x, y), TileType.Wall);
        }

        // draw vertical walls
        for (int col = 0; col <= cols; col++)
        {
            int x = col * cellW;
            if (x >= level.Width) break;
            for (int y = 0; y < level.Height; y++)
                level.Set(new(x, y), TileType.Wall);
        }

        // create one big lit room so FOV keeps everything visible
        List<Pos> border = [];
        List<Pos> interior = [];
        for (int y = 0; y < level.Height; y++)
        for (int x = 0; x < level.Width; x++)
        {
            Pos p = new(x, y);
            if (level[p].Type == TileType.Wall) border.Add(p);
            else interior.Add(p);
        }
        Room room = new(border, interior) { Flags = RoomFlags.Lit };
        level.Rooms.Add(room);
        foreach (var p in border) level.GetOrCreateState(p).Room = room;
        foreach (var p in interior) level.GetOrCreateState(p).Room = room;
        level.SpawnFlags = SpawnFlags.None;

        // place player in first cell
        level.StairsUp = new(cellW / 2, cellH / 2);

        // place monsters, one per cage, with stasis + release trap
        int idx = 0;
        for (int row = 0; row < rows && idx < monsters.Length; row++)
        for (int col = 0; col < cols && idx < monsters.Length; col++)
        {
            if (row == 0 && col == 0) continue;
            Pos center = new(col * cellW + cellW / 2, row * cellH + cellH / 2);
            var mon = Monster.Spawn(monsters[idx], "menagerie");
            mon.IsAsleep = true;
            mon.AddFact(StasisBuff.Instance);
            level.PlaceUnit(mon, center);

            // place release trap one tile south of center (or north if no room)
            Pos trapPos = center + new Pos(0, 3);
            if (level[trapPos].IsPassable && level.NoUnit(trapPos))
                level.Traps[trapPos] = new ReleaseTrap { PlayerSeen = true };

            idx++;
        }

        level.BaseLit.Reset(true);
    }
}
