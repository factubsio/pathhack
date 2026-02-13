namespace Pathhack.Map;

public enum CaveAlgorithm { Worley, WorleyCavern, WorleyWarren, CA, Drunkard, BSP, Perlin, Circles, GrowingTree }

public enum WorleyMode { F2MinusF1, F1 }

public record WorleyConfig(int Seeds, double Threshold, bool Invert, WorleyMode Mode = WorleyMode.F2MinusF1, int MinSpacing = 0)
{
    public static readonly WorleyConfig Outdoor = new(30, 3.0, false, MinSpacing: 8);
    public static readonly WorleyConfig Cavern = new(40, 2.0, false);
    public static readonly WorleyConfig Warren = new(15, 4.0, false, WorleyMode.F1, MinSpacing: 10);

    // TODO: for gameplay, randomize config between two endpoints instead of sweep
    // e.g. seeds rn(30,45), threshold rn(1.5,2.5) — sweep 4+ devolves into single room
    public static WorleyConfig Sweep(WorleyConfig baseConfig)
    {
        int s = LevelGen.ParamSweep;
        if (s < 0) return baseConfig;
        return baseConfig with { Seeds = baseConfig.Seeds + s * 3, Threshold = baseConfig.Threshold + s * 0.3 };
    }
}

public static class CaveGen
{
    public static void GenerateWorley(LevelGenContext ctx, WorleyConfig? config = null)
    {
        config ??= WorleyConfig.Outdoor;
        var level = ctx.level;
        int width = level.Width, height = level.Height;

        // Fill rock
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            level.Set(new(x, y), TileType.Rock);

        // Scatter seed points
        List<Pos> seeds = [];
        int minDist = config.MinSpacing;
        for (int attempts = 0; seeds.Count < config.Seeds && attempts < config.Seeds * 20; attempts++)
        {
            Pos candidate = new(LevelGen.RnRange(2, width - 3), LevelGen.RnRange(2, height - 3));
            if (minDist > 0)
            {
                bool tooClose = false;
                foreach (var s in seeds)
                    if (candidate.ChebyshevDist(s) < minDist) { tooClose = true; break; }
                if (tooClose) continue;
            }
            seeds.Add(candidate);
        }

        // Debug: log seed positions
        ctx.Log($"Worley: {seeds.Count} seeds, threshold={config.Threshold}, mode={config.Mode}");
        foreach (var s in seeds)
            ctx.Log($"  seed at {s.X},{s.Y}");

        for (int y = 1; y < height - 1; y++)
        for (int x = 1; x < width - 1; x++)
        {
            double f1 = double.MaxValue, f2 = double.MaxValue;
            foreach (var s in seeds)
            {
                double d = Math.Sqrt((x - s.X) * (x - s.X) + (y - s.Y) * (y - s.Y));
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) { f2 = d; }
            }

            double value = config.Mode == WorleyMode.F1 ? f1 : f2 - f1;
            bool isFloor = config.Invert ? value > config.Threshold : value <= config.Threshold;

            // Debug: log a few tiles around first seed
            if (seeds.Count > 0 && Math.Abs(x - seeds[0].X) <= 5 && y == seeds[0].Y)
                ctx.Log($"  tile ({x},{y}) f1={f1:F2} value={value:F2} floor={isFloor}");

            if (isFloor)
                level.Set(new(x, y), TileType.Floor);
        }

