namespace Pathhack.Game.Spells;

public static class BasicLevel2Spells
{
  public static readonly SpellBrick ScorchingRay = new("Scorching ray", 2,
      """A blazing beam of fire. Make a spell attack roll for 4d6 fire damage.""",
      (c, t) =>
      {
        if (t.Pos == null) return;
        Pos dir = t.Pos.Value;

        g.YouObserve(c, $"{c:The} {VTense(c, "shoot")} a scorching ray!", "a searing hiss");

        Pos last = c.Pos;
        foreach (var step in Beam.Fire(c.Pos, dir, canBounce: false, g.RnRange(6, 10)))
        {
          var unit = lvl.UnitAt(step.Pos);
          if (unit != null)
          {
            Draw.AnimateBeam(step.SegmentStart, step.Pos, new Glyph('*', ConsoleColor.Red));
            using var ctx = PHContext.Create(c, Target.From(unit));
            ctx.Spell = ScorchingRay;
            ctx.Damage.Add(new() { Formula = d(4, 6), Type = DamageTypes.Fire });
            if (DoAttackRoll(ctx, 0))
            {
              g.YouObserve(unit, $"The ray hits {unit:the}!", "something sizzles!");
              DoDamage(ctx);
            }
            else
            {
              g.YouObserve(unit, $"The ray misses {unit:the}!");
            }
            return;
          }
          last = step.Pos;
          if (step.WillBounce || step.IsLast)
            Draw.AnimateBeam(step.SegmentStart, step.Pos, new Glyph('*', ConsoleColor.Red));
        }
      }, TargetingType.Direction);

  public static readonly SpellBrick SoundBurst = new("Sound burst", 2,
      """A burst of sonic energy. 2d8 sonic damage, fort save or stunned for 1 round.""",
      (c, t) =>
      {
        if (t.Pos == null) return;
        Pos center = t.Pos.Value;

        g.YouObserve(c, $"{c:The} {VTense(c, "unleash")} a burst of sound!", "a deafening boom");

        List<Pos> tiles = [];
        for (int dx = -1; dx <= 1; dx++)
          for (int dy = -1; dy <= 1; dy++)
          {
            Pos p = center + new Pos(dx, dy);
            if (lvl.InBounds(p)) tiles.Add(p);
          }

        Draw.AnimateFlash(tiles, new Glyph('*', ConsoleColor.Cyan));

        int dc = c.GetSpellDC();
        foreach (var pos in tiles)
        {
          var victim = lvl.UnitAt(pos);
          if (victim.IsNullOrDead() || victim == c) continue;

          using var ctx = PHContext.Create(c, Target.From(victim));
          bool saved = CheckFort(ctx, dc, "sound burst");
          ctx.Damage.Add(new() { Formula = d(2, 8), Type = DamageTypes.Force, HalfOnSave = true });
          DoDamage(ctx);
          if (!saved && !victim.IsDead)
            victim.AddFact(StunnedBuff.Instance.Timed(), 1);
        }
      }, TargetingType.Pos, maxRange: 6);

  public static readonly SpellBrickBase ResistFire = ResistEnergyBuff.MakeSpell("Resist fire", DamageTypes.Fire);
  public static readonly SpellBrickBase ResistCold = ResistEnergyBuff.MakeSpell("Resist cold", DamageTypes.Cold);
  public static readonly SpellBrickBase ResistShock = ResistEnergyBuff.MakeSpell("Resist shock", DamageTypes.Shock);
  public static readonly SpellBrickBase ResistAcid = ResistEnergyBuff.MakeSpell("Resist acid", DamageTypes.Acid);

  public static readonly SpellBrickBase DelayPoison = new ActivateMaintainedSpell("Delay poison", 2,
      """You suppress the effects of poison. While maintained, poison afflictions do not tick.""",
      c =>
      {
        g.YouObserveSelf(c, "You feel the poison slow in your veins.", $"{c:The} looks steadier.", "a calming hum");
      }, DelayPoisonBuff.Instance);

