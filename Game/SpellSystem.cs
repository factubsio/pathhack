namespace Pathhack.Game;

public abstract class SpellBrickBase(string name, int level, string description, TargetingType targeting, bool maintained = false, int maxRange = -1, AbilityTags tags = AbilityTags.None) : ActionBrick(name, targeting, maxRange, tags)
{
  public string Description => description;

  public int Level => level;

  public bool Maintained => maintained;

  public readonly string Pool = $"spell_l{level}";

  public override ActionPlan CanExecute(IUnit unit, object? data, Target target) =>
    unit.HasCharge(Pool, out var whyNot) ? true : new ActionPlan(false, whyNot);

  public FeatDef ToFeat() => new()
  {
    id = $"spell_{Name}",
    Name = $"{Name}",
    Description = Description,
    Type = FeatType.Class,
    Components = [new GrantSpell(this)],
    CheckWhyNot = () => !u.HasPool($"spell_l{level}") ? $"Must be able to cast level {level} spells" : null,
  };
}

public class SpellBrick(string name, int level, string description, Action<IUnit, Target> act, TargetingType targeting = TargetingType.None, bool maintained = false, int maxRange = -1, AbilityTags tags = AbilityTags.None) : SpellBrickBase(name, level, description, targeting, maintained, maxRange, tags)
{
  public override void Execute(IUnit unit, object? data, Target target, object? plan = null) 
  {
    if (unit.TryUseCharge(Pool)) act(unit, target);
  }
}

public class ActivateMaintainedSpell(string name, int level, string description, Action<IUnit> act, MaintainedBuff buff) : SpellBrickBase(name, level, description, TargetingType.None, true, tags: AbilityTags.Beneficial)
{
  public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
  {
    var basePlan = base.CanExecute(unit, data, target);
    if (!basePlan) return basePlan;
    return !unit.HasFact(buff) ? true : new ActionPlan(false, "buff already active");
  }

  public override void Execute(IUnit unit, object? data, Target target, object? plan = null) 
  {
    if (unit.TryUseCharge(Pool))
    {
      act(unit);
      unit.AddFact(buff);
    }
  }
}

public class SpellBrick<T>(string name, int level, string description, Action<IUnit, Target, T> act, TargetingType targeting = TargetingType.None, bool maintained = false) : SpellBrickBase(name, level, description, targeting, maintained) where T : new()
{
  public override object? CreateData() => new T();
  public override void Execute(IUnit unit, object? data, Target target, object? plan = null) 
  {
    if (unit.TryUseCharge(Pool)) act(unit, target, (T)data!);
  }
}

public abstract class MaintainedBuff(string pool) : LogicBrick
{
  public override bool IsActive => true;

  protected override void OnFactAdded(Fact fact) => (fact.Entity as IUnit)?.GetPool(pool)?.Lock();
  protected override void OnFactRemoved(Fact fact) => (fact.Entity as IUnit)?.GetPool(pool)?.Unlock();
}

public class DismissAction() : ActionBrick("Dismiss")
{
  public static readonly DismissAction Instance = new();

  public override ActionPlan CanExecute(IUnit unit, object? data, Target target) =>
    unit.Facts.Any(f => f.Brick is MaintainedBuff) ? true : new ActionPlan(false, "No maintained spells active.");

  public override void Execute(IUnit unit, object? data, Target target, object? plan = null) => DoDismiss(unit);

  public static void DoDismiss(IUnit unit)
  {
    var buffs = unit.Facts.Where(f => f.Brick is MaintainedBuff).ToList();
    if (buffs.Count == 0)
    {
      g.pline("No maintained spells active.");
      return;
    }

    var menu = new Menu<Fact>();
    menu.Add("Dismiss which spell?", LineStyle.Heading);
    char letter = 'a';
    foreach (var f in buffs)
      menu.Add(letter++, f.DisplayName, f);
    
    var picked = menu.Display(MenuMode.PickOne);
    if (picked.Count == 0) return;
    
    picked[0].Remove();
    g.pline("You dismiss the spell.");
  }
}


// testing nosnesne stuff
internal class ConsumeSpell(int lvl) : ActionBrick($"consume spell {lvl}")
{
    private string Pool => $"spell_l{lvl}";
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) =>
        unit.HasCharge(Pool, out var whyNot) ? true : new ActionPlan(false, whyNot);

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        unit.TryUseCharge(Pool);
    }
}