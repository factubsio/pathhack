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

  protected override void OnBeforeCheck(Fact fact, PHContext context)
  {
    if (context.Source != fact.Entity || context.Weapon == null) return;
    if (context.Source.Has("blind_fight")) return;
    context.Check!.Disadvantage++;
  }

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

public class NauseatedBuff : LogicBrick
{
  public static readonly NauseatedBuff Instance = new();
  public override bool IsBuff => true;
  public override string? BuffName => "Nauseated";
  public override StackMode StackMode => StackMode.Stack;

  protected override void OnBeforeCheck(Fact fact, PHContext context)
  {
    if (context.Source == fact.Entity)
    {
      context.Check!.Disadvantage++;
      fact.Entity.RemoveStack(Instance);
    }
  }
}
