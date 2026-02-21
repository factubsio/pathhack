using System.Security.Cryptography;
using System.Text;

namespace Pathhack.Map;

public static partial class LevelGen
{
    static Random _rng = new();
    static StreamWriter? _log;
    
    public static bool QuietLog = false; // only log major stages, not every corridor step
    public static bool TestMode = false; // skip population, return after structure gen
    public static StreamWriter? SharedLog; // for --gen-dungeons, receives final renders
    public static bool ForceRiver = false; // always generate river
    public static bool ForceMiniVault = false; // always generate mini vault
    public static CaveAlgorithm? ForceAlgorithm; // override level gen algorithm
    public static int ParamSweep = -1; // parameter sweep index, -1 = off
    public static int ParamSweepMax = 10; // total number of sweep steps

    static void Log(string msg) => _log?.WriteLine(msg);
    static void LogVerbose(string msg) { if (!QuietLog) _log?.WriteLine(msg); }

    public static int Rn2(int n) => _rng.Rn2(n);
    public static int Rn1(int x, int y) => _rng.Rn1(x, y);
    public static int RnRange(int min, int max) => _rng.RnRange(min, max);
    public static int Rne(int n) => _rng.Rne(n);
    public static Pos RandomInterior(Rect r) => _rng.RandomInterior(r);

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
    public static MonsterDef[]? MenagerieMonsters;
    public static bool MonitorAttached;

