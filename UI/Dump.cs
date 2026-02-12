using System.Text.Json;

namespace Pathhack.UI;

public static class Dump
{
    public static void DumpLog()
    {
        Level level = g.CurrentLevel!;
        int w = level.Width;
        int h = level.Height;

        // Build cell grid — full reveal, no FOV
        int[][] chars = new int[h][];
        int[][] colors = new int[h][];
        int[][] tips = new int[h][];
        Dictionary<string, int> tipIndex = new();
        List<string> tipTable = [];

        for (int y = 0; y < h; y++)
        {
            chars[y] = new int[w];
            colors[y] = new int[w];
            tips[y] = new int[w]; // 0 = no tip

            for (int x = 0; x < w; x++)
            {
                Pos p = new(x, y);
                Cell cell = ResolveCell(level, p);
                chars[y][x] = cell.Ch;
                colors[y][x] = (int)cell.Fg;
                string? desc = DescribeCell(level, p);
                if (desc != null)
                {
                    if (!tipIndex.TryGetValue(desc, out int idx))
                    {
                        tipTable.Add(desc);
                        idx = tipTable.Count; // 1-based
                        tipIndex[desc] = idx;
                    }
                    tips[y][x] = idx;
                }
            }
        }

        // Recent messages
        List<string> messages = g.MessageHistory;
        int msgStart = Math.Max(0, messages.Count - 50);
        string[] recentMessages = messages.GetRange(msgStart, messages.Count - msgStart).ToArray();

        // Per-frame player state
        HungerState hunger = Hunger.GetState(u.Nutrition);
        var playerState = new
        {
            hp = u.HP.Current,
            maxHp = u.HP.Max,
            tempHp = u.TempHp,
            ac = u.GetAC(),
            cl = u.CharacterLevel,
            xp = u.XP,
            gold = u.Gold,
            str = u.Str, dex = u.Dex, con = u.Con,
            @int = u.Int, wis = u.Wis, cha = u.Cha,
            hunger = Hunger.GetLabel(hunger),
            buffs = u.LiveFacts
                .Where(f => f.Brick.BuffName != null)
                .Select(f => f.DisplayName)
                .ToArray(),
            spellSlots = GetSpellSlots(),
        };

        var frame = new
        {
            round = g.CurrentRound,
            width = w,
            height = h,
            chars,
            colors,
            tips,
            tipTable = tipTable.ToArray(),
            messages = recentMessages,
            player = playerState,
        };

        // Per-game data
        var inventory = u.Inventory.Select(i => $"{i.InvLet} - {i.RealName}").ToArray();
        var abilities = u.Actions.Select(a => a.Name).ToArray();
        var feats = u.TakenFeats.ToArray();
        var discoveries = ItemDb.Instance.IdentifiedDefs
            .Select(d => new { appearance = ItemDb.Instance.GetAppearance(d)?.Name, real = d.Name })
            .Where(d => d.appearance != null)
            .ToArray();

        // Pokedex: collect unique monsters and items on the level
        var monsterDex = CollectMonsters(level);
        var itemDex = CollectItems(level);

        var data = new
        {
            player = "player",
            @class = u.Class.Name,
            deity = u.Deity.Name,
            ancestry = u.Ancestry.Name,
            level = u.CharacterLevel,
            branch = level.Branch.Name,
            depth = level.EffectiveDepth,
            inventory,
            abilities,
            feats,
            discoveries,
            vanquished = g.Vanquished,
            monsters = monsterDex,
            items = itemDex,
            frames = new[] { frame },
        };

        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        string path = Path.Combine(Environment.CurrentDirectory, "dump.json");
        File.WriteAllText(path, json);
        g.pline($"Dumped to {path}");
    }

    static object[] GetSpellSlots()
    {
        List<object> slots = [];
        for (int lvl = 1; lvl <= 9; lvl++)
        {
            var pool = u.GetPool($"spell_l{lvl}");
            if (pool == null) continue;
            slots.Add(new { level = lvl, current = pool.Current, max = pool.Max, effectiveMax = pool.EffectiveMax, ticks = pool.Ticks, regenRate = pool.RegenRate });
        }
        return slots.ToArray();
    }

    static Cell ResolveCell(Level level, Pos p)
    {
        if (level.UnitAt(p) is { } unit)
            return Cell.From(unit.Glyph);
        var items = level.ItemsAt(p);
        if (items.Count > 0)
            return Cell.From(items[^1].Glyph);
        if (level.Traps.TryGetValue(p, out var trap))
            return Cell.From(trap.Glyph);
        if (level.GetState(p)?.Feature is { } feature && feature.Id[0] != '_')
            return new('_', ConsoleColor.DarkGreen);
        return TileCell(level, p);
    }

    static Cell TileCell(Level level, Pos p)
    {
        Tile tile = level[p];
        DoorState door = level.GetState(p)?.Door ?? DoorState.Closed;
        ConsoleColor fg = Draw.TileColor(level, tile.Type, door);
        if (tile.Type == TileType.BranchUp)
            fg = level.BranchUpTarget?.Branch.Color ?? ConsoleColor.Cyan;
        else if (tile.Type == TileType.BranchDown)
            fg = level.BranchDownTarget?.Branch.Color ?? ConsoleColor.Cyan;
        char ch = tile.Type switch
        {
            TileType.Floor => '.',
            TileType.Wall => '\x01',
            TileType.Rock => ' ',
            TileType.Corridor => '#',
            TileType.Door => door switch { DoorState.Open or DoorState.Broken => '.', _ => '+' },
            TileType.StairsUp or TileType.BranchUp => '<',
            TileType.StairsDown or TileType.BranchDown => '>',
            TileType.Grass => '.',
            TileType.Pool => '~',
            TileType.Water => '~',
            _ => '?',
        };
        return new(ch, fg);
    }

