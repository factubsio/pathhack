namespace Pathhack.UI;

public enum RunMode { None, UntilBlocked, UntilInteresting, Travel }

public static class Movement
{
    public static RunMode Mode;
    public static Pos Dir;
    public static List<Pos>? TravelPath;
    static int _travelIdx;

    static bool IsInteresting(Pos p) => lvl[p].IsStairs;

    public static void StartRun(RunMode mode, Pos dir)
    {
        Mode = mode;
        Dir = dir;
        TravelPath = null;
    }

    public static void StartTravel(List<Pos> path)
    {
        Mode = RunMode.Travel;
        TravelPath = path;
        _travelIdx = 0;
    }

    public static void Stop() => Mode = RunMode.None;

    public static bool TryContinueRun()
    {
        Log.Verbose("movement", $"TryContinueRun: Mode={Mode} TravelPath={TravelPath?.Count} idx={_travelIdx}");
        if (Mode == RunMode.None) return false;

        if (Mode == RunMode.Travel)
        {
            if (TravelPath == null || _travelIdx >= TravelPath.Count)
            {
                Log.Verbose("movement", "Travel: path exhausted");
                Stop();
                return false;
            }
            Dir = TravelPath[_travelIdx++];
            Log.Verbose("movement", $"Travel: step {_travelIdx}, dir={Dir}");
        }

        // Check blockers before moving
        Pos next = upos + Dir;
        if (!lvl.InBounds(next) || !lvl.CanMoveTo(upos, next, u))
        {
            Log.Verbose("movement", "Can't move there");
            Stop();
            return false;
        }

        if (lvl.UnitAt(next) != null)
        {
            Log.Verbose("movement", "Blocked by monster");
            Stop();
            return false;
        }

        if (lvl.Traps.TryGetValue(next, out var trap) && trap.PlayerSeen)
        {
            Log.Verbose("movement", "Trap ahead");
            Stop();
            return false;
        }

        // Shift: stop ON doorways/transitions (check before moving through)
        if (Mode == RunMode.UntilBlocked)
        {
            bool nowInRoom = lvl[upos].Type == TileType.Floor;
            bool nextIsRoom = lvl[next].Type == TileType.Floor;
            if (lvl.IsDoor(next) || (nowInRoom != nextIsRoom))
            {
                // Move onto the threshold, then stop
                Log.Verbose("movement", $"Moving to doorway/transition at {next}");
                lvl.MoveUnit(u, next);
                Stop();
                return true;
            }
        }

        // Move
        Log.Verbose("movement", $"Moving from {upos} to {next}");
        lvl.MoveUnit(u, next);
        // Note: MoveUnit already charges energy

        // Check if we should stop AFTER this move
        if (ShouldStopAfterMove())
        {
            Log.Verbose("movement", "Stopping after move");
            Stop();
        }

        return true;
    }

    static bool ShouldStopAfterMove()
    {
        if (Mode == RunMode.UntilBlocked)
        {
            // Stop ON items
            if (lvl.ItemsAt(upos).Count > 0)
            {
                Log.Verbose("movement", "Stop: items here");
                return true;
            }

            // Stop ON stairs
            if (lvl[upos].IsStairs)
            {
                Log.Verbose("movement", "Stop: on stairs");
                return true;
            }

            // Only stop for monster directly ahead
            Pos ahead = upos + Dir;
            Pos behind = upos - Dir;
            if (lvl.InBounds(ahead) && lvl.UnitAt(ahead) is { IsDead: false } m && m != u)
            {
                Log.Verbose("movement", $"Stop: monster ahead at {ahead}");
                return true;
            }

            // Corridor following: if blocked ahead, try to turn
            if (!lvl.InBounds(ahead) || !lvl.CanMoveTo(upos, ahead, u))
            {
                // Count passable exits (excluding behind)
                List<Pos> exits = [];
                foreach (var d in Pos.AllDirs)
                {
                    Pos p = upos + d;
                    if (p == behind) continue;
                    if (lvl.InBounds(p) && lvl.CanMoveTo(upos, p, u) && lvl.UnitAt(p) == null)
                        exits.Add(d);
                }

                if (exits.Count == 1)
                {
                    // Single exit - turn and continue
                    Dir = exits[0];
                    Log.Verbose("movement", $"Corridor turn: new dir={Dir}");
                    return false;
                }

                // No exits or multiple - stop
                Log.Verbose("movement", "Stop: blocked, no single exit");
                return true;
            }

            return false;
        }

        // UntilInteresting: stop for any adjacent monster (except behind)
        foreach (var n in upos.Neighbours())
        {
            if (n == upos - Dir) continue;
            if (!lvl.InBounds(n)) continue;
            if (lvl.UnitAt(n) is { IsDead: false } m && m != u)
            {
                Log.Verbose("movement", $"Stop: monster at {n}");
                return true;
            }
        }

        if (Mode == RunMode.UntilInteresting)
        {
            // Stop if we're on interesting terrain
            if (lvl[upos].IsStairs)
            {
                Log.Verbose("movement", "Stop: on stairs");
                return true;
            }
            if (lvl.ItemsAt(upos).Count > 0)
            {
                Log.Verbose("movement", "Stop: items here");
                return true;
            }

            // Check forward neighbors (5 tiles: excludes behind + behind-diagonals)
            foreach (var offset in Pos.ForwardNeighbours[Dir])
            {
                Pos p = upos + offset;
                if (!lvl.InBounds(p)) continue;

                // Stop for stairs/interesting terrain
                if (IsInteresting(p))
                {
                    Log.Verbose("movement", $"Stop: interesting at {p}");
                    return true;
                }

                // Stop for closed doors
                if (lvl.IsDoorClosed(p))
                {
                    Log.Verbose("movement", $"Stop: closed door at {p}");
                    return true;
                }

                // Stop for open doors (any adjacent)
                if (lvl.IsDoor(p) && !lvl.IsDoorClosed(p))
                {
                    Log.Verbose("movement", $"Stop: open door at {p}");
                    return true;
                }
            }

            // Check for new corridor openings (wall was beside us, now there's a path)
            Pos left = new(-Dir.Y, Dir.X);
            Pos right = new(Dir.Y, -Dir.X);
            Pos ahead = upos + Dir;
            bool diagonal = Dir.X != 0 && Dir.Y != 0;

            if (!diagonal && lvl.InBounds(ahead))
            {
                foreach (var side in (Pos[])[left, right])
                {
                    Pos sidePos = upos + side;
                    Pos aheadSide = ahead + side;
                    if (!lvl.InBounds(aheadSide)) continue;

                    bool wallToSide = !lvl.InBounds(sidePos) || !lvl.CanMoveTo(upos, sidePos, u);
                    bool openAhead = lvl.CanMoveTo(ahead, aheadSide, u);
                    if (wallToSide && openAhead)
                    {
                        Log.Verbose("movement", $"Stop: upcoming opening at {aheadSide}");
                        return true;
                    }
                }
            }
        }

        if (Mode == RunMode.Travel)
        {
            if (lvl.ItemsAt(upos).Count > 1)
            {
                Log.Verbose("movement", "Stop: items here");
                return true;
            }
            if (IsInteresting(upos))
            {
                Log.Verbose("movement", "Stop: interesting here");
                return true;
            }
        }

        return false;
    }
}
