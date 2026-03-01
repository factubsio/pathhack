namespace Pathhack.Game.Bestiary;

// Ooze immunities: same as construct minus bleed (oozes can bleed)
public class OozeTraits : LogicBrick
{
    static readonly HashSet<string> Immunities =
    [
        CommonQueries.PoisonImmune,
        CommonQueries.SleepImmune,
        CommonQueries.ParalysisImmune,
        CommonQueries.ConfusionImmune,
        CommonQueries.StunImmune,
        CommonQueries.DazeImmune,
    ];

    public static readonly OozeTraits Instance = new();
    public override string Id => "ooze:traits";
    public override string? PokedexDescription => "Ooze: immune to mind-affecting, poison";

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        Immunities.Contains(key) ? true : null;
}

public class ConfuseOnHit(int duration) : LogicBrick
{
    public static readonly ConfuseOnHit Instance = new(3);
    public override string Id => "ooze:confuse_on_hit";
    public override string? PokedexDescription => $"Confuses on hit (will, {duration} rounds)";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result || !ctx.Melee) return;
        if (ctx.Target?.Unit is not { } target) return;
        if (target.Has(CommonQueries.ConfusionImmune)) return;
        var attacker = (IUnit)fact.Entity;
        using var fctx = PHContext.Create(attacker, Target.From(target));
        if (CheckWill(fctx, attacker.GetSpellDC(), "maddening slime")) return;
        target.AddFact(ConfusedBuff.Instance, attacker, duration);
    }
}

public class MagneticPullAura(int range) : LogicBrick
{
    public static readonly MagneticPullAura Instance = new(4);
    public override string Id => "ooze:magnetic_pull_aura";
    public override bool IsActive => true;
    public override string? PokedexDescription => $"Pulls creatures wearing metal armor closer (range {range})";

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        if (unit.CannotAct) return;

        if (g.Rn2(4) != 0) return;

        foreach (var victim in lvl.LiveUnits)
        {
            if (victim == unit || victim.IsDead) continue;
            if (unit.Pos.ChebyshevDist(victim.Pos) > range) continue;
            if (!victim.Equipped.TryGetValue(ItemSlots.BodySlot, out var armor) || !Golems.IsFerromagnetic(armor)) continue;

            Pos dir = (unit.Pos - victim.Pos).Signed;
            Pos dest = victim.Pos + dir;
            if (!lvl.InBounds(dest) || !lvl[dest].IsPassable || !lvl.NoUnit(dest)) continue;

            lvl.MoveUnit(victim, dest, true);
            g.YouObserveSelf(victim, "You are pulled by a magnetic force!", $"{victim:The} is pulled toward {unit:the}!");
        }
    }
}

public class PlasmaRay(int range, int cd)
    : CooldownAction("Plasma Ray", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    public override string? PokedexDescription => "4d8 shock + 4d8 fire beam";
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Line, unit, dir, MaxRange, ConsoleColor.Magenta, "plasma", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "plasma ray");
            ctx.Damage.Add(new DamageRoll { Formula = d(4, 8), Type = DamageTypes.Shock, HalfOnSave = true });
            ctx.Damage.Add(new DamageRoll { Formula = d(4, 8), Type = DamageTypes.Fire, HalfOnSave = true });
            DoDamage(ctx);
        });
    }

    public static readonly PlasmaRay Instance = new(8, 8);
}

public class ReactiveSlam : LogicBrick<ReactiveSlam.State>
{
    public static readonly ReactiveSlam Instance = new();
    public override string Id => "ooze:reactive_slam";
    public override bool IsActive => true;
    public override string? PokedexDescription => "Free slam when taking damage (suppressed by cold)";

    public class State { public int SuppressedUntil; }

    protected override void OnRoundStart(Fact fact)
    {
        // clear suppression naturally via timer
    }

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not IUnit unit) return;
        if (ctx.Source is not IUnit attacker || attacker.IsDM) return;

        if (ctx.Damage.Any(r => !r.Negated && r.Type == DamageTypes.Cold))
        {
            X(fact).SuppressedUntil = g.CurrentRound + 1;
            return;
        }

        if (X(fact).SuppressedUntil > g.CurrentRound) return;
        if (attacker.IsDead || unit.IsDead) return;
        if (unit.Pos.ChebyshevDist(attacker.Pos) > 1) return;

        g.YouObserve(unit, $"{unit:The} lashes out at {attacker:the}!", "a wet smack");
        DoWeaponAttack(unit, attacker, unit.GetWieldedItem());
    }
}

