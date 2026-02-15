// SPLIT: move to UnitSystem.cs:
//   GrantProficiency, EntityExts, ArmorBrick  (~lines 393-421)
//   Inventory, Hitpoints, UnitExts            (~lines 582-716)
//   IUnit, ChargePool                         (~lines 718-804)
//   Unit<T>                                   (~lines 806-1062)
//   GrantAction, GrantSpell, GrantPool        (~lines 1064-1084)
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Pathhack.Game;

public enum UnequipResult { Empty, Ok, Cursed }

// This is a valiant attempt to not leak Draw into here, is it worth it?
[Flags]
public enum GlyphFlags : byte
{
    None = 0,
    Bold = 1,
    Underline = 2,
    Reverse = 4,
}

public record struct Glyph(char Value, ConsoleColor Color = ConsoleColor.White, ConsoleColor? Background = null, GlyphFlags Flags = GlyphFlags.None)
{
    public static readonly Glyph Null = new(' ');
}


public interface IEntity
{
    static uint NextId;
    public uint Id { get; }
    public IEnumerable<Fact> LiveFacts { get; }
    public IEnumerable<Fact> GetAllFacts(PHContext? ctx);
    public IEnumerable<Fact> GetOwnFacts();
    public void CleanupMarkedFacts();
    public Fact AddFact(LogicBrick brick, int? duration = null, int count = 1);
    public Fact? FindFact(LogicBrick brick);
    public void RemoveStack(LogicBrick brick, int count = 1);
    public void DecrementActiveFact();
    public void ExpireFacts();
    object? Query(string key, string? arg = null, MergeStrategy merge = MergeStrategy.Replace);
    T Query<T>(string key, string? arg, MergeStrategy merge, T defaultValue);
    bool Has(string key);
    bool Allows(string key);
    bool HasFact(LogicBrick brick);
    IEnumerable<string> ActiveBuffNames { get; }

    int EffectiveLevel { get; }
}

public class Fact(IEntity entity, LogicBrick brick, object? data)
{
    public IEntity Entity => entity;
    public LogicBrick Brick { get; set; } = brick;
    public object? Data => data;
    public bool MarkedForRemoval;

    public T As<T>() => (T)data!;

    public int Stacks { get; set; } = 1;
    public int? ExpiresAt { get; set; }

    public int? RemainingRounds => ExpiresAt.HasValue ? Math.Max(0, ExpiresAt.Value - g.CurrentRound) : null;

    public string DisplayName
    {
        get
        {
            List<string> parts = [];
            if (Brick.DisplayMode.HasFlag(FactDisplayMode.Name))
                parts.Add(Brick.BuffName ?? Brick.GetType().Name);
            if (Brick.DisplayMode.HasFlag(FactDisplayMode.Stacks))
                parts.Add(Stacks.ToString());
            if (Brick.DisplayMode.HasFlag(FactDisplayMode.Duration))
                parts.Add($"{RemainingRounds} left");

            return string.Join(" ", parts);
        }
    }

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

public enum StackMode { Independent, Stack, ExtendDuration, ExtendStacks }

[Flags]
public enum FactDisplayMode
{
    Name = 1,
    Stacks = 2,
    Duration = 4,
}

public abstract class LogicBrick
{
    public static LogicBrick? GlobalHook;

    public abstract string Id { get; }

    public virtual LogicBrick? MergeWith(LogicBrick other) => this == other ? this : null;

    public virtual object? CreateData() => null;
    public virtual bool IsBuff => false;
    public virtual string? BuffName => null;
    public virtual bool IsActive => false;
    public virtual StackMode StackMode => StackMode.Independent;
    public virtual FactDisplayMode DisplayMode => FactDisplayMode.Name;
    public virtual int MaxStacks => int.MaxValue;
    public virtual bool RequiresEquipped => false;
    public virtual string? PokedexDescription => null;
    public virtual AbilityTags Tags => AbilityTags.None;

    protected virtual object? OnQuery(Fact fact, string key, string? arg) => null;

