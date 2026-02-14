namespace Pathhack.Game;

public record struct BeamStep(Pos SegmentStart, Pos Pos, int Bounces, bool WillBounce, bool IsLast);

public class Beam(Pos start, Pos dir, bool canBounce = false, int maxRange = 12) : IEnumerable<BeamStep>
{
    public static Beam Fire(Pos start, Pos dir, bool canBounce = false, int maxRange = 12) =>
        new(start, dir, canBounce, maxRange);

    public IEnumerator<BeamStep> GetEnumerator()
    {
        Pos pos = start;
        Pos segmentStart = start;
        Pos d = dir.Signed;
        int bounces = 0;

        for (int i = 0; i < maxRange; i++)
        {
            Pos next = pos + d;
            
            if (!lvl.InBounds(next)) yield break;
            
            if (!lvl[next].IsPassable)
            {
                if (!canBounce)
                {
                    if (pos != start)
                        yield return new BeamStep(segmentStart, pos, bounces, false, true);
                    yield break;
                }
                
                // Yield current pos with WillBounce before changing direction
                yield return new BeamStep(segmentStart, pos, bounces, true, i == maxRange - 1);
                
                d = Bounce(pos, next, d);
                bounces++;
                segmentStart = pos;
                next = pos + d;
                
                Log.Write($"After bounce: pos={pos} d={d} next={next} passable={lvl.InBounds(next) && lvl[next].IsPassable}");
                
                if (!lvl.InBounds(next) || !lvl[next].IsPassable) yield break;
            }

            pos = next;
            yield return new BeamStep(segmentStart, pos, bounces, false, i == maxRange - 1);
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    static Pos Bounce(Pos lastPos, Pos wallPos, Pos dir)
    {
        // Cardinal directions just reverse
        if (dir.X == 0 || dir.Y == 0)
            return new(-dir.X, -dir.Y);
        
        // Diagonal: check which directions are clear
        Pos flipX = new(-dir.X, dir.Y);  // flip X, keep Y
        Pos flipY = new(dir.X, -dir.Y);  // keep X, flip Y
        
        // Check if we can continue in just X or just Y direction from lastPos
        Pos hCheck = lastPos + new Pos(dir.X, 0);  // one step in X only
        Pos vCheck = lastPos + new Pos(0, dir.Y);  // one step in Y only
        
        bool hClear = lvl.InBounds(hCheck) && lvl[hCheck].IsPassable;
        bool vClear = lvl.InBounds(vCheck) && lvl[vCheck].IsPassable;
        
        Log.Write($"Bounce: lastPos={lastPos} wallPos={wallPos} dir={dir} hCheck={hCheck}({hClear}) vCheck={vCheck}({vClear})");
        
        // If X is clear, keep X (flip Y). If Y is clear, keep Y (flip X).
        if (hClear && !vClear) return flipY;
        if (vClear && !hClear) return flipX;
        if (hClear && vClear) return g.Rn2(2) == 0 ? flipX : flipY;
        
        return new(-dir.X, -dir.Y);
    }
}
