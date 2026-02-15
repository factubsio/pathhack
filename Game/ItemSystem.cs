namespace Pathhack.Game;

public enum BUC { Cursed = -1, Uncursed = 0, Blessed = 1 }

[Flags]
public enum ItemKnowledge { None = 0, Seen = 1, BUC = 4, PropRunes = 8, PropQuality = 16, PropPotency = 32, Props = PropRunes | PropQuality | PropPotency }

public enum AppearanceCategory { Potion, Scroll, Amulet, Boots, Gloves, Cloak, Ring, Bottle, Wand }

public record Appearance(string Name, ConsoleColor Color, string? Material = null);

public class ItemDb
{
    public static ItemDb Instance { get; private set; } = new();

    static readonly Dictionary<AppearanceCategory, Appearance[]> Pools = new()
    {
        [AppearanceCategory.Potion] = [
            new("milky potion", ConsoleColor.White),
            new("fizzy potion", ConsoleColor.Yellow),
            new("murky potion", ConsoleColor.DarkGray),
            new("bubbly potion", ConsoleColor.Cyan),
            new("smoky potion", ConsoleColor.Gray),
            new("glowing potion", ConsoleColor.Magenta),
            new("viscous potion", ConsoleColor.DarkGreen),
            new("oily potion", ConsoleColor.DarkYellow),
        ],
        [AppearanceCategory.Bottle] = [
            new("luminous bottle", ConsoleColor.Yellow),
            new("cloudy bottle", ConsoleColor.Gray),
            new("shimmering bottle", ConsoleColor.Cyan),
            new("dark bottle", ConsoleColor.DarkMagenta),
            new("warm bottle", ConsoleColor.DarkRed),
            new("humming bottle", ConsoleColor.Green),
            new("frosted bottle", ConsoleColor.White),
            new("sparkling bottle", ConsoleColor.DarkYellow),
        ],
        [AppearanceCategory.Wand] = [
            new("glass wand", ConsoleColor.White),
            new("balsa wand", ConsoleColor.DarkYellow),
            new("crystal wand", ConsoleColor.Cyan),
            new("maple wand", ConsoleColor.DarkYellow),
            new("oak wand", ConsoleColor.DarkYellow),
            new("ebony wand", ConsoleColor.DarkGray),
            new("marble wand", ConsoleColor.White),
            new("tin wand", ConsoleColor.Gray),
            new("brass wand", ConsoleColor.Yellow),
            new("copper wand", ConsoleColor.DarkYellow),
            new("silver wand", ConsoleColor.White),
            new("platinum wand", ConsoleColor.White),
            new("iridium wand", ConsoleColor.Cyan),
            new("zinc wand", ConsoleColor.Gray),
            new("iron wand", ConsoleColor.DarkGray),
            new("steel wand", ConsoleColor.Gray),
            new("hexagonal wand", ConsoleColor.DarkGray),
            new("runed wand", ConsoleColor.DarkGray),
            new("curved wand", ConsoleColor.DarkGray),
            new("forked wand", ConsoleColor.DarkYellow),
            new("jeweled wand", ConsoleColor.Magenta),
            new("ceramic wand", ConsoleColor.DarkCyan),
            new("pine wand", ConsoleColor.DarkYellow),
            new("spiked wand", ConsoleColor.DarkGray),
        ],
        [AppearanceCategory.Scroll] = [
            new("scroll labelled ZELGO MOR", ConsoleColor.White),
            new("scroll labelled FOOBIE BLETCH", ConsoleColor.White),
            new("scroll labelled TEMOV", ConsoleColor.White),
            new("scroll labelled GARVEN DEH", ConsoleColor.White),
            new("scroll labelled READ ME", ConsoleColor.White),
            new("scroll labelled XIXAXA XOXAXA", ConsoleColor.White),
            new("scroll labelled PRATYAVAYAH", ConsoleColor.White),
            new("scroll labelled ELBIB YLOH", ConsoleColor.White),
        ],
        [AppearanceCategory.Amulet] = [
            new("circular amulet", ConsoleColor.Gray),
            new("jade amulet", ConsoleColor.Green),
            new("spherical amulet", ConsoleColor.Cyan),
            new("triangular amulet", ConsoleColor.Yellow),
            new("copper amulet", ConsoleColor.DarkYellow),
            new("silver amulet", ConsoleColor.White),
        ],
        [AppearanceCategory.Boots] = [
            new("leather boots", ConsoleColor.DarkYellow),
            new("riding boots", ConsoleColor.Gray),
            new("fur-lined boots", ConsoleColor.White),
            new("combat boots", ConsoleColor.DarkGray),
            new("mud boots", ConsoleColor.DarkYellow),
            new("jackboots", ConsoleColor.DarkRed),
            new("hiking boots", ConsoleColor.Green),
            new("iron-shod boots", ConsoleColor.DarkGray, "iron"),
        ],
        [AppearanceCategory.Gloves] = [
            new("leather gloves", ConsoleColor.DarkYellow),
            new("padded gloves", ConsoleColor.Gray),
            new("riding gloves", ConsoleColor.White),
            new("gauntlets", ConsoleColor.DarkGray),
            new("fencing gloves", ConsoleColor.DarkCyan),
            new("iron gauntlets", ConsoleColor.DarkGray, "iron"),
            new("silk gloves", ConsoleColor.Magenta),
            new("fingerless gloves", ConsoleColor.DarkYellow),
        ],
        [AppearanceCategory.Cloak] = [
            new("tattered cloak", ConsoleColor.DarkGray),
            new("opera cloak", ConsoleColor.Red),
            new("ornate cloak", ConsoleColor.Magenta),
            new("faded cloak", ConsoleColor.Gray),
        ],
        [AppearanceCategory.Ring] = [
            new("iron ring", ConsoleColor.DarkGray, "iron"),
            new("silver ring", ConsoleColor.White, "silver"),
            new("gold ring", ConsoleColor.Yellow, "gold"),
            new("copper ring", ConsoleColor.DarkYellow, "copper"),
            new("jade ring", ConsoleColor.Green, "jade"),
            new("ruby ring", ConsoleColor.Red, "ruby"),
            new("sapphire ring", ConsoleColor.Blue, "sapphire"),
            new("opal ring", ConsoleColor.Cyan, "opal"),
            new("obsidian ring", ConsoleColor.DarkGray, "obsidian"),
            new("pearl ring", ConsoleColor.White, "pearl"),
            new("amethyst ring", ConsoleColor.Magenta, "amethyst"),
            new("onyx ring", ConsoleColor.DarkGray, "onyx"),
            new("topaz ring", ConsoleColor.Yellow, "topaz"),
            new("emerald ring", ConsoleColor.Green, "emerald"),
            new("bone ring", ConsoleColor.White, "bone"),
            new("bronze ring", ConsoleColor.DarkYellow, "bronze"),
            new("moonstone ring", ConsoleColor.Cyan, "moonstone"),
            new("granite ring", ConsoleColor.Gray, "granite"),
            new("coral ring", ConsoleColor.Red, "coral"),
            new("ivory ring", ConsoleColor.White, "ivory"),
            new("tin ring", ConsoleColor.Gray, "tin"),
            new("brass ring", ConsoleColor.DarkYellow, "brass"),
            new("wooden ring", ConsoleColor.DarkYellow, "wood"),
            new("agate ring", ConsoleColor.DarkRed, "agate"),
        ],
    };

