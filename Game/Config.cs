namespace Pathhack.Game;

// --- Attributes ---

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class ShorthandAttribute(params char[] chars) : Attribute
{
    public char[] Chars => chars;
}

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
    [Shorthand('c')] Combination,
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

// --- Directive entries ---

public record struct AutoPickupException(bool Include, string Pattern);
public record struct MsgTypeRule(MsgTypeAction Action, string Pattern);
public record struct MenuColorRule(string Pattern, string Color);
public record struct StatusColorRule(string Field, string Condition, string Color);
public record struct MonsterColorRule(string Monster, string Color);
public record struct BindRule(string Key, string Command);

// --- Config data ---

public record class ConfigData
{
    // Gameplay
    public bool AutoPickup { get; set; } = false;
    public HashSet<char> PickupTypes { get; set; } = [];
    public bool PickupAll { get; set; } = true;
    public PickupBurden PickupBurden { get; set; } = PickupBurden.Stressed;
    public bool PickupThrown { get; set; } = true;
    public bool AutoDig { get; set; } = false;
    public bool AutoOpen { get; set; } = true;
    public bool AutoQuiver { get; set; } = false;
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
    public bool HilitePet { get; set; } = false;
    public bool HilitePile { get; set; } = false;
    public bool HiliteHiddenStairs { get; set; } = true;
    public bool LitCorridor { get; set; } = false;
    public bool MentionWalls { get; set; } = false;
    public bool Sparkle { get; set; } = true;
    public bool Standout { get; set; } = false;
    public bool UseDarkGray { get; set; } = false;
    public bool UseInverse { get; set; } = false;
    public char Boulder { get; set; } = '`';

    // Interface
    public bool FixInv { get; set; } = true;
    public bool ImplicitUncursed { get; set; } = true;
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
    public List<MenuColorRule> MenuColors { get; set; } = [];
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
            if (line.Length == 0 || line[0] == '#') continue;
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
}
