using System.Runtime.InteropServices;

namespace Pathhack.Game;

public enum ModifierCategory
{
    UntypedStackable,
    CircumstanceBonus,
    CircumstancePenalty,
    ItemBonus,
    ItemPenalty,
    StatusBonus,
    StatusPenalty,
    StatBonus,
    StatPenalty,

    Override, // BE CAREFUL WITH THIS
}

public record class Modifier(ModifierCategory Category, int Value, string? Why = null, int Priority = 0)
{
    public static int operator +(int value, Modifier? modifier) => value + modifier?.Value ?? 0;

    public string Label => $"{Why} :{Category}";
}


public sealed class PHContext : IDisposable
{
    public required PHContext? Parent;
    public required IUnit? Source;
    public required Target Target;
    public SpellBrick? Spell;
    public ActionBrick? Action;
    public Item? Weapon;
    public bool Melee = false;
    public Check? Check;
    public List<DamageRoll> Damage = [];
    public DiceFormula HealFormula;
    public Modifiers HealModifiers = new();
    public int HealedAmount;
    public string? DeathReason;
    public bool SilentCheck;
    public bool SilentDamage;
    public int TotalDamageDealt;
    public int HpBefore;
    public int HpAfter;

    public PHContext Root => Parent?.Root ?? this;

    public bool IsCheckingOwnerOf(Fact fact) => fact.Entity == Target.Unit;

    public static PHContext? Current { get; private set; }

    public void Dispose() => Current = Parent;
    public static PHContext Create(IUnit? source, Target target)
    {
        PHContext n = new()
        {
            Parent = Current,
            Source = source,
            Target = target,
        };
        Current = n;
        return n;
    }
}

public static class Proficiencies
{
    // Weapon groups
    public const string Unarmed = "unarmed";
    public const string Natural = "natural";
    public const string HeavyBlade = "heavy_blade";
    public const string LightBlade = "light_blade";
    public const string Club = "club";
    public const string Axe = "axe";
    public const string Polearm = "polearm";
    public const string Staff = "staff";
    public const string Bow = "bow";
    public const string Thrown = "thrown";
    public const string Flail = "flail";
    public const string Whip = "whip";
    public const string Close = "close";

    // Armor
    public const string NakedArmor = "armor_naked";
    public const string LightArmor = "armor_light";
    public const string MediumArmor = "armor_medium";
    public const string HeavyArmor = "armor_heavy";
}

public static class WeaponTypes
{
    public const string Unarmed = "unarmed";
    public const string Dagger = "dagger";
    public const string Shortsword = "shortsword";
    public const string Longsword = "longsword";
    public const string Rapier = "rapier";
    public const string Scimitar = "scimitar";
    public const string Falchion = "falchion";
    public const string Club = "club";
    public const string Greatclub = "greatclub";
    public const string Mace = "mace";
    public const string Flail = "flail";
    public const string Quarterstaff = "quarterstaff";
    public const string BoStaff = "bo_staff";
    public const string Spear = "spear";
    public const string Scythe = "scythe";
    public const string Whip = "whip";
    public const string SpikedChain = "spiked_chain";
    public const string Longbow = "longbow";
    public const string Shortbow = "shortbow";
    public const string Dart = "dart";
    public const string Bola = "bola";
    public const string OrcKnuckleDagger = "orc_knuckle_dagger";
    public const string OrcNecksplitter = "orc_necksplitter";
}

public enum ProficiencyLevel
{
    Untrained = 0,
    Trained = 2,
    Expert = 4,
    Master = 6,
    Legendary = 8
}

public class Modifiers
{
    public readonly Dictionary<ModifierCategory, Modifier> Unstackable = [];
    public readonly List<Modifier> Stackable = [];
    public Modifier? Override { get; private set; }

    public Modifier Untyped(int value, string why) => Mod(ModifierCategory.UntypedStackable, value, why);

    public Modifier Mod(ModifierCategory cat, int value, string why) => AddModifier(new(cat, value, why));

    public Modifier AddModifier(Modifier modifier)
    {
        if (modifier.Category == ModifierCategory.Override)
        {
            if (Override == null || modifier.Priority > Override.Priority)
                Override = modifier;
        }
        if (modifier.Category == ModifierCategory.UntypedStackable)
        {
            Stackable.Add(modifier);
        }
        else if (Unstackable.TryGetValue(modifier.Category, out var current))
        {
            if (modifier.Value > current.Value)
            {
                Unstackable[modifier.Category] = modifier;
            }
        }
        else
        {
            Unstackable.Add(modifier.Category, modifier);
        }

        return modifier;
    }

