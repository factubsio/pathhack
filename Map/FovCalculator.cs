using Pathhack.Core;
using Pathhack.Game;

namespace Pathhack.Map;

public static class FovCalculator
{
    const int MaxRadius = 33;

    static readonly byte[] CircleData = [
        /*  0*/ 1,1,
        /*  2*/ 2,2,1,
        /*  5*/ 3,3,2,1,
        /*  9*/ 4,4,4,3,2,
        /* 14*/ 5,5,5,4,3,2,
        /* 20*/ 6,6,6,5,5,4,2,
        /* 27*/ 7,7,7,6,6,5,4,2,
        /* 35*/ 8,8,8,7,7,6,6,4,2,
        /* 44*/ 9,9,9,9,8,8,7,6,5,3,
        /* 54*/ 10,10,10,10,9,9,8,7,6,5,3,
        /* 65*/ 11,11,11,11,10,10,9,9,8,7,5,3,
        /* 77*/ 12,12,12,12,11,11,10,10,9,8,7,5,3,
        /* 90*/ 13,13,13,13,12,12,12,11,10,10,9,7,6,3,
        /*104*/ 14,14,14,14,13,13,13,12,12,11,10,9,8,6,3,
        /*119*/ 15,15,15,15,14,14,14,13,13,12,11,10,9,8,6,3,
        /*135*/ 16,16,16,16,16,15,15,14,14,13,13,12,11,10,8,6,4,
        /*152*/ 17,17,17,17,17,16,16,16,15,15,14,13,12,11,10,9,7,4,
        /*170*/ 18,18,18,18,18,17,17,17,16,16,15,14,14,13,12,10,9,7,4,
        /*189*/ 19,19,19,19,19,18,18,18,17,17,16,16,15,14,13,12,11,9,7,4,
        /*209*/ 20,20,20,20,20,19,19,19,18,18,17,17,16,15,14,13,12,11,9,7,4,
        /*230*/ 21,21,21,21,21,20,20,20,19,19,19,18,17,17,16,15,14,13,11,10,7,4,
        /*252*/ 22,22,22,22,22,21,21,21,21,20,20,19,19,18,17,16,15,14,13,12,10,8,4,
        /*275*/ 23,23,23,23,23,22,22,22,22,21,21,20,20,19,18,18,17,16,15,13,12,10,8,4,
        /*299*/ 24,24,24,24,24,23,23,23,23,22,22,21,21,20,20,19,18,17,16,15,14,12,10,8,4,
        /*324*/ 25,25,25,25,25,25,24,24,24,23,23,23,22,21,21,20,19,19,18,17,15,14,12,11,8,5,
        /*350*/ 26,26,26,26,26,26,25,25,25,24,24,24,23,23,22,21,21,20,19,18,17,16,14,13,11,8,5,
        /*377*/ 27,27,27,27,27,27,26,26,26,25,25,25,24,24,23,23,22,21,20,19,18,17,16,15,13,11,8,5,
        /*405*/ 28,28,28,28,28,28,27,27,27,27,26,26,25,25,24,24,23,22,22,21,20,19,18,16,15,13,11,9,5,
        /*434*/ 29,29,29,29,29,29,28,28,28,28,27,27,26,26,25,25,24,24,23,22,21,20,19,18,17,15,13,11,9,5,
        /*464*/ 30,30,30,30,30,30,29,29,29,29,28,28,28,27,27,26,25,25,24,23,23,22,21,20,18,17,15,14,12,9,5,
        /*495*/ 31,31,31,31,31,31,31,30,30,30,30,29,29,29,28,28,27,27,26,25,25,24,23,22,21,20,19,17,16,14,12,9,5,
        /*527*/ 32,32,32,32,32,32,32,31,31,31,31,30,30,30,29,29,28,28,27,27,26,25,24,23,22,21,20,19,18,16,14,12,9,5,
        /*560*/ 33,33,33,33,33,33,33,32,32,32,32,31,31,31,30,30,29,29,28,28,27,26,26,25,24,23,22,21,19,18,16,14,12,9,5,
        /*594*/ 34
    ];

    static readonly int[] CircleStart = [
        0,    // 0 - not used
        0,    // 1
        2,    // 2
        5,    // 3
        9,    // 4
        14,   // 5
        20,   // 6
        27,   // 7
        35,   // 8
        44,   // 9
        54,   // 10
        65,   // 11
        77,   // 12
        90,   // 13
        104,  // 14
        119,  // 15
        135,  // 16
        152,  // 17
        170,  // 18
        189,  // 19
        209,  // 20
        230,  // 21
        252,  // 22
        275,  // 23
        299,  // 24
        324,  // 25
        350,  // 26
        377,  // 27
        405,  // 28
        434,  // 29
        464,  // 30
        495,  // 31
        527,  // 32
        560,  // 33
    ];

    static bool InCircle(int col, int row, int radius)
    {
        if (radius < 1) return col == 0 && row == 0;
        if (radius > MaxRadius) radius = MaxRadius;
        int absRow = Math.Abs(row);
        if (absRow > radius) return false;
        int start = CircleStart[radius];
        return Math.Abs(col) <= CircleData[start + absRow];
    }

