namespace Pathhack.Game;

public class SpawnBag
{
    const int LevelOffset = 1; // BaseLevel -1 → bit 0

    record struct FamilyEntry(MonsterFamily Family, MonsterDef[] Defs, uint LevelMask);

    readonly FamilyEntry[] _entries;
    readonly int[] _tokens;
    int _cursor;

    public SpawnBag(IEnumerable<MonsterDef> defs)
    {
        _entries = defs
            .GroupBy(m => m.Family)
            .Select(g =>
            {
                uint mask = 0;
                foreach (var m in g)
                    mask |= 1u << (m.BaseLevel + LevelOffset);
                return new FamilyEntry(g.Key, g.ToArray(), mask);
            })
            .ToArray();
        _tokens = new int[_entries.Length];
        Refill();
    }

    void Refill()
    {
        for (int i = 0; i < _tokens.Length; i++) _tokens[i] = i;
        g.Shuffle(_tokens.AsSpan());
        _cursor = 0;
    }

    static uint RangeMask(int lo, int hi)
    {
        lo = Math.Max(0, lo + LevelOffset);
        hi += LevelOffset;
        if (hi < lo || lo >= 32) return 0;
        hi = Math.Min(hi, 31);
        return ((1u << (hi - lo + 1)) - 1) << lo;
    }

    public (MonsterFamily Family, MonsterDef[] Defs)? Draw(int depth, int playerLevel)
    {
        if (_cursor >= _tokens.Length) Refill();

        int minLevel = depth / 6;
        int maxLevel = (depth + playerLevel) / 2;

        while (_cursor < _tokens.Length)
        {
            ref var entry = ref _entries[_tokens[_cursor++]];
            int effectiveMax = Math.Min(maxLevel, depth - entry.Family.DepthOffset);
            uint mask = RangeMask(minLevel, effectiveMax);
            if ((entry.LevelMask & mask) != 0)
            {
                if (_cursor * 10 >= _tokens.Length * 7)
                    Refill();
                return (entry.Family, entry.Defs);
            }
        }

        Refill();
        return null;
    }
}

public static class MonsterSpawner
{
    const int RuntimeSpawnFrequency = 70;
    const double CatchUpRate = 0.3;

    public static void TryRuntimeSpawn(Level level)
    {
        if (!level.SpawnFlags.HasFlag(SpawnFlags.Runtime)) return;

        if (g.Rn2(RuntimeSpawnFrequency) != 0) return;
        
        const int minDist = 10;
        bool far(Pos p) => p.ChebyshevDist(upos) >= minDist;
        bool basic(Pos p) => level.NoUnit(p) && !level[p].IsStairs;

        // Layered: far+no LOS, far+no visible, far, no LOS, anywhere
        var pos = level.FindLocation(p => basic(p) && far(p) && !level.HasLOS(p))
               ?? level.FindLocation(p => basic(p) && far(p) && !level.IsVisible(p))
               ?? level.FindLocation(p => basic(p) && far(p))
               ?? level.FindLocation(p => basic(p) && !level.HasLOS(p))
               ?? level.FindLocation(p => basic(p));
        
        if (pos == null) return;
        
        SpawnAndPlace(level, $"runtime DL={level.Id}", null, true, pos);
    }

    public static void CatchUpSpawns(Level level, long turnDelta)
    {
        if (!level.SpawnFlags.HasFlag(SpawnFlags.Catchup)) return;

        int expectedSpawns = (int)(turnDelta / RuntimeSpawnFrequency);
        int actualSpawns = (int)(expectedSpawns * CatchUpRate);

        for (int i = 0; i < actualSpawns; i++)
        {
            if (!SpawnAndPlace(level, "catchup", null, true))
                break;
        }
    }

    public static bool SpawnAndPlace(Level level, string reason, MonsterDef? def, bool allowTemplate, Pos? pos = null, bool asleep = false, Func<MonsterDef, bool>? filter = null, bool noGroup = false)
    {
        int depth = level.EffectiveDepth;
        int playerLevel = u?.CharacterLevel ?? 1;
        
        var resolved = level.Id.Branch.ResolvedLevels[level.Id.Depth - 1];
        var pick = resolved.Behaviour?.PickMonster(level, depth, reason);
        def ??= pick?.Def ?? PickMonster(depth, playerLevel, filter);
        if (def == null) return false;

        // Grow up if effective level reaches grown form's base level
        int bonusLevels = CalcBonusLevels(def.BaseLevel, depth, playerLevel);
        int effectiveLevel = def.BaseLevel + bonusLevels;
        if (def.GrowsInto?.Invoke() is { } grown && effectiveLevel > grown.BaseLevel)
        {
            def = grown;
            bonusLevels = effectiveLevel - grown.BaseLevel;
        }

        pos ??= level.FindLocation(p => level.NoUnit(p) && !level[p].IsStairs);
        if (pos == null) return false;

        MonsterTemplate? template = pick?.Template is { } t && t.CanApplyTo(def) ? t : null;

        // FIXME: logic, etc.
        if (template == null && allowTemplate && g.Rn2(10) < 1)
        {
            template = MonsterTemplate.All.Shuffled().FirstOrDefault(x => x.CanApplyTo(def));
        }

        var mon = Monster.Spawn(def, reason, template, bonusLevels);
        mon.IsAsleep = asleep;

        // me like gold
        if (def.CreatureType == CreatureTypes.Humanoid && g.Rn2(4) == 0)
            mon.Gold += (1 + (g.Rn2(depth + 2) + 1) * (g.Rn2(30) + 1)) / 4;
        level.PlaceUnit(mon, pos.Value);

        if (!noGroup)
            TrySpawnGroup(level, mon, template, pos.Value, asleep);
        return true;
    }

