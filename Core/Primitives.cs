using System.Collections;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;

namespace Pathhack.Core;

public readonly record struct Pos(int X, int Y) : IFormattable
{
    public static readonly Pos Zero = new(0, 0);
    public static readonly Pos Invalid = new(-1, -1);

    public static readonly Pos N = new(0, -1);
    public static readonly Pos S = new(0, 1);
    public static readonly Pos E = new(1, 0);
    public static readonly Pos W = new(-1, 0);
    public static readonly Pos NE = new(1, -1);
    public static readonly Pos NW = new(-1, -1);
    public static readonly Pos SE = new(1, 1);
    public static readonly Pos SW = new(-1, 1);

    public static readonly Pos[] AllDirs = [N, NE, E, SE, S, SW, W, NW];
    public static readonly Pos[] CardinalDirs = [N, E, S, W];

    // For each direction, the 5 "forward" neighbors to check when running (excludes behind + behind-diagonals)
    public static readonly Dictionary<Pos, Pos[]> ForwardNeighbours = new()
    {
        [N]  = [NW, N, NE, W, E],
        [NE] = [N, NE, E, NW, SE],
        [E]  = [NE, E, SE, N, S],
        [SE] = [E, SE, S, NE, SW],
        [S]  = [SE, S, SW, E, W],
        [SW] = [S, SW, W, SE, NW],
        [W]  = [SW, W, NW, S, N],
        [NW] = [W, NW, N, SW, NE],
    };

    public bool IsValid => X >= 0 && Y >= 0;

    public override string ToString() => $"({X},{Y})";

    public string ToString(string? format, IFormatProvider? provider)
    {
        if (format == "c")
        {
            const string xChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            char xc = xChars[X % xChars.Length];
            char yc = (char)('0' + (Y % 10));
            return $"{xc}{yc}";
        }
        return ToString();
    }

    public static Pos operator +(Pos a, Pos b) => new(a.X + b.X, a.Y + b.Y);
    public static Pos operator -(Pos a, Pos b) => new(a.X - b.X, a.Y - b.Y);
    public static Pos operator *(Pos p, int s) => new(p.X * s, p.Y * s);
    public static Pos operator -(Pos p) => new(-p.X, -p.Y);

    public int ManhattanDist(Pos other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
    public int ChebyshevDist(Pos other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
    public int EuclideanDistSq(Pos other) => (X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y);
    public Pos Signed => new(Math.Sign(X), Math.Sign(Y));
    public bool IsCompassFrom(Pos other)
    {
        int dx = X - other.X, dy = Y - other.Y;
        return dx == 0 || dy == 0 || Math.Abs(dx) == Math.Abs(dy);
    }

    internal IEnumerable<Pos> CardinalNeighbours()
    {
        yield return new(X, Y - 1);
        yield return new(X + 1, Y);
        yield return new(X, Y + 1);
        yield return new(X - 1, Y);
    }
    internal IEnumerable<Pos> DiagonalNeighbours()
    {
        yield return new(X + 1, Y - 1);
        yield return new(X + 1, Y + 1);
        yield return new(X - 1, Y + 1);
        yield return new(X - 1, Y - 1);
    }
    internal IEnumerable<Pos> Neighbours()
    {
        foreach (var d in AllDirs) yield return this + d;
    }
}

public readonly record struct Rect(int X, int Y, int W, int H)
{
    public Pos Min => new(X, Y);
    public Pos Max => new(X + W, Y + H);
    public Pos Center => new(X + W / 2, Y + H / 2);
    public int Area => W * H;

    public bool Contains(Pos p) => p.X >= X && p.X < X + W && p.Y >= Y && p.Y < Y + H;
    public bool Contains(Rect r) => r.X >= X && r.Y >= Y && r.X + r.W <= X + W && r.Y + r.H <= Y + H;
    public bool Overlaps(Rect r) => X < r.X + r.W && X + W > r.X && Y < r.Y + r.H && Y + H > r.Y;

    public IEnumerable<Pos> All()
    {
        for (int py = Y; py < Y + H; py++)
            for (int px = X; px < X + W; px++)
                yield return new(px, py);
    }

    public IEnumerable<Pos> Interior()
    {
        for (int py = Y + 1; py < Y + H - 1; py++)
            for (int px = X + 1; px < X + W - 1; px++)
                yield return new(px, py);
    }

    public IEnumerable<Pos> Border()
    {
        for (int px = X; px < X + W; px++) yield return new(px, Y);
        for (int px = X; px < X + W; px++) yield return new(px, Y + H - 1);
        for (int py = Y + 1; py < Y + H - 1; py++) yield return new(X, py);
        for (int py = Y + 1; py < Y + H - 1; py++) yield return new(X + W - 1, py);
    }

    public static Rect FromMinMax(Pos min, Pos max) => new(min.X, min.Y, max.X - min.X, max.Y - min.Y);
}

public sealed class TileBitset(int width, int height) : IEnumerable<Pos>, IDisposable
{
    readonly uint[] _bits = new uint[width];
    public int Width => width;
    public int Height => height;

    public bool this[Pos p]
    {
        get => (_bits[p.X] & (1u << p.Y)) != 0;
        set
        {
            if (value) _bits[p.X] |= 1u << p.Y;
            else _bits[p.X] &= ~(1u << p.Y);
        }
    }

    public void Clear() => Array.Clear(_bits);

    private static readonly TileBitset _pooled = new(80, 21);
    internal static TileBitset GetPooled()
    {
        // TODO: pooling if we need it
        return new(80, 21);
    }

    public IEnumerator<Pos> GetEnumerator()
    {
        for (int x = 0; x < _bits.Length; x++)
        {
            uint col = _bits[x];
            if (col == 0) continue;
            for (int y = 0; y < height; y++)
                if ((col & (1u << y)) != 0) yield return new(x, y);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Later, return to pool
    public void Dispose() { }

    internal void Set(TileBitset o)
    {
        for (int x = 0; x < width; x++)
            _bits[x] |= o._bits[x];
    }
}
