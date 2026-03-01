using System.Text;
using Pathhack.Game.Classes;

namespace Pathhack.UI;

public static class Pokedex
{
    public static void Farlook()
    {
        Pos cursor = upos;
        var monsters = lvl.LiveUnits.Where(m => !m.IsDead && lvl.IsVisible(m.Pos)).ToList();
        int monsterIdx = -1;

        using var handle = WM.CreateTransient(Draw.MapWidth, Draw.MapHeight, x: 0, y: 1, z: 3);
        var ov = handle.Window;
        // Also need a message overlay
        using var msgHandle = WM.CreateTransient(Draw.ScreenWidth, 1, x: 0, y: 0, z: 3);
        var msgOv = msgHandle.Window;

        while (true)
        {
            ov.Clear();
            msgOv.Clear();
            ov[cursor.X, cursor.Y] = new Cell('X', ConsoleColor.Yellow, ConsoleColor.Black, CellStyle.Bold);

            string desc = DescribeAt(cursor);
            msgOv.At(0, 0).Write(desc.PadRight(Draw.ScreenWidth));

            Draw.Blit();
            var key = Input.NextKey();

            if (key.Key == ConsoleKey.Escape) break;

            if (key.KeyChar is '.' or ',' or ';' or ':')
            {
                var unit = lvl.UnitAt(cursor);
                if (unit is Monster m && m.Perception >= PlayerPerception.Warned)
                    ShowMonsterEntry(m);
                break;
            }

            Pos? dir = key.KeyChar switch
            {
                'h' => Pos.W,
                'j' => Pos.S,
                'k' => Pos.N,
                'l' => Pos.E,
                'y' => Pos.NW,
                'u' => Pos.NE,
                'b' => Pos.SW,
                'n' => Pos.SE,
                'H' => Pos.W * 8,
                'J' => Pos.S * 8,
                'K' => Pos.N * 8,
                'L' => Pos.E * 8,
                'Y' => Pos.NW * 8,
                'U' => Pos.NE * 8,
                'B' => Pos.SW * 8,
                'N' => Pos.SE * 8,
                _ => null
            };
            if (dir is { } d)
            {
                Pos next = cursor + d;
                if (lvl.InBounds(next)) cursor = next;
                continue;
            }

            if (key.KeyChar == 'm' && monsters.Count > 0)
            {
                monsterIdx = (monsterIdx + 1) % monsters.Count;
                cursor = monsters[monsterIdx].Pos;
            }
            else if (key.KeyChar == 'M' && monsters.Count > 0)
            {
                monsterIdx = (monsterIdx - 1 + monsters.Count) % monsters.Count;
                cursor = monsters[monsterIdx].Pos;
            }
            else if (key.KeyChar == '@')
            {
                cursor = upos;
            }
        }
    }

    static readonly string[] WarningDescs = [
        "unknown creature causing you worry",
        "unknown creature causing you concern",
        "unknown creature causing you anxiety",
        "unknown creature causing you disquiet",
        "unknown creature causing you alarm",
        "unknown creature causing you dread",
    ];

    static string? SwarmAt(Pos p) => lvl.IsVisible(p) ? lvl.AllSwarms.FirstOrDefault(s => s.Where == p)?.Name : null;

