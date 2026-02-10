namespace Pathhack.Game;

public class AncestryDef : BaseDef, ISelectable
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public UnitSize Size = UnitSize.Medium;
    public int Speed = 25;
    public AbilityStat[] Boosts = [];
    public AbilityStat[] Flaws = [];
    public IEnumerable<FeatDef> Feats = [];
    public int DarkVisionRadius = 2;
    public string? WhyNot => null;

    public IEnumerable<string> Details
    {
        get
        {
            if (Boosts.Length > 0)
                yield return "[fg=green]+" + string.Join(" +", Boosts) + "[/fg]";
            if (Flaws.Length > 0)
                yield return "[fg=red]-" + string.Join(" -", Flaws) + "[/fg]";
            if (Size != UnitSize.Medium)
                yield return $"Size: {Size}";
            if (Speed != 25)
                yield return $"Speed: {Speed}";
        }
    }
}

public static class Ancestries
{
    public static readonly AncestryDef Human = new()
    {
        id = "human",
        Name = "Human",
        Description = "Versatile and ambitious. Humans thrive in any environment and adapt to any challenge.",
        Boosts = [AbilityStat.Str, AbilityStat.Dex],
    };

    public static readonly AncestryDef Kasatha = new()
    {
        id = "kasatha",
        Name = "Kasatha",
        Description = "Four-armed wanderers from a distant world. Honor-bound and [fg=cyan]deadly with multiple weapons[/fg].",
        Boosts = [AbilityStat.Str, AbilityStat.Wis],
        Flaws = [AbilityStat.Cha],
        // TODO: 4 arms
    };

    public static readonly AncestryDef Ghoran = new()
    {
        id = "ghoran",
        Name = "Ghoran",
        Description = "Sentient plant creatures. Can [fg=green]photosynthesize[/fg] and regrow from cuttings.",
        Boosts = [AbilityStat.Con, AbilityStat.Cha],
        Flaws = [AbilityStat.Dex],
        // TODO: regen in light, regrow from death
    };

    public static readonly AncestryDef Ratfolk = new()
    {
        id = "ratfolk",
        Name = "Ratfolk",
        Description = "Small, clever, and quick. Masters of [fg=yellow]trade[/fg] and tight spaces.",
        Size = UnitSize.Small,
        Boosts = [AbilityStat.Dex, AbilityStat.Int],
        Flaws = [AbilityStat.Str],
        // TODO: cheek pouches, swarming
    };

    public static readonly AncestryDef Goblin = new()
    {
        id = "goblin",
        Name = "Goblin",
        Description = "Chaotic, curious, and surprisingly [fg=red]fire-resistant[/fg]. Reformed from their villainous past.",
        Size = UnitSize.Small,
        Boosts = [AbilityStat.Dex, AbilityStat.Cha],
        Flaws = [AbilityStat.Wis],
        // TODO: fire resist, eat anything
    };

    public static readonly AncestryDef[] All = [Human, Kasatha, Ghoran, Ratfolk, Goblin];
}