    private static Pos lastLosPov = Pos.Invalid;
    private static LevelId lastLosLevel = new();
    private static int lastLosGeometryVersion = -1;
    private static int lastLosRange = -1;

    public static void Compute(Level level, Pos origin, int lightRadius, TileBitset? moreLit = null)
    {
        Perf.Start();
        const int maxRange = 66;

        int losRange = u.Can("can_see") ? maxRange : 0;

        bool losDirty =
                lastLosPov != origin
                || lastLosLevel != level.Id
                || lastLosGeometryVersion != level.GeometryVersion
                || lastLosRange != losRange;
        if (losDirty)
        {
            level.ClearLOS();
            if (losRange > 0)
                ScanShadowcast(level, level.LOS, origin, losRange);
            lastLosPov = origin;
            lastLosGeometryVersion = level.GeometryVersion;
            lastLosLevel = level.Id;
            lastLosRange = losRange;
        }

        level.ClearLit();
        foreach (var room in level.Rooms)
        {
            if (!room.Lit) continue;
            foreach (var p in room.Interior)
                level.SetLit(p);
            foreach (var p in room.Border)
                level.SetLit(p);
        }

        if (level.Outdoors)
        {
            for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                Pos p = new(x, y);
                var type = level[p].Type;

                // Should this be water too? do we want a better way of tracking tiles that are actually "outdoors"?
                if (type == TileType.Grass)
                    level.SetLit(p);
            }
        }

        if (moreLit != null) level.Lit.Set(moreLit);

        // todo other lights, for now we just use a light radius centered on the
        // player
        ScanShadowcast(level, level.Lit, origin, lightRadius);

        // Visiblity = lit & in_los, later telepathy, warning, etc (thoug we
        // need a separate bitmask or something since it shouldnt' show the tile
        // they are on?)
        level.ClearVisible();
        int visCount = 0;
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                Pos p = new(x, y);
                if (level.HasLOS(p) && level.IsLit(p))
                {
                    level.SetVisible(p);
                    level.UpdateMemory(p);
                    visCount++;
                }
            }
        }

        // Compute monster perception
        foreach (var unit in level.LiveUnits)
        {
            if (unit is not Monster m) continue;
            m.Perception = GetAwareness(u, m).Perception;
        }

        Perf.Stop("FovCompute");
    }

    public static void ScanShadowcast(Level level, TileBitset target, Pos origin, int radius, bool includeWalls = true)
    {
        target[origin] = true;
        for (int octant = 0; octant < 8; octant++)
            ScanOctant(level, target, origin, radius, octant, 1, 0.0, 1.0, includeWalls);
    }

    internal static void ScanOctant(Level level, TileBitset target, Pos origin, int radius, int octant, int row = 1, double startSlope = 0.0, double endSlope = 1.0, bool includeWalls = true)
    {
        if (startSlope >= endSlope) return;

        for (int r = row; r <= radius; r++)
        {
            bool blocked = false;
            double newStart = startSlope;

            for (int col = (int)Math.Floor(r * startSlope); col <= (int)Math.Ceiling(r * endSlope); col++)
            {
                double leftSlope = (col - 0.5) / (r + 0.5);
                double rightSlope = (col + 0.5) / (r - 0.5);

                if (rightSlope < startSlope) continue;
                if (leftSlope > endSlope) break;

                Pos p = Transform(origin, col, r, octant);
                bool isOpaque = level.InBounds(p) && level.IsOpaque(p);
                if (InCircle(col, r, radius) && level.InBounds(p) && (includeWalls || !isOpaque))
                    target[p] = true;

                if (blocked)
                {
                    if (isOpaque)
                    {
                        newStart = rightSlope;
                    }
                    else
                    {
                        blocked = false;
                        startSlope = newStart;
                    }
                }
                else if (level.InBounds(p) && level.IsOpaque(p) && r < radius)
                {
                    blocked = true;
                    ScanOctant(level, target, origin, radius, octant, r + 1, startSlope, leftSlope, includeWalls);
                    newStart = rightSlope;
                }
            }

            if (blocked) break;
        }
    }

    static Pos Transform(Pos origin, int col, int row, int octant) => octant switch
    {
        0 => new(origin.X + col, origin.Y + row),
        1 => new(origin.X + row, origin.Y + col),
        2 => new(origin.X + row, origin.Y - col),
        3 => new(origin.X + col, origin.Y - row),
        4 => new(origin.X - col, origin.Y - row),
        5 => new(origin.X - row, origin.Y - col),
        6 => new(origin.X - row, origin.Y + col),
        7 => new(origin.X - col, origin.Y + row),
        _ => origin,
    };

    /// <summary>
    /// Bresenham line check. Returns true if no opaque tile blocks the path.
    /// Does not check endpoints.
    /// </summary>
    public static bool IsPathClear(Level level, Pos from, Pos to)
    {
        int x0 = from.X, y0 = from.Y;
        int x1 = to.X, y1 = to.Y;

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            // move before check so we skip the start point
            int e2 = err * 2;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }

            // reached destination (don't check endpoint)
            if (x0 == x1 && y0 == y1) return true;

            // blocked
            if (level.IsOpaque(new(x0, y0))) return false;
        }
    }
}
