namespace Pathhack.Game.Bestiary;

public static class TemplateHelper
{
  public static readonly HashSet<string> CannotBeUndead = [
    CreatureTypes.Plant,
    CreatureTypes.Undead,
    CreatureTypes.Elemental,
    CreatureTypes.Outsider,
    CreatureTypes.Construct,
    CreatureTypes.Ooze,
  ];
}

public class ZombieTemplate() : MonsterTemplate("zombie")
{
  class ZombieFacts : LogicBrick
  {
    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
      "speed_bonus" => new Modifier(ModifierCategory.UntypedStackable, -4, "zombie"),
      "mindless" => true,
      _ => null,
    };

    internal static readonly ZombieFacts Instance = new();
  }

  public override bool CanApplyTo(MonsterDef def) => !TemplateHelper.CannotBeUndead.Contains(def.CreatureType);

  public override IEnumerable<LogicBrick> GetComponents(MonsterDef def)
  {
    // TODO: remove abilities with mental/good tags (first implement ability tags)
    foreach (var c in def.Components) yield return c;
    yield return ZombieFacts.Instance;
  }

  public override void ModifySpawn(Monster m)
  {
    // Undead are ALWAYS evil and NEVER lawful
    m.OwnMoralAxis = MoralAxis.Evil;
    if (m.Def.EthicalAxis == EthicalAxis.Lawful) m.OwnEthicalAxis = EthicalAxis.Neutral;

    m.TemplateBonusLevels = Math.Clamp((int)(m.Def.CR * 0.2), 1, 3);

    m.OwnCreatureType = CreatureTypes.Undead;

    m.OwnGlyph = m.Def.Glyph with { Background = ConsoleColor.DarkGreen };

    if (m.Def.IsUnique)
      m.TemplatedName = $"Zombie {m.Def.Name}";
    else
      m.TemplatedName = $"{m.Def.Name} zombie";
  }
}
