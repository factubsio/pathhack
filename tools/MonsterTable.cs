// dotnet run -- --monsters

using System.Reflection;

public static class MonsterTable
{
    public static void Print(string? family)
    {
        var defs = AllMonsters.ActuallyAll
            .Where(m => family == null || m.Family.Contains(family, StringComparison.InvariantCultureIgnoreCase))
            .DistinctBy(m => m.id)
            .OrderBy(m => m.BaseLevel)
            .ThenBy(m => m.Name);

        Console.WriteLine($"{"Name",-24} {"Lvl",3} {"HP/L",4} {"AC",3} {"AB",3} {"Dmg",3} {"Move",4} {"Size",-8} {"Family",-10} {"Group",-6} Actions");
        Console.WriteLine(new string('-', 120));

        foreach (var m in defs)
        {
            var actions = m.Components
                .OfType<GrantAction>()
                .Select(ga => ga.Action.PokedexDescription ?? ga.Action.Name)
                .ToList();
            var passives = m.Components
                .Where(c => c is not GrantAction && c.PokedexDescription != null)
                .Select(c => c.PokedexDescription!)
                .ToList();
            string actStr = string.Join(", ", actions.Concat(passives));
            string grp = m.GroupSize switch
            {
                GroupSize.Small => "S",
                GroupSize.SmallMixed => "SM",
                GroupSize.Large => "L",
                GroupSize.LargeMixed => "LM",
                _ => ""
            };
            Console.WriteLine($"{m.Name,-24} {m.BaseLevel,3} {m.HpPerLevel,4} {m.AC.Combined,3} {m.AttackBonus,3} {m.DamageBonus,3} {m.LandMove.Value,4} {m.Size,-8} {m.Family ?? "",-10} {grp,-6} {actStr}");
        }
    }

    public static void PrintItems(string? filter)
    {
        var defs = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.FieldType.IsAssignableTo(typeof(ItemDef)))
            .Select(f => (ItemDef)f.GetValue(null)!)
            .Where(i => filter == null || ClassName(i.Class).Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Class)
            .ThenBy(i => i.Name);

        Console.WriteLine($"{"Name",-24} {"Class",-10} {"Price",6}");
        Console.WriteLine(new string('-', 44));

        foreach (var i in defs)
            Console.WriteLine($"{i.Name,-24} {ClassName(i.Class),-10} {i.Price,6}");
    }

    static string ClassName(char c) => c switch
    {
        ItemClasses.Weapon => "Weapon",
        ItemClasses.Armor => "Armor",
        ItemClasses.Food => "Food",
        ItemClasses.Potion => "Potion",
        ItemClasses.Scroll => "Scroll",
        ItemClasses.Spellbook => "Spellbook",
        ItemClasses.Wand => "Wand",
        ItemClasses.Ring => "Ring",
        ItemClasses.Amulet => "Amulet",
        ItemClasses.Tool => "Tool",
        ItemClasses.Gem => "Gem",
        ItemClasses.Gold => "Gold",
        _ => "Other",
    };

    public static void PrintSpells(string? filter)
    {
        var spells = MasonryYard.AllSpells
            .Where(s => filter == null || s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Level)
            .ThenBy(s => s.Name);

        Console.WriteLine($"{"Name",-28} {"Lvl",3} {"Target",-10} {"Range",5} {"Maint",-6} {"Tags",-16} Description");
        Console.WriteLine(new string('-', 130));

        foreach (var s in spells)
        {
            string tags = s.Tags == AbilityTags.None ? "" : s.Tags.ToString();
            string range = s.MaxRange < 0 ? "-" : s.MaxRange.ToString();
            string desc = s.Description.ReplaceLineEndings(" ");
            if (desc.Length > 50) desc = desc[..47] + "...";
            Console.WriteLine($"{s.Name,-28} {s.Level,3} {s.Targeting,-10} {range,5} {(s.Maintained ? "Y" : ""),-6} {tags,-16} {desc}");
        }

        Console.WriteLine($"\n{spells.Count()} spells registered.");
    }

    public static void PrintBricks(string? filter)
    {
        var bricks = MasonryYard.AllBricks
            .Where(kv => filter == null || kv.Key.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key);

        Console.WriteLine($"{"Id",-40} {"Type",-30} {"Buff?",-6} {"Active?",-8}");
        Console.WriteLine(new string('-', 86));

        foreach (var (id, brick) in bricks)
            Console.WriteLine($"{id,-40} {brick.GetType().Name,-30} {(brick.IsBuff ? "Y" : ""),-6} {(brick.IsActive ? "Y" : ""),-8}");

        Console.WriteLine($"\n{bricks.Count()} bricks registered.");
    }
}
