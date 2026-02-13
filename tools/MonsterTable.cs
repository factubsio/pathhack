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
}
