namespace Pathhack.Game;

public class FleetBrick : LogicBrick
{
    public override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "speed_bonus" ? new Modifier(ModifierCategory.CircumstanceBonus, 2, "fleet") : null;
}

public static class GeneralFeats
{
    public static readonly FeatDef Fleet = new()
    {
        id = "fleet",
        Name = "Fleet",
        Description = "+2 speed.",
        Type = FeatType.General,
        Level = 1,
        Components = [new FleetBrick()],
    };

    public static readonly FeatDef[] All = [Fleet];
}
