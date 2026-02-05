namespace Pathhack.Game;

public class ItemDef : BaseDef
{
    public string Name = "";
    public Glyph Glyph = new(']', ConsoleColor.White);
    public string DefaultEquipSlot = ItemSlots.None;
    public int Weight = 1;
    public string Material = Materials.Iron;
    public bool Stackable;
    public bool IsUnique = false;

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

    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            
            if (Count > 1)
                parts.Add($"{Count}");
            
            // Only show potency for weapons
            if (Def is WeaponDef)
            {
                int bonus = EffectiveBonus;
                parts.Add($"{bonus:+#;-#;+0}");
            }

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
            
            parts.Add(Count > 1 ? Def.Name.Plural() : Def.Name);
            return string.Join(" ", parts);
        }
    }

    public int EffectiveBonus
    {
        get
        {
            return Potency;
        }
    }

    public static Item Create(ItemDef def) => new(def);

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
