using System.Text.RegularExpressions;

namespace Pathhack.Game;

// --- Attributes ---

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class ShorthandAttribute(params char[] chars) : Attribute
{
    public char[] Chars => chars;
}

[AttributeUsage(AttributeTargets.Property)]
public class HonouredAttribute : Attribute;

// --- Enums ---

public enum MenuStyle
{
    [Shorthand('n', 't')] Traditional,
    [Shorthand('c')] Combination,
    [Shorthand('p')] Partial,
    [Shorthand('f')] Full,
}

public enum MsgWindow
{
    [Shorthand('s')] Single,
    [Shorthand('c')] Combined,
    [Shorthand('f')] Full,
    [Shorthand('r')] Reversed,
}

public enum CfgRunMode
{
    Teleport,
    Run,
    Walk,
    Crawl,
}

public enum PickupBurden
{
    [Shorthand('u')] Unencumbered,
    [Shorthand('b')] Burdened,
    [Shorthand('s')] Stressed,
    [Shorthand('n')] Strained,
    [Shorthand('o', 't')] Overtaxed,
    [Shorthand('l')] Overloaded,
}

public enum SortLoot
{
    [Shorthand('f')] Full,
    [Shorthand('l')] Loot,
    [Shorthand('n')] None,
}

public enum WhatisCoord
{
    [Shorthand('n')] None,
    [Shorthand('c')] Compass,
    [Shorthand('f')] FullCompass,
    [Shorthand('m')] Map,
    [Shorthand('s')] Screen,
}

public enum NumberPad { Off = 0, On = 1, GPrefix = 2, Phone = 3, PhoneGPrefix = 4, German = -1 }

[Flags]
public enum ParanoidConfirm
{
    None    = 0,
    Confirm = 1 << 0,
    Quit    = 1 << 1,
    Die     = 1 << 2,
    Bones   = 1 << 3,
    Attack  = 1 << 4,
    Pray    = 1 << 5,
    WandBreak = 1 << 6,
    WereChange = 1 << 7,
    Remove  = 1 << 8,
    Swim    = 1 << 9,
    Trap    = 1 << 10,
    All     = ~None,
}

public enum MsgTypeAction { Show, Hide, Stop, NoRep }

public enum RangeIndicator { None, PosOnly, All }

// --- Directive entries ---

public record struct AutoPickupException(bool Include, string Pattern, Regex Regex);
public record struct MsgTypeRule(MsgTypeAction Action, string Pattern);
public record struct MenuColorRule(string Pattern, string Color, CellStyle Style, Regex Regex);
public record struct StatusColorRule(string Field, string Condition, string Color);
public record struct MonsterColorRule(string Monster, string Color);
public record struct BindRule(string Key, string Command);

// --- Config data ---

public record class ConfigData
{
    // Gameplay
    [Honoured] public bool AutoPickup { get; set; } = false;
    public HashSet<char> PickupTypes { get; set; } = [];
    [Honoured] public bool PickupAll { get; set; } = true;
    [Honoured] public PickupBurden PickupBurden { get; set; } = PickupBurden.Stressed;
    [Honoured] public bool PickupThrown { get; set; } = true;
    [Honoured] public bool AutoDig { get; set; } = false;
    public bool AutoOpen { get; set; } = true;
    public bool AutoQuiver { get; set; } = false;
    [Honoured] public bool ZapQuivered { get; set; } = false;
    public bool Confirm { get; set; } = true;
    public bool SafePet { get; set; } = true;
    public bool PushWeapon { get; set; } = false;
    public bool RestOnSpace { get; set; } = false;
    public bool Travel { get; set; } = true;
    public ParanoidConfirm ParanoidConfirmation { get; set; } = ParanoidConfirm.Pray;

    // Display
    public bool Color { get; set; } = true;
    public bool DarkRoom { get; set; } = false;
    public bool EightBitTty { get; set; } = false;
    [Honoured] public RangeIndicator RangeIndicator { get; set; } = RangeIndicator.All;
    public bool HilitePet { get; set; } = false;
    [Honoured] public bool HilitePile { get; set; } = false;
    [Honoured] public bool HiliteHiddenStairs { get; set; } = true;
    public bool LitCorridor { get; set; } = false;
    public bool MentionWalls { get; set; } = false;
    [Honoured] public bool MenuColors { get; set; } = false;
    public bool Sparkle { get; set; } = true;
    [Honoured] public bool Standout { get; set; } = false;
    [Honoured] public bool UseDarkGray { get; set; } = false;
    public bool UseInverse { get; set; } = false;
    public char Boulder { get; set; } = '`';

