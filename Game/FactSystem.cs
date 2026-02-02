using System.Diagnostics.CodeAnalysis;

namespace Pathhack.Game;

public record struct Glyph(char Value, ConsoleColor Color = ConsoleColor.White)
{
  public static readonly Glyph Null = new(' ');
}


public interface IEntity
{
    public IEnumerable<Fact> LiveFacts { get; }
    public IEnumerable<Fact> GetAllFacts(PHContext? ctx);
    public void CleanupMarkedFacts();
    public Fact AddFact(LogicBrick brick, int? duration = null);
    public void RemoveStack(Type type);
    public void RemoveStack<T>() where T : LogicBrick;
    public void DecrementActiveFact();
    public void ExpireFacts();
    object? Query(string key, string? arg = null, MergeStrategy merge = MergeStrategy.Replace);
    T Query<T>(string key, string? arg, MergeStrategy merge, T defaultValue);
    bool Has(string key);
    bool Can(string key);
    bool HasFact<T>() where T : LogicBrick;
}

public class Fact(IEntity entity, LogicBrick brick, object? data)
{
    public IEntity Entity => entity;
    public LogicBrick Brick => brick;
    public object? Data => data;
    public bool MarkedForRemoval;

    public int Stacks { get; set; } = 1;
    public int? ExpiresAt { get; set; }

    public int? RemainingRounds => ExpiresAt.HasValue ? Math.Max(0, ExpiresAt.Value - g.CurrentRound) : null;

    public void Remove()
    {
        Log.Write($"removing fact {Brick} (already? {MarkedForRemoval})");
        if (MarkedForRemoval) return;
        MarkedForRemoval = true;
        LogicBrick.FireOnFactRemoved(Brick, this);
        if (Brick.IsActive)
            Entity.DecrementActiveFact();
        g.PendingFactCleanup.Add(Entity);
    }
}

public enum StackMode { Independent, Stack, Replace, Extend }

public abstract class LogicBrick
{
    public static LogicBrick? GlobalHook;

    public virtual object? CreateData() => null;
    public virtual bool IsBuff => false;
    public virtual string? BuffName => null;
    public virtual bool IsActive => false;
    public virtual StackMode StackMode => StackMode.Independent;
    public virtual int MaxStacks => int.MaxValue;
    public virtual bool RequiresEquipped => false;

    protected virtual object? OnQuery(Fact fact, string key, string? arg) => null;

    protected virtual void OnFactAdded(Fact fact) { }
    protected virtual void OnFactRemoved(Fact fact) { }
    protected virtual void OnStackAdded(Fact fact) { }
    protected virtual void OnStackRemoved(Fact fact) { }

    protected virtual void OnRoundStart(Fact fact, PHContext context) { }
    protected virtual void OnRoundEnd(Fact fact, PHContext context) { }

    protected virtual void OnTurnStart(Fact fact, PHContext context) { }
    protected virtual void OnTurnEnd(Fact fact, PHContext context) { }

    protected virtual void OnBeforeDamageRoll(Fact fact, PHContext context) { }
    protected virtual void OnBeforeDamageIncomingRoll(Fact fact, PHContext context) { }

    protected virtual void OnDamageTaken(Fact fact, PHContext context) { }
    protected virtual void OnDamageDone(Fact fact, PHContext context) { }

    protected virtual void OnBeforeAttackRoll(Fact fact, PHContext context) { }
    protected virtual void OnAfterAttackRoll(Fact fact, PHContext context) { }

    protected virtual void OnBeforeDefendRoll(Fact fact, PHContext context) { }
    protected virtual void OnAfterDefendRoll(Fact fact, PHContext context) { }

    protected virtual void OnBeforeCheck(Fact fact, PHContext context) { }

    protected virtual void OnBeforeHealGiven(Fact fact, PHContext context) { }
    protected virtual void OnBeforeHealReceived(Fact fact, PHContext context) { }
    protected virtual void OnAfterHealReceived(Fact fact, PHContext context) { }

    protected virtual void OnEquip(Fact fact, PHContext context) { }
    protected virtual void OnUnequip(Fact fact, PHContext context) { }

    protected virtual void OnSpawn(Fact fact, PHContext context) { }
    protected virtual void OnDeath(Fact fact, PHContext context) { }

