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

        // Move
        Log.Verbose("movement", $"Moving from {upos} to {next}");
        lvl.MoveUnit(u, next);
        u.Energy -= ActionCosts.OneAction.Value;

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
        // Always stop if monster adjacent (except directly behind)
        foreach (var n in upos.Neighbours())
        {
            if (n == upos - Dir) continue;
            if (lvl.UnitAt(n) is { } m && m != u)
            {
                Log.Verbose("movement", $"Stop: monster at {n}");
                return true;
            }
        }

        if (Mode == RunMode.UntilBlocked)
        {
            Pos next = upos + Dir;
            return !lvl.InBounds(next) || !lvl.CanMoveTo(upos, next, u);
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

            // Check sides for openings (perpendicular to movement)
            Pos left = new(-Dir.Y, Dir.X);
            Pos right = new(Dir.Y, -Dir.X);

            // Check one tile ahead for upcoming openings
            // If wall to side now, but diagonal-ahead is passable = opening coming up
            Pos ahead = upos + Dir;
            if (lvl.InBounds(ahead))
            {
                foreach (var side in (Pos[])[left, right])
                {
                    Pos sidePos = upos + side;
                    Pos aheadSide = ahead + side;
                    if (!lvl.InBounds(aheadSide)) continue;

                    // Wall to side now, but passable diagonal-ahead = upcoming opening
                    bool wallToSide = !lvl.InBounds(sidePos) || !lvl.CanMoveTo(upos, sidePos, u);
                    bool openAhead = lvl.CanMoveTo(ahead, aheadSide, u);

                    if (wallToSide && openAhead)
                    {
                        Log.Verbose("movement", $"Stop: upcoming opening at {aheadSide}");
                        return true;
                    }
                }

                // Closed door ahead - stop adjacent
                if (lvl.IsDoorClosed(ahead))
                {
                    Log.Verbose("movement", "Stop: closed door ahead");
                    return true;
                }

                // Interesting terrain ahead - stop adjacent
                if (IsInteresting(ahead))
                {
                    Log.Verbose("movement", "Stop: interesting ahead");
                    return true;
                }
            }

            // Check current position sides (only for cardinal movement)
            if (Dir.X == 0 || Dir.Y == 0)
            {
                foreach (var side in (Pos[])[left, right])
                {
                    Pos sidePos = upos + side;
                    if (!lvl.InBounds(sidePos)) continue;

                    // Any door directly to side - stop adjacent
                    if (lvl.IsDoor(sidePos))
                    {
                        Log.Verbose("movement", $"Stop: door to side at {sidePos}");
                        return true;
                    }

                    var tile = lvl[sidePos];

                    // Corridor/floor opening to the side that wasn't there before
                    if (tile.Type is TileType.Floor or TileType.Corridor)
                    {
                        Pos prevSide = (upos - Dir) + side;
                        if (lvl.InBounds(prevSide) && !lvl.CanMoveTo(upos - Dir, prevSide, u))
                        {
                            Log.Verbose("movement", $"Stop: new opening to side at {sidePos}");
                            return true;
                        }
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