    static string DescribeAt(Pos p)
    {
        var unit = lvl.UnitAt(p);
        var swarmName = SwarmAt(p);
        string swarmSuffix = swarmName != null ? $" + {swarmName}" : "";

        if (unit is Monster m && !m.IsPlayer)
        {
            switch (m.Perception)
            {
                case PlayerPerception.Visible:
                case PlayerPerception.Detected:
                case PlayerPerception.Warned:
                    return $"{m.Glyph.Value}  {m}{swarmSuffix}";
                case PlayerPerception.Unease:
                    int warnLevel = Math.Clamp(m.EffectiveLevel / 4, 0, 5);
                    return $"{warnLevel}  {WarningDescs[warnLevel]}";
                case PlayerPerception.Guess:
                    return "?  something was here";
            }
        }

        if (unit != null && unit.IsPlayer) return $"yourself{swarmSuffix}";

        if (!lvl.IsVisible(p) && !lvl.WasSeen(p)) return "unexplored";

        if (swarmName != null)
            return $"µ  {swarmName}";

        var items = lvl.ItemsAt(p);
        if (items.Count > 0)
            return $"{items[^1].Def.Class}  {items[^1].Def.Name}" + (items.Count > 1 ? $" (and {items.Count - 1} more)" : "");

        if (lvl.Traps.TryGetValue(p, out var trap) && trap.PlayerSeen)
            return $"^  {trap.Type} trap";

        if (lvl.GetState(p)?.Feature is { } feature && feature.Desc != null)
            return $"{feature.Glyph?.Value ?? '_'}  {feature.Desc}";

        var tile = lvl[p];
        return tile.Type switch
        {
            TileType.Floor => ". floor",
            TileType.Wall => "wall",
            TileType.Corridor => "# corridor",
            TileType.Door => "+ door",
            TileType.StairsUp => "< stairs up",
            TileType.StairsDown => "> stairs down",
            TileType.BranchUp => "< branch stairs up",
            TileType.BranchDown => "> branch stairs down",
            TileType.Rock => "solid rock",
            TileType.Grass => ", grass",
            TileType.Tree => "± tree",
            TileType.Water => "~ water",
            _ => "unknown"
        };
    }

    static void ShowMonsterEntry(Monster m)
    {
        var menu = new TextMenu();

        menu.Add();
        menu.AddHeading($"{m.RealName,-24} Creature CR {m.Def.BaseLevel} {m.CreatureTypeRendered}");
        menu.Add($"{m.Def.Size}");
        menu.Add();
        menu.Add($"AC {m.GetAC()}; HP {m.Def.HpPerLevel}");
        var speed = m.QueryModifiers(CommonQueries.SpeedModifiersFlat);
        Log.Write($"speed: {speed}");
        menu.Add($"Movement: {SpeedDesc(m.LandMove)}");
        menu.Add();

        var grantedActions = m.LiveFacts
            .Where(f => f.Brick is GrantAction)
            .Select(f => ((GrantAction)f.Brick).Action)
            .ToList();

        bool hasWeaponOrNatural = grantedActions.Any(a => a is AttackWithWeapon || (a is NaturalAttack n && n.Weapon != m.Def.Unarmed));

        foreach (var fact in m.LiveFacts.Where(f => f.Brick is GrantAction))
        {
            var grant = (GrantAction)fact.Brick;
            if (grant.Action is AttackWithWeapon)
                menu.Add($"Melee weapon {SignedBonus(m.Def.AttackBonus)}{Bonus(m.Def.DamageBonus, " damage")}");
            else if (grant.Action is NaturalAttack nat)
            {
                if (nat.Weapon == m.Def.Unarmed && hasWeaponOrNatural) continue;
                menu.Add($"Melee {nat.Weapon.Name} {SignedBonus(m.Def.AttackBonus)}, Damage {nat.Weapon.BaseDamage}{Bonus(m.Def.DamageBonus)} {nat.Weapon.DamageType.SubCat}");
            }
        }

        foreach (var fact in m.LiveFacts.Where(f => f.Brick is GrantAction))
        {
            var grant = (GrantAction)fact.Brick;
            if (grant.Action is not AttackWithWeapon and not NaturalAttack)
                menu.Add($"  {grant.Action.Name}");
        }

        foreach (var fact in m.LiveFacts.Where(f => f.Brick is not GrantAction && f.Brick.PokedexDescription != null))
            menu.Add($"  {fact.Brick.PokedexDescription}");

        if (m.Spells.Count > 0)
        {
            menu.Add();
            var byLevel = m.Spells.GroupBy(s => s.Level).OrderBy(g => g.Key);
            foreach (var group in byLevel)
            {
                var pool = m.GetPool($"spell_l{group.Key}");
                string slots = pool != null ? $" ({pool.Max} slots)" : "";
                menu.Add($"Level {group.Key} spells{slots}:");
                foreach (var spell in group)
                    menu.Add($"  {spell.Name}");
            }
        }

        bool firstBuff = true;
        foreach (var buff in m.LiveFacts.Where(f => f.Brick.IsBuff))
        {
            if (firstBuff) { menu.Add(); menu.Add("Active effects:"); firstBuff = false; }
            menu.Add($"  {buff.DisplayName}");
        }

        menu.Display();
    }

