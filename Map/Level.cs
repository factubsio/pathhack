namespace Pathhack.Map;

// TileType, TileFlags, and TileInfo.DefaultFlags must be kept in sync
public enum TileType : ushort
{
    Rock,
    Floor,
    Wall,
    Corridor,
    Door,
    StairsUp,
    StairsDown,
    BranchUp,
    BranchDown,
}

// TileType, TileFlags, and TileInfo.DefaultFlags must be kept in sync
[Flags]
public enum TileFlags : ushort
{
    None = 0,
    Passable = 1 << 0,
    Diggable = 1 << 1,
    Structural = 1 << 2,
}

// TileType, TileFlags, and TileInfo.DefaultFlags must be kept in sync
public static class TileInfo
{
    public static TileFlags DefaultFlags(TileType type) => type switch
    {
        TileType.Rock => TileFlags.Diggable,
        TileType.Floor => TileFlags.Passable,
        TileType.Wall => TileFlags.None | TileFlags.Structural,
        TileType.Corridor => TileFlags.Passable,
        TileType.Door => TileFlags.Passable | TileFlags.Structural,
        TileType.StairsUp => TileFlags.Passable | TileFlags.Structural,
        TileType.StairsDown => TileFlags.Passable | TileFlags.Structural,
        TileType.BranchUp => TileFlags.Passable | TileFlags.Structural,
        TileType.BranchDown => TileFlags.Passable | TileFlags.Structural,
        _ => TileFlags.None,
    };
}

public enum DoorState : byte
{
    Closed,
    Open,
    Locked,
    Broken,
    None, // doorway with no door - converted to floor after levelgen
}

public record struct TileMemory(Tile Tile, DoorState Door, Item? TopItem);

public class TileFeature(string id)
{
    public string Id => id;
}

public class DummyCellDef : BaseDef
{
    public static readonly DummyCellDef Instance = new();
}

public class CellFx
{
    public virtual StackMode Stack => StackMode.Extend;

    public virtual void OnSpawn(IUnit unit) {}
    public virtual void OnDespawn(IUnit unit) {}
    public virtual void OnRoundStart(IUnit unit) {}
    public virtual void OnRoundEnd(IUnit unit) {}
    public virtual void OnEnter(IUnit unit) {}
    public virtual void OnExit(IUnit unit) {}
}

public record class CellFxFact(CellFx Fx, int ExpiresAt);

public class CellFxList
{
    public List<(CellFx Fx, int ExpiresAt)> Effects = [];
    private int FxCount = 0;

    public void AddFx(CellFx fx, int duration)
    {
        int expireAt = g.CurrentRound + duration;
        var current = Effects.FirstOrDefault(x => x.Fx == fx);
        if (current.Fx != null)
        {
            current.ExpiresAt = expireAt;
        }
        else
        {
            Effects.Add((fx, expireAt));
            FxCount++;
        }
    }

    public bool Tick()
    {
        Effects.RemoveAll(fx => fx.ExpiresAt <= g.CurrentRound);
        return Effects.Count > 0;
    }


}

public class CellState
{
    public DoorState Door;
    public IUnit? Unit;
    public Room? Room;
    public List<Item>? Items;
    public string? Message;
    internal TileFeature? Feature;
    public CellFxList? Fx = null;
}

public readonly record struct Tile(TileType Type, TileFlags Flags)
{
    public bool IsPassable => (Flags & TileFlags.Passable) != 0;
    public bool IsDiggable => (Flags & TileFlags.Diggable) != 0;
    public bool IsStructural => (Flags & TileFlags.Structural) != 0;

    public bool IsDoor => Type == TileType.Door;
    public bool IsStairs => Type is TileType.StairsUp or TileType.StairsDown or TileType.BranchUp or TileType.BranchDown;


    public static Tile From(TileType type) => new(type, TileInfo.DefaultFlags(type));
}

public enum BranchDir { Down, Up }

public static class BranchExt
{
    public static BranchDir Reversed(this BranchDir dir) => dir == BranchDir.Down ? BranchDir.Up : BranchDir.Down;
}

public record class Branch(string Id, string Name, int MaxDepth, ConsoleColor Color = ConsoleColor.White, BranchDir Dir = BranchDir.Down)
{
    public List<ResolvedLevel> ResolvedLevels { get; init; } = [];
    public int? EntranceDepthInParent { get; init; }
}

public readonly record struct LevelId(Branch Branch, int Depth)
{
    public static LevelId operator +(LevelId id, int delta) => new(id.Branch, id.Depth + delta);
    public static LevelId operator -(LevelId id, int delta) => new(id.Branch, id.Depth - delta);

    public override string ToString() => $"{Branch.Name}/{Depth}";

}