    static string? DescribeCell(Level level, Pos p)
    {
        if (level.UnitAt(p) is Monster m && !m.IsPlayer)
            return m.ToString();
        if (level.UnitAt(p) is { IsPlayer: true })
            return "you";
        var items = level.ItemsAt(p);
        if (items.Count > 0)
            return items[^1].Def.Name;
        if (level.Traps.TryGetValue(p, out var trap))
            return $"{trap.Type} trap";
        var tile = level[p];
        return tile.Type switch
        {
            TileType.StairsUp => "stairs up",
            TileType.StairsDown => "stairs down",
            TileType.BranchUp => $"stairs to {level.BranchUpTarget?.Branch.Name ?? "?"}",
            TileType.BranchDown => $"stairs to {level.BranchDownTarget?.Branch.Name ?? "?"}",
            _ => null,
        };
    }

    static Dictionary<string, object> CollectMonsters(Level level)
    {
        Dictionary<string, object> dex = [];
        foreach (var unit in level.LiveUnits)
        {
            if (unit is not Monster m || m.IsPlayer) continue;
            if (dex.ContainsKey(m.Def.Name)) continue;

            List<string> attacks = [];
            List<string> naturalAttacks = [];
            List<string> abilities = [];
            List<string> passives = [];

            bool hasWeaponOrNatural = m.LiveFacts
                .Where(f => f.Brick is GrantAction)
                .Select(f => ((GrantAction)f.Brick).Action)
                .Any(a => a is AttackWithWeapon || (a is NaturalAttack n && n.Weapon != m.Def.Unarmed));

            foreach (var fact in m.LiveFacts.Where(f => f.Brick is GrantAction))
            {
                var action = ((GrantAction)fact.Brick).Action;
                if (action is AttackWithWeapon)
                    attacks.Add($"weapon {Signed(m.Def.AttackBonus)}{BonusStr(m.Def.DamageBonus, " damage")}");
                else if (action is NaturalAttack nat)
                {
                    if (nat.Weapon == m.Def.Unarmed && hasWeaponOrNatural) continue;
                    naturalAttacks.Add($"{nat.Weapon.Name} {Signed(m.Def.AttackBonus)}, {nat.Weapon.BaseDamage} {nat.Weapon.DamageType.SubCat}");
                }
                else
                    abilities.Add(action.Name);
            }

            foreach (var fact in m.LiveFacts.Where(f => f.Brick is not GrantAction && f.Brick.PokedexDescription != null))
                passives.Add(fact.Brick.PokedexDescription!);

            dex[m.Def.Name] = new
            {
                cr = m.Def.BaseLevel,
                size = m.Def.Size.ToString(),
                type = m.CreatureTypeRendered,
                ac = m.GetAC(),
                hp = m.Def.HpPerLevel,
                speed = m.LandMove.Value,
                attacks,
                naturalAttacks,
                abilities,
                passives,
            };
        }
        return dex;
    }

    static Dictionary<string, object> CollectItems(Level level)
    {
        Dictionary<string, object> dex = [];
        // Floor items
        for (int y = 0; y < level.Height; y++)
        for (int x = 0; x < level.Width; x++)
            foreach (var item in level.ItemsAt(new(x, y)))
                AddItemDex(dex, item);
        // Monster inventories
        foreach (var unit in level.LiveUnits)
            if (unit is Monster m)
                foreach (var item in m.Inventory)
                    AddItemDex(dex, item);
        return dex;
    }

    static void AddItemDex(Dictionary<string, object> dex, Item item)
    {
        if (dex.ContainsKey(item.Def.Name)) return;
        var def = item.Def;

        // Collect brick descriptions
        var descs = def.Components
            .Where(b => b.PokedexDescription != null)
            .Select(b => b.PokedexDescription!)
            .ToArray();

        object entry = def switch
        {
            WeaponDef wpn => new
            {
                type = "weapon",
                damage = wpn.BaseDamage.ToString(),
                damageType = wpn.DamageType.SubCat,
                hands = wpn.Hands,
                group = wpn.Profiency,
                weight = def.Weight,
                material = item.Material.ToString(),
                description = def.PokedexDescription,
                effects = descs,
            },
            ArmorDef armor => new
            {
                type = "armor",
                ac = armor.ACBonus,
                dexCap = armor.DexCap < 99 ? armor.DexCap : (int?)null,
                proficiency = armor.Proficiency,
                weight = def.Weight,
                material = item.Material.ToString(),
                description = def.PokedexDescription,
                effects = descs,
            },
            _ => (object)new
            {
                type = def.Class.ToString(),
                weight = def.Weight,
                description = def.PokedexDescription,
                effects = descs,
            },
        };
        dex[def.Name] = entry;
    }

    static string Signed(int val) => $"{val:+#;-#;+0}";
    static string BonusStr(int val, string suffix) => val == 0 ? "" : $" {val:+#;-#}{suffix}";
}