    static string SpeedDesc(ActionCost cost) => cost.Value switch
    {
        0 => "immobile",
        <= 8 => $"very fast ({cost.Value})",
        <= 10 => $"fast ({cost.Value})",
        12 => $"normal ({cost.Value})",
        <= 16 => $"slow ({cost.Value})",
        _ => $"very slow ({cost.Value})"
    };

    internal static string Bonus(int val, string suffix = "") =>
        val == 0 ? "" : $" {val:+#;-#}{suffix}";

    internal static string SignedBonus(int val) => $"{val:+#;-#;+0}";

    public static void ShowItemEntry(Item item)

    {
        var def = item.Def;
        var menu = new TextMenu();
        bool runesKnown = item.Knowledge.HasFlag(ItemKnowledge.PropRunes);
        bool qualityKnown = item.Knowledge.HasFlag(ItemKnowledge.PropQuality);
        bool potencyKnown = item.Knowledge.HasFlag(ItemKnowledge.PropPotency);

        var eqKv = item.Holder?.Equipped.FirstOrDefault(kv => kv.Value == item);
        string equipped = eqKv is { Value: not null } kv ? $" {Input.EquipDescription(item, kv.Key)}" : "";
        menu.AddHeading($"{item.DisplayNameWeighted}{equipped}");
        menu.Add();

        if (def is WeaponDef wpn)
        {
            ConsoleColor ProfColor(ProficiencyLevel l) => l switch
            {
                ProficiencyLevel.Legendary => ConsoleColor.Magenta,
                ProficiencyLevel.Master => ConsoleColor.Blue,
                ProficiencyLevel.Expert => ConsoleColor.Green,
                ProficiencyLevel.Trained => ConsoleColor.White,
                _ => ConsoleColor.DarkGray,
            };

            string C(string text, string key) => $"[fg={ProfColor(u.GetProficiency(key))}]{text}[/]";
            string capType = wpn.WeaponType != null ? char.ToUpper(wpn.WeaponType[0]) + wpn.WeaponType[1..] : "";
            string typeStr = wpn.WeaponType != null ? $"{C(capType, wpn.WeaponType)} / " : "";
            menu.Add($"{typeStr}{C(WeaponStyle.Pretty(wpn.Style), wpn.Style)} / {C(WeaponGrip.Pretty(wpn.Grip), wpn.Grip)}");

            string hands = wpn.Hands == 1 ? "One-handed" : "Two-handed";
            string throwable = wpn.Launcher != null ? " Throwable." : "";
            menu.Add($"{hands} {wpn.DamageType.SubCat} weapon.{throwable}");
            if (wpn.Reach > 1) menu.Add($"Reach {wpn.Reach}.");
            menu.Add($"Base damage: {wpn.BaseDamage}");
            int ab = u.GetAttackBonus(wpn) + (potencyKnown ? item.Potency : 0);
            menu.Add($"Attack bonus: {ab:+#;-#;+0}");
            int profLevel = (int)u.GetProficiency(wpn).Level;
            menu.Add($"  str {u.StrMod:+#;-#;+0}  prof {profLevel:+#;-#;+0}" +
                (potencyKnown ? $"  potency {item.Potency:+#;-#;+0}" : ""), ConsoleColor.Cyan);

            if (potencyKnown)
            {
                menu.Add($"Potency: {item.Potency}");
            }

            if (qualityKnown)
            {
                if (item.Fundamental?.Brick is RuneBrick fund)
                {
                    if (fund.IsNull)
                        menu.Add("Fundamental: blocked");
                    else
                        menu.Add($"Fundamental: {fund.DisplayName}, {fund.Description}");
                }
                else
                    menu.Add("Fundamental: empty");
            }

            if (runesKnown)
            {
                if (potencyKnown)
                    menu.Add($"Property slots: {item.PropertyRunes.Count}/{item.Potency}");
                foreach (var rune in item.PropertyRunes)
                    menu.Add($"  - {((RuneBrick)rune.Brick).DisplayName}, {((RuneBrick)rune.Brick).Description}");
                if (potencyKnown)
                    for (int i = item.PropertyRunes.Count; i < item.Potency; i++)
                        menu.Add("  - [empty]");
            }
            else if (item.HasEnchantments)
            {
                menu.Add("Enchanted — properties unknown.", ConsoleColor.DarkYellow);
            }
        }
        else if (def is ArmorDef armor)
        {
            string armorName = armor.Proficiency switch
            {
                Proficiencies.NakedArmor => "Unarmored",
                Proficiencies.LightArmor => "Light armor",
                Proficiencies.MediumArmor => "Medium armor",
                Proficiencies.HeavyArmor => "Heavy armor",
                Proficiencies.Shield => "Shield",
                _ => armor.Proficiency,
            };
            var prof = u.GetProficiency(armor.Proficiency);
            ConsoleColor profColor = prof switch
            {
                ProficiencyLevel.Legendary => ConsoleColor.Magenta,
                ProficiencyLevel.Master => ConsoleColor.Blue,
                ProficiencyLevel.Expert => ConsoleColor.Green,
                ProficiencyLevel.Trained => ConsoleColor.White,
                _ => ConsoleColor.DarkGray,
            };
            menu.Add($"{armorName} ({prof})", profColor);
            menu.Add($"AC bonus: +{armor.ACBonus}");

            if (armor.DexCap < 99)
                menu.Add($"Dex cap: {armor.DexCap}");
            if (armor.CheckPenalty != 0)
                menu.Add($"Check penalty: {armor.CheckPenalty}");
        }

        menu.Add();
        string stackable = def.Stackable ? " Stackable." : "";
        menu.Add($"Weighs {def.Weight}. Made of {item.Material}.{stackable}");

        if ((def.IsUnique || def.IsKnown()) && def.PokedexDescription != null)
        {
            menu.Add();
            menu.Add(def.PokedexDescription);
        }

        if (!runesKnown && item.HasEnchantments && def is not WeaponDef)
        {
            menu.Add("Properties not identified.", ConsoleColor.DarkYellow);
        }

        menu.Display();
    }
}

