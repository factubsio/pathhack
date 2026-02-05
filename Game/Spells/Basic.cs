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
      }, TargetingType.Direction);

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

  public static readonly SpellBrick BurningHands = new("Burning hands", 1,
      """A cone of flame erupts from the caster's hands, dealing 2d6 fire damage. Reflex save for half.""",
      (c, t) =>
      {
        if (t.Pos == null) return;
        Pos dir = t.Pos.Value;

        const int radius = 3;
        using var cone = lvl.CollectCone(c.Pos, dir, radius);

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
          CreateAndDoCheck(ctx, "reflex_save", dc, "fire");

          var dmg = new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Fire, HalfOnSave = true };
          ctx.Damage = [dmg];
          DoDamage(ctx);
        }
      }, TargetingType.Direction);
}