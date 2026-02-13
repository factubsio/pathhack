namespace Pathhack.UI;

public static class Pokedex
{
    public static void Farlook()
    {
        Pos cursor = upos;
        var monsters = lvl.LiveUnits.Where(m => !m.IsDead && lvl.IsVisible(m.Pos)).ToList();
        int monsterIdx = -1;

        var layer = Draw.Overlay;
        using var _ = layer.Activate();

        while (true)
        {
            Draw.ClearOverlay();
            Draw.OverlayWrite(cursor.X, cursor.Y + Draw.MapRow, "X", ConsoleColor.Yellow, ConsoleColor.Black, CellStyle.Bold);
            
            string desc = DescribeAt(cursor);
            Draw.OverlayWrite(0, Draw.MsgRow, desc.PadRight(Draw.ScreenWidth));
            
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

    static string DescribeAt(Pos p)
    {
        var unit = lvl.UnitAt(p);
        if (unit is Monster m && !m.IsPlayer)
        {
            switch (m.Perception)
            {
                case PlayerPerception.Visible:
                case PlayerPerception.Detected:
                case PlayerPerception.Warned:
                    return $"{m.Glyph.Value}  {m}";
                case PlayerPerception.Unease:
                    int warnLevel = Math.Clamp(m.EffectiveLevel / 4, 0, 5);
                    return $"{warnLevel}  {WarningDescs[warnLevel]}";
                case PlayerPerception.Guess:
                    return "?  something was here";
            }
        }
        
        if (unit != null && unit.IsPlayer) return "yourself";

        if (!lvl.IsVisible(p) && !lvl.WasSeen(p)) return "unexplored";

        var items = lvl.ItemsAt(p);
        if (items.Count > 0)
            return $"{items[^1].Def.Class}  {items[^1].Def.Name}" + (items.Count > 1 ? $" (and {items.Count - 1} more)" : "");

        if (lvl.Traps.TryGetValue(p, out var trap) && trap.PlayerSeen)
            return $"^  {trap.Type} trap";

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
            _ => "unknown"
        };
    }

    static void ShowMonsterEntry(Monster m)
    {
        var menu = new Menu();
        
        menu.Add($"{m.RealName,-24} Creature CR {m.Def.BaseLevel} {m.CreatureTypeRendered}", LineStyle.Heading);
        menu.Add($"{m.Def.Size}");
        menu.Add("");
        menu.Add($"AC {m.GetAC()}; HP {m.Def.HpPerLevel}");
        var speed = m.QueryModifiers("speed_bonus");
        Log.Write($"speed: {speed}");
        menu.Add($"Movement: {SpeedDesc(m.LandMove)}");
        menu.Add("");
        
        var grantedActions = m.LiveFacts
            .Where(f => f.Brick is GrantAction)
            .Select(f => ((GrantAction)f.Brick).Action)
            .ToList();
        
        bool hasWeaponOrNatural = grantedActions.Any(a => a is AttackWithWeapon || (a is NaturalAttack n && n.Weapon != m.Def.Unarmed));

        // attacks first
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

        // other actions
        foreach (var fact in m.LiveFacts.Where(f => f.Brick is GrantAction))
        {
            var grant = (GrantAction)fact.Brick;
            if (grant.Action is not AttackWithWeapon and not NaturalAttack)
                menu.Add($"  {grant.Action.Name}");
        }

        // passives
        foreach (var fact in m.LiveFacts.Where(f => f.Brick is not GrantAction && f.Brick.PokedexDescription != null))
            menu.Add($"  {fact.Brick.PokedexDescription}");

        // spells
        if (m.Spells.Count > 0)
        {
            menu.Add("");
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

        // active buffs
        bool firstBuff = true;
        foreach (var buff in m.LiveFacts.Where(f => f.Brick.IsBuff))
        {
            if (firstBuff) { menu.Add(""); menu.Add("Active effects:"); firstBuff = false; }
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

    static string Bonus(int val, string suffix = "") =>
        val == 0 ? "" : $" {val:+#;-#}{suffix}";

    static string SignedBonus(int val) => $"{val:+#;-#;+0}";

    public static void ShowItemEntry(Item item)
    {
        var def = item.Def;
        var menu = new Menu();
        bool propsKnown = item.Knowledge.HasFlag(ItemKnowledge.Props);

        string equipped = item.Holder?.Equipped.ContainsValue(item) == true
            ? (def is ArmorDef ? " (being worn)" : " (weapon in hand)")
            : "";
        menu.Add($"{item.DisplayName}{equipped}", LineStyle.Heading);
        menu.Add("");

        if (def is WeaponDef wpn)
        {
            if (wpn.AltProficiencies?.Length > 0)
                menu.Add($"Group: {wpn.Profiency} [{string.Join(",", wpn.AltProficiencies)}]");
            else
                menu.Add($"Group: {wpn.Profiency}");
            var (prof, profSource) = u.GetProficiency(wpn);
            ConsoleColor? profColor = prof == ProficiencyLevel.Untrained ? ConsoleColor.Red : null;
            menu.Add($"Proficiency: {prof} ({profSource})", color: profColor);

            string hands = wpn.Hands == 1 ? "One-handed" : "Two-handed";
            menu.Add($"{hands} {wpn.DamageType.SubCat} weapon.");
            menu.Add($"Base damage: {wpn.BaseDamage}");

            if (propsKnown)
            {
                menu.Add($"Potency: {item.Potency}");
                
                if (item.Fundamental is { } fund)
                {
                    if (fund.Def.IsNull)
                        menu.Add("Fundamental: [blocked]");
                    else
                        menu.Add($"Fundamental: {fund.Def.DisplayName}, {fund.Def.Description}");
                }
                else
                    menu.Add("Fundamental: [empty]");
                
                menu.Add($"Property slots: {item.PropertyRunes.Count}/{item.Potency}");
                foreach (var rune in item.PropertyRunes)
                    menu.Add($"  - {rune.Def.DisplayName}, {rune.Def.Description}");
                for (int i = item.PropertyRunes.Count; i < item.Potency; i++)
                    menu.Add("  - [empty]");
            }
        }
        else if (def is ArmorDef armor)
        {
            var prof = u.GetProficiency(armor.Proficiency);
            ConsoleColor? profColor = prof == ProficiencyLevel.Untrained ? ConsoleColor.Red : null;
            menu.Add($"Proficiency: {prof} ({armor.Proficiency})", color: profColor);
            menu.Add($"Armor. AC bonus: {armor.ACBonus}");

            if (propsKnown)
            {
                if (armor.DexCap < 99)
                    menu.Add($"Dex cap: {armor.DexCap}");
            }
        }

        menu.Add("");
        menu.Add($"Weighs {def.Weight}. Made of {item.Material}.");

        // Description: only show when identified
        if (!propsKnown)
        {
            menu.Add("Properties not identified.", color: ConsoleColor.DarkYellow);
        }
        else if (def.IsKnown())
        {
            if (def.PokedexDescription != null)
            {
                menu.Add("");
                menu.Add(def.PokedexDescription);
            }
            else
            {
                foreach (var brick in def.Components.Where(b => b.PokedexDescription != null))
                {
                    menu.Add("");
                    menu.Add(brick.PokedexDescription!);
                }
            }
        }

        menu.Display();
    }
}
