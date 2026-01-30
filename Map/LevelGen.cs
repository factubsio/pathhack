using System.Security.Cryptography;
using System.Text;

namespace Pathhack.Map;

public static class LevelGen
{
    static Random _rng = new();
    static StreamWriter? _log;

    static void Log(string msg) => _log?.WriteLine(msg);

    public static int Rn2(int n) => _rng.Rn2(n);
    public static int Rn1(int x, int y) => _rng.Rn1(x, y);
    public static int RnRange(int min, int max) => _rng.RnRange(min, max);
    public static int Rne(int n) => _rng.Rne(n);
    public static Pos RandomInterior(Rect r) => _rng.RandomInterior(r);

    // 1/3 has door: 1/5 open, 1/6 locked, rest closed. null = no door.
    static DoorState RollDoorState()
    {
        if (Rn2(3) != 0) return DoorState.None;
        if (Rn2(5) == 0) return DoorState.Open;
        // if (Rn2(6) == 0) return DoorState.Locked;
        return DoorState.Closed;
    }
    public static Pos RandomBorder(Rect r) => _rng.RandomBorder(r);
    public static List<Pos> RandomInteriorN(Rect r, int n) => _rng.RandomInteriorN(r, n);
    public static T Pick<T>(List<T> list) => list[Rn2(list.Count)];
    public static Pos RandomInterior(Room room) => room.Interior[Rn2(room.Interior.Count)];