    public void RemoveModifier(Modifier modifier)
    {
        if (modifier.Category == ModifierCategory.UntypedStackable)
            Stackable.Remove(modifier);
        else if (Unstackable.GetValueOrDefault(modifier.Category) == modifier)
            Unstackable.Remove(modifier.Category);
    }

    public int Calculate() => Override != null ? Override.Value : (Unstackable.Values.Sum(x => x.Value) + Stackable.Sum(x => x.Value));

    internal void AddAll(Modifiers mods)
    {
        Stackable.AddRange(mods.Stackable);
        foreach (var m in mods.Unstackable.Values)
            AddModifier(m);
    }

    public override string ToString()
    {
        var parts = Stackable.Concat(Unstackable.Values)
            .Where(m => m.Value != 0)
            .Select(m => m.Value > 0 ? $"+{m.Value} ({m.Label})" : $"{m.Value} ({m.Label})");
        return string.Join(" ", parts);
    }
}

public enum Degree
{
    CriticalSuccess,
    Success,
    Fail,
    CriticalFail,
}

public class Check
{
    public int Roll;
    public int Roll1;
    public int? Roll2;
    public int BaseRoll;
    public int DC;
    public Modifiers Modifiers = new();
    public string Tag = "_";
    public string Key = "_";

    public bool? ForcedResult;
    public int Advantage;
    public int Disadvantage;

    public void ForceSuccess() => ForcedResult = true;
    public void ForceFailure() => ForcedResult = false;

    public bool Result => ForcedResult ?? (Roll >= DC);

    public bool IsSave => Key == Fort || Key == Reflex || Key == Will;

    public string RollStr => Roll2 == null ? $"{Roll1}" : $"${Roll1},{Roll2},{(Advantage > Disadvantage ? "hi": "lo")}]";

    public const string Fort = "fortitude_save";
    public const string Reflex = "reflex_save";
    public const string Will = "will_save";
}

public class DamageRoll
{
    public DiceFormula Formula;
    public DamageType Type;
    public Modifiers Modifiers = new();
    public HashSet<string> Tags = [];

    public bool Has(string tag) => Tags.Contains(tag);
    public bool HasAll(params string[] tags) => tags.All(Has);
    public bool HasAny(params string[] tags) => tags.Any(Has);
    public bool HasNone(params string[] tags) => !tags.Any(Has);

    public bool HalfOnSave;
    public bool DoubleOnFail;

    public bool Negated { get; private set; }
    public bool Halved { get; private set; }
    public bool Doubled { get; private set; }

    private int _extraDice = 0;
    public int ExtraDice
    {
        get => _extraDice;
        set => _extraDice = Math.Max(value, _extraDice);
    }

    private int _dr = 0;
    public int DR => _dr;
    public void ApplyDR(int amount) => _dr = Math.Max(_dr, amount);

    public int Protection;
    public int ProtectionUsed { get; private set; }

    private int? _rolled;
    public int Rolled => _rolled ?? throw new InvalidOperationException("Roll not resolved");
    public int Resolve(int rolled) { _rolled = rolled; return Total; }

    public int Total
    {
        get
        {
            if (Negated) return 0;
            int raw = Math.Max(1, Rolled + Modifiers.Calculate());
            if (Halved && Doubled) { } // cancel
            else if (Halved) raw = Math.Max(1, raw / 2);
            else if (Doubled) raw *= 2;
            ProtectionUsed = Math.Min(Protection, raw);
            int afterProt = raw - ProtectionUsed;
            return Math.Max(0, afterProt - _dr);
        }
    }

    public void Negate() => Negated = true;
    public void Halve() => Halved = true;
    public void Double() => Doubled = true;

    public double Multiplier
    {
        get
        {
            if (Negated) return 0;
            if (Halved && Doubled) return 1;
            if (Halved) return 0.5;
            if (Doubled) return 2;
            return 1;
        }
    }

