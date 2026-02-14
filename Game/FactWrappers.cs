namespace Pathhack.Game;

internal static class WrapperHelper<T, B> where B : class
{
  private static readonly Dictionary<B, T> cache = [];

  public static T For(B brick, Func<B, T> factory)
  {
    if (!cache.TryGetValue(brick, out var timed))
    {
      timed = factory(brick);
      cache[brick] = timed;
    }
    return timed;
  }
}

public class GrantWhenEquipped(ActionBrick action) : LogicBrick
{
  public override string Id => $"grant_equipped+{action.Name}";
  public static GrantWhenEquipped For(ActionBrick brick) => WrapperHelper<GrantWhenEquipped, ActionBrick>.For(brick, a => new(a));

  protected override void OnEquip(Fact fact, PHContext ctx)
  {
    if (ctx.Source == null) return;
    ctx.Source.AddAction(action);
    if (action is CooldownAction && ctx.Source != null)
      CooldownAction.SetCooldownMax(ctx.Source, ctx.Source.ActionData[action]);
  }

  protected override void OnUnequip(Fact fact, PHContext ctx) => ctx.Source?.RemoveAction(action);
}

public class ApplyWhenEquipped(LogicBrick brick) : LogicBrick
{
  public override string Id => $"on_equip+{brick.Id}";
  public static ApplyWhenEquipped For(LogicBrick brick) => WrapperHelper<ApplyWhenEquipped, LogicBrick>.For(brick, brick => new(brick));

  protected override void OnEquip(Fact fact, PHContext ctx) => ctx.Source?.AddFact(brick);

  protected override void OnUnequip(Fact fact, PHContext ctx) => ctx.Source?.RemoveStack(brick);
}

public class ApplyAfflictionOnHit(AfflictionBrick affliction) : LogicBrick
{
  public override string Id => $"on_hit+{affliction.Id}";
  public override AbilityTags Tags => affliction.Tags;
  public override string? PokedexDescription => $"On hit: {affliction.AfflictionName} (DC {affliction.DC})";

  protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
  {
    if (!ctx.Check!.Result) return;
    var target = ctx.Target.Unit;
    if (target == null) return;

    using var saveCtx = PHContext.Create(ctx.Source!, Target.From(target));
    bool saved = CreateAndDoCheck(saveCtx, affliction.SaveKey, affliction.DC, affliction.BuffName ?? "unknown_affliction");
    if (!saved)
      target.AddFact(affliction);
  }
}

public class TimedFact(LogicBrick brick) : LogicBrick
{
  public override string Id => $"timed+{brick.Id}";
  public static TimedFact For(LogicBrick brick) => WrapperHelper<TimedFact, LogicBrick>.For(brick, brick => new(brick));

  protected override void OnFactAdded(Fact fact) => fact.Entity.AddFact(brick);

  protected override void OnFactRemoved(Fact fact) => fact.Entity.RemoveStack(brick);
}

public static class LogicBrickExts
{
  public static TimedFact Timed(this LogicBrick brick) => TimedFact.For(brick);
  public static ApplyWhenEquipped WhenEquipped(this LogicBrick brick) => ApplyWhenEquipped.For(brick);
  public static GrantWhenEquipped WhenEquipped(this ActionBrick action) => GrantWhenEquipped.For(action);
  public static ApplyAfflictionOnHit OnHit(this AfflictionBrick a) => WrapperHelper<ApplyAfflictionOnHit, LogicBrick>.For(a, b => new((AfflictionBrick)b));
}