    readonly Dictionary<AppearanceCategory, int[]> _shuffled = [];
    readonly HashSet<ItemDef> _identified = [];
    readonly Dictionary<ItemDef, string> _called = [];

    public void Initialize(int gameSeed)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{gameSeed}:appearances"));
        var rng = new Random(BitConverter.ToInt32(bytes, 0));
        _shuffled.Clear();
        _identified.Clear();
        _called.Clear();
        foreach (var (cat, pool) in Pools)
        {
            var indices = Enumerable.Range(0, pool.Length).ToArray();
            rng.Shuffle(indices);
            _shuffled[cat] = indices;
        }
    }

    public Appearance? GetAppearance(ItemDef def)
    {
        if (def.AppearanceCategory is not { } cat) return null;
        if (def.AppearanceIndex < 0) return null;
        if (!_shuffled.TryGetValue(cat, out var indices)) return null;
        var pool = Pools[cat];
        var idx = indices[def.AppearanceIndex % indices.Length];
        return pool[idx];
    }

    public bool IsIdentified(ItemDef def) => def.AppearanceCategory == null || _identified.Contains(def);
    public void Identify(ItemDef def) => _identified.Add(def);
    public IEnumerable<ItemDef> IdentifiedDefs => _identified;
    
    public string? GetCalledName(ItemDef def) => _called.GetValueOrDefault(def);
    public void SetCalledName(ItemDef def, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            _called.Remove(def);
        else
            _called[def] = name;
    }

    public static void Reset(int seed)
    {
        Instance = new ItemDb();
        Instance.Initialize(seed);
    }
}

