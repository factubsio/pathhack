using System.Security.Cryptography;
using System.Text;

namespace Pathhack.Map;

public enum DepthAnchor { FromTop, FromBottom, RelativeTo }

public record LevelRule(
    string Id,
    string[]? Templates,        // null = normal gen, else pick one
    DepthAnchor Anchor,
    int Base,
    int Range = 1,
    string? RelativeTo = null,
    bool Required = true,
    string? BranchTarget = null, // if set, place branch stairs here
    bool NoBranchEntrance = false, // if true, no branch stairs can be placed here
    CaveAlgorithm? Algorithm = null // override algorithm for this floor
);

public record BranchTemplate(
    string Id,
    string Name,
    (int Min, int Max) DepthRange,
    string? Parent = null,
    (int From, int To)? EntranceDepth = null, // negative = from bottom
    ConsoleColor Color = ConsoleColor.White,
    BranchDir Dir = BranchDir.Down
)
{
    public List<LevelRule> Levels { get; init; } = [];
    public CaveAlgorithm[]? AlgorithmPool { get; init; }
    public (ConsoleColor Floor, ConsoleColor Wall)[]? ColorPool { get; init; }
}

public record ResolvedLevel(int LocalIndex, SpecialLevel? Template = null)
{
    private readonly List<DungeonGenCommand> GenCommands = [];
    public string? BranchDown { get; set; }
    public string? BranchUp { get; set; }
    public CaveAlgorithm? Algorithm { get; set; }
    public ConsoleColor? FloorColor { get; set; }
    public ConsoleColor? WallColor { get; set; }

    public IEnumerable<DungeonGenCommand> Commands => GenCommands;
    public void AddCommand(string debug, Action<LevelGenContext> action) => GenCommands.Add(new(action, debug));
}

public static class DungeonResolver
{
    static Random _rng = new();
    static StreamWriter? _log;
    static int _iterations;
    const int MaxIterations = 10000;

    static void Log(string msg) => _log?.WriteLine(msg);

    static int Rn2(int n) => _rng.Rn2(n);
    static int RnRange(int min, int max) => _rng.RnRange(min, max);
    static T Pick<T>(T[] arr) => arr[Rn2(arr.Length)];

    public static Dictionary<string, Branch> Resolve(List<BranchTemplate> templates, int gameSeed, bool log = true)
    {
        if (log) _log = new StreamWriter("dungeongen.log");
        try { return ResolveInner(templates, gameSeed); }
        finally { _log?.Dispose(); _log = null; }
    }

