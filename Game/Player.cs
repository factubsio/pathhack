using System.Security.Principal;

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

    public override MoralAxis MoralAxis => Deity.Moral;
    public override EthicalAxis EthicalAxis => Deity.Ethical;
    public override bool IsCreature(string? type = null, string? subtype = null) =>
      (type == null || type == CreatureTypes.Humanoid) && (subtype == null || Has(subtype));

    public LevelId Level { get; set; }
    public int DarkVisionRadius => Math.Clamp(Ancestry.DarkVisionRadius + QueryModifiers("light_radius").Calculate(), 0, 100);
    public override ActionCost LandMove
    {
        get
        {
            int cost = ActionCosts.StandardLandMove.Value - QueryModifiers("speed_bonus").Calculate();
            double mult = Query<double>("speed_mult", null, MergeStrategy.Replace, 1.0);
            return (int)(cost / mult);
        }
    }

    public ValueStatBlock<int> BaseAttributes;
    public readonly int BaseLandSpeed = ActionCosts.StandardLandMove.Value;
    private readonly Dictionary<string, ProficiencyLevel> Proficiencies = [];
    public int CharacterLevel = 0;
    public int XP = 0;
    public int Nutrition = Hunger.Satiated - 1;
    public long Gold;
    public int HippoCounter;
    public Activity? CurrentActivity;
    public HashSet<string> TakenFeats = [];
    public Dictionary<LevelId, HashSet<string>> SeenFeatures = [];

    public required ClassDef Class = null!;
    public required DeityDef Deity = null!;
    public required AncestryDef Ancestry = null!;
    public Item? Quiver;

    Room? _currentRoom;
    public Room? CurrentRoom
    {
        get => _currentRoom;
        set
        {
            if (_currentRoom == value) return;
            var oldRoom = _currentRoom;
            _currentRoom = value;
            OnRoomChange(oldRoom, value);
        }
    }

    private static void OnRoomChange(Room? from, Room? to)
    {
        // Theft check
        if (from?.Type == RoomType.Shop && from.Resident is { } shk)
        {
            var shop = shk.FindFact(ShopkeeperBrick.Instance)?.As<ShopState>();
            if (shop != null && shop.Bill > 0)
            {
                if (shk.Peaceful)
                {
                    g.pline($"{shk:The} shouts: \"Thief!\"");
                    shk.Peaceful = false;
                }

                foreach (var item in shop.UnpaidItems())
                    item.Stolen = true;
            }
        }

        // Room entry message (first time only)
        if (to != null && to.Entered && to.Type == RoomType.Shop && to.Resident is Monster shk2)
        {
            var state2 = shk2.FindFact(ShopkeeperBrick.Instance)?.As<ShopState>();
            if (shk2.Peaceful && state2 != null)
                g.pline($"{shk2.ProperName} says: \"Welcome again to {shk2.ProperName}'s {ShopTypes.DisplayName(state2.Type)}!\"");
            else
                g.pline($"{shk2.ProperName} says: \"Criminal! I'll get you!\"");
        }
        else if (to != null && !to.Entered)
        {
            to.Entered = true;
            string? msg = null;
            if (to.Type == RoomType.Shop && to.Resident is Monster shkNew)
            {
                var state = shkNew.FindFact(ShopkeeperBrick.Instance)?.As<ShopState>();
                if (shkNew.Peaceful && state != null)
                    msg = $"{shkNew.ProperName} says: \"Welcome to {shkNew.ProperName}'s {ShopTypes.DisplayName(state.Type)}!\"";
                else if (shkNew is { Peaceful: false })
                    msg = $"{shkNew.ProperName} says: \"I saw your wanted poster, you're mine!\"";
            }
            else
            {
                msg = to.Type switch
                {
                    RoomType.GoblinNest => "You find a goblin prayer circle.",
                    RoomType.GremlinParty => "You stumble across the aftermath of a gremlin party.",
                    RoomType.GremlinPartyBig => "You enter the chaos of a gremlin bender.",
                    _ => null
                };
            }
            if (msg != null) g.pline(msg);
        }
    }

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
        p.AddAction(DismissAction.Instance);

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

    public (ProficiencyLevel Level, string Source) GetProficiency(WeaponDef weapon)
    {
        int fromGroup = (int)GetProficiency(weapon.Profiency);
        int fromType = weapon.WeaponType != null ? (int)GetProficiency(weapon.WeaponType) : 0;

        if (fromType > fromGroup)
            return ((ProficiencyLevel)fromType, weapon.WeaponType!);
        else
            return ((ProficiencyLevel)fromGroup, weapon.Profiency);
    }

    public override int GetAttackBonus(WeaponDef weapon) => StrMod + (int)GetProficiency(weapon).Level;
    public override int GetSpellAttackBonus(SpellBrickBase brick) => DexMod + ProfBonus("spell_attack");

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

    const int TrackSize = 200;
    readonly Pos[] _track = new Pos[TrackSize];
    int _trackCount;
    int _trackPtr;

    public void RecordTrack()
    {
        if (_trackCount < TrackSize) _trackCount++;
        if (_trackPtr == TrackSize) _trackPtr = 0;
        _track[_trackPtr++] = upos;
    }

    public void ClearTrack()
    {
        _trackCount = 0;
        _trackPtr = 0;
    }

    public Pos? GetTrack(Pos from, int maxTrack = 0)
    {
        int cnt = maxTrack <= 0 ? _trackCount : Math.Min(maxTrack, _trackCount);
        int ptr = _trackPtr;
        for (; cnt > 0; cnt--)
        {
            if (ptr == 0) ptr = TrackSize;
            ptr--;
            int dist = from.ChebyshevDist(_track[ptr]);
            if (dist == 1) return _track[ptr];
            if (dist == 0) return null;
        }
        return null;
    }
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
            Check.Reflex => new Modifier(ModifierCategory.StatBonus, u.DexMod),
            Check.Fort => new Modifier(ModifierCategory.StatBonus, u.ConMod),
            Check.Will => new Modifier(ModifierCategory.StatBonus, u.WisMod),
            _ => null,
        };
    }
}