    protected virtual void OnBeforeSpellCast(Fact fact, PHContext context) { }

    // Static dispatch methods - call these instead of instance methods directly
    static bool Skip(LogicBrick b, Fact f) => b.RequiresEquipped && !f.IsEquipped();
    public static object? FireOnQuery(LogicBrick b, Fact f, string key, string? arg) { if (Skip(b, f)) return null; GlobalHook?.OnQuery(f, key, arg); return b.OnQuery(f, key, arg); }
    public static void FireOnFactAdded(LogicBrick b, Fact f) { GlobalHook?.OnFactAdded(f); b.OnFactAdded(f); }
    public static void FireOnFactRemoved(LogicBrick b, Fact f) { GlobalHook?.OnFactRemoved(f); b.OnFactRemoved(f); }
    public static void FireOnStackAdded(LogicBrick b, Fact f) { GlobalHook?.OnStackAdded(f); b.OnStackAdded(f); }
    public static void FireOnStackRemoved(LogicBrick b, Fact f) { GlobalHook?.OnStackRemoved(f); b.OnStackRemoved(f); }
    public static void FireOnRoundStart(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnRoundStart(f, c); b.OnRoundStart(f, c); }
    public static void FireOnRoundEnd(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnRoundEnd(f, c); b.OnRoundEnd(f, c); }
    public static void FireOnTurnStart(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnTurnStart(f, c); b.OnTurnStart(f, c); }
    public static void FireOnTurnEnd(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnTurnEnd(f, c); b.OnTurnEnd(f, c); }
    public static void FireOnBeforeDamageRoll(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnBeforeDamageRoll(f, c); b.OnBeforeDamageRoll(f, c); }
    public static void FireOnBeforeDamageIncomingRoll(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnBeforeDamageIncomingRoll(f, c); b.OnBeforeDamageIncomingRoll(f, c); }
    public static void FireOnDamageTaken(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnDamageTaken(f, c); b.OnDamageTaken(f, c); }
    public static void FireOnDamageDone(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnDamageDone(f, c); b.OnDamageDone(f, c); }
    public static void FireOnBeforeAttackRoll(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnBeforeAttackRoll(f, c); b.OnBeforeAttackRoll(f, c); }
    public static void FireOnAfterAttackRoll(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnAfterAttackRoll(f, c); b.OnAfterAttackRoll(f, c); }
    public static void FireOnBeforeDefendRoll(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnBeforeDefendRoll(f, c); b.OnBeforeDefendRoll(f, c); }
    public static void FireOnAfterDefendRoll(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnAfterDefendRoll(f, c); b.OnAfterDefendRoll(f, c); }
    public static void FireOnBeforeCheck(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnBeforeCheck(f, c); b.OnBeforeCheck(f, c); }
    public static void FireOnBeforeHealGiven(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnBeforeHealGiven(f, c); b.OnBeforeHealGiven(f, c); }
    public static void FireOnBeforeHealReceived(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnBeforeHealReceived(f, c); b.OnBeforeHealReceived(f, c); }
    public static void FireOnAfterHealReceived(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnAfterHealReceived(f, c); b.OnAfterHealReceived(f, c); }
    public static void FireOnEquip(LogicBrick b, Fact f, PHContext c) { GlobalHook?.OnEquip(f, c); b.OnEquip(f, c); }
    public static void FireOnUnequip(LogicBrick b, Fact f, PHContext c) { GlobalHook?.OnUnequip(f, c); b.OnUnequip(f, c); }
    public static void FireOnSpawn(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnSpawn(f, c); b.OnSpawn(f, c); }
    public static void FireOnDeath(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnDeath(f, c); b.OnDeath(f, c); }
    public static void FireOnBeforeSpellCast(LogicBrick b, Fact f, PHContext c) { if (Skip(b, f)) return; GlobalHook?.OnBeforeSpellCast(f, c); b.OnBeforeSpellCast(f, c); }

