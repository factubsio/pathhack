using System.Security.Cryptography;
using System.Text;

namespace Pathhack.Map;

public record LevelTemplate(
    string Id,
    string? BehaviourId = null,
    CaveAlgorithm? Algorithm = null,
    CaveAlgorithm[]? AlgorithmPool = null,
    string[]? Variants = null,
    ConsoleColor? WallColor = null,
    ConsoleColor? FloorColor = null,
    bool Outdoors = false,
    bool NoBranchEntrance = false
);

public record LinearPlacementRule(
    LevelTemplate? Template = null,
    int Count = 1,
    int CountMax = -1,                      // -1 = fixed, else [Count, CountMax]
    string? BranchId = null
);

public record ConstraintPlacementRule(
    LevelTemplate? Template = null,
    string? BranchId = null,
    (int Min, int Max) Depth = default,     // negative = from bottom
    string? RelativeTo = null,
    int Probability = 100                   // 100 = required, 0 = try but don't fail
);

public record BranchTemplate(
    string Id,
    string Name,
    BranchDir Dir = BranchDir.Down,
    ConsoleColor Color = ConsoleColor.White,
    string? Entry = null,                   // template id of entry floor (default: first)
    string? DefaultBehaviour = null,
    CaveAlgorithm[]? DefaultAlgorithmPool = null,
    ConsoleColor? DefaultWallColor = null,
    ConsoleColor? DefaultFloorColor = null
)
{
    // exactly one of these should be set
    public LinearPlacementRule[]? Linear { get; init; }
    public ConstraintPlacementRule[]? Constraints { get; init; }
    public (int Min, int Max) DepthRange { get; init; }     // constraint mode only
}

public record ResolvedLevel(int LocalIndex, SpecialLevel? Template = null)
{
    private readonly List<DungeonGenCommand> GenCommands = [];
    public string? TemplateId { get; set; }
    public string? BranchDown { get; set; }
    public string? BranchUp { get; set; }
    public CaveAlgorithm? Algorithm { get; set; }
    public ConsoleColor? FloorColor { get; set; }
    public ConsoleColor? WallColor { get; set; }
    public bool NoBranchEntrance { get; set; }

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
    static void Shuffle<T>(List<T> list) { for (int i = list.Count - 1; i > 0; i--) { int j = Rn2(i + 1); (list[i], list[j]) = (list[j], list[i]); } }

    public static Dictionary<string, Branch> Resolve(List<BranchTemplate> templates, int gameSeed, bool log = true)
    {
        if (log) _log = new StreamWriter("dungeongen.log");
        try { return ResolveInner(templates, gameSeed); }
        finally { _log?.Dispose(); _log = null; }
    }

    // --- collect child branch ids from placement rules ---

    static HashSet<string> CollectChildBranchIds(BranchTemplate t)
    {
        HashSet<string> ids = [];
        if (t.Linear != null)
            foreach (var r in t.Linear)
                if (r.BranchId != null) ids.Add(r.BranchId);
        if (t.Constraints != null)
            foreach (var r in t.Constraints)
                if (r.BranchId != null) ids.Add(r.BranchId);
        return ids;
    }

    // --- main resolver ---