public class ItemDef : BaseDef
{
    public string Name = "";
    public Glyph Glyph = new(']', ConsoleColor.White);
    public string DefaultEquipSlot = ItemSlots.None;
    public int Weight = 1;
    public string Material = Materials.Iron;
    public bool Stackable;
    public bool IsUnique = false;
    public string? PokedexDescription;

    // ID system - set for magical consumables/accessories
    public AppearanceCategory? AppearanceCategory;
    public int AppearanceIndex = -1;
    public required int Price;
    public bool CanHavePotency;
    public int BUCBias; // -2=always cursed, -1=90% cursed, 0=normal, +1=90% blessed, +2=always blessed

    public char Class => Glyph.Value;
    public virtual ItemKnowledge RelevantKnowledge => ItemKnowledge.Seen;
}

public enum RuneSlot { Fundamental, Property }

public abstract class RuneBrick(string displayName, int quality, RuneSlot slot) : LogicBrick
{
    public string DisplayName => displayName;
    public int Quality => quality;
    public RuneSlot Slot => slot;
    public bool IsNull => this == NullFundamental.Instance;
    public virtual string Description => "";
}

public enum WeaponCategory { Unarmed, Natural, Item }

public class WeaponDef : ItemDef
{
    public required DiceFormula BaseDamage;
    public required string Profiency; // weapon group
    public required DamageType DamageType;
    public string? WeaponType; // specific weapon type for feats/sacred weapon
    public int Range = 1; // TODO: unused for attack resolution, only quiver filter. Remove and use Launcher instead.
    public int Hands = 1;
    public ActionCost Cost = 12;
    public string[]? AltProficiencies;
    public string? Launcher; // "hand" for thrown weapons
    public string? MeleeVerb; // "thrusts", "swings", etc.
    public WeaponCategory Category = WeaponCategory.Item;
    public override ItemKnowledge RelevantKnowledge => ItemKnowledge.Seen | ItemKnowledge.Props | ItemKnowledge.BUC;

    public WeaponDef()
    {
        DefaultEquipSlot = ItemSlots.Hand;
        Glyph = new(ItemClasses.Weapon);
    }
}

public class ArmorDef : ItemDef
{
    public required int ACBonus;
    public required string Proficiency;
    public int DexCap = 99;
    public int CheckPenalty = 0;
    public int SpeedPenalty = 0;
    public override ItemKnowledge RelevantKnowledge => ItemKnowledge.Seen | ItemKnowledge.Props | ItemKnowledge.BUC;

    public ArmorDef()
    {
        DefaultEquipSlot = ItemSlots.Body;
        Glyph = new(ItemClasses.Armor);
    }
}

