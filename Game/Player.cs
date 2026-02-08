using System.Transactions;

namespace Pathhack.Game;

public class PlayerDef : BaseDef { }

public class Player(PlayerDef def) : Unit<PlayerDef>(def, def.Components), IFormattable
{
    public static Player u { get; set; } = null!;
    public static Pos upos
    {
        get => u.Pos;
        set => u.Pos = value;
    }

    public override int NaturalRegen => 15 * CharacterLevel;

    public int GetAttribute(AbilityStat stat) => BaseAttributes.Get(stat) + QueryModifiers($"stat/{stat}").Calculate();
    public int Str => BaseAttributes.Str + QueryModifiers("stat/Str").Calculate();
    public int Dex => BaseAttributes.Dex + QueryModifiers("stat/Dex").Calculate();
    public int Con => BaseAttributes.Con + QueryModifiers("stat/Con").Calculate();
    public int Int => BaseAttributes.Int + QueryModifiers("stat/Int").Calculate();
    public int Wis => BaseAttributes.Wis + QueryModifiers("stat/Wis").Calculate();
    public int Cha => BaseAttributes.Cha + QueryModifiers("stat/Cha").Calculate();
    public override int StrMod => Mod(Str);
    public int DexMod => Mod(Dex);
    public int ConMod => Mod(Con);
    public int IntMod => Mod(Int);
    public int WisMod => Mod(Wis);
    public int ChaMod => Mod(Cha);
    public int KeyAttribute => Class.KeyAbility switch
    {
        AbilityStat.Str => Str,
        AbilityStat.Dex => Dex,
        AbilityStat.Con => Con,
        AbilityStat.Int => Int,
        AbilityStat.Wis => Wis,
        AbilityStat.Cha => Cha,
        _ => throw new NotImplementedException(),
    };

    public override MoralAxis MoralAxis => Query("moral_axis", null, MergeStrategy.Replace, MoralAxis.Neutral);
    public override EthicalAxis EthicalAxis => Query("ethical_axis", null, MergeStrategy.Replace, EthicalAxis.Neutral);
    public override bool IsCreature(string? type = null, string? subtype = null) =>
      (type == null || type == CreatureTypes.Humanoid) && (subtype == null || Has(subtype));

    public LevelId Level { get; set; }
    public int DarkVisionRadius => Math.Clamp(Ancestry.DarkVisionRadius + QueryModifiers("light_radius").Calculate(), 0, 100);
    public override ActionCost LandMove => ActionCosts.StandardLandMove.Value - QueryModifiers("speed_bonus").Calculate();

    public ValueStatBlock<int> BaseAttributes;
    public readonly int BaseLandSpeed = ActionCosts.StandardLandMove.Value;
    private readonly Dictionary<string, ProficiencyLevel> Proficiencies = [];
    public int CharacterLevel = 0;
    public int XP = 0;
    public int Nutrition = Hunger.Satiated - 1;
    public int HippoCounter;
    public Activity? CurrentActivity;
    public HashSet<string> TakenFeats = [];
    public Dictionary<LevelId, HashSet<string>> SeenFeatures = [];

    public required ClassDef Class = null!;
    public required DeityDef Deity = null!;
    public required AncestryDef Ancestry = null!;
    public Item? Quiver;

    public override bool IsPlayer => true;

    public override string ToString() => "you";
    public string ToString(string? format, IFormatProvider? provider) => format switch
    {
        "Own" => "Your",
        "own" => "your",
        "The" or "An" => "You",
        _ => "you",
    };

    public static Player Create(ClassDef cls, DeityDef deity, AncestryDef ancestry)
    {
        PlayerDef def = new();
        Player p = new(def)
        {
            Class = cls,
            Deity = deity,
            Ancestry = ancestry,
        };

        p.BaseAttributes = cls.StartingStats;

        p.AddFact(new PlayerSkills());

        // Apply ancestry boosts
        foreach (var boost in ancestry.Boosts)
            p.BaseAttributes.Modify(boost, current => current + 2);
        foreach (var flaw in ancestry.Flaws)
            p.BaseAttributes.Modify(flaw, current => current - 2);

        // Starting equipment
        cls.GrantStartingEquipment?.Invoke(p);

        // base max hp
        p.HP.Reset(p.Class.HpPerLevel + 12);

        return p;
    }

    public ProficiencyLevel GetProficiency(string skill)
    {
        var baseLevel = Proficiencies.GetValueOrDefault(skill, ProficiencyLevel.Untrained);
        var fromItems = Query("proficiency", skill, MergeStrategy.Max, 0);
        return (ProficiencyLevel)Math.Max((int)baseLevel, fromItems);
    }

    public void SetProficiency(string skill, ProficiencyLevel level) => Proficiencies[skill] = level;

    public int ProfBonus(string skill) => (int)GetProficiency(skill);

    public static int Mod(int stat) => (stat - 10) / 2;

    public override Glyph Glyph => new('@', ConsoleColor.White);

    public override int GetAC()
    {
        int dexMod = DexMod;
        int dexCap = Query("dex_cap", null, MergeStrategy.Min, 99);
        var armorProf = (Equipped.TryGetValue(ItemSlots.BodySlot, out var armor) && armor.Def is ArmorDef armorDef) ? armorDef.Proficiency : Game.Proficiencies.NakedArmor;
        var mods = QueryModifiers("ac");
        // Log.Write("GetAC: dexMod={0} dexCap={1} mods={2}", dexMod, dexCap, mods.Calculate());
        return 10 + Math.Min(dexMod, dexCap) + mods.Calculate() + ProfBonus(armorProf);
    }

    public override int GetAttackBonus(WeaponDef weapon) => StrMod + ProfBonus(weapon.Profiency);

    public override int GetDamageBonus() => StrMod;

    public override int GetSpellDC() => 10 + CasterLevel + Mod(KeyAttribute);

    protected override WeaponDef GetUnarmedDef() => NaturalWeapons.Fist;

    public override bool IsAwareOf(Trap trap) => trap.PlayerSeen;
    public override void ObserveTrap(Trap trap) => trap.PlayerSeen = true;

    public int CalculateMaxHp(int baseVal)
    {
        var hpMod = QueryModifiers("max_hp");
        return baseVal + CharacterLevel * ConMod + hpMod.Calculate();
    }

    internal void RecalculateMaxHp()
    {
        u.HP.Max = CalculateMaxHp(u.HP.BaseMax);
        u.HP.Current = Math.Clamp(u.HP.Current, 1, u.HP.Max);
    }

    public override bool IsDM => false;
    public override int CasterLevel => CharacterLevel;
    public override int EffectiveLevel => CharacterLevel;
}

internal class PlayerSkills : LogicBrick
{
  protected override object? OnQuery(Fact fact, string key, string? arg)
  {
    return key switch
    {
        "perception" => new Modifier(ModifierCategory.StatBonus, u.WisMod),
        "athletics" => new Modifier(ModifierCategory.StatBonus, u.StrMod),

        // Hmm, how do we let classes override this? Tbh attributes are kinda lame?
        "reflex_save" => new Modifier(ModifierCategory.StatBonus, u.DexMod),
        "fortitude_save" => new Modifier(ModifierCategory.StatBonus, u.ConMod),
        "will_save" => new Modifier(ModifierCategory.StatBonus, u.WisMod),
        _ => null,
    };
  }
}