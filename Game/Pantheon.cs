using Pathhack.UI;

namespace Pathhack.Game;

public class DeityDef : ISelectable
{
    public required string Id;
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Alignment;
    public required string FavoredWeapon;
    public required string[] Aspects;
    public string[] Tags => Aspects;
    public string Subtitle => $"[fg={AlignmentToColor}]{Alignment}[/fg]";

    private string AlignmentToColor => 
        Alignment[1] switch
        {
            'G' => "green",
            'N' => "gray",
            'E' => "red",
            _ => "cyan",
        };


}

public static class Pantheon
{
    public static readonly DeityDef Iomedae = new()
    {
        Id = "iomedae",
        Aspects = ["Glory", "Good", "War"],
        Name = "Iomedae",
        Description = "Iomedae, the youngest among the prominent deities of the Inner Sea region, had already proven herself worthy of divinity before her ascension. Born in Cheliax, she followed the path of the sword and fought evil, eventually becoming a paladin of Aroden’s herald Arazni. She became a legend among the Shining Crusade, leading the Knights of Ozem in a series of victories over the Whispering Tyrant. Iomedae became the third known mortal to pass the Test of the Starstone when she ascended to divinity in 3832 AR. As Arazni had been slain during the Shining Crusade, Aroden elevated the newly ascended goddess to be his new herald. When Aroden himself died, Iomedae inherited most of his worshippers and became a major deity of honor and justice.",
        Alignment = "LG",
        FavoredWeapon = Proficiencies.Longsword,
    };

    public static readonly DeityDef Sarenrae = new()
    {
        Id = "sarenrae",
        Aspects = ["Sun", "Fire", "Healing"],
        Name = "Sarenrae",
        Description = "The Dawnflower. Goddess of the [fg=yellow]sun[/fg], redemption, and healing. Offers mercy to those who seek it.",
        Alignment = "NG",
        FavoredWeapon = Proficiencies.Scimitar,
    };

    public static readonly DeityDef CaydenCailean = new()
    {
        Id = "cayden_cailean",
        Aspects = ["Chaos", "Strength", "Travel"],
        Name = "Cayden Cailean",
        Description = "The Drunken Hero. God of [fg=yellow]freedom[/fg], ale, and bravery. Ascended mortal who won godhood on a dare.",
        Alignment = "CG",
        FavoredWeapon = Proficiencies.Rapier,
    };

    public static readonly DeityDef Irori = new()
    {
        Id = "irori",
        Aspects = ["Knowledge", "Law", "Rune"],
        Name = "Irori",
        Description = "Master of Masters. God of [fg=yellow]self-perfection[/fg], knowledge, and discipline. Achieved godhood through self-mastery.",
        Alignment = "LN",
        FavoredWeapon = Proficiencies.Unarmed,
    };

    public static readonly DeityDef Calistria = new()
    {
        Id = "calistria",
        Aspects = ["Curse", "Luck", "Trickery"],
        Name = "Calistria",
        Description = "The Savored Sting. Goddess of [fg=yellow]revenge[/fg], lust, and trickery. Patron of elves and the scorned.",
        Alignment = "CN",
        FavoredWeapon = Proficiencies.Whip,
    };

    public static readonly DeityDef ZonKuthon = new()
    {
        Id = "zon_kuthon",
        Aspects = ["Darkness", "Destruction", "Shadow"],
        Name = "Zon-Kuthon",
        Description = "The Midnight Lord. God of [fg=red]pain[/fg], darkness, and loss. Once a god of beauty, now twisted beyond recognition.",
        Alignment = "LE",
        FavoredWeapon = Proficiencies.SpikedChain,
    };

    public static readonly DeityDef Urgathoa = new()
    {
        Id = "urgathoa",
        Aspects = ["Death", "Magic", "Strength"],
        Name = "Urgathoa",
        Description = "The Pallid Princess. Goddess of [fg=red]undeath[/fg], disease, and gluttony. First mortal to reject the cycle of souls.",
        Alignment = "NE",
        FavoredWeapon = Proficiencies.Scythe,
    };

    public static readonly DeityDef Lamashtu = new()
    {
        Id = "lamashtu",
        Aspects = ["Chaos", "Strength", "Trickery"],
        Name = "Lamashtu",
        Description = "Mother of Monsters. Goddess of [fg=red]madness[/fg], nightmares, and deformity. Creator of the world's foulest creatures.",
        Alignment = "CE",
        FavoredWeapon = Proficiencies.Falchion,
    };

    public static readonly DeityDef[] All = [Iomedae, Sarenrae, CaydenCailean, Irori, Calistria, ZonKuthon, Urgathoa, Lamashtu];
}
