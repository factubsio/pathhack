namespace Pathhack.Game;

public class BlindBuff : LogicBrick
{
    public static readonly BlindBuff Instance = new();
    public override string Id => "blind";

    public override bool IsBuff => true;
    public override string? BuffName => "Blind";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Severe;
    public override StackMode StackMode => StackMode.Stack;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.FalseWhen(CommonQueries.See);

    protected override void OnStackRemoved(Fact fact)
    {
        if (fact.Entity is not IUnit { IsPlayer: true }) return;
        if (fact.Stacks == 0)
            g.pline("You can see again.");
        else
            g.pline("Your vision clears slightly.");
    }
}

// Prone: -2 AC, half speed
public class ProneBuff : LogicBrick
{
    public static readonly ProneBuff Instance = new();
    public override string Id => "prone";
    public override bool IsBuff => true;
    public override string? BuffName => "Hamstrung";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Moderate;
    public override StackMode StackMode => StackMode.Stack;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "ac" => new Modifier(ModifierCategory.UntypedStackable, -2, "prone"),
        CommonQueries.SpeedPenaltyMul => 0.5,
        _ => null
    };
}

public class SilencedBuff : LogicBrick
{
    public static readonly SilencedBuff Instance = new();
    public override string Id => "silenced";
    public override bool IsBuff => true;
    public override string? BuffName => "Silenced";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Moderate;
    public override StackMode StackMode => StackMode.Stack;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "can_speak" => false,
        _ => null
    };
}

public class ParalyzedBuff : LogicBrick
{
    public static readonly ParalyzedBuff Instance = new();
    public override string Id => "paralyzed";
    public override bool IsBuff => true;
    public override bool IsActive => true;
    public override string? BuffName => "Paralyzed";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Critical;
    public override StackMode StackMode => StackMode.Stack;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key == "can_act" && !fact.Entity.Has(CommonQueries.ParalysisImmune) ? false : null;

    protected override void OnFactRemoved(Fact fact)
    {
        if (fact.Entity is IUnit { IsPlayer: true } _)
            g.pline("You can move again!");
    }
}

public class NauseatedBuff : LogicBrick
{
    public static readonly NauseatedBuff Instance = new();
    public override string Id => "nauseated";
    public override bool IsBuff => true;
    public override string? BuffName => "Nauseated";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Severe;
    public override StackMode StackMode => StackMode.Stack;

    protected override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (context.IsCheckingOwnerOf(fact))
        {
            context.Check!.Disadvantage++;
            fact.Entity.RemoveStack(Instance);
        }
    }
}

public class FleeingBuff : LogicBrick
{
    public static readonly FleeingBuff Instance = new();
    public override string Id => "fleeing";
    public override bool IsBuff => true;
    public override string? BuffName => "Fleeing";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Severe;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "fleeing" => true,
        _ => null
    };
}

public class StunnedBuff : LogicBrick
{
    public static readonly StunnedBuff Instance = new();
    public override string Id => "stunned";
    public override bool IsBuff => true;
    public override bool IsActive => true;
    public override string? BuffName => "Stunned";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Critical;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key == "can_act" && !fact.Entity.Has(CommonQueries.StunImmune) ? false : null;
}

public class BleedBuff : LogicBrick
{
    public static readonly BleedBuff Instance = new();
    public override string Id => "bleed";
    public override bool IsBuff => true;
    public override bool IsActive => true;
    public override string? BuffName => "Bleed";
    public override StackMode StackMode => StackMode.ExtendStacks;
    public override int MaxStacks => 10;
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Severe;

    public static bool TryApplyBleed(IUnit source, IUnit target, int stacks)
    {
        if (target.Has(CommonQueries.BleedImmune)) return false;
        using var ctx = PHContext.Create(source, Target.From(target));
        if (CheckFort(ctx, source.GetSpellDC(), "bleed")) return false;
        target.AddFact(Instance, source, count: stacks);
        return true;
    }

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        if (unit.Has(CommonQueries.BleedImmune)) { fact.Remove(); return; }

        using var ctx = PHContext.Create(fact.Source ?? DungeonMaster.Mook, Target.From(unit));
        ctx.Damage.Add(new DamageRoll { Formula = d(fact.Stacks, 4), Type = DamageTypes.Bleed });
        DoDamage(ctx);
    }

    protected override void OnFactAdded(Fact fact)
    {
        if (fact.Entity is IUnit unit)
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "start")} bleeding!");
    }

    protected override void OnAfterHealReceived(Fact fact, PHContext ctx)
    {
        if (!ctx.MagicalHeal) return;
        fact.Remove();
        if (ctx.Target.Unit is { } unit)
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "staunch")} the wound.");
    }
}

