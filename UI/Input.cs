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

public record Command(string Name, ArgType Arg, Action<CommandArg> Action);

public static class Input
{
    static readonly Dictionary<string, Command> _extCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["quit"] = new("quit", ArgType.None, _ => g.Done("Quit")),
        ["dbg.jump"] = new("dbg.jump", ArgType.Int("Level"), DebugJump),
        ["levelup"] = new("levelup", ArgType.None, _ => DoLevelUp()),
        ["invoke"] = new("invoke", ArgType.None, _ => InvokeItem()),
    };

    static readonly Dictionary<char, Command> _commands = new()
    {
        ['o'] = new("open", ArgType.Dir, OpenDoor),
        ['i'] = new("inventory", ArgType.None, _ => ShowInventory()),
        ['d'] = new("drop", ArgType.None, _ => DropItem()),
        ['D'] = new("multidrop", ArgType.None, _ => DropItems()),
        [','] = new("pickup", ArgType.None, _ => PickupItem()),
        ['.'] = new("pickup", ArgType.None, _ => WaitTurn()),
        ['w'] = new("wield", ArgType.None, _ => WieldWeapon()),
        ['W'] = new("wear", ArgType.None, _ => WearArmor()),
        ['P'] = new("puton", ArgType.None, _ => PutOnAccessory()),
        ['T'] = new("takeoff", ArgType.None, _ => TakeOff()),
        ['R'] = new("remove", ArgType.None, _ => RemoveAccessory()),
        [';'] = new("farlook", ArgType.None, _ => Pokedex.Farlook()),
        ['f'] = new("fire", ArgType.Dir, Fire),
        ['Q'] = new("quiver", ArgType.None, _ => SetQuiver()),
    };

    static void DebugJump(CommandArg arg)
    {
        if (!g.DebugMode || arg is not IntArg(var depth) || depth < 1) return;
        g.GoToLevel(new(u.Level.Branch, depth), SpawnAt.StairsUp);
    }

    public static void DoLevelUp()
    {
        if (!Progression.HasPendingLevelUp(u))
        {
            g.pline("No level up available.");
            return;
        }

        int newLevel = u.CharacterLevel + 1;

        // Build stages
        List<LevelUpStage> stages = [];

        // Class-specific selections
        var classEntry = u.Class?.Progression.ElementAtOrDefault(newLevel - 1);
        if (classEntry?.Selections != null)
        {
            foreach (var sel in classEntry.Selections)
            {
                var options = sel.Options
                    .Where(f => !u.TakenFeats.Contains(f.id))
                    .Where(f => (f.Prereq?.Invoke(u) ?? Availability.Now).State != AvailabilityState.Never)
                    .ToList();
                if (options.Count > 0)
                    stages.Add(new FeatStage(sel.Label, options, sel.Count));
            }
        }

        // Shared schedule feats
        foreach (var featType in Progression.FeatsAtLevel(newLevel))
        {
            var (label, pool) = featType switch
            {
                FeatType.Class => ("Choose a class feat", u.Class?.ClassFeats ?? []),
                FeatType.General => ("Choose a general feat", GeneralFeats.All),
                FeatType.Ancestry => ("Choose an ancestry feat", u.Ancestry?.Feats ?? []),
                _ => (null, null)
            };
            
            if (pool != null && label != null)
            {
                foreach (var f in pool)
                    Log.Write($"DEBUG: feat={f?.Name ?? "NULL"} id={f?.id ?? "NULL"} level={f?.Level}");
                var available = pool
                    .Where(f => f.Level <= newLevel)
                    .Where(f => !u.TakenFeats.Contains(f.id))
                    .Where(f => (f.Prereq?.Invoke(u) ?? Availability.Now).State != AvailabilityState.Never)
                    .ToList();
                if (available.Count > 0)
                    stages.Add(new FeatStage(label, available, 1));
            }
            else if (featType == FeatType.AttributeBoost)
            {
                stages.Add(new AttributeBoostStage());
            }
        }

        // Run stages with back navigation
        int step = 0;
        while (step < stages.Count)
        {
            bool? result = stages[step].Run();
            if (result == null)
            {
                if (step == 0) return; // cancel
                step--;
            }
            else
                step++;
        }

        // Apply everything
        int hpGain = (u.Class?.HpPerLevel ?? 6) + Player.Mod(u.Attributes.Con.Value);
        u.HP.Max += hpGain;
        u.HP.Current += hpGain;

        if (classEntry != null)
            foreach (var brick in classEntry.Grants)
                u.AddFact(brick);

        foreach (var stage in stages)
            stage.Apply();

        u.CharacterLevel = newLevel;
        g.pline($"Welcome to level {newLevel}!");
    }

    interface LevelUpStage
    {
        public abstract bool? Run();
        public abstract void Apply();
    }

    class FeatStage(string label, List<FeatDef> options, int count) : LevelUpStage
    {
        List<FeatDef> _picked = [];

        public bool? Run()
        {
            if (count == 1)
            {
                var picked = ListPicker.Pick(options, label);
                if (picked == null) return null;
                _picked = [picked];
            }
            else
            {
                var picked = ListPicker.PickMultiple(options, label, count);
                if (picked == null) return null;
                _picked = picked;
            }
            return true;
        }

        public void Apply()
        {
            foreach (var feat in _picked)
            {
                u.TakenFeats.Add(feat.id);
                foreach (var brick in feat.Components)
                    u.AddFact(brick);
            }
        }
    }

    class AttributeBoostStage : LevelUpStage
    {
        static readonly SimpleSelectable[] StatSelectables = [
            new("Strength", "Physical power and carrying capacity."),
            new("Dexterity", "Agility, reflexes, and balance."),
            new("Constitution", "Health and stamina."),
            new("Intelligence", "Reasoning and memory."),
            new("Wisdom", "Perception and insight."),
            new("Charisma", "Force of personality."),
        ];

        List<SimpleSelectable>? _picked;

        public bool? Run()
        {
            _picked = ListPicker.PickMultiple(StatSelectables, "Choose 4 attribute boosts:", 4);
            return _picked != null ? true : null;
        }

        public void Apply()
        {
            if (_picked == null) return;
            foreach (var stat in _picked)
            {
                var attr = stat.Name switch
                {
                    "Strength" => u.Attributes.Str,
                    "Dexterity" => u.Attributes.Dex,
                    "Constitution" => u.Attributes.Con,
                    "Intelligence" => u.Attributes.Int,
                    "Wisdom" => u.Attributes.Wis,
                    "Charisma" => u.Attributes.Cha,
                    _ => null
                };
                if (attr == null) continue;
                attr.BaseValue += attr.BaseValue >= 18 ? 1 : 2;
            }
        }
    }

    static void OpenDoor(CommandArg arg)
    {
        if (arg is not DirArg(var d)) return;
        Pos target = upos + d;
        if (lvl.InBounds(target) && lvl[target].Type == TileType.Door)
        {
            if (lvl.OpenDoor(target))
            {
                u.Energy -= ActionCosts.OneAction.Value;
            }
            else
            {
                if (lvl.IsDoorOpen(target))
                    g.pline("It's already open.");
                else if (lvl.IsDoorBroken(target))
                    g.pline("It's already broken beyond repair.");
                else if (lvl.IsDoorLocked(target))
                    g.pline("It's locked.");
            }
        }
        else
        {
            g.pline("There is no door there.");
        }
    }

    static void ShowInventory()
    {
        var menu = new Menu<Item>();
        if (!u.Inventory.Any())
        {
            menu.Add("You have nothing.");
            menu.Display();
            return;
        }
        
        int weight = u.Inventory.Sum(i => i.Def.Weight);
        int maxWeight = 500; // TODO: calc from str
        int slots = u.Inventory.Count();
        int maxSlots = 52;
        menu.Add($"Inventory: {weight}/{maxWeight} weight ({slots}/{maxSlots} slots)", LineStyle.Heading);
        BuildItemList(menu, u.Inventory, u);
        
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        
        // TODO: add menu here later (option based?)
        Pokedex.ShowItemEntry(picked[0]);
    }

    static void BuildItemList(Menu<Item> menu, IEnumerable<Item> items, IUnit? unit = null, bool useInvLet = true)
    {
        var sorted = items
            .OrderBy(i => ItemClasses.Order.IndexOf(i.Def.Class))
            .ThenBy(i => i.InvLet);

        char? lastClass = null;
        char autoLet = 'a';
        foreach (var item in sorted)
        {
            if (item.Def.Class != lastClass)
            {
                lastClass = item.Def.Class;
                menu.Add(ClassDisplayName(lastClass.Value), LineStyle.SubHeading);
            }
            char let = useInvLet ? item.InvLet : autoLet++;
            string name = item.DisplayName;
            if (unit?.Equipped.ContainsValue(item) == true)
                name += item.Def is ArmorDef ? " (being worn)" : " (weapon in hand)";
            if (item == u.Quiver)
                name += " (quivered)";
            menu.Add(let, name, item, item.Def.Class);
        }
    }

    static string ClassDisplayName(char c) => c switch
    {
        ItemClasses.Weapon => "Weapons",
        ItemClasses.Armor => "Armor",
        ItemClasses.Food => "Comestibles",
        ItemClasses.Potion => "Potions",
        ItemClasses.Scroll => "Scrolls",
        ItemClasses.Spellbook => "Spellbooks",
        ItemClasses.Wand => "Wands",
        ItemClasses.Ring => "Rings",
        ItemClasses.Amulet => "Amulets",
        ItemClasses.Tool => "Tools",
        ItemClasses.Gem => "Gems",
        ItemClasses.Gold => "Coins",
        _ => "Other",
    };

    static void DropItem()
    {
        if (!u.Inventory.Any()) return;
        var menu = new Menu<Item>();
        menu.Add("Drop what?", LineStyle.Heading);
        BuildItemList(menu, u.Inventory);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        g.DoDrop(u, picked[0]);
        g.pline($"You drop {picked[0].InvLet} - {picked[0]}.");
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void DropItems()
    {
        if (!u.Inventory.Any()) return;
        var menu = new Menu<Item>();
        menu.Add("Drop what?", LineStyle.Heading);
        BuildItemList(menu, u.Inventory);
        var toDrop = menu.Display(MenuMode.PickAny);
        if (toDrop.Count == 0) return;
        foreach (var item in toDrop)
            g.DoDrop(u, item);
        g.pline("You drop {0} item{1}.", toDrop.Count, toDrop.Count == 1 ? "" : "s");
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void InvokeItem()
    {
        if (!u.Inventory.Any())
        {
            g.pline("You have nothing to invoke.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Invoke what?", LineStyle.Heading);
        BuildItemList(menu, u.Inventory);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        ItemInvoke.Invoke(picked[0]);
    }

    static void WaitTurn() => u.Energy -= ActionCosts.OneAction.Value;

    static void Fire(CommandArg arg)
    {
        if (arg is not DirArg(var dir)) return;
        if (u.Quiver == null)
        {
            g.pline("You have nothing readied.");
            return;
        }
        Item toThrow;
        if (u.Quiver.Count > 1)
            toThrow = u.Quiver.Split(1);
        else
        {
            toThrow = u.Quiver;
            u.Inventory.Remove(toThrow);
            u.Quiver = null;
        }
        g.DoThrow(u, toThrow, dir);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void SetQuiver()
    {
        var throwable = u.Inventory.Where(i => i.Def is WeaponDef w && w.Range > 1).ToList();
        if (!throwable.Any())
        {
            g.pline("You have nothing to ready.");
            return;
        }
        var menu = new Menu<Item?>();
        menu.Add("Ready what? (- for nothing)", LineStyle.Heading);
        menu.AddHidden('-', null);
        foreach (var item in throwable.OrderBy(i => i.InvLet))
        {
            string name = item.DisplayName;
            if (item == u.Quiver)
                name += " (quivered)";
            menu.Add(item.InvLet, name, item);
        }
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        u.Quiver = picked[0];
        if (u.Quiver != null)
            g.pline("You ready {0:the}.", u.Quiver);
        else
            g.pline("You empty your quiver.");
    }

    static void WieldWeapon()
    {
        var weapons = u.Inventory.Where(i => i.Def is WeaponDef).ToList();
        var menu = new Menu<Item?>();
        menu.Add("Wield what? (- for bare hands)", LineStyle.Heading);
        menu.AddHidden('-', null);
        foreach (var item in weapons.OrderBy(i => ItemClasses.Order.IndexOf(i.Def.Class)).ThenBy(i => i.InvLet))
        {
            string name = item.DisplayName;
            if (u.Equipped.ContainsValue(item))
                name += " (weapon in hand)";
            menu.Add(item.InvLet, name, item);
        }
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        g.DoEquip(u, picked[0]);
        if (picked[0] == null)
            g.pline("You are empty handed.");
        else
            g.pline("{0} - {1} (weapon in hand).", picked[0]!.InvLet, picked[0]!.Def.Name);
    }

    static void WearArmor()
    {
        var armors = u.Inventory.Where(i => i.Def is ArmorDef).ToList();
        if (armors.Count == 0)
        {
            g.pline("You have no armor.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Wear what?", LineStyle.Heading);
        BuildItemList(menu, armors, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        var armor = picked[0];
        g.DoEquip(u, armor);
        g.pline("{0} - {1} (being worn).", armor.InvLet, armor.Def.Name);
    }

    static void PutOnAccessory()
    {
        var accessories = u.Inventory.Where(i => i.Def.Class is ItemClasses.Ring or ItemClasses.Amulet).ToList();
        if (accessories.Count == 0)
        {
            g.pline("You have no accessories.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Put on what?", LineStyle.Heading);
        BuildItemList(menu, accessories, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        var item = picked[0];
        g.DoEquip(u, item);
        g.pline("{0} - {1} (being worn).", item.InvLet, item.DisplayName);
    }

    static void TakeOff()
    {
        var equipped = u.Equipped.Values.Where(i => i.Def is ArmorDef).ToList();
        if (equipped.Count == 0)
        {
            g.pline("You have no armor equipped.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Take off what?", LineStyle.Heading);
        BuildItemList(menu, equipped, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        g.DoUnequip(u, picked[0]);
        g.pline("You take off {0}.", Grammar.DoName(picked[0]));
    }

    static void RemoveAccessory()
    {
        var equipped = u.Equipped.Values.Where(i => i.Def.Class is ItemClasses.Ring or ItemClasses.Amulet).ToList();
        if (equipped.Count == 0)
        {
            g.pline("You have no accessories equipped.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Remove what?", LineStyle.Heading);
        BuildItemList(menu, equipped, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        g.DoUnequip(u, picked[0]);
        g.pline("You remove {0}.", Grammar.DoName(picked[0]));
    }

    static void ShowAbilities()
    {
        if (u.Actions.Count == 0)
        {
            g.pline("You have no abilities.");
            return;
        }
        var menu = new Menu<ActionBrick>();
        menu.Add("Use which ability?", LineStyle.Heading);
        char let = 'a';
        string whyNot;
        foreach (var action in u.Actions)
        {
            var data = u.ActionData.GetValueOrDefault(action);
            bool ready = action.CanExecute(u, data, Target.None, out whyNot);
            string status = ready ? "" : $" ({whyNot})";
            menu.Add(let++, action.Name + status, action);
        }
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        var ability = picked[0];
        var abilityData = u.ActionData.GetValueOrDefault(ability);
        
        Target target = Target.None;
        if (ability.Targeting == TargetingType.Direction)
        {
            g.pline("In what direction?");
            Draw.DrawCurrent();
            var dir = GetDirection(Console.ReadKey(true).Key);
            if (dir == null) return;
            target = new Target(null, dir.Value);
        }
        // TODO: handle TargetingType.Unit, TargetingType.Pos
        
        if (!ability.CanExecute(u, abilityData, target, out whyNot))
        {
            g.pline($"That ability is not ready ({whyNot}).");
            return;
        }
        ability.Execute(u, abilityData, target);
        u.Energy -= ability.GetCost(u, abilityData, target).Value;
    }

    static void ShowBranchOverview()
    {
        Branch branch = u.Level.Branch;
        while (true)
        {
            var menu = new Menu<string>();
            menu.Add($"{branch.Name}: levels 1 to {branch.MaxDepth}", LineStyle.Heading);
            
            for (int d = 1; d <= branch.MaxDepth; d++)
            {
                List<string> features = [];
                var resolved = branch.ResolvedLevels[d - 1];
                
                if (resolved.Template != null) features.Add(resolved.Template.DisplayName);
                if (resolved.BranchDown != null) features.Add($"Stairs down to {g.Branches[resolved.BranchDown].Name}");
                if (resolved.BranchUp != null) features.Add($"Stairs up to {g.Branches[resolved.BranchUp].Name}");
                
                bool isHere = branch == u.Level.Branch && d == u.Level.Depth;
                if (features.Count == 0 && !isHere) continue;
                
                string marker = isHere ? " <- You are here" : "";
                menu.Add($"   Level {d}:{marker}");
                foreach (var f in features)
                    menu.Add($"      {f}");
            }

            if (branch.Name != "Dungeon")
            {
                menu.Add("");
                menu.Add("(tab) switch branch, (esc) close");
                menu.AddHidden('\t', "switch");

                var result = menu.Display(MenuMode.PickOne);
                if (result.Count == 0 || result[0] != "switch") break;

                // Toggle to main or back to current
                branch = branch == g.Branches["dungeon"] ? u.Level.Branch : g.Branches["dungeon"];
            }
            else
            {
                menu.Display(MenuMode.None);
                break;
            }
            
        }
    }

    static void ShowCharacterInfo()
    {
        var menu = new Menu<string>();
        menu.Add("Base Attributes", LineStyle.Heading);
        menu.Add("");
        menu.Add("Starting", LineStyle.SubHeading);
        menu.Add($"  class          : {u.Class?.Name ?? "none"}");
        menu.Add($"  deity          : {u.Deity?.Name ?? "none"}");
        menu.Add($"  ancestry       : {u.Ancestry?.Name ?? "none"}");
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
        menu.Add("");
        menu.Add("Attributes", LineStyle.SubHeading);
        menu.Add($"  STR {u.Attributes.Str.Value,2}  INT {u.Attributes.Int.Value,2}  DEX {u.Attributes.Dex.Value,2}  WIS {u.Attributes.Wis.Value,2}  CON {u.Attributes.Con.Value,2}  CHA {u.Attributes.Cha.Value,2}");
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
        var buffs = u.LiveFacts.Where(f => f.Brick.IsBuff).ToList();
        // Also check inventory items for buffs
        foreach (var item in u.Inventory)
            buffs.AddRange(item.LiveFacts.Where(f => f.Brick.IsBuff));
        
        var menu = new Menu();
        menu.Add("Current Effects", LineStyle.Heading);
        menu.Add("");
        if (buffs.Count == 0)
        {
            menu.Add("You have no active effects.");
        }
        else
        {
            foreach (var fact in buffs)
                menu.Add($"  {fact.Brick.BuffName}");
        }
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
            g.Messages.Clear();
            g.Messages.Add(history[history.Count - 1 - _msgHistoryIdx]);
            Draw.DrawCurrent();
        }
        else
        {
            // Third press or exhausted: show full list
            _msgHistoryIdx = -1;
            var menu = new Menu();
            menu.Add("Message History", LineStyle.Heading);
            for (int i = 0; i < history.Count; i++)
                menu.Add(history[i]);
            menu.Display();
        }
    }

    static void ResetMessageHistory() => _msgHistoryIdx = -1;

    static void PickupItem()
    {
        var items = lvl.ItemsAt(upos);
        if (items.Count == 0) return;
        List<Item> toPickup;
        if (items.Count == 1)
        {
            toPickup = [items[0]];
        }
        else
        {
            var menu = new Menu<Item>();
            menu.Add("Pick up what?", LineStyle.Heading);
            BuildItemList(menu, items, useInvLet: false);
            toPickup = menu.Display(MenuMode.PickAny);
            if (toPickup.Count == 0) return;
        }
        foreach (var item in toPickup)
            g.DoPickup(u, item);
        if (toPickup.Count == 1)
            g.pline($"{toPickup[0].InvLet} - {toPickup[0]}.");
        else
            g.pline("You pick up {0} items.", toPickup.Count);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void DoTravel()
    {
        g.pline("Where do you want to travel to?");
        Pos cursor = upos;
        char lastGlyph = '\0';
        int glyphIndex = 0;
        
        while (true)
        {
            Draw.DrawCurrent(cursor);
            var key = Console.ReadKey(true);
            
            if (key.Key == ConsoleKey.Escape)
            {
                g.Messages.Clear();
                return;
            }
            
            if (key.Key == ConsoleKey.Enter || key.KeyChar == '.')
            {
                g.Messages.Clear();
                if (cursor == upos) return;
                var path = Pathfinding.FindPath(lvl, upos, cursor);
                if (path == null || path.Count == 0)
                {
                    g.pline("You can't find a path there.");
                    return;
                }
                Log.Verbose("movement", $"Travel: from={upos} to={cursor} path=[{string.Join(",", PathToPositions(upos, path))}]");
                Movement.StartTravel(path);
                return;
            }
            
            if (GetDirection(key.Key) is { } dir)
            {
                int dist = key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? 5 : 1;
                Pos next = cursor + dir * dist;
                if (lvl.InBounds(next)) cursor = next;
                lastGlyph = '\0';
            }
            else if (key.KeyChar is '<' or '>' or '+' or '#')
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

    static void LookHere()
    {
        var items = lvl.ItemsAt(upos);
        if (items.Count == 0) {}
        else if (items.Count == 1)
            g.pline($"You see here {items[0]:an}.");
        else if (items.Count >= 5)
            g.pline("There are {0} items here.", items.Count >= 10 ? "many" : "several");
        else
            g.pline("There are {0} items here.", items.Count);

        foreach (var n in upos.Neighbours())
        {
            if (!lvl.Traps.TryGetValue(n, out var trap) || trap.PlayerSeen) continue;

            using var ctx = PHContext.Create(Monster.DM, Target.From(u));
            if (CreateAndDoCheck(ctx, "perception", trap.DetectDC, "detect trap"))
            {
                g.pline("You find a trap.");
                trap.PlayerSeen = true;
            }
        }

        var msg = lvl.GetState(upos)?.Message;
        if (msg != null)
            g.pline(msg);
    }

    static CommandArg GetArg(ArgType type) => type.Kind switch
    {
        ArgKind.None => new NoArg(),
        ArgKind.Dir => GetDirection(Console.ReadKey(true).Key) is { } d ? new DirArg(d) : new NoArg(),
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
        return s;
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
        _ => null,
    };

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
        while (true)
        {
            ConsoleKeyInfo k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Enter) return new string([.. chars]);
            if (k.Key == ConsoleKey.Escape) return null;
            if (k.Key == ConsoleKey.Backspace && chars.Count > 0)
            {
                chars.RemoveAt(chars.Count - 1);
                Console.Write("\b \b");
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
            }
            else if (!char.IsControl(k.KeyChar))
            {
                chars.Add(k.KeyChar);
                Console.Write(k.KeyChar);
            }
        }
    }

    public static void HandleKey(ConsoleKeyInfo key)
    {
        Log.Verbose("movement", $"HandleKey: Key={key.Key} Char={(int)key.KeyChar} Mods={key.Modifiers}");
        if (key.Key == ConsoleKey.P && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            ShowMessageHistory();
            return;
        }
        
        ResetMessageHistory();
        Movement.Stop(); // any manual input stops running
        
        if (key.Key == ConsoleKey.G && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            ShowAbilities();
        }
        else if (key.Key == ConsoleKey.O && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            ShowBranchOverview();
        }
        else if (key.Key == ConsoleKey.X && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            ShowCharacterInfo();
        }
        else if (key.KeyChar == '#')
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

            Pos next = upos + dir;
            if (!lvl.InBounds(next)) return;
            var tgt = lvl.UnitAt(next);
            if (tgt != null)
            {
                g.Attack(u, tgt, u.GetWieldedItem());
                u.Energy -= ActionCosts.OneAction.Value;
            }
            else if (lvl.CanMoveTo(upos, next, u) || g.DebugMode)
            {
                lvl.MoveUnit(u, next);
                LookHere();
            }
            else if (lvl.IsDoorClosed(next))
            {
                OpenDoor(new DirArg(dir));
            }
        }
        else if (key.KeyChar == '>')
        {
            if (lvl[upos].Type == TileType.StairsDown || lvl[upos].Type == TileType.BranchDown)
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
        // Continue running if in run mode
        if (Movement.TryContinueRun())
        {
            Draw.DrawCurrent();
            return;
        }

        Draw.ClearMessages();
        Perf.Pause();
        var key = Console.ReadKey(true);
        Perf.Resume();
        HandleKey(key);
    }
}

public enum MenuMode { None, PickOne, PickAny }
public enum LineStyle { Text, Heading, SubHeading, Item }

public class Menu<T>
{
    readonly List<(char? Letter, string Text, T? Value, LineStyle Style, char? Category)> _items = [];
    readonly Dictionary<char, T?> _hidden = [];
    
    public void Add(string line, LineStyle style = LineStyle.Text) => _items.Add((null, line, default, style, null));
    public void Add(char letter, string text, T value, char? category = null) => _items.Add((letter, text, value, LineStyle.Item, category));
    public void AddHidden(char letter, T? value) => _hidden[letter] = value;

    public List<T> Display(MenuMode mode = MenuMode.None)
    {
        var layer = Draw.Overlay;
        using var _ = layer.Activate();
        
        int maxLines = Draw.ViewHeight - 3;
        int pages = (_items.Count + maxLines - 1) / maxLines;
        bool fullscreen = _items.Count > maxLines;
        
        // fullscreen uses more lines (covers status)
        if (fullscreen)
        {
            maxLines = Draw.ScreenHeight - 2; // leave 1 for prompt
            pages = (_items.Count + maxLines - 1) / maxLines;
            layer.FullScreen = true;
        }

        int page = 0;
        HashSet<int> selected = [];

        // calc width once from all items
        int contentWidth = _items.Max(x => x.Style == LineStyle.Item && x.Letter.HasValue 
            ? x.Text.Length + 5  // "a  - text"
            : x.Text.Length);
        contentWidth = Math.Max(contentWidth, 30); // min width for prompt
        int menuWidth = contentWidth + 2;

        // if too wide or multi-page, go fullscreen (offx=0), else right-align
        int menuX = (menuWidth >= Draw.ViewWidth - 10 || fullscreen)
            ? 0
            : Draw.ViewWidth - menuWidth - 1;
        
        // fullscreen uses full width
        if (menuX == 0)
        {
            contentWidth = Draw.ViewWidth - 2;
            menuWidth = Draw.ViewWidth;
        }

        int startY = fullscreen ? 0 : Draw.MapRow;

        while (true)
        {
            Draw.ClearOverlay();
            Draw.Overlay.FullScreen = fullscreen;
            var pageItems = _items.Skip(page * maxLines).Take(maxLines).ToList();
            int pageOffset = page * maxLines;
            
            var lines = new List<(string Text, LineStyle Style)>();
            for (int i = 0; i < pageItems.Count; i++)
            {
                var (letter, text, _, style, _) = pageItems[i];
                if (style == LineStyle.Item && letter.HasValue)
                {
                    char sel = mode == MenuMode.PickAny && selected.Contains(pageOffset + i) ? '+' : '-';
                    lines.Add(($"{letter} {sel} {text}", style));
                }
                else
                {
                    lines.Add((text, style));
                }
            }
            
            string prompt = mode switch
            {
                MenuMode.PickAny => pages > 1 ? $"({page + 1}/{pages}) < > page, letter toggle, enter confirm" : "letter toggle, enter confirm",
                _ => pages > 1 ? $"({page + 1}/{pages}) < > page" : "(press any key)"
            };
            lines.Add((prompt, LineStyle.Text));

            int menuHeight = lines.Count + 2;
            Draw.OverlayFill(menuX, startY, menuWidth, menuHeight);

            int y = startY + 1;
            foreach (var (text, style) in lines)
            {
                if (style == LineStyle.SubHeading)
                {
                    Draw.OverlayWrite(menuX + 1, y++, text, style: CellStyle.Reverse);
                }
                else
                {
                    Draw.OverlayWrite(menuX + 1, y++, text);
                }
            }

            Draw.Blit();
            var key = Console.ReadKey(true);
            
            // paging
            if (key.Key == ConsoleKey.RightArrow || key.KeyChar == '>' || key.Key == ConsoleKey.Spacebar)
            {
                if (pages > 1) page = (page + 1) % pages;
                else break;
                continue;
            }
            if (key.Key == ConsoleKey.LeftArrow || key.KeyChar == '<' || key.KeyChar == '^')
            {
                if (pages > 1) page = (page - 1 + pages) % pages;
                continue;
            }
            
            // select all in PickAny
            if (mode == MenuMode.PickAny && (key.KeyChar == '.' || key.KeyChar == ','))
            {
                var selectable = _items.Select((item, i) => (item, i)).Where(x => x.item.Value != null).ToList();
                bool allSelected = selectable.All(x => selected.Contains(x.i));
                foreach (var (_, i) in selectable)
                {
                    if (allSelected) selected.Remove(i);
                    else selected.Add(i);
                }
                continue;
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                return [];
            }
            
            // confirm for PickAny
            if (mode == MenuMode.PickAny && (key.Key == ConsoleKey.Enter || key.KeyChar == '\n'))
            {
                return selected.Select(i => _items[i].Value!).ToList();
            }
            
            // letter selection
            char ch = key.KeyChar;
            
            // hidden hotkeys
            if (_hidden.TryGetValue(ch, out var hiddenValue))
            {
                return [hiddenValue!];
            }
            
            int idx = _items.FindIndex(x => x.Letter == ch);
            if (idx >= 0 && _items[idx].Value != null)
            {
                if (mode == MenuMode.PickOne)
                {
                    return [_items[idx].Value!];
                }
                if (mode == MenuMode.PickAny)
                {
                    if (!selected.Remove(idx)) selected.Add(idx);
                    continue;
                }
            }
            
            // category toggle in PickAny
            if (mode == MenuMode.PickAny)
            {
                var catItems = _items.Select((item, i) => (item, i))
                    .Where(x => x.item.Category == ch && x.item.Value != null)
                    .ToList();
                if (catItems.Count > 0)
                {
                    bool allSelected = catItems.All(x => selected.Contains(x.i));
                    foreach (var (_, i) in catItems)
                    {
                        if (allSelected) selected.Remove(i);
                        else selected.Add(i);
                    }
                    continue;
                }
            }
            
            // any other key exits for None/PickOne
            if (mode != MenuMode.PickAny) break;
        }
        return [];
    }
}

public class Menu : Menu<object>
{
    public void Add(char letter, string text) => Add(letter, text, text);
    public void Display() => Display(MenuMode.None);
}
