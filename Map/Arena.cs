namespace Pathhack.Map;

public static class Arena
{
    public static void Generate(LevelGenContext ctx)
    {
        Level level = ctx.level;
        const int size = 13;
        int ox = (level.Width - size) / 2;
        int oy = (level.Height - size) / 2;

        List<Pos> border = [];
        List<Pos> interior = [];

        for (int dy = 0; dy < size; dy++)
        for (int dx = 0; dx < size; dx++)
        {
            Pos p = new(ox + dx, oy + dy);
            bool edge = dx == 0 || dy == 0 || dx == size - 1 || dy == size - 1;
            level.Set(p, edge ? TileType.Wall : TileType.Floor);
            level.SetLit(p);
            (edge ? border : interior).Add(p);
        }

        Room room = new(border, interior) { Flags = RoomFlags.Lit };
        level.Rooms.Add(room);
        foreach (var p in border) level.GetOrCreateState(p).Room = room;
        foreach (var p in interior) level.GetOrCreateState(p).Room = room;

        level.StairsUp = new(ox + size / 2, oy + size / 2);
        level.NoInitialSpawns = true;
    }
}
