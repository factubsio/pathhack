namespace Pathhack.Game.Spells;

public static class BasicLevel1Spells
{
  public static readonly SpellBrick CureLightWounds = new("Cure light wounds", 1,
      """Heals a living creature for 1d6 per 2 caster levels, or damages undead.""",
      (c, t) =>
      {
        if (t.Pos == null) return;
        var target = lvl.UnitAt(c.Pos + t.Pos.Value);
        if (target == null) return;

        int dice = (1 + c.CasterLevel) / 2;
        using var ctx = PHContext.Create(c, t);

        if (target.IsCreature(CreatureTypes.Undead) == true)
        {
          ctx.Damage.Add(new()
          {
            Formula = d(dice, 8) + 2,
            Type = DamageTypes.Magic,
          });
          DoDamage(ctx);
          g.pline($"The positive energy sears {target:the}!");
        }
        else
        {
          g.DoHeal(c, target, d(dice, 6));
          g.pline($"{target:The} {VTense(target, "look")} a little better.");
        }
      }, TargetingType.Direction, tags: AbilityTags.Beneficial | AbilityTags.Heal);

  public static readonly SpellBrick MagicMissile = new("Magic missile", 1,
      """Unerring darts of force strike your target. 1d4+1 damage per missile, one missile plus one per 3 caster levels (max 4). Bounces off walls.""",
      (c, t) =>
      {
        if (t.Pos == null) return;

        int missiles = Math.Min(4, 1 + c.CasterLevel / 3);
        g.YouObserve(c, $"{{0:The}} {VTense(c, "cast")} magic missile!", "a magical hum");

        var range = g.RnRange(6, 10);
        Log.Write($"beam: range:{range}");

        foreach (var step in Beam.Fire(c.Pos, t.Pos.Value, canBounce: true, range))
        {
          var unit = lvl.UnitAt(step.Pos);

          if (unit != null)
          {
            Draw.AnimateBeam(step.SegmentStart, step.Pos, new Glyph('*', ConsoleColor.Magenta));
            g.YouObserve(unit, "The magic missile hits {0}.");
            using var ctx = PHContext.Create(c, Target.From(unit));
            for (int i = 0; i < missiles; i++)
            {
              ctx.Damage.Add(new()
              {
                Formula = d(4) + 1,
                Type = DamageTypes.Magic,
              });
            }
            DoDamage(ctx);
            return;
          }

          if (step.WillBounce || step.IsLast)
          {
            Draw.AnimateBeam(step.SegmentStart, step.Pos, new Glyph('*', ConsoleColor.Magenta));
          }
        }
      }, TargetingType.Direction);

  public static readonly SpellBrick Grease = new("Grease", 1,
      """You conjure a patch of grease, it's very slippy.""",
      (c, t) =>
      {
        g.YouObserve(c, "Greasy, yum!", "something squelches");
        var center = t.Pos!.Value;
        var area = new GreaseArea("Grease", c, c.GetSpellDC(), 6) { Tiles = [..center.CardinalNeighbours(true)] };
        lvl.CreateArea(area);
      }, TargetingType.Pos, maxRange: 4);

  public static readonly SpellBrick AcidArrow = new("Acid Arrow", 2,
      """You conjure an arrow of acid that continues corroding the target after it hits. Make a spell attack against the target. On a hit, you deal 3d8 acid damage plus 1d6 persistent acid damage.""",
      (c, t) =>
      {
        if (t.Pos == null) return;
        Pos dir = t.Pos.Value;

        var range = g.RnRange(6, 10);
        int dc = c.GetSpellDC();

        g.YouObserve(c, $"{c:The} {VTense(c, "fling")} an acid arrow!", "a hissing sound");
        
        Pos last = c.Pos;
        foreach (var step in Beam.Fire(c.Pos, t.Pos.Value, canBounce: false, range))
        {
          var unit = lvl.UnitAt(step.Pos);

          if (unit != null)
          {
            using var ctx = PHContext.Create(c, Target.From(unit));
            ctx.Spell = AcidArrow;
            ctx.Damage.Add(new()
            {
              Formula = d(3, 8),
              Type = DamageTypes.Acid,
            });

            Draw.AnimateProjectile(c.Pos, unit.Pos, new(dir.Char, ConsoleColor.Green));

            if (DoAttackRoll(ctx, 0))
            {
              g.YouObserve(unit, $"It hits {unit:the}!", "it hit something!");
              DoDamage(ctx);

              if (unit.IsDead) return;

              using var sizzleCtx = PHContext.Dupe();
              if (!CheckFort(sizzleCtx, unit.GetSpellDC(), "acid arrow sizzle"))
              {
                g.YouObserve(unit, $"{unit:The} is covered in acid!", "something scream!");
                unit.AddFact(AcidBurnBuff.Instance, 4);
              }
            }
            else
            {
              g.YouObserve(unit, $"It misses {unit:the}!");
            }
            return;
          }

          last = step.Pos;
        }

        Draw.AnimateProjectile(c.Pos, last, new(dir.Char, ConsoleColor.Green));

      }, TargetingType.Direction);


