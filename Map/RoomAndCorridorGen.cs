namespace Pathhack.Map;

// Room-and-corridor generation: the classic algorithm + corridor digging.
public static partial class LevelGen
{
    // 1/3 has door: 1/5 open, 1/6 locked, rest closed. null = no door.
    static DoorState RollDoorState()
    {
        if (Rn2(3) != 0) return DoorState.Broken;
        if (Rn2(5) == 0) return DoorState.Open;
        // if (Rn2(6) == 0) return DoorState.Locked;
        return DoorState.Closed;
    }

    static void GenRoomsAndCorridors(LevelGenContext ctx, bool populate = true)
    {
        // Fill with rock
        for (int y = 0; y < ctx.level.Height; y++)
            for (int x = 0; x < ctx.level.Width; x++)
                ctx.level.Set(new(x, y), TileType.Rock);

        // Place rooms (virtual)
        int targetRooms = RnRange(5, 8);
        const int maxTries = 200;

        int triesRemaining = maxTries;

        while (triesRemaining > 0 && ctx.Stamps.Count < targetRooms)
            triesRemaining -= TryPlaceRoom(ctx, triesRemaining);

        Log($"Placed {ctx.Stamps.Count} rooms (target was {targetRooms})");

        // Render rooms to tiles
        Log("RenderRooms...");
        RenderRooms(ctx);

        // Note: room merging now happens during PlaceRoom, not here

        // Connect rooms with corridors
        Log("MakeCorridors...");
        MakeCorridors(ctx);
        Log("MakeCorridors done");

        // Rivers — defer until after room assignment so shops can be protected
        if (ForceRiver || (ctx.level.EffectiveDepth > 3 && Rn2(4) == 0))
            ctx.WantsRiver = true;

        if (ForceMiniVault || (ctx.level.EffectiveDepth >= 3 && Rn2(8) == 0))
        {
            Log("TryMiniVault...");
            TryMiniVault(ctx);
        }

        if (TestMode || !populate) return;
    }

    static int RoomLeftX(Room room) => room.Bounds?.X ?? room.Border.Min(p => p.X);

    // Find a border tile suitable for a door (has diggable/corridor neighbor, not a corner)
    static List<(Pos pos, Pos outDir)> GetDoorCandidates(Level level, Room room)
    {
        List<(Pos, Pos)> candidates = [];
        foreach (var p in room.Border)
        {
            // Skip corners - tiles with 2+ non-room neighbors
            int wallNeighbors = 0;
            foreach (var d in Pos.CardinalDirs)
                if (!room.Interior.Contains(p + d) && !room.Border.Contains(p + d))
                    wallNeighbors++;
            if (wallNeighbors >= 2) continue;
            
            foreach (var dir in Pos.CardinalDirs)
            {
                var neighbor = p + dir;
                var tile = level[neighbor];
                if (tile.Type is TileType.Rock or TileType.Corridor)
                    candidates.Add((p, dir));
            }
        }
        return candidates;
    }

    // Pick door positions for connecting two rooms - random but biased toward facing direction
    static (Pos doorA, Pos doorB, Pos dir)? PickDoorPair(Level level, Room roomA, Room roomB)
    {
        var candA = GetDoorCandidates(level, roomA);
        var candB = GetDoorCandidates(level, roomB);
        if (candA.Count == 0 || candB.Count == 0) return null;

        // Compute rough direction from A to B using any interior point
        Pos centerA = roomA.Interior.Count > 0 ? roomA.Interior[0] : roomA.Border[0];
        Pos centerB = roomB.Interior.Count > 0 ? roomB.Interior[0] : roomB.Border[0];
        int towardX = Math.Sign(centerB.X - centerA.X);
        int towardY = Math.Sign(centerB.Y - centerA.Y);

        // Filter candidates facing roughly the right direction
        var goodA = candA.Where(c => c.outDir.X == towardX || c.outDir.Y == towardY).ToList();
        var goodB = candB.Where(c => c.outDir.X == -towardX || c.outDir.Y == -towardY).ToList();

        // Fall back to all candidates if no good ones
        if (goodA.Count == 0) goodA = candA;
        if (goodB.Count == 0) goodB = candB;

        var (doorA, dirA) = goodA[Rn2(goodA.Count)];
        var (doorB, _) = goodB[Rn2(goodB.Count)];

        return (doorA, doorB, dirA);
    }