    // IEntity overloads - fire on all facts via GetAllFacts (null-safe)
    public static void FireOnRoundStart(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnRoundStart(f.Brick, f, c); }
    public static void FireOnRoundEnd(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnRoundEnd(f.Brick, f, c); }
    public static void FireOnBeforeCheck(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnBeforeCheck(f.Brick, f, c); }
    public static void FireOnBeforeDamageRoll(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnBeforeDamageRoll(f.Brick, f, c); }
    public static void FireOnBeforeDamageIncomingRoll(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnBeforeDamageIncomingRoll(f.Brick, f, c); }
    public static void FireOnDamageTaken(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnDamageTaken(f.Brick, f, c); }
    public static void FireOnDamageDone(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnDamageDone(f.Brick, f, c); }
    public static void FireOnBeforeAttackRoll(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnBeforeAttackRoll(f.Brick, f, c); }
    public static void FireOnAfterAttackRoll(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnAfterAttackRoll(f.Brick, f, c); }
    public static void FireOnBeforeDefendRoll(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnBeforeDefendRoll(f.Brick, f, c); }
    public static void FireOnAfterDefendRoll(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnAfterDefendRoll(f.Brick, f, c); }
    public static void FireOnBeforeHealGiven(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnBeforeHealGiven(f.Brick, f, c); }
    public static void FireOnBeforeHealReceived(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnBeforeHealReceived(f.Brick, f, c); }
    public static void FireOnAfterHealReceived(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnAfterHealReceived(f.Brick, f, c); }
    public static void FireOnEquip(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnEquip(f.Brick, f, c); }
    public static void FireOnUnequip(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnUnequip(f.Brick, f, c); }
    public static void FireOnSpawn(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnSpawn(f.Brick, f, c); }
    public static void FireOnDeath(IEntity? e, PHContext c) { if (e == null) return; foreach (var f in e.GetAllFacts(c)) FireOnDeath(f.Brick, f, c); }
}

public class ApplyFactOnAttackHit(LogicBrick toApply, int? duration = null) : LogicBrick
{
    protected override void OnAfterAttackRoll(Fact fact, PHContext context)
    {
        if (context.Check!.Result)
            context.Target?.Unit?.AddFact(toApply, duration);
    }
}
public enum MergeStrategy { Replace, Max, Min, Sum, Or, And }

public enum TargetingType { None, Direction, Unit, Pos }

public abstract class ActionBrick(string name, TargetingType targeting = TargetingType.None)
{
    public string Name => name;
    public TargetingType Targeting => targeting;

    public virtual object? CreateData() => null;
    public virtual ActionCost GetCost(IUnit unit, object? data, Target target) => ActionCosts.OneAction;
    public abstract bool CanExecute(IUnit unit, object? data, Target target, out string whyNot);
    public abstract void Execute(IUnit unit, object? data, Target target);
}

public static class ActionHelpers
{
    public static bool IsAdjacent(IUnit unit, Target target) =>
        target.Unit != null && unit.Pos.ChebyshevDist(target.Unit.Pos) == 1;
}

public static class LogicHelpers
{
    public static LogicBrick ModifierBrick(string key, ModifierCategory cat, int value, string why) =>
        new QueryBrick(key, new Modifier(cat, value, why));
}

public class QueryBrick(string queryKey, object value) : LogicBrick
{
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == queryKey ? value : null;
}

public class AttackWithWeapon() : ActionBrick("attack_with_weapon")
{
    public static readonly AttackWithWeapon Instance = new();

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "no target in range";

        if (target.Unit == null) return false;
        int dist = unit.Pos.ChebyshevDist(target.Unit.Pos);
        if (dist == 1) return true; // melee
        
        var weapon = unit.GetWieldedItem().Def as WeaponDef;
        if (weapon?.Launcher == null) return false;
        // TODO: check we are wielding the correct launcher
        return dist <= 4 && target.Unit.Pos.IsCompassFrom(unit.Pos) && (unit as Monster)?.CanSeeYou == true;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        int dist = unit.Pos.ChebyshevDist(target.Unit!.Pos);
        if (dist > 1)
        {
            Pos dir = (target.Unit.Pos - unit.Pos).Signed;
            var weapon = unit.GetWieldedItem();
            Item toThrow;
            if (weapon.Count > 1)
                toThrow = weapon.Split(1);
            else
            {
                toThrow = weapon;
                unit.Inventory.Remove(weapon);
            }
            g.DoThrow(unit, toThrow, dir);
        }
        else
        {
            g.Attack(unit, target.Unit, unit.GetWieldedItem());
        }
    }
}

