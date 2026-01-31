namespace Pathhack.Game;

public abstract class SpellBrickBase(string name, int level, string description, TargetingType targeting) : ActionBrick(name, targeting)
{
  public string Description => description;

  public int Level => level;

  public readonly string Pool = $"spell_l{level}";

  public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => unit.HasCharge(Pool, out whyNot);

  public FeatDef ToFeat() => new()
  {
    id = $"spell_{Name}",
    Name = $"{Name}",
    Description = Description,
    Type = FeatType.Class,
    Components = [new GrantSpell(this)]
  };
}

public class SpellBrick(string name, int level, string description, Action<IUnit, Target> act, TargetingType targeting = TargetingType.None) : SpellBrickBase(name, level, description, targeting)
{
  public override void Execute(IUnit unit, object? data, Target target) => act(unit, target);
}

public class SpellBrick<T>(string name, int level, string description, Action<IUnit, Target, T> act, TargetingType targeting = TargetingType.None) : SpellBrickBase(name, level, description, targeting) where T : new()
{
  public override object? CreateData() => new T();
  public override void Execute(IUnit unit, object? data, Target target) => act(unit, target, (T)data!);
}