public class Item(ItemDef def) : Entity<ItemDef>(def, def.Components), IFormattable
{
    public char InvLet;
    public IUnit? Holder;
    public int Count = 1;
    public int Charges = 0;
    public ItemKnowledge Knowledge;
    public BUC BUC;

    public bool IsNamedUnique = false;

    public bool IsCursed => BUC == BUC.Cursed;
    public bool IsBlessed => BUC == BUC.Blessed;

    public bool IsUnique => IsNamedUnique || Def.IsUnique;
    
    string? _material;
    public string Material
    {
        get => _material ?? ItemDb.Instance.GetAppearance(Def)?.Material ?? Def.Material;
        set => _material = value;
    }
    
    // runes (weapons only for now)
    public int Potency;
    public Fact? Fundamental;
    public List<Fact> PropertyRunes = [];
    
    public int PropertySlots => Potency;
    public int EmptyPropertySlots => Potency - PropertyRunes.Count;
    public bool HasEmptyPropertySlot => PropertyRunes.Count < Potency;
    public bool HasEnchantments => Potency > 0 || PropertyRunes.Any(r => r.Brick is RuneBrick { IsNull: false });

    public int UnitPrice;
    public bool Unpaid;
    public bool Stolen;

    // food/corpse state
    public int Eaten;
    public MonsterDef? CorpseOf;
    public int RotTimer;
    public int CookProgress;

    public int BaseNutrition => CorpseOf?.Nutrition ?? (Def as ConsumableDef)?.Nutrition ?? 0;
    public int RemainingNutrition => BaseNutrition - Eaten;
    public bool IsFood => Def is ConsumableDef || CorpseOf != null;

    public Appearance? Appearance => ItemDb.Instance.GetAppearance(Def);

    public Glyph Glyph => this switch
    {
        { CorpseOf: { } c } => new(ItemClasses.Food, c.Glyph.Color),
        { Appearance: { } app } => Def.Glyph with { Color = app.Color },
        _ => Def.Glyph
    };

    public string DisplayName => CostOf(GetDisplayName(Count));
    public string SingleName => CostOf(GetDisplayName(1));
    public string RealName => GetRealName(Count);

    private string CostOf(string displayName)
    {
        if (Stolen)
            return $"{displayName} (stolen)";
        else if (UnitPrice == 0)
            return displayName;
        else if (Unpaid)
            return $"{displayName} (unpaid, {Price.Crests()})";
        else
            return $"{displayName} ({Price.Crests()})";
    }