public class NaturalAttack(WeaponDef weapon) : ActionBrick("attack_with_nat")
{
    public WeaponDef Weapon => weapon;
    readonly Item _item = new(weapon);

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "no target in range";
        return ActionHelpers.IsAdjacent(unit, target);
    }

    public override void Execute(IUnit unit, object? data, Target target) =>
        g.Attack(unit, target.Unit!, _item);
}

public class GrantProficiency(string skill, ProficiencyLevel level, bool requiresEquipped = false) : LogicBrick
{
    public override bool RequiresEquipped => requiresEquipped;
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "proficiency" && arg == skill ? (int)level : null;
}

public static class EntityExts
{
    public static bool IsEquipped(this Fact fact) => fact.Entity is Item item && item.Holder?.Equipped.ContainsValue(item) == true;

}

public class ArmorBrick(int acBonus, int dexCap) : LogicBrick
{
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        fact.IsEquipped() ? key switch
        {
            "ac" => new Modifier(ModifierCategory.ItemBonus, acBonus),
            "dex_cap" => dexCap,
            _ => null,
        } : null;
}

public class BaseDef
{
    public string id = "";
    public LogicBrick[] Components = [];
}

public class Entity<DefT> : IEntity where DefT : BaseDef
{
    public readonly DefT Def;
    public int ActiveFactCount;
    
    protected Entity(DefT def)
    {
        Def = def;
        foreach (var c in Def.Components)
            AddFact(c);
    }
    private readonly List<Fact> Facts = [];

    public IEnumerable<Fact> LiveFacts => Facts.Where(x => !x.MarkedForRemoval);

    public virtual IEnumerable<Fact> GetAllFacts(PHContext? ctx) => LiveFacts;

    public void ShareFactsFrom(Entity<DefT> other)
    {
        foreach (var fact in other.LiveFacts)
            Facts.Add(fact);
    }

    public Fact AddFact(LogicBrick brick, int? duration = null)
    {
        if (brick.StackMode == StackMode.Stack)
        {
            var existing = LiveFacts.FirstOrDefault(f => f.Brick.GetType() == brick.GetType());
            if (existing != null)
            {
                if (existing.Stacks < brick.MaxStacks)
                {
                    existing.Stacks++;
                    LogicBrick.FireOnStackAdded(existing.Brick, existing);
                }
                return existing;
            }
        }

        var fact = new Fact(this, brick, brick.CreateData());
        if (duration.HasValue)
            fact.ExpiresAt = g.CurrentRound + duration.Value;
        Facts.Add(fact);
        Log.Write($"add fact {fact.Brick} => {this}");
        if (brick.IsActive)
        {
            if (ActiveFactCount++ == 0 && this is not IUnit)
                g.ActiveEntities.Add(this);
        }
        LogicBrick.FireOnFactAdded(brick, fact);
        return fact;
    }

    public void RemoveStack(Type type)
    {
        var fact = LiveFacts.FirstOrDefault(f => f.Brick.GetType() == type);
        if (fact == null) return;
        fact.Stacks--;
        LogicBrick.FireOnStackRemoved(fact.Brick, fact);
        if (fact.Stacks <= 0)
            fact.Remove();
    }

    public void RemoveStack<T>() where T : LogicBrick => RemoveStack(typeof(T));

    public void ExpireFacts()
    {
        foreach (var fact in LiveFacts)
            if (fact.ExpiresAt.HasValue && g.CurrentRound >= fact.ExpiresAt.Value)
                fact.Remove();
    }

    public void CleanupMarkedFacts() => Facts.RemoveAll(f => f.MarkedForRemoval);

    public void DecrementActiveFact()
    {
        if (--ActiveFactCount == 0 && this is not IUnit)
            g.ActiveEntities.Remove(this);
    }

    public virtual object? Query(string key, string? arg = null, MergeStrategy merge = MergeStrategy.Replace)
    {
        object? result = null;
        foreach (var fact in LiveFacts)
            result = Merge(result, LogicBrick.FireOnQuery(fact.Brick, fact, key, arg), merge);
        return result;
    }

    public virtual T Query<T>(string key, string? arg, MergeStrategy merge, T defaultValue) =>
        Query(key, arg, merge) is T v ? v : defaultValue;

