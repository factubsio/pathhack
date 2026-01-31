namespace Pathhack.Game;

public class PlayerDef : BaseDef { }

public class Player(PlayerDef def) : Unit<PlayerDef>(def), IFormattable
{
    public static Player u { get; set; } = null!;
    public static Pos upos
    {
        get => u.Pos;
        set => u.Pos = value;
    }

    public override int NaturalRegen => 15 * CharacterLevel;
    public override int StrMod => Mod(Attributes.Str.Value);

    public LevelId Level { get; set; }
    public int DarkVisionRadius => Math.Clamp(Ancestry.DarkVisionRadius + QueryModifiers("light_radius").Calculate(), 0, 100);
    public override ActionCost LandMove => ActionCosts.StandardLandMove;

    public StatBlock<ModifiableValue> Attributes = new(() => new(10));
    public readonly ModifiableValue LandSpeed = new(ActionCosts.StandardLandMove.Value);
    private readonly Dictionary<string, ProficiencyLevel> Proficiencies = [];
    public int CharacterLevel = 0;
    public int XP = 0;
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

        // Apply class starting stats
        p.Attributes.Str.BaseValue = cls.StartingStats.Str;
        p.Attributes.Dex.BaseValue = cls.StartingStats.Dex;
        p.Attributes.Con.BaseValue = cls.StartingStats.Con;
        p.Attributes.Int.BaseValue = cls.StartingStats.Int;
        p.Attributes.Wis.BaseValue = cls.StartingStats.Wis;
        p.Attributes.Cha.BaseValue = cls.StartingStats.Cha;

        p.AddFact(new PlayerSkills());

        // Apply ancestry boosts
        foreach (var boost in ancestry.Boosts)
            p.ApplyStatMod(boost, 2);
        foreach (var flaw in ancestry.Flaws)
            p.ApplyStatMod(flaw, -2);

        // HP from class
        p.HP.Reset(cls.HpPerLevel + Mod(p.Attributes.Con.Value) + 12);

        // Starting equipment
        cls.GrantStartingEquipment?.Invoke(p);

        return p;
    }

    protected void ApplyStatMod(AbilityStat stat, int mod)
    {
        var attr = stat switch
        {
            AbilityStat.Str => Attributes.Str,
            AbilityStat.Dex => Attributes.Dex,
            AbilityStat.Con => Attributes.Con,
            AbilityStat.Int => Attributes.Int,
            AbilityStat.Wis => Attributes.Wis,
            AbilityStat.Cha => Attributes.Cha,
            _ => throw new ArgumentException()
        };
        attr.BaseValue += mod;
    }

    public ProficiencyLevel GetProficiency(string skill)
    {
        var baseLevel = Proficiencies.GetValueOrDefault(skill, ProficiencyLevel.Untrained);
        var fromItems = Query("proficiency", skill, MergeStrategy.Max, 0);
        return (ProficiencyLevel)Math.Max((int)baseLevel, fromItems);
    }

    public void SetProficiency(string skill, ProficiencyLevel level) => Proficiencies[skill] = level;

    public int ProfBonus(string skill)
    {
        var prof = GetProficiency(skill);
        return prof == ProficiencyLevel.Untrained ? 0 : CharacterLevel + (int)prof;
    }

    public static int Mod(int stat) => (stat - 10) / 2;

    public override Glyph Glyph => new('@', ConsoleColor.White);

    public override int GetAC()
    {
        int dexMod = Mod(Attributes.Dex.Value);
        int dexCap = Query("dex_cap", null, MergeStrategy.Min, 99);
        var mods = QueryModifiers("ac");
        // Log.Write("GetAC: dexMod={0} dexCap={1} mods={2}", dexMod, dexCap, mods.Calculate());
        return 10 + Math.Min(dexMod, dexCap) + mods.Calculate();
    }

    public override int GetAttackBonus(WeaponDef weapon) => Mod(Attributes.Str.Value) + ProfBonus(weapon.Profiency);

    public override int GetDamageBonus() => Mod(Attributes.Str.Value);

    public override int GetSpellDC() => 10 + CasterLevel + Mod(Attributes[Class.KeyAbility].Value);

    protected override WeaponDef GetUnarmedDef() => NaturalWeapons.Fist;

    public override bool IsAwareOf(Trap trap) => trap.PlayerSeen;
    public override void ObserveTrap(Trap trap) => trap.PlayerSeen = true;
    public override bool IsDM => false;
    public override int CasterLevel => CharacterLevel;
}

internal class PlayerSkills : LogicBrick
{
  public override object? OnQuery(Fact fact, string key, string? arg)
  {
    return key switch
    {
        "perception" => new Modifier(ModifierCategory.UntypedStackable, u.CharacterLevel + Mod(u.Attributes.Wis.Value)),
        _ => null,
    };
  }
}