public static class PokedexExplorer
{
    const int SearchBarY = 1;
    const int MaxDropdown = 12;

    static object[] BuildEntries() =>
    [
        .. AllMonsters.ActuallyAll,
        .. AllItems.All,
        .. MasonryYard.AllSpells,
        .. GeneralMechanics.All,
        .. StatusPages.All,
        .. AfflictionPages.All,
        .. Pantheon.All,
        .. Ancestries.All,
        .. GeneralFeats.All,
        .. ClassDefs.All.SelectMany(c => c.ClassFeats),
    ];

    static string NameOf(object entry) => entry switch
    {
        MonsterDef m => m.Name,
        ItemDef i => i.Name,
        SpellBrickBase s => s.Name,
        MechanicsPage p => p.Name,
        DeityDef d => d.Name,
        AncestryDef a => a.Name,
        FeatDef f => f.Name,
        _ => entry.ToString() ?? "",
    };

    static string CategoryOf(object entry) => entry switch
    {
        MonsterDef => "monster",
        WeaponDef => "weapon",
        ArmorDef => "armor",
        PotionDef => "potion",
        ScrollDef => "scroll",
        ItemDef => "item",
        SpellBrickBase => "spell",
        MechanicsPage => "mechanics",
        DeityDef => "deity",
        AncestryDef => "ancestry",
        FeatDef f => f.Type switch
        {
            FeatType.Class => "class feat",
            FeatType.General => "general feat",
            FeatType.Ancestry => "ancestry feat",
            _ => "feat",
        },
        _ => "",
    };

