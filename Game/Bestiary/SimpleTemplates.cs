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

public class SkeletonTemplate() : MonsterTemplate("skeleton")
{
  class SkeletonFacts : LogicBrick
  {
    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
      "mindless" => true,
      _ => null,
    };

    protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
      ctx.Check!.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, -2, "skeleton"));
    }

    protected override void OnBeforeDamageRoll(Fact fact, PHContext ctx)
    {
      foreach (var roll in ctx.Damage)
        roll.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, -2, "skeleton"));
    }

    internal static readonly SkeletonFacts Instance = new();
  }

  public override bool CanApplyTo(MonsterDef def) => !TemplateHelper.CannotBeUndead.Contains(def.CreatureType);

  public override int LevelBonus(MonsterDef def, int level) => Math.Clamp((int)(level * 0.15), 0, 2);

  public override IEnumerable<LogicBrick> GetComponents(MonsterDef def)
  {
    foreach (var c in def.Components) yield return c;
    yield return SkeletonFacts.Instance;
    yield return SimpleDR.Blunt.Lookup(def.BaseLevel);
  }

  public override void ModifySpawn(Monster m)
  {
    m.OwnMoralAxis = MoralAxis.Evil;
    if (m.Def.EthicalAxis == EthicalAxis.Lawful) m.OwnEthicalAxis = EthicalAxis.Neutral;

    m.OwnCreatureType = CreatureTypes.Undead;
    m.ItemBonusAC -= 2;

    m.OwnGlyph = m.Def.Glyph with { Background = ConsoleColor.Gray };

    if (m.Def.IsUnique)
      m.TemplatedName = $"Skeletal {m.Def.Name}";
    else
      m.TemplatedName = $"{m.Def.Name} skeleton";
  }
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

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
      foreach (var dmg in ctx.Damage)
        if (dmg.Type == DamageTypes.Fire) dmg.Double();
    }

    internal static readonly ZombieFacts Instance = new();
  }

  public override bool CanApplyTo(MonsterDef def) => !TemplateHelper.CannotBeUndead.Contains(def.CreatureType);

  public override int LevelBonus(MonsterDef def, int level) => Math.Clamp((int)(level * 0.2), 1, 3);

  public override IEnumerable<LogicBrick> GetComponents(MonsterDef def)
  {
    // TODO: remove abilities with mental/good tags (first implement ability tags)
    foreach (var c in def.Components) yield return c;
    yield return ZombieFacts.Instance;
    yield return SimpleDR.Slashing.Lookup(def.BaseLevel);
  }

  public override void ModifySpawn(Monster m)
  {
    // Undead are ALWAYS evil and NEVER lawful
    m.OwnMoralAxis = MoralAxis.Evil;
    if (m.Def.EthicalAxis == EthicalAxis.Lawful) m.OwnEthicalAxis = EthicalAxis.Neutral;

    m.OwnCreatureType = CreatureTypes.Undead;

    m.OwnGlyph = m.Def.Glyph with { Background = ConsoleColor.DarkGreen };

    if (m.Def.IsUnique)
      m.TemplatedName = $"Zombie {m.Def.Name}";
    else
      m.TemplatedName = $"{m.Def.Name} zombie";
  }
}