  const int BurningHandsRad = 3;
  public static readonly SpellBrick BurningHands = new("Burning hands", 1,
      """A cone of flame erupts from the caster's hands, dealing 2d6 fire damage. Reflex save for half.""",
      (c, t) =>
      {
        if (t.Pos == null) return;
        Pos dir = t.Pos.Value;

        using var cone = lvl.CollectCone(c.Pos, dir, BurningHandsRad);

        Draw.AnimateCone(c.Pos, cone,
            new Glyph('≈', ConsoleColor.Yellow),
            new Glyph('≈', ConsoleColor.Red),
            new Glyph('~', ConsoleColor.DarkRed));
        g.YouObserve(c, $"{{0:The}} {VTense(c, "shoot")} flames!", "a whoosh of flames");

        int dc = c.GetSpellDC();

        foreach (var pos in cone)
        {
          var victim = lvl.UnitAt(pos);
          if (victim.IsNullOrDead() || victim == c) continue;

          using var ctx = PHContext.Create(c, Target.From(victim));
          CheckReflex(ctx, dc, "fire");

          var dmg = new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Fire, HalfOnSave = true };
          ctx.Damage = [dmg];
          DoDamage(ctx);
        }
      }, TargetingType.Direction, maxRange: BurningHandsRad - 1);

  public static readonly SpellBrickBase Light = new ActivateMaintainedSpell("Light", 1,
      """You create an orb of light that circles the edge of your natural light radius, shedding bright light for a further 20-foot distance.""",
      (c) =>
      {
        g.YouObserve(c, $"{c:The} {VTense(c, "create")} a brightly glowing orb.");
      }, CastedLightBuff.Instance);

  public static readonly SpellBrickBase Shield = new ActivateMaintainedSpell("Shield", 1,
      """You raise a magical shield of force, giving you a +1 circumstance bonus to AC per 7 levels, but it doesn't require a hand to use.""", (c) =>
      {
        g.YouObserve(c, $"A glimmering shield materialises in front of {c:the}.", "a new hum");
      }, CastedShieldBuff.Instance);

  public static readonly SpellBrick Command = new("Command", 1,
      """You shout a command at a creature, forcing it to flee. Will save negates.""",
      (c, t) =>
      {
        if (t.Unit is not IUnit target) return;
        if (target.IsPlayer) { g.pline("It has no effect on you."); return; }
        if (target.Has("mindless")) { g.pline($"{target:The} is mindless."); return; }

        g.YouObserve(c, $"{c:The} {VTense(c, "command")} {target:the} to flee!", "a commanding shout");
        using var ctx = PHContext.Create(c, t);
        if (!CheckWill(ctx, c.GetSpellDC(), "command"))
        {
          g.pline($"{target:The} {VTense(target, "turn")} to flee!");
          target.AddFact(FleeingBuff.Instance.Timed(), 3 + c.CasterLevel / 3);
        }
        else
        {
          g.pline($"{target:The} {VTense(target, "resist")} the command.");
        }
      }, TargetingType.Unit, maxRange: 3);

  public static readonly SpellBrick FalseLifeLesser = new("False life, lesser", 1,
      """You gain a small amount of temporary hit points.""",
      (c, _) =>
      {
        int amount = d(4).Roll() + c.CasterLevel;
        c.GrantTempHp(amount);
        g.YouObserveSelf(c, $"You feel a surge of vitality! (+{amount} temp HP)", $"{c:The} looks more resilient.", "a surge of energy");
      }, tags: AbilityTags.Beneficial);

  public static SpellBrickBase ProtFromEvil => ProtectionFromAlignmentBuff.SpellEvil;
  public static SpellBrickBase ProtFromGood => ProtectionFromAlignmentBuff.SpellGood;
  public static SpellBrickBase ProtFromLaw => ProtectionFromAlignmentBuff.SpellLaw;
  public static SpellBrickBase ProtFromChaos => ProtectionFromAlignmentBuff.SpellChaos;
}

public class CastedLightBuff() : MaintainedBuff("spell_l1")
{
    public override string Id => "spb:light";
  internal static readonly CastedLightBuff Instance = new();
  public override string? BuffName => "Light";

  protected override object? OnQuery(Fact fact, string key, string? arg) =>
    key == "light_radius" ? new Modifier(ModifierCategory.CircumstanceBonus, 2, "light spell") : null;
}

