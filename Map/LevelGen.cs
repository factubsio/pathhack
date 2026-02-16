using System.Security.Cryptography;
using System.Text;

namespace Pathhack.Map;

public static class LevelGen
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

    // 1/3 has door: 1/5 open, 1/6 locked, rest closed. null = no door.
    static DoorState RollDoorState()
    {
        if (Rn2(3) != 0) return DoorState.Broken;
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

            if (ctx.level.Rooms.Count == 0)
            {
                Log("PopulateCave (roomless)...");
                PopulateCave(ctx);
            }
            
            LogLevel(ctx.level);
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

    static void GenRoomsAndCorridors(LevelGenContext ctx, bool populate = true)
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

        Log($"Placed {ctx.Stamps.Count} rooms (target was {targetRooms})");

        // Render rooms to tiles
        Log("RenderRooms...");
        RenderRooms(ctx);

        // Note: room merging now happens during PlaceRoom, not here

        // Connect rooms with corridors
        Log("MakeCorridors...");
        MakeCorridors(ctx);
        Log("MakeCorridors done");

        // Rivers — defer until after room assignment so shops can be protected
        if (ForceRiver || (ctx.level.EffectiveDepth > 3 && Rn2(4) == 0))
            ctx.WantsRiver = true;

        if (ForceMiniVault || (ctx.level.EffectiveDepth >= 3 && Rn2(8) == 0))
        {
            Log("TryMiniVault...");
            TryMiniVault(ctx);
        }

        if (TestMode || !populate) return;
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
        new(RoomType.GoblinNest, (l, r) => RequireNoUpStairs(l, r) * RequireSize(r, 9) * MapRange(l.Depth, 1..6, 12..0)),
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
    static void PopulateCave(LevelGenContext ctx)
    {
        var level = ctx.level;
        int count = RnRange(4, 8 + level.EffectiveDepth / 2);
        for (int i = 0; i < count; i++)
        {
            var pos = level.FindLocation(p => level.NoUnit(p) && !level[p].IsStairs);
            if (pos == null) break;
            MonsterSpawner.SpawnAndPlace(level, "cave", null, true, pos, true);
        }
    }

    static void FillOrdinaryRoom(LevelGenContext ctx, Room room)
    {
        var level = ctx.level;
        
        // Monster: 1/3 chance
        if (Rn2(3) == 0)
        {
            var pos = ctx.FindLocationInRoom(room, p => level.NoUnit(p) && !level[p].IsStairs);
            if (pos != null)
            {
                var def = MonsterSpawner.PickMonster(level.EffectiveDepth, u?.CharacterLevel ?? 1);
                if (def != null)
                {
                    var mon = MonsterSpawner.SpawnAndPlace(level, "OROOM", null, true, pos, true);
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
            level.Set(p, TileType.Wall);
            level.GetOrCreateState(p).Room = room;
        }
        foreach (Pos p in room.Interior)
        {
            level.Set(p, TileType.Floor);
            level.GetOrCreateState(p).Room = room;
        }
    }

    static int RoomLeftX(Room room) => room.Bounds?.X ?? room.Border.Min(p => p.X);

    // Find a border tile suitable for a door (has diggable/corridor neighbor, not a corner)
    static List<(Pos pos, Pos outDir)> GetDoorCandidates(Level level, Room room)
    {
        List<(Pos, Pos)> candidates = [];
        foreach (var p in room.Border)
        {
            // Skip corners - tiles with 2+ non-room neighbors
            int wallNeighbors = 0;
            foreach (var d in Pos.CardinalDirs)
                if (!room.Interior.Contains(p + d) && !room.Border.Contains(p + d))
                    wallNeighbors++;
            if (wallNeighbors >= 2) continue;
            
            foreach (var dir in Pos.CardinalDirs)
            {
                var neighbor = p + dir;
                var tile = level[neighbor];
                if (tile.Type is TileType.Rock or TileType.Corridor)
                    candidates.Add((p, dir));
            }
        }
        return candidates;
    }

    // Pick door positions for connecting two rooms - random but biased toward facing direction
    static (Pos doorA, Pos doorB, Pos dir)? PickDoorPair(Level level, Room roomA, Room roomB)
    {
        var candA = GetDoorCandidates(level, roomA);
        var candB = GetDoorCandidates(level, roomB);
        if (candA.Count == 0 || candB.Count == 0) return null;

        // Compute rough direction from A to B using any interior point
        Pos centerA = roomA.Interior.Count > 0 ? roomA.Interior[0] : roomA.Border[0];
        Pos centerB = roomB.Interior.Count > 0 ? roomB.Interior[0] : roomB.Border[0];
        int towardX = Math.Sign(centerB.X - centerA.X);
        int towardY = Math.Sign(centerB.Y - centerA.Y);

        // Filter candidates facing roughly the right direction
        var goodA = candA.Where(c => c.outDir.X == towardX || c.outDir.Y == towardY).ToList();
        var goodB = candB.Where(c => c.outDir.X == -towardX || c.outDir.Y == -towardY).ToList();

        // Fall back to all candidates if no good ones
        if (goodA.Count == 0) goodA = candA;
        if (goodB.Count == 0) goodB = candB;

        var (doorA, dirA) = goodA[Rn2(goodA.Count)];
        var (doorB, _) = goodB[Rn2(goodB.Count)];

        return (doorA, doorB, dirA);
    }

    public static void MakeCorridors(LevelGenContext ctx)
    {
        List<Room> connectable = [.. ctx.level.Rooms
            .Where(r => (r.Flags & RoomFlags.NoConnect) == 0)
            .OrderBy(RoomLeftX)];

        if (connectable.Count < 2) return;

        // Union-find for connectivity
        int[] group = new int[connectable.Count];
        for (int i = 0; i < group.Length; i++) group[i] = i;
        int Find(int x) => group[x] == x ? x : group[x] = Find(group[x]);
        void Union(int a, int b) => group[Find(a)] = Find(b);

        // Note: merged rooms are now single Room objects, no pre-union needed

        LogVerbose($"MakeCorridors: {connectable.Count} rooms");

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
        int connectAttempts = 0;
        const int maxConnectAttempts = 100;
        while (any && connectAttempts++ < maxConnectAttempts)
        {
            any = false;
            for (int i = 0; i < connectable.Count; i++)
            {
                for (int j = 0; j < connectable.Count; j++)
                {
                    if (Find(i) != Find(j))
                    {
                        LogVerbose($"Room {i} and {j} not connected, joining");
                        if (TryDigCorridor(ctx, connectable[i], connectable[j]))
                            Union(i, j);
                        any = true;
                    }
                }
            }
        }
        if (connectAttempts >= maxConnectAttempts)
            Log($"WARNING: gave up connecting rooms after {maxConnectAttempts} attempts");

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
                if (TryDigCorridor(ctx, connectable[a], connectable[b], nxcor: true))
                    connected.Add(pair);
            }
        }
    }

    static bool TryDigCorridor(LevelGenContext ctx, Room roomA, Room roomB, bool nxcor = false)
    {
        var level = ctx.level;
        
        var pair = PickDoorPair(level, roomA, roomB);
        if (pair == null)
        {
            LogVerbose($"  No door candidates found");
            return false;
        }
        
        var (doorA, doorB, outDir) = pair.Value;
        int dx = outDir.X, dy = outDir.Y;
        
        // If outDir is zero (shouldn't happen), compute from positions
        if (dx == 0 && dy == 0)
        {
            dx = Math.Sign(doorB.X - doorA.X);
            dy = Math.Sign(doorB.Y - doorA.Y);
            if (dx == 0 && dy == 0) dy = 1;
        }

        LogVerbose($"TryDigCorridor doors {doorA.X},{doorA.Y} -> {doorB.X},{doorB.Y} dir {dx},{dy}");

        // Dig from just outside doorA to just outside doorB
        Pos org = new(doorA.X + dx, doorA.Y + dy);
        Pos dest = new(doorB.X - dx, doorB.Y - dy);
        
        // If dest is inside a room, adjust
        if (level[dest].Type is not TileType.Rock && level[dest].Type != TileType.Corridor)
            dest = doorB;

        List<Pos> dug = [];
        bool placedDoorA = false;
        int x = org.X, y = org.Y;
        int maxSteps = 500;

        while (maxSteps-- > 0)
        {
            // Randomly abandon non-essential corridors (creates dead ends)
            if (nxcor && Rn2(35) == 0)
            {
                LogVerbose($"  Randomly abandoned nxcor after {dug.Count} tiles");
                return false; // leave the partial corridor as a dead end
            }

            Tile t = level[new(x, y)];
            if (t.Type == TileType.Rock)
            {
                level.Set(new(x, y), TileType.Corridor);
                dug.Add(new(x, y));
                
                // Place doorA after first successful dig
                if (!placedDoorA)
                {
                    if (level[doorA].Type != TileType.Door && CanPlaceDoor(level, doorA))
                    {
                        level.PlaceDoor(doorA, RollDoorState());
                        placedDoorA = true;
                    }
                }
            }
            else if (!t.IsPassable)
            {
                LogVerbose($"  Failed at {x},{y} tile={t.Type}, undoing {dug.Count} tiles");
                UndoCorridorOrCupboard(level, dug, placedDoorA, doorA);
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
            LogVerbose($"  Dead end at {x},{y}, undoing {dug.Count} tiles");
            UndoCorridorOrCupboard(level, dug, placedDoorA, doorA);
            return false;
        }

        // Exceeded max steps - probably oscillating
        if (maxSteps <= 0)
        {
            LogVerbose($"  Exceeded max steps, undoing {dug.Count} tiles");
            UndoCorridorOrCupboard(level, dug, placedDoorA, doorA);
            return false;
        }

        // Place end door on success
        if (level[doorB].Type != TileType.Door && CanPlaceDoor(level, doorB))
            level.PlaceDoor(doorB, RollDoorState());

        LogVerbose($"  Success, dug {dug.Count} tiles");
        LogVerbose("");
        LogLevelVerbose(level);
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

    static void UndoCorridorOrCupboard(Level level, List<Pos> dug, bool placedDoorA, Pos doorA)
    {
        // 50% chance to leave a cupboard (door + 1 tile) if we dug anything and placed a door
        if (placedDoorA && dug.Count > 0 && Rn2(2) == 0)
        {
            // Keep first tile, undo the rest
            for (int i = 1; i < dug.Count; i++)
                level.Set(dug[i], TileType.Rock);
            return;
        }
        
        // Full undo
        foreach (Pos p in dug)
            level.Set(p, TileType.Rock);
        if (placedDoorA && level[doorA].Type == TileType.Door)
            level.Set(doorA, TileType.Wall);
    }

    static bool CanDig(Level level, int x, int y)
    {
        if (x <= 0 || x >= level.Width - 1 || y <= 0 || y >= level.Height - 1)
            return false;
        Tile t = level[new(x, y)];
        return t.Type is TileType.Rock or TileType.Corridor;
    }
}


public class LevelGenContext(TextWriter? log)
{
    public uint[] occupied = new uint[80];
    public required Level level;
    public bool NoSpawns;
    public bool NoRoomAssignment;
    public bool WantsRiver;

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