namespace Pathhack.Map;

public static class Pathfinding
{
    public static List<Pos>? FindPath(Level level, Pos from, Pos to)
    {
        if (from == to) return [];
        if (!level.InBounds(to)) return null;
        if (!level.WasSeen(to)) return null;

        Dictionary<Pos, Pos> cameFrom = [];
        Dictionary<Pos, int> cost = new() { [to] = 0 };
        PriorityQueue<Pos, int> frontier = new();
        frontier.Enqueue(to, 0);

        while (frontier.Count > 0)
        {
            Pos current = frontier.Dequeue();
            if (current == from)
            {
                List<Pos> path = [];
                Pos p = from;
                while (p != to)
                {
                    Pos next = cameFrom[p];
                    path.Add(next - p);
                    p = next;
                }
                return path;
            }

            foreach (var next in current.Neighbours())
            {
                if (!level.InBounds(next)) continue;
                if (!level.WasSeen(next)) continue;
                if (!level.CanMoveTo(current, next, u)) continue;
                
                int moveCost = (next.X != current.X && next.Y != current.Y) ? 14 : 10;
                int newCost = cost[current] + moveCost;
                if (!cost.TryGetValue(next, out int oldCost) || newCost < oldCost)
                {
                    cost[next] = newCost;
                    cameFrom[next] = current;
                    frontier.Enqueue(next, newCost);
                }
            }
        }
        return null;
    }
}
