using Pathhack.Core;
using Pathhack.Game;
using Pathhack.Map;

namespace Pathhack.UI;

public static class Pokedex
{
    public static void Farlook()
    {
        Pos cursor = upos;
        var monsters = lvl.Units.Where(m => lvl.IsVisible(m.Pos)).ToList();
        int monsterIdx = -1;

        var layer = Draw.Overlay;
        using var _ = layer.Activate();

        while (true)
        {
            Draw.ClearOverlay();
            Draw.OverlayWrite(cursor.X, cursor.Y + Draw.MapRow, "X", ConsoleColor.Yellow, ConsoleColor.Black, CellStyle.Bold);
            
            string desc = DescribeAt(cursor);
            Draw.OverlayWrite(0, Draw.MsgRow, desc.PadRight(Draw.ViewWidth));
            
            Draw.Blit();
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape) break;

            if (key.KeyChar is '.' or ',' or ';' or ':')
            {
                var unit = lvl.UnitAt(cursor);
                if (unit is Monster m)
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

    static string DescribeAt(Pos p)
    {
        if (!lvl.IsVisible(p) && !lvl.WasSeen(p)) return "unexplored";
        
        var unit = lvl.UnitAt(p);
        if (unit != null)
        {
            if (unit.IsPlayer) return "yourself";
            return $"{unit.Glyph.Value}  {unit}";
        }

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
        var def = m.Def;
        var menu = new Menu();
        
        menu.Add($"{def.Name,-40} Creature CR {def.CR}", LineStyle.Heading);
        menu.Add($"{def.Size}");
        menu.Add("");
        menu.Add($"AC {def.AC}; HP {def.HP}");
        menu.Add($"Movement: {SpeedDesc(def.LandMove)}");
        menu.Add("");
        
        foreach (var grant in def.Components.OfType<GrantAction>())
        {
            if (grant.Action is AttackWithWeapon)
                menu.Add($"Melee weapon +{def.AttackBonus}{Bonus(def.DamageBonus, " damage")}");
            else if (grant.Action is NaturalAttack nat)
                menu.Add($"Melee {nat.Weapon.Name} +{def.AttackBonus}, Damage {nat.Weapon.BaseDamage}{Bonus(def.DamageBonus)} {nat.Weapon.DamageType.SubCat}");
            else
                menu.Add($"  {grant.Action.Name}");
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
        val == 0 ? "" : $"{val:+#;-#}{suffix}";

    public static void ShowItemEntry(Item item)
    {
        var def = item.Def;
        var menu = new Menu();

        string equipped = item.Holder?.Equipped.ContainsValue(item) == true
            ? (def is ArmorDef ? " (being worn)" : " (weapon in hand)")
            : "";
        menu.Add($"{item.DisplayName}{equipped}", LineStyle.Heading);
        menu.Add("");

        if (def is WeaponDef wpn)
        {
            string hands = wpn.Hands == 1 ? "One-handed" : "Two-handed";
            menu.Add($"{hands} {wpn.DamageType.SubCat} weapon.");
            menu.Add($"Base damage: {wpn.BaseDamage}");
            menu.Add($"Potency: {item.Potency}");
            
            // fundamental rune
            if (item.Fundamental is { } fund)
            {
                if (fund.Def.IsNull)
                    menu.Add("Fundamental: [blocked]");
                else
                    menu.Add($"Fundamental: {fund.Def.DisplayName}, {fund.Def.Description}");
            }
            else
                menu.Add("Fundamental: [empty]");
            
            // property slots
            menu.Add($"Property slots: {item.PropertyRunes.Count}/{item.Potency}");
            foreach (var rune in item.PropertyRunes)
                menu.Add($"  - {rune.Def.DisplayName}, {rune.Def.Description}");
            for (int i = item.PropertyRunes.Count; i < item.Potency; i++)
                menu.Add("  - [empty]");
        }
        else if (def is ArmorDef armor)
        {
            menu.Add($"Armor. AC bonus: {armor.ACBonus}");
            if (armor.DexCap < 99)
                menu.Add($"Dex cap: {armor.DexCap}");
        }

        menu.Add("");
        menu.Add($"Weighs {def.Weight}. Made of {def.Material}.");

        menu.Display();
    }
}
