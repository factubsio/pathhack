namespace Pathhack.Game;

public class RuneItemDef : ItemDef
{
    public required RuneBrick Rune;

    public RuneItemDef()
    {
        Glyph = new(ItemClasses.Gem, ConsoleColor.Magenta);
        AppearanceCategory = Game.AppearanceCategory.Rune;
        Weight = 5;
    }
}

[GenerateAll("All", typeof(RuneItemDef))]
public static partial class Runes
{
    static RuneItemDef Make(string name, RuneBrick rune, int price) => new()
    {
        Name = $"rune of {name}",
        Rune = rune,
        Price = price,
    };

    public static readonly RuneItemDef Striking1 = Make("lesser striking", StrikingRune.Q1, 200);
    public static readonly RuneItemDef Striking2 = Make("striking", StrikingRune.Q2, 500);
    public static readonly RuneItemDef Striking3 = Make("greater striking", StrikingRune.Q3, 1000);
    public static readonly RuneItemDef Striking4 = Make("overwhelming striking", StrikingRune.Q4, 2000);

    public static readonly RuneItemDef Flaming1 = Make("lesser flaming", ElementalRune.Flaming1, 300);
    public static readonly RuneItemDef Flaming2 = Make("flaming", ElementalRune.Flaming2, 600);
    public static readonly RuneItemDef Flaming3 = Make("greater flaming", ElementalRune.Flaming3, 1200);
    public static readonly RuneItemDef Flaming4 = Make("overwhelming flaming", ElementalRune.Flaming4, 2400);

    public static readonly RuneItemDef Frost1 = Make("lesser frost", ElementalRune.Frost1, 300);
    public static readonly RuneItemDef Frost2 = Make("frost", ElementalRune.Frost2, 600);
    public static readonly RuneItemDef Frost3 = Make("greater frost", ElementalRune.Frost3, 1200);
    public static readonly RuneItemDef Frost4 = Make("overwhelming frost", ElementalRune.Frost4, 2400);

    public static readonly RuneItemDef Shock1 = Make("lesser shock", ElementalRune.Shock1, 300);
    public static readonly RuneItemDef Shock2 = Make("shock", ElementalRune.Shock2, 600);
    public static readonly RuneItemDef Shock3 = Make("greater shock", ElementalRune.Shock3, 1200);
    public static readonly RuneItemDef Shock4 = Make("overwhelming shock", ElementalRune.Shock4, 2400);

    public static readonly RuneItemDef[] Strikings = [Striking1, Striking2, Striking3, Striking4];
    public static readonly RuneItemDef[] Flamings = [Flaming1, Flaming2, Flaming3, Flaming4];
    public static readonly RuneItemDef[] Frosts = [Frost1, Frost2, Frost3, Frost4];
    public static readonly RuneItemDef[] Shocks = [Shock1, Shock2, Shock3, Shock4];
}