    public static Level Generate(LevelId id, int gameSeed)
    {
        _log = new StreamWriter($"levelgen_{id.Branch.Name}_{id.Depth}.log") { AutoFlush = false };
        try
        {
            Log($"Generating {id.Branch.Name}:{id.Depth} seed={gameSeed}");
            Seed(id, gameSeed);
            LevelGenContext ctx = new(_log)
            {
                level = new(id, Draw.MapWidth, Draw.MapHeight),
            };
            
            var resolved = id.Branch.ResolvedLevels[id.Depth - 1];
            ctx.level.FloorColor = resolved.FloorColor;
            ctx.level.WallColor = resolved.WallColor;

            if (MenagerieMonsters is { } menagerie)
            {
                Menagerie.Generate(ctx, menagerie);
                ctx.level.UnderConstruction = false;
                return ctx.level;
            }

            if (MonitorAttached)
            {
                Arena.Generate(ctx);
                ctx.level.UnderConstruction = false;
                return ctx.level;
            }

            SpecialLevel? special = (ForcedLevel1, id.Depth, resolved.Template) switch
            {
                ({ } forced, 1, _) => forced,
                (_, _, { } template) => template,
                _ => null
            };
            
            if (special != null)
            {
                Log($"GenSpecial: {special.Id}");
                GenSpecial(ctx, special);
                Log("GenSpecial done");
            }
            else if ((ForceAlgorithm ?? resolved.Algorithm) is { } algo)
            {
                Log($"GenCave: {algo} sweep={ParamSweep}");
                switch (algo)
                {
                    case CaveAlgorithm.Worley: CaveGen.GenerateWorley(ctx, WorleyConfig.Sweep(WorleyConfig.Outdoor)); break;
                    case CaveAlgorithm.WorleyCavern: CaveGen.GenerateWorley(ctx, WorleyConfig.Sweep(WorleyConfig.Cavern)); break;
                    case CaveAlgorithm.WorleyWarren: CaveGen.GenerateWorley(ctx, WorleyConfig.Sweep(WorleyConfig.Warren)); break;
                    case CaveAlgorithm.CA: CaveGen.GenerateCA(ctx); break;
                    case CaveAlgorithm.OutdoorCA:
                        ctx.WallTile = TileType.Tree;
                        ctx.FloorTile = TileType.Grass;
                        ctx.level.Outdoors = true;
                        CaveGen.GenerateCA(ctx, fillPct: 0.50, smooth: 0);
                        break;
                    case CaveAlgorithm.OutdoorCAOpen:
                        ctx.WallTile = TileType.Tree;
                        ctx.FloorTile = TileType.Grass;
                        ctx.level.Outdoors = true;
                        CaveGen.GenerateCA(ctx, fillPct: 0.52);
                        break;
                    case CaveAlgorithm.Drunkard: CaveGen.GenerateDrunkard(ctx); break;
                    case CaveAlgorithm.BSP: CaveGen.GenerateBSP(ctx); break;
                    case CaveAlgorithm.Perlin: PerlinNoise.Generate(ctx); break;
                    case CaveAlgorithm.Circles: CaveGen.GenerateCircles(ctx); break;
                    case CaveAlgorithm.GrowingTree: CaveGen.GenerateGrowingTree(ctx); break;
                }
                Log("GenCave done");
            }
            else
            {
                Log("GenRoomsAndCorridors...");
                GenRoomsAndCorridors(ctx);
                Log("GenRoomsAndCorridors done");
            }

            if (TestMode)
            {
                Log("TestMode: returning after structure gen");
                LogLevel(ctx.level, toShared: true);
                ctx.level.UnderConstruction = false;
                return ctx.level;
            }

            Log("Resolving commands...");
            ctx.level.FloorTile = ctx.FloorTile;
            foreach (var cmd in resolved.Commands)
            {
                Log($"  executing: {cmd.Debug}");
                cmd.Action(ctx);
            }
            Log("Resolved commands done");

            ctx.level.BakeWallChars();

            if (!ctx.NoRoomAssignment)
            {
                Log("AssignRoomTypes...");
                AssignRoomTypes(ctx);

                if (ctx.WantsRiver)
                {
                    Log("MakeRiver...");
                    MakeRiver(ctx);
                    RemoveOrphanWalls(ctx.level);
                    ctx.level.Outdoors = Rn2(20) > ctx.level.EffectiveDepth;
                }

                Log("PopulateRooms...");
                PopulateRooms(ctx);
                Log("PopulateRooms done");
            }

            bool anywhere = ctx.level.SpawnFlags.HasFlag(SpawnFlags.Anywhere);
            if (ctx.level.Rooms.Count == 0 || anywhere)
            {
                Log($"PopulateCave (roomless={ctx.level.Rooms.Count == 0}, anywhere={anywhere})...");
                PopulateCave(ctx, anywhere ? ctx.level.Rooms.Count : 0);
            }
            
            LogLevel(ctx.level);
            BakeBaseLit(ctx.level);
            ctx.level.UnderConstruction = false;
            return ctx.level;
        }
        finally
        {
            _log?.Dispose();
            _log = null;
        }
    }

    public static SpecialLevel? GetTemplate(string? name) => name == null ? null : SpecialLevels.TryGetValue(name, out var level) ? level : null;

