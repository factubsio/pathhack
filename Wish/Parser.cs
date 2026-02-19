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
        if (matches.Count > 1)
        {
            g.pline($"Ambiguous: {string.Join(", ", matches.Select(d => d.Name))}");
            return null;
        }

        return null;
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

    static WishMods StripMods(ref string input)
    {
        WishMods mods = new();

        // Trailing charges: "c6", "c12" — strip first so it doesn't interfere
        var chargeMatch = TrailingCharges().Match(input);
        if (chargeMatch.Success)
        {
            mods.Charges = int.Parse(chargeMatch.Groups[1].Value);
            input = input[..chargeMatch.Index].TrimEnd();
        }

        // Trailing runes: "striking/3 flaming/2" or bare "striking flaming"
        while (true)
        {
            var runeMatch = TrailingRune().Match(input);
            if (!runeMatch.Success) break;
            mods.Runes ??= [];
            int? q = runeMatch.Groups[2].Success ? int.Parse(runeMatch.Groups[2].Value) : null;
            mods.Runes.Add((runeMatch.Groups[1].Value, q));
            input = input[..runeMatch.Index].TrimEnd();
        }

        // Loop: strip leading modifiers in any order
        bool found = true;
        while (found)
        {
            found = false;
            input = input.TrimStart();

            if (mods.Count == null)
            {
                var m = LeadingCount().Match(input);
                if (m.Success) { mods.Count = int.Parse(m.Groups[1].Value); input = input[m.Length..]; found = true; continue; }
            }

            if (mods.Buc == null)
            {
                if (input.StartsWith("blessed ")) { mods.Buc = BUC.Blessed; input = input[8..]; found = true; continue; }
                if (input.StartsWith("uncursed ")) { mods.Buc = BUC.Uncursed; input = input[9..]; found = true; continue; }
                if (input.StartsWith("cursed ")) { mods.Buc = BUC.Cursed; input = input[7..]; found = true; continue; }
            }

            if (mods.Potency == null)
            {
                var m = LeadingPotency().Match(input);
                if (m.Success) { mods.Potency = int.Parse(m.Groups[1].Value); input = input[m.Length..]; found = true; continue; }
            }

            // Recognized but ignored (for now): erodeproof synonyms
            foreach (var word in _ignoredPrefixes)
            {
                if (input.StartsWith(word))
                {
                    input = input[word.Length..];
                    found = true;
                    break;
                }
            }
            if (found) continue;

            // Leading runes: "flaming/2 longsword" or "striking longsword"
            var lrm = LeadingRune().Match(input);
            if (lrm.Success)
            {
                mods.Runes ??= [];
                int? q = lrm.Groups[2].Success ? int.Parse(lrm.Groups[2].Value) : null;
                mods.Runes.Add((lrm.Groups[1].Value, q));
                input = input[lrm.Length..].TrimStart();
                found = true;
            }
        }

        input = input.Trim();
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

    [GeneratedRegex(@"^(\d+)\s")]
    private static partial Regex LeadingCount();

    [GeneratedRegex(@"^([+-]\d+)\s")]
    private static partial Regex LeadingPotency();

    [GeneratedRegex(@"\bc(\d+)$")]
    private static partial Regex TrailingCharges();

    // Rune names are duplicated here and in ApplyMods — keep in sync manually.
    // Bad news bears: rune names are hardcoded in these regexes. We can't source-gen from
    // rune defs because chained source generators aren't a thing. May move to table lookup later.
    [GeneratedRegex(@"^(striking|bonus|accurate|flaming|fire|frost|freezing|cold|shock|shocking|electric)(?:/(\d+))?\s")]
    private static partial Regex LeadingRune();

    [GeneratedRegex(@"\b(striking|bonus|accurate|flaming|fire|frost|freezing|cold|shock|shocking|electric)(?:/(\d+))?$")]
    private static partial Regex TrailingRune();

    static readonly string[] _ignoredPrefixes =
        ["fixed ", "rustproof ", "fireproof ", "corrodeproof ", "erodeproof ", "greased "];
}