    protected virtual void OnFactAdded(Fact fact) { }
    protected virtual void OnFactRemoved(Fact fact) { }
    protected virtual void OnStackAdded(Fact fact) { }
    protected virtual void OnStackRemoved(Fact fact) { }

    protected virtual void OnRoundStart(Fact fact) { }
    protected virtual void OnRoundEnd(Fact fact) { }

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
    public static void FireOnRoundStart(LogicBrick b, Fact f) { if (Skip(b, f)) return; GlobalHook?.OnRoundStart(f); b.OnRoundStart(f); }
    public static void FireOnRoundEnd(LogicBrick b, Fact f) { if (Skip(b, f)) return; GlobalHook?.OnRoundEnd(f); b.OnRoundEnd(f); }
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
    public static void FireOnRoundStart(IEntity? e) { if (e == null) return; foreach (var f in e.GetOwnFacts()) FireOnRoundStart(f.Brick, f); }
    public static void FireOnRoundEnd(IEntity? e) { if (e == null) return; foreach (var f in e.GetOwnFacts()) FireOnRoundEnd(f.Brick, f); }
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

public class DataFlag
{
    public bool On;

    public static implicit operator bool(DataFlag flag) => flag.On;
}

public class ScalarData<T>(T val) where T : struct
{
    public T Value = val;

    public static implicit operator T(ScalarData<T> val) => val.Value;
}

public abstract class LogicBrick<T> : LogicBrick where T : class, new()
{
  public sealed override object? CreateData() => new T();
  protected static T X(Fact fact) => (T)fact.Data!;
}

public enum MergeStrategy { Replace, Max, Min, Sum, Or, And }

public enum TargetingType { None, Direction, Unit, Pos }

[Flags]
public enum AbilityTags
{
    None       = 0,
    Harmful    = 1 << 0,
    Beneficial = 1 << 1,
    Heal       = 1 << 2,  // only self-cast when below 50% HP
    Mental     = 1 << 3,  // blocked by mindless
    Verbal     = 1 << 4,  // blocked by silence
    AoE        = 1 << 5,  // AI avoids if allies in blast
    Evil       = 1 << 6,
    Holy       = 1 << 7,
    Biological = 1 << 8,  // stripped by undead templates
    FirstSpawnOnly  = 1 << 9,  // skipped on respawn
}

public enum ToggleState { NotAToggle, Off, On }

public record struct ActionPlan(bool Ok, string WhyNot = "", object? Plan = null)
{
    public static implicit operator ActionPlan(bool ok) => new(ok);
    public static implicit operator bool(ActionPlan p) => p.Ok;
    public static implicit operator ActionPlan(string whyNot) => new(false, whyNot);
}

public abstract class ActionBrick(string name, TargetingType targeting = TargetingType.None, int maxRange = -1, AbilityTags tags = AbilityTags.None)
{
    public int MaxRange => maxRange;
    public string Name => name;
    public TargetingType Targeting => targeting;
    public AbilityTags Tags => tags;
    public virtual string? PokedexDescription => null;

    public int EffectiveMaxRange => maxRange < 0 ? 101 : maxRange;

    public virtual ToggleState IsToggleOn(object? data) => ToggleState.NotAToggle;

    public virtual object? CreateData() => null;
    public virtual ActionCost GetCost(IUnit unit, object? data, Target target) => ActionCosts.OneAction;
    public abstract ActionPlan CanExecute(IUnit unit, object? data, Target target);
    public abstract void Execute(IUnit unit, object? data, Target target, object? plan = null);

    protected static ActionPlan Always() => true;
}

public abstract class CooldownAction(string name, TargetingType target, Func<IUnit, int> cooldown, int maxRange = -1) : ActionBrick(name, target, maxRange)
{
    public class CooldownTracker(Func<IUnit, int> cd)
    {
        public int CooldownUntil;

        public bool CanExecute(out string whyNot)
        {
            int remaining = CooldownUntil - g.CurrentRound;
            whyNot = $"{remaining} rounds left";
            return remaining <= 0;
        }

