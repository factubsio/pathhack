using System.Text.Json;

namespace Pathhack.UI;

public static class Dump
{
    public static void DumpLog()
    {
        // Capture final frame if not already recorded this round
        BlackBox.Record();

        Level level = g.CurrentLevel!;
        Snapshot[] snapshots = BlackBox.Drain();

        // Build global tip table across all frames
        Dictionary<string, int> tipIndex = new();
        List<string> tipTable = [];

        var frames = snapshots.Select(snap =>
        {
            int[][] tips = new int[snap.Height][];
            for (int y = 0; y < snap.Height; y++)
            {
                tips[y] = new int[snap.Width];
                for (int x = 0; x < snap.Width; x++)
                {
                    string? desc = snap.Tips[y][x];
                    if (desc != null)
                    {
                        if (!tipIndex.TryGetValue(desc, out int idx))
                        {
                            tipTable.Add(desc);
                            idx = tipTable.Count;
                            tipIndex[desc] = idx;
                        }
                        tips[y][x] = idx;
                    }
                }
            }

            return new
            {
                round = snap.Round,
                width = snap.Width,
                height = snap.Height,
                chars = snap.Chars,
                colors = snap.Colors,
                vis = snap.Vis,
                tips,
                messages = snap.Messages,
                player = new
                {
                    hp = snap.Hp, maxHp = snap.MaxHp, tempHp = snap.TempHp,
                    ac = snap.AC, cl = snap.CL, xp = snap.XP, gold = snap.Gold,
                    str = snap.Str, dex = snap.Dex, con = snap.Con,
                    @int = snap.Int, wis = snap.Wis, cha = snap.Cha,
                    hunger = snap.Hunger,
                    buffs = snap.Buffs,
                    spellSlots = snap.SpellSlots.Select(s => new
                    {
                        level = s.Level, current = s.Current, max = s.Max,
                        effectiveMax = s.EffectiveMax, ticks = s.Ticks, regenRate = s.RegenRate,
                    }).ToArray(),
                },
            };
        }).ToArray();

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
            tipTable = tipTable.ToArray(),
            frames,
        };

        string json = JsonSerializer.Serialize(data);
        string path = Path.Combine(Environment.CurrentDirectory, "dump.json");
        File.WriteAllText(path, json);

        // Also produce self-contained HTML
        string? htmlPath = BundleHtml(json);

        g.pline(htmlPath != null ? $"Dumped to {htmlPath}" : $"Dumped to {path}");
    }

    static string? BundleHtml(string json)
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        string? html = ReadResource(asm, "dump.html");
        string? js = ReadResource(asm, "dump-view.js");
        if (html == null || js == null) return null;

        html = html.Replace("/*__DUMP_DATA__*/", $"const DATA = {json};");
        html = html.Replace("<script type=\"module\" src=\"dump-view.js\"></script>", $"<script>{js}</script>");

        string path = Path.Combine(Environment.CurrentDirectory, "dump.html");
        File.WriteAllText(path, html);
        return path;
    }

    static string? ReadResource(System.Reflection.Assembly asm, string name)
    {
        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static Cell ResolveCell(Level level, Pos p)
    {
        if (level.UnitAt(p) is { } unit)
            return Cell.From(unit.Glyph);
        var items = level.ItemsAt(p);
        if (items.Count > 0)
            return Cell.From(items[^1].Glyph);
        if (level.Traps.TryGetValue(p, out var trap))
            return Cell.From(trap.Glyph);
        if (level.GetState(p)?.Feature is { } feature && !feature.Hidden)
            return feature.Glyph is { } g ? Cell.From(g) : new('_', ConsoleColor.DarkGreen);
        return TileCell(level, p);
    }

    internal static bool IsWallLike(Level level, Pos p)
    {
        var type = level[p].Type;
        if (type == TileType.Wall) return true;
        if (type == TileType.Door)
        {
            var door = level.GetState(p)?.Door ?? DoorState.Closed;
            return door is DoorState.Closed or DoorState.Locked;
        }
        return false;
    }

    // FOV-aware cell resolution: returns (cell, visibility) where vis: 0=unseen, 1=memory, 2=visible
    internal static (Cell Cell, int Vis) ResolveCellFov(Level level, Pos p)
    {
        if (level.IsVisible(p) || p == upos)
            return (ResolveCell(level, p), 2);

        if (level.WasSeen(p) && level.GetMemory(p) is { } mem)
        {
            Cell cell;
            if (mem.TopItem is { } item && !mem.Tile.IsStairs)
                cell = new(item.Glyph.Value, item.Glyph.Color);
            else if (mem.Trap is { } trap)
                cell = new(trap.Glyph.Value, trap.Glyph.Color);
            else
            {
                ConsoleColor fg = mem.Tile.Type == TileType.Wall
                    ? level.WallColor ?? ConsoleColor.Gray
                    : ConsoleColor.DarkBlue;
                cell = TileCell(level, p, mem.Tile.Type, mem.Door, fg);
            }
            return (cell, 1);
        }

        return (Cell.Empty, 0);
    }

    static Cell TileCell(Level level, Pos p, TileType type, DoorState door, ConsoleColor fg)
    {
        char ch = type switch
        {
            TileType.Floor => '.',
            TileType.Wall => '\x01',
            TileType.Rock => ' ',
            TileType.Corridor => '#',
            TileType.Door => door switch { DoorState.Open or DoorState.Broken => '.', _ => '+' },
            TileType.StairsUp or TileType.BranchUp => '<',
            TileType.StairsDown or TileType.BranchDown => '>',
            TileType.Grass => '.',
            TileType.Tree => '±',
            TileType.Pool => '~',
            TileType.Water => '~',
            _ => '?',
        };
        return new(ch, fg);
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
        return TileCell(level, p, tile.Type, door, fg);
    }

    internal static string? DescribeCell(Level level, Pos p)
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