    public override string ToString()
    {
        if (Negated) return $"{Formula} {Type.SubCat} (negated)";

        var parts = new List<string> { $"{Formula}" };

        int mod = Modifiers.Calculate();
        if (mod != 0)
            parts.Add(mod > 0 ? $"+{mod}" : $"{mod}");

        if (Halved) parts.Add("(halved)");
        if (Doubled) parts.Add("(doubled)");
        if (Halved && Doubled) parts.Add("(cancelled)");

        parts.Add(Type.SubCat);

        return string.Join(" ", parts);
    }
}

public enum AbilityStat { Str, Dex, Con, Int, Wis, Cha }

public struct ValueStatBlock<T> where T : struct
{
    public required T Str;
    public required T Dex;
    public required T Con;
    public required T Int;
    public required T Wis;
    public required T Cha;

    internal void Modify(AbilityStat stat, Func<T, T> modify)
    {
        switch (stat)
        {
            case AbilityStat.Str:
                Str = modify(Str);
                break;
            case AbilityStat.Dex:
                Dex = modify(Dex);
                break;
            case AbilityStat.Con:
                Con = modify(Con);
                break;
            case AbilityStat.Int:
                Int = modify(Int);
                break;
            case AbilityStat.Wis:
                Wis = modify(Wis);
                break;
            case AbilityStat.Cha:
                Cha = modify(Cha);
                break;
        }
    }

    internal readonly T Get(AbilityStat stat) => stat switch
    {
        AbilityStat.Str => Str,
        AbilityStat.Dex => Dex,
        AbilityStat.Con => Con,
        AbilityStat.Int => Int,
        AbilityStat.Wis => Wis,
        AbilityStat.Cha => Cha,
        _ => throw new ArgumentException($"not a valid stat: {stat}")
    };
}

public class StatBlock<T>(Func<T> creat)
{
    public readonly T Str = creat();
    public readonly T Dex = creat();
    public readonly T Con = creat();
    public readonly T Int = creat();
    public readonly T Wis = creat();
    public readonly T Cha = creat();

    public T this[AbilityStat stat] => stat switch
    {
      AbilityStat.Str => Str,
      AbilityStat.Dex => Dex,
      AbilityStat.Con => Con,
      AbilityStat.Int => Int,
      AbilityStat.Wis => Wis,
      AbilityStat.Cha => Cha,
      _ => throw new NotImplementedException(),
    };
}

public record struct ActionCost(int Value)
{
    public static implicit operator ActionCost(int value) => new(value);
}

public static class ActionCosts
{
    public static readonly ActionCost Free = 0;
    public static readonly ActionCost OneAction = 12;
    // 30ft speed equivalent, let's say?
    public static readonly ActionCost StandardLandMove = 12;
    public static readonly ActionCost LandMove25 = 15;
    internal static readonly ActionCost LandMove20 = 18;
    internal static readonly ActionCost LandMove15 = 21;
    internal static readonly ActionCost LandMove10 = 24;
}

public record struct DamageType(string Category, string SubCat);

public static class DamageTypes
{
    public static readonly DamageType Slashing = new("phys", "slashing");
    public static readonly DamageType Piercing = new("phys", "piercing");
    public static readonly DamageType Blunt = new("phys", "blunt");
    public static readonly DamageType Fire = new("elem", "fire");
    public static readonly DamageType Cold = new("elem", "cold");
    public static readonly DamageType Shock = new("elem", "shock");
    public static readonly DamageType Acid = new("elem", "acid");
    public static readonly DamageType Magic = new("magic", "_");
    public static readonly DamageType Force = new("force", "_");

    public static readonly DamageType Holy = new("spirit", "good");
    public static readonly DamageType Unholy = new("spirit", "evil");
    public static readonly DamageType Poison = new("poison", "_");
}

public enum UnitSize
{
    Tiny,
    Small,
    Medium,
    Large,
    Huge,
    Gargantuan,
}

public record class Target(IUnit? Unit, Pos? Pos)
{
    public static readonly Target None = new(null, null);

    public static Target From(IUnit unit) => new(unit, null);
    public static Target From(Pos pos) => new(null, pos);
}

public static class StatBlocks
{
    public readonly static ValueStatBlock<int> Standard = Block(10, 10, 10, 10, 10, 10);

    private static ValueStatBlock<int> Block(int str, int dex, int con, int @int, int wis, int cha) => new()
    {
        Str = str,
        Dex = dex,
        Con = con,
        Int = @int,
        Wis = wis,
        Cha = cha,

    };
}