        public void OnActivate(IUnit unit)
        {
            CooldownUntil = g.CurrentRound + cd(unit);
        }
    }

    public sealed override object? CreateData() => new CooldownTracker(cooldown);

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var tracker = (CooldownTracker)data!;
        int remaining = tracker.CooldownUntil - g.CurrentRound;
        return remaining <= 0 ? true : new ActionPlan(false, $"{remaining} rounds left");
    }

    public sealed override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        ((CooldownTracker)data!).OnActivate(unit);
        Execute(unit, target);
    }

    public static void SetCooldownMax(IUnit unit, object? data)
    {
        ((CooldownTracker)data!).OnActivate(unit);

    }

    protected abstract void Execute(IUnit unit, Target target);
}

public abstract class SimpleToggleAction<T>(string name, T fact) : ActionBrick(name, TargetingType.None) where T : LogicBrick
{
    public override ToggleState IsToggleOn(object? data) => ((DataFlag)data!).On ? ToggleState.On : ToggleState.Off;

    public override object? CreateData() => new DataFlag();

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        DataFlag dat = (DataFlag)data!;
        dat.On = !dat.On;
        if (dat.On)
            unit.AddFact(fact);
        else
            unit.RemoveStack(fact);
    }
}

public static class ActionHelpers
{
    public static bool IsAdjacent(this IUnit unit, Target target) =>
        target.Unit != null && unit.Pos.ChebyshevDist(target.Unit.Pos) == 1;
    public static ActionPlan IsAdjacentPlan(this IUnit unit, Target target) =>
        unit.IsAdjacent(target) ? true : new ActionPlan(false, "not adjacent");
}

public static class EntityExts
{
    public static bool IsEquipped(this Fact fact) => fact.Entity is Item item && item.Holder?.Equipped.ContainsValue(item) == true;
    public static bool IsKnown(this ItemDef def) => ItemDb.Instance.IsIdentified(def);
    public static void SetKnown(this ItemDef def) => ItemDb.Instance.Identify(def);
}

public class BaseDef
{
    public string id = "";
    public LogicBrick[] Components = [];
}

public class Entity<DefT> : IEntity where DefT : BaseDef
{
    private readonly uint _id = IEntity.NextId++;
    public uint Id => _id;
    public readonly DefT Def;
    public int ActiveFactCount;
    
    protected Entity(DefT def, IEnumerable<LogicBrick> components)
    {
        Def = def;
        foreach (var c in components)
            AddFact(c);
    }
    private readonly List<Fact> Facts = [];
    public int FactCount => Facts.Count;
    public Fact FactAt(int i) => Facts[i];
    public Fact? FindFact(LogicBrick brick) => LiveFacts.FirstOrDefault(x => x.Brick == brick);

    public IEnumerable<Fact> LiveFacts => Facts.Where(x => !x.MarkedForRemoval);

    public virtual IEnumerable<Fact> GetAllFacts(PHContext? ctx) => LiveFacts;

    public IEnumerable<Fact> GetOwnFacts()
    {
        for (int i = 0; i < FactCount; i++)
            if (!FactAt(i).MarkedForRemoval) yield return FactAt(i);
    }

    public void ShareFactsFrom(Entity<DefT> other)
    {
        foreach (var fact in other.LiveFacts)
            Facts.Add(fact);
    }

    private Fact? GetFact(LogicBrick brick, bool doMerge)
    {
        foreach (var fact in LiveFacts)
        {
            var res = fact.Brick.MergeWith(brick);
            if (res != null)
            {
                if (doMerge) fact.Brick = res;
                return fact;
            }
        }
        return null;
    }