    static Dictionary<string, Branch> ResolveInner(List<BranchTemplate> templates, int gameSeed)
    {
        _iterations = 0;
        Log($"Resolving dungeon structure, seed={gameSeed}");
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{gameSeed}:dungeon_structure"));
        _rng = new Random(BitConverter.ToInt32(bytes, 0));

        var byId = templates.ToDictionary(t => t.Id);

        // Track unresolved children per template and parent relationships
        Dictionary<string, int> unresolvedChildren = [];
        Dictionary<string, string> parentOf = [];
        foreach (var t in templates)
        {
            var childIds = CollectChildBranchIds(t);
            unresolvedChildren[t.Id] = childIds.Count;
            foreach (var c in childIds)
                parentOf[c] = t.Id;
        }

        // Resolve leaf-first
        List<BranchTemplate> remaining = [..templates];
        Dictionary<string, Branch> branches = [];

        while (remaining.Count > 0)
        {
            int idx = remaining.FindIndex(t => unresolvedChildren[t.Id] == 0);
            if (idx < 0) throw new Exception("Cycle or unresolvable branch dependencies");

            var template = remaining[idx];
            remaining.RemoveAt(idx);

            Log($"\nResolving {template.Id}");
            List<ResolvedLevel> levels = template.Linear != null
                ? ResolveLinear(template)
                : ResolveConstraint(template);

            // Assign algorithms: per-level template > branch default pool
            foreach (var rl in levels)
            {
                if (rl.Algorithm != null) continue;
                if (template.DefaultAlgorithmPool is { } pool)
                    rl.Algorithm = pool[_rng.Next(pool.Length)];
            }

            // Assign colors from branch defaults
            foreach (var rl in levels)
            {
                rl.WallColor ??= template.DefaultWallColor;
                rl.FloorColor ??= template.DefaultFloorColor;
            }

            // Determine entry floor
            int entry = 0;
            if (template.Entry != null)
            {
                int found = levels.FindIndex(l => l.TemplateId == template.Entry);
                if (found >= 0) entry = found;
            }

            // Compute blocked entrance depths
            HashSet<int> blocked = levels
                .Select((r, i) => (r, i))
                .Where(x => x.r.NoBranchEntrance)
                .Select(x => x.i)
                .ToHashSet();

            branches[template.Id] = new Branch(template.Id, template.Name, levels.Count, template.Color, template.Dir)
            {
                ResolvedLevels = levels,
                Entry = entry,
                BlockedEntranceDepths = blocked,
            };

            if (parentOf.TryGetValue(template.Id, out var pid))
                unresolvedChildren[pid]--;
        }

        // Compute EntranceDepthInParent now that all branches exist
        foreach (var (childId, pId) in parentOf)
        {
            var parent = branches[pId];
            for (int i = 0; i < parent.ResolvedLevels.Count; i++)
            {
                var rl = parent.ResolvedLevels[i];
                if (rl.BranchDown == childId || rl.BranchUp == childId)
                {
                    branches[childId] = branches[childId] with { EntranceDepthInParent = i };
                    break;
                }
            }
        }

        // --- post-pass: portals and stairs ---
        foreach (var (id, branch) in branches)
        {
            var template = byId[id];

            // Wire portals for child branches referenced by this branch's levels
            for (int i = 0; i < branch.ResolvedLevels.Count; i++)
            {
                var rl = branch.ResolvedLevels[i];

                if (rl.BranchDown is { } downId && branches.TryGetValue(downId, out var child))
                {
                    var childEntry = child.ResolvedLevels[child.Entry];
                    childEntry.BranchUp = id;

                    rl.AddCommand($"place portal TO {child.Name}",
                        PlacePortalTo(downId, child.Entry, BranchDir.Down, rl.Template?.HasPortalToChild == true));
                    childEntry.AddCommand($"place portal BACK {id}",
                        PlacePortalTo(id, i, BranchDir.Up, childEntry.Template?.HasPortalToParent == true));
                }

                if (rl.BranchUp is { } upId && branches.TryGetValue(upId, out var childUp))
                {
                    var childEntry = childUp.ResolvedLevels[childUp.Entry];
                    childEntry.BranchDown = id;

                    rl.AddCommand($"place portal TO {childUp.Name}",
                        PlacePortalTo(upId, childUp.Entry, BranchDir.Up, rl.Template?.HasPortalToChild == true));
                    childEntry.AddCommand($"place portal BACK {id}",
                        PlacePortalTo(id, i, BranchDir.Down, childEntry.Template?.HasPortalToParent == true));
                }
            }

            // Stairs
            for (int i = 0; i < branch.MaxDepth; i++)
            {
                bool hasAbove = template.Dir == BranchDir.Down ? i > 0 : i < branch.MaxDepth - 1;
                bool hasBelow = template.Dir == BranchDir.Down ? i < branch.MaxDepth - 1 : i > 0;
                var r = branch.ResolvedLevels[i];

                if (hasAbove && r.Template?.HasStairsUp != true) r.AddCommand("stairs up", PlaceStairs(TileType.StairsUp));
                if (hasBelow && r.Template?.HasStairsDown != true) r.AddCommand("stairs down", PlaceStairs(TileType.StairsDown));
            }
        }

        branches["dungeon"].ResolvedLevels[0].AddCommand("place dungeon entrance", PlaceStairs(TileType.StairsUp));
        return branches;
    }

    // --- linear resolver ---