public class StunOnHit : LogicBrick
{
    public static readonly StunOnHit Instance = new();
    public override string Id => "ooze:stun_on_hit";
    public override string? PokedexDescription => "Stuns on hit (fort, low DC)";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result) return;
        if (ctx.Target?.Unit is not { } target) return;
        if (target.Has(CommonQueries.StunImmune)) return;
        var attacker = (IUnit)fact.Entity;
        int dc = attacker.GetSpellDC() - 4;
        using var fctx = PHContext.Create(attacker, Target.From(target));
        if (CheckFort(fctx, dc, "neurophagic jolt")) return;
        target.AddFact(StunnedBuff.Instance.Timed(), attacker, 1);
    }
}

public class AbsorbFlesh(int cd)
    : CooldownAction("Absorb Flesh", TargetingType.None, _ => cd, tags: AbilityTags.Heal)
{
    public override string? PokedexDescription => "Absorbs a corpse to heal half missing HP";
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        if (unit.HP.Current >= unit.HP.Max) return "full hp";
        for (int i = lvl.Corpses.Count - 1; i >= 0; i--)
        {
            var (corpse, pos) = lvl.Corpses[i];
            if (pos == unit.Pos && corpse.CorpseOf != null) return true;
        }
        return "no corpse";
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        for (int i = lvl.Corpses.Count - 1; i >= 0; i--)
        {
            var (corpse, pos) = lvl.Corpses[i];
            if (pos != unit.Pos || corpse.CorpseOf == null) continue;
            g.YouObserve(unit, $"{unit:The} absorbs the corpse of {corpse.CorpseOf.Name:the}!", "a wet slurping");
            lvl.RemoveItem(corpse, pos);
            int heal = (unit.HP.Max - unit.HP.Current) / 2;
            if (heal > 0) g.DoHeal(unit, unit, heal, magical: false);
            break;
        }
    }

    public static readonly AbsorbFlesh Instance = new(3);
}

public class HagEyeBeam(int range, int cd)
    : CooldownAction("Hag Eye Beam", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    public override string? PokedexDescription => "Fire beam (80%) or hold person (20%)";
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();
        bool holdPerson = g.Rn2(5) == 0;

        if (holdPerson)
        {
            BreathAttack.CollectBreath(BreathShape.Line, unit, dir, MaxRange, ConsoleColor.DarkMagenta, "baleful light", victim =>
            {
                using var ctx = PHContext.Create(unit, Target.From(victim));
                if (!CheckWill(ctx, dc, "hold person") && !victim.Has(CommonQueries.ParalysisImmune))
                    victim.AddFact(ParalyzedBuff.Instance.Timed(), unit, (d(4) + 2).Roll());
            });
        }
        else
        {
            BreathAttack.CollectBreath(BreathShape.Line, unit, dir, MaxRange, ConsoleColor.Red, "searing light", victim =>
            {
                using var atk = PHContext.Create(unit, Target.From(victim));
                atk.AttackType = AttackType.Spell;
                if (!DoAttackRoll(atk)) return;
                using var ctx = PHContext.Create(unit, Target.From(victim));
                CheckReflex(ctx, dc, "hag eye beam");
                ctx.Damage.Add(new DamageRoll { Formula = d(6, 6), Type = DamageTypes.Fire, HalfOnSave = true });
                DoDamage(ctx);
            });
        }
    }

    public static readonly HagEyeBeam Instance = new(6, 6);
}

public class BrainDrainBuff : LogicBrick
{
    public static readonly BrainDrainBuff Instance = new();
    public override string Id => "ooze:brain_drain";
    public override bool IsBuff => true;
    public override string? BuffName => "Brain Drain";
    public override string? PokedexDescription => "-1 Int per stack";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Severe;
    public override StackMode StackMode => StackMode.Stack;
    public override int MaxStacks => 8;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "stat/Int" => new Modifier(ModifierCategory.UntypedStackable, -1 * fact.Stacks, "brain drain"),
        _ => null
    };
}

