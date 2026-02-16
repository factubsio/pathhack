namespace Pathhack.Wish;

public static class WishParser
{
    static ItemDef[] AllDefs => AllItems.All;

    public static MonsterDef? ParseMonster(string input)
    {
        string normalized = input.Trim().ToLowerInvariant();
        if (normalized.Length == 0) return null;

        foreach (var def in AllMonsters.ActuallyAll)
            if (def.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return def;

        List<MonsterDef> matches = [];
        foreach (var def in AllMonsters.ActuallyAll)
            if (def.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                matches.Add(def);

        if (matches.Count == 1) return matches[0];
        if (matches.Count > 1)
        {
            g.pline($"Ambiguous: {string.Join(", ", matches.Select(d => d.Name))}");
            return null;
        }

        return null;
    }

    public static Item? Parse(string input)
    {
        var match = FuzzyMatch(input);
        if (match == null) return null;

        return match is WeaponDef or ArmorDef
            ? ItemGen.GenerateItem(match, u.Level.Depth)
            : Item.Create(match);
    }

    static ItemDef? FuzzyMatch(string input)
    {
        string normalized = input.Trim().ToLowerInvariant();
        if (normalized.Length == 0) return null;

        // Exact match
        foreach (var def in AllDefs)
            if (def.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return def;

        // Substring match
        List<ItemDef> matches = [];
        foreach (var def in AllDefs)
            if (def.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                matches.Add(def);

        if (matches.Count == 1) return matches[0];
        if (matches.Count > 1)
        {
            g.pline($"Ambiguous: {string.Join(", ", matches.Select(d => d.Name))}");
            return null;
        }

        // Token match — all input words must appear in name
        string[] tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var def in AllDefs)
        {
            string name = def.Name.ToLowerInvariant();
            if (tokens.All(t => name.Contains(t)))
                matches.Add(def);
        }

        if (matches.Count == 1) return matches[0];
        if (matches.Count > 1)
        {
            g.pline($"Ambiguous: {string.Join(", ", matches.Select(d => d.Name))}");
            return null;
        }

        return null;
    }
}