    private string GetDisplayName(int count)
    {
        // Corpses use monster name
        if (CorpseOf != null)
            return $"{CorpseOf.Name} corpse";

        var seen = Knowledge.HasFlag(ItemKnowledge.Seen);
        var runesKnown = Knowledge.HasFlag(ItemKnowledge.PropRunes);
        var qualityKnown = Knowledge.HasFlag(ItemKnowledge.PropQuality);
        var potencyKnown = Knowledge.HasFlag(ItemKnowledge.PropPotency);

        // Items with appearances (potions, scrolls, rings, etc.)
        if (ItemDb.Instance.GetAppearance(Def) is { } app)
        {
            // Not seen - class name only
            if (!seen)
            {
                var className = Def.Class switch
                {
                    ItemClasses.Potion => "potion",
                    ItemClasses.Scroll => "scroll",
                    ItemClasses.Ring => "ring",
                    ItemClasses.Amulet => "amulet",
                    ItemClasses.Wand => "wand",
                    _ => "item"
                };
                return count > 1 ? $"{count} {className.Plural()}" : className;
            }

            // Seen but def not known - appearance + called
            if (!Def.IsKnown())
            {
                var called = ItemDb.Instance.GetCalledName(Def);
                var appName = count > 1 ? app.Name.Plural() : app.Name;
                var name = called != null ? $"{appName} called {called}" : appName;
                return count > 1 ? $"{count} {name}" : name;
            }
        }

        // Def known (or no appearance) - show base name, maybe props
        var parts = new List<string>();
        
        if (count > 1)
            parts.Add($"{count}");

        var bucKnown = Knowledge.HasFlag(ItemKnowledge.BUC);
        if (bucKnown)
            parts.Add(BUC switch { BUC.Blessed => "blessed", BUC.Cursed => "cursed", _ => "uncursed" });
        
        if (potencyKnown)
        {
            if (Def is WeaponDef)
                parts.Add($"+{Potency}");
            else if (Def is ArmorDef && Potency > 0)
                parts.Add($"+{Potency}");
        }

        if (qualityKnown)
        {
            if (Fundamental?.Brick is RuneBrick { IsNull: false } fb)
                parts.Add($"{fb.DisplayName}/{fb.Quality}");
        }

        if (runesKnown)
        {
            var props = PropertyRunes
                .Select(r => (RuneBrick)r.Brick)
                .Where(r => !r.IsNull);
            foreach (var r in props)
                parts.Add(r.DisplayName);
        }
        else if (HasEnchantments)
        {
            parts.Add("enchanted");
        }
        
        parts.Add(count > 1 ? Def.Name.Plural() : Def.Name);
        
        if (Def is WandDef && potencyKnown)
            parts.Add($"({Charges})");
        
        return string.Join(" ", parts);
    }

    private string GetRealName(int count)
    {
        if (CorpseOf != null) return $"{CorpseOf.Name} corpse";
        List<string> parts = [];
        if (count > 1) parts.Add($"{count}");
        if (BUC != BUC.Uncursed)
            parts.Add(BUC == BUC.Blessed ? "blessed" : "cursed");
        if (Def is WeaponDef) parts.Add($"+{Potency}");
        else if (Def is ArmorDef && Potency > 0) parts.Add($"+{Potency}");
        if (Fundamental?.Brick is RuneBrick { IsNull: false } fb)
            parts.Add($"{fb.DisplayName}/{fb.Quality}");
        var props = PropertyRunes.Select(r => (RuneBrick)r.Brick).Where(r => !r.IsNull).Select(r => r.DisplayName);
        if (props.Any()) parts.Add(string.Join(" ", props));
        parts.Add(count > 1 ? Def.Name.Plural() : Def.Name);
        return string.Join(" ", parts);
    }

    public int Price => UnitPrice * Count;

    public static Item Create(ItemDef def, int count = 1) => new(def) { Count = count };

    public void Identify()
    {
        Knowledge = ItemKnowledge.Seen | ItemKnowledge.Props | ItemKnowledge.BUC;
        Def.SetKnown();
    }

    public Item Split(int count)
    {
        if (count >= Count) throw new InvalidOperationException("Can't split entire stack");
        Count -= count;
        var other = new Item(Def)
        {
            Count = count,
            Potency = Potency,
            Fundamental = Fundamental,
            Knowledge = Knowledge,
            Stolen = Stolen,
            Unpaid = Unpaid,
            UnitPrice = UnitPrice,
        };
        other.PropertyRunes.AddRange(PropertyRunes);
        other.ShareFactsFrom(this);
        return other;
    }