public class BrainDrainOnHit : LogicBrick
{
    public static readonly BrainDrainOnHit Instance = new();
    public override string Id => "ooze:brain_drain_on_hit";
    public override string? PokedexDescription => "Drains Int on hit (50 round duration)";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result) return;
        if (ctx.Target?.Unit is not { } target) return;
        target.AddFact(BrainDrainBuff.Instance.Timed(), ctx.Source, duration: 50);
    }
}

public class AdaptiveResistance : LogicBrick<AdaptiveResistance.State>
{
    public static readonly AdaptiveResistance Instance = new();
    public override string Id => "ooze:adaptive";
    public override string? PokedexDescription => "Adapts resistance to last damage type taken";

    public class State
    {
        public DamageType LastPhysical;
        public DamageType LastEnergy;
    }

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        var state = X(fact);
        foreach (var roll in ctx.Damage)
        {
            if (roll.Negated) continue;
            if (roll.Type.Category == "phys") state.LastPhysical = roll.Type;
            else if (roll.Type.Category == "elem") state.LastEnergy = roll.Type;
        }
    }

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
        var state = X(fact);
        foreach (var roll in ctx.Damage)
        {
            if (roll.Type.Category == "phys" && state.LastPhysical != default && roll.Type == state.LastPhysical)
                roll.ApplyDR(5);
            else if (roll.Type.Category == "elem" && state.LastEnergy != default && roll.Type == state.LastEnergy)
                roll.ApplyDR(5);
        }
    }
}

public class CorrosionOnBeingHit : LogicBrick
{
    public static readonly CorrosionOnBeingHit Instance = new();
    public override string Id => "ooze:corrosion";
    public override string? PokedexDescription => "Corrodes attacker's weapon when hit";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (ctx.Source is not IUnit attacker || attacker.IsDM) return;
        if (ctx.Weapon is not { Def: WeaponDef } || ctx.Weapon.Def.IsEphemeral) return;
        var result = ctx.Weapon.TryDegrade();
        Item.PrintDegrade(attacker, ctx.Weapon, result);
    }
}

public class SplitOnSlashPierce : LogicBrick
{
    public static readonly SplitOnSlashPierce Instance = new();
    public override string Id => "ooze:split";
    public override string? PokedexDescription => "Splits in two when hit by slashing or piercing";

    class Regrowing : LogicBrick
    {
        public override string Id => "ooze:split.regrowing";
        public static readonly Regrowing Instance = new();
    }

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not Monster ooze) return;
        if (ooze.FindFactOfType<Regrowing>() != null) return;
        if (ooze.HP.Max < 10) return;
        if (ooze.HP.Current > ooze.HP.Max / 2) return;

        if (!ctx.Damage.Any(r => !r.Negated && r.Type.SubCat is DamageTypes.P_Slashing or DamageTypes.P_Piercing)) return;

        int cloneMaxHp = ooze.HP.Max / 2;
        int cloneHp = cloneMaxHp * 2 / 3;

        ooze.AddFact(Regrowing.Instance, ooze, 8);

        MonsterDef def = ooze.Def;
        Pos pos = ooze.Pos;
        g.YouObserve(ooze, $"{ooze:The} splits in two!", "a wet tearing sound");
        g.Defer(() =>
        {
            if (!lvl.FirstFreeAdjacent(pos, out var spot)) return;
            var clone = Monster.Spawn(def, "split");
            clone.HP.BaseMax = cloneMaxHp;
            clone.HP.Max = cloneMaxHp;
            clone.HP.Current = cloneHp;
            clone.AddFact(Regrowing.Instance, ooze, 4);
            lvl.PlaceUnit(clone, spot);
        });
    }
}