    public virtual bool Has(string key) => Query(key, null, MergeStrategy.Or, false);
    public virtual bool Can(string key) => Query(key, null, MergeStrategy.And, true);

    public bool HasFact<T>() where T : LogicBrick => LiveFacts.Any(f => f.Brick is T);

    protected static object? Merge(object? current, object? next, MergeStrategy strategy)
    {
        if (next == null) return current;
        if (current == null) return next;
        return strategy switch
        {
            MergeStrategy.Replace => next,
            MergeStrategy.Max => Math.Max(Convert.ToInt32(current), Convert.ToInt32(next)),
            MergeStrategy.Min => Math.Min(Convert.ToInt32(current), Convert.ToInt32(next)),
            MergeStrategy.Sum => Convert.ToInt32(current) + Convert.ToInt32(next),
            MergeStrategy.Or => Convert.ToBoolean(current) || Convert.ToBoolean(next),
            MergeStrategy.And => Convert.ToBoolean(current) && Convert.ToBoolean(next),
            _ => next,
        };
    }
}

public class Inventory(IUnit owner) : IEnumerable<Item>
{
    const char NoSym = '#';
    int _lastInvNr = -1;

    readonly List<Item> Items = [];
    ulong inUse;

    public IEnumerator<Item> GetEnumerator() => Items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(Item item)
    {
        int idx = LetterToIndex(item.InvLet);
        if (idx >= 0 && (inUse & (1UL << idx)) == 0)
        {
            inUse |= 1UL << idx;
            item.Holder = owner;
            Items.Add(item);
            return;
        }

        for (int i = (_lastInvNr + 1) % 52; ; i = (i + 1) % 52)
        {
            if ((inUse & (1UL << i)) == 0)
            {
                item.InvLet = IndexToLetter(i);
                inUse |= 1UL << i;
                _lastInvNr = i;
                item.Holder = owner;
                Items.Add(item);
                return;
            }
        }
    }

    public bool Remove(Item item)
    {
        if (!Items.Remove(item)) return false;
        int idx = LetterToIndex(item.InvLet);
        if (idx >= 0) inUse &= ~(1UL << idx);
        
        // auto-unequip if equipped
        foreach (var slot in owner.Equipped.Where(kv => kv.Value == item).Select(kv => kv.Key).ToList())
            owner.Unequip(slot);
        
        item.Holder = null;
        return true;
    }

    public bool Contains(Item item) => Items.Contains(item);

    static int LetterToIndex(char c) => c switch
    {
        >= 'a' and <= 'z' => c - 'a',
        >= 'A' and <= 'Z' => c - 'A' + 26,
        _ => -1
    };

    static char IndexToLetter(int i) => i < 26 ? (char)('a' + i) : (char)('A' + i - 26);
}

public class Hitpoints
{
    public int Max;
    public int Current;

    public void Reset(int max)
    {
        Max = max;
        Current = max;
    }

    public bool IsZero => Current <= 0;

    public int Heal(int amount)
    {
        int before = Current;
        Current = Math.Min(Current + amount, Max);
        return Current - before;
    }

    public static Hitpoints operator -(Hitpoints hp, int delta) => new()
    {
        Max = hp.Max,
        Current = Math.Clamp(hp.Current - delta, 0, hp.Max),
    };

    public static Hitpoints operator +(Hitpoints hp, int delta) => new()
    {
        Max = hp.Max,
        Current = Math.Clamp(hp.Current + delta, 0, hp.Max),
    };
}

public static class UnitExts
{
    public static bool IsNullOrDead([NotNullWhen(false)] this IUnit? unit) => unit == null || unit.IsDead;
}

public interface IUnit : IEntity
{
    bool IsPlayer { get; }
    bool IsDead { get; set; }
    Hitpoints HP { get; set; }
    Pos Pos { get; set; }
    int Energy { get; set; }
    int Initiative { get; set; }
    Glyph Glyph { get; }
    Dictionary<EquipSlot, Item> Equipped { get; }
    Inventory Inventory { get; }
    List<ActionBrick> Actions { get; }
    Dictionary<ActionBrick, object?> ActionData { get; }
    void AddAction(ActionBrick action);
    void AddSpell(SpellBrickBase spell);
    IEnumerable<Fact> Facts { get; }
    ActionCost LandMove { get; }

