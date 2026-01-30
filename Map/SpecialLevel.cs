namespace Pathhack.Map;

public record SpecialLevel(string Id, string Map, Action<LevelBuilder>? PreRender = null, Action<LevelBuilder>? PostRender = null);

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
    };
    
    static readonly HashSet<char> MarkerChars = ['+', '<', '>', 'S', '^'];
    
    public static Dictionary<char, List<Pos>> Parse(SpecialLevel spec, LevelGenContext ctx)
    {
        Dictionary<char, List<Pos>> marks = [];
        OrderedDictionary<int, List<Pos>> roomTiles = [];
        
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
                    ctx.level.PlaceDoor(p, DoorState.Closed);
                    AddMark(marks, c, p);
                }
                else if (char.IsDigit(c))
                {
                    int n = c - '0';
                    if (!roomTiles.TryGetValue(n, out var list))
                        roomTiles[n] = list = [];
                    list.Add(p);
                }
                else if (MarkerChars.Contains(c) || char.IsLetter(c))
                {
                    ctx.level.Set(p, TileType.Floor);
                    AddMark(marks, c, p);
                }
            }
        }
        
        // Build rooms from digit tiles
        foreach (var (n, tiles) in roomTiles)
        {
            int minX = tiles.Min(p => p.X);
            int maxX = tiles.Max(p => p.X);
            int minY = tiles.Min(p => p.Y);
            int maxY = tiles.Max(p => p.Y);
            Rect bounds = new(minX, minY, maxX - minX + 1, maxY - minY + 1);
            ctx.Log("adding room");
            LevelGen.PlaceRoom(ctx, bounds);
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