public class CastedShieldBuff() : MaintainedBuff("spell_l1")
{
  public override string Id => "spb:shield";
  internal static readonly CastedShieldBuff Instance = new();
  public override string? BuffName => "Shield";

  protected override object? OnQuery(Fact fact, string key, string? arg) =>
    key == "ac" ? ShieldFormula(fact) : null;

  internal static Modifier ShieldFormula(Fact fact) =>
    new(ModifierCategory.StatusBonus, Math.Clamp(((fact.Entity as IUnit)?.EffectiveLevel ?? 1) / 7, 1, 3), "shield");
}

public class AcidBurnBuff : LogicBrick
{
  public override string Id => "spb:acid_burn";
  public override bool IsActive => true;
  public override bool IsBuff => true;
  public override string? BuffName => "Acid burn";

  public static readonly AcidBurnBuff Instance = new();

  private static readonly DamageRoll AcidBurnDamage = new()
  {
    Type = DamageTypes.Acid,
    Formula = d(6),
  };

  protected override void OnRoundStart(Fact fact)
  {
    if (fact.Entity is not IUnit unit) return;
    using var ctx = PHContext.Create(DungeonMaster.Mook, Target.From(unit));
    ctx.Damage.Add(AcidBurnDamage);
    g.YouObserve(unit, $"The acid on {unit:the} sizzles!");
    DoDamage(ctx);
  }
}

public class GreaseArea(string name, IUnit? source, int dc, int duration) : Area(duration)
{
  public override string Name => name;
  public override Glyph Glyph => new('~', ConsoleColor.DarkYellow);
  public override bool IsDifficultTerrain => true;

  private void TryTrip(IUnit unit)
  {
    if (unit.HasFact(ProneBuff.Instance)) return;

    using var ctx = PHContext.Create(source, Target.From(unit));
    var slips = VTense(unit, "slip");
    if (!CheckReflex(ctx, dc, "difficult_terrain"))
    {
      g.pline($"{unit:The} {slips} on some {name} and {VTense(unit, "fall")}!");
      unit.AddFact(ProneBuff.Instance.Timed(), 1);
    }
    else
    {
      g.pline($"{unit:The} {slips} on some {name} but {VTense(unit, "keep")} {unit:own} balance.");
    }
  }

  protected override void OnMove(IUnit unit) => TryTrip(unit);
  protected override void OnEnter(IUnit unit) => TryTrip(unit);

  protected override void OnTick()
  {
    foreach (var unit in Occupants)
    {
      if (unit.HasFact(ProneBuff.Instance)) unit.Energy -= unit.LandMove.Value;
    }
  }
}

public class ProtectionFromAlignmentBuff(string name, MoralAxis? moral, EthicalAxis? ethical) : MaintainedBuff("spell_l1")
{
  public override string Id => $"spb:prot_align+{moral}/{ethical}";
  public override string? BuffName => name;

  protected override void OnBeforeDefendRoll(Fact fact, PHContext ctx)
  {
    if (!ctx.IsCheckingOwnerOf(fact)) return;
    if (ctx.Source is not IUnit attacker) return;
    bool matches = (moral != null && attacker.MoralAxis == moral) || (ethical != null && attacker.EthicalAxis == ethical);
    if (matches)
      ctx.Check!.Modifiers.AddModifier(new(ModifierCategory.CircumstanceBonus, 1, name));
  }

  public static readonly ProtectionFromAlignmentBuff Evil = new("Prot. from Evil", MoralAxis.Evil, null);
  public static readonly ProtectionFromAlignmentBuff Good = new("Prot. from Good", MoralAxis.Good, null);
  public static readonly ProtectionFromAlignmentBuff Law = new("Prot. from Law", null, EthicalAxis.Lawful);
  public static readonly ProtectionFromAlignmentBuff Chaos = new("Prot. from Chaos", null, EthicalAxis.Chaotic);

  static ActivateMaintainedSpell MakeSpell(string label, ProtectionFromAlignmentBuff buff) => new(label, 1,
      $"You are protected from {label.Split(' ').Last().ToLower()} creatures, gaining +1 AC against their attacks.",
      c => g.YouObserveSelf(c, $"A ward shimmers around you.", $"A ward shimmers around {c:the}.", "a faint hum"),
      buff);

  public static readonly ActivateMaintainedSpell SpellEvil = MakeSpell("Protection from Evil", Evil);
  public static readonly ActivateMaintainedSpell SpellGood = MakeSpell("Protection from Good", Good);
  public static readonly ActivateMaintainedSpell SpellLaw = MakeSpell("Protection from Law", Law);
  public static readonly ActivateMaintainedSpell SpellChaos = MakeSpell("Protection from Chaos", Chaos);
}