    static Dictionary<string, Branch> ResolveInner(List<BranchTemplate> templates, int gameSeed)
    {
        _iterations = 0;
        Log($"Resolving dungeon structure, seed={gameSeed}");
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{gameSeed}:dungeon_structure"));
        _rng = new Random(BitConverter.ToInt32(bytes, 0));
        Dictionary<string, Branch> branches = [];

        var byId = templates.ToDictionary(t => t.Id);
        var sorted = TopoSort(templates, t => t.Parent != null && byId.TryGetValue(t.Parent, out BranchTemplate? value) ? [value] : []);
        Log($"Topo order: {string.Join(", ", sorted.Select(t => t.Id))}");

        foreach (var template in sorted)
        {
            int branchLength = RnRange(template.DepthRange.Min, template.DepthRange.Max);
            Log($"\nBranch {template.Id}: depth={branchLength} (range {template.DepthRange})");

            int? depthInParent = null;
            if (template.Parent != null && template.EntranceDepth is var (from, to))
            {
                var parent = branches[template.Parent];
                var parentTemplate = templates.First(t => t.Id == template.Parent);
                int minD = from < 0 ? parent.MaxDepth + from + 1 : from;
                int maxD = to < 0 ? parent.MaxDepth + to + 1 : to;
                maxD = Math.Min(maxD, parent.MaxDepth);
                minD = Math.Min(minD, maxD);
                
                // Exclude depths with NoBranchEntrance special levels
                var blocked = parent.BlockedEntranceDepths;
                var valid = Enumerable.Range(minD - 1, maxD - minD + 1).Where(d => !blocked.Contains(d)).ToList();
                
                if (valid.Count == 0)
                    throw new Exception($"No valid entrance depth for {template.Id} in {template.Parent}");
                
                depthInParent = valid[_rng.Next(valid.Count)];
                parent.BlockedEntranceDepths.Add(depthInParent.Value);
                Log($"  Entrance in {template.Parent} at depth {depthInParent} (range {minD}-{maxD}, blocked: [{string.Join(",", blocked)}])");
            }

            List<ResolvedLevel> resolved = [.. new ResolvedLevel[branchLength]];
            for (int i = 0; i < branchLength; i++) resolved[i] = new(i);

            Dictionary<string, int> placed = [];

            Log($"  Placing {template.Levels.Count} level rules...");
            if (!PlaceLevels(template.Levels, 0, branchLength, resolved, placed))
                throw new Exception($"Failed to resolve branch {template.Id}");

            // Assign cave algorithms from pool
            if (template.AlgorithmPool is { } pool)
                foreach (var rl in resolved)
                    if (rl.Template == null)
                        rl.Algorithm = pool[_rng.Next(pool.Length)];

            // Apply per-rule algorithm overrides
            foreach (var rule in template.Levels)
                if (rule.Algorithm is { } algo && placed.TryGetValue(rule.Id, out int ruleDepth))
                    resolved[ruleDepth - 1].Algorithm = algo;

            // Assign colors from pool
            if (template.ColorPool is { } colors)
                foreach (var rl in resolved)
                {
                    var pick = colors[_rng.Next(colors.Length)];
                    rl.FloorColor = pick.Floor;
                    rl.WallColor = pick.Wall;
                }

            // Compute blocked entrance depths from NoBranchEntrance rules
            var blockedDepths = template.Levels
                .Where(r => r.NoBranchEntrance && placed.ContainsKey(r.Id))
                .Select(r => placed[r.Id] - 1)
                .ToHashSet();

            branches[template.Id] = new Branch(template.Id, template.Name, branchLength, template.Color, template.Dir)
            {
                ResolvedLevels = resolved,
                EntranceDepthInParent = depthInParent,
                BlockedEntranceDepths = blockedDepths
            };
        }

        foreach (var template in sorted)
        {
            var branch = branches[template.Id];
            // Add portals between branches
            if (template.Parent != null && branch.EntranceDepthInParent != null)
            {
                var parent = branches[template.Parent];
                int parentIndex = branch.EntranceDepthInParent.Value;
                var parentLevel = parent.ResolvedLevels[parentIndex];
                var branchEntry = branch.ResolvedLevels[0];

                if (template.Dir == BranchDir.Down)
                {
                    parentLevel.BranchDown = branch.Id;
                    branchEntry.BranchUp = parent.Id;
                }
                else
                {
                    parentLevel.BranchUp = branch.Id;
                    branchEntry.BranchDown = parent.Id;
                }

                parentLevel.AddCommand($"place portal TO {template.Name}",
                    PlacePortalTo(template.Id, 0, template.Dir, parentLevel.Template?.HasPortalToChild == true));

                branchEntry.AddCommand($"place portal BACK {template.Parent}",
                    PlacePortalTo(template.Parent, parentIndex, template.Dir.Reversed(), branchEntry.Template?.HasPortalToParent == true));
            }

            for (int i = 0; i < branch.MaxDepth; i++)
            {
                // A bit crappy but iiwii, work out if there is a level physically above/below us.
                bool hasAbove = (template.Dir == BranchDir.Down) ? i > 0 : i < branch.MaxDepth - 1;
                bool hasBelow = (template.Dir == BranchDir.Down) ? i < branch.MaxDepth - 1 : i > 0;

                var r = branch.ResolvedLevels[i];

                if (hasAbove && r.Template?.HasStairsUp != true) r.AddCommand("stairs up", PlaceStairs(TileType.StairsUp));
                if (hasBelow && r.Template?.HasStairsDown != true) r.AddCommand("stairs down", PlaceStairs(TileType.StairsDown));
            }
        }

        branches["dungeon"].ResolvedLevels[0].AddCommand("place dungeon entrance", PlaceStairs(TileType.StairsUp));

        return branches;
    }