    public bool CanMerge(Item other)
    {
        if (other.Def != Def) return false;
        if (!Def.Stackable) return false;
        Log.Verbose("merging", $"merge check: {DisplayName} vs {other.DisplayName}");
        if (other.Stolen != Stolen) { Log.Verbose("merging", "  fail: Stolen"); return false; }
        if (other.Unpaid != Unpaid) { Log.Verbose("merging", "  fail: Unpaid"); return false; }
        if (other.UnitPrice != UnitPrice) { Log.Verbose("merging", $"  fail: UnitPrice {UnitPrice} vs {other.UnitPrice}"); return false; }
        var mask = Def.RelevantKnowledge;
        if ((other.Knowledge & mask) != (Knowledge & mask)) { Log.Verbose("merging", $"  fail: Knowledge {Knowledge & mask} vs {other.Knowledge & mask}"); return false; }
        if (other.Potency != Potency) { Log.Verbose("merging", $"  fail: Potency {Potency} vs {other.Potency}"); return false; }
        if (!FactsEquivalent(other)) { Log.Verbose("merging", "  fail: FactsEquivalent"); return false; }
        if (Eaten != 0 || other.Eaten != 0) { Log.Verbose("merging", "  fail: Eaten"); return false; }
        if (CorpseOf != other.CorpseOf) { Log.Verbose("merging", "  fail: CorpseOf"); return false; }
        Log.Verbose("merging", "  merge OK");
        return true;
    }

    bool FactsEquivalent(Item other)
    {
        var mine = LiveFacts.ToList();
        var theirs = other.LiveFacts.ToList();
        if (mine.Count != theirs.Count) return false;
        for (int i = 0; i < mine.Count; i++)
        {
            if (mine[i].Brick != theirs[i].Brick) return false;
            if (!Equals(mine[i].Data, theirs[i].Data)) return false;
        }
        return true;
    }

    public void MergeFrom(Item other)
    {
        Count += other.Count;
    }

    public override string ToString() => DisplayName;
    public string ToString(string? format, IFormatProvider? provider) => format switch
    {
        "the" => DisplayName.The(),
        "The" => DisplayName.The().Capitalize(),
        "the,noprice" => GetDisplayName(Count).The(),
        "The,noprice" => GetDisplayName(Count).The().Capitalize(),
        "an" when IsUnique => Count > 1 ? DisplayName : DisplayName.The(),
        "An" when IsUnique => Count > 1 ? DisplayName : DisplayName.The().Capitalize(),
        "an" => Count > 1 ? DisplayName : DisplayName.An(),
        "An" => Count > 1 ? DisplayName.Capitalize() : DisplayName.An().Capitalize(),
        _ => DisplayName
    };
}

public record struct EquipSlot(string Type, string Slot);

public static class Materials
{
    public const string Iron = "iron";
    public const string Silver = "silver";
    public const string Gold = "gold";
    public const string ColdIron = "cold_iron";
    public const string Adamantine = "adamantine";
}

public static class ItemSlots
{
    public const string None = "_";
    public const string Hand = "hand";
    public const string Body = "body";
    public const string Ring = "ring";
    public const string Amulet = "amulet";
    public const string Face = "face";
    public const string Feet = "feet";
    public const string Hands = "hands";

    public static readonly EquipSlot BodySlot = new(Body, "_");
    public static readonly EquipSlot FaceSlot = new(Face, "_");
    public static readonly EquipSlot MainHandSlot = new(Hand, "_");
    public static readonly EquipSlot OffHandSlot = new(Hand, "off");
    public static readonly EquipSlot FeetSlot = new(Feet, "_");
    public static readonly EquipSlot HandsSlot = new(Hands, "_");
}

public static class ItemClasses
{
    public const char Illobj = ']';
    public const char Weapon = ')';
    public const char Armor = '[';
    public const char Ring = '=';
    public const char Amulet = '"';
    public const char Tool = '(';
    public const char Food = '%';
    public const char Potion = '!';
    public const char Scroll = '?';
    public const char Spellbook = '+';
    public const char Wand = '/';
    public const char Gold = '$';
    public const char Gem = '*';

    public const string Order = "$\")[%?+!/=(*."; // coin, amulet, weapon, armor, food, scroll, spellbook, potion, ring, wand, tool, gem, rock
}

public static class MiscItems
{
    public static readonly ItemDef SilverCrest = new()
    {
        Name = "silver crest",
        Glyph = new(ItemClasses.Gold, ConsoleColor.White, null, GlyphFlags.Bold),
        Stackable = true,
        Weight = 0,
        Price = 1,
    };
}
