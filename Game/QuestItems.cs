namespace Pathhack.Game;

public class EverflameData
{
    public Pos Shrine = Pos.Invalid;
    public Pos Lock = Pos.Invalid;
    public int DistanceToShrine = 100;
    public int LightRadiusForced = 100;
    public bool Locked = true;
}

public class EverflameComponent : LogicBrick<EverflameData>
{
    public override string Id => "_everflame";
    public override bool IsActive => true;

    protected override void OnRoundStart(Fact fact)
    {
        var data = X(fact);

        if (lvl.Branch.Name == "Dungeon" && lvl.Depth == lvl.Branch.MaxDepth)
        {
            if (data.Shrine == Pos.Invalid)
            {
                g.pline($"Radiance overflows from the Everflame!");
                data.Shrine = lvl.FindTile(p => lvl.HasFeature(p, "shrine")) ?? Pos.Invalid;
                data.Lock = lvl.FindTile(p => lvl.HasFeature(p, "_lock")) ?? Pos.Invalid;
            }

            if (upos == data.Lock && data.Locked)
            {
                g.pline($"The blazing light dispells the illusion of a wall");
                lvl.GetOrCreateState(data.Lock).Message = null;
                lvl.Set(data.Lock + Pos.NW, TileType.Floor);
                lvl.Set(data.Lock + Pos.W, TileType.Floor);
                lvl.Set(data.Lock + Pos.SW, TileType.Floor);
                data.Locked = false;
            }

            data.DistanceToShrine = data.Shrine.ChebyshevDist(upos);
            data.LightRadiusForced = Math.Clamp(data.DistanceToShrine - 5, -1, 5);
        }
    }

    protected override object? OnQuery(Fact fact, string key, string? arg)
    {
        var data = X(fact);
        if (data.LightRadiusForced != 100 && key == "light_radius")
            return new Modifier(ModifierCategory.Override, data.LightRadiusForced, "everflame", 10);

        return null;
    }
}

public static class QuestItems
{
    public static readonly ItemDef Everflame = new()
    {
        id = "everflame",
        Name = "Everflame",
        IsUnique = true,
        Glyph = new('*', ConsoleColor.Yellow),
        Weight = 1,
        Components = [new EverflameComponent()],
        Price = 0,
    };
}