    public Fact AddFact(LogicBrick brick, int? duration = null, int count = 1)
    {
        var existing = GetFact(brick, true);
        if (brick.StackMode == StackMode.Stack)
        {
            if (existing != null)
            {
                existing.Stacks = Math.Min(existing.Brick.MaxStacks, existing.Stacks + count);
                LogicBrick.FireOnStackAdded(existing.Brick, existing);
                return existing;
            }
        }
        if (brick.StackMode == StackMode.ExtendStacks)
        {
            if (existing != null)
            {
                if (count > existing.Stacks)
                {
                    existing.Stacks = Math.Min(existing.Brick.MaxStacks, count);
                    LogicBrick.FireOnStackAdded(existing.Brick, existing);
                }
                return existing;
            }
        }

        if (existing == null || brick.StackMode == StackMode.Independent)
        {
            var fact = new Fact(this, brick, brick.CreateData());
            if (duration.HasValue)
                fact.ExpiresAt = g.CurrentRound + duration.Value;
            Facts.Add(fact);
            if (this is not Item) Log.Write($"add fact {fact.Brick} => {this}");
            if (brick.IsActive)
            {
                if (ActiveFactCount++ == 0 && this is not IUnit)
                    g.ActiveEntities.Add(this);
            }
            LogicBrick.FireOnFactAdded(brick, fact);
            return fact;
        }

        // Extend (and existing is not null here): use new expiry if longer than remaining
        if (duration.HasValue)
        {
            int existingExpiresAt = existing.ExpiresAt.GetValueOrDefault(0);
            existing.ExpiresAt = Math.Max(existingExpiresAt, g.CurrentRound + duration.Value);
        }
        return existing;
    }

    public void RemoveStack(LogicBrick brick, int count = 1)
    {
        var fact = GetFact(brick, false);
        if (fact == null) return;
        fact.Stacks -= count;
        LogicBrick.FireOnStackRemoved(fact.Brick, fact);
        if (fact.Stacks <= 0)
            fact.Remove();
    }

    public virtual int EffectiveLevel => 1;

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
    public virtual bool Allows(string key) => Query(key, null, MergeStrategy.And, true);

    public bool HasFact(LogicBrick brick) => LiveFacts.Any(f => f.Brick == brick);
    public IEnumerable<string> ActiveBuffNames => LiveFacts.Where(f => f.Brick.IsBuff).Select(f => f.DisplayName!);

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

    public int Count => Items.Count;
    public Item this[int i] => Items[i];

