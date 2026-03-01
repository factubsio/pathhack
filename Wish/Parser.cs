using System.Text.RegularExpressions;

namespace Pathhack.Wish;

// Wish parser: strips modifiers from input, fuzzy-matches an item def, creates via GenerateItem,
// then overrides explicit fields. Anything not specified stays random.
//
// Syntax: [count] [buc] [+potency] [erodeproof] [runes...] <item name> [runes...] [cN]
//   e.g. "4 blessed +3 flaming longsword striking/3 c6"
//
// TODO (for real wishes): gamble mechanic — asking for high values risks fizzle (a la dNH).
public static partial class WishParser
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
        if (matches.Count > 1) return PickMonsterMenu(matches);

        return null;
    }

    static MonsterDef? PickMonsterMenu(List<MonsterDef> matches)
    {
        Menu<MonsterDef> menu = new();
        menu.Add("Which monster?", LineStyle.Heading);
        char letter = 'a';
        foreach (var def in matches)
            menu.Add(letter++, def.Name, def);
        var picked = menu.Display(MenuMode.PickOne);
        return picked.FirstOrDefault();
    }

    public static Item? Parse(string input)
    {
        string remaining = input.Trim().ToLowerInvariant();
        if (remaining.Length == 0) return null;

        Log.Structured("wish", $"{remaining:raw}");
        WishMods mods = StripMods(ref remaining);
        string runeStr = string.Join(", ", mods.Runes?.Select(r => r.Quality is { } q ? $"{r.Name}/{q}" : r.Name) ?? []);
        Log.Structured("wish", $"{remaining:name}{mods.Buc:buc}{mods.Potency:potency}{mods.Count:count}{mods.Charges:charges}{runeStr:runes}");
        var def = FuzzyMatch(remaining);
        if (def == null) { Log.Structured("wish", $"{remaining:nomatch}"); return null; }
        Log.Structured("wish", $"{def.Name:matched}");

        bool hasExplicitRunes = mods.Runes != null;
        bool hasExplicitFundamental = mods.Runes?.Any(r => r.Name is "striking" or "bonus" or "accurate") ?? false;

        Item item = def is WeaponDef or ArmorDef
            ? ItemGen.GenerateItem(def, u.Level.Depth, maxPotency: mods.Potency, propertyRunes: !hasExplicitRunes, fundamental: !hasExplicitFundamental)
            : ItemGen.GenerateItem(def, u.Level.Depth);

        ApplyMods(item, mods);
        return item;
    }

    static List<string> Tokenize(string input)
    {
        List<string> tokens = [];
        int i = 0;
        while (i < input.Length)
        {
            if (input[i] == ' ') { i++; continue; }
            if (input[i] == '\'')
            {
                int end = input.IndexOf('\'', i + 1);
                if (end < 0) end = input.Length;
                tokens.Add(input[(i + 1)..end]);
                i = end + 1;
            }
            else
            {
                int end = input.IndexOf(' ', i);
                if (end < 0) end = input.Length;
                tokens.Add(input[i..end]);
                i = end;
            }
        }
        return tokens;
    }

    static WishMods StripMods(ref string input)
    {
        WishMods mods = new();
        var tokens = Tokenize(input);
        List<string> remaining = [];

        foreach (var tok in tokens)
        {
            if (mods.Count == null && TokenCount().IsMatch(tok))
            { mods.Count = int.Parse(tok); continue; }

            if (mods.Potency == null && TokenPotency().IsMatch(tok))
            { mods.Potency = int.Parse(tok); continue; }

            if (mods.Charges == null && TokenCharges().IsMatch(tok))
            { mods.Charges = int.Parse(tok[1..]); continue; }

            if (mods.Buc == null)
            {
                if (tok == "blessed") { mods.Buc = BUC.Blessed; continue; }
                if (tok == "uncursed") { mods.Buc = BUC.Uncursed; continue; }
                if (tok == "cursed") { mods.Buc = BUC.Cursed; continue; }
            }

            if (_ignoredWords.Contains(tok)) continue;

            var rm = TokenRune().Match(tok);
            if (rm.Success)
            {
                mods.Runes ??= [];
                int? q = rm.Groups[2].Success ? int.Parse(rm.Groups[2].Value) : null;
                mods.Runes.Add((rm.Groups[1].Value, q));
                continue;
            }

            remaining.Add(tok);
        }

        input = string.Join(" ", remaining);
        return mods;
    }

    static void ApplyMods(Item item, WishMods mods)
    {
        if (mods.Buc is { } buc)
            item.BUC = buc;

        if (mods.Potency is { } pot)
            if (item.Def is WeaponDef or ArmorDef || item.Def.CanHavePotency)
                item.Potency = pot;

        if (mods.Count is { } cnt && item.Def.Stackable)
            item.Count = cnt;

        if (mods.Charges is { } charges && item.MaxCharges > 0)
            item.Charges = Math.Min(charges, item.MaxCharges);

        if (mods.Runes is { } runes && item.Def is WeaponDef)
        {
            foreach (var (name, quality) in runes)
            {
                int q = quality is { } v ? Math.Clamp(v, 1, 4) : ItemGen.RollQuality(u.Level.Depth);
                RuneBrick? rune = name switch
                {
                    "striking" => StrikingRune.Of(q),
                    "bonus" or "accurate" => BonusRune.Of(q),
                    "flaming" or "fire" => ElementalRune.Flaming(q),
                    "frost" or "freezing" or "cold" => ElementalRune.Frost(q),
                    "shock" or "shocking" or "electric" => ElementalRune.Shock(q),
                    _ => null,
                };
                if (rune == null) continue;
                bool isFundamental = rune is StrikingRune or BonusRune;
                ItemGen.ApplyRune(item, rune, fundamental: isFundamental);
            }
            // Fill remaining property rune slots randomly.
            // Safe: GenerateItem was called with propertyRunes:false when we have explicit runes,
            // so no existing property runes to conflict with.
            int usedSlots = runes.Count(r => r.Name is not "striking" and not "bonus" and not "accurate");
            ItemGen.RollPropertyRunes(item, u.Level.Depth, startSlot: usedSlots);
        }
    }

    static ItemDef? FuzzyMatch(string input)
    {
        if (input.Length == 0) return null;

        var result = FuzzyMatchInner(input);
        if (result != null) return result;

        // Retry with depluralized input
        string? singular = Depluralize(input);
        return singular != null ? FuzzyMatchInner(singular) : null;
    }

    static string? Depluralize(string s)
    {
        if (s.EndsWith("ies") && s.Length > 3)
            return s[..^3] + "y";
        if (s.EndsWith("ses") || s.EndsWith("xes") || s.EndsWith("ches") || s.EndsWith("shes"))
            return s[..^2];
        if (s.EndsWith('s') && !s.EndsWith("ss") && s.Length > 2)
            return s[..^1];
        return null;
    }

    static ItemDef? FuzzyMatchInner(string input)
    {
        if (input.Length == 0) return null;

        // Exact match
        foreach (var def in AllDefs)
            if (def.Name.Equals(input, StringComparison.OrdinalIgnoreCase))
                return def;

        // Substring match
        List<ItemDef> matches = [];
        foreach (var def in AllDefs)
            if (def.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
                matches.Add(def);

        if (matches.Count == 1) return matches[0];
        if (matches.Count > 1)
        {
            g.pline($"Ambiguous: {string.Join(", ", matches.Select(d => d.Name))}");
            return null;
        }

        // Token match — all input words must appear in name
        string[] tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex TokenCount();

    [GeneratedRegex(@"^[+-]\d+$")]
    private static partial Regex TokenPotency();

    [GeneratedRegex(@"^c(\d+)$")]
    private static partial Regex TokenCharges();

    // Rune names are duplicated here and in ApplyMods — keep in sync manually.
    [GeneratedRegex(@"^(striking|bonus|accurate|flaming|fire|frost|freezing|cold|shock|shocking|electric)(?:/(\d+))?$")]
    private static partial Regex TokenRune();

    static readonly HashSet<string> _ignoredWords =
        ["fixed", "rustproof", "fireproof", "corrodeproof", "erodeproof", "greased"];
}
