namespace Pathhack.Game;

public class FleetBrick : LogicBrick
{
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "speed_bonus" ? new Modifier(ModifierCategory.CircumstanceBonus, 2, "fleet") : null;
}

public static class GeneralFeats
{
    public static readonly FeatDef Fleet = new()
    {
        id = "fleet",
        Name = "Fleet",
        Description = "You move a little faster. Note: this does not affect the time taken to perform combat or item actions.",
        Type = FeatType.General,
        Level = 1,
        Components = [new FleetBrick()],
    };

    public static readonly FeatDef[] All = [Fleet];
}
