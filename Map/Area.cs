using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Pathhack.Map;

public interface IArea
{
    public string Name { get; }
    public Glyph Glyph { get; }
    public int ZOrder { get; }
    public IEnumerable<Pos> Tiles { get; }

}

public abstract class Area(int duration) : IArea
{
    public abstract string Name { get; }
    public abstract Glyph Glyph { get; }
    public virtual int ZOrder => 0;
    public IEnumerable<Pos> Tiles => TileSet;

    public HashSet<Pos> TileSet = [];
    public virtual bool IsDifficultTerrain => false;

    public int ExpiresAt = g.CurrentRound + duration;
    public HashSet<IUnit> Occupants = [];

    public bool Contains(Pos p) => TileSet.Contains(p);

    public void HandleMove(IUnit unit)
    {
        if (Occupants.Add(unit))
            OnEnter(unit);
        else
            OnMove(unit);
    }

    public void HandleExit(IUnit unit)
    {
        if (Occupants.Remove(unit))
            OnExit(unit, false);
    }

    protected virtual void OnEnter(IUnit unit) { }
    protected virtual void OnMove(IUnit unit) { }
    protected virtual void OnExit(IUnit unit, bool areaFaded) { }
    protected virtual void OnTick() { }

    public void Tick()
    {
        if (g.CurrentRound >= ExpiresAt)
        {
            foreach (var unit in Occupants)
                OnExit(unit, true);
        }
        else
        {
            OnTick();
        }
    }
}

public abstract class Swarm(string name, Glyph glyph, int wounds, Pos where) : IArea
{
    public string Name => name;
    public Glyph Glyph => glyph;
    public int ZOrder => 0;
    public int Wounds { get; private set; } = wounds;
    public bool IsDead => Wounds == 0;

    public Pos Where { get; private set; } = where;

    public IEnumerable<Pos> Tiles
    {
        get
        {
            yield return Where;
        }
    }

    public void Tick()
    {
        Pos? next = null;
        var toU = Where.ChebyshevDist(upos);
        if (toU == 0)
        {
            // don't move
        }
        else if (toU < 8 && g.Rn2(3) > 0)
        {
            int bestScore = int.MaxValue;
            foreach (var dir in Pos.AllDirs.Shuffled())
            {
                Pos candidate = Where + dir;
                if (!CanMoveTo(candidate)) continue;

                int dist = candidate.ChebyshevDist(upos);
                int score = dist;

                bool wins = score < bestScore
                    || (score == bestScore && g.Rn2(dir.IsDiagonal ? 6 : 3) == 0);

                if (wins)
                {
                    next = candidate;
                    bestScore = score;
                }
            }
        }
        else
        {
            // move randomly
            Pos candidate = Where + Pos.AllDirs.Pick();
            if (CanMoveTo(candidate)) next = candidate;
        }

        if (next.HasValue)
        {
            Where = next.Value;
        }

        // TODO: some swarms are airborn/sea-boarn, probably want a mask?
        if (lvl.UnitAt(Where) is {} tgt && !tgt.Has(CreatureTags.Flying))
        {
            SwarmUnit(tgt);
        }

        bool CanMoveTo(Pos candidate)
        {
            if (!lvl.InBounds(candidate)) return false;
            if (!lvl.CanMoveTo(Where, candidate, null)) return false;
            if (lvl.AllSwarms.Any(x => x.Where == candidate)) return false; // a bit lame but it's probably ok
            return true;
        }
    }

    protected abstract void SwarmUnit(IUnit unit);

    internal bool Damage(int v)
    {
        Wounds = Math.Max(Wounds - v, 0);
        return IsDead;
    }
}
