namespace Pathhack.Game;

public enum BUC { Cursed = -1, Uncursed = 0, Blessed = 1 }

public class ItemDef : BaseDef, IFormattable
{
    public string Name = "";
    public Glyph Glyph = new(']', ConsoleColor.White);
    public string DefaultEquipSlot = ItemSlots.None;
    public int Weight = 1;
    public string Material = Materials.Iron;
    public bool Stackable;
    public bool IsUnique = false;
    public string? PokedexDescription;

    public bool IsEphemeral => Weight < 0;

    // ID system - set for magical consumables/accessories
    public AppearanceCategory? AppearanceCategory;
    public int AppearanceIndex = -1;
    public required int Price;
    public bool CanHavePotency;
    public int BUCBias; // -2=always cursed, -1=90% cursed, 0=normal, +1=90% blessed, +2=always blessed

    public char Class => Glyph.Value;
    public virtual ItemKnowledge RelevantKnowledge => ItemKnowledge.Seen;

    public string ResolveName(bool seen, int count = 1)
    {
        if (ItemDb.Instance.GetAppearance(this) is { } app)
        {
            if (!seen)
            {
                var className = Class switch
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

            if (!this.IsKnown())
            {
                var called = ItemDb.Instance.GetCalledName(this);
                var appName = count > 1 ? app.Name.Plural() : app.Name;
                var name = called != null ? $"{appName} called {called}" : appName;
                return count > 1 ? $"{count} {name}" : name;
            }
        }

        return count > 1 ? $"{count} {Name.Plural()}" : Name;
    }

    public string ToString(string? format, IFormatProvider? provider)
    {
        // format: "the", "an", etc. Append ",noseen" to suppress seen knowledge.
        bool seen = format?.Contains("noseen") != true;
        var name = ResolveName(seen);
        var fmt = format?.Replace(",noseen", "");

        return fmt switch
        {
            "the" => name.The(),
            "The" => name.The().Capitalize(),
            "an" when IsUnique => name.The(),
            "An" when IsUnique => name.The().Capitalize(),
            "an" => name.An(),
            "An" => name.An().Capitalize(),
            _ => name
        };
    }
}

public enum RuneSlot { Fundamental, Property }

public abstract class RuneBrick(string displayName, int quality, RuneSlot slot) : LogicBrick
{
    public string DisplayName => displayName;
    public string QualifiedName => $"{displayName}/{Quality}";
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
    public int Reach = 1;
    public int Hands = 1;
    public ActionCost Cost = 12;
    public string[]? AltProficiencies;
    public string? Launcher; // "hand" for thrown weapons
    public string? MeleeVerb; // "thrust", "swing", etc.
    public WeaponCategory Category = WeaponCategory.Item;
    public override ItemKnowledge RelevantKnowledge => ItemKnowledge.Seen | ItemKnowledge.Props | ItemKnowledge.BUC;
    public bool NotForWhacking = false;
    public double StrBonus = 1;

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

public class Item(ItemDef def) : Entity<ItemDef>(def, def.Components), IFormattable, ISelectable
{
    public char InvLet;
    public IUnit? Holder;
    public int Count = 1;
    public int Charges = 0;
    public int MaxCharges = -1;
    public ItemKnowledge Knowledge;
    public BUC BUC;

    public bool IsNamedUnique = false;

    public bool IsCursed => BUC == BUC.Cursed;
    public bool IsBlessed => BUC == BUC.Blessed;

    public bool IsUnique => IsNamedUnique || Def.IsUnique;

    public int EffectiveWeight => CorpseOf is { } c
        ? CorpseWeight(c.Size)
        : (Def.Weight + Query<int>("weight", null, MergeStrategy.Sum, 0)) * Count;

    static int CorpseWeight(UnitSize size) => size switch
    {
        UnitSize.Tiny => 60,
        UnitSize.Small => 250,
        UnitSize.Medium => 1000,
        UnitSize.Large => 2000,
        UnitSize.Huge => 4000,
        UnitSize.Gargantuan => 8000,
        _ => 1000,
    };

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

    public string DisplayName => CostOf(Count == 1 ? GetDisplayName(Count).An() : GetDisplayName(Count));
    public string DisplayNameWeighted => DisplayName + $" {{{EffectiveWeight}}}";
    public string SingleName => CostOf(GetDisplayName(1).An());
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

