namespace Pathhack.Game;

public static class QuestItems
{
    public static readonly ItemDef Everflame = new()
    {
        id = "everflame",
        Name = "Everflame",
        IsUnique = true,
        Glyph = new('*', ConsoleColor.Yellow),
        Weight = 1,
    };
}
