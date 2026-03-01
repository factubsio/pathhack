using System.Reflection;

namespace Pathhack.UI;

enum OptionKind { Bool, Enum, FlagsEnum, String, Char, DirectiveList }

class OptionEntry(PropertyInfo prop, string group, OptionKind kind) : ISelectable
{
    public PropertyInfo Prop => prop;
    public string Group => group;
    public OptionKind Kind => kind;
    public bool IsHonoured => prop.GetCustomAttribute<HonouredAttribute>() != null;

    public string Name
    {
        get
        {
            string val = FormatValue();
            return $"{prop.Name,-22} {val}";
        }
    }

    public string? Subtitle => null;
    public string Description
    {
        get
        {
            return Kind switch
            {
                OptionKind.Bool => "Toggle with [Space], [Enter], [H], or [L].",
                OptionKind.Enum => "Cycle with [H]/[L] or [Space]. [Enter] also cycles forward.",
                OptionKind.FlagsEnum => "Press [Enter] to edit flags.",
                OptionKind.String or OptionKind.Char => "Press [Enter] to edit.",
                OptionKind.DirectiveList => "Read-only. Edit .pathhackrc to change.",
                _ => "",
            };
        }
    }

    public IEnumerable<string> Details => [];
    public string? WhyNot => Kind == OptionKind.DirectiveList ? "read-only" : null;
    public string[] Tags => IsHonoured ? [Group] : [Group, "not yet used"];
    public ConsoleColor? ListColor => IsHonoured ? null : ConsoleColor.DarkGray;

    public string FormatValue()
    {
        object? val = prop.GetValue(Config.Data);
        return Kind switch
        {
            OptionKind.Bool => (bool)val! ? "on" : "off",
            OptionKind.FlagsEnum => val!.ToString()!,
            OptionKind.Enum => val!.ToString()!,
            OptionKind.Char => $"\u2018{val}\u2019",
            OptionKind.String => $"\u201c{val}\u201d",
            OptionKind.DirectiveList => CountDirective(val),
            _ => val?.ToString() ?? "",
        };
    }

    public ConsoleColor ValueColor()
    {
        if (Kind == OptionKind.Bool) return (bool)prop.GetValue(Config.Data)! ? ConsoleColor.Green : ConsoleColor.Red;
        if (Kind == OptionKind.DirectiveList) return ConsoleColor.DarkGray;
        return ConsoleColor.White;
    }

    static string CountDirective(object? val) => val switch
    {
        System.Collections.ICollection c => $"({c.Count} entries)",
        _ => "",
    };

    public void Toggle()
    {
        if (Kind != OptionKind.Bool) return;
        prop.SetValue(Config.Data, !(bool)prop.GetValue(Config.Data)!);
    }

    public void CycleEnum(int dir)
    {
        if (Kind != OptionKind.Enum) return;
        var values = Enum.GetValues(prop.PropertyType);
        int cur = Array.IndexOf(values, prop.GetValue(Config.Data));
        int next = (cur + dir + values.Length) % values.Length;
        prop.SetValue(Config.Data, values.GetValue(next));
    }
}

static class OptionsMenu
{
    static readonly (string Group, string[] Props)[] Groups =
    [
        ("Gameplay", ["AutoPickup", "PickupAll", "PickupBurden", "PickupThrown", "AutoDig", "AutoOpen",
            "AutoQuiver", "ZapQuivered", "Confirm", "SafePet", "PushWeapon", "RestOnSpace", "Travel",
            "ParanoidConfirmation"]),
        ("Display", ["Color", "DarkRoom", "EightBitTty", "RangeIndicator", "HilitePet", "HilitePile",
            "HiliteHiddenStairs", "LitCorridor", "MentionWalls", "MenuColors", "Sparkle", "Standout",
            "UseDarkGray", "UseInverse", "Boulder"]),
        ("Interface", ["FixInv", "ImplicitUncursed", "LootAbc", "SortPack", "MenuStyle", "MsgWindow",
            "NumberPad", "RunMode", "SortLoot", "WhatisCoord", "PackOrder", "Fruit"]),
        ("Status", ["ShowExp", "ShowRace", "ShowScore", "Time", "HitPointBar", "Verbose"]),
        ("Misc", ["Blind", "Bones", "Help", "Legacy", "Mail", "Nudist", "Silent", "Tombstone"]),
        ("Directives", ["ApGrabs", "ApLeaves", "MsgTypes", "MenuColorRules", "StatusColors",
            "MonsterColors", "Binds"]),
    ];

