namespace Pathhack.Game;

public enum BeamHit { Stop, Continue }

[Flags]
public enum BeamFlags
{
    None        = 0,
    BounceOnWall = 1,
    Reflectable  = 2,
}

public static class Beam
{
    static Pos Bounce(Pos lastPos, Pos wallPos, Pos dir)
    {
        // Cardinal directions just reverse
        if (dir.X == 0 || dir.Y == 0)
            return new(-dir.X, -dir.Y);
        
        // Diagonal: check which directions are clear
        Pos flipX = new(-dir.X, dir.Y);  // flip X, keep Y
        Pos flipY = new(dir.X, -dir.Y);  // keep X, flip Y
        
        Pos hCheck = lastPos + new Pos(dir.X, 0);
        Pos vCheck = lastPos + new Pos(0, dir.Y);
        
        bool hClear = lvl.InBounds(hCheck) && lvl[hCheck].IsPassable;
        bool vClear = lvl.InBounds(vCheck) && lvl[vCheck].IsPassable;
        
        if (hClear && !vClear) return flipY;
        if (vClear && !hClear) return flipX;
        if (hClear && vClear) return g.Rn2(2) == 0 ? flipX : flipY;
        
        return new(-dir.X, -dir.Y);
    }

    public static Pos? Cast(
        Pos origin, Pos dir, string name, Glyph glyph, int range,
        BeamFlags flags,
        Func<IUnit, BeamHit> onHit,
        bool pulse = false,
        bool projectile = false,
        Action<Pos>? onTile = null)
    {
        Pos pos = origin;
        Pos segmentStart = origin;
        Pos d = dir.Signed;
        Pos? last = null;
        bool bounce = flags.HasFlag(BeamFlags.BounceOnWall);
        bool reflectable = flags.HasFlag(BeamFlags.Reflectable);

        void Animate(Pos from, Pos to)
        {
            if (projectile) Draw.AnimateProjectile(from, to, glyph);
            else Draw.AnimateBeam(from, to, glyph, pulse: pulse);
        }

        for (int i = 0; i < range; i++)
        {
            Pos next = pos + d;

            if (!lvl.InBounds(next)) break;

            if (!lvl[next].IsPassable)
            {
                if (!bounce) break;

                if (pos != origin)
                    Animate(segmentStart, pos);

                d = Bounce(pos, next, d);
                segmentStart = pos;
                next = pos + d;

                if (!lvl.InBounds(next) || !lvl[next].IsPassable) break;
            }

            pos = next;
            last = pos;
            onTile?.Invoke(pos);

            var unit = lvl.UnitAt(pos);
            if (unit != null)
            {
                if (reflectable)
                {
                    var reflection = unit.Query<Func<IUnit, string>>("reflection");
                    if (reflection != null)
                    {
                        Animate(segmentStart, pos);
                        g.YouObserve(unit, $"The {name} {reflection(unit)}.");
                        d = new(-d.X, -d.Y);
                        segmentStart = pos;
                        continue;
                    }
                }

                Animate(segmentStart, pos);
                BeamHit result = onHit(unit);

                if (result == BeamHit.Stop)
                    return pos;

                segmentStart = pos;
                continue;
            }
        }

        if (last != null && segmentStart != last)
            Animate(segmentStart, last.Value);

        return last;
    }
}
