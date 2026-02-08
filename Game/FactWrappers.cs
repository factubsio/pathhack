namespace Pathhack.Game;

internal static class WrapperHelper<T>
{
  private static readonly Dictionary<LogicBrick, T> cache = [];

  public static T For(LogicBrick brick, Func<LogicBrick, T> factory)
  {
    if (!cache.TryGetValue(brick, out var timed))
    {
      timed = factory(brick);
      cache[brick] = timed;
    }
    return timed;
  }
}

public class ApplyWhenEquipped(LogicBrick brick) : LogicBrick
{
  public static ApplyWhenEquipped For(LogicBrick brick) => WrapperHelper<ApplyWhenEquipped>.For(brick, brick => new(brick));

  protected override void OnEquip(Fact fact, PHContext ctx) => ctx.Source?.AddFact(brick);

  protected override void OnUnequip(Fact fact, PHContext ctx) => ctx.Source?.RemoveStack(brick);
}

public class ApplyAfflictionOnHit(AfflictionBrick affliction) : LogicBrick
{
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
  public static TimedFact For(LogicBrick brick) => WrapperHelper<TimedFact>.For(brick, brick => new(brick));

  protected override void OnFactAdded(Fact fact) => fact.Entity.AddFact(brick);

  protected override void OnFactRemoved(Fact fact) => fact.Entity.RemoveStack(brick);
}

public static class LogicBrickExts
{
  public static TimedFact Timed(this LogicBrick brick) => TimedFact.For(brick);
  public static ApplyWhenEquipped WhenEquipped(this LogicBrick brick) => ApplyWhenEquipped.For(brick);
  public static ApplyAfflictionOnHit OnHit(this AfflictionBrick a) => WrapperHelper<ApplyAfflictionOnHit>.For(a, b => new((AfflictionBrick)b));
}