  public static readonly SpellBrick HoldPerson = new("Hold person", 2,
      """You freeze a humanoid in place. Will save negates.""",
      (c, t) =>
      {
        if (t.Unit is not IUnit target) return;
        if (!target.IsCreature(CreatureTypes.Humanoid))
        {
          g.pline($"{target:The} is not humanoid.");
          return;
        }

        g.YouObserve(c, $"{c:The} {VTense(c, "gesture")} at {target:the}!", "words of binding");
        using var ctx = PHContext.Create(c, t);
        if (!CheckWill(ctx, c.GetSpellDC(), "hold person"))
        {
          g.pline($"{target:The} {VTense(target, "freeze")} in place!");
          target.AddFact(ParalyzedBuff.Instance.Timed(), 3 + c.CasterLevel / 4);
        }
        else
        {
          g.pline($"{target:The} {VTense(target, "resist")} the hold.");
        }
      }, TargetingType.Unit, maxRange: 3);

  public static readonly SpellBrick DimensionDoor = new("Dimension door", 2,
      """You teleport to a nearby location. Without teleport control, you land on a random adjacent tile.""",
      (c, t) =>
      {
        if (t.Pos == null) return;
        Pos target = t.Pos.Value;

        if (!lvl.InBounds(target)) { g.pline("You can't teleport there."); return; }

        Pos landing;
        if (c.Has("teleport_control"))
        {
          if (!lvl.CanMoveTo(target, target, c) || lvl.UnitAt(target) != null)
          {
            g.pline("You can't land there.");
            return;
          }
          landing = target;
        }
        else
        {
          List<Pos> candidates = [];
          foreach (var p in target.Neighbours(andSelf: true))
          {
            if (lvl.InBounds(p) && lvl.CanMoveTo(p, p, c) && lvl.UnitAt(p) == null)
              candidates.Add(p);
          }
          if (candidates.Count == 0) { g.pline("There's no space to land."); return; }
          landing = candidates[g.Rn2(candidates.Count)];
        }

        g.YouObserveSelf(c,
            $"You vanish and reappear!",
            $"{c:The} vanishes!",
            "a pop of displaced air");
        lvl.MoveUnit(c, landing, free: true);
      }, TargetingType.Pos, maxRange: 8);
}

public class ResistEnergyBuff(string name, DamageType element) : MaintainedBuff("spell_l2")
{
  public override string? BuffName => name;

  protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
  {
    if (fact.Entity is not IUnit unit) return;
    int dr = 5 + unit.CasterLevel;
    foreach (var roll in ctx.Damage)
    {
      if (roll.Type == element)
        roll.ApplyDR(dr);
    }
  }

  static readonly ResistEnergyBuff Fire = new("Resist Fire", DamageTypes.Fire);
  static readonly ResistEnergyBuff Cold = new("Resist Cold", DamageTypes.Cold);
  static readonly ResistEnergyBuff Shock = new("Resist Shock", DamageTypes.Shock);
  static readonly ResistEnergyBuff Acid = new("Resist Acid", DamageTypes.Acid);

  public static ActivateMaintainedSpell MakeSpell(string label, DamageType type)
  {
    var buff = type == DamageTypes.Fire ? Fire : type == DamageTypes.Cold ? Cold : type == DamageTypes.Shock ? Shock : Acid;
    return new(label, 2,
        $"You gain resistance to {type.SubCat}, reducing incoming damage.",
        c => g.YouObserveSelf(c, $"You feel resistant to {type.SubCat}.", $"{c:The} shimmers briefly.", "a faint hum"),
        buff);
  }
}

public class DelayPoisonBuff() : MaintainedBuff("spell_l2")
{
  internal static readonly DelayPoisonBuff Instance = new();
  public override string? BuffName => "Delay Poison";

  protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
  {
    "suppress_affliction" when arg == "poison" => true,
    _ => null
  };
}