        Wallify(level);
        EnsureConnectivity(ctx);
    }

    public static void GenerateCA(LevelGenContext ctx, double fillPct = 0.40, int smooth = 2)
    {
        var level = ctx.level;
        int width = level.Width, height = level.Height;

        // Fill rock
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            level.Set(new(x, y), TileType.Rock);

        // Random fill
        int limit = (int)((width - 2) * (height - 2) * fillPct);
        int count = 0;
        while (count < limit)
        {
            int x = LevelGen.RnRange(2, width - 3);
            int y = LevelGen.RnRange(1, height - 2);
            if (level[new(x, y)].Type == TileType.Rock)
            {
                level.Set(new(x, y), TileType.Floor);
                count++;
            }
        }

        // Pass one: birth/death
        for (int y = 1; y < height - 1; y++)
        for (int x = 2; x < width - 1; x++)
        {
            int n = CountNeighbors(level, x, y, TileType.Floor);
            if (n <= 2) level.Set(new(x, y), TileType.Rock);
            else if (n >= 5) level.Set(new(x, y), TileType.Floor);
        }

        // Pass two + smoothing (double-buffered)
        bool[,] buf = new bool[width, height];
        for (int pass = 0; pass < 1 + smooth; pass++)
        {
            int killThreshold = pass == 0 ? 5 : 3;
            for (int y = 1; y < height - 1; y++)
            for (int x = 2; x < width - 1; x++)
            {
                int n = CountNeighbors(level, x, y, TileType.Floor);
                bool kill = pass == 0 ? n == killThreshold : n < killThreshold;
                buf[x, y] = kill ? false : level[new(x, y)].Type == TileType.Floor;
            }
            for (int y = 1; y < height - 1; y++)
            for (int x = 2; x < width - 1; x++)
                level.Set(new(x, y), buf[x, y] ? TileType.Floor : TileType.Rock);
        }

        Wallify(level);
        EnsureConnectivity(ctx);
    }

    static int CountNeighbors(Level level, int x, int y, TileType type)
    {
        int count = 0;
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            if (level[new(x + dx, y + dy)].Type == type) count++;
        }
        return count;
    }

    public static void PostProcess(LevelGenContext ctx, bool connect = true)
    {
        Wallify(ctx.level);
        if (connect) EnsureConnectivity(ctx);
    }

    static void Wallify(Level level)
    {
        for (int y = 0; y < level.Height; y++)
        for (int x = 0; x < level.Width; x++)
        {
            Pos p = new(x, y);
            if (level[p].Type != TileType.Rock) continue;
            foreach (var d in Pos.AllDirs)
            {
                var n = p + d;
                if (level.InBounds(n) && level[n].Type == TileType.Floor)
                {
                    level.Set(p, TileType.Wall);
                    break;
                }
            }
        }

        // Remove walls with no cardinal wall/door neighbors (diagonal-only orphans)
        for (int y = 0; y < level.Height; y++)
        for (int x = 0; x < level.Width; x++)
        {
            Pos p = new(x, y);
            if (level[p].Type != TileType.Wall) continue;
            bool hasCardinalWall = false;
            foreach (var d in Pos.CardinalDirs)
            {
                var n = p + d;
                if (level.InBounds(n) && level[n].Type is TileType.Wall or TileType.Door)
                { hasCardinalWall = true; break; }
            }
            if (!hasCardinalWall) level.Set(p, TileType.Floor);
        }
    }

    static void EnsureConnectivity(LevelGenContext ctx)
    {
        // Run until fully connected (tunnels can create new small regions)
        for (int pass = 0; pass < 3; pass++)
        {
            if (!ConnectPass(ctx)) break;
        }
    }

    static bool ConnectPass(LevelGenContext ctx)
    {
        var level = ctx.level;
        int[,] region = new int[level.Width, level.Height];
        int regionCount = 0;
        List<List<Pos>> regions = [];

        for (int y = 1; y < level.Height - 1; y++)
        for (int x = 1; x < level.Width - 1; x++)
        {
            Pos p = new(x, y);
            if (level[p].Type != TileType.Floor || region[x, y] != 0) continue;

            regionCount++;
            List<Pos> tiles = [];
            Queue<Pos> queue = new();
            queue.Enqueue(p);
            region[x, y] = regionCount;

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                tiles.Add(cur);
                foreach (var d in Pos.CardinalDirs)
                {
                    var n = cur + d;
                    if (!level.InBounds(n) || region[n.X, n.Y] != 0) continue;
                    if (level[n].Type != TileType.Floor) continue;
                    region[n.X, n.Y] = regionCount;
                    queue.Enqueue(n);
                }
            }
            regions.Add(tiles);
        }

        if (regions.Count <= 1) return false;

        regions.Sort((a, b) => b.Count.CompareTo(a.Count));

        // Remove tiny regions
        for (int i = regions.Count - 1; i >= 1; i--)
        {
            if (regions[i].Count < 8)
            {
                foreach (var p in regions[i])
                    level.Set(p, TileType.Rock);
                regions.RemoveAt(i);
            }
        }

        // Connect each region to the largest
        for (int i = 1; i < regions.Count; i++)
        {
            var from = regions[i][LevelGen.Rn2(regions[i].Count)];
            var to = regions[0][LevelGen.Rn2(regions[0].Count)];
            DigTunnel(level, from, to);
        }
        return true;
    }

    static void DigTunnel(Level level, Pos from, Pos to)
    {
        int x = from.X, y = from.Y;
        while (x != to.X || y != to.Y)
        {
            if (x != to.X && (LevelGen.Rn2(2) == 0 || y == to.Y))
                x += Math.Sign(to.X - x);
            else if (y != to.Y)
                y += Math.Sign(to.Y - y);

            Pos p = new(x, y);
            if (level.InBounds(p) && level[p].Type != TileType.Floor)
                level.Set(p, TileType.Floor);
        }
        Wallify(level);
    }

    public static void GenerateDrunkard(LevelGenContext ctx, int walkers = 2, double fillTarget = 0.25)
    {
        var level = ctx.level;
        int width = level.Width, height = level.Height;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            level.Set(new(x, y), TileType.Rock);

        int target = (int)((width - 2) * (height - 2) * fillTarget);
        int carved = 0;

        int[] wx = new int[walkers], wy = new int[walkers];
        int[] wdir = new int[walkers];
        int cx = width / 2, cy = height / 2;

        for (int w = 0; w < walkers; w++)
        {
            wx[w] = width / (walkers + 1) * (w + 1);
            wy[w] = height / 2 + LevelGen.RnRange(-3, 3);
            wdir[w] = LevelGen.Rn2(4);
        }

        int active = 0;
        while (carved < target)
        {
            int w = active++ % walkers;
            int x = wx[w], y = wy[w];

            if (level[new(x, y)].Type != TileType.Floor)
            {
                level.Set(new(x, y), TileType.Floor);
                carved++;
            }

            // 85% momentum, 15% pick biased direction
            if (LevelGen.Rn2(20) >= 17)
            {
                // Bias toward center + away from other walkers
                int bx = Math.Sign(cx - x), by = Math.Sign(cy - y);
                for (int ow = 0; ow < walkers; ow++)
                {
                    if (ow == w) continue;
                    bx += Math.Sign(x - wx[ow]);
                    by += Math.Sign(y - wy[ow]);
                }

                // Pick direction weighted by bias
                int pick = LevelGen.Rn2(6);
                if (pick == 0 && bx != 0) wdir[w] = bx > 0 ? 2 : 0; // E or W (index into CardinalDirs)
                else if (pick == 1 && by != 0) wdir[w] = by > 0 ? 1 : 3; // S or N
                else wdir[w] = LevelGen.Rn2(4);
            }

            int nx = x + Pos.CardinalDirs[wdir[w]].X;
            int ny = y + Pos.CardinalDirs[wdir[w]].Y;
            if (nx > 1 && nx < width - 2 && ny > 1 && ny < height - 2)
            {
                wx[w] = nx;
                wy[w] = ny;
            }
            else
            {
                wdir[w] = LevelGen.Rn2(4);
            }
        }

        Wallify(level);
        EnsureConnectivity(ctx);
    }

    public static void GenerateBSP(LevelGenContext ctx, int minSize = 6, int maxDepth = 4)
    {
        var level = ctx.level;
        int width = level.Width, height = level.Height;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            level.Set(new(x, y), TileType.Rock);

        List<Rect> leaves = [];
        SplitBSP(new Rect(1, 1, width - 2, height - 2), minSize, maxDepth, 0, leaves);

        // Carve a room inside each leaf
        List<Rect> rooms = [];
        foreach (var leaf in leaves)
        {
            int rw = Math.Min(12, LevelGen.RnRange(Math.Max(3, leaf.W - 6), leaf.W - 3));
            int rh = Math.Min(8, LevelGen.RnRange(Math.Max(3, leaf.H - 6), leaf.H - 3));
            int rx = leaf.X + LevelGen.RnRange(1, leaf.W - rw - 1);
            int ry = leaf.Y + LevelGen.RnRange(1, leaf.H - rh - 1);
            Rect room = new(rx, ry, rw, rh);
            rooms.Add(room);

            for (int y = room.Y; y < room.Y + room.H; y++)
            for (int x = room.X; x < room.X + room.W; x++)
                level.Set(new(x, y), TileType.Floor);
        }

        // Connect each room to the next
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            Pos a = new(rooms[i].X + rooms[i].W / 2, rooms[i].Y + rooms[i].H / 2);
            Pos b = new(rooms[i + 1].X + rooms[i + 1].W / 2, rooms[i + 1].Y + rooms[i + 1].H / 2);
            DigTunnel(level, a, b);
        }

        Wallify(level);
    }

    public static void GenerateCircles(LevelGenContext ctx, int count = 8, int minRadius = 2, int maxRadius = 4)
    {
        var level = ctx.level;
        int width = level.Width, height = level.Height;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            level.Set(new(x, y), TileType.Rock);

        // Bridson's Poisson disk sampling for circle centers
        int minDist = maxRadius * 2 + 3;
        double cellSize = minDist / 1.414;
        int gridW = (int)Math.Ceiling(width / cellSize);
        int gridH = (int)Math.Ceiling(height / cellSize);
        int?[,] grid = new int?[gridW, gridH];
        List<Pos> points = [];
        List<int> active = [];

        Pos first = new(width / 2, height / 2);
        points.Add(first);
        active.Add(0);
        grid[(int)(first.X / cellSize), (int)(first.Y / cellSize)] = 0;

        while (active.Count > 0 && points.Count < count)
        {
            int idx = LevelGen.Rn2(active.Count);
            Pos p = points[active[idx]];
            bool found = false;

            for (int k = 0; k < 30; k++)
            {
                double angle = LevelGen.Rn2(360) * Math.PI / 180;
                double dist = minDist + LevelGen.Rn2(minDist);
                int nx = p.X + (int)(Math.Cos(angle) * dist);
                int ny = p.Y + (int)(Math.Sin(angle) * dist);

                if (nx < maxRadius + 2 || nx >= width - maxRadius - 2 ||
                    ny < maxRadius + 2 || ny >= height - maxRadius - 2) continue;

                int gx = (int)(nx / cellSize), gy = (int)(ny / cellSize);
                bool tooClose = false;
                for (int dy = -2; dy <= 2 && !tooClose; dy++)
                for (int dx = -2; dx <= 2 && !tooClose; dx++)
                {
                    int cx = gx + dx, cy = gy + dy;
                    if (cx < 0 || cy < 0 || cx >= gridW || cy >= gridH) continue;
                    if (grid[cx, cy] is { } pi)
                    {
                        int ddx = nx - points[pi].X, ddy = ny - points[pi].Y;
                        if (ddx * ddx + ddy * ddy < minDist * minDist) tooClose = true;
                    }
                }

                if (!tooClose)
                {
                    Pos np = new(nx, ny);
                    grid[gx, gy] = points.Count;
                    active.Add(points.Count);
                    points.Add(np);
                    found = true;
                    break;
                }
            }
            if (!found) active.RemoveAt(idx);
        }

        // Carve circles with wobble
        List<(Pos center, int radius)> circles = [];
        foreach (var center in points)
        {
            int r = LevelGen.RnRange(minRadius, maxRadius);
            circles.Add((center, r));
            for (int y = center.Y - r - 1; y <= center.Y + r + 1; y++)
            for (int x = center.X - r - 1; x <= center.X + r + 1; x++)
            {
                if (!level.InBounds(new(x, y)) || x <= 0 || y <= 0 || x >= width - 1 || y >= height - 1) continue;
                double d = Math.Sqrt((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y));
                double wobble = r + (LevelGen.Rn2(3) - 1) * 0.8;
                if (d <= wobble)
                    level.Set(new(x, y), TileType.Floor);
            }
        }

        // Connect each circle to its nearest neighbor
        List<int> connected = [0];
        HashSet<int> remaining = [];
        for (int i = 1; i < circles.Count; i++) remaining.Add(i);

        while (remaining.Count > 0)
        {
            int bestFrom = -1, bestTo = -1;
            double bestDist = double.MaxValue;
            foreach (int from in connected)
            foreach (int to in remaining)
            {
                double d = Math.Sqrt(
                    (circles[from].center.X - circles[to].center.X) * (circles[from].center.X - circles[to].center.X) +
                    (circles[from].center.Y - circles[to].center.Y) * (circles[from].center.Y - circles[to].center.Y));
                if (d < bestDist) { bestDist = d; bestFrom = from; bestTo = to; }
            }
            if (bestTo < 0) break;
            DigTunnel(level, circles[bestFrom].center, circles[bestTo].center);
            connected.Add(bestTo);
            remaining.Remove(bestTo);
        }

        // Random extra connections for loops
        for (int i = 0; i < circles.Count / 3; i++)
        {
            int a = LevelGen.Rn2(circles.Count);
            int b = LevelGen.Rn2(circles.Count);
            if (a != b) DigTunnel(level, circles[a].center, circles[b].center);
        }

        Wallify(level);
    }

    public static void GenerateGrowingTree(LevelGenContext ctx, int minRadius = 3, int maxRadius = 5, double fillTarget = 0.30)
    {
        int minFloor = (int)((ctx.level.Width - 2) * (ctx.level.Height - 2) * fillTarget * 0.6);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int carved = TryGrowingTree(ctx, minRadius, maxRadius, fillTarget);
            if (carved >= minFloor) break;
        }
        Wallify(ctx.level);
    }

    static int TryGrowingTree(LevelGenContext ctx, int minRadius, int maxRadius, double fillTarget)
    {
        var level = ctx.level;
        int width = level.Width, height = level.Height;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            level.Set(new(x, y), TileType.Rock);

        List<Pos> centers = [];
        int target = (int)((width - 2) * (height - 2) * fillTarget);
        int carved = 0;

        // First room at center
        Pos first = new(width / 2, height / 2);
        carved += CarveCircle(level, first, LevelGen.RnRange(minRadius, maxRadius));
        centers.Add(first);

        int failures = 0;
        while (carved < target && failures < 200)
        {
            // Pick an existing room — bias toward newest (70%) for snaking, random (30%) for branching
            int pick = LevelGen.Rn2(10) < 7
                ? centers.Count - 1 - LevelGen.Rn2(Math.Min(3, centers.Count))
                : LevelGen.Rn2(centers.Count);
            Pos parent = centers[pick];

            // Pick random direction and distance
            double angle = LevelGen.Rn2(360) * Math.PI / 180;
            int r = LevelGen.RnRange(minRadius, maxRadius);
            int dist = r + LevelGen.RnRange(minRadius, maxRadius) + 2;
            int nx = parent.X + (int)(Math.Cos(angle) * dist);
            int ny = parent.Y + (int)(Math.Sin(angle) * dist);

            // Check bounds
            if (nx - r <= 1 || nx + r >= width - 2 || ny - r <= 1 || ny + r >= height - 2)
            {
                failures++;
                continue;
            }

            // Check overlap with existing rooms (allow slight touch but not heavy overlap)
            Pos candidate = new(nx, ny);
            bool tooClose = false;
            foreach (var c in centers)
            {
                double d = Math.Sqrt((nx - c.X) * (nx - c.X) + (ny - c.Y) * (ny - c.Y));
                if (d < r + maxRadius + 1) { tooClose = true; break; }
            }
            if (tooClose) { failures++; continue; }

            // Carve room and tunnel
            carved += CarveCircle(level, candidate, r);
            DigTunnel(level, parent, candidate);
            centers.Add(candidate);
            failures = 0;
        }

        return carved;
    }

    static int CarveCircle(Level level, Pos center, int r)
    {
        int carved = 0;
        for (int y = center.Y - r; y <= center.Y + r; y++)
        for (int x = center.X - r; x <= center.X + r; x++)
        {
            if (!level.InBounds(new(x, y)) || x <= 0 || y <= 0 || x >= level.Width - 1 || y >= level.Height - 1) continue;
            double dist = Math.Sqrt((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y));
            double wobble = r + (LevelGen.Rn2(3) - 1) * 0.7;
            if (dist <= wobble && level[new(x, y)].Type != TileType.Floor)
            {
                level.Set(new(x, y), TileType.Floor);
                carved++;
            }
        }
        return carved;
    }

    static void SplitBSP(Rect r, int minSize, int maxDepth, int depth, List<Rect> leaves)
    {
        if (depth >= maxDepth || (r.W < minSize * 2 && r.H < minSize * 2))
        {
            leaves.Add(r);
            return;
        }

        bool splitH = r.W < minSize * 2 ? true
                     : r.H < minSize * 2 ? false
                     : LevelGen.Rn2(2) == 0;

        if (splitH)
        {
            int split = LevelGen.RnRange(r.Y + minSize, r.Y + r.H - minSize);
            SplitBSP(new Rect(r.X, r.Y, r.W, split - r.Y), minSize, maxDepth, depth + 1, leaves);
            SplitBSP(new Rect(r.X, split, r.W, r.Y + r.H - split), minSize, maxDepth, depth + 1, leaves);
        }
        else
        {
            int split = LevelGen.RnRange(r.X + minSize, r.X + r.W - minSize);
            SplitBSP(new Rect(r.X, r.Y, split - r.X, r.H), minSize, maxDepth, depth + 1, leaves);
            SplitBSP(new Rect(split, r.Y, r.X + r.W - split, r.H), minSize, maxDepth, depth + 1, leaves);
        }
    }
}

