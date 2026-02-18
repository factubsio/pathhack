namespace Pathhack.Game.Bestiary;

public static class TemplateHelper
{
  public static readonly HashSet<string> CannotBeUndead = [
    CreatureTypes.Plant,
    CreatureTypes.Undead,
    CreatureTypes.Outsider,
    CreatureTypes.Construct,
    CreatureTypes.Ooze,
  ];

  const AbilityTags MindlessUndeadStrip = AbilityTags.Biological | AbilityTags.Mental | AbilityTags.Holy | AbilityTags.Verbal;

  public static IEnumerable<LogicBrick> StripMindless(IEnumerable<LogicBrick> components) =>
    components.Where(c => (c.Tags & MindlessUndeadStrip) == 0);

  public static void MakeUndead(Monster m)
  {
    m.OwnMoralAxis = MoralAxis.Evil;
    if (m.Def.EthicalAxis == EthicalAxis.Lawful) m.OwnEthicalAxis = EthicalAxis.Neutral;
    m.OwnCreatureType = CreatureTypes.Undead;
  }
}

public class SkeletonTemplate() : MonsterTemplate("skeleton")
{
  public class SkeletonFacts : LogicBrick
  {
    public override string Id => "template:skeleton";
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

    public static readonly SkeletonFacts Instance = new();
  }

  // Gems?
  // public static ItemDef Bones = new()
  // {
  //   Price = 1,
  //   Material = "bone",
  // };

  public override bool CanApplyTo(MonsterDef def) => !TemplateHelper.CannotBeUndead.Contains(def.CreatureType);

  public override int LevelBonus(MonsterDef def, int level) => Math.Clamp((int)(level * 0.15), 0, 2);

  public override IEnumerable<LogicBrick> GetComponents(MonsterDef def) =>
    TemplateHelper.StripMindless(def.Components)
      .Append(SkeletonFacts.Instance)
      .Append(SimpleDR.Blunt.Lookup(def.BaseLevel));
      // .Append(new DropOnDeath(Bones, d(4)+4));

  public override void ModifySpawn(Monster m)
  {
    TemplateHelper.MakeUndead(m);
    m.ItemBonusAC -= 2;
    m.OwnBrainFlags = m.Def.BrainFlags | MonFlags.NoCorpse;

    m.OwnGlyph = m.Def.Glyph with { Background = ConsoleColor.Gray };

    if (m.Def.IsUnique)
      m.TemplatedName = $"Skeletal {m.Def.Name}";
    else
      m.TemplatedName = $"{m.Def.Name} skeleton";
  }
}

public class ZombieTemplate() : MonsterTemplate("zombie")
{
  public class ZombieFacts : LogicBrick
  {
    public override string Id => "template:zombie";
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

    public static readonly ZombieFacts Instance = new();
  }

  public override bool CanApplyTo(MonsterDef def) => !TemplateHelper.CannotBeUndead.Contains(def.CreatureType);

  public override int LevelBonus(MonsterDef def, int level) => Math.Clamp((int)(level * 0.2), 1, 3);

  public override IEnumerable<LogicBrick> GetComponents(MonsterDef def) =>
    TemplateHelper.StripMindless(def.Components).Append(ZombieFacts.Instance).Append(SimpleDR.Slashing.Lookup(def.BaseLevel));

  public override void ModifySpawn(Monster m)
  {
    TemplateHelper.MakeUndead(m);

    m.OwnGlyph = m.Def.Glyph with { Background = ConsoleColor.DarkGreen };

    if (m.Def.IsUnique)
      m.TemplatedName = $"Zombie {m.Def.Name}";
    else
      m.TemplatedName = $"{m.Def.Name} zombie";
  }
}
