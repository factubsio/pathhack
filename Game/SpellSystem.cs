using System.Runtime.Serialization.Formatters;

namespace Pathhack.Game;

public abstract class SpellBrickBase(string name, int level, string description, TargetingType targeting, bool maintained = false, int maxRange = -1) : ActionBrick(name, targeting, maxRange)
{
  public string Description => description;

  public int Level => level;

  public bool Maintained => maintained;

  public readonly string Pool = $"spell_l{level}";

  public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => unit.HasCharge(Pool, out whyNot);

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

public class SpellBrick(string name, int level, string description, Action<IUnit, Target> act, TargetingType targeting = TargetingType.None, bool maintained = false, int maxRange = -1) : SpellBrickBase(name, level, description, targeting, maintained, maxRange)
{
  public override void Execute(IUnit unit, object? data, Target target) 
  {
    if (unit.TryUseCharge(Pool)) act(unit, target);
  }
}

public class ActivateMaintainedSpell(string name, int level, string description, Action<IUnit> act, MaintainedBuff buff) : SpellBrickBase(name, level, description, TargetingType.None, true)
{
  public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
  {
    if (!base.CanExecute(unit, data, target, out whyNot)) return false;
    whyNot = "buff already active";
    return !unit.HasFact(buff);
  }

  public override void Execute(IUnit unit, object? data, Target target) 
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
  public override void Execute(IUnit unit, object? data, Target target) 
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

  public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
  {
    whyNot = "No maintained spells active.";
    return unit.Facts.Any(f => f.Brick is MaintainedBuff);
  }

  public override void Execute(IUnit unit, object? data, Target target) => DoDismiss(unit);

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
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => unit.HasCharge(Pool, out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        unit.TryUseCharge(Pool);
    }
}