    static readonly Dictionary<object, string> _descCache = [];

    static string CachedDescription(object entry)
    {
        if (!_descCache.TryGetValue(entry, out var desc))
        {
            desc = DescribeEntry(entry);
            _descCache[entry] = desc;
        }
        return desc;
    }

    static int MatchRank(object entry, string query)
    {
        if (NameOf(entry).Contains(query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (CachedDescription(entry).Contains(query, StringComparison.OrdinalIgnoreCase)) return 1;
        return -1;
    }

    public static void Open()
    {
        object[] entries = BuildEntries();

        using var handle = WM.CreateTransient(Draw.ScreenWidth, Draw.ScreenHeight, z: 5, opaque: true);
        var win = handle.Window;

        string query = "";
        object? viewing = null;
        int cursor = 0;
        bool searching = true;
        int linkCursor = -1;
        List<string> links = [];
        List<object> navStack = [];
        int navPos = -1;

        while (true)
        {
            win.Clear();

            int barX = 2;
            int barWidth = Draw.ScreenWidth - 4;
            string prompt = searching ? $"/ {query}▌" : $"/ {query}";
            win.At(barX, SearchBarY).Write(prompt, ConsoleColor.White);
            win.At(barX, SearchBarY + 1).Write(new string('─', barWidth), ConsoleColor.DarkGray);

            if (searching)
            {
                List<object> matches = [];
                if (query.Length > 0)
                    matches = entries
                        .Select(e => (entry: e, rank: MatchRank(e, query)))
                        .Where(x => x.rank >= 0)
                        .OrderBy(x => x.rank)
                        .Select(x => x.entry)
                        .ToList();

                if (matches.Count > 0)
                {
                    cursor = Math.Clamp(cursor, 0, matches.Count - 1);
                    int dropY = SearchBarY + 2;
                    int shown = Math.Min(matches.Count, MaxDropdown);

                    int scroll = 0;
                    if (shown < matches.Count)
                    {
                        scroll = cursor - shown / 2;
                        scroll = Math.Clamp(scroll, 0, matches.Count - shown);
                    }

                    for (int i = 0; i < shown; i++)
                    {
                        int idx = scroll + i;
                        var style = idx == cursor ? CellStyle.Reverse : CellStyle.None;
                        string label = NameOf(matches[idx]);
                        string cat = CategoryOf(matches[idx]);
                        string line = cat.Length > 0 ? $"{label}  ({cat})" : label;
                        if (line.Length > barWidth) line = line[..(barWidth - 1)] + "…";
                        win.At(barX, dropY + i).Write(line, ConsoleColor.White, ConsoleColor.Black, style);
                    }

                    if (matches.Count > shown)
                        win.At(barX + barWidth - 6, dropY + shown).Write($"({matches.Count})", ConsoleColor.DarkGray);
                }
                else if (query.Length > 0)
                {
                    win.At(barX, SearchBarY + 2).Write("No matches", ConsoleColor.DarkGray);
                }
                else
                {
                    int hy = SearchBarY + 2;
                    win.At(barX, hy++).Write("Search monsters, spells, items, feats,", ConsoleColor.DarkGray);
                    win.At(barX, hy++).Write("deities, ancestries, statuses, and more.", ConsoleColor.DarkGray);
                }

                string help = "[↑↓] navigate  [Enter] select  [Esc] close";
                win.At(barX, Draw.ScreenHeight - 2).Write(help, ConsoleColor.DarkGray);
                Draw.Blit();

                var key = Input.NextKey();

                if (key.Key == ConsoleKey.Escape) { if (query.Length > 0) { query = ""; cursor = 0; } else return; continue; }
                if (key.Key == ConsoleKey.Backspace) { if (query.Length > 0) { query = query[..^1]; cursor = 0; } continue; }
                if (key.Key == ConsoleKey.UpArrow || (key.Key == ConsoleKey.P && key.Modifiers.HasFlag(ConsoleModifiers.Control)))
                { cursor = Math.Max(0, cursor - 1); continue; }
                if (key.Key == ConsoleKey.DownArrow || (key.Key == ConsoleKey.N && key.Modifiers.HasFlag(ConsoleModifiers.Control)))
                { cursor++; continue; }
                if (key.Key == ConsoleKey.U && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                { query = ""; cursor = 0; continue; }
                if (key.Key == ConsoleKey.W && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    query = query.TrimEnd();
                    int last = query.LastIndexOf(' ');
                    query = last >= 0 ? query[..last] : "";
                    cursor = 0;
                    continue;
                }
                if (key.Key == ConsoleKey.Enter && matches.Count > 0)
                {
                    viewing = matches[cursor];
                    searching = false;
                    links = RichText.ExtractLinks(CachedDescription(viewing));
                    linkCursor = links.Count > 0 ? 0 : -1;
                    navStack = [viewing];
                    navPos = 0;
                    continue;
                }
                if (key.KeyChar >= ' ' && key.KeyChar <= '~') { query += key.KeyChar; cursor = 0; continue; }
            }
            else
            {
                string desc = CachedDescription(viewing!);
                RichText.Write(win, barX, SearchBarY + 2, barWidth, desc, linkCursor);

                string help = "[Tab] next link  [Enter] follow  [[] back  []] forward  [/] search  [Esc] back";
                if (linkCursor >= 0 && linkCursor < links.Count)
                    win.At(barX, Draw.ScreenHeight - 3).Write(links[linkCursor], ConsoleColor.Cyan);
                win.At(barX, Draw.ScreenHeight - 2).Write(help, ConsoleColor.DarkGray);
                Draw.Blit();

                var key = Input.NextKey();
                if (key.Key == ConsoleKey.Escape) { searching = true; linkCursor = -1; continue; }
                if (key.KeyChar == '/') { searching = true; linkCursor = -1; continue; }
                if (key.Key == ConsoleKey.Tab && links.Count > 0)
                {
                    bool shift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
                    linkCursor = shift
                        ? (linkCursor - 1 + links.Count) % links.Count
                        : (linkCursor + 1) % links.Count;
                    continue;
                }
                if (key.Key == ConsoleKey.Enter && linkCursor >= 0 && linkCursor < links.Count)
                {
                    string target = links[linkCursor];
                    var found = entries.FirstOrDefault(e => NameOf(e).Equals(target, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        // trim forward history
                        if (navPos < navStack.Count - 1)
                            navStack.RemoveRange(navPos + 1, navStack.Count - navPos - 1);
                        navStack.Add(found);
                        navPos = navStack.Count - 1;
                        viewing = found;
                        links = RichText.ExtractLinks(CachedDescription(viewing));
                        linkCursor = links.Count > 0 ? 0 : -1;
                    }
                    continue;
                }
                if (key.KeyChar == '[' && navPos > 0)
                {
                    navPos--;
                    viewing = navStack[navPos];
                    links = RichText.ExtractLinks(CachedDescription(viewing));
                    linkCursor = links.Count > 0 ? 0 : -1;
                    continue;
                }
                if (key.KeyChar == ']' && navPos < navStack.Count - 1)
                {
                    navPos++;
                    viewing = navStack[navPos];
                    links = RichText.ExtractLinks(CachedDescription(viewing));
                    linkCursor = links.Count > 0 ? 0 : -1;
                    continue;
                }
            }
        }
    }

    static string DescribeEntry(object entry) => entry switch
    {
        MonsterDef m => DescribeMonster(m),
        WeaponDef w => DescribeWeapon(w),
        ArmorDef a => DescribeArmor(a),
        ItemDef i => DescribeItem(i),
        SpellBrickBase s => DescribeSpell(s),
        MechanicsPage p => DescribeMechanics(p),
        DeityDef d => DescribeDeity(d),
        AncestryDef a => DescribeAncestry(a),
        FeatDef f => DescribeFeat(f),
        _ => "",
    };

    static string DescribeMonster(MonsterDef m)
    {
        StringBuilder sb = new();
        sb.AppendLine($"[fg=Yellow]{m.Name}[/]  [fg=Cyan]({m.CreatureType}, {m.Family.Name})[/]");
        sb.AppendLine($"CR {m.BaseLevel}  {m.Size}  {m.Glyph.Value}");
        sb.AppendLine($"AC {m.AC.Combined:+#;-#;+0}  AB {m.AttackBonus:+#;-#;+0}  HP/lvl {m.HpPerLevel}");
        if (m.Unarmed != null)
            sb.AppendLine($"Unarmed: {m.Unarmed.Name} {m.Unarmed.BaseDamage} {m.Unarmed.DamageType.SubCat}");

        bool hasWeaponOrNatural = false;
        foreach (var comp in m.Components)
        {
            if (comp is GrantAction ga && (ga.Action is AttackWithWeapon || (ga.Action is NaturalAttack n && n.Weapon != m.Unarmed)))
            { hasWeaponOrNatural = true; break; }
        }

        sb.AppendLine();
        foreach (var comp in m.Components)
        {
            switch (comp)
            {
                case GrantAction { Action: AttackWithWeapon }:
                    sb.AppendLine($"Melee weapon {Pokedex.SignedBonus(m.AttackBonus)}{Pokedex.Bonus(m.DamageBonus, " damage")}");
                    break;
                case GrantAction { Action: NaturalAttack nat }:
                    if (nat.Weapon == m.Unarmed && hasWeaponOrNatural) break;
                    sb.AppendLine($"Melee {nat.Weapon.Name} {Pokedex.SignedBonus(m.AttackBonus)}, Damage {nat.Weapon.BaseDamage}{Pokedex.Bonus(m.DamageBonus)} {nat.Weapon.DamageType.SubCat}");
                    break;
                case GrantAction { Action: FullAttack } ga:
                    sb.AppendLine($"Full attack: {ga.Action.Name}");
                    break;
                case GrantAction ga:
                    sb.AppendLine($"  {ga.Action.Name}");
                    break;
                case GrantSpell gs:
                    sb.AppendLine($"  [fg=Cyan]Spell: {gs.Spell.Name} (L{gs.Spell.Level})[/]");
                    break;
                case GrantPool:
                    break;
                default:
                    if (comp.PokedexDescription != null)
                        sb.AppendLine($"  {comp.PokedexDescription}");
                    break;
            }
        }

        sb.AppendLine();
        sb.AppendLine($"[fg=DarkGray]Max depth {m.MaxDepth}  Spawn weight {m.SpawnWeight}[/]");
        if (m.PokedexDescription != null)
        {
            sb.AppendLine();
            sb.Append(m.PokedexDescription);
        }
        return sb.ToString();
    }

    static string DescribeWeapon(WeaponDef w)
    {
        StringBuilder sb = new();
        sb.AppendLine($"[fg=Yellow]{w.Name}[/]  [fg=Cyan](weapon)[/]");
        string hands = w.Hands == 1 ? "One-handed" : "Two-handed";
        sb.AppendLine($"{hands} {w.DamageType.SubCat}");
        sb.AppendLine($"Damage: {w.BaseDamage}");
        if (w.Reach > 1) sb.AppendLine($"Reach: {w.Reach}");
        if (w.Launcher != null) sb.AppendLine("Throwable");
        sb.AppendLine($"[fg=DarkGray]Weight {w.Weight}  Price {w.Price}[/]");
        if (w.PokedexDescription != null)
        {
            sb.AppendLine();
            sb.Append(w.PokedexDescription);
        }
        return sb.ToString();
    }

    static string DescribeArmor(ArmorDef a)
    {
        StringBuilder sb = new();
        sb.AppendLine($"[fg=Yellow]{a.Name}[/]  [fg=Cyan](armor)[/]");
        sb.AppendLine($"AC bonus: +{a.ACBonus}");
        if (a.DexCap < 99) sb.AppendLine($"Dex cap: {a.DexCap}");
        if (a.CheckPenalty != 0) sb.AppendLine($"Check penalty: {a.CheckPenalty}");
        sb.AppendLine($"[fg=DarkGray]Weight {a.Weight}  Price {a.Price}[/]");
        if (a.PokedexDescription != null)
        {
            sb.AppendLine();
            sb.Append(a.PokedexDescription);
        }
        return sb.ToString();
    }

    static string DescribeItem(ItemDef i)
    {
        StringBuilder sb = new();
        sb.AppendLine($"[fg=Yellow]{i.Name}[/]");
        sb.AppendLine($"[fg=DarkGray]Weight {i.Weight}  Price {i.Price}[/]");
        if (i.PokedexDescription != null)
        {
            sb.AppendLine();
            sb.AppendLine(i.PokedexDescription);
        }
        if (i is BottleDef b)
        {
            sb.AppendLine();
            sb.Append(b.Spell.Description);
        }
        if (i is WandDef w)
        {
            sb.AppendLine();
            sb.Append(w.Spell.Description);
        }
        return sb.ToString();
    }

    static string DescribeSpell(SpellBrickBase s)
    {
        StringBuilder sb = new();
        sb.AppendLine($"[fg=Yellow]{s.Name}[/]  [fg=Cyan](level {s.Level} spell)[/]");
        sb.AppendLine();
        sb.Append(s.Description);
        return sb.ToString();
    }

    static string DescribeMechanics(MechanicsPage p)
    {
        StringBuilder sb = new();
        sb.AppendLine($"[fg=Yellow]{p.Name}[/]  [fg=Cyan](mechanics)[/]");
        sb.AppendLine();
        sb.Append(p.Description);
        return sb.ToString();
    }

    static string DescribeDeity(DeityDef d)
    {
        StringBuilder sb = new();
        string alignColor = d.Moral == MoralAxis.Good ? "green" : d.Moral == MoralAxis.Evil ? "red" : "gray";
        sb.AppendLine($"[fg=Yellow]{d.Name}[/]  [fg=Cyan](deity)[/]");
        sb.AppendLine($"[fg={alignColor}]{d.Alignment}[/]  Aspects: {string.Join(", ", d.Aspects)}");
        sb.AppendLine();
        sb.AppendLine($"[fg=White]Favoured weapon: [link={d.FavoredWeapon}]{d.FavoredWeapon}[/][/]");
        sb.AppendLine();
        sb.Append(d.Description);
        return sb.ToString();
    }

    static string DescribeAncestry(AncestryDef a)
    {
        StringBuilder sb = new();
        sb.AppendLine($"[fg=Yellow]{a.Name}[/]  [fg=Cyan](ancestry)[/]");
        if (a.Boosts.Length > 0)
            sb.AppendLine($"[fg=Green]Boosts: {string.Join(", ", a.Boosts)}[/]");
        if (a.Flaws.Length > 0)
            sb.AppendLine($"[fg=Red]Flaws: {string.Join(", ", a.Flaws)}[/]");
        if (a.Size != UnitSize.Medium)
            sb.AppendLine($"Size: {a.Size}");
        if (a.Speed != 25)
            sb.AppendLine($"Speed: {a.Speed}");
        sb.AppendLine();
        sb.Append(a.Description);
        return sb.ToString();
    }

    static string DescribeFeat(FeatDef f)
    {
        string cat = f.Type switch
        {
            FeatType.Class => "class feat",
            FeatType.General => "general feat",
            FeatType.Ancestry => "ancestry feat",
            _ => "feat",
        };
        StringBuilder sb = new();
        sb.AppendLine($"[fg=Yellow]{f.Name}[/]  [fg=Cyan]({cat}, level {f.Level})[/]");
        foreach (string tag in f.TagArray)
            sb.AppendLine($"[fg=DarkGray]{tag}[/]");
        sb.AppendLine();
        sb.Append(f.Description);
        return sb.ToString();
    }
}