    public Item Add(Item item)
    {
        // Try to merge with existing stack
        foreach (var existing in Items)
        {
            if (existing.CanMerge(item))
            {
                existing.MergeFrom(item);

                // A bit odd, cos `item` will get gc, but for pickup.
                item.InvLet = existing.InvLet;
                return existing;
            }
        }

        int idx = LetterToIndex(item.InvLet);
        if (idx >= 0 && (inUse & (1UL << idx)) == 0)
        {
            inUse |= 1UL << idx;
            item.Holder = owner;
            Items.Add(item);
            return item;
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
                return item;
            }
        }
    }

    public bool Remove(Item item)
    {
        int i = Items.IndexOf(item);
        if (i < 0) return false;
        RemoveAt(i);
        return true;
    }

    public void RemoveAt(int i)
    {
        var item = Items[i];
        Items.RemoveAt(i);
        int idx = LetterToIndex(item.InvLet);
        if (idx >= 0) inUse &= ~(1UL << idx);
        
        // auto-unequip if equipped
        foreach (var slot in owner.Equipped.Where(kv => kv.Value == item).Select(kv => kv.Key).ToList())
            owner.Unequip(slot, force: true);
        
        item.Holder = null;
    }

    public bool Contains(Item item) => Items.Contains(item);

    public void Consume(Item item, int count = 1)
    {
        if (item.Count > count)
            item.Count -= count;
        else
            Remove(item);
    }

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
    public int BaseMax; //unmodified
    public int Max; //modified, kinda scary to cache here but bleh
    public int Current;

    public void Reset(int max)
    {
        BaseMax = max;
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
        BaseMax = hp.BaseMax,
        Max = hp.Max,
        Current = Math.Clamp(hp.Current - delta, 0, hp.Max),
    };

    public static Hitpoints operator +(Hitpoints hp, int delta) => new()
    {
        BaseMax = hp.BaseMax,
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
    string? ProperName { get; set; }
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
    void RemoveAction(ActionBrick action);
    void AddSpell(SpellBrickBase spell);
    IEnumerable<Fact> Facts { get; }
    ActionCost LandMove { get; }

    public int NaturalRegen { get; }
    public int StrMod { get; }

    Trap? TrappedIn { get; set; }
    int EscapeAttempts { get; set; }
    IUnit? GrabbedBy { get; set; }
    IUnit? Grabbing { get; set; }
    MoveMode CurrentMoveMode { get; set; }
    bool IsDM { get; }
    int CasterLevel { get; }

    MoralAxis MoralAxis { get; }
    EthicalAxis EthicalAxis { get; }
    bool IsCreature(string? type = null, string? subtype = null);

    int GetAC();
    int GetAttackBonus(WeaponDef weapon);
    int GetSpellAttackBonus(SpellBrickBase spell);
    int GetDamageBonus();
    int GetSpellDC();
    Item GetWieldedItem();
    EquipSlot? Equip(Item item);
    UnequipResult Unequip(EquipSlot slot, bool force = false);
    Modifiers QueryModifiers(string key, string? arg = null);
    List<Fact> QueryFacts(string key, string? arg = null);

    public bool IsAwareOf(Trap trap);
    public void ObserveTrap(Trap trap);

    void AddPool(string name, int max, DiceFormula regenRate);
    bool HasCharge(string name, out string whyNot);
    bool TryUseCharge(string name);
    void TickPools();
    ChargePool? GetPool(string name);

    int TempHp { get; }
    int LastDamagedOnTurn { get; set; }
    void GrantTempHp(int amount);
    int AbsorbTempHp(int damage, out int absorbed);
    void TickTempHp();

    // for stat tracking
    public int HitsTaken { get; set; }
    public int MissesTaken { get; set; }
    public int DamageTaken { get; set; }
}

public class ChargePool(int max, DiceFormula regenRate)
{
    public int Current = max;
    public int Max = max;
    public DiceFormula RegenRate = regenRate;
    public int Ticks;
    public int NextRegen = regenRate.Roll();
    public int Locked { get; private set; }

    public int EffectiveMax => Max - Locked;

    public void Lock() { Locked++; Current = Math.Min(Current, EffectiveMax); }
    public void Unlock() => Locked = Math.Max(0, Locked - 1);

    public void Tick()
    {
        if (Current < EffectiveMax && ++Ticks >= NextRegen)
        {
            Current++;
            Ticks = 0;
            NextRegen = RegenRate.Roll();
        }
    }
}

public abstract class Unit<TDef>(TDef def, IEnumerable<LogicBrick> components) : Entity<TDef>(def, components), IUnit where TDef : BaseDef
{
    public abstract MoralAxis MoralAxis { get; }
    public abstract EthicalAxis EthicalAxis { get; }
    public abstract bool IsCreature(string? type = null, string? subtype = null);

    public int HitsTaken { get; set; }
    public int MissesTaken { get; set; }
    public int DamageTaken { get; set; }

    public Dictionary<EquipSlot, Item> Equipped { get; } = [];
    Inventory? _inventory;
    public Inventory Inventory => _inventory ??= new(this);
    public List<ActionBrick> Actions { get; } = [];
    public List<SpellBrickBase> Spells { get; } = [];
    public Dictionary<ActionBrick, object?> ActionData { get; } = [];

    public override IEnumerable<Fact> GetAllFacts(PHContext? ctx)
    {
        for (int i = 0; i < FactCount; i++)
            if (!FactAt(i).MarkedForRemoval) yield return FactAt(i);
        foreach (var item in Inventory)
            for (int i = 0; i < item.FactCount; i++)
                if (!item.FactAt(i).MarkedForRemoval) yield return item.FactAt(i);
        if (ctx?.Weapon != null && !Inventory.Contains(ctx.Weapon))
            for (int i = 0; i < ctx.Weapon.FactCount; i++)
                if (!ctx.Weapon.FactAt(i).MarkedForRemoval) yield return ctx.Weapon.FactAt(i);
    }
    IEnumerable<Fact> IUnit.Facts => LiveFacts;
    readonly Dictionary<string, ChargePool> Pools = [];
    public abstract bool IsDM { get; }
    public abstract int CasterLevel { get; }