    public static void MakeCorridors(LevelGenContext ctx)
    {
        List<Room> connectable = [.. ctx.level.Rooms
            .Where(r => (r.Flags & RoomFlags.NoConnect) == 0)
            .OrderBy(RoomLeftX)];

        if (connectable.Count < 2) return;

        // Union-find for connectivity
        int[] group = new int[connectable.Count];
        for (int i = 0; i < group.Length; i++) group[i] = i;
        int Find(int x) => group[x] == x ? x : group[x] = Find(group[x]);
        void Union(int a, int b) => group[Find(a)] = Find(b);

        LogVerbose($"MakeCorridors: {connectable.Count} rooms");

        // Connect each room to next neighbor
        for (int i = 0; i < connectable.Count - 1; i++)
        {
            if (Rn2(50) == 0) break; // occasionally stop early
            if (TryDigCorridor(ctx, connectable[i], connectable[i + 1]))
                Union(i, i + 1);
        }

        // Connect to second neighbor if not connected
        for (int i = 0; i < connectable.Count - 2; i++)
        {
            if (Find(i) != Find(i + 2))
                if (TryDigCorridor(ctx, connectable[i], connectable[i + 2]))
                    Union(i, i + 2);
        }

        // Ensure full connectivity - only if needed
        bool any = true;
        int connectAttempts = 0;
        const int maxConnectAttempts = 100;
        while (any && connectAttempts++ < maxConnectAttempts)
        {
            any = false;
            for (int i = 0; i < connectable.Count; i++)
            {
                for (int j = 0; j < connectable.Count; j++)
                {
                    if (Find(i) != Find(j))
                    {
                        LogVerbose($"Room {i} and {j} not connected, joining");
                        if (TryDigCorridor(ctx, connectable[i], connectable[j]))
                            Union(i, j);
                        any = true;
                    }
                }
            }
        }
        if (connectAttempts >= maxConnectAttempts)
            Log($"WARNING: gave up connecting rooms after {maxConnectAttempts} attempts");

        // Random extra connections for loops - but limit to one corridor per room pair
        if (connectable.Count > 2)
        {
            var connected = new HashSet<(int, int)>();
            for (int i = 0; i < connectable.Count - 1; i++)
                connected.Add((i, i + 1));
            for (int i = 0; i < connectable.Count - 2; i++)
                connected.Add((i, i + 2));

            int extras = Rn2(connectable.Count) + 4;
            for (int i = 0; i < extras; i++)
            {
                int a = Rn2(connectable.Count);
                int b = Rn2(connectable.Count - 2);
                if (b >= a) b += 2;
                var pair = a < b ? (a, b) : (b, a);
                if (connected.Contains(pair)) continue;
                if (TryDigCorridor(ctx, connectable[a], connectable[b], nxcor: true))
                    connected.Add(pair);
            }
        }
    }