public static class CommonQueries
{
    public const string StunImmune = "stun_immunity";
    public const string DazeImmune = "daze_immune";
    public const string ParalysisImmune = "paralysis_immunity";
    public const string WebImmune = "web_immunity";
    public const string SleepImmune = "sleep_immunity";
    public const string BleedImmune = "bleed_immunity";
    public const string PoisonImmune = "poison_immunity";
    public const string ConfusionImmune = "confusion_immunity";
    public const string DifficultTerrainImmune = "difficult_terrain_immunity";
    public const string PetrificationImmune = "petrification_immunity";
    public const string See = "can_see";
    public const string Hallucinating = "hallucinating";

    public const string SpeedPenaltyMul = "speed_pen";
    public const string SpeedBonusMul = "speed_bon";
    public const string SpeedModifiersFlat = "speed_bonus";
}

public class AfflictionData
{
    public int NextTick;
    public int AppliedAt;
}

public abstract class AfflictionBrick(int dc, string? tag = null) : LogicBrick<AfflictionData>
{
    public int DC => dc;
    public string? Tag => tag;

    public override bool IsBuff => true;
    public override bool IsActive => true;
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Affliction;
    public override StackMode StackMode => StackMode.Stack;
    public override FactDisplayMode DisplayMode => FactDisplayMode.Name | FactDisplayMode.Stacks;

    public abstract string AfflictionName { get; }
    public abstract int MaxStage { get; }
    public abstract DiceFormula TickInterval { get; }
    public virtual int? AutoCureMax => null;
    public virtual string SaveKey => "fortitude_save";
    public virtual string? ImmunityKey => null;

    public override string? BuffName => AfflictionName;
    public override int MaxStacks => MaxStage + 1;

    public override LogicBrick? MergeWith(LogicBrick other) =>
        other is AfflictionBrick a && a.GetType() == GetType()
            ? (a.DC > DC ? a : this)
            : null;

    protected abstract void DoPeriodicEffect(Fact fact, IUnit unit, int stage);
    protected abstract object? DoQuery(int stage, string key, string? arg);

    protected static int Stage(Fact fact) => fact.Stacks - 1;

    protected override void OnFactAdded(Fact fact)
    {
        var data = X(fact);
        data.AppliedAt = g.CurrentRound;
        data.NextTick = g.CurrentRound + TickInterval.Roll();
    }

    protected override void OnFactRemoved(Fact fact) => OnCured((IUnit)fact.Entity);

    protected override void OnStackAdded(Fact fact)
    {
        int stage = Stage(fact);
        if (stage > 0)
            DoPeriodicEffect(fact, (IUnit)fact.Entity, stage);
    }

    protected override void OnRoundStart(Fact fact)
    {
        var data = X(fact);
        if (g.CurrentRound < data.NextTick) return;

        var unit = (IUnit)fact.Entity;

        if (tag != null && unit.Query<bool>("suppress_affliction", tag, MergeStrategy.Or, false)) return;

        data.NextTick = g.CurrentRound + TickInterval.Roll();

        // auto-cure
        int roundsAfflicted = g.CurrentRound - data.AppliedAt;
        if (AutoCureMax is int max && g.Rn2(max) < roundsAfflicted)
        {
            fact.Remove();
            return;
        }

        // save
        using var saveCtx = PHContext.Create(DungeonMaster.WithDC(DC), Target.From(unit));
        bool saved = CreateAndDoCheck(saveCtx, SaveKey, DC, AfflictionName);
        if (saved)
            unit.RemoveStack(this);
        else
            unit.AddFact(this, fact.Source);
    }

    protected virtual void OnCured(IUnit unit) => g.YouObserveSelf(unit, $"{unit:The} {VTense(unit, "feel")} better.", null);

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == Tag ? fact : DoQuery(Stage(fact), key, arg);
}

public class RegenBrick(params DamageType[] suppressedBy) : LogicBrick<RegenBrick.State>
{
    public override string Id => suppressedBy.Length == 0 ? "regen" : $"regen+{string.Join("/", suppressedBy.Select(t => t.SubCat))}";
    public class State
    {
        public int SuppressedUntil;
        public void Suppress() => SuppressedUntil = Math.Max(SuppressedUntil, g.CurrentRound + d(3).Roll());
        public bool IsSuppressed => g.CurrentRound < SuppressedUntil;
    }

    public override AbilityTags Tags => AbilityTags.Biological;
    public override bool IsActive => true;