public static class PerlinNoise
{
    public static void Generate(LevelGenContext ctx, double scale = 0.12, double threshold = 0.05, int octaves = 3)
    {
        if (LevelGen.ParamSweep >= 0)
            threshold = -0.1 + LevelGen.ParamSweep * 0.04;
        var level = ctx.level;
        int width = level.Width, height = level.Height;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            level.Set(new(x, y), TileType.Rock);

        int[] perm = new int[512];
        int[] base256 = new int[256];
        for (int i = 0; i < 256; i++) base256[i] = i;
        for (int i = 255; i > 0; i--)
        {
            int j = LevelGen.Rn2(i + 1);
            (base256[i], base256[j]) = (base256[j], base256[i]);
        }
        for (int i = 0; i < 512; i++) perm[i] = base256[i & 255];

        for (int y = 1; y < height - 1; y++)
        for (int x = 1; x < width - 1; x++)
        {
            double val = 0, amp = 1, freq = scale, total = 0;
            for (int o = 0; o < octaves; o++)
            {
                val += Sample(x * freq, y * freq, perm) * amp;
                total += amp;
                amp *= 0.5;
                freq *= 2;
            }
            val /= total;

            if (val > threshold)
                level.Set(new(x, y), TileType.Floor);
        }

        CaveGen.PostProcess(ctx, connect: false);
    }

    static double Sample(double x, double y, int[] perm)
    {
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);
        double u = Fade(xf);
        double v = Fade(yf);

        int aa = perm[perm[xi] + yi];
        int ab = perm[perm[xi] + yi + 1];
        int ba = perm[perm[xi + 1] + yi];
        int bb = perm[perm[xi + 1] + yi + 1];

        return Lerp(v,
            Lerp(u, Grad(aa, xf, yf), Grad(ba, xf - 1, yf)),
            Lerp(u, Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1)));
    }

    static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    static double Lerp(double t, double a, double b) => a + t * (b - a);
    static double Grad(int hash, double x, double y) => (hash & 3) switch
    {
        0 => x + y,
        1 => -x + y,
        2 => x - y,
        _ => -x - y,
    };
}
