namespace Pathhack.Game;

public static class Grammar
{
    static readonly HashSet<char> Vowels = ['a', 'e', 'i', 'o', 'u'];
    static readonly string[] AnExceptions = []; // leading vowels that sound like constants

    static readonly string[] PluralWords = ["boots", "gloves", "gauntlets"];

    public static string An(this string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (PluralWords.Any(p => s.Contains(p)))
            return "a pair of " + s;
        bool useAn = Vowels.Contains(char.ToLower(s[0]))
            && !AnExceptions.Any(ex => s.StartsWith(ex, StringComparison.OrdinalIgnoreCase));
        return (useAn ? "an " : "a ") + s;
    }

    public static string The(this string s) => "the " + s;

    public static string Capitalize(this string s) => 
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    public static string Plural(this string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        
        // Handle "bottled X" -> "bottles of X"
        if (s.StartsWith("bottled "))
            return "bottles of " + s[8..];
        
        // Handle "X of Y" patterns - pluralize first word
        int ofIdx = s.IndexOf(" of ");
        if (ofIdx < 0)
            ofIdx = s.IndexOf(" labelled ");
        if (ofIdx < 0)
            ofIdx = s.IndexOf(" scribed ");
        if (ofIdx > 0)
            return PluralWord(s[..ofIdx]) + s[ofIdx..];
        
        return PluralWord(s);
    }

    static string PluralWord(string s)
    {
        if (s.EndsWith('s') || s.EndsWith('x') || s.EndsWith("ch") || s.EndsWith("sh"))
            return s + "es";
        if (s.EndsWith('y') && s.Length > 1 && !Vowels.Contains(s[^2]))
            return s[..^1] + "ies";
        return s + "s";
    }

    public static string VTense(IUnit subj, string verb)
    {
        if (subj.IsPlayer) return verb == "is" ? "are" : verb;
        // compound verb: conjugate first word only ("attack with" → "attacks with")
        int sp = verb.IndexOf(' ');
        if (sp >= 0) return VTense(subj, verb[..sp]) + verb[sp..];
        // singular: add s/es
        if (verb.EndsWith('s') || verb.EndsWith('x') || verb.EndsWith("ch") || verb.EndsWith("sh"))
            return verb + "es";
        if (verb.EndsWith('y') && verb.Length > 1 && !Vowels.Contains(verb[^2]))
            return verb[..^1] + "ies";
        return verb + "s";
    }

    public static string DoName(Item item) => item.DisplayName;
    public static string DoNameOne(Item item) => item.SingleName;
    public static bool IsPluralItem(Item item) => PluralWords.Any(p => item.Def.Name.Contains(p));
}
