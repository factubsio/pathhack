namespace Pathhack.Game;

public static class MonsterSpawner
{
    const int RuntimeSpawnFrequency = 70;
    const double CatchUpRate = 0.3;

    public static void TryRuntimeSpawn(Level level)
    {
        if (level.NoInitialSpawns) return;

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
        if (level.NoInitialSpawns) return;

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
        
        def ??= PickMonster(depth, playerLevel, filter);
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

        MonsterTemplate? template = null;

        // FIXME: logic, etc.
        if (allowTemplate && g.Rn2(10) < 1)
        {
            template = MonsterTemplate.All.Shuffled().FirstOrDefault(x => x.CanApplyTo(def));
        }

        var mon = Monster.Spawn(def, reason, template, bonusLevels);
        mon.IsAsleep = asleep;
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

        var candidates = AllMonsters.All
            .Where(m => depth >= m.MinDepth && m.BaseLevel >= minLevel && m.BaseLevel <= maxLevel
                && (filter == null || filter(m)))
            .ToList();

        return PickWeighted(candidates);
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