public class EmotionalBacklash : LogicBrick
{
    public static readonly EmotionalBacklash Instance = new();
    public override string Id => "ooze:emotional_backlash";
    public override string? PokedexDescription => "Random status effect on attacker when hit in melee";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (!ctx.Melee) return;
        if (fact.Entity is not IUnit ooze) return;
        if (ctx.Source is not IUnit attacker || attacker.IsDM) return;
        int dc = ooze.GetSpellDC() - 2;
        using var fctx = PHContext.Create(ooze, Target.From(attacker));
        if (CheckWill(fctx, dc, "emotional backlash")) return;
        switch (g.Rn2(4))
        {
            case 0: attacker.AddFact(ConfusedBuff.Instance, ooze, 3); break;
            case 1 when !attacker.Has(CommonQueries.DazeImmune): attacker.AddFact(DazedBuff.Instance, ooze, 1); break;
            case 2: attacker.AddFact(HallucinatingBuff.Instance, ooze, 3); break;
            case 3: attacker.AddFact(SilencedBuff.Instance.Timed(), ooze, 2); break;
        }
    }
}

public class ParalyzeGaze : LogicBrick
{
    public static readonly ParalyzeGaze Instance = new();
    public override string Id => "ooze:paralyze_gaze";
    public override string? PokedexDescription => "Paralyzes melee attackers who can see it";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (!ctx.Melee) return;
        if (fact.Entity is not IUnit ooze) return;
        if (ctx.Source is not IUnit attacker || attacker.IsDM) return;
        if (attacker.Has(CommonQueries.ParalysisImmune)) return;
        if (!GameState.CanSee(attacker, ooze)) return;
        int dc = ooze.GetSpellDC();
        using var fctx = PHContext.Create(ooze, Target.From(attacker));
        if (CheckWill(fctx, dc, "compelling reflection")) return;
        attacker.AddFact(ParalyzedBuff.Instance.Timed(), ooze, (d(6) + 3).Roll());
    }
}

public static class Oozes
{
    public static readonly MonsterFamily Family = new("ooze");

    static MonsterDef O(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 6, int ac = 0, int ab = 0,
        UnitSize size = UnitSize.Medium, ActionCost? speed = null,
        WeaponDef? unarmed = null, int spawnWeight = 10,
        int maxDepth = 99, GroupSize group = GroupSize.None,
        bool isHider = false, string? revealMessage = null, string? revealSound = null,
        bool stationary = false, MonFlags extraFlags = MonFlags.None) => new()
    {
        id = $"ooze_{id}",
        Name = name,
        Family = Family,
        CreatureType = CreatureTypes.Ooze,
        Glyph = new('P', color),
        HpPerLevel = hp,
        AC = ac,
        AttackBonus = ab,
        Unarmed = unarmed ?? NaturalWeapons.Slam_1d4,
        LandMove = speed ?? ActionCosts.LandMove15,
        Size = size,
        BaseLevel = level,
        SpawnWeight = spawnWeight,
        MaxDepth = maxDepth,
        GroupSize = group,
        Stationary = stationary,
        IsHider = isHider,
        RevealMessage = revealMessage,
        RevealSound = revealSound,
        BrainFlags = MonFlags.NoCorpse | extraFlags,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        Components = [OozeTraits.Instance, ..components,
            new GrantAction(new NaturalAttack(unarmed ?? NaturalWeapons.Slam_1d4))],
    };

    // --- Level 1-2: early oozes ---

    public class SanguineSwarm(Pos where) : Swarm("Sanguine Ooze Swarm", new('µ', ConsoleColor.DarkRed), 1, where)
    {
        protected override void SwarmUnit(IUnit unit)
        {
            using var ctx = PHContext.Create(DungeonMaster.Mook, Target.From(unit));
            g.YouObserveSelf(unit, "Euphoric ooze seeps over you!", $"ooze seeps over {unit:the}!", "a wet squelching");
            ctx.Damage.Add(new() { Formula = d(2), Type = DamageTypes.Acid });
            DoDamage(ctx);
            if (!CheckWill(ctx, 11, "euphoric slime"))
                unit.AddFact(HallucinatingBuff.Instance, null, 3);
        }
    }