    public static readonly RegenBrick Always = new();
    public static readonly RegenBrick FireOrAcid = new(DamageTypes.Fire, DamageTypes.Acid);
    public static readonly RegenBrick Fire = new(DamageTypes.Fire);
    public static readonly RegenBrick Acid = new(DamageTypes.Acid);

    public override string? PokedexDescription => $"Regen{SuppressingString}";
    private string SuppressingString => suppressedBy.Length switch
    {
        0 => "",
        _ => $"/{string.Join(",", suppressedBy.Select(x => x.SubCat.Capitalize()))}",
    };

    protected override void OnRoundEnd(Fact fact)
    {
        if (fact.Entity is not IUnit { } unit) return;
        if (X(fact).IsSuppressed) return;
        unit.HP += 1;
    }

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        foreach (var roll in ctx.Damage)
            if (suppressedBy.Contains(roll.Type))
            {
                X(fact).Suppress();
                return;
            }
    }

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "respawn_from_corpse" && !X(fact).IsSuppressed ? true : null;
}

public class ConfusedBuff : LogicBrick
{
    public static readonly ConfusedBuff Instance = new();
    public override string Id => "confused";
    public override bool IsBuff => true;
    public override string? BuffName => "Confused";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Severe;
    public override StackMode StackMode => StackMode.ExtendDuration;

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "confused" && !fact.Entity.Has(CommonQueries.ConfusionImmune) ? true : null;

    protected override void OnFactRemoved(Fact fact)
    {
        g.plineu((IUnit)fact.Entity, "You feel less confused now.");
    }

}

public class DazeImmunity : LogicBrick
{
    public static readonly DazeImmunity Instance = new();
    public override string Id => "daze_immune";
    public override string? BuffName => "Daze Immunity";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Low;
    public override StackMode StackMode => StackMode.Reject;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.TrueWhen(CommonQueries.DazeImmune);
}

public class DazedBuff : LogicBrick
{
    public static readonly DazedBuff Instance = new();
    public override string Id => "dazed";
    public override bool IsBuff => true;
    public override string? BuffName => "Dazed";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Critical;
    public override StackMode StackMode => StackMode.Reject;

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "can_act" && !fact.Entity.Has(CommonQueries.DazeImmune) ? false : null;

    protected override void OnFactRemoved(Fact fact)
    {
        if (fact.Entity is IUnit unit)
            g.Defer(() => unit.AddFact(DazeImmunity.Instance, null, 4));
    }
}

public class DazeOnHit : LogicBrick
{
    public static readonly DazeOnHit Instance = new();
    public override string Id => "daze_on_hit";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result || !ctx.Melee) return;
        if (ctx.Target?.Unit is { } target && !target.Has(CommonQueries.DazeImmune))
            target.AddFact(DazedBuff.Instance, ctx.Source, 1);
    }
}

public class BleedOnHit(int stacks) : LogicBrick
{
    public override string Id => $"bleed_on_hit+{stacks}";
    public override string? PokedexDescription => $"Inflicts {stacks} bleed on hit";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result || !ctx.Melee) return;
        if (ctx.Target?.Unit is not { } target) return;
        BleedBuff.TryApplyBleed((IUnit)fact.Entity, target, stacks);
    }

    public static readonly BleedOnHit S1 = new(1);
    public static readonly BleedOnHit S2 = new(2);
    public static readonly BleedOnHit S3 = new(3);
    public static readonly BleedOnHit S4 = new(4);
    public static readonly BleedOnHit S5 = new(5);
    public static readonly BleedOnHit S6 = new(6);
    public static readonly BleedOnHit S7 = new(7);
    public static readonly BleedOnHit S8 = new(8);
    public static readonly BleedOnHit S9 = new(9);
}

public class DisarmOnHit : LogicBrick
{
    public static readonly DisarmOnHit Instance = new();
    public override string Id => "disarm_on_hit";
    public override string? PokedexDescription => "Disarms on hit";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result || !ctx.Melee) return;
        if (ctx.Target?.Unit is not { } target) return;
        var weapon = target.GetWieldedItem();
        if (weapon.Def is not WeaponDef { Category: WeaponCategory.Item }) return;

        int dc = ((IUnit)fact.Entity).GetSpellDC();
        using var fctx = PHContext.Create((IUnit)fact.Entity, Target.From(target));
        if (!CheckFort(fctx, dc, "disarm"))
        {
            g.YouObserve(target, $"{fact.Entity:The} tries to disarm {target:the} but can't get a grip!");
            return;
        }

        weapon.Knowledge |= ItemKnowledge.BUC;
        if (!g.DoUnequip(target, weapon, free: true, consensual: false))
        {
            g.YouObserve(target, $"{fact.Entity:The} wrenches at {target:own} {weapon} but it's stuck!");
            return;
        }

        g.YouObserve(target, $"{fact.Entity:The} knocks {target:own} {weapon} free!");
    }
}

