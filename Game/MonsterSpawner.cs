namespace Pathhack.Game;

public static class MonsterSpawner
{
    const int RuntimeSpawnFrequency = 70;
    const double CatchUpRate = 0.3;

    public static void TryRuntimeSpawn(Level level)
    {
        if (level.NoInitialSpawns) return;

        if (g.Rn2(RuntimeSpawnFrequency) != 0) return;
        
        // Layered approach like dNH: prefer out of LOS, then out of sight, then anywhere
        var pos = level.FindLocation(p => level.NoUnit(p) && !level[p].IsStairs && !level.HasLOS(p))
               ?? level.FindLocation(p => level.NoUnit(p) && !level[p].IsStairs && !level.IsVisible(p))
               ?? level.FindLocation(p => level.NoUnit(p) && !level[p].IsStairs);
        
        if (pos == null) return;
        
        var def = PickMonster(level.Depth, u.CharacterLevel);
        if (def == null) return;
        
        var mon = Monster.Spawn(def);
        level.PlaceUnit(mon, pos.Value);
    }

    public static void CatchUpSpawns(Level level, long turnDelta)
    {
        if (level.NoInitialSpawns) return;

        int expectedSpawns = (int)(turnDelta / RuntimeSpawnFrequency);
        int actualSpawns = (int)(expectedSpawns * CatchUpRate);

        for (int i = 0; i < actualSpawns; i++)
        {
            if (!SpawnAndPlace(level, u.CharacterLevel, _ => level.FindLocation(p => 
                level.NoUnit(p) && !level[p].IsStairs)))
                break;
        }
    }

    static bool SpawnAndPlace(Level level, int playerLevel, Func<MonsterDef, Pos?> findPos)
    {
        var def = PickMonster(level.Depth, playerLevel);
        if (def == null) return false;

        MonsterTemplate? template = null;

        // FIXME: logic, etc.
        if (g.Rn2(10) < 1)
        {
            template = MonsterTemplate.All.Shuffled().FirstOrDefault(x => x.CanApplyTo(def));
        }

        Pos? pos = findPos(def);
        if (pos == null) return false;

        var mon = Monster.Spawn(def, template);
        level.PlaceUnit(mon, pos.Value);

        TrySpawnGroup(level, def, pos.Value);
        return true;
    }

    static void TrySpawnGroup(Level level, MonsterDef leader, Pos origin)
    {
        if (leader.GroupSize == GroupSize.None) return;

        // dNH: SGROUP 50% chance, LGROUP 66% large / 33% small
        bool isLarge = leader.GroupSize >= GroupSize.Large;
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

        bool mixed = leader.GroupSize is GroupSize.SmallMixed or GroupSize.LargeMixed;
        var familyCandidates = mixed && leader.Family != null
            ? AllMonsters.All.Where(m => m.Family == leader.Family && Math.Abs(m.BaseLevel - leader.BaseLevel) <= 2).ToList()
            : null;

        for (int i = 0; i < count; i++)
        {
            var adj = FindAdjacentEmpty(level, origin);
            if (adj == null) break;

            var def = familyCandidates != null ? familyCandidates[g.Rn2(familyCandidates.Count)] : leader;
            var mon = Monster.Spawn(def);
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

    public static MonsterDef? PickMonster(int depth, int playerLevel)
    {
        int maxLevel = (depth + playerLevel) / 2;

        var candidates = AllMonsters.All
            .Where(m => depth >= m.MinDepth && m.BaseLevel <= maxLevel)
            .ToList();

        return PickWeighted(candidates);
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
