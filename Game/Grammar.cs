namespace Pathhack.Game;

public static class Grammar
{
    static readonly HashSet<char> Vowels = ['a', 'e', 'i', 'o', 'u'];
    static readonly string[] AnExceptions = []; // leading vowels that sound like constants

    public static string An(this string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
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
        if (s.EndsWith('s') || s.EndsWith('x') || s.EndsWith("ch") || s.EndsWith("sh"))
            return s + "es";
        if (s.EndsWith('y') && s.Length > 1 && !Vowels.Contains(s[^2]))
            return s[..^1] + "ies";
        return s + "s";
    }

    public static string VTense(IUnit subj, string verb)
    {
        if (subj.IsPlayer) return verb == "is" ? "are" : verb;
        // singular: add s/es
        if (verb.EndsWith('s') || verb.EndsWith('x') || verb.EndsWith("ch") || verb.EndsWith("sh"))
            return verb + "es";
        if (verb.EndsWith('y') && verb.Length > 1 && !Vowels.Contains(verb[^2]))
            return verb[..^1] + "ies";
        return verb + "s";
    }

    public static string DoName(Item item) => item.DisplayName.An();
}