public class CursedWoundsBuff : LogicBrick
{
    public static readonly CursedWoundsBuff Instance = new();
    public override string Id => "cursed_wounds";
    public override bool IsBuff => true;
    public override string? BuffName => "Cursed Wounds";
    public override StackMode StackMode => StackMode.ExtendDuration;
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Severe;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.NumWhen("natural_regen_mult", 0.0);

    protected override void OnBeforeHealReceived(Fact fact, PHContext ctx)
    {
        ctx.HealModifiers.Mod(ModifierCategory.Override, -9999, "cursed wounds");
    }

    protected override void OnFactAdded(Fact fact)
    {
        if (fact.Entity is IUnit unit)
            g.YouObserveSelf(unit, "You feel extremely itchy.", $"{unit:The} wounds don't seem to close!");
    }

    protected override void OnFactRemoved(Fact fact)
    {
        if (fact.Entity is IUnit unit)
            g.YouObserveSelf(unit, "You feel extremely relieved.", $"{unit:own} wounds look more normal.");
    }
}

public class CursedWoundsOnHit : LogicBrick
{
    public static readonly CursedWoundsOnHit Instance = new();
    public override string Id => "cursed_wounds_on_hit";
    public override string? PokedexDescription => "Inflicts cursed wounds on hit (12 rounds)";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result || !ctx.Melee) return;
        if (ctx.Target?.Unit is not { } target) return;

        int dc = ((IUnit)fact.Entity).GetSpellDC();
        using var fctx = PHContext.Create((IUnit)fact.Entity, Target.From(target));
        if (CheckFort(fctx, dc, "cursed wounds")) return;

        target.AddFact(CursedWoundsBuff.Instance, ctx.Source, 12);
    }
}

public class TrueSeeingBuff : LogicBrick
{
    public static readonly TrueSeeingBuff Instance = new();
    public override string Id => "true_seeing";

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.TrueWhen("see_invisible");
}

public class HallucinatingBuff : LogicBrick
{
    public static readonly HallucinatingBuff Instance = new();
    public override string Id => "hallucinating";
    public override bool IsBuff => true;
    public override string? BuffName => "Hallucinating";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Severe;
    public override StackMode StackMode => StackMode.ExtendDuration;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.TrueWhen(CommonQueries.Hallucinating);

    protected override void OnFactAdded(Fact fact)
    {
        g.plineu((IUnit)fact.Entity, "Oh wow! Everything looks so cosmic!");
    }

    protected override void OnFactRemoved(Fact fact)
    {
        g.plineu((IUnit)fact.Entity, "Everything looks SO boring now.");
    }
}

public static class Hallucination
{
    static int _suppressed;

    public static bool Active => _suppressed == 0 && u?.Has(CommonQueries.Hallucinating) == true;

    public static Suppressor Suppress() => new();

    public readonly struct Suppressor : IDisposable
    {
        public Suppressor() => _suppressed++;
        public void Dispose() => _suppressed--;
    }

    public static Glyph ScrambleMonsterGlyph(Glyph real)
    {
        if (!Active) return real;
        var defs = AllMonsters.All;
        var pick = defs[g.Rn2(defs.Length)];
        return pick.Glyph with { Color = (ConsoleColor)g.Rn2(16) };
    }

    public static Glyph ScrambleItemGlyph(Glyph real)
    {
        if (!Active) return real;
        char cls = ItemClasses.Order[g.Rn2(ItemClasses.Order.Length)];
        return new(cls, (ConsoleColor)g.Rn2(16));
    }

    public static string ScrambleMonsterName()
    {
        int pick = g.Rn2(AllMonsters.All.Length + Dat.Hallucinations.BogusMonsters.Length);
        return pick < AllMonsters.All.Length
            ? AllMonsters.All[pick].Name
            : Dat.Hallucinations.BogusMonsters[pick - AllMonsters.All.Length];
    }

    public static string ScrambleItemName()
    {
        var bogus = Dat.Hallucinations.BogusItems;
        var real = AllItems.All;
        int pick = g.Rn2(real.Length + bogus.Length);
        return pick < real.Length ? real[pick].Name : bogus[pick - real.Length];
    }
}