    private static Action<LevelGenContext> PlaceStairs(TileType type)
    {
        return ctx =>
        {
            var pos = ctx.FindStairsLocation() ?? ctx.Throw<Pos>($"cannot place stairs {type}");
            ctx.level.Set(pos, type);
        };
    }

    private static Action<LevelGenContext> PlacePortalTo(string toBranch, int toLevel, BranchDir dir, bool patchExisting)
    {
        return ctx =>
        {
            TileType type = dir == BranchDir.Down ? TileType.BranchDown : TileType.BranchUp;
            LevelId to = new(g.Branches[toBranch], toLevel + 1);

            if (!patchExisting)
            {
                var pos = ctx.FindStairsLocation() ?? ctx.Throw<Pos>("cannot place portal");
                ctx.level.Set(pos, type);
            }

            if (dir == BranchDir.Down)
                ctx.level.BranchDownTarget = to;
            else
                ctx.level.BranchUpTarget = to;
        };
    }

    static bool PlaceLevels(List<LevelRule> rules, int idx, int branchDepth,
          List<ResolvedLevel> resolved, Dictionary<string, int> placed)
    {
        if (++_iterations > MaxIterations)
            throw new Exception($"DungeonResolver exceeded {MaxIterations} iterations - check branch constraints");
        
        string indent = new(' ', idx * 2);
        if (idx >= rules.Count) return true;

        var rule = rules[idx];
        var valid = GetValidDepths(rule, branchDepth, resolved, placed);
        Log($"{indent}{rule.Id}: valid=[{string.Join(",", valid)}] (anchor={rule.Anchor} base={rule.Base} range={rule.Range})");

        if (valid.Count == 0)
        {
            Log($"{indent}{rule.Id}: FAIL (required={rule.Required})");
            return !rule.Required && PlaceLevels(rules, idx + 1, branchDepth, resolved, placed);
        }

        // Shuffle valid depths
        for (int i = valid.Count - 1; i > 0; i--)
        {
            int j = Rn2(i + 1);
            (valid[i], valid[j]) = (valid[j], valid[i]);
        }

        foreach (int d in valid)
        {
            Log($"{indent}{rule.Id}: trying depth {d}");
            placed[rule.Id] = d;
            string? template = rule.Templates != null ? Pick(rule.Templates) : null;
            var spec = LevelGen.GetTemplate(template);
            var old = resolved[d - 1];
            resolved[d - 1] = old with { Template = spec ?? old.Template };

            if (PlaceLevels(rules, idx + 1, branchDepth, resolved, placed))
                return true;

            Log($"{indent}{rule.Id}: backtrack from {d}");
            resolved[d - 1] = old;
            placed.Remove(rule.Id);
        }

        return false;
    }

    static List<int> GetValidDepths(LevelRule rule, int branchDepth,
        List<ResolvedLevel> resolved, Dictionary<string, int> placed)
    {
        if (rule.Anchor == DepthAnchor.RelativeTo && !placed.ContainsKey(rule.RelativeTo!))
            return [];

        int baseDepth = rule.Anchor switch
        {
            DepthAnchor.FromTop => rule.Base,
            DepthAnchor.FromBottom => branchDepth + rule.Base,
            DepthAnchor.RelativeTo => placed[rule.RelativeTo!] + rule.Base,
            _ => throw new ArgumentOutOfRangeException()
        };

        List<int> valid = [];
        for (int d = baseDepth; d < baseDepth + rule.Range; d++)
        {
            if (d < 1 || d > branchDepth) continue;
            if (resolved[d - 1].Template != null) continue;
            valid.Add(d);
        }
        return valid;
    }

    static List<T> TopoSort<T>(List<T> items, Func<T, List<T>> getDeps)
    {
        List<T> result = [];
        HashSet<T> visited = [];
        HashSet<T> visiting = [];

        void Visit(T item)
        {
            if (visited.Contains(item)) return;
            if (visiting.Contains(item)) throw new Exception("Cycle in branch dependencies");
            visiting.Add(item);
            foreach (var dep in getDeps(item)) Visit(dep);
            visiting.Remove(item);
            visited.Add(item);
            result.Add(item);
        }

        foreach (var item in items) Visit(item);
        return result;
    }
}