    static OptionKind Classify(PropertyInfo prop)
    {
        Type t = prop.PropertyType;
        if (t == typeof(bool)) return OptionKind.Bool;
        if (t == typeof(string)) return OptionKind.String;
        if (t == typeof(char)) return OptionKind.Char;
        if (t.IsEnum && t.GetCustomAttribute<FlagsAttribute>() != null) return OptionKind.FlagsEnum;
        if (t.IsEnum) return OptionKind.Enum;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) return OptionKind.DirectiveList;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>)) return OptionKind.DirectiveList;
        return OptionKind.String;
    }

    static List<OptionEntry> BuildEntries()
    {
        List<OptionEntry> entries = [];
        Type configType = typeof(ConfigData);
        foreach (var (group, propNames) in Groups)
        {
            foreach (string name in propNames)
            {
                PropertyInfo? prop = configType.GetProperty(name);
                if (prop == null) continue;
                entries.Add(new OptionEntry(prop, group, Classify(prop)));
            }
        }
        return entries;
    }

    public static void Show()
    {
        List<OptionEntry> entries = BuildEntries();

        ListPicker.Pick(entries, "Options", listWidth: 40, custom: (w, entry, own) =>
        {
            RenderRHS(w, entry);
            if (!own) return false;
            Draw.Blit();

            if (entry.Kind == OptionKind.FlagsEnum)
            {
                EditFlags(w, entry);
                return false;
            }
            if (entry.Kind is OptionKind.String or OptionKind.Char)
            {
                EditText(w, entry);
                return false;
            }
            // For bool/enum, Enter acts as toggle/cycle forward
            if (entry.Kind == OptionKind.Bool) entry.Toggle();
            else if (entry.Kind == OptionKind.Enum) entry.CycleEnum(1);
            return false;
        },
        keyHandler: (entry, key) =>
        {
            if (entry.Kind == OptionKind.Bool && key.KeyChar is ' ' or 'h' or 'l')
            {
                entry.Toggle();
                return true;
            }
            if (entry.Kind == OptionKind.Enum)
            {
                if (key.KeyChar is ' ' or 'l') { entry.CycleEnum(1); return true; }
                if (key.KeyChar == 'h') { entry.CycleEnum(-1); return true; }
            }
            return false;
        });
    }

    static void RenderRHS(WindowWriter w, OptionEntry entry)
    {
        w.SetCursor(0, 0);
        w.Write(entry.Prop.Name, ConsoleColor.Yellow);
        w.SetCursor(0, 2);
        w.Write(entry.FormatValue(), entry.ValueColor());

        int y = 4;
        w.SetCursor(0, y);
        w.Write(entry.Description, ConsoleColor.DarkGray);

        if (entry.Kind == OptionKind.DirectiveList)
        {
            RenderDirectiveList(w, entry);
        }
        else if (entry.Kind == OptionKind.Enum)
        {
            y += 2;
            w.SetCursor(0, y++);
            w.Write("Values:", ConsoleColor.DarkGray);
            object? cur = entry.Prop.GetValue(Config.Data);
            foreach (var val2 in Enum.GetValues(entry.Prop.PropertyType))
            {
                bool isCur = val2.Equals(cur);
                w.SetCursor(2, y++);
                w.Write(val2.ToString()!, isCur ? ConsoleColor.White : ConsoleColor.DarkGray,
                    style: isCur ? CellStyle.Reverse : CellStyle.None);
            }
        }
    }

    static void RenderDirectiveList(WindowWriter w, OptionEntry entry)
    {
        object? val = entry.Prop.GetValue(Config.Data);
        if (val is not System.Collections.IEnumerable enumerable) return;

        int y = 6;
        w.SetCursor(0, y++);
        w.Write("Entries:", ConsoleColor.DarkGray);

        foreach (object? item in enumerable)
        {
            if (y >= w.Height - 1) { w.SetCursor(0, y); w.Write("...", ConsoleColor.DarkGray); break; }
            w.SetCursor(2, y++);
            w.Write(item?.ToString() ?? "", ConsoleColor.Gray);
        }
    }

    static void EditFlags(WindowWriter w, OptionEntry entry)
    {
        var values = Enum.GetValues(entry.Prop.PropertyType);
        // Skip None/All-like entries (value 0 and ~0)
        List<(object Value, string Name)> flags = [];
        foreach (object v in values)
        {
            long lv = Convert.ToInt64(v);
            if (lv == 0 || lv == ~0L) continue;
            if ((lv & (lv - 1)) != 0) continue; // skip compound flags
            flags.Add((v, v.ToString()!));
        }

        int cursor = 0;
        while (true)
        {
            w.SetCursor(0, 0);
            w.Clear();
            w.SetCursor(0, 0);
            w.Write(entry.Prop.Name, ConsoleColor.Yellow);
            w.SetCursor(0, 1);
            w.Write("[Space] toggle  [Esc] done", ConsoleColor.DarkGray);

            long current = Convert.ToInt64(entry.Prop.GetValue(Config.Data));
            for (int i = 0; i < flags.Count; i++)
            {
                long fv = Convert.ToInt64(flags[i].Value);
                bool on = (current & fv) != 0;
                w.SetCursor(0, 3 + i);
                string prefix = on ? "[+] " : "[ ] ";
                ConsoleColor fg = on ? ConsoleColor.Green : ConsoleColor.Gray;
                CellStyle style = i == cursor ? CellStyle.Reverse : CellStyle.None;
                w.Write(prefix + flags[i].Name, fg, style: style);
            }
            Draw.Blit();

            var key = Input.NextKey();
            switch (key.Key)
            {
                case ConsoleKey.Escape: return;
                case ConsoleKey.UpArrow or ConsoleKey.K:
                    cursor = (cursor - 1 + flags.Count) % flags.Count;
                    break;
                case ConsoleKey.DownArrow or ConsoleKey.J:
                    cursor = (cursor + 1) % flags.Count;
                    break;
                case ConsoleKey.Spacebar or ConsoleKey.Enter:
                    long fv = Convert.ToInt64(flags[cursor].Value);
                    long toggled = current ^ fv;
                    entry.Prop.SetValue(Config.Data, Enum.ToObject(entry.Prop.PropertyType, toggled));
                    break;
            }
        }
    }

    static void EditText(WindowWriter w, OptionEntry entry)
    {
        object? cur = entry.Prop.GetValue(Config.Data);
        string text = entry.Kind == OptionKind.Char ? cur?.ToString() ?? "" : (string)(cur ?? "");

        while (true)
        {
            w.SetCursor(0, 0);
            w.Clear();
            w.SetCursor(0, 0);
            w.Write(entry.Prop.Name, ConsoleColor.Yellow);
            w.SetCursor(0, 2);
            w.Write(text + "▌", ConsoleColor.White);
            w.SetCursor(0, 4);
            w.Write("[Enter] confirm  [Esc] cancel", ConsoleColor.DarkGray);
            Draw.Blit();

            var key = Input.NextKey();
            if (key.Key == ConsoleKey.Escape) return;
            if (key.Key == ConsoleKey.Enter)
            {
                if (entry.Kind == OptionKind.Char)
                    entry.Prop.SetValue(Config.Data, text.Length > 0 ? text[0] : ' ');
                else
                    entry.Prop.SetValue(Config.Data, text);
                return;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (text.Length > 0) text = text[..^1];
            }
            else if (key.KeyChar >= ' ' && key.KeyChar <= '~')
            {
                if (entry.Kind == OptionKind.Char)
                    text = key.KeyChar.ToString();
                else
                    text += key.KeyChar;
            }
        }
    }
}
