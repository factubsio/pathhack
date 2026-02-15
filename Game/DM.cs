namespace Pathhack.Game;

/// <summary>
/// The Dungeon Master — a sourceless IUnit used as the origin for environmental effects,
/// traps, bottles, and anything else that isn't cast by a real unit.
/// Has infinite charge pools so spells execute without consuming resources.
/// </summary>
public class DungeonMaster : Entity<BaseDef>, IUnit
{
    public static DungeonMaster WithDC(int dc) => new(dc);
    // intentional clamp to 30 to allow bosses and shit
    public static int DCForLevel(int level) => 12 + (Math.Clamp(level, 1, 30) + 1) / 2;
    public static DungeonMaster AsLevel(int level) => new(DCForLevel(level));

    readonly int _dc;
    internal static readonly DungeonMaster Mook = AsLevel(1);

    DungeonMaster(int dc = 15) : base(new BaseDef { id = "dm" }, [])
    {
        _dc = dc;
    }

    public bool IsPlayer => false;
    public bool IsDead { get; set; }
    public bool IsDM => true;
    public string? ProperName { get; set; } = "DM";
    public Hitpoints HP { get; set; } = new() { BaseMax = 1000, Current = 1000, Max = 1000 };
    public Pos Pos { get; set; } = new(-100, -100);
    public int Energy { get; set; }
    public int Initiative { get; set; }
    public Glyph Glyph => Glyph.Null;
    public Dictionary<EquipSlot, Item> Equipped { get; } = [];
    Inventory? _inventory;

    public Inventory Inventory => _inventory ??= new(this);
    public List<ActionBrick> Actions { get; } = [];
    public Dictionary<ActionBrick, object?> ActionData { get; } = [];
    public ActionCost LandMove => 12;
    public int NaturalRegen => 0;
    public int StrMod => 0;
    public int CasterLevel => 20;
    public Trap? TrappedIn { get; set; }
    public int EscapeAttempts { get; set; }
    public IUnit? GrabbedBy { get; set; }
    public IUnit? Grabbing { get; set; }
    public MoveMode CurrentMoveMode { get; set; } = MoveMode.Walk;
    public MoralAxis MoralAxis => MoralAxis.Neutral;
    public EthicalAxis EthicalAxis => EthicalAxis.Neutral;
    public int HitsTaken { get; set; }
    public int MissesTaken { get; set; }
    public int DamageTaken { get; set; }
    public int TempHp => 0;
    public int LastDamagedOnTurn { get; set; }

    public bool IsCreature(string? type = null, string? subtype = null) => false;
    public int GetAC() => 0;
    public int GetAttackBonus(WeaponDef weapon) => 0;
    public int GetSpellAttackBonus(SpellBrickBase spell) => 0;
    public int GetDamageBonus() => 0;
    public int GetSpellDC() => _dc;
    public Item GetWieldedItem() => new(NaturalWeapons.Fist);

    public EquipSlot? Equip(Item item) => null;
    public UnequipResult Unequip(EquipSlot slot, bool force = false) => UnequipResult.Empty;
    public Modifiers QueryModifiers(string key, string? arg = null) => new();
    public List<Fact> QueryFacts(string key, string? arg = null) => [];

    public bool IsAwareOf(Trap trap) => true;
    public void ObserveTrap(Trap trap) { }

    public void AddAction(ActionBrick action) { }
    public void RemoveAction(ActionBrick action) { }
    public void AddSpell(SpellBrickBase spell) { }

    // Infinite charges — the whole point
    public void AddPool(string name, int max, DiceFormula regenRate) { }
    public bool HasCharge(string name, out string whyNot) { whyNot = ""; return true; }
    public bool TryUseCharge(string name) => true;
    public void TickPools() { }
    public ChargePool? GetPool(string name) => null;

    public void GrantTempHp(int amount) { }
    public int AbsorbTempHp(int damage, out int absorbed) { absorbed = 0; return damage; }
    public void TickTempHp() { }

    IEnumerable<Fact> IEntity.LiveFacts => [];
    IEnumerable<Fact> IEntity.GetOwnFacts() => [];
    IEnumerable<Fact> IUnit.Facts => [];

    public override string ToString() => "the dungeon";

    internal IUnit At(Pos pos)
    {
        Pos = pos;
        return this;
    }
}
