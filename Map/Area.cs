namespace Pathhack.Map;

public abstract class Area(int duration)
{
    public abstract string Name { get; }
    public abstract Glyph Glyph { get; }
    public virtual int ZOrder => 0;
    public virtual bool IsDifficultTerrain => false;

    public HashSet<Pos> Tiles = [];
    public int ExpiresAt = g.CurrentRound + duration;
    public HashSet<IUnit> Occupants = [];

    public bool Contains(Pos p) => Tiles.Contains(p);

    public void HandleEnter(IUnit unit)
    {
        if (Occupants.Add(unit))
            OnEnter(unit);
    }

    public void HandleExit(IUnit unit)
    {
        if (Occupants.Remove(unit))
            OnExit(unit, false);
    }

    protected virtual void OnEnter(IUnit unit) { }
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