    public int NaturalRegen { get; }
    public int StrMod { get; }

    Trap? TrappedIn { get; set; }
    int EscapeAttempts { get; set; }
    bool IsDM { get; }
    int CasterLevel { get; }

    int GetAC();
    int GetAttackBonus(WeaponDef weapon);
    int GetDamageBonus();
    int GetSpellDC();
    Item GetWieldedItem();
    bool Equip(Item item);
    bool Unequip(EquipSlot slot);
    Modifiers QueryModifiers(string key, string? arg = null);

    public bool IsAwareOf(Trap trap);
    public void ObserveTrap(Trap trap);

    void AddPool(string name, int max, int regenRate);
    bool HasCharge(string name, out string whyNot);
    bool TryUseCharge(string name);
    void TickPools();
    ChargePool? GetPool(string name);
}

public class ChargePool(int max, int regenRate)
{
    public int Current = max;
    public int Max = max;
    public int RegenRate = regenRate;
    public int Ticks;

    public void Tick()
    {
        if (Current < Max && ++Ticks >= RegenRate)
        {
            Current++;
            Ticks = 0;
        }
    }
}

public abstract class Unit<TDef>(TDef def) : Entity<TDef>(def), IUnit where TDef : BaseDef
{
    public Dictionary<EquipSlot, Item> Equipped { get; } = [];
    Inventory? _inventory;
    public Inventory Inventory => _inventory ??= new(this);
    public List<ActionBrick> Actions { get; } = [];
    public List<SpellBrickBase> Spells { get; } = [];
    public Dictionary<ActionBrick, object?> ActionData { get; } = [];

    public override IEnumerable<Fact> GetAllFacts(PHContext? ctx)
    {
        foreach (var f in LiveFacts) yield return f;
        foreach (var item in Inventory)
            foreach (var f in item.LiveFacts) yield return f;
        if (ctx?.Weapon != null && !Inventory.Contains(ctx.Weapon))
            foreach (var f in ctx.Weapon.LiveFacts) yield return f;
    }
    IEnumerable<Fact> IUnit.Facts => LiveFacts;
    readonly Dictionary<string, ChargePool> Pools = [];
    public abstract bool IsDM { get; }
    public abstract int CasterLevel { get; }

    public Trap? TrappedIn { get; set; }
    public int EscapeAttempts { get; set; }

    public abstract bool IsAwareOf(Trap trap);
    public abstract void ObserveTrap(Trap trap);

    public void AddPool(string name, int max, int regenRate)
    {
        if (Pools.TryGetValue(name, out var existing))
        {
            existing.Max = Math.Max(existing.Max, max);
            existing.Current = Math.Max(existing.Current, max);
            existing.RegenRate = Math.Min(existing.RegenRate, regenRate);
        }
        else
            Pools[name] = new(max, regenRate);
    }
    public bool HasCharge(string name, out string whyNot)
    {
        whyNot = "no charges";
        return Pools.TryGetValue(name, out var p) && p.Current > 0;
    }
    public bool TryUseCharge(string name)
    {
        if (!Pools.TryGetValue(name, out var p) || p.Current <= 0) return false;
        p.Current--;
        return true;
    }
    public void TickPools() { foreach (var p in Pools.Values) p.Tick(); }
    public ChargePool? GetPool(string name) => Pools.GetValueOrDefault(name);

    public abstract int NaturalRegen { get; }
    public abstract int StrMod { get; }

    public void AddSpell(SpellBrickBase spell)
    {
        Spells.Add(spell);
        ActionData[spell] = spell.CreateData();
    }

    public void AddAction(ActionBrick action)
    {
        Actions.Add(action);
        ActionData[action] = action.CreateData();
    }

    public abstract ActionCost LandMove { get; }
    public override bool Has(string key) => Query<bool>(key, null, MergeStrategy.Or, false);

