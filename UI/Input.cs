namespace Pathhack.UI;

public enum ArgKind { None, Dir, Int, String }

public readonly record struct ArgType(ArgKind Kind, string? Prompt = null)
{
    public static readonly ArgType None = new(ArgKind.None);
    public static readonly ArgType Dir = new(ArgKind.Dir);
    public static ArgType Int(string prompt) => new(ArgKind.Int, prompt);
    public static ArgType String(string prompt) => new(ArgKind.String, prompt);
}

public record CommandArg;
public record NoArg : CommandArg;
public record DirArg(Pos Dir) : CommandArg;
public record IntArg(int Value) : CommandArg;
public record StringArg(string Value) : CommandArg;

public record Command(string Name, string Desc, ArgType Arg, Action<CommandArg> Action, bool Hidden = false);

public record SpecialCommand(ConsoleKey Key, ConsoleModifiers Mods, string Desc, Action Action)
{
    public bool Matches(ConsoleKeyInfo k) => k.Key == Key && k.Modifiers == Mods;
    public string KeyName => Mods.HasFlag(ConsoleModifiers.Control) ? $"Ctrl+{Key}" : Key.ToString();
}

public static partial class Input
{
    public static Queue<ConsoleKey>? InjectedKeys;

    public static ConsoleKeyInfo NextKey()
    {
        if (InjectedKeys != null && InjectedKeys.TryDequeue(out var k))
            return new ConsoleKeyInfo('\0', k, false, false, false);
        return Console.ReadKey(true);
    }
    static readonly Dictionary<string, Command> _extCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["quit"] = new("quit", "Quit game", ArgType.None, _ => g.Done("Quit")),
        ["levelup"] = new("levelup", "Level up (if available)", ArgType.None, _ => DoLevelUp()),
        ["invoke"] = new("invoke", "Invoke item power", ArgType.None, _ => InvokeItem()),
        ["chat"] = new("chat", "Talk to adjacent creature", ArgType.Dir, Chat),
        ["help"] = new("help", "Show help", ArgType.None, _ => ShowHelp()),
        ["name"] = new("name", "Name an item type", ArgType.None, _ => CallItem()),
        ["dismiss"] = new("dismiss", "Dismiss a maintained buff", ArgType.None, _ => DismissAction.DoDismiss(u)),
    };

    static readonly Dictionary<char, Command> _commands = new()
    {
        ['o'] = new("open", "Open door", ArgType.Dir, OpenDoor),
        ['i'] = new("inventory", "Show inventory", ArgType.None, _ => ShowInventory()),
        ['d'] = new("drop", "Drop item", ArgType.None, _ => DropItem()),
        ['D'] = new("multidrop", "Drop multiple items", ArgType.None, _ => DropItems()),
        [','] = new("pickup", "Pick up item", ArgType.None, _ => PickupItem()),
        ['.'] = new("wait", "Wait one turn", ArgType.None, _ => WaitTurn()),
        ['s'] = new("wait", "Wait one turn", ArgType.None, _ => WaitTurn()),
        ['w'] = new("wield", "Wield weapon", ArgType.None, _ => WieldWeapon()),
        ['W'] = new("wear", "Wear armor", ArgType.None, _ => WearArmor()),
        ['P'] = new("puton", "Put on accessory", ArgType.None, _ => PutOnAccessory()),
        ['T'] = new("takeoff", "Take off armor", ArgType.None, _ => TakeOff()),
        ['R'] = new("remove", "Remove accessory", ArgType.None, _ => RemoveAccessory()),
        [';'] = new("farlook", "Examine (farlook)", ArgType.None, _ => Pokedex.Farlook()),
        ['f'] = new("fire", "Fire quivered item", ArgType.None, _ => Fire()),
        ['Q'] = new("quiver", "Set quiver", ArgType.None, _ => SetQuiver()),
        ['Z'] = new("cast", "Cast spell", ArgType.None, _ => ZapSpell()),
        ['q'] = new("quaff", "Drink potion", ArgType.None, _ => Quaff()),
        ['r'] = new("read", "Read scroll", ArgType.None, _ => ReadScroll()),
        ['t'] = new("throw", "Throw item", ArgType.None, _ => Throw()),
        ['z'] = new("zap", "Zap wand", ArgType.None, _ => ZapWand()),
        ['e'] = new("eat", "Eat food", ArgType.None, _ => Eat()),
        ['a'] = new("apply", "Apply item", ArgType.None, _ => Apply()),
        ['\\'] = new("discoveries", "Show discoveries", ArgType.None, _ => ShowDiscoveries()),
        ['C'] = new("call", "Name an item type", ArgType.None, _ => CallItem()),
        ['?'] = new("help", "Show help", ArgType.None, _ => ShowHelp()),
        ['p'] = new("pay", "Pay shopkeeper", ArgType.None, _ => PayShopkeeper()),
    };

    static readonly List<SpecialCommand> _specialCommands = [
        new(ConsoleKey.F, ConsoleModifiers.Control, "Use ability", ShowAbilities),
        new(ConsoleKey.O, ConsoleModifiers.Control, "Branch overview", DungeonOverview.Show),
        new(ConsoleKey.X, ConsoleModifiers.Control, "Character info", ShowCharacterInfo),
        new(ConsoleKey.P, ConsoleModifiers.Control, "Message history", ShowMessageHistory),
    ];

    static void DebugExp()
    {
        int needed = Progression.XpForLevel(u.CharacterLevel + 1) - u.XP;
        if (needed > 0) g.GainExp(needed);
        g.pline($"XP set to {u.XP}. Level up available.");
    }

    public static void InitDebugCommands()
    {
        _extCommands["wish"] = new("wish", "Wish for an item", ArgType.String("For what do you wish?"), DoWish, Hidden: true);
        _extCommands["exp"] = new("exp", "", ArgType.None, _ => DebugExp(), Hidden: true);
        _extCommands["dumplog"] = new("dumplog", "Dump screen to JSON", ArgType.None, _ => Dump.DumpLog(), Hidden: true);
        _extCommands["spawn"] = new("spawn", "Spawn a monster", ArgType.String("What monster?"), DoSpawn, Hidden: true);
        _extCommands["place"] = new("place", "Place a trap", ArgType.String("What trap?"), DoPlace, Hidden: true);
        _extCommands["brickstats"] = new("brickstats", "Dump brick hook stats", ArgType.None, _ => BrickStatsHook.Instance.Dump(), Hidden: true);
        _specialCommands.Add(new(ConsoleKey.T, ConsoleModifiers.Control, "Teleport (debug)", DebugTeleport));
        LogicBrick.GlobalHook = BrickStatsHook.Instance;
    }

    static void DebugTeleport()
    {
        g.pline("Teleport to?");
        var pos = PickPosition();
        if (pos == null || pos == upos) return;
        lvl.MoveUnit(u, pos.Value, free: true);
    }

    static void DoWish(CommandArg arg)
    {
        if (arg is not StringArg s || string.IsNullOrWhiteSpace(s.Value)) return;
        var item = Pathhack.Wish.WishParser.Parse(s.Value);
        if (item == null) { g.pline("Nothing happens."); return; }
        u.Inventory.Add(item);
        g.pline($"{item.InvLet} - {item}.");
    }

    static void DoSpawn(CommandArg arg)
    {
        if (arg is not StringArg s || string.IsNullOrWhiteSpace(s.Value)) return;
        var def = Pathhack.Wish.WishParser.ParseMonster(s.Value);
        if (def == null) { g.pline("No such monster."); return; }
        g.pline("Spawn where?");
        var pos = PickPosition();
        if (pos == null) return;
        if (!MonsterSpawner.SpawnAndPlace(lvl, "debug", def, false, pos: pos))
        { g.pline("Couldn't place monster."); return; }
        g.pline($"A {def.Name} appears!");
    }

    static void DoPlace(CommandArg arg)
    {
        if (arg is not StringArg s || string.IsNullOrWhiteSpace(s.Value)) return;
        int depth = lvl.Id.Depth;
        Trap? trap = s.Value.Trim().ToLowerInvariant() switch
        {
            "pit" => new PitTrap(depth),
            "web" => new WebTrap(depth),
            "hole" => new HoleTrap(TrapType.Hole, depth),
            "trapdoor" => new HoleTrap(TrapType.Trapdoor, depth),
            "ambush" => new AmbushTrap(depth),
            _ => null
        };
        if (trap == null) { g.pline("Unknown trap. Try: pit, web, hole, trapdoor, ambush."); return; }
        g.pline("Place where?");
        var pos = PickPosition();
        if (pos == null) return;
        lvl.Traps[pos.Value] = trap;
        trap.PlayerSeen = true;
        g.pline("Trap placed.");
        Draw.DrawCurrent();
    }

    static void ResolveTargetAndExecute(ActionBrick ability)
    {
        var data = u.ActionData.GetValueOrDefault(ability);
        
        var plan = ability.CanExecute(u, data, Target.None);
        if (!plan)
        {
            g.pline($"That ability is not ready ({plan.WhyNot}).");
            return;
        }

        Target target = Target.None;
        if (ability.Targeting == TargetingType.Direction)
        {
            Draw.DrawCurrent();
            if (!PromptDirection(out var dir)) return;
            target = new Target(null, dir);
        }
        else if (ability.Targeting == TargetingType.Unit)
        {
            g.pline("Target what?");
            Draw.DrawCurrent();
            List<IUnit> candidates = [..lvl.LiveUnits
                .OfType<Monster>()
                .Where(m => m.Pos.ChebyshevDist(upos) < ability.EffectiveMaxRange && m.Perception >= PlayerPerception.Detected)
                .OrderBy(m => m.Pos.ChebyshevDist(upos))
            ];
            Menu<IUnit> m = new();
            char let = 'a';
            foreach (var candidate in candidates.Take(6))
                m.Add(let++, $"{candidate:An} [{candidate.Pos.RelativeTo(upos)}]", candidate);
            
            m.Add('x', "Pick manually", u);

            var tgt_ = m.Display(MenuMode.PickOne);
            if (tgt_.Count == 0) return;
            var tgt = tgt_[0];

            if (tgt.IsPlayer)
            {
                g.pline("Pick a target.");
                var pos = PickPosition();
                if (pos == null) return;
                if (pos.Value.ChebyshevDist(upos) > ability.EffectiveMaxRange)
                {
                    g.pline("Too far.");
                    return;
                }
                var unit = lvl.UnitAt(pos.Value) as Monster;
                if (unit == null || unit.Perception < PlayerPerception.Detected)
                {
                    g.pline("You can't target that.");
                    return;
                }
                tgt = unit;
            }

            Log.Write($"target unit {tgt} at {tgt.Pos}");

            target = Target.From(tgt);
            var targetPlan = ability.CanExecute(u, data, target);
            if (!targetPlan)
            {
                g.pline($"Cannot target there ({targetPlan.WhyNot}).");
                return;
            }
        }
        else if (ability.Targeting == TargetingType.Pos)
        {
            g.pline("Target where?");
            Draw.DrawCurrent();
            var pos = PickPosition();
            if (pos == null) return;
            if (pos.Value.ChebyshevDist(upos) > ability.EffectiveMaxRange)
            {
                g.pline("Too far.");
                return;
            }
            target = new Target(null, pos.Value);
        }
        
        ability.Execute(u, data, target, plan.Plan);
        u.Energy -= ability.GetCost(u, data, target).Value;
    }


    static void ShowCharacterInfo()
    {
        var menu = new Menu<string>();
        menu.Add("Base Attributes", LineStyle.Heading);
        menu.Add("");
        menu.Add("Starting", LineStyle.SubHeading);
        menu.Add($"  Class          : {u.Class?.Name ?? "none"}");
        menu.Add($"  Deity          : {u.Deity?.Name ?? "none"}");
        menu.Add($"  Ancestry       : {u.Ancestry?.Name ?? "none"}");
        menu.Add("");
        menu.Add("Current", LineStyle.SubHeading);
        string levelDisplay = Progression.HasPendingLevelUp(u)
            ? $"{u.CharacterLevel} (LEVEL UP AVAILABLE)"
            : $"{u.CharacterLevel}";
        int nextLevelXp = Progression.XpForLevel(u.CharacterLevel + 1);
        string xpDisplay = u.CharacterLevel >= Progression.MaxLevel
            ? $"{u.XP} (max level)"
            : $"{u.XP} / {nextLevelXp}";
        menu.Add($"  XP             : {xpDisplay}");
        menu.Add($"  Level          : {levelDisplay}");
        menu.Add($"  HP             : {u.HP.Current}/{u.HP.Max}");
        menu.Add($"  AC             : {u.GetAC()}");
        var hungerLabel = Hunger.GetLabel(Hunger.GetState(u.Nutrition));
        menu.Add($"  Nutrition      : {u.Nutrition}/{Hunger.Max}{(hungerLabel != "" ? $" ({hungerLabel})" : "")}");
        menu.Add("");
        menu.Add("Attributes", LineStyle.SubHeading);
        menu.Add($"  STR {u.Str,2}  INT {u.Int,2}  DEX {u.Dex,2}  WIS {u.Wis,2}  CON {u.Con,2}  CHA {u.Cha,2}");
        menu.Add('a', "Show current effects.", "effects");
        if (Progression.HasPendingLevelUp(u))
            menu.Add('l', "Level up!", "levelup");
        
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        if (picked[0] == "effects")
            ShowEffects();
        else if (picked[0] == "levelup")
            DoLevelUp();
    }

    static void ShowEffects()
    {
        var buffs = u.ActiveBuffNames.ToList();
        // Also check inventory items for buffs
        foreach (var item in u.Inventory)
            buffs.AddRange(item.ActiveBuffNames);
        
        var menu = new Menu();
        menu.Add("Current Effects", LineStyle.Heading);
        menu.Add("");
        if (buffs.Count == 0)
        {
            menu.Add("You have no active effects.");
        }
        else
        {
            foreach (var name in buffs)
                menu.Add($"  {name}");
        }
        menu.Display();
    }

    static void ShowHelp()
    {
        var menu = new Menu();
        menu.Add("Pathhack Commands", LineStyle.Heading);
        menu.Add("");
        menu.Add("Movement", LineStyle.SubHeading);
        menu.Add("  hjkl/yubn      Move (vi keys)");
        menu.Add("  arrows         Move (arrow keys)");
        menu.Add("  numpad         Move (numpad)");
        menu.Add("  Shift+dir      Run until blocked");
        menu.Add("  Ctrl+dir       Run until interesting");
        menu.Add("  _              Travel to location");
        menu.Add("  < >            Use stairs");
        menu.Add("");
        menu.Add("Commands", LineStyle.SubHeading);
        foreach (var (key, cmd) in _commands.OrderBy(x => x.Key))
            menu.Add($"  {key,-14} {cmd.Desc}");
        menu.Add("");
        menu.Add("Special Keys", LineStyle.SubHeading);
        foreach (var cmd in _specialCommands)
            menu.Add($"  {cmd.KeyName,-14} {cmd.Desc}");
        menu.Add("");
        menu.Add("Extended Commands (#)", LineStyle.SubHeading);
        foreach (var (name, cmd) in _extCommands.Where(x => !x.Value.Hidden).OrderBy(x => x.Key))
            menu.Add($"  #{name,-13} {cmd.Desc}");
        menu.Display();
    }

    static int _msgHistoryIdx = -1;

    static void ShowMessageHistory()
    {
        var history = g.MessageHistory;
        if (history.Count == 0) return;
        
        _msgHistoryIdx++;
        
        if (_msgHistoryIdx < 2 && _msgHistoryIdx < history.Count)
        {
            Draw.RenderTopLine(history[history.Count - 1 - _msgHistoryIdx]);
        }
        else
        {
            // Third press or exhausted: show full list
            _msgHistoryIdx = -1;
            var menu = new Menu { InitialPage = -1 };
            menu.Add("Message History", LineStyle.Heading);
            for (int i = 0; i < history.Count; i++)
                menu.Add(history[i]);
            menu.Display();
        }
    }

    static void ResetMessageHistory() => _msgHistoryIdx = -1;

    static void DoTravel()
    {
        g.pline("Where do you want to travel to?");
        var cursor = PickPosition();
        if (cursor == null || cursor == upos) return;
        
        var path = Pathfinding.FindPath(lvl, upos, cursor.Value);
        if (path == null || path.Count == 0)
        {
            g.pline("You can't find a path there.");
            return;
        }
        Log.Verbose("movement", $"Travel: from={upos} to={cursor} path=[{string.Join(",", PathToPositions(upos, path))}]");
        Movement.StartTravel(path);
    }

    static Pos? PickPosition(Pos? start = null, Action<Pos>? onMove = null, bool allowGlyphJump = true)
    {
        Pos cursor = start ?? upos;
        char lastGlyph = '\0';
        int glyphIndex = 0;
        
        while (true)
        {
            onMove?.Invoke(cursor);
            Draw.DrawCurrent(cursor);
            var key = NextKey();
            
            if (key.Key == ConsoleKey.Escape) return null;
            
            if (key.Key == ConsoleKey.Enter || key.KeyChar == '.' || key.KeyChar == ',')
                return cursor;
            
            if (GetDirection(key.Key) is { } dir)
            {
                int dist = key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? 5 : 1;
                Pos next = cursor + dir * dist;
                if (lvl.InBounds(next)) cursor = next;
                lastGlyph = '\0';
            }
            else if (allowGlyphJump && key.KeyChar is '<' or '>' or '+' or '#')
            {
                var matches = FindGlyphPositions(key.KeyChar);
                if (matches.Count > 0)
                {
                    if (key.KeyChar != lastGlyph) glyphIndex = 0;
                    else glyphIndex = (glyphIndex + 1) % matches.Count;
                    cursor = matches[glyphIndex];
                    lastGlyph = key.KeyChar;
                }
            }
        }
    }

    static List<Pos> FindGlyphPositions(char glyph)
    {
        List<Pos> result = [];
        for (int y = 0; y < lvl.Height; y++)
        for (int x = 0; x < lvl.Width; x++)
        {
            Pos p = new(x, y);
            if (!lvl.WasSeen(p)) continue;
            char ch = GetTileGlyph(p);
            if (ch == glyph) result.Add(p);
        }
        return result;
    }

    static char GetTileGlyph(Pos p)
    {
        var mem = lvl.GetMemory(p);
        if (mem is not { } m) return '\0';
        return m.Tile.Type switch
        {
            TileType.Door => m.Door == DoorState.Open ? '\0' : '+',
            TileType.StairsUp or TileType.BranchUp => '<',
            TileType.StairsDown or TileType.BranchDown => '>',
            TileType.Corridor => '#',
            _ => '\0',
        };
    }

    static IEnumerable<Pos> PathToPositions(Pos start, List<Pos> dirs)
    {
        Pos p = start;
        foreach (var d in dirs) yield return p += d;
    }

    static CommandArg GetArg(ArgType type) => type.Kind switch
    {
        ArgKind.None => new NoArg(),
        ArgKind.Dir => GetDirection(NextKey().Key) is { } d ? new DirArg(d) : new NoArg(),
        ArgKind.Int => PromptLine(type.Prompt) is { } s && int.TryParse(s, out int v) ? new IntArg(v) : new NoArg(),
        ArgKind.String => PromptLine(type.Prompt) is { } s ? new StringArg(s) : new NoArg(),
        _ => new NoArg(),
    };

    static string? PromptLine(string? prompt)
    {
        Console.SetCursorPosition(0, 0);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, 0);
        Console.CursorVisible = true;
        if (prompt != null) Console.Write(prompt + ": ");
        string? s = ReadLine();
        Console.CursorVisible = false;
        Console.SetCursorPosition(0, 0);
        Console.Write(new string(' ', Console.WindowWidth));
        return s;
    }

    public static bool YesNo(string prompt)
    {
        g.pline($"{prompt} [yn]");
        while (true)
        {
            var key = NextKey();
            if (key.KeyChar is 'y' or 'Y') return true;
            if (key.KeyChar is 'n' or 'N' or (char)27) return false; // ESC = no
        }
    }

    static char? Ynaq(string prompt)
    {
        g.pline($"{prompt} [ynaq]");
        while (true)
        {
            var key = NextKey();
            if (key.KeyChar is 'y' or 'Y') return 'y';
            if (key.KeyChar is 'n' or 'N') return 'n';
            if (key.KeyChar is 'a' or 'A') return 'a';
            if (key.KeyChar is 'q' or 'Q') return 'q';
            if (key.Key == ConsoleKey.Escape) return 'q';
        }
    }

    public static Pos? GetDirection(ConsoleKey key, bool withCtrl = false) => key switch
    {
        ConsoleKey.H or ConsoleKey.LeftArrow or ConsoleKey.NumPad4 => Pos.W,
        ConsoleKey.Backspace when withCtrl => Pos.W,  // Ctrl+H
        ConsoleKey.J or ConsoleKey.DownArrow or ConsoleKey.NumPad2 => Pos.S,
        ConsoleKey.Enter when withCtrl => Pos.S,      // Ctrl+J
        ConsoleKey.K or ConsoleKey.UpArrow or ConsoleKey.NumPad8 => Pos.N,
        ConsoleKey.L or ConsoleKey.RightArrow or ConsoleKey.NumPad6 => Pos.E,
        ConsoleKey.Y or ConsoleKey.NumPad7 => Pos.NW,
        ConsoleKey.U or ConsoleKey.NumPad9 => Pos.NE,
        ConsoleKey.B or ConsoleKey.NumPad1 => Pos.SW,
        ConsoleKey.N or ConsoleKey.NumPad3 => Pos.SE,
        ConsoleKey.OemPeriod or ConsoleKey.NumPad5 => Pos.Zero,
        _ => null,
    };

    public static bool PromptDirection(out Pos dir, bool allowVertical = false)
    {
        g.pline("In what direction? ");

        if (GetDirection(NextKey().Key) is {} d)
        {
            dir = d;
            return true;
        }
        else
        {
            dir = default;
            return false;
        }
    }


    static void HandleExtended()
    {
        Console.SetCursorPosition(0, 0);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, 0);
        Console.CursorVisible = true;
        Console.Write("#");
        string? name = ReadLine(_extCommands.Keys);
        Console.CursorVisible = false;
        Console.SetCursorPosition(0, 0);
        Console.Write(new string(' ', Console.WindowWidth));
        if (name == null) return;
        if (_extCommands.TryGetValue(name, out Command? cmd))
        {
            CommandArg arg = GetArg(cmd.Arg);
            cmd.Action(arg);
        }
    }

    static string? ReadLine(IEnumerable<string>? completions = null)
    {
        List<char> chars = [];
        int autoCompleteLen = 0; // how many chars were auto-filled
        bool suppressAutoComplete = false;
        while (true)
        {
            // auto-complete if unambiguous
            if (completions != null && chars.Count > 0 && autoCompleteLen == 0 && !suppressAutoComplete)
            {
                string prefix = new([.. chars]);
                var matches = completions.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 1 && matches[0].Length > chars.Count)
                {
                    string suffix = matches[0][chars.Count..];
                    Console.Write(suffix);
                    chars.AddRange(suffix);
                    autoCompleteLen = suffix.Length;
                }
            }
            suppressAutoComplete = false;

            ConsoleKeyInfo k = NextKey();
            if (k.Key == ConsoleKey.Enter) return new string([.. chars]);
            if (k.Key == ConsoleKey.Escape) return null;
            if (k.Key == ConsoleKey.Backspace && chars.Count > 0)
            {
                if (autoCompleteLen > 0)
                {
                    // clear entire autocomplete suffix
                    for (int i = 0; i < autoCompleteLen; i++)
                    {
                        chars.RemoveAt(chars.Count - 1);
                        Console.Write("\b \b");
                    }
                    autoCompleteLen = 0;
                }
                else
                {
                    chars.RemoveAt(chars.Count - 1);
                    Console.Write("\b \b");
                }
                suppressAutoComplete = true;
            }
            else if (k.Key == ConsoleKey.U && k.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                for (int i = chars.Count - 1; i >= 0; i--)
                    Console.Write("\b \b");
                chars.Clear();
                autoCompleteLen = 0;
            }
            else if (k.Key == ConsoleKey.W && k.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                if (autoCompleteLen > 0)
                {
                    for (int i = 0; i < autoCompleteLen; i++)
                    {
                        chars.RemoveAt(chars.Count - 1);
                        Console.Write("\b \b");
                    }
                    autoCompleteLen = 0;
                }
                // delete back to previous space
                while (chars.Count > 0 && chars[^1] == ' ')
                {
                    chars.RemoveAt(chars.Count - 1);
                    Console.Write("\b \b");
                }
                while (chars.Count > 0 && chars[^1] != ' ')
                {
                    chars.RemoveAt(chars.Count - 1);
                    Console.Write("\b \b");
                }
            }
            else if (k.Key == ConsoleKey.Tab && completions != null)
            {
                string prefix = new([.. chars]);
                string? match = completions.FirstOrDefault(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    Console.Write(match[chars.Count..]);
                    chars.AddRange(match[chars.Count..]);
                }
                autoCompleteLen = 0;
            }
            else if (!char.IsControl(k.KeyChar))
            {
                // if typing matches autocompleted char, just consume it
                if (autoCompleteLen > 0 && char.ToLowerInvariant(k.KeyChar) == char.ToLowerInvariant(chars[chars.Count - autoCompleteLen]))
                {
                    autoCompleteLen--;
                }
                else
                {
                    // clear autocomplete suffix if typing something different
                    if (autoCompleteLen > 0)
                    {
                        for (int i = 0; i < autoCompleteLen; i++)
                        {
                            chars.RemoveAt(chars.Count - 1);
                            Console.Write("\b \b");
                        }
                        autoCompleteLen = 0;
                    }
                    chars.Add(k.KeyChar);
                    Console.Write(k.KeyChar);
                }
            }
        }
    }

    public static void HandleKey(ConsoleKeyInfo key)
    {
        Log.Verbose("movement", $"HandleKey: Key={key.Key} Char={(int)key.KeyChar} Mods={key.Modifiers}");
        
        // Ctrl+P is special - doesn't reset message history
        if (key.Key == ConsoleKey.P && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            ShowMessageHistory();
            return;
        }
        
        ResetMessageHistory();
        Movement.Stop(); // any manual input stops running
        
        // Check special commands (ctrl+key)
        foreach (var special in _specialCommands)
        {
            if (special.Matches(key)) { special.Action(); return; }
        }
        
        if (key.KeyChar == '#')
        {
            HandleExtended();
        }
        else if (key.KeyChar == '_')
        {
            DoTravel();
        }
        else if (_commands.TryGetValue(key.KeyChar, out Command? cmd))
        {
            CommandArg arg = GetArg(cmd.Arg);
            cmd.Action(arg);
        }
        else if (GetDirection(key.Key, key.Modifiers.HasFlag(ConsoleModifiers.Control)) is { } dir)
        {
            // Check for shift/ctrl modifiers for running
            Log.Verbose("movement", $"dir key: {key.Key} char: {(int)key.KeyChar} mods: {key.Modifiers} shift={key.Modifiers.HasFlag(ConsoleModifiers.Shift)} ctrl={key.Modifiers.HasFlag(ConsoleModifiers.Control)}");
            if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
            {
                Movement.StartRun(RunMode.UntilBlocked, dir);
                return;
            }
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                Movement.StartRun(RunMode.UntilInteresting, dir);
                return;
            }

            DoMoveU(dir);
        }
        else if (key.KeyChar == '>')
        {
            if (lvl[upos].Type == TileType.StairsDown || lvl[upos].Type == TileType.BranchDown || lvl.HasHole(upos))
                g.Portal(u);
            else if (lvl[upos].IsStairs)
                g.pline("These stairs don't go down.");
        }
        else if (key.KeyChar == '<')
        {
            if (lvl[upos].Type == TileType.StairsUp || lvl[upos].Type == TileType.BranchUp)
                g.Portal(u);
            else if (lvl[upos].IsStairs)
                g.pline("These stairs don't go up.");


        }
    }

    public static void PlayerTurn()
    {
        // Continue activity if one is in progress
        if (u.CurrentActivity != null)
        {
            if (!u.CurrentActivity.Tick())
                u.CurrentActivity = null;
            u.Energy -= ActionCosts.OneAction.Value;
            return;
        }
        
        // Continue running if in run mode
        if (Movement.TryContinueRun())
        {
            Draw.DrawCurrent();
            return;
        }

        Perf.Pause();
        var key = NextKey();
        Perf.Resume();
        Draw.ClearTopLine();
        HandleKey(key);
    }
}