    static List<ResolvedLevel> ResolveLinear(BranchTemplate branch)
    {
        List<ResolvedLevel> levels = [];
        Dictionary<string, List<int>> candidates = [];

        foreach (var rule in branch.Linear!)
        {
            int count = rule.CountMax < 0 ? rule.Count : RnRange(rule.Count, rule.CountMax);
            int startIdx = levels.Count;

            for (int i = 0; i < count; i++)
                levels.Add(ResolveLevel(rule.Template, branch, levels.Count));

            if (rule.BranchId != null)
            {
                if (!candidates.TryGetValue(rule.BranchId, out var list))
                    candidates[rule.BranchId] = list = [];
                for (int i = startIdx; i < levels.Count; i++)
                    list.Add(i);
            }
        }

        // Assign branch exits to random floors within their candidate segments
        HashSet<int> used = [];
        foreach (var (branchId, floors) in candidates)
        {
            List<int> available = floors.Where(f => !used.Contains(f)).ToList();
            if (available.Count == 0)
                throw new Exception($"No available floor for branch '{branchId}' in '{branch.Id}'");
            int pick = available[Rn2(available.Count)];
            levels[pick].BranchDown = branchId;
            used.Add(pick);
        }

        return levels;
    }

    // --- constraint resolver ---

    static List<ResolvedLevel> ResolveConstraint(BranchTemplate branch)
    {
        int depth = RnRange(branch.DepthRange.Min, branch.DepthRange.Max);
        List<ResolvedLevel> levels = new(depth);
        for (int i = 0; i < depth; i++)
            levels.Add(new ResolvedLevel(i));

        var rules = branch.Constraints ?? [];

        // Pre-resolve negative depths
        var resolved = rules.Select(r => (
            rule: r,
            min: r.Depth.Min < 0 ? depth + r.Depth.Min : r.Depth.Min,
            max: r.Depth.Max < 0 ? depth + r.Depth.Max : r.Depth.Max
        )).ToArray();

        Dictionary<string, int> placed = [];

        bool Place(int idx)
        {
            if (++_iterations > MaxIterations)
                throw new Exception($"DungeonResolver exceeded {MaxIterations} iterations in '{branch.Id}'");
            if (idx >= resolved.Length) return true;

            var (rule, min, max) = resolved[idx];

            if (rule.Probability < 100 && RnRange(1, 100) > rule.Probability)
                return Place(idx + 1);

            if (rule.RelativeTo != null)
            {
                if (!placed.TryGetValue(rule.RelativeTo, out int basePos))
                    return false;
                min += basePos;
                max += basePos;
            }

            min = Math.Max(0, min);
            max = Math.Min(depth - 1, max);

            List<int> valid = [];
            for (int d = min; d <= max; d++)
                if (levels[d].Template == null && levels[d].BranchDown == null && !placed.ContainsValue(d))
                    valid.Add(d);

            Shuffle(valid);

            foreach (int d in valid)
            {
                var old = levels[d];
                levels[d] = ResolveLevel(rule.Template, branch, d);
                if (rule.BranchId != null) levels[d].BranchDown = rule.BranchId;
                string? key = rule.Template?.Id ?? rule.BranchId;
                if (key != null) placed[key] = d;

                if (Place(idx + 1)) return true;

                levels[d] = old;
                if (key != null) placed.Remove(key);
            }

            return rule.Probability == 0;
        }

        if (!Place(0))
            throw new Exception($"Constraint solver failed for '{branch.Id}'");

        return levels;
    }

    // --- shared helpers ---

    static ResolvedLevel ResolveLevel(LevelTemplate? t, BranchTemplate branch, int index)
    {
        string? variant = t?.Variants != null ? Pick(t.Variants) : null;
        SpecialLevel? spec = LevelGen.GetTemplate(variant);

        CaveAlgorithm? algo = t?.Algorithm
            ?? (t?.AlgorithmPool is { } pool ? pool[Rn2(pool.Length)] : null);

        return new ResolvedLevel(index, spec)
        {
            TemplateId = t?.Id,
            Algorithm = algo,
            WallColor = t?.WallColor,
            FloorColor = t?.FloorColor,
            NoBranchEntrance = t?.NoBranchEntrance ?? false,
        };
    }

    static Action<LevelGenContext> PlaceStairs(TileType type) => ctx =>
    {
        var pos = ctx.FindStairsLocation() ?? ctx.Throw<Pos>($"cannot place stairs {type}");
        ctx.level.Set(pos, type);
    };

    static Action<LevelGenContext> PlacePortalTo(string toBranch, int toLevel, BranchDir dir, bool patchExisting) => ctx =>
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