    public override object? Query(string key, string? arg = null, MergeStrategy merge = MergeStrategy.Replace)
    {
        object? result = null;
        foreach (var fact in LiveFacts)
            result = Merge(result, LogicBrick.FireOnQuery(fact.Brick, fact, key, arg), merge);
        foreach (var item in Inventory)
            foreach (var fact in item.LiveFacts)
                result = Merge(result, LogicBrick.FireOnQuery(fact.Brick, fact, key, arg), merge);
        return result;
    }
    public override T Query<T>(string key, string? arg, MergeStrategy merge, T defaultValue) =>
        Query(key, arg, merge) is T v ? v : defaultValue;
    public Modifiers QueryModifiers(string key, string? arg = null)
    {
        var mods = new Modifiers();
        foreach (var fact in LiveFacts)
            if (LogicBrick.FireOnQuery(fact.Brick, fact, key, arg) is Modifier m)
                mods.AddModifier(m);
        foreach (var item in Inventory)
            foreach (var fact in item.LiveFacts)
                if (LogicBrick.FireOnQuery(fact.Brick, fact, key, arg) is Modifier m)
                    mods.AddModifier(m);
        return mods;
    }

    public abstract bool IsPlayer { get; }
    public bool IsDead { get; set; }

    public Hitpoints HP { get; set; } = new();
    public Pos Pos { get; set; }
    public int Energy { get; set; }
    public int Initiative { get; set; }
    public abstract Glyph Glyph { get; }

    Item? _unarmedItem;

    public abstract int GetAC();
    public abstract int GetAttackBonus(WeaponDef weapon);
    public abstract int GetDamageBonus();
    public abstract int GetSpellDC();
    protected abstract WeaponDef GetUnarmedDef();

    public Item GetWieldedItem()
    {
        EquipSlot handSlot = new(ItemSlots.Hand, "_");
        if (Equipped.TryGetValue(handSlot, out var item) && item.Def is WeaponDef)
            return item;
        return _unarmedItem ??= new(GetUnarmedDef());
    }

    public bool Equip(Item item)
    {
        if (!Inventory.Contains(item))
            throw new InvalidOperationException("Item not in inventory");
        if (Equipped.ContainsValue(item))
            return false;

        EquipSlot mainSlot = new(item.Def.DefaultEquipSlot, "_");
        EquipSlot offSlot = new(item.Def.DefaultEquipSlot, "off");

        int hands = item.Def is WeaponDef w ? w.Hands : 1;
        if (hands == 2)
        {
            if (Equipped.ContainsKey(mainSlot) || Equipped.ContainsKey(offSlot))
                return false;
            Equipped[mainSlot] = item;
            Equipped[offSlot] = item;
        }
        else
        {
            if (Equipped.ContainsKey(mainSlot))
                return false;
            Equipped[mainSlot] = item;
        }

        using var ctx = PHContext.Create(this, Target.None);
        LogicBrick.FireOnEquip(item, ctx);
        Log.Write("equip: {0}", item.Def.Name);
        return true;
    }

    public bool Unequip(EquipSlot slot)
    {
        if (!Equipped.TryGetValue(slot, out var item)) return false;

        Equipped.Remove(slot);
        // Remove from off hand too if 2h
        EquipSlot offSlot = new(slot.Type, "off");
        if (Equipped.TryGetValue(offSlot, out var offItem) && offItem == item)
            Equipped.Remove(offSlot);
        // Remove from main hand if unequipping off
        EquipSlot mainSlot = new(slot.Type, "_");
        if (Equipped.TryGetValue(mainSlot, out var mainItem) && mainItem == item)
            Equipped.Remove(mainSlot);

        using var ctx = PHContext.Create(this, Target.None);
        LogicBrick.FireOnUnequip(item, ctx);
        Log.Write("unequip: {0}", item.Def.Name);
        return true;
    }
}

public class GrantAction(ActionBrick action) : LogicBrick
{
    public ActionBrick Action => action;
  protected override void OnFactAdded(Fact fact) => (fact.Entity as IUnit)?.AddAction(action);
}

public class GrantSpell(SpellBrickBase spell) : LogicBrick
{
    public SpellBrickBase Spell => spell;
    protected override void OnFactAdded(Fact fact) => (fact.Entity as IUnit)?.AddSpell(spell);
}

public class GrantPool(string name, int max, int regenRate) : LogicBrick
{
    protected override void OnFactAdded(Fact fact)
    {
        Log.Write($"on fact added pool {name} to {fact.Entity}");
        (fact.Entity as IUnit)?.AddPool(name, max, regenRate);
    } 
}