    public static void TrySpawnGroup(Level level, Monster initiator, MonsterTemplate? template, Pos origin, bool asleep)
    {
        MonsterDef initDef = initiator.Def;
        if (initDef.GroupSize == GroupSize.None) return;

        // dNH: SGROUP 50% chance, LGROUP 66% large / 33% small
        bool isLarge = initDef.GroupSize >= GroupSize.Large;
        int max;
        if (isLarge)
        {
            max = g.Rn2(3) != 0 ? 10 : 3; // 66% large, 33% small
        }
        else
        {
            if (g.Rn2(2) != 0) return; // 50% no group
            max = 3;
        }

        int count = d(max).Roll();

        // dNH: reduce at low player levels
        count = u.CharacterLevel switch
        {
            < 3 => (count + 3) / 4,
            < 5 => (count + 1) / 2,
            _ => count
        };

        bool mixed = initDef.GroupSize is GroupSize.SmallMixed or GroupSize.LargeMixed;
        int levelSpread = Math.Clamp((int)Math.Round(initDef.BaseLevel / 4.0), 1, 3);
        var familyCandidates = mixed && initDef.Family != null
            ? AllMonsters.All.Where(m => m.Family == initDef.Family && Math.Abs(m.BaseLevel - initDef.BaseLevel) <= levelSpread).ToList()
            : null;

        for (int i = 0; i < count; i++)
        {
            var adj = FindAdjacentEmpty(level, origin);
            if (adj == null) break;

            var def = familyCandidates != null ? familyCandidates[g.Rn2(familyCandidates.Count)] : initDef;
            var mon = Monster.Spawn(def, "group", template);
            mon.IsAsleep = asleep;
            level.PlaceUnit(mon, adj.Value);
        }
    }

    static Pos? FindAdjacentEmpty(Level level, Pos origin)
    {
        var candidates = origin.Neighbours()
            .Where(p => level.InBounds(p) && level.NoUnit(p) && level[p].IsPassable)
            .ToList();
        return candidates.Count > 0 ? candidates[g.Rn2(candidates.Count)] : null;
    }

    public static MonsterDef? PickMonster(int depth, int playerLevel, Func<MonsterDef, bool>? filter = null)
    {
        int minLevel = depth / 6;
        int maxLevel = (depth + playerLevel) / 2;

        bool eligible(MonsterDef m) => m.BaseLevel >= minLevel && m.BaseLevel <= maxLevel
            && depth >= m.BaseLevel + m.Family.DepthOffset
            && (filter == null || filter(m));

        // Try bag-drawn family first
        var draw = g.SpawnBag.Draw(depth, playerLevel);
        var pick = draw != null ? PickWeighted(draw.Value.Defs.Where(eligible).ToList()) : null;

        // Fallback to full pool
        pick ??= PickWeighted(AllMonsters.All.Where(eligible).ToList());

        // Reroll if pick is too far below player level
        int gap = pick != null ? playerLevel - pick.BaseLevel : 0;
        if ((gap > 2 && g.Rn2(2) == 0) || (gap > 1 && g.Rn2(3) == 0))
        {
            var reroll = g.SpawnBag.Draw(depth, playerLevel);
            pick = (reroll != null ? PickWeighted(reroll.Value.Defs.Where(eligible).ToList()) : null) ?? pick;
        }
        return pick;
    }

    public static int CalcBonusLevels(int baseLevel, int depth, int playerLevel)
    {
        int depthBonus = Math.Max(0, depth - baseLevel) / 5;
        int playerBonus = Math.Max(0, playerLevel - baseLevel) / 4;
        return depthBonus + playerBonus;
    }

    public static MonsterDef? PickWeighted(IReadOnlyList<MonsterDef> candidates)
    {
        if (candidates.Count == 0) return null;

        int totalWeight = candidates.Sum(m => m.SpawnWeight);
        if (totalWeight == 0) return candidates[g.Rn2(candidates.Count)];

        int roll = g.Rn2(totalWeight);
        foreach (var m in candidates)
        {
            roll -= m.SpawnWeight;
            if (roll < 0) return m;
        }
        return candidates[^1];
    }
}