    static bool TryDigCorridor(LevelGenContext ctx, Room roomA, Room roomB, bool nxcor = false)
    {
        var level = ctx.level;
        
        var pair = PickDoorPair(level, roomA, roomB);
        if (pair == null)
        {
            LogVerbose($"  No door candidates found");
            return false;
        }
        
        var (doorA, doorB, outDir) = pair.Value;
        int dx = outDir.X, dy = outDir.Y;
        
        if (dx == 0 && dy == 0)
        {
            dx = Math.Sign(doorB.X - doorA.X);
            dy = Math.Sign(doorB.Y - doorA.Y);
            if (dx == 0 && dy == 0) dy = 1;
        }

        LogVerbose($"TryDigCorridor doors {doorA.X},{doorA.Y} -> {doorB.X},{doorB.Y} dir {dx},{dy}");

        Pos org = new(doorA.X + dx, doorA.Y + dy);
        Pos dest = new(doorB.X - dx, doorB.Y - dy);
        
        if (level[dest].Type is not TileType.Rock && level[dest].Type != TileType.Corridor)
            dest = doorB;

        List<Pos> dug = [];
        bool placedDoorA = false;
        int x = org.X, y = org.Y;
        int maxSteps = 500;

        while (maxSteps-- > 0)
        {
            if (nxcor && Rn2(35) == 0)
            {
                LogVerbose($"  Randomly abandoned nxcor after {dug.Count} tiles");
                return false;
            }

            Tile t = level[new(x, y)];
            if (t.Type == TileType.Rock)
            {
                level.Set(new(x, y), TileType.Corridor);
                dug.Add(new(x, y));
                
                if (!placedDoorA)
                {
                    if (level[doorA].Type != TileType.Door && CanPlaceDoor(level, doorA))
                    {
                        level.PlaceDoor(doorA, RollDoorState());
                        placedDoorA = true;
                    }
                }
            }
            else if (!t.IsPassable)
            {
                LogVerbose($"  Failed at {x},{y} tile={t.Type}, undoing {dug.Count} tiles");
                UndoCorridorOrCupboard(level, dug, placedDoorA, doorA);
                return false;
            }

            if (x == dest.X && y == dest.Y)
                break;

            int dix = Math.Abs(x - dest.X);
            int diy = Math.Abs(y - dest.Y);
            int ddx = x == dest.X ? 0 : dest.X > x ? 1 : -1;
            int ddy = y == dest.Y ? 0 : dest.Y > y ? 1 : -1;

            if (dy != 0 && dix > diy && CanDig(level, x + ddx, y))
            {
                dx = ddx;
                dy = 0;
            }
            else if (dx != 0 && diy > dix && CanDig(level, x, y + ddy))
            {
                dy = ddy;
                dx = 0;
            }

            if (CanDig(level, x + dx, y + dy))
            {
                x += dx;
                y += dy;
                continue;
            }

            if (dx != 0)
            {
                dx = 0;
                dy = ddy != 0 ? ddy : 1;
            }
            else
            {
                dy = 0;
                dx = ddx != 0 ? ddx : 1;
            }

            if (CanDig(level, x + dx, y + dy))
            {
                x += dx;
                y += dy;
                continue;
            }

            LogVerbose($"  Dead end at {x},{y}, undoing {dug.Count} tiles");
            UndoCorridorOrCupboard(level, dug, placedDoorA, doorA);
            return false;
        }

        if (maxSteps <= 0)
        {
            LogVerbose($"  Exceeded max steps, undoing {dug.Count} tiles");
            UndoCorridorOrCupboard(level, dug, placedDoorA, doorA);
            return false;
        }

        if (level[doorB].Type != TileType.Door && CanPlaceDoor(level, doorB))
            level.PlaceDoor(doorB, RollDoorState());

        LogVerbose($"  Success, dug {dug.Count} tiles");
        LogVerbose("");
        LogLevelVerbose(level);
        return true;
    }

    static bool CanPlaceDoor(Level level, Pos p)
    {
        foreach (var dir in new Pos[] { new(0, 1), new(0, -1), new(1, 0), new(-1, 0) })
        {
            if (level[p + dir].Type == TileType.Door)
                return false;
        }
        return true;
    }

    static void UndoCorridorOrCupboard(Level level, List<Pos> dug, bool placedDoorA, Pos doorA)
    {
        if (placedDoorA && dug.Count > 0 && Rn2(2) == 0)
        {
            for (int i = 1; i < dug.Count; i++)
                level.Set(dug[i], TileType.Rock);
            return;
        }
        
        foreach (Pos p in dug)
            level.Set(p, TileType.Rock);
        if (placedDoorA && level[doorA].Type == TileType.Door)
            level.Set(doorA, TileType.Wall);
    }

    static bool CanDig(Level level, int x, int y)
    {
        if (x <= 0 || x >= level.Width - 1 || y <= 0 || y >= level.Height - 1)
            return false;
        Tile t = level[new(x, y)];
        return t.Type is TileType.Rock or TileType.Corridor;
    }
}
