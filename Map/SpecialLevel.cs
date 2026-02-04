namespace Pathhack.Map;

public record SpecialLevel(string Id, string Map, Action<LevelBuilder>? PreRender = null, Action<LevelBuilder>? PostRender = null)
{
    public bool HasPortalToChild = false;
    public bool HasPortalToParent = false;
    public bool HasStairsUp = false;
    public bool HasStairsDown = false;

    public string? Name { get; init; }
    public string DisplayName => Name ?? Id;
    public HashSet<int>? IrregularRooms { get; init; }
}

public class LevelBuilder(Dictionary<char, List<Pos>> marks, LevelGenContext ctx)
{
    public Level Level => ctx.level;
    public LevelGenContext Context => ctx;
    
    public Pos this[char c] => marks.TryGetValue(c, out var list) ? list[0] : throw new($"No mark '{c}'");
    public List<Pos> Marks(char c) => marks.GetValueOrDefault(c, []);
    public Pos RnMark(char c) => LevelGen.Pick(Marks(c));
    public Room Room(int n) => Level.Rooms[n];
    
    public void Stair(Pos p, TileType type) => Level.Set(p, type);
    
    public void Door(Pos p, DoorState state) => Level.PlaceDoor(p, state);
    
    public void Monster(MonsterDef def, Pos p) =>
        Level.PlaceUnit(Game.Monster.Spawn(def), p);
    
    public void PlaceItem(ItemDef def, Pos p) =>
        Level.PlaceItem(ItemGen.GenerateItem(def), p);

    public void Trap(Trap trap, Pos p) =>
        Level.Traps[p] = trap;
    
    public Pos? FindLocation(Func<Pos, bool> predicate, int maxAttempts = 100)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            Room room = LevelGen.Pick(Level.Rooms);
            Pos p = LevelGen.RandomInterior(room);
            if (predicate(p)) return p;
        }
        return null;
    }
    
    public Pos? FindLocationInRoom(Room room, Func<Pos, bool> predicate, int maxAttempts = 100)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            Pos p = LevelGen.RandomInterior(room);
            if (predicate(p)) return p;
        }
        return null;
    }
    
    // public void NonDiggable(Rect r)
    // {
    //     for (int y = r.Y; y < r.Y + r.H; y++)
    //     for (int x = r.X; x < r.X + r.W; x++)
    //     {
    //         Pos p = new(x, y);
    //         var tile = Level[p];
    //         Level[p] = tile with { Flags = tile.Flags & ~TileFlags.Diggable };
    //     }
    // }
    
    // public void NonPasswall(Rect r)
    // {
    //     for (int y = r.Y; y < r.Y + r.H; y++)
    //     for (int x = r.X; x < r.X + r.W; x++)
    //     {
    //         Pos p = new(x, y);
    //         var tile = Level[p];
    //         Level[p] = tile with { Flags = tile.Flags & ~TileFlags.Passable };
    //     }
    // }
}

public static class SpecialLevelParser
{
    static readonly Dictionary<char, TileType> TileMap = new()
    {
        [' '] = TileType.Rock,
        ['.'] = TileType.Floor,
        ['#'] = TileType.Corridor,
        ['-'] = TileType.Wall,
        ['|'] = TileType.Wall,
        [','] = TileType.Grass,
    };
    
    static readonly HashSet<char> MarkerChars = ['+', '<', '>', 'S', '^', '_'];
    
    public static Dictionary<char, List<Pos>> Parse(SpecialLevel spec, LevelGenContext ctx)
    {
        Dictionary<char, List<Pos>> marks = [];
        SortedDictionary<int, List<Pos>> roomTiles = [];
        
        var lines = spec.Map.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int mapH = lines.Length;
        int mapW = lines.Max(l => l.Length);
        
        // Center the map
        int offX = (ctx.level.Width - mapW) / 2;
        int offY = (ctx.level.Height - mapH) / 2;
        
        for (int y = 0; y < mapH; y++)
        {
            var line = lines[y];
            for (int x = 0; x < line.Length; x++)
            {
                char c = line[x];
                Pos p = new(offX + x, offY + y);
                
                if (TileMap.TryGetValue(c, out var type))
                {
                    ctx.level.Set(p, type);
                }
                else if (c == '+')
                {
                    AddMark(marks, c, p);
                }
                else if (char.IsDigit(c))
                {
                    ctx.level.Set(p, TileType.Wall);
                    int n = c - '0';
                    if (!roomTiles.TryGetValue(n, out var list))
                        roomTiles[n] = list = [];
                    list.Add(p);
                }
                else if (MarkerChars.Contains(c) || char.IsLetter(c))
                {
                    // infer tile type from neighbors
                    var counts = new Dictionary<TileType, int>();
                    foreach (var d in Pos.CardinalDirs)
                    {
                        int nx = x + d.X, ny = y + d.Y;
                        if (ny >= 0 && ny < lines.Length && nx >= 0 && nx < lines[ny].Length)
                        {
                            char nc = lines[ny][nx];
                            if (TileMap.TryGetValue(nc, out var nt))
                                counts[nt] = counts.GetValueOrDefault(nt) + 1;
                        }
                    }
                    var tile = counts.MaxBy(kv => kv.Value).Key;
                    if (tile == TileType.Rock || tile == TileType.Wall) tile = TileType.Floor;
                    ctx.level.Set(p, tile);
                    AddMark(marks, c, p);
                }
            }
        }
        
        // Build rooms from digit walls via flood fill from adjacent floor
        foreach (var (n, walls) in roomTiles)
        {
            // Find a floor tile adjacent to any wall
            Pos? seed = null;
            foreach (var w in walls)
            {
                foreach (var d in Pos.CardinalDirs)
                {
                    var np = w + d;
                    if (ctx.level.InBounds(np) && ctx.level[np].Type == TileType.Floor)
                    {
                        seed = np;
                        break;
                    }
                }
                if (seed != null) break;
            }
            
            if (seed == null)
            {
                ctx.Log($"room {n}: no adjacent floor found");
                continue;
            }
            
            HashSet<Pos> filled = [];
            Queue<Pos> queue = new([seed.Value]);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (!filled.Add(p)) continue;
                foreach (var d in Pos.CardinalDirs)
                {
                    var np = p + d;
                    if (ctx.level.InBounds(np) && ctx.level[np].Type == TileType.Floor && !filled.Contains(np))
                        queue.Enqueue(np);
                }
            }
            
            // Turn digit walls to floor so they become part of room, RenderRoom
            // constructs walls later, it's all a bit hokey?
            foreach (var w in walls)
            {
                ctx.level.Set(w, TileType.Floor);
                filled.Add(w);
            }
            
            if (spec.IrregularRooms?.Contains(n) == true)
            {
                ctx.Log($"adding irregular room with {filled.Count} tiles");
                LevelGen.PlaceIrregularRoom(ctx, filled);
            }
            else
            {
                int minX = filled.Min(p => p.X);
                int maxX = filled.Max(p => p.X);
                int minY = filled.Min(p => p.Y);
                int maxY = filled.Max(p => p.Y);
                Rect bounds = new(minX, minY, maxX - minX + 1, maxY - minY + 1);
                ctx.Log($"adding room {n}");
                LevelGen.PlaceRoom(ctx, bounds);
            }
        }
        
        return marks;
    }
    
    static void AddMark(Dictionary<char, List<Pos>> marks, char c, Pos p)
    {
        if (!marks.TryGetValue(c, out var list))
            marks[c] = list = [];
        list.Add(p);
    }
}
