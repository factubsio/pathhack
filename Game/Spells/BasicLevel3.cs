namespace Pathhack.Game.Spells;

public static class BasicLevel3Spells
{
  public static readonly SpellBrick Fireball = new("Fireball", 3,
      """A ball of fire explodes at the target location, dealing 6d6 fire damage in a burst. Reflex save for half.""",
      (c, t) =>
      {
        if (t.Pos == null) return;
        Pos center = t.Pos.Value;

        g.YouObserve(c, $"{c:The} {VTense(c, "hurl")} a ball of fire!", "a roaring whoosh");

        Draw.AnimateProjectile(c.Pos, center, new Glyph('*', ConsoleColor.Red));

        List<Pos> tiles = [];
        for (int dx = -2; dx <= 2; dx++)
          for (int dy = -2; dy <= 2; dy++)
          {
            Pos p = center + new Pos(dx, dy);
            if (lvl.InBounds(p) && p.ChebyshevDist(center) <= 2) tiles.Add(p);
          }

        Draw.AnimateFlash(tiles, new Glyph('≈', ConsoleColor.Red));

        int dc = c.GetSpellDC();
        foreach (var pos in tiles)
        {
          var victim = lvl.UnitAt(pos);
          if (victim.IsNullOrDead() || victim == c) continue;

          using var ctx = PHContext.Create(c, Target.From(victim));
          CheckReflex(ctx, dc, "fire");
          ctx.Damage.Add(new() { Formula = d(6, 6), Type = DamageTypes.Fire, HalfOnSave = true });
          DoDamage(ctx);
        }
      }, TargetingType.Pos, maxRange: 8);

  public static readonly SpellBrick VampiricTouch = new("Vampiric touch", 3,
      """Your touch drains life. Make a spell attack for 4d6 necrotic damage and heal for the damage dealt.""",
      (c, t) =>
      {
        if (t.Pos == null) return;
        var target = lvl.UnitAt(c.Pos + t.Pos.Value);
        if (target == null) return;

        g.YouObserve(c, $"{c:The} {VTense(c, "reach")} out with a deathly touch!", "a chilling grasp");

        using var ctx = PHContext.Create(c, Target.From(target));
        ctx.Spell = VampiricTouch;
        ctx.Damage.Add(new() { Formula = d(4, 6), Type = DamageTypes.Magic });
        if (DoAttackRoll(ctx, 0))
        {
          DoDamage(ctx);
          if (ctx.TotalDamageDealt > 0)
          {
            g.DoHeal(c, c, ctx.TotalDamageDealt);
            g.YouObserveSelf(c,
                $"You drain {ctx.TotalDamageDealt} life from {target:the}!",
                $"{c:The} drains life from {target:the}!",
                "a sickening slurp");
          }
        }
        else
        {
          g.YouObserve(target, $"{c:The} {VTense(c, "miss")} {target:the}!");
        }
      }, TargetingType.Direction);

  public static readonly SpellBrickBase FlyLesser = new ActivateMaintainedSpell("Fly, lesser", 3,
      """You gain the ability to fly, but your movement is slowed.""",
      c =>
      {
        g.YouObserveSelf(c, "You rise into the air!", $"{c:The} rises into the air!", "a rush of wind");
      }, FlyLesserBuff.Instance);

  public static readonly SpellBrickBase Heroism = new ActivateMaintainedSpell("Heroism", 3,
      """You are filled with heroic resolve, gaining +2 to attacks and saves.""",
      c =>
      {
        g.YouObserveSelf(c, "You feel heroic!", $"{c:The} looks emboldened!", "a surge of confidence");
      }, HeroismBuff.Instance);

  public static readonly SpellBrick FalseLife = new("False life", 3,
      """You gain a moderate amount of temporary hit points.""",
      (c, _) =>
      {
        int amount = d(2, 6).Roll() + c.CasterLevel;
        c.GrantTempHp(amount);
        g.YouObserveSelf(c, $"You feel a surge of vitality! (+{amount} temp HP)", $"{c:The} looks more resilient.", "a surge of energy");
      }, tags: AbilityTags.Beneficial);

  public static readonly SpellBrick ProtectFire = MakeProtect("Protection from fire", DamageTypes.Fire, ProtectionBrick.Fire);
  public static readonly SpellBrick ProtectCold = MakeProtect("Protection from cold", DamageTypes.Cold, ProtectionBrick.Cold);
  public static readonly SpellBrick ProtectShock = MakeProtect("Protection from shock", DamageTypes.Shock, ProtectionBrick.Shock);
  public static readonly SpellBrick ProtectAcid = MakeProtect("Protection from acid", DamageTypes.Acid, ProtectionBrick.Acid);

  static SpellBrick MakeProtect(string name, DamageType type, ProtectionBrick brick) => new(name, 3,
      $"You gain a pool of absorption against {type.SubCat}. Absorbs damage until depleted.",
      (c, _) =>
      {
        int pool = 10 + c.CasterLevel * 5;
        c.AddFact(brick, pool);
        g.YouObserveSelf(c, $"You feel protected from {type.SubCat}. ({pool} absorbed)", $"{c:The} shimmers with protection.", "a warm hum");
      }, tags: AbilityTags.Beneficial);
}

public class FlyLesserBuff() : MaintainedBuff("spell_l3")
{
  public override string Id => "spb:fly";
  internal static readonly FlyLesserBuff Instance = new();
  public override string? BuffName => "Fly";
  public override bool IsBuff => true;
  public override StackMode StackMode => StackMode.Stack;
  public override int MaxStacks => 2;

  protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
  {
    "flying" => true,
    "speed_bonus" => new Modifier(ModifierCategory.UntypedStackable, fact.Stacks == 1 ? -4 : 0, "lesser fly"),
    _ => null
  };
}

public class HeroismBuff() : MaintainedBuff("spell_l3")
{
  public override string Id => "spb:heroism";
  internal static readonly HeroismBuff Instance = new();
  public override string? BuffName => "Heroism";

  protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
  {
    "attack_bonus" => new Modifier(ModifierCategory.StatusBonus, 2, "heroism"),
    "fortitude_save" or "reflex_save" or "will_save" => new Modifier(ModifierCategory.StatusBonus, 2, "heroism"),
    _ => null
  };
}
