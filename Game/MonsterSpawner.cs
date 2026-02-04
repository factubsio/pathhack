using Pathhack.Map;

namespace Pathhack.Game;

public static class MonsterSpawner
{
    const int RuntimeSpawnFrequency = 50;
    const double CatchUpRate = 0.3;

    public static void SpawnInitialMonsters(Level level, int playerLevel = 1)
    {
        if (level.NoInitialSpawns) return;

        foreach (var room in level.Rooms)
        {
            if (g.Rn2(2) != 0) continue;
            SpawnAndPlace(level, playerLevel, p => level.FindLocationInRoom(room, pos => 
                level.NoUnit(pos) && !level[pos].IsStairs));
        }
    }

    public static void TryRuntimeSpawn(Level level)
    {
        if (level.NoInitialSpawns) return;

        if (g.Rn2(RuntimeSpawnFrequency) != 0) return;
        SpawnAndPlace(level, u.CharacterLevel, _ => level.FindLocation(p => 
            level.NoUnit(p) && !level[p].IsStairs && p.ChebyshevDist(upos) > 5));
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

        Pos? pos = findPos(def);
        if (pos == null) return false;

        var mon = Monster.Spawn(def);
        level.PlaceUnit(mon, pos.Value);
        return true;
    }

    static MonsterDef? PickMonster(int depth, int playerLevel)
    {
        int maxLevel = (depth + playerLevel) / 2;

        var candidates = AllMonsters.All
            .Where(m => depth >= m.MinDepth && m.CR <= maxLevel)
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
