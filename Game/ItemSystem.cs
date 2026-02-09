namespace Pathhack.Game;

public enum AppearanceCategory { Potion, Scroll, Amulet, Boots, Gloves, Cloak }

public record Appearance(string Name, ConsoleColor Color);

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
        ],
        [AppearanceCategory.Gloves] = [
            new("leather gloves", ConsoleColor.DarkYellow),
            new("padded gloves", ConsoleColor.Gray),
            new("riding gloves", ConsoleColor.White),
            new("gauntlets", ConsoleColor.DarkGray),
        ],
        [AppearanceCategory.Cloak] = [
            new("tattered cloak", ConsoleColor.DarkGray),
            new("opera cloak", ConsoleColor.Red),
            new("ornate cloak", ConsoleColor.Magenta),
            new("faded cloak", ConsoleColor.Gray),
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

    // ID system - set for magical consumables/accessories
    public AppearanceCategory? AppearanceCategory;
    public int AppearanceIndex = -1;

    public char Class => Glyph.Value;
}

public enum RuneSlot { Fundamental, Property }

public class RuneDef : BaseDef
{
    public required RuneSlot Slot;
    public required string DisplayName;
    public required int Quality;
    public required string Description;

    public bool IsNull; // blocker rune

}

public class Rune(RuneDef def)
{
    public RuneDef Def => def;
    public List<Fact> Facts = [];
}

public enum WeaponCategory { Unarmed, Natural, Item }

public class WeaponDef : ItemDef
{
    public required DiceFormula BaseDamage;
    public required string Profiency; // weapon group
    public required DamageType DamageType;
    public string? WeaponType; // specific weapon type for feats/sacred weapon
    public int Range = 1;
    public int Hands = 1;
    public ActionCost Cost = 12;
    public string[]? AltProficiencies;
    public string? Launcher; // "hand" for thrown weapons
    public string? MeleeVerb; // "thrusts", "swings", etc.
    public WeaponCategory Category = WeaponCategory.Item;

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

    public bool IsNamedUnique = false;

    public bool IsUnique => IsNamedUnique || Def.IsUnique;
    
    // runes (weapons only for now)
    public int Potency;
    public Rune? Fundamental;
    public List<Rune> PropertyRunes = [];
    
    public int PropertySlots => Potency;
    public int EmptyPropertySlots => Potency - PropertyRunes.Count;
    public bool HasEmptyPropertySlot => PropertyRunes.Count < Potency;

    // food/corpse state
    public int Eaten;
    public MonsterDef? CorpseOf;
    public int RotTimer;
    public int CookProgress;

    public int BaseNutrition => CorpseOf?.Nutrition ?? (Def as ConsumableDef)?.Nutrition ?? 0;
    public int RemainingNutrition => BaseNutrition - Eaten;
    public bool IsFood => Def is ConsumableDef || CorpseOf != null;

    public Glyph Glyph => CorpseOf != null
        ? new(ItemClasses.Food, CorpseOf.Glyph.Color)
        : Def.Glyph;

    public string DisplayName => GetDisplayName(Count);
    public string SingleName => GetDisplayName(1);

    string GetDisplayName(int count)
    {
        // Corpses use monster name
        if (CorpseOf != null)
            return $"{CorpseOf.Name} corpse";

        // Unidentified items show appearance or called name
        if (!ItemDb.Instance.IsIdentified(Def) && ItemDb.Instance.GetAppearance(Def) is { } app)
        {
            var called = ItemDb.Instance.GetCalledName(Def);
            var appName = count > 1 ? app.Name.Plural() : app.Name;
            var name = called != null ? $"{appName} called {called}" : appName;
            return count > 1 ? $"{count} {name}" : name;
        }

        var parts = new List<string>();
        
        if (count > 1)
            parts.Add($"{count}");
        
        // Show potency for weapons (always) and armor (when > 0)
        if (Def is WeaponDef)
            parts.Add($"+{Potency}");
        else if (Def is ArmorDef && Potency > 0)
            parts.Add($"+{Potency}");

        if (Fundamental != null && !Fundamental.Def.IsNull)
            parts.Add($"{Fundamental.Def.DisplayName}/{Fundamental.Def.Quality}");
        
        // property runes: "flaming/4 shock/4"
        var props = PropertyRunes
            .Where(r => !r.Def.IsNull)
            .Select(r => {
                return r.Def.DisplayName;
            });
        if (props.Any())
            parts.Add(string.Join(" ", props));
        
        parts.Add(count > 1 ? Def.Name.Plural() : Def.Name);
        return string.Join(" ", parts);
    }

    public int EffectiveBonus
    {
        get
        {
            return Potency;
        }
    }

    public static Item Create(ItemDef def, int count = 1) => new(def) { Count = count };

    public Item Split(int count)
    {
        if (count >= Count) throw new InvalidOperationException("Can't split entire stack");
        Count -= count;
        var other = new Item(Def)
        {
            Count = count,
            Potency = Potency,
            Fundamental = Fundamental,
        };
        other.PropertyRunes.AddRange(PropertyRunes);
        other.ShareFactsFrom(this);
        return other;
    }

    public bool CanMerge(Item other)
    {
        if (!Def.Stackable) return false;
        if (other.Def != Def) return false;
        if (other.Potency != Potency) return false;
        if (other.Fundamental != Fundamental) return false;
        if (!PropertyRunes.SequenceEqual(other.PropertyRunes)) return false;
        if (!FactsEquivalent(other)) return false;
        // Don't stack partially eaten food
        if (Eaten != 0 || other.Eaten != 0) return false;
        if (CorpseOf != other.CorpseOf) return false;
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
}

public static class ItemSlots
{
    public const string None = "_";
    public const string Hand = "hand";
    public const string Body = "body";
    public const string Ring = "ring";
    public const string Amulet = "amulet";
    public const string Face = "face";

    public static readonly EquipSlot BodySlot = new(Body, "_");
    public static readonly EquipSlot FaceSlot = new(Face, "_");
    public static readonly EquipSlot HandSlot = new(Hand, "_");
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