    public class CholericSwarm(Pos where) : Swarm("Choleric Ooze Swarm", new('µ', ConsoleColor.DarkGreen), 2, where)
    {
        protected override void SwarmUnit(IUnit unit)
        {
            using var ctx = PHContext.Create(DungeonMaster.Mook, Target.From(unit));
            CheckReflex(ctx, 13, "corrosive slime");
            g.YouObserveSelf(unit, "Corrosive ooze burns you!", $"ooze burns {unit:the}!", "a hissing sound");
            ctx.Damage.Add(new() { Formula = d(4), Type = DamageTypes.Acid, HalfOnSave = true });
            DoDamage(ctx);

            if (g.Rn2(3) != 0) return;
            int roll = g.Rn2(3);
            Item? target = roll switch
            {
                0 => unit.Equipped.GetValueOrDefault(ItemSlots.MainHandSlot),
                1 => unit.Equipped.GetValueOrDefault(ItemSlots.OffHandSlot),
                _ => unit.Equipped.GetValueOrDefault(ItemSlots.BodySlot),
            };
            if (target is not { Def: WeaponDef or ArmorDef }) return;
            var result = target.TryDegrade();
            Item.PrintDegrade(unit, target, result);
        }
    }

    public static readonly MonsterDef Phlegmatic = O("phlegmatic", "phlegmatic ooze", 1, ConsoleColor.DarkCyan,
        [
            ConfuseOnHit.Instance,
            EnergyResist.Cold.DR5,
        ], hp: 4, size: UnitSize.Tiny, unarmed: NaturalWeapons.Slam_1d4,
        speed: ActionCosts.LandMove10);

    public static readonly MonsterDef Melancholic = O("melancholic", "melancholic ooze", 2, ConsoleColor.DarkBlue,
        [
            DazeOnHit.Instance,
            EnergyResist.Shock.DR5,
        ], hp: 4, size: UnitSize.Tiny, unarmed: NaturalWeapons.Slam_1d4,
        speed: ActionCosts.LandMove10);

    public static readonly MonsterDef Garden = O("garden", "garden ooze", 2, ConsoleColor.Green,
        [
            MeleeDamageRider.Acid_1d4,
        ], hp: 4, size: UnitSize.Small, unarmed: NaturalWeapons.Slam_1d4,
        isHider: true, revealMessage: "oozes out of the vegetation!", revealSound: "a squelching sound");

    public static readonly MonsterDef Sapphire = O("sapphire", "sapphire ooze", 2, ConsoleColor.Blue,
        [
            SimpleDR.Blunt.DR5,
            MeleeDamageRider.Cold_1d4,
        ], hp: 4, unarmed: NaturalWeapons.Slam_1d4);

    public static readonly MonsterDef Warpglass = O("warpglass", "warpglass ooze", 2, ConsoleColor.Cyan,
        [
            SimpleDR.Blunt.DR5,
            MeleeDamageRider.Acid_1d4,
            ParalyzeGaze.Instance,
        ], hp: 4, unarmed: NaturalWeapons.Slam_1d4);

    // --- Level 3-4: ---

    public static readonly MonsterDef HagEye = O("hag_eye", "hag eye ooze", 3, ConsoleColor.DarkGreen,
        [
            new GrantSpell(BasicLevel1Spells.MagicMissile),
            new GrantSpell(BasicLevel2Spells.HoldPerson),
            ..GrantPool.StandardLevel2Caster,
        ], hp: 5, size: UnitSize.Small, unarmed: NaturalWeapons.Slam_2d6,
        isHider: true, revealMessage: "opens a baleful eye!", revealSound: "a wet squelch",
        extraFlags: MonFlags.PrefersCasting);

    public static readonly MonsterDef Gray = O("gray", "gray ooze", 4, ConsoleColor.Gray,
        [
            GrabOnHit.Instance,
            Constrict.MediumAcid,
            EnergyResist.Cold.Immune,
            EnergyResist.Fire.Immune,
        ], hp: 6, unarmed: NaturalWeapons.Slam_1d6,
        isHider: true, revealMessage: "surges up from the floor!", revealSound: "a wet slithering");

    // --- Level 6: ---

    public static readonly MonsterDef Emotion = O("emotion", "emotion ooze", 6, ConsoleColor.Magenta,
        [
            EmotionalBacklash.Instance,
        ], unarmed: NaturalWeapons.Slam_1d6);

