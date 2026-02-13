namespace Pathhack.Game;

public class BlindBuff : LogicBrick
{
  public static readonly BlindBuff Instance = new();

  public override bool IsBuff => true;
  public override string? BuffName => "Blind";
  public override StackMode StackMode => StackMode.Stack;

  protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
  {
    "can_see" => false,
    _ => null,
  };

  protected override void OnStackRemoved(Fact fact)
  {
    if (fact.Entity is not IUnit { IsPlayer: true }) return;
    if (fact.Stacks == 0)
      g.pline("You can see again.");
    else
      g.pline("Your vision clears slightly.");
  }
}

// Prone: -2 AC, half speed
public class ProneBuff : LogicBrick
{
  public static readonly ProneBuff Instance = new();
  public override bool IsBuff => true;
  public override string? BuffName => "Hamstrung";
  public override StackMode StackMode => StackMode.Stack;

  protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
  {
    "ac" => new Modifier(ModifierCategory.UntypedStackable, -2, "prone"),
    "speed_mult" => 0.5,
    _ => null
  };
}

public class SilencedBuff : LogicBrick
{
  public static readonly SilencedBuff Instance = new();
  public override bool IsBuff => true;
  public override string? BuffName => "Silenced";
  public override StackMode StackMode => StackMode.Stack;

  protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
  {
    "can_speak" => false,
    _ => null
  };
}

public class ParalyzedBuff : LogicBrick
{
  public static readonly ParalyzedBuff Instance = new();
  public override bool IsBuff => true;
  public override string? BuffName => "Paralyzed";
  public override StackMode StackMode => StackMode.Stack;

  protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
  {
    "paralyzed" => true,
    _ => null
  };

  protected override void OnStackRemoved(Fact fact)
  {
    if (fact.Entity is not IUnit { IsPlayer: true }) return;
    if (fact.Stacks == 0)
      g.pline("You can move again!");
  }
}

public class NauseatedBuff : LogicBrick
{
  public static readonly NauseatedBuff Instance = new();
  public override bool IsBuff => true;
  public override string? BuffName => "Nauseated";
  public override StackMode StackMode => StackMode.Stack;

  protected override void OnBeforeCheck(Fact fact, PHContext context)
  {
    if (context.IsCheckingOwnerOf(fact))
    {
      context.Check!.Disadvantage++;
      fact.Entity.RemoveStack(Instance);
    }
  }
}

public class AfflictionData
{
  public int NextTick;
  public int AppliedAt;
}

public abstract class AfflictionBrick(int dc, string? tag = null) : LogicBrick<AfflictionData>
{
  public int DC => dc;
  public string? Tag => tag;

  public override bool IsBuff => true;
  public override bool IsActive => true;
  public override StackMode StackMode => StackMode.Stack;
  public override FactDisplayMode DisplayMode => FactDisplayMode.Name | FactDisplayMode.Stacks;

  public abstract string AfflictionName { get; }
  public abstract int MaxStage { get; }
  public abstract DiceFormula TickInterval { get; }
  public virtual int? AutoCureMax => null;
  public virtual string SaveKey => "fortitude_save";

  public override string? BuffName => AfflictionName;
  public override int MaxStacks => MaxStage + 1;

  public override LogicBrick? MergeWith(LogicBrick other) =>
      other is AfflictionBrick a && a.GetType() == GetType()
          ? (a.DC > DC ? a : this)
          : null;

  protected abstract void DoPeriodicEffect(IUnit unit, int stage);
  protected abstract object? DoQuery(int stage, string key, string? arg);

  protected static int Stage(Fact fact) => fact.Stacks - 1;

  protected override void OnFactAdded(Fact fact)
  {
    var data = X(fact);
    data.AppliedAt = g.CurrentRound;
    data.NextTick = g.CurrentRound + TickInterval.Roll();
  }

  protected override void OnFactRemoved(Fact fact) => OnCured((IUnit)fact.Entity);

  protected override void OnStackAdded(Fact fact)
  {
    int stage = Stage(fact);
    if (stage > 0)
      DoPeriodicEffect((IUnit)fact.Entity, stage);
  }

  protected override void OnRoundStart(Fact fact)
  {
    var data = X(fact);
    if (g.CurrentRound < data.NextTick) return;

    var unit = (IUnit)fact.Entity;
    data.NextTick = g.CurrentRound + TickInterval.Roll();

    // auto-cure
    int roundsAfflicted = g.CurrentRound - data.AppliedAt;
    if (AutoCureMax is int max && g.Rn2(max) < roundsAfflicted)
    {
      fact.Remove();
      return;
    }

    // save
    using var saveCtx = PHContext.Create(Monster.DM, Target.From(unit));
    bool saved = CreateAndDoCheck(saveCtx, SaveKey, DC, AfflictionName);
    if (saved)
      unit.RemoveStack(this);
    else
      unit.AddFact(this);
  }

  protected virtual void OnCured(IUnit unit) => g.pline($"{unit:The} {VTense(unit, "feel")} better.");

  protected override object? OnQuery(Fact fact, string key, string? arg) =>
      key == Tag ? fact : DoQuery(Stage(fact), key, arg);
}

public class RegenBrick(params DamageType[] suppressedBy) : LogicBrick<RegenBrick.State>
{
  public class State { public bool Suppressed; }

  public override AbilityTags Tags => AbilityTags.Biological;

  public static readonly RegenBrick Always = new();

  protected override void OnDamageTaken(Fact fact, PHContext ctx)
  {
    foreach (var roll in ctx.Damage)
      if (suppressedBy.Contains(roll.Type))
      {
        X(fact).Suppressed = true;
        return;
      }
  }

  protected override void OnRoundEnd(Fact fact) => X(fact).Suppressed = false;

  protected override object? OnQuery(Fact fact, string key, string? arg) =>
      key == "respawn_from_corpse" && !X(fact).Suppressed ? true : null;
}