        // Items with appearances that aren't fully identified yet
        if (ItemDb.Instance.GetAppearance(Def) is not null && (!seen || !Def.IsKnown()))
            return Def.ResolveName(seen, count);

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
                parts.Add(fb.QualifiedName);
        }

        if (runesKnown)
        {
            var props = PropertyRunes
                .Select(r => (RuneBrick)r.Brick)
                .Where(r => !r.IsNull);
            foreach (var r in props)
                parts.Add(r.QualifiedName);
        }
        else if (HasEnchantments)
        {
            parts.Add("enchanted");
        }

        parts.Add(count > 1 ? Def.Name.Plural() : Def.Name);

        if (Def is WandDef or QuiverDef && potencyKnown)
            parts.Add($"({Charges})");

        var result = string.Join(" ", parts);
        return result;
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
            parts.Add(fb.QualifiedName);
        var props = PropertyRunes.Select(r => (RuneBrick)r.Brick).Where(r => !r.IsNull).Select(r => r.DisplayName);
        if (props.Any()) parts.Add(string.Join(" ", props));
        parts.Add(count > 1 ? Def.Name.Plural() : Def.Name);
        return string.Join(" ", parts);
    }

    public int Price => UnitPrice * Count;

    public string Name => GetDisplayName(Count);

    public string Description => Def.PokedexDescription ?? Def.Name;

    public string? WhyNot => null;

    public static Item Create(ItemDef def, int count = 1)
    {
        Item item = new(def) { Count = count };
        if (def is WeaponDef { Reach: > 1 })
            item.AddFact(ReachAttackVerb.Instance);
        return item;
    }

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
        "the" => CostOf(GetDisplayName(Count).The()),
        "The" => CostOf(GetDisplayName(Count).The().Capitalize()),
        "the,noprice" => GetDisplayName(Count).The(),
        "The,noprice" => GetDisplayName(Count).The().Capitalize(),
        "an" when IsUnique => CostOf(GetDisplayName(Count).The()),
        "An" when IsUnique => CostOf(GetDisplayName(Count).The().Capitalize()),
        "an" => DisplayName,
        "An" => DisplayName.Capitalize(),
        "bare" => GetDisplayName(Count),
        _ => DisplayName
    };

    internal void Charge(int by)
    {
        if (MaxCharges != -1)
        {
            Charges = Math.Min(Charges + by, MaxCharges);
        }
    }

    internal Item Uncurse()
    {
        if (BUC == BUC.Cursed) BUC = BUC.Uncursed;
        return this;
    }

    internal void PlaceAt(Pos pos) => lvl.PlaceItem(this, pos);

    internal Item AsStack(int stackSize)
    {
        if (!Def.Stackable) throw new NotSupportedException();
        Count = stackSize;
        return this;
    }
}

public record struct EquipSlot(string Type, string Slot);

public static class Materials
{
    public const string Iron = "iron";
    public const string Silver = "silver";
    public const string Gold = "gold";
    public const string ColdIron = "cold_iron";
    public const string Adamantine = "adamantine";

    public const string Leather = "leather";
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
    public const string Quiver = "quiver";

    public const string Alt = "alt";

    public static readonly EquipSlot BodySlot = new(Body, "_");
    public static readonly EquipSlot FaceSlot = new(Face, "_");
    public static readonly EquipSlot MainHandSlot = new(Hand, "_");
    public static readonly EquipSlot OffHandSlot = new(Hand, "off");
    public static readonly EquipSlot AltSlot = new(Alt, "_");
    public static readonly EquipSlot FeetSlot = new(Feet, "_");
    public static readonly EquipSlot HandsSlot = new(Hands, "_");
    public static readonly EquipSlot QuiverSlot = new(Quiver, "_");
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

// only for special stuff, equip/wield/put-on etc is handled separately
[Flags]
public enum ItemVerb
{
    None = 0,

    Read = 1,
    Quaff = 2,
    Apply = 4,
    Zap = 8,
    Eat = 16,

    Throw = 32, //?
}

public abstract class VerbResponder(ItemVerb subjectOf) : LogicBrick
{
    public ItemVerb SubjectOf => subjectOf;

}

public static class VerbExts
{
    public static bool IsSubjectOf(this VerbResponder? verbResponder, ItemVerb verb) => verbResponder?.SubjectOf.HasFlag(verb) == true;
}