    // Interface
    public bool FixInv { get; set; } = true;
    [Honoured] public bool ImplicitUncursed { get; set; } = true;
    public bool LootAbc { get; set; } = false;
    public bool SortPack { get; set; } = true;
    public MenuStyle MenuStyle { get; set; } = MenuStyle.Full;
    public MsgWindow MsgWindow { get; set; } = MsgWindow.Single;
    public NumberPad NumberPad { get; set; } = NumberPad.Off;
    public CfgRunMode RunMode { get; set; } = CfgRunMode.Run;
    public SortLoot SortLoot { get; set; } = SortLoot.Full;
    public WhatisCoord WhatisCoord { get; set; } = WhatisCoord.None;
    public string PackOrder { get; set; } = ")[\"%?+!=/(*`0_";
    public string Fruit { get; set; } = "slime mold";

    // Status line
    public bool ShowExp { get; set; } = false;
    public bool ShowRace { get; set; } = false;
    public bool ShowScore { get; set; } = false;
    public bool Time { get; set; } = false;
    public bool HitPointBar { get; set; } = false;
    public bool Verbose { get; set; } = true;

    // Misc
    public bool Blind { get; set; } = false;
    public bool Bones { get; set; } = true;
    public bool Help { get; set; } = true;
    public bool Legacy { get; set; } = true;
    public bool Mail { get; set; } = true;
    public bool Nudist { get; set; } = false;
    public bool Silent { get; set; } = true;
    public bool Tombstone { get; set; } = true;

    // Directives
    public List<AutoPickupException> AutoPickupExceptions { get; set; } = [];
    public List<MsgTypeRule> MsgTypes { get; set; } = [];
    public List<MenuColorRule> MenuColorRules { get; set; } = [];
    public List<StatusColorRule> StatusColors { get; set; } = [];
    public List<MonsterColorRule> MonsterColors { get; set; } = [];
    public List<BindRule> Binds { get; set; } = [];
}

public static partial class Config
{
    public static ConfigData Data { get; private set; } = new();

    static readonly string _logPath = "config.log";

    public static void Load()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pathhackrc");
        if (!File.Exists(path)) return;

        List<string> warnings = [];

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            // allow #PH_ to be parsed so we can dump junk in a shared dnh/pathhack rc
            if (line.StartsWith("#PH_"))
                line = line[4..].Trim();

            // Skip empty or comment lines (after the special #PH_ stripping)
            if (line.Length == 0 || line[0] == '#') continue;

            if (line.StartsWith("AUTOPICKUP_EXCEPTION=", StringComparison.OrdinalIgnoreCase))
            {
                ParseAutoPickupException(line[21..], warnings);
                continue;
            }
            if (line.StartsWith("MENUCOLOR=", StringComparison.OrdinalIgnoreCase))
            {
                ParseMenuColor(line[10..], warnings);
                continue;
            }

            if (!line.StartsWith("OPTIONS=", StringComparison.OrdinalIgnoreCase)) continue;
            line = line[8..];