    static void BakeBaseLit(Level level)
    {
        foreach (var room in level.Rooms)
        {
            if (!room.Lit) continue;
            foreach (var p in room.Interior) level.BaseLit[p] = true;
            foreach (var p in room.Border) level.BaseLit[p] = true;
        }

        if (level.Outdoors)
            for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                Pos p = new(x, y);
                if (level[p].Type != TileType.Rock) level.BaseLit[p] = true;
            }
    }

    internal static readonly Dictionary<string, SpecialLevel> SpecialLevels = new()
    {
        ["everflame_tomb"] = Dat.CryptLevels.EverflameEnd,
        ["bigroom_rect"] = Dat.BigRoomLevels.Rectangle,
        ["bigroom_oval"] = Dat.BigRoomLevels.Oval,
        ["sanctuary_1"] = Dat.EndShrineLevels.EndShrine1,
        ["trunau_home"] = Dat.TrunauLevels.Home,
        ["trunau_siege"] = Dat.TrunauLevels.Siege,
        ["trunau_tomb"] = Dat.TrunauLevels.Tomb,
        ["redlake_outer"] = Dat.TrunauLevels.FortOuter,
        ["redlake_inner"] = Dat.TrunauLevels.RedlakeInner,
        ["ss_shore_beached"] = Dat.SerpentsSkullLevels.ShoreBeached,
        ["ss_shore_debris"] = Dat.SerpentsSkullLevels.ShoreDebris,
    };
    
    static SpecialLevel? FindSpecialLevel(string id) => 
        SpecialLevels.GetValueOrDefault(id);

    static void LogLevelVerbose(Level level) { if (!QuietLog) LogLevel(level); }

    const string xChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    static string PosStr(int x, int y) => $"{xChars[x % xChars.Length]}{y}";
    static string PosStr(Pos p) => PosStr(p.X, p.Y);
    static string RectStr(Rect r) => $"{PosStr(r.X, r.Y)} size {r.W}x{r.H}";

    static void LogLevel(Level level, bool toShared = false)
    {
        const string xChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        
        void Out(string s)
        {
            _log?.WriteLine(s);
            if (toShared) SharedLog?.WriteLine(s);
        }
        
        Out("");
        // X axis header
        char[] xAxis = new char[level.Width + 2];
        xAxis[0] = ' ';
        xAxis[1] = ' ';
        for (int x = 0; x < level.Width; x++)
            xAxis[x + 2] = (x % 5 == 0) ? xChars[x % xChars.Length] : ' ';
        Out(new string(xAxis));

        // Build room type overlay
        Dictionary<Pos, char> roomLabels = [];
        for (int i = 0; i < level.Rooms.Count; i++)
        {
            var room = level.Rooms[i];
            if (room.Type == RoomType.Ordinary) continue;
            if (room.Interior.Count == 0) continue;
            char label = room.Type switch
            {
                RoomType.GoblinNest => 'G',
                RoomType.GremlinParty => 'g',
                RoomType.GremlinPartyBig => 'P',
                _ => '?'
            };
            roomLabels[room.Interior[0]] = label;
        }

        for (int y = 0; y < level.Height; y++)
        {
            char[] row = new char[level.Width + 2];
            row[0] = (y % 5 == 0) ? (char)('0' + (y % 10)) : ' ';
            row[1] = ' ';
            for (int x = 0; x < level.Width; x++)
            {
                Pos p = new(x, y);
                if (roomLabels.TryGetValue(p, out char label))
                    row[x + 2] = label;
                else
                    row[x + 2] = level[p].Type switch
                    {
                        TileType.Rock => ' ',
                        TileType.Floor => '.',
                        TileType.Wall => '#',
                        TileType.Corridor => ',',
                        TileType.Door => '+',
                        TileType.StairsUp => '<',
                        TileType.StairsDown => '>',
                        TileType.Pool => '~',
                        TileType.Water => '~',
                        _ => '?',
                    };
            }
            Out(new string(row));
        }
    }

    static void GenSpecial(LevelGenContext ctx, SpecialLevel spec)
    {
        // Empty map = use room+corridor gen but still run hooks
        if (string.IsNullOrWhiteSpace(spec.Map))
        {
            spec.PreRender?.Invoke(new LevelBuilder([], ctx));
            GenRoomsAndCorridors(ctx, populate: false);
            spec.PostRender?.Invoke(new LevelBuilder([], ctx));
            return;
        }

        // Fill with rock
        for (int y = 0; y < ctx.level.Height; y++)
            for (int x = 0; x < ctx.level.Width; x++)
                ctx.level.Set(new(x, y), TileType.Rock);

        var marks = SpecialLevelParser.Parse(spec, ctx);

        spec.PreRender?.Invoke(new LevelBuilder(marks, ctx));

        // // Render rooms to tiles
        RenderRooms(ctx);

        // Note: room merging now happens during PlaceRoom
        
        // Re-place doors (RenderRooms may have overwritten them)
        foreach (var p in marks.GetValueOrDefault('+', []))
            ctx.level.PlaceDoor(p, DoorState.Closed);

        spec.PostRender?.Invoke(new LevelBuilder(marks, ctx));
    }

    static void MakeRiver(LevelGenContext ctx)
    {
        var level = ctx.level;
        bool horizontal = Rn2(4) == 0;
        
        if (horizontal)
        {
            int center = RnRange(6, level.Height - 6);
            int width = RnRange(4, 7);
            for (int x = 1; x < level.Width - 1; x++)
            {
                for (int y = center - width / 2; y <= center + width / 2; y++)
                {
                    bool edge = (y == center - width / 2 || y == center + width / 2);
                    Liquify(level, x, y, edge);
                }
                // Drift
                if (Rn2(3) == 0) center += Rn2(3) - 1;
                if (Rn2(3) == 0) width = Math.Clamp(width + Rn2(3) - 1, 4, 7);
                center = Math.Clamp(center, 4, level.Height - 5);
            }
        }
        else
        {
            int center = RnRange(7, level.Width - 7);
            int width = RnRange(5, 8);
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = center - width / 2; x <= center + width / 2; x++)
                {
                    bool edge = (x == center - width / 2 || x == center + width / 2);
                    Liquify(level, x, y, edge);
                }
                // Drift
                if (Rn2(3) == 0) center += Rn2(3) - 1;
                if (Rn2(3) == 0) width = Math.Clamp(width + Rn2(3) - 1, 5, 8);
                center = Math.Clamp(center, 5, level.Width - 6);
            }
        }
    }
    
    static void RemoveOrphanWalls(Level level)
    {
        for (int y = 1; y < level.Height - 1; y++)
            for (int x = 1; x < level.Width - 1; x++)
            {
                var p = new Pos(x, y);
                if (level[p].Type != TileType.Wall) continue;
                if (!p.CardinalNeighbours().Any(n => level[n].Type is TileType.Wall or TileType.Door))
                    level.Set(p, TileType.Water);
            }
    }
    
    static void Liquify(Level level, int x, int y, bool edge)
    {
        if (x <= 0 || x >= level.Width - 1 || y <= 0 || y >= level.Height - 1) return;
        
        var pos = new Pos(x, y);
        var tile = level[pos];
        
        // Don't liquify shop tiles
        var room = level.RoomAt(pos);
        if (room?.Type == RoomType.Shop) return;
        
        // Rock or non-edge wall -> Water
        if (tile.Type == TileType.Rock || (tile.Type == TileType.Wall && !edge && Rn2(3) != 0))
        {
            level.Set(pos, TileType.Water);
        }
        // Corridor/door -> Floor (preserves walkability)
        else if (tile.Type == TileType.Corridor || tile.Type == TileType.Door)
        {
            level.Set(pos, TileType.Floor);
        }
    }

    static void TryMiniVault(LevelGenContext ctx)
    {
        var level = ctx.level;
        for (int tries = 0; tries < 50; tries++)
        {
            int x = Rn2(level.Width - 6) + 1;
            int y = Rn2(level.Height - 6) + 1;
            
            bool ok = true;
            for (int i = 0; i < 6 && ok; i++)
                for (int j = 0; j < 6 && ok; j++)
                    if (level[new Pos(x + i, y + j)].Type != TileType.Rock)
                        ok = false;
            
            if (!ok) continue;
            
            // Outer ring = water
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                    level.Set(new Pos(x + i, y + j), TileType.Water);
            
            // Inner 2x2 = floor with walls
            for (int i = 1; i < 5; i++)
                for (int j = 1; j < 5; j++)
                    level.Set(new Pos(x + i, y + j), TileType.Wall);
            
            for (int i = 2; i < 4; i++)
                for (int j = 2; j < 4; j++)
                    level.Set(new Pos(x + i, y + j), TileType.Floor);
            
            Log($"MiniVault placed at {x},{y}");
            
            // TODO: chests, guard monster
            return;
        }
        Log("MiniVault failed to find spot");
    }

    public static bool PlaceRoom(LevelGenContext ctx, Rect bounds)
    {
        Log($"PlaceRoom trying {RectStr(bounds)}");
        
        // Find all touching stamps
        List<int> touching = [];
        HashSet<Pos> newBorder = [..bounds.Border()];
        
        for (int i = 0; i < ctx.Stamps.Count; i++)
        {
            var stamp = ctx.Stamps[i];
            IEnumerable<Pos> existingBorder = stamp.Bounds?.Border() ?? stamp.Tiles ?? [];
            
            bool touches = existingBorder.Any(p => 
                Pos.CardinalDirs.Any(dir => newBorder.Contains(p + dir)));
            
            if (touches)
            {
                Log($"  touches stamp {i}");
                touching.Add(i);
            }
        }
        
        if (touching.Count > 1)
        {
            Log($"  Rejected - touches {touching.Count} stamps");
            return false;
        }
        
        if (touching.Count == 1)
        {
            int i = touching[0];
            if (TryMergeStamps(ctx, i, bounds, out var merged))
            {
                ctx.Stamps.RemoveAt(i);
                ctx.Stamps.Add(new(merged));
                ctx.MarkOccupied(bounds);
                Log($"  Merged into stamp {i}, now {merged.Count} tiles");
                return true;
            }
            Log($"  Rejected - touches stamp {i} but can't merge");
            return false;
        }
        
        ctx.Stamps.Add(new(bounds));
        ctx.MarkOccupied(bounds);
        Log($"  Placed as room {ctx.Stamps.Count - 1}");
        return true;
    }
    
    static bool TryMergeStamps(LevelGenContext ctx, int existingIdx, Rect newBounds, out HashSet<Pos> merged)
    {
        merged = [];
        var existing = ctx.Stamps[existingIdx];
        HashSet<Pos> existingTiles = existing.Tiles ?? [..existing.Bounds!.Value.All()];
        HashSet<Pos> newBorder = [..newBounds.Border()];

        int touching = 0;
        foreach (var p in newBorder)
            foreach (var dir in Pos.CardinalDirs)
                if (existingTiles.Contains(p + dir)) { touching++; break; }

        if (touching < 3) return false;

        foreach (var p in existingTiles) merged.Add(p);
        foreach (var p in newBounds.All()) merged.Add(p);
        return true;
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

            if (CanPlace(ctx.occupied, bounds) && PlaceRoom(ctx, bounds))
                break;
        }

        return i;
    }

    static int RequireDepth(Level l, int min, int max = 99) => l.Depth >= min && l.Depth <= max ? 1 : 0;
    static int RequireSize(Room r, int min) => r.Interior.Count >= min ? 1 : 0;
    static int RequireNoUpStairs(Level l, Room r) => r.Interior.Any(p => 
        l[p].Type is TileType.StairsUp or TileType.BranchUp) ? 0 : 1;

    static int MapRange(int value, Range input, Range output)
    {
        int inMin = input.Start.Value, inMax = input.End.Value;
        int outMin = output.Start.Value, outMax = output.End.Value;
        if (value <= inMin) return outMin;
        if (value >= inMax) return outMax;
        return outMin + (outMax - outMin) * (value - inMin) / (inMax - inMin);
    }

    record RoomRule(RoomType Type, Func<Level, Room, int> Chance);

    static readonly RoomRule[] RoomRules = [
        new(RoomType.GoblinNest, (l, r) => RequireNoUpStairs(l, r) * RequireSize(r, 9) * RequireDepth(l, 3) * MapRange(l.Depth, 3..6, 12..0)),
        new(RoomType.GremlinParty, (l, r) => RequireNoUpStairs(l, r) * RequireDepth(l, 2) * 10),
        new(RoomType.GremlinPartyBig, (l, r) => RequireNoUpStairs(l, r) * RequireSize(r, 16) * RequireDepth(l, 4) * 5),
    ];

    static void AssignRoomTypes(LevelGenContext ctx)
    {
        var level = ctx.level;
        
        List<int> eligible = [];
        for (int i = 0; i < level.Rooms.Count; i++)
            if ((level.Rooms[i].Flags & RoomFlags.Merged) == 0)
                eligible.Add(i);

        foreach (int i in eligible)
        {
            var room = level.Rooms[i];
            RoomRule[] shuffled = [.. RoomRules];
            _rng.Shuffle(shuffled);

            bool madeRoom = false;
            foreach (var rule in shuffled)
            {
                int chance = rule.Chance(level, room);
                if (chance <= 0) continue;
                int roll = Rn2(100);
                if (roll >= chance) { Log($"Room {i} {rule.Type}: roll {roll} >= {chance}, skip"); continue; }
                room.Type = rule.Type;
                Log($"Room {i} {rule.Type}: roll {roll} < {chance}, assigned");
                madeRoom = true;
                break;
            }

            if (madeRoom) break;
        }

        // Shop: depth > 1, rn2(depth) < 4, one door only
        int depth = level.EffectiveDepth;
        if (depth > 1 && Rn2(depth) < 4)
        {
            var ordinary = level.Rooms.Where(r =>
                r.Type == RoomType.Ordinary && 
                !r.HasStairs &&
                r.Interior.Count < 64 &&
                r.Border.Count(p => level[p].Type == TileType.Door) == 1
            ).ToList();
            if (ordinary.Count > 0)
            {
                var shop = Pick(ordinary);
                shop.Type = RoomType.Shop;
                shop.Flags |= RoomFlags.Lit;
                Log($"Shop assigned");
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
                case RoomType.GremlinParty:
                    FillGremlinParty(ctx, room, small: true);
                    break;
                case RoomType.GremlinPartyBig:
                    FillGremlinParty(ctx, room, small: false);
                    break;
                case RoomType.Shop:
                    FillShop(ctx, room);
                    break;
                default:
                    FillOrdinaryRoom(ctx, room);
                    break;
            }
        }
    }

    // NH rules from mklev.c for OROOM
    static void PopulateCave(LevelGenContext ctx, int subtract = 0)
    {
        var level = ctx.level;
        int count = Math.Max(0, RnRange(4, 8 + level.EffectiveDepth / 2) - subtract);
        for (int i = 0; i < count; i++)
        {
            var pos = level.FindLocation(p => level.NoUnit(p) && !level[p].IsStairs);
            if (pos == null) break;
            MonsterSpawner.SpawnAndPlace(level, "OCAVE", null, true, pos);
        }
    }

    static void FillOrdinaryRoom(LevelGenContext ctx, Room room)
    {
        var level = ctx.level;

        // Forge: 1/40 chance per room
        if (Rn2(40) == 0)
        {
            var pos = ctx.FindLocationInRoom(room, p => level[p].IsPassable && !level.HasFeature(p) && !level[p].IsStairs);
            if (pos != null)
            {
                level.GetOrCreateState(pos.Value).Feature = new TileFeature("rune_forge", new('∆', ConsoleColor.Red), "a rune forge");
                Log($"forge: placed at {pos.Value}");
            }
        }

        // Monster: 1/3 chance
        if (Rn2(3) == 0)
        {
            var pos = ctx.FindLocationInRoom(room, p => level.NoUnit(p) && !level[p].IsStairs);
            if (pos != null)
            {
                var def = MonsterSpawner.PickMonster(level.EffectiveDepth, u?.CharacterLevel ?? 1);
                if (def != null)
                {
                    var mon = MonsterSpawner.SpawnAndPlace(level, "OROOM", null, true, pos);
                }
            }
        }
        
        // Traps: x = 12 - depth/6, while rn2(x)==0 place trap
        int trapChance = Math.Max(2, 12 - level.EffectiveDepth / 6);
        while (Rn2(trapChance) == 0)
        {
            var pos = ctx.FindLocationInRoom(room, p => level[p].IsPassable && !level[p].IsStairs && !level.Traps.ContainsKey(p));
            if (pos == null) break;
            
            Trap trap = Rn2(5) switch
            {
                0 => new WebTrap(level.EffectiveDepth),
                1 => new HoleTrap(TrapType.Trapdoor, level.EffectiveDepth),
                2 => new AmbushTrap(level.EffectiveDepth),
                _ => new PitTrap(level.EffectiveDepth),
            };
            level.Traps[pos.Value] = trap;
            Log($"trapgen: placed {trap.Type} at {pos.Value}");
        }
        
        // Gold: 1/3 chance
        if (Rn2(3) == 0)
        {
            var pos = ctx.FindLocationInRoom(room, p => level[p].IsPassable && !level.HasFeature(p));
            if (pos != null)
            {
                int amount = 1 + (Rn2(level.EffectiveDepth + 2) + 1) * (Rn2(30) + 1);
                level.PlaceItem(Item.Create(MiscItems.SilverCrest, amount), pos.Value);
            }
        }
        
        // Items: 1/3 chance for first, then 1/5 for each additional
        if (Rn2(3) == 0)
        {
            PlaceRoomItem(ctx, room);
            while (Rn2(5) == 0)
                PlaceRoomItem(ctx, room);
        }
    }
    
    static void FillShop(LevelGenContext ctx, Room room)
    {
        var level = ctx.level;
        
        HashSet<Pos> shopTiles = [.. room.Interior];

        var doorPos = room.Border.FirstOrDefault(p => level[p].Type == TileType.Door);

        if (doorPos != default)
        {
            var block = doorPos.CardinalNeighbours().First(shopTiles.Contains);
            Pos inwards = block - doorPos;
            Pos home;

            var ortho = inwards.Ortho;
            if (shopTiles.Contains(block + ortho))
                home = block + ortho;
            else
                home = block - ortho;

            // Find interior tile adjacent to door
            var shk = Monster.Spawn(EconomySystem.Shopkeeper, "shop");
            var fact = shk.AddFact(ShopkeeperBrick.Instance);
            var state = fact.As<ShopState>();
            state.Type = ShopTypes.Roll();
            shk.ProperName = ShopTypes.PickName(state.Type);
            state.Block = block;
            state.Door = doorPos;
            state.Home = home;
            state.Shopkeeper = shk;
            state.Room = room;
            level.PlaceUnit(shk, home);
            level.GetOrCreateState(doorPos).Door = DoorState.Closed;

            shopTiles.Remove(block);
            for (int i = 1; i < 100; i++)
            {
                Pos p = block + ortho * i;
                if (!shopTiles.Remove(p)) break;
            }
            for (int i = 1; i < 100; i++)
            {
                Pos p = block - ortho * i;
                if (!shopTiles.Remove(p)) break;
            }

            // Spawn items
            foreach (var p in shopTiles)
            {
                var item = ItemGen.GenerateForShop(state.Type, ctx.level.EffectiveDepth);
                if (item == null) continue;
                state.Stock[item] = new();
                ctx.level.PlaceItem(item, p);
            }

            room.Resident = shk;

            // Shop walls are undiggable
            foreach (var p in room.Border)
                level.GetOrCreateState(p).Undiggable = true;
        }
    }
    
    static void PlaceRoomItem(LevelGenContext ctx, Room room)
    {
        var pos = ctx.FindLocationInRoom(room, p => ctx.level[p].IsPassable && !ctx.level.HasFeature(p));
        if (pos == null) return;
        
        var item = ItemGen.GenerateRandomItem(ctx.level.EffectiveDepth);
        if (item == null) return;
        
        ctx.level.PlaceItem(item, pos.Value);
        Log($"objgen: placed {item.DisplayName} at {pos.Value}");
    }

    static void FillGoblinNest(LevelGenContext ctx, Room room)
    {
        // Find center of room
        int cx = (int)room.Interior.Average(p => p.X);
        int cy = (int)room.Interior.Average(p => p.Y);
        Pos center = new(cx, cy);
        
        // Scale goblin count by depth: 3 at D1, up to 8 at D6+
        int count = Math.Min(3 + ctx.level.EffectiveDepth, 8);
        
        MonsterDef[] pool = [Goblins.Warrior, Goblins.Warrior, Goblins.Warrior, Goblins.Warrior,
                             Goblins.Chef, Goblins.WarChanter, Goblins.Pyro, Goblins.Warrior];
        
        foreach (var dir in Pos.AllDirs)
        {
            if (count-- <= 0) break;
            Pos p = center + dir;
            if (!room.Interior.Contains(p)) continue;
            if (ctx.level.UnitAt(p) != null) continue;
            
            var def = pool.Pick();
            MonsterSpawner.SpawnAndPlace(ctx.level, "goblin nest", def, false, p, true);
        }
    }

    static void FillGremlinParty(LevelGenContext ctx, Room room, bool small)
    {
        void Place(MonsterDef def)
        {
            if (ctx.level.FindLocationInRoom(room, ctx.level.NoUnit) is { } pos)
                MonsterSpawner.SpawnAndPlace(ctx.level, "gremlin party", def, false, pos, true);
        }

        int total = small ? RnRange(2, 5) : RnRange(5, 12);
        int drunk = small ? 1 : 5;

        for (int i = 0; i < drunk; i++)
            Place(Gremlins.VeryDrunkJinkin);

        MonsterDef[] others = [Gremlins.Mitflit, Gremlins.Pugwampi, Gremlins.Jinkin];
        for (int i = drunk; i < total; i++)
            Place(others.Pick());
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
            // dNH formula: rnd(1+depth) < 11 && rn2(77)
            if (Rn1(1, ctx.level.EffectiveDepth + 1) < 11 && Rn2(77) != 0)
                room.Flags |= RoomFlags.Lit;
            ctx.level.Rooms.Add(room);
            RenderRoom(ctx.level, room);
        }
    }

    public static void RenderRoom(Level level, Room room)
    {
        foreach (Pos p in room.Border)
        {
            if (level[p].Type == TileType.Door) continue;
            level.Set(p, TileType.Wall);
            level.GetOrCreateState(p).Room = room;
        }
        foreach (Pos p in room.Interior)
        {
            level.Set(p, TileType.Floor);
            level.GetOrCreateState(p).Room = room;
        }
    }
}


public class LevelGenContext(TextWriter? log)
{
    public uint[] occupied = new uint[80];
    public required Level level;
    public bool NoSpawns;
    public bool NoRoomAssignment;
    public bool WantsRiver;
    public TileType WallTile = TileType.Rock;
    public TileType FloorTile = TileType.Floor;
    public bool Joined = true;

    public Pos? FindStairsLocation()
    {
        if (level.Rooms.Count > 0)
        {
            var r = FindRoom(r => !r.HasStairs) ?? PickRoom();
            return FindLocationInRoom(r, p => !level.HasFeature(p) && !level[p].IsStructural);
        }
        return level.FindLocation(p => !level.HasFeature(p) && !level[p].IsStructural && !level[p].IsStairs);
    }
    public List<RoomStamp> Stamps = [];

    public void Log(string str) => log?.WriteLine(str);

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

    internal Pos? FindLocationInRoom(Room room, Func<Pos, bool> predicate, int maxAttempts = 100)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            Pos p = LevelGen.RandomInterior(room);
            if (predicate(p)) return p;
        }
        return null;

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