public enum RoomType : byte
{
    Ordinary,
    GoblinNest,
    GremlinParty,
    GremlinPartyBig,
}

[Flags]
public enum RoomFlags : byte
{
    None = 0,
    Lit = 1 << 0,
    NoConnect = 1 << 1,
    Merged = 1 << 2,
    HasStairs = 1 << 3,
}

public record struct RoomStamp
{
    public Rect? Bounds { get; init; }
    public HashSet<Pos>? Tiles { get; init; }
    
    public RoomStamp(Rect bounds) { Bounds = bounds; }
    public RoomStamp(HashSet<Pos> tiles) { Tiles = tiles; }
}

public record class Room(List<Pos> Border, List<Pos> Interior, RoomType Type = RoomType.Ordinary)
{
    public RoomFlags Flags { get; set; }
    public bool Lit => (Flags & RoomFlags.Lit) != 0;
    public bool HasStairs => (Flags & RoomFlags.HasStairs) != 0;
    public bool Entered { get; set; }
    public Rect? Bounds { get; init; }
    
    public Pos RandomInterior() => Interior.Pick();
    
    public static Room FromStamp(RoomStamp stamp, RoomType type = RoomType.Ordinary)
    {
        if (stamp.Tiles != null)
        {
            List<Pos> border = [];
            List<Pos> interior = [];
            foreach (var p in stamp.Tiles)
            {
                bool edge = false;
                foreach (var d in Pos.CardinalDirs)
                    if (!stamp.Tiles.Contains(p + d)) { edge = true; break; }
                (edge ? border : interior).Add(p);
            }
            return new(border, interior, type);
        }
        return new([..stamp.Bounds!.Value.Border()], [..stamp.Bounds!.Value.Interior()], type) { Bounds = stamp.Bounds };
    }
}

public class Level(LevelId id, int width, int height)
{
    public LevelId Id => id;
    public Branch Branch => id.Branch;
    public int Depth => id.Depth;
    public int Width => width;
    public int Height => height;
    readonly Tile[] _tiles = new Tile[width * height];
    readonly CellState[] _state = new CellState[width * height];
    readonly TileBitset _los = new(width, height);
    readonly TileBitset _lit = new(width, height);
    readonly TileBitset _visible = new(width, height);
    readonly TileBitset _seen = new(width, height);
    readonly TileMemory?[] _memory = new TileMemory?[width * height];
    public TileBitset LOS => _los;
    public TileBitset Lit => _lit;
    private readonly List<IUnit> Units = [];
    public IEnumerable<IUnit> LiveUnits => Units.Where(u => !u.IsDead);
    public readonly Dictionary<Pos, Trap> Traps = [];
    public List<Room> Rooms { get; } = [];
    public Pos? StairsUp { get; set; }
    public Pos? StairsDown { get; set; }
    public Pos? BranchUp { get; set; }
    public Pos? BranchDown { get; set; }
    public LevelId? BranchUpTarget { get; set; }
    public LevelId? BranchDownTarget { get; set; }
    
    public long LastExitTurn { get; set; }
    public bool NoInitialSpawns;
    public string? FirstIntro;
    public string? ReturnIntro;
    public int GeometryVersion;

    public void Spill(IEnumerable<Pos> to, CellFx fx, int duration)
    {
        foreach (var pos in to)
        {
            GetOrCreateState(pos).Fx ??= new();
            GetOrCreateState(pos).Fx!.AddFx(fx, duration);
        }
    }

    public bool UnderConstruction = true;
    public void InvalidateGeometry()
    {
        if (!UnderConstruction) GeometryVersion++;
    }