            foreach (var part in line.Split(','))
            {
                var opt = part.Trim();
                if (opt.Length == 0) continue;

                int colon = opt.IndexOf(':');
                if (colon >= 0)
                {
                    var key = opt[..colon].Trim();
                    var val = opt[(colon + 1)..].Trim();
                    if (!TrySetCompound(Data, key, val))
                        warnings.Add($"unknown compound option: {key}");
                }
                else
                {
                    bool enable = true;
                    if (opt[0] == '!')
                    {
                        enable = false;
                        opt = opt[1..];
                    }
                    else if (opt.StartsWith("no", StringComparison.OrdinalIgnoreCase)
                             && IsBoolOption(opt[2..]))
                    {
                        enable = false;
                        opt = opt[2..];
                    }

                    if (!TrySetBool(Data, opt, enable))
                    {
                        if (!enable)
                            warnings.Add($"unknown bool option: {opt}");
                        else
                            warnings.Add($"unknown option: {opt}");
                    }
                }
            }
        }

        if (warnings.Count > 0)
            File.WriteAllLines(_logPath, warnings);
    }

    // Generated: Normalize, TrySetBool, TrySetCompound, IsBoolOption
    // Special cases handled here, called from generated TrySetCompound:
    internal static bool TrySetCompoundSpecial(ConfigData data, string key, string val)
    {
        switch (key)
        {
            case "pickuptypes":
                data.PickupTypes.Clear();
                data.PickupAll = val.Equals("all", StringComparison.OrdinalIgnoreCase);
                if (!data.PickupAll)
                    foreach (var c in val) data.PickupTypes.Add(c);
                return true;
            case "paranoidconfirmation":
                data.ParanoidConfirmation = ParseParanoidConfirm(val);
                return true;
        }
        return false;
    }

    static ParanoidConfirm ParseParanoidConfirm(string val)
    {
        if (val.Equals("none", StringComparison.OrdinalIgnoreCase))
            return ParanoidConfirm.None;
        if (val.Equals("all", StringComparison.OrdinalIgnoreCase))
            return ParanoidConfirm.All;

        ParanoidConfirm result = ParanoidConfirm.None;
        foreach (var token in val.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<ParanoidConfirm>(token.Replace("-", ""), ignoreCase: true, out var flag))
                result |= flag;
        }
        return result;
    }

    // AUTOPICKUP_EXCEPTION="<pattern" or ">pattern"
    static void ParseAutoPickupException(string val, List<string> warnings)
    {
        val = val.Trim('"');
        if (val.Length < 2 || (val[0] != '<' && val[0] != '>'))
        {
            warnings.Add($"bad AUTOPICKUP_EXCEPTION: {val}");
            return;
        }
        bool include = val[0] == '<';
        string pattern = val[1..];
        try
        {
            Regex rx = new(GlobToRegex(pattern), RegexOptions.IgnoreCase | RegexOptions.Compiled);
            Data.AutoPickupExceptions.Add(new(include, pattern, rx));
        }
        catch (RegexParseException)
        {
            warnings.Add($"bad pattern in AUTOPICKUP_EXCEPTION: {pattern}");
        }
    }

    // MENUCOLOR="pattern"=color[&style[&style]]
    static void ParseMenuColor(string val, List<string> warnings)
    {
        // Format: "pattern"=color or "pattern"=color&bold&inverse
        if (val.Length < 4 || val[0] != '"')
        {
            warnings.Add($"bad MENUCOLOR: {val}");
            return;
        }
        int closeQuote = val.IndexOf('"', 1);
        if (closeQuote < 0 || closeQuote + 1 >= val.Length || val[closeQuote + 1] != '=')
        {
            warnings.Add($"bad MENUCOLOR: {val}");
            return;
        }
        string pattern = val[1..closeQuote];
        string colorSpec = val[(closeQuote + 2)..];

        var parts = colorSpec.Split('&');
        string colorName = parts[0].Trim();
        CellStyle style = CellStyle.None;
        for (int i = 1; i < parts.Length; i++)
        {
            switch (parts[i].Trim().ToLowerInvariant())
            {
                case "bold": style |= CellStyle.Bold; break;
                case "inverse": style |= CellStyle.Reverse; break;
                case "underline": style |= CellStyle.Underline; break;
                case "hilite": style |= CellStyle.Reverse; break;
                // blink — we don't support it, ignore
            }
        }

        try
        {
            Regex rx = new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            Data.MenuColorRules.Add(new(pattern, colorName, style, rx));
        }
        catch (RegexParseException)
        {
            warnings.Add($"bad regex in MENUCOLOR: {pattern}");
        }
    }

    /// <summary>Last matching MENUCOLOR rule wins. Returns null if no match or menucolors disabled.</summary>
    public static (ConsoleColor Color, CellStyle Style)? ResolveMenuColor(string text)
    {
        if (!Data.MenuColors || Data.MenuColorRules.Count == 0) return null;

        (ConsoleColor Color, CellStyle Style)? result = null;
        foreach (var rule in Data.MenuColorRules)
        {
            if (rule.Regex.IsMatch(text) && TryParseColor(rule.Color, out var color))
                result = (color, rule.Style);
        }
        return result;
    }

    static bool TryParseColor(string name, out ConsoleColor color)
    {
        var n = name.ToLowerInvariant().Replace(" ", "");
        (color, bool ok) = n switch
        {
            "black" => (ConsoleColor.Black, true),
            "red" => (ConsoleColor.DarkRed, true),
            "green" => (ConsoleColor.DarkGreen, true),
            "brown" or "orange" => (ConsoleColor.DarkYellow, true),
            "blue" => (ConsoleColor.DarkBlue, true),
            "magenta" => (ConsoleColor.DarkMagenta, true),
            "cyan" => (ConsoleColor.DarkCyan, true),
            "gray" or "grey" => (ConsoleColor.Gray, true),
            "darkgray" or "darkgrey" => (ConsoleColor.DarkGray, true),
            "darkgreen" => (ConsoleColor.DarkGreen, true),
            "lightred" or "brightred" => (ConsoleColor.Red, true),
            "lightgreen" or "brightgreen" => (ConsoleColor.Green, true),
            "yellow" => (ConsoleColor.Yellow, true),
            "lightblue" or "brightblue" => (ConsoleColor.Blue, true),
            "lightmagenta" or "brightmagenta" => (ConsoleColor.Magenta, true),
            "lightcyan" or "brightcyan" => (ConsoleColor.Cyan, true),
            "white" => (ConsoleColor.White, true),
            _ => (default, false),
        };
        return ok;
    }

    static string GlobToRegex(string glob)
    {
        var sb = new System.Text.StringBuilder("^");
        foreach (char c in glob)
        {
            sb.Append(c switch
            {
                '*' => ".*",
                '?' => ".",
                _ when ".+^${}()|[]\\".Contains(c) => $"\\{c}",
                _ => c.ToString(),
            });
        }
        return sb.Append('$').ToString();
    }
}