    public static readonly MonsterDef Verdurous = O("verdurous", "verdurous ooze", 6, ConsoleColor.DarkGreen,
        [
            MeleeDamageRider.Acid_1d4,
            SplitOnSlashPierce.Instance,
            DazeAura.Instance,
            SimpleDR.Slashing.DR5,
            SimpleDR.Piercing.DR5,
            EnergyResist.Fire.Immune,
        ], unarmed: NaturalWeapons.Slam_2d6,
        stationary: true, speed: ActionCosts.LandMove10);

    // --- Level 7: ---

    public static readonly MonsterDef Adaptive = O("adaptive", "adaptive ooze", 7, ConsoleColor.DarkYellow,
        [
            AdaptiveResistance.Instance,
        ], size: UnitSize.Large, unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef Benaioh = O("benaioh", "benaioh", 7, ConsoleColor.DarkGray,
        [
            GrabOnHit.Instance,
            Constrict.Medium,
            FoodPoisoning.Instance.OnHit(), // TODO: proper benaioh disease
            // TODO: Engulf mechanic
        ], size: UnitSize.Large, unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef BlackPudding = O("black_pudding", "black pudding", 7, ConsoleColor.DarkGray,
        [
            MeleeDamageRider.Acid_2d6,
            Thorns.Acid_1d6,
            SplitOnSlashPierce.Instance,
            CorrosionOnBeingHit.Instance,
        ], size: UnitSize.Huge, unarmed: NaturalWeapons.Slam_2d6,
        speed: ActionCosts.LandMove20);

    public static readonly MonsterDef Brain = O("brain", "brain ooze", 7, ConsoleColor.Magenta,
        [
            BrainDrainOnHit.Instance,
            new QueryBrick(CreatureTags.Flying, true),
            ..GrantPool.StandardLevel2Caster,
            new GrantSpell(BasicLevel1Spells.Grease),
            new GrantSpell(BasicLevel1Spells.MagicMissile),
            new GrantSpell(BasicLevel2Spells.AcidArrow),
            new GrantSpell(BasicLevel2Spells.ScorchingRay),
            new GrantSpell(BasicLevel2Spells.SoundBurst),
        ], hp: 5, ac: 2, size: UnitSize.Tiny, unarmed: NaturalWeapons.Tendril_1d4,
        speed: ActionCosts.StandardLandMove, // flies, fast
        extraFlags: MonFlags.PrefersCasting);

    public static readonly MonsterDef Magma = O("magma", "magma ooze", 7, ConsoleColor.Red,
        [
            MeleeDamageRider.Fire_2d6,
            GrabOnHit.Instance,
            Constrict.Medium,
            EnergyResist.Fire.Immune,
            VulnerableToElement.Cold, // vuln water — cold is close enough
        ], size: UnitSize.Large, unarmed: NaturalWeapons.Slam_1d6,
        speed: ActionCosts.LandMove10);

    // --- Level 8: ---

    public static readonly MonsterDef Deathtrap = O("deathtrap", "deathtrap ooze", 8, ConsoleColor.DarkYellow,
        [
            MeleeDamageRider.Acid_2d6,
            GrabOnHit.Instance,
            Constrict.Medium,
        ], size: UnitSize.Large, unarmed: NaturalWeapons.Slam_2d6,
        isHider: true, revealMessage: "springs to life!", revealSound: "a grinding of stone");

    // --- Level 9: ---

    public static readonly MonsterDef Coven = O("coven", "coven ooze", 9, ConsoleColor.DarkMagenta,
        [
            EnergyResist.Acid.Immune,
            EnergyResist.Cold.Immune,
            EnergyResist.Fire.DR10,
            new GrantAction(HagEyeBeam.Instance),
            new GrantAction(AbsorbFlesh.Instance),
            SplitOnSlashPierce.Instance,
        ], size: UnitSize.Large, unarmed: NaturalWeapons.Slam_1d6,
        speed: ActionCosts.LandMove20);

    public static readonly MonsterDef Mimic = O("mimic", "mimic ooze", 9, ConsoleColor.White,
        [
            EnergyResist.Acid.Immune,
            // TODO: mimic mechanics (disguise as item/feature)
        ], unarmed: NaturalWeapons.Slam_1d6,
        isHider: true, revealMessage: "reveals its true form!", revealSound: "a wet shifting");

    // --- Level 11: ---

    public static readonly MonsterDef Capacitor = O("capacitor", "capacitor ooze", 11, ConsoleColor.Yellow,
        [
            MeleeDamageRider.Shock_2d6,
            ShockRetaliation.Instance, // TODO: should only trigger on metal weapons
            EnergyResist.Shock.Immune,
            StunOnHit.Instance,
        ], unarmed: NaturalWeapons.Slam_2d6,
        speed: ActionCosts.LandMove10);

    public static readonly MonsterDef GreaterVerdurous = O("greater_verdurous", "greater verdurous ooze", 11, ConsoleColor.Green,
        [
            MeleeDamageRider.Acid_2d6,
            EnergyResist.Fire.Immune,
            DazeAura.Instance,
            SplitOnSlashPierce.Instance,
        ], size: UnitSize.Large, unarmed: NaturalWeapons.Slam_2d6,
        speed: ActionCosts.LandMove20);

    static readonly BreathAttack AcidSplatter = new(BreathShape.Line, DamageTypes.Acid, ConsoleColor.Green, _ => 8, _ => d(4, 6), 4);

    public static readonly MonsterDef Putrid = O("putrid", "putrid ooze", 11, ConsoleColor.DarkGreen,
        [
            MeleeDamageRider.Acid_2d6,
            GrabOnHit.Instance,
            Constrict.Heavy,
            FlatDR.DR10,
            EnergyResist.Shock.DR20,
            EnergyResist.Fire.DR20,
            new GrantAction(AcidSplatter),
        ], size: UnitSize.Huge, unarmed: NaturalWeapons.Slam_2d6);

    // --- Level 13+: late ---

    public static readonly MonsterDef Carnivorous = O("carnivorous", "carnivorous blob", 13, ConsoleColor.DarkRed,
        [
            GrabOnHit.Instance,
            Constrict.Crushing,
            FlatDR.DR10,
            EnergyResist.Acid.Immune,
            EnergyResist.Shock.DR20,
            EnergyResist.Fire.DR20,
            VulnerableToElement.Cold,
            ReactiveSlam.Instance,
            SplitOnSlashPierce.Instance,
            new GrantAction(AbsorbFlesh.Instance),
        ], size: UnitSize.Gargantuan, unarmed: NaturalWeapons.Slam_4d8,
        speed: ActionCosts.LandMove20);

    public static readonly MonsterDef Gunpowder = O("gunpowder", "gunpowder ooze", 14, ConsoleColor.DarkRed,
        [
            GrabOnHit.Instance,
            Constrict.Heavy,
            EnergyResist.Cold.Immune,
            Thorns.Fire_2d8,
            new GrantAction(CannonSplash.Instance),
            DeathExplosion.Fire_10d8_R1,
        ], size: UnitSize.Large, unarmed: NaturalWeapons.Slam_2d6,
        speed: ActionCosts.LandMove20);

    public static readonly MonsterDef Plasma = O("plasma", "plasma ooze", 16, ConsoleColor.Magenta,
        [
            MeleeDamageRider.Shock_2d6,
            MeleeDamageRider.Fire_2d6,
            new QueryBrick(CreatureTags.Flying, true),
            GrabOnHit.Instance,
            Constrict.Crushing,
            FlatDR.DR10, // DR 15/— in source, toned down
            EnergyResist.Acid.Immune,
            EnergyResist.Shock.Immune,
            EnergyResist.Fire.Immune,
            EnergyResist.Cold.DR20,
            new GrantAction(PlasmaRay.Instance),
            MagneticPullAura.Instance,
            SplitOnSlashPierce.Instance,
        ], size: UnitSize.Gargantuan, unarmed: NaturalWeapons.Slam_4d8,
        speed: ActionCosts.LandMove20);

    public static readonly MonsterDef[] All =
    [
        Phlegmatic, Melancholic, Garden, Sapphire, Warpglass,
        HagEye, Gray,
        Emotion, Verdurous,
        Adaptive, Benaioh, BlackPudding, Brain, Magma,
        Deathtrap,
        Coven, Mimic,
        Capacitor, GreaterVerdurous, Putrid,
        Carnivorous, Gunpowder, Plasma,
    ];
}