    public bool HasPool(string pool) => Pools.ContainsKey(pool);

    public Trap? TrappedIn { get; set; }
    public int EscapeAttempts { get; set; }
    public IUnit? GrabbedBy { get; set; }
    public IUnit? Grabbing { get; set; }
    public MoveMode CurrentMoveMode { get; set; } = MoveMode.Walk;

    public abstract bool IsAwareOf(Trap trap);
    public abstract void ObserveTrap(Trap trap);

    public void AddPool(string name, int max, DiceFormula regenRate)
    {
        if (Pools.TryGetValue(name, out var existing))
        {
            existing.Max += max;
            existing.Current += max;
            if (regenRate.Average() < existing.RegenRate.Average())
                existing.RegenRate = regenRate;
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

    int _tempHp;
    public int TempHp => _tempHp;
    public int LastDamagedOnTurn { get; set; } = -100;

    public void GrantTempHp(int amount)
    {
        if (amount <= 0) return;
        int cap = HP.Max / 2;
        if (_tempHp <= 0)
            _tempHp = Math.Min(amount, cap);
        else
        {
            float efficiency = (float)amount / (amount + _tempHp);
            _tempHp = Math.Min(_tempHp + (int)(amount * efficiency), cap);
        }
    }

    public int AbsorbTempHp(int damage, out int absorbed)
    {
        if (_tempHp <= 0 || damage <= 0) { absorbed = 0; return damage; }
        absorbed = Math.Min(_tempHp, damage);
        _tempHp -= absorbed;
        return damage - absorbed;
    }

    public void TickTempHp()
    {
        if (_tempHp <= 0 || g.CurrentRound - LastDamagedOnTurn > 5) return;
        int decay = Math.Max(5, _tempHp / 3);
        _tempHp = Math.Max(0, _tempHp - decay);
    }

    public abstract int NaturalRegen { get; }
    public abstract int StrMod { get; }

    public void AddSpell(SpellBrickBase spell)
    {
        Spells.Add(spell);
        ActionData[spell] = spell.CreateData();
    }

    public void AddAction(ActionBrick action)
    {
        if (!Actions.Contains(action))
            ActionData[action] = action.CreateData();
        Actions.Add(action);
    }

    public void RemoveAction(ActionBrick action)
    {
        Actions.Remove(action);
        if (!Actions.Contains(action))
            ActionData.Remove(action);
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

    public List<Fact> QueryFacts(string key, string? arg = null)
    {
        List<Fact> facts = [];
        foreach (var fact in LiveFacts)
            if (LogicBrick.FireOnQuery(fact.Brick, fact, key, arg) is Fact f)
                facts.Add(f);
        foreach (var item in Inventory)
            foreach (var fact in item.LiveFacts)
                if (LogicBrick.FireOnQuery(fact.Brick, fact, key, arg) is Fact f)
                    facts.Add(f);
        return facts;
    }

    public abstract bool IsPlayer { get; }
    public bool IsDead { get; set; }
    public string? ProperName { get; set; }

    public Hitpoints HP { get; set; } = new();
    public Pos Pos { get; set; }
    public int Energy { get; set; }
    public int Initiative { get; set; }
    public abstract Glyph Glyph { get; }

    Item? _unarmedItem;

    public abstract int GetAC();
    public abstract int GetAttackBonus(WeaponDef weapon);
    public abstract int GetSpellAttackBonus(SpellBrickBase spell);
    public abstract int GetDamageBonus();
    public abstract int GetSpellDC();
    protected abstract WeaponDef GetUnarmedDef();

    public Item GetWieldedItem()
    {
        if (Equipped.TryGetValue(ItemSlots.HandSlot, out var item) && item.Def is WeaponDef)
            return item;
        return _unarmedItem ??= new(GetUnarmedDef());
    }

    public EquipSlot? Equip(Item item)
    {
        if (!Inventory.Contains(item))
            throw new InvalidOperationException("Item not in inventory");
        if (Equipped.ContainsValue(item))
            return null;
        if (item.Def.DefaultEquipSlot == ItemSlots.None)
            return null;

        EquipSlot mainSlot = new(item.Def.DefaultEquipSlot, "_");
        EquipSlot offSlot = new(item.Def.DefaultEquipSlot, "off");
        EquipSlot resultSlot;

        // Rings use left/right
        if (item.Def.DefaultEquipSlot == ItemSlots.Ring)
        {
            EquipSlot left = new(ItemSlots.Ring, "left");
            EquipSlot right = new(ItemSlots.Ring, "right");
            EquipSlot slot = !Equipped.ContainsKey(left) ? left : !Equipped.ContainsKey(right) ? right : default;
            if (slot == default) return null;
            Equipped[slot] = item;
            resultSlot = slot;
        }
        else
        {
            int hands = item.Def is WeaponDef w ? w.Hands : 1;
            if (hands == 2)
            {
                if (Equipped.ContainsKey(mainSlot) || Equipped.ContainsKey(offSlot))
                    return null;
                Equipped[mainSlot] = item;
                Equipped[offSlot] = item;
                resultSlot = mainSlot;
            }
            else
            {
                if (Equipped.ContainsKey(mainSlot))
                    return null;
                Equipped[mainSlot] = item;
                resultSlot = mainSlot;
            }
        }

        using var ctx = PHContext.Create(this, Target.None);
        LogicBrick.FireOnEquip(item, ctx);
        Log.Structured("equip", $"{item.Def.Name:item}");
        return resultSlot;
    }

    public UnequipResult Unequip(EquipSlot slot, bool force = false)
    {
        if (!Equipped.TryGetValue(slot, out var item)) return UnequipResult.Empty;
        if (!force && item.IsCursed)
        {
            item.Knowledge |= ItemKnowledge.BUC;
            return UnequipResult.Cursed;
        }

        Equipped.Remove(slot);
        EquipSlot offSlot = new(slot.Type, "off");
        if (Equipped.TryGetValue(offSlot, out var offItem) && offItem == item)
            Equipped.Remove(offSlot);
        EquipSlot mainSlot = new(slot.Type, "_");
        if (Equipped.TryGetValue(mainSlot, out var mainItem) && mainItem == item)
            Equipped.Remove(mainSlot);

        using var ctx = PHContext.Create(this, Target.None);
        LogicBrick.FireOnUnequip(item, ctx);
        Log.Structured("unequip", $"{item.Def.Name:item}");
        return UnequipResult.Ok;
    }
}

public class GrantAction(ActionBrick action) : LogicBrick
{
    public override string Id => $"grant_action+{action.Name}";
    public ActionBrick Action => action;
    public override AbilityTags Tags => action.Tags;
    protected override void OnFactAdded(Fact fact) => (fact.Entity as IUnit)?.AddAction(action);
    // TODO: if action is a toggle and currently on, clean up the inner buff fact
    protected override void OnFactRemoved(Fact fact) => (fact.Entity as IUnit)?.RemoveAction(action);
}

public class GrantSpell(SpellBrickBase spell) : LogicBrick
{
    public override string Id => $"grant_spell+{spell.Name}";
    public SpellBrickBase Spell => spell;
    protected override void OnFactAdded(Fact fact) => (fact.Entity as IUnit)?.AddSpell(spell);
}

public class GrantPool(string name, int max, DiceFormula regenRate) : LogicBrick
{
    public override string Id => $"grant_pool+{name}";
    protected override void OnFactAdded(Fact fact)
    {
        Log.Write($"on fact added pool {name} to {fact.Entity}");
        (fact.Entity as IUnit)?.AddPool(name, max, regenRate);
    } 
}