    public static void Seed(LevelId id, int gameSeed)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{gameSeed}:{id.Branch.Name}:{id.Depth}"));
        int seed = BitConverter.ToInt32(bytes, 0);
        _rng = new Random(seed);
    }

    static readonly SpecialLevel[] TestLevels = [
        Dat.TestLevel.DrunkJinkin,
        Dat.TestLevel.OneRoom,
        Dat.TestLevel.Mitflit,
        Dat.TestLevel.Pugwampi,
        Dat.TestLevel.Jinkin,
        Dat.TestLevel.Nuglub,
        Dat.TestLevel.Grimple,
    ];

    public static SpecialLevel? ForcedLevel1;

    public static Level Generate(LevelId id, int gameSeed)
    {
        _log = new StreamWriter($"levelgen_{id.Branch.Name}_{id.Depth}.log");
        try
        {
            Log($"Generating {id.Branch.Name}:{id.Depth} seed={gameSeed}");
            Seed(id, gameSeed);
            LevelGenContext ctx = new(_log)
            {
                level = new(id, 80, 21),
            };
            
            var resolved = id.Branch.ResolvedLevels[id.Depth - 1];
            SpecialLevel? special = (ForcedLevel1, id.Depth, resolved.Template) switch
            {
                ({ } forced, 1, _) => forced,
                (_, _, { } template) => template,
                _ => null
            };
            
            if (special != null)
            {
                GenSpecial(ctx, special);
            }
            else
            {
                GenRoomsAndCorridors(ctx);
            }

            foreach (var cmd in resolved.Commands)
            {
                Log($"executing resolved action: {cmd.Debug}");
                cmd.Action(ctx);
            }
            
            PatchEmptyDoors(ctx.level);
            LogLevel(ctx.level);
            return ctx.level;
        }
        finally
        {
            _log.Dispose();
            _log = null;
        }
    }

    public static SpecialLevel? GetTemplate(string? name) => name == null ? null : SpecialLevels.TryGetValue(name, out var level) ? level : null;

    static readonly Dictionary<string, SpecialLevel> SpecialLevels = new()
    {
        ["everflame_tomb"] = Dat.CryptLevels.EverflameEnd,
        ["bigroom_rect"] = Dat.BigRoomLevels.Rectangle,
        ["bigroom_oval"] = Dat.BigRoomLevels.Oval,
    };
    
    static SpecialLevel? FindSpecialLevel(string id) => 
        SpecialLevels.GetValueOrDefault(id);

    static void PatchEmptyDoors(Level level)
    {
        for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                Pos p = new(x, y);
                if (level[p].Type == TileType.Door && level.GetState(p)?.Door == DoorState.None)
                    level.Set(p, TileType.Floor);
            }
    }

    static void LogLevel(Level level)
    {
        const string xChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Log("");
        // X axis header
        char[] xAxis = new char[level.Width + 2];
        xAxis[0] = ' ';
        xAxis[1] = ' ';
        for (int x = 0; x < level.Width; x++)
            xAxis[x + 2] = (x % 5 == 0) ? xChars[x % xChars.Length] : ' ';
        Log(new string(xAxis));

        for (int y = 0; y < level.Height; y++)
        {
            char[] row = new char[level.Width + 2];
            row[0] = (y % 5 == 0) ? (char)('0' + (y % 10)) : ' ';
            row[1] = ' ';
            for (int x = 0; x < level.Width; x++)
            {
                row[x + 2] = level[new(x, y)].Type switch
                {
                    TileType.Rock => ' ',
                    TileType.Floor => '.',
                    TileType.Wall => '#',
                    TileType.Corridor => ',',
                    TileType.Door => '+',
                    TileType.StairsUp => '<',
                    TileType.StairsDown => '>',
                    _ => '?',
                };
            }
            Log(new string(row));
        }
    }

    static void GenSpecial(LevelGenContext ctx, SpecialLevel spec)
    {
        // Fill with rock
        for (int y = 0; y < ctx.level.Height; y++)
            for (int x = 0; x < ctx.level.Width; x++)
                ctx.level.Set(new(x, y), TileType.Rock);

        var marks = SpecialLevelParser.Parse(spec, ctx);

        spec.PreRender?.Invoke(new LevelBuilder(marks, ctx));

        // // Render rooms to tiles
        RenderRooms(ctx);

        // // Merge adjacent rooms (after render so we can modify tiles)
        MergeAdjacentRooms(ctx);
        
        // Re-place doors (RenderRooms may have overwritten them)
        foreach (var p in marks.GetValueOrDefault('+', []))
            ctx.level.PlaceDoor(p, DoorState.Closed);

        spec.PostRender?.Invoke(new LevelBuilder(marks, ctx));
    }

    static void GenRoomsAndCorridors(LevelGenContext ctx)
    {
        // Fill with rock
        for (int y = 0; y < ctx.level.Height; y++)
            for (int x = 0; x < ctx.level.Width; x++)
                ctx.level.Set(new(x, y), TileType.Rock);

        // Place rooms (virtual)
        int targetRooms = RnRange(5, 8);
        const int maxTries = 200;

        int triesRemaining = maxTries;

        while (triesRemaining > 0 && ctx.Stamps.Count < targetRooms)
            triesRemaining -= TryPlaceRoom(ctx, triesRemaining);

        // Render rooms to tiles
        RenderRooms(ctx);

        // Merge adjacent rooms (after render so we can modify tiles)
        MergeAdjacentRooms(ctx);

        // Connect rooms with corridors
        MakeCorridors(ctx);

        // Assign special room types
        AssignRoomTypes(ctx);

        // Populate special rooms
        PopulateRooms(ctx);
    }

    public static void PlaceRoom(LevelGenContext ctx, Rect bounds)
    {
        ctx.Stamps.Add(new(bounds));
        ctx.MarkOccupied(bounds);
        Log($"Placed room {ctx.Stamps.Count - 1} at {bounds.X},{bounds.Y} size {bounds.W}x{bounds.H}");
    }

    public static void PlaceIrregularRoom(LevelGenContext ctx, HashSet<Pos> tiles)
    {
        ctx.Stamps.Add(new(tiles));
        ctx.MarkOccupied(tiles);
        Log($"Placed irregular room {ctx.Stamps.Count - 1} with {tiles.Count} tiles");
    }

    public static int TryPlaceRoom(LevelGenContext ctx, int maxTries)
    {
        int i;
        for (i = 1; i <= maxTries; i++)
        {
            int rw = RnRange(5, 12);
            int rh = RnRange(5, 8);
            if ((rw - 2) * (rh - 2) > 50)
                rh = 50 / (rw - 2) + 2;
            int rx = RnRange(1, ctx.level.Width - rw - 1);
            int ry = RnRange(1, ctx.level.Height - rh - 1);
            Rect bounds = new(rx, ry, rw, rh);

            if (CanPlace(ctx.occupied, bounds))
            {
                PlaceRoom(ctx, bounds);
                break;
            }
        }

        return i;
    }

    static void AssignRoomTypes(LevelGenContext ctx)
    {
        var level = ctx.level;
        // Goblin nest: high chance depth 1-3, decreasing after
        int nestChance = level.Depth <= 3 ? 3 : Math.Max(0, 6 - level.Depth);
        if (nestChance > 0 && Rn2(10) < 100)
        {
            // Pick an eligible room (not first/last which have stairs)
            List<int> eligible = [];
            for (int i = 1; i < level.Rooms.Count - 1; i++)
                if ((level.Rooms[i].Flags & RoomFlags.Merged) == 0)
                    eligible.Add(i);
            if (eligible.Count > 0)
            {
                int idx = eligible[Rn2(eligible.Count)];
                level.Rooms[idx] = level.Rooms[idx] with { Type = RoomType.GoblinNest };
                Log($"Assigned GoblinNest to room {idx}");
            }
        }
    }

    static void PopulateRooms(LevelGenContext ctx)
    {
        foreach (var room in ctx.level.Rooms)
        {
            switch (room.Type)
            {
                case RoomType.GoblinNest:
                    FillGoblinNest(ctx, room);
                    break;
            }
        }
    }

    static void FillGoblinNest(LevelGenContext ctx, Room room)
    {
        void Place(MonsterDef def)
        {
            if (ctx.level.FindLocationInRoom(room, ctx.level.NoUnit) is { } pos)
            {
                var m = Monster.Spawn(def);
                ctx.level.PlaceUnit(m, pos);
            }
        }

        // Always: 1 chef
        Place(Goblins.Chef);

        // 2-4 warriors
        int warriors = RnRange(2, 4);
        for (int i = 0; i < warriors; i++)
            Place(Goblins.Warrior);

        // 1/3 chance: chanter
        if (Rn2(3) == 0)
            Place(Goblins.WarChanter);

        // 1/4 chance: pyro
        if (Rn2(4) == 0)
            Place(Goblins.Pyro);

        // 1/10 chance: medium boss
        if (Rn2(10) == 0)
            Place(Goblins.MediumBoss);
    }

    static bool CanPlace(uint[] occupied, Rect r)
    {
        uint mask = ((1u << r.H) - 1) << r.Y;
        for (int x = r.X; x < r.X + r.W; x++)
            if ((occupied[x] & mask) != 0)
                return false;
        return true;
    }

    public static void RenderRooms(LevelGenContext ctx)
    {
        foreach (var stamp in ctx.Stamps)
        {
            Room room = Room.FromStamp(stamp);
            if (Rn2(ctx.level.Depth + 5) < 5)
                room.Flags |= RoomFlags.Lit;
            ctx.level.Rooms.Add(room);
            RenderRoom(ctx.level, room);
        }
    }

    public static void RenderRoom(Level level, Room room)
    {
        foreach (Pos p in room.Border)
        {
            level.Set(p, TileType.Wall);
            level.GetOrCreateState(p).Room = room;
        }
        foreach (Pos p in room.Interior)
        {
            level.Set(p, TileType.Floor);
            level.GetOrCreateState(p).Room = room;
        }
    }

    static void MergeAdjacentRooms(LevelGenContext ctx)
    {
        var level = ctx.level;
        for (int i = 0; i < level.Rooms.Count - 1; i++)
        {
            if (level.Rooms[i].Bounds is not { } a) continue;
            for (int j = i + 1; j < level.Rooms.Count; j++)
            {
                if (level.Rooms[j].Bounds is not { } b) continue;

                // Check if walls are adjacent (sharing a wall)
                bool xAdj = a.X + a.W == b.X || b.X + b.W == a.X;
                bool yAdj = a.Y + a.H == b.Y || b.Y + b.H == a.Y;

                if (xAdj && !yAdj)
                {
                    // Vertical adjacency - check Y overlap
                    int minY = Math.Max(a.Y + 1, b.Y + 1);
                    int maxY = Math.Min(a.Y + a.H - 2, b.Y + b.H - 2);
                    if (maxY >= minY)
                    {
                        int wallX = a.X + a.W == b.X ? a.X + a.W - 1 : b.X + b.W - 1;
                        for (int y = minY; y <= maxY; y++)
                        {
                            level.Set(new(wallX, y), TileType.Floor);
                            level.Set(new(wallX + 1, y), TileType.Floor);
                            level.Rooms[i].Border.Remove(new(wallX, y));
                            level.Rooms[i].Border.Remove(new(wallX + 1, y));
                            level.Rooms[j].Border.Remove(new(wallX, y));
                            level.Rooms[j].Border.Remove(new(wallX + 1, y));
                        }
                        level.Rooms[i].Flags |= RoomFlags.Merged;
                        level.Rooms[j].Flags |= RoomFlags.Merged;
                    }
                }
                else if (yAdj && !xAdj)
                {
                    // Horizontal adjacency - check X overlap
                    int minX = Math.Max(a.X + 1, b.X + 1);
                    int maxX = Math.Min(a.X + a.W - 2, b.X + b.W - 2);
                    if (maxX >= minX)
                    {
                        int wallY = a.Y + a.H == b.Y ? a.Y + a.H - 1 : b.Y + b.H - 1;
                        for (int x = minX; x <= maxX; x++)
                        {
                            level.Set(new(x, wallY), TileType.Floor);
                            level.Set(new(x, wallY + 1), TileType.Floor);
                            level.Rooms[i].Border.Remove(new(x, wallY));
                            level.Rooms[i].Border.Remove(new(x, wallY + 1));
                            level.Rooms[j].Border.Remove(new(x, wallY));
                            level.Rooms[j].Border.Remove(new(x, wallY + 1));
                        }
                        level.Rooms[i].Flags |= RoomFlags.Merged;
                        level.Rooms[j].Flags |= RoomFlags.Merged;
                    }
                }
            }
        }
    }

    public static void MakeCorridors(LevelGenContext ctx)
    {
        List<Room> connectable = [.. ctx.level.Rooms
            .Where(r => (r.Flags & RoomFlags.NoConnect) == 0 && r.Bounds != null)
            .OrderBy(r => r.Bounds!.Value.X)];

        if (connectable.Count < 2) return;

        // Union-find for connectivity
        int[] group = new int[connectable.Count];
        for (int i = 0; i < group.Length; i++) group[i] = i;
        int Find(int x) => group[x] == x ? x : group[x] = Find(group[x]);
        void Union(int a, int b) => group[Find(a)] = Find(b);

        Log($"MakeCorridors: {connectable.Count} rooms");

        // Connect each room to next neighbor
        for (int i = 0; i < connectable.Count - 1; i++)
        {
            if (Rn2(50) == 0) break; // occasionally stop early
            if (TryDigCorridor(ctx, connectable[i], connectable[i + 1]))
                Union(i, i + 1);
        }

        // Connect to second neighbor if not connected
        for (int i = 0; i < connectable.Count - 2; i++)
        {
            if (Find(i) != Find(i + 2))
                if (TryDigCorridor(ctx, connectable[i], connectable[i + 2]))
                    Union(i, i + 2);
        }

        // Ensure full connectivity - only if needed
        bool any = true;
        while (any)
        {
            any = false;
            for (int i = 0; i < connectable.Count; i++)
            {
                for (int j = 0; j < connectable.Count; j++)
                {
                    if (Find(i) != Find(j))
                    {
                        Log($"Room {i} and {j} not connected, joining");
                        if (TryDigCorridor(ctx, connectable[i], connectable[j]))
                            Union(i, j);
                        any = true;
                    }
                }
            }
        }

        // Random extra connections for loops - but limit to one corridor per room pair
        if (connectable.Count > 2)
        {
            var connected = new HashSet<(int, int)>();
            // Mark existing connections
            for (int i = 0; i < connectable.Count - 1; i++)
                connected.Add((i, i + 1));
            for (int i = 0; i < connectable.Count - 2; i++)
                connected.Add((i, i + 2));

            int extras = Rn2(connectable.Count) + 4;
            for (int i = 0; i < extras; i++)
            {
                int a = Rn2(connectable.Count);
                int b = Rn2(connectable.Count - 2);
                if (b >= a) b += 2;
                var pair = a < b ? (a, b) : (b, a);
                if (connected.Contains(pair)) continue;
                // Skip merged rooms 7/8 of time
                if ((connectable[a].Flags & RoomFlags.Merged) != 0 && Rn2(8) != 0)
                    continue;
                if ((connectable[b].Flags & RoomFlags.Merged) != 0 && Rn2(8) != 0)
                    continue;
                if (TryDigCorridor(ctx, connectable[a], connectable[b]))
                    connected.Add(pair);
            }
        }
    }

    static bool TryDigCorridor(LevelGenContext ctx, Room roomA, Room roomB)
    {
        var level = ctx.level;
        Rect a = roomA.Bounds!.Value;
        Rect b = roomB.Bounds!.Value;

        int dx, dy;
        Pos doorA, doorB;

        if (b.X > a.X + a.W - 1)
        {
            // B is to the right of A
            dx = 1; dy = 0;
            int minY = Math.Max(a.Y + 1, b.Y + 1);
            int maxY = Math.Min(a.Y + a.H - 2, b.Y + b.H - 2);
            if (maxY >= minY)
            {
                int doorY = RnRange(minY, maxY);
                doorA = new(a.X + a.W - 1, doorY);
                doorB = new(b.X, doorY);
            }
            else
            {
                // No Y overlap - pick doors on facing walls at different Y
                doorA = new(a.X + a.W - 1, RnRange(a.Y + 1, a.Y + a.H - 2));
                doorB = new(b.X, RnRange(b.Y + 1, b.Y + b.H - 2));
            }
        }
        else if (b.X + b.W - 1 < a.X)
        {
            // B is to the left of A
            dx = -1; dy = 0;
            int minY = Math.Max(a.Y + 1, b.Y + 1);
            int maxY = Math.Min(a.Y + a.H - 2, b.Y + b.H - 2);
            if (maxY >= minY)
            {
                int doorY = RnRange(minY, maxY);
                doorA = new(a.X, doorY);
                doorB = new(b.X + b.W - 1, doorY);
            }
            else
            {
                doorA = new(a.X, RnRange(a.Y + 1, a.Y + a.H - 2));
                doorB = new(b.X + b.W - 1, RnRange(b.Y + 1, b.Y + b.H - 2));
            }
        }
        else if (b.Y > a.Y + a.H - 1)
        {
            // B is below A
            dx = 0; dy = 1;
            int minX = Math.Max(a.X + 1, b.X + 1);
            int maxX = Math.Min(a.X + a.W - 2, b.X + b.W - 2);
            if (maxX >= minX)
            {
                int doorX = RnRange(minX, maxX);
                doorA = new(doorX, a.Y + a.H - 1);
                doorB = new(doorX, b.Y);
            }
            else
            {
                doorA = new(RnRange(a.X + 1, a.X + a.W - 2), a.Y + a.H - 1);
                doorB = new(RnRange(b.X + 1, b.X + b.W - 2), b.Y);
            }
        }
        else if (b.Y + b.H - 1 < a.Y)
        {
            // B is above A
            dx = 0; dy = -1;
            int minX = Math.Max(a.X + 1, b.X + 1);
            int maxX = Math.Min(a.X + a.W - 2, b.X + b.W - 2);
            if (maxX >= minX)
            {
                int doorX = RnRange(minX, maxX);
                doorA = new(doorX, a.Y);
                doorB = new(doorX, b.Y + b.H - 1);
            }
            else
            {
                doorA = new(RnRange(a.X + 1, a.X + a.W - 2), a.Y);
                doorB = new(RnRange(b.X + 1, b.X + b.W - 2), b.Y + b.H - 1);
            }
        }
        else
        {
            // Rooms overlap - shouldn't happen
            Log($"  Rooms overlap, skipping");
            return false;
        }

        Log($"TryDigCorridor room {a.X},{a.Y} to {b.X},{b.Y} doors {doorA.X},{doorA.Y} -> {doorB.X},{doorB.Y}");

        // Dig from just outside doorA to just outside doorB
        Pos org = new(doorA.X + dx, doorA.Y + dy);
        Pos dest = new(doorB.X - dx, doorB.Y - dy);

        List<Pos> dug = [];
        int x = org.X, y = org.Y;

        while (true)
        {
            Tile t = level[new(x, y)];
            if (t.IsDiggable)
            {
                level.Set(new(x, y), TileType.Corridor);
                dug.Add(new(x, y));
                Pos dp = new(x, y);
                // Super fine grained logging for tunnel digging
                // Log($"  Dug {dp:c}");
                // LogLevel(level);
            }
            else if (!t.IsPassable)
            {
                Log($"  Failed at {x},{y} tile={t.Type}, undoing {dug.Count} tiles");
                foreach (Pos p in dug)
                    level.Set(p, TileType.Rock);
                return false;
            }

            if (x == dest.X && y == dest.Y)
                break;

            // Determine direction to goal
            int dix = Math.Abs(x - dest.X);
            int diy = Math.Abs(y - dest.Y);
            int ddx = x == dest.X ? 0 : dest.X > x ? 1 : -1;
            int ddy = y == dest.Y ? 0 : dest.Y > y ? 1 : -1;

            // Try to change direction if other axis is further
            if (dy != 0 && dix > diy && CanDig(level, x + ddx, y))
            {
                dx = ddx;
                dy = 0;
            }
            else if (dx != 0 && diy > dix && CanDig(level, x, y + ddy))
            {
                dy = ddy;
                dx = 0;
            }

            // Can we continue in current direction?
            if (CanDig(level, x + dx, y + dy))
            {
                x += dx;
                y += dy;
                continue;
            }

            // Try perpendicular
            if (dx != 0)
            {
                dx = 0;
                dy = ddy != 0 ? ddy : 1;
            }
            else
            {
                dy = 0;
                dx = ddx != 0 ? ddx : 1;
            }

            if (CanDig(level, x + dx, y + dy))
            {
                x += dx;
                y += dy;
                continue;
            }

            // Dead end - undo
            Log($"  Dead end at {x},{y}, undoing {dug.Count} tiles");
            foreach (Pos p in dug)
                level.Set(p, TileType.Rock);
            return false;
        }

        // Place doors if not adjacent to existing doors along wall (reuse existing door is ok)
        if (level[doorA].Type != TileType.Door && CanPlaceDoor(level, doorA))
        {
            level.PlaceDoor(doorA, RollDoorState());
        }
        if (level[doorB].Type != TileType.Door && CanPlaceDoor(level, doorB))
        {
            level.PlaceDoor(doorB, RollDoorState());
        }

        Log($"  Success, dug {dug.Count} tiles");
        LogLevel(level);
        return true;
    }

    static bool CanPlaceDoor(Level level, Pos p)
    {
        foreach (var dir in new Pos[] { new(0, 1), new(0, -1), new(1, 0), new(-1, 0) })
        {
            if (level[p + dir].Type == TileType.Door)
                return false;
        }
        return true;
    }

    static bool CanDig(Level level, int x, int y)
    {
        if (x <= 0 || x >= level.Width - 1 || y <= 0 || y >= level.Height - 1)
            return false;
        Tile t = level[new(x, y)];
        return t.IsDiggable || t.Type == TileType.Corridor;
    }
}


public class LevelGenContext(TextWriter log)
{
    public uint[] occupied = new uint[80];
    public required Level level;
    public bool NoSpawns;
    public List<RoomStamp> Stamps = [];

    public void Log(string str) => log.WriteLine(str);

    public void MarkOccupied(Rect bounds)
    {
        uint mask = ((1u << bounds.H) - 1) << bounds.Y;
        for (int x = bounds.X; x < bounds.X + bounds.W; x++)
            occupied[x] |= mask;
    }

    public void MarkOccupied(HashSet<Pos> tiles)
    {
        foreach (var p in tiles)
            occupied[p.X] |= 1u << p.Y;
    }

    internal Room? FindRoom(Func<Room, bool> accept, int maxAttempts = 15)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            var candidate = LevelGen.Pick(level.Rooms);
            if (accept(candidate)) return candidate;
        }
        return null;
    }

    internal Room PickRoom() => LevelGen.Pick(level.Rooms);

    internal T Throw<T>(string v)
    {
        throw new Exception($"building: {level.Id} -> {v}");
    }
}

public record class DungeonGenCommand(Action<LevelGenContext> Action, string Debug);