    public bool InBounds(Pos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

    public bool IsVisible(Pos p) => _visible[p];
    public void SetVisible(Pos p) => _visible[p] = true;
    public void ClearVisible() => _visible.Clear();

    public bool HasLOS(Pos p) => _los[p];
    public void SetLOS(Pos p) => _los[p] = true;
    public void ClearLOS() => _los.Clear();

    public bool IsLit(Pos p) => _lit[p];
    public void SetLit(Pos p) => _lit[p] = true;
    public void ClearLit() => _lit.Clear();

    public bool WasSeen(Pos p) => _seen[p];
    public TileMemory? GetMemory(Pos p) => _memory[p.Y * Width + p.X];

    public void UpdateMemory(Pos p)
    {
        _seen[p] = true;
        var items = ItemsAt(p);
        Item? top = items.Count > 0 ? items[^1] : null;
        _memory[p.Y * Width + p.X] = new TileMemory(this[p], GetState(p)?.Door ?? DoorState.Closed, top);
    }

    public bool IsOpaque(Pos p)
    {
        Tile t = this[p];
        return t.Type switch
        {
            TileType.Rock or TileType.Wall => true,
            TileType.Door => IsDoorClosed(p),
            _ => false,
        };
    }

    public Tile this[Pos p]
    {
        get => _tiles[p.Y * Width + p.X];
        set => _tiles[p.Y * Width + p.X] = value;
    }

    public void Set(Pos p, TileType type)
    {
        this[p] = Tile.From(type);

        switch (type)
        {
            case TileType.StairsUp: StairsUp = p; break;
            case TileType.StairsDown: StairsDown = p; break;
            case TileType.BranchUp: BranchUp = p; break;
            case TileType.BranchDown: BranchDown = p; break;
        }

        if (this[p].IsStairs)
        {
            var room = RoomAt(p);
            if (room != null)
                room.Flags |= RoomFlags.HasStairs;
        }

        InvalidateGeometry();
    }

    public CellState? GetState(Pos p) => _state[p.Y * Width + p.X];
    public CellState GetOrCreateState(Pos p)
    {
        ref CellState s = ref _state[p.Y * Width + p.X];
        return s ??= new CellState();
    }

    public IUnit? UnitAt(Pos p)
    {
        var unit = _state[p.Y * Width + p.X]?.Unit;
        return unit is { IsDead: false } ? unit : null;
    }
    public Room? RoomAt(Pos p) => _state[p.Y * Width + p.X]?.Room;

    public void PlaceUnit(IUnit Unit, Pos p)
    {
        Unit.Pos = p;
        Units.Add(Unit);
        GetOrCreateState(p).Unit = Unit;
    }

    public void MoveUnit(IUnit Unit, Pos to)
    {
        if (Unit.TrappedIn is {} trappedIn)
        {
            if (trappedIn.TryEscape(Unit))
                Unit.TrappedIn = null;

            Unit.EscapeAttempts++;
            return;
        }

        if (UnitAt(Unit.Pos) == Unit)
            GetOrCreateState(Unit.Pos).Unit = null;
        Unit.Pos = to;
        GetOrCreateState(to).Unit = Unit;
        Unit.Energy -= Unit.LandMove.Value;

        if (Traps.TryGetValue(to, out var trap))
        {
            if (trap.Trigger(Unit, null))
            {
                using var bitset = TileBitset.GetPooled();
                FovCalculator.ScanShadowcast(lvl, bitset, to, 80, false);
                foreach (var u in bitset.Select(UnitAt))
                {
                    if (u == null) continue;

                    if (u.Pos.ChebyshevDist(to) < 4 || lvl.IsLit(to))
                        u.ObserveTrap(trap);
                }
            }
        }
    }

    public void RemoveUnit(IUnit unit)
    {
        if (UnitAt(unit.Pos) == unit)
            GetOrCreateState(unit.Pos).Unit = null;
        Units.Remove(unit);
    }

    public bool IsDoorClosed(Pos p) => IsDoor(p) && GetState(p) is { } s && (s.Door == DoorState.Closed || s.Door == DoorState.Locked);
    public bool IsDoorPassable(Pos p) => IsDoor(p) && GetState(p) is { } s && (s.Door == DoorState.Open || s.Door == DoorState.Broken);
    public bool IsDoorLocked(Pos p) => IsDoor(p) && GetState(p) is { } s && s.Door == DoorState.Locked;
    public bool IsDoorOpen(Pos p) => IsDoor(p) && GetState(p) is { } s && s.Door == DoorState.Open;
    public bool IsDoorBroken(Pos p) => IsDoor(p) && GetState(p) is { } s && s.Door == DoorState.Broken;

    public bool HasFeature(Pos p) => GetState(p)?.Feature != null;
    public bool HasFeature(Pos p, string id) => GetState(p)?.Feature?.Id == id;

    public bool IsDoor(Pos p) => this[p].Type == TileType.Door;

    public bool CanMoveTo(Pos from, Pos to, IUnit? who = null)
    {
        Tile t = this[to];
        if (IsDoor(to) && IsDoorClosed(to)) return false;
        if (!t.IsPassable) return false;

        // diagonal movement through doors blocked
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx != 0 && dy != 0) // diagonal
        {
            bool fromDoor = from.IsValid && this[from].IsDoor;
            if (t.Type == TileType.Door || fromDoor)
            {
                Log.Write($"blocking movement: {from} -> {to}: fd:{fromDoor}, td:{t.Type == TileType.Door}");
                return false;
            }
        }

        // TODO: phasing, swimming, flying, etc. based on who

        return true;
    }

    public Pos? FindTile(Func<Pos, bool> predicate)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Pos p = new(x, y);
                if (predicate(p)) return p;
            }
        return null;
    }

    public Pos? FindTile(TileType type)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (_tiles[y * Width + x].Type == type)
                    return new(x, y);
        return null;
    }

    public Pos? FindLocation(Func<Pos, bool> predicate, int maxAttempts = 100)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            Room room = Rooms.Pick();
            Pos p = room.RandomInterior();
            if (predicate(p)) return p;
        }
        return null;
    }

    public Pos? FindLocationInRoom(Room room, Func<Pos, bool> predicate, int maxAttempts = 100)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            Pos p = room.RandomInterior();
            if (predicate(p)) return p;
        }
        return null;
    }

    internal bool NoUnit(Pos pos) => UnitAt(pos) == null;

    public IReadOnlyList<Item> ItemsAt(Pos p) => GetState(p)?.Items ?? (IReadOnlyList<Item>)[];

    public void PlaceItem(Item item, Pos p)
    {
        var state = GetOrCreateState(p);
        state.Items ??= [];
        
        foreach (var existing in state.Items)
        {
            if (existing.CanMerge(item))
            {
                existing.MergeFrom(item);
                return;
            }
        }
        state.Items.Add(item);
    }

    public bool RemoveItem(Item item, Pos p)
    {
        var items = GetState(p)?.Items;
        return items?.Remove(item) ?? false;
    }

    internal void PlaceDoor(Pos pos, DoorState state = DoorState.Closed)
    {
        Set(pos, TileType.Door);
        GetOrCreateState(pos).Door = state;
    }

    internal IEnumerable<Pos> CollectLine(Pos src, Pos dir, int range)
    {
        for (int i = 1; i <= range; i++)
        {
            src += dir;
            if (InBounds(src)) yield return src;
        }
    }
    internal IEnumerable<T> CollectLine<T>(Pos src, Pos dir, int range, Func<Pos, T?> func)
    {
        for (int i = 1; i <= range; i++)
        {
            src += dir;
            if (InBounds(src) && func(src) is {} val) yield return val;
        }
    }
    internal TileBitset CollectCircle(Pos src, int range, bool includeWalls)
    {
        using var tiles = TileBitset.GetPooled();
        FovCalculator.ScanShadowcast(this, tiles, src, range, includeWalls);
        tiles[src] = false;
        return tiles;
    }

    internal TileBitset CollectCone(Pos origin, Pos dir, int radius)
    {
        using var tiles = TileBitset.GetPooled();
        var octants = OctantsForDirection(dir);
        Log.Verbose("cone", "CollectCone origin=({0},{1}) dir=({2},{3}) radius={4} octants=[{5}]", origin.X, origin.Y, dir.X, dir.Y, radius, string.Join(",", octants));
        foreach (int oct in octants)
            FovCalculator.ScanOctant(this, tiles, origin, radius, oct, includeWalls: false);
        tiles[origin] = false;
        return tiles;
    }

    internal IEnumerable<T> CollectCone<T>(Pos origin, Pos dir, int radius, Func<Pos, T?> func)
    {
        foreach (var pos in CollectCone(origin, dir, radius))
            if (func(pos) is {} val) yield return val;
    }

    static int[] OctantsForDirection(Pos dir) => dir switch
    {
        { X: 1, Y: 0 } => [1, 2],   // E
        { X: 1, Y: 1 } => [0, 1],   // SE
        { X: 0, Y: 1 } => [7, 0],   // S
        { X: -1, Y: 1 } => [6, 7],  // SW
        { X: -1, Y: 0 } => [5, 6],  // W
        { X: -1, Y: -1 } => [4, 5], // NW
        { X: 0, Y: -1 } => [3, 4],  // N
        { X: 1, Y: -1 } => [2, 3],  // NE
        _ => [0, 1, 2, 3, 4, 5, 6, 7],
    };

    internal bool OpenDoor(Pos target)
    {
        CellState state = GetOrCreateState(target);
        if (state.Door == DoorState.Closed)
        {
            state.Door = DoorState.Open;
            InvalidateGeometry();
            return true;
        }
        return false;
    }

    internal void ReapDead() => Units.RemoveAll(x => x.IsDead);
    internal void SortUnitsByInitiative() => Units.Sort((a, b) => b.Initiative.CompareTo(a.Initiative));

    internal void AddFx<T>(Pos p, T fx, int endAt) where T : CellFx
    {
        var state = GetOrCreateState(p);
        state.Fx ??= new();
        state.Fx.AddFx(fx, endAt);
    }
}


public enum SpawnAt
{
    StairsUp,
    StairsDown,
    BranchUp,
    BranchDown,
    RandomLegal,
    RandomAny,
    Explicit,
}