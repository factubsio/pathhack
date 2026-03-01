namespace Pathhack.Game.Bestiary;

public class ConstructTraits(string id, HashSet<string> immunities) : LogicBrick
{
    // A bit clunky but iiwii
    static readonly HashSet<string> AlwaysImmune =
    [
        CommonQueries.PoisonImmune,
        CommonQueries.SleepImmune,
        CommonQueries.ParalysisImmune,
        CommonQueries.ConfusionImmune,
        CommonQueries.StunImmune,
        CommonQueries.DazeImmune,
    ];
    static readonly HashSet<string> WithoutBleedImmunity =
    [
        ..AlwaysImmune
    ];
    static readonly HashSet<string> AllImmunities =
    [
        ..AlwaysImmune,
        CommonQueries.BleedImmune,
    ];
    static readonly HashSet<string> AllWithPetrification =
    [
        ..AllImmunities,
        CommonQueries.PetrificationImmune,
    ];

    public static readonly ConstructTraits Instance = new("construct:traits", AllImmunities);
    public static readonly ConstructTraits WithoutBleed = new("construct:traits_without_bleed_immune", WithoutBleedImmunity);
    public static readonly ConstructTraits WithPetrification = new("construct:traits_petrification", AllWithPetrification);

    public override string Id => id;
    public override string? PokedexDescription => $"Immune to {string.Join(", ", immunities)}";

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        immunities.Contains(key) ? true : null;
}

public class HealsFromElement(DamageType type) : LogicBrick
{
    public override string Id => $"heals_from+{type.SubCat}";
    public override string? PokedexDescription => $"Heals from {type.SubCat}";

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
        foreach (var roll in ctx.Damage)
            if (roll.Type == type) roll.IsAttuned = true;
    }

    public static readonly HealsFromElement Fire = new(DamageTypes.Fire);
    public static readonly HealsFromElement Cold = new(DamageTypes.Cold);
    public static readonly HealsFromElement Shock = new(DamageTypes.Shock);
    public static readonly HealsFromElement Acid = new(DamageTypes.Acid);
}

public class SlowedByElement(DamageType type) : LogicBrick
{
    public override string Id => $"slowed_by+{type.SubCat}";
    public override string? PokedexDescription => $"Slowed by {type.SubCat}";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not IUnit unit) return;
        if (ctx.Damage.Any(r => r.Type == type && !r.Negated))
            unit.AddFact(SlowBuff.Half, ctx.Source, 3);
    }

    public static readonly SlowedByElement Fire = new(DamageTypes.Fire);
    public static readonly SlowedByElement Cold = new(DamageTypes.Cold);
    public static readonly SlowedByElement Shock = new(DamageTypes.Shock);
}

public class VulnerableToElement(DamageType type) : LogicBrick
{
    public override string Id => $"vulnerable+{type.SubCat}";
    public override string? PokedexDescription => $"Vulnerable to {type.SubCat}";

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
        foreach (var roll in ctx.Damage)
            if (roll.Type == type) roll.Double();
    }

    public static readonly VulnerableToElement Fire = new(DamageTypes.Fire);
    public static readonly VulnerableToElement Cold = new(DamageTypes.Cold);
    public static readonly VulnerableToElement Shock = new(DamageTypes.Shock);
    public static readonly VulnerableToElement Acid = new(DamageTypes.Acid);
    public static readonly VulnerableToElement Sonic = new(DamageTypes.Sonic);
}

public class CarrionAura : LogicBrick
{
    public override string Id => "golem:carrion_aura";
    public override bool IsActive => true;

    public static readonly CarrionAura Instance = new();

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit un) return;
        if (un.CannotAct) return;
        if (g.Rn2(3) > 0) return;

        foreach (var tgt in lvl.AdjacentUnits(un.Pos))
        {
            if (tgt.Has(CommonQueries.DazeImmune)) continue;
            tgt.AddFact(DazedBuff.Instance, un, 1);
        }
    }
}

public class GolemHasteBuff : LogicBrick
{
    public static readonly GolemHasteBuff Instance = new();
    public override string Id => "golem:haste";
    public override bool IsBuff => true;
    public override bool IsActive => true;
    public override string? BuffName => "Haste";
    public override StackMode StackMode => StackMode.Reject;

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        if (unit.CannotAct) return;
        unit.Energy += 4;
    }
}

public class GolemHaste(int cd)
    : CooldownAction("Haste", TargetingType.None, _ => cd, tags: AbilityTags.Beneficial)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        if (unit.HasFact(GolemHasteBuff.Instance)) return "already hasted";
        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "surge")} with speed!", "a grinding acceleration");
        unit.AddFact(GolemHasteBuff.Instance, null, 5);
    }

    public static readonly GolemHaste Instance = new(12);
}

public class AlchemicalBomb(int range, int cd)
    : CooldownAction("Alchemical Bomb", TargetingType.Unit, _ => cd, tags: AbilityTags.Harmful, maxRange: range)
{
    static readonly BottleDef[] Bombs = [Bottles.AlchemistFireBottle, Bottles.AlchemistAcidBottle, Bottles.AlchemistFrostBottle, Bottles.AlchemistShockBottle];

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit == null) return;
        var bottle = Bombs.Pick();
        var item = Item.Create(bottle);

        if (YouCanObserve(unit)) item.Def.SetKnown();
        g.YouObserve(unit, $"{unit:The} throws {item:an}!");
        if (YouCanObserve(target.Unit)) item.Def.SetKnown();
        ThrowLands(unit, item, target.Unit.Pos, null);
    }

    public static readonly AlchemicalBomb Instance = new(4, 3);
}

public class CoralSpike(int range, int cd)
    : CooldownAction("Coral Spike", TargetingType.Direction, _ => cd, tags: AbilityTags.Harmful, maxRange: range)
{
    static readonly WeaponDef SpikeProjectile = new()
    {
        id = "coral_spike",
        Name = "coral spike",
        BaseDamage = d(8),
        Style = WeaponStyle.Exotic,
        Grip = WeaponGrip.Exotic,
        DamageType = DamageTypes.Piercing,
        Glyph = new('/', ConsoleColor.DarkCyan),
        Launcher = "coral",
        Weight = -1,
        Price = -1,
        Components = [BleedOnHit.S1],
    };

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        var item = Item.Create(SpikeProjectile);
        g.YouObserve(unit, $"{unit:The} hurls a coral spike!", "a sharp whistle");
        DoThrow(unit, item, dir, AttackType.Thrown);
    }

    public static readonly CoralSpike Instance = new(5, 8);
}

public class SandBlast(int range, int cd)
    : CooldownAction("Sand Blast", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Cone, unit, dir, MaxRange, ConsoleColor.Yellow, "sand", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "sand blast");
            ctx.Damage.Add(new DamageRoll { Formula = d(3, 6), Type = DamageTypes.Blunt, HalfOnSave = true });
            DoDamage(ctx);
            if (!ctx.Check!.Result)
                victim.AddFact(BlindBuff.Instance.Timed(), unit, (d(4) + 2).Roll());
        });
    }

    public static readonly SandBlast Instance = new(3, 8);
}

public class NecroticAura(Dice damage) : LogicBrick
{
    public override string Id => "golem:necrotic_aura";
    public override bool IsActive => true;
    public override string? PokedexDescription => $"Necrotic aura: {damage} negative energy to adjacent";

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        if (unit.CannotAct) return;
        int dc = unit.GetSpellDC();

        foreach (var victim in lvl.AdjacentUnits(unit.Pos))
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckWill(ctx, dc, "negative energy");
            ctx.Damage.Add(new DamageRoll { Formula = damage, Type = DamageTypes.Negative, HalfOnSave = true });
            DoDamage(ctx);
        }
    }

    public static readonly NecroticAura Instance = new(d(8));
}

public class NecroticBurst(Dice damage, int radius, int cd)
    : CooldownAction("Necrotic Burst", TargetingType.None, _ => cd, tags: AbilityTags.Harmful)
{
    public override string? PokedexDescription => $"Burst of negative energy: {damage}, radius {radius}";

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        if (target.Unit == null) return "no target";
        if (unit.Pos.ChebyshevDist(target.Unit.Pos) > radius) return "too far";
        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "unleash")} a wave of necrotic energy!", "a pulse of dread");
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Burst, unit, Pos.Zero, radius, ConsoleColor.DarkMagenta, "necrotic energy", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckWill(ctx, dc, "negative energy");
            ctx.Damage.Add(new DamageRoll { Formula = damage, Type = DamageTypes.Negative, HalfOnSave = true });
            DoDamage(ctx);
        }, AreaSystem.OnTile(DamageTypes.Negative));
    }

    public static readonly NecroticBurst Instance = new(d(2, 8), 2, 10);
}

public class MagneticPull(int range, int cd)
    : CooldownAction("Magnetic Pull", TargetingType.Unit, _ => cd, tags: AbilityTags.Harmful, maxRange: range)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        var item = FindTarget(target.Unit!);
        if (item == null) return "no metal";
        return new(true, Plan: item);
    }

    static Item? FindTarget(IUnit target)
    {
        var weapon = target.GetWieldedItem();
        if (weapon.Def is WeaponDef w && w.Category == WeaponCategory.Item && Golems.IsFerromagnetic(weapon)) return weapon;
        foreach (var item in target.Inventory)
        {
            bool equipped = target.Equipped.ContainsValue(item);
            if (!equipped && Golems.IsFerromagnetic(item)) return item;
        }
        return null;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit is not { } victim) return;
        if (plan is not Item item) return;

        int dc = unit.GetSpellDC();
        using var ctx = PHContext.Create(unit, Target.From(victim));
        if (CheckFort(ctx, dc, "magnetic pull")) return;

        bool wasEquipped = victim.Equipped.ContainsValue(item);
        if (wasEquipped)
        {
            if (!g.DoUnequip(victim, item, free: true, consensual: false))
            {
                g.YouObserve(victim, $"{unit:The} pulls at {victim:own} {item} but it's stuck!");
                return;
            }
        }

        victim.Inventory.Remove(item);
        unit.Inventory.Add(item);
        g.YouObserve(victim, $"{unit:The} wrenches {victim:own} {item} away with magnetic force!");
    }

    public static readonly MagneticPull Instance = new(4, 8);
}

public class MagneticGrabOnHit : LogicBrick
{
    public static readonly MagneticGrabOnHit Instance = new();
    public override string Id => "golem:magnetic_grab";
    public override string? PokedexDescription => "Grabs targets wearing metal armor on hit";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Melee || !ctx.Check!.Result) return;
        var attacker = ctx.Source!;
        var target = ctx.Target.Unit;
        if (target == null) return;
        if (attacker.Grabbing != null || target.GrabbedBy != null) return;
        if (!target.Equipped.TryGetValue(ItemSlots.BodySlot, out var armor) || !Golems.IsFerromagnetic(armor)) return;

        attacker.Grabbing = target;
        target.GrabbedBy = attacker;
        g.YouObserve(attacker, $"{attacker:The} {VTense(attacker, "latch")} onto {target:own} {armor}!");
    }
}

public class PoisonCloudArea(IUnit? source, int dc, int duration) : Area(duration)
{
    public override string Name => "poison cloud";
    public override Glyph Glyph => new('≈', ConsoleColor.DarkGreen);

    protected override void OnEnter(IUnit unit) => Poison(unit);
    protected override void OnTick()
    {
        foreach (var unit in Occupants) Poison(unit);
    }

    void Poison(IUnit unit)
    {
        if (unit == source || unit.Has(CommonQueries.PoisonImmune)) return;
        using var ctx = PHContext.Create(source, Target.From(unit));
        if (CheckFort(ctx, dc, "poison cloud")) return;
        int dmg = Math.Max(1, unit.HP.Current / 10);
        ctx.Damage.Add(new DamageRoll { Formula = dmg, Type = DamageTypes.Poison });
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "choke")} on poisonous fumes!");
        DoDamage(ctx);
    }
}

public class PoisonCloudOnMeleeHit : LogicBrick
{
    public static readonly PoisonCloudOnMeleeHit Instance = new();
    public override string Id => "golem:poison_cloud_on_melee_hit";
    public override string? PokedexDescription => "Emits poison cloud when struck in melee";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (!ctx.Melee) return;
        if (fact.Entity is not IUnit unit) return;
        if (ctx.Source is not { } attacker) return;
        int dc = unit.GetSpellDC();
        var area = new PoisonCloudArea(unit, dc, 3) { TileSet = [attacker.Pos] };
        lvl.CreateArea(area);
    }
}

public class BoneCharge(int range, int cd)
    : CooldownAction("Bone Charge", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        IUnit? finalTarget = target.Unit;
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "charge")} forward!", "thundering hooves");

        Pos[] perps = [new(-dir.Y, dir.X), new(dir.Y, -dir.X)];

        for (int i = 0; i < MaxRange; i++)
        {
            Pos next = unit.Pos + dir;
            if (!lvl.InBounds(next) || !lvl[next].IsPassable) break;

            if (lvl.UnitAt(next) is { } victim)
            {
                if (victim == finalTarget)
                {
                    g.YouObserve(unit, $"{unit:The} {VTense(unit, "slam")} into {victim:the}!");
                    {
                        using var ctx = PHContext.Create(unit, Target.From(victim));
                        ctx.Damage.Add(new DamageRoll { Formula = d(2, 8), Type = DamageTypes.Blunt });
                        DoDamage(ctx);
                    }

                    if (!victim.IsDead)
                    {
                        DoWeaponAttack(unit, victim, unit.GetWieldedItem(), attackBonus: 2);
                    }
                    break;
                }

                // Intermediate: push aside + trample
                bool pushed = false;
                if (victim.IsPlayer || (victim is Monster mv && mv.Def.Size <= UnitSize.Large))
                {
                    foreach (var p in perps)
                    {
                        Pos side = next + p;
                        if (lvl.InBounds(side) && lvl[side].IsPassable && lvl.NoUnit(side))
                        {
                            lvl.MoveUnit(victim, side, true);
                            pushed = true;
                            break;
                        }
                    }
                }

                if (!pushed) break;

                using var tctx = PHContext.Create(unit, Target.From(victim));
                tctx.Damage.Add(new DamageRoll { Formula = d(12), Type = DamageTypes.Blunt });
                g.YouObserve(victim, $"{unit:The} {VTense(unit, "trample")} {victim:the}!");
                DoDamage(tctx);
            }

            lvl.MoveUnit(unit, next, true);
        }

        unit.AddFact(ChargeLowAc.Instance, null, 2);
        unit.Energy -= ActionCosts.OneAction.Value;
    }

    public static readonly BoneCharge Instance = new(5, 14);
}

public class PoisonBreath(int range, int cd)
    : CooldownAction("Poison Breath", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();
        PoisonCloudArea area = new(unit, dc, 3);

        BreathAttack.CollectBreath(BreathShape.Line, unit, dir, MaxRange, ConsoleColor.DarkGreen, "poison gas", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckFort(ctx, dc, "poison breath");
            int dmg = Math.Max(1, victim.HP.Current / 5);
            if (ctx.Check!.Result) dmg /= 2;
            ctx.Damage.Add(new DamageRoll { Formula = dmg, Type = DamageTypes.Poison });
            DoDamage(ctx);
        }, onTile: p => { if (lvl[p].IsPassable) area.TileSet.Add(p); });

        if (area.TileSet.Count > 0)
            lvl.CreateArea(area);
    }

    public static readonly PoisonBreath Instance = new(6, 6);
}

public class EyeBeam(int range, int cd)
    : CooldownAction("Eye Beam", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Line, unit, dir, MaxRange, ConsoleColor.Red, "searing light", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckFort(ctx, dc, "eye beam");
            ctx.Damage.Add(new DamageRoll { Formula = d(4, 6), Type = DamageTypes.Fire, HalfOnSave = true });
            DoDamage(ctx);
            if (!ctx.Check!.Result)
                victim.AddFact(BlindBuff.Instance.Timed(), unit, (d(2, 4) + 4).Roll());
        });
    }

    public static readonly EyeBeam Instance = new(5, 12);
}

public class SlowBuff(double severity) : LogicBrick
{
    public double Severity => severity;
    public override string Id => $"slow+{severity}";
    public override bool IsBuff => true;
    public override string? BuffName => "Slowed";
    public override StackMode StackMode => StackMode.ExtendDuration;
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Moderate;

    public override LogicBrick? MergeWith(LogicBrick other) =>
        other is SlowBuff s ? (s.Severity > Severity ? s : this) : null;

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == CommonQueries.SpeedPenaltyMul ? severity : null;

    public static readonly SlowBuff Half = new(0.5);
    public static readonly SlowBuff TwoThirds = new(0.34);
    public static readonly SlowBuff Quarter = new(0.25);
}

public class SlowOnHit : LogicBrick
{
    public static readonly SlowOnHit Instance = new();
    public override string Id => "golem:slow_on_hit";
    public override string? PokedexDescription => "Slows on hit (reflex)";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result || !ctx.Melee) return;
        if (ctx.Target?.Unit is not { } target) return;
        int dc = ((IUnit)fact.Entity).GetSpellDC();
        using var fctx = PHContext.Create((IUnit)fact.Entity, Target.From(target));
        if (CheckReflex(fctx, dc, "slow")) return;
        target.AddFact(SlowBuff.Half, ctx.Source, 3);
    }
}

public class MudArea(IUnit? source, int duration) : Area(duration)
{
    public override string Name => "mud";
    public override Glyph Glyph => new('~', ConsoleColor.DarkYellow);
    public override bool IsDifficultTerrain => true;

    protected override void OnEnter(IUnit unit)
    {
        if (unit == source || unit.Has(CommonQueries.DifficultTerrainImmune)) return;
        unit.AddFact(SlowBuff.Half, null, 1);
        unit.Energy -= 3;
    }

    protected override void OnTick()
    {
        foreach (var unit in Occupants)
        {
            if (unit == source || unit.Has(CommonQueries.DifficultTerrainImmune)) continue;
            unit.AddFact(SlowBuff.Half, null, 1);
            unit.Energy -= 3;
        }
    }
}

public class TransmuteRockToMud(int range, int cd)
    : CooldownAction("Transmute Rock to Mud", TargetingType.Unit, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        var tgt = target.Unit!;
        bool hasWall = false;
        foreach (var n in tgt.Pos.Neighbours())
        {
            if (lvl.InBounds(n) && lvl[n].Type == TileType.Wall)
            { hasWall = true; break; }
        }
        return hasWall ? true : "no adjacent wall";
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit is not { } tgt) return;
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "transmute")} rock to mud!", "a wet grinding sound");

        foreach (var n in tgt.Pos.Neighbours())
        {
            if (lvl.InBounds(n) && lvl[n].Type == TileType.Wall)
                lvl.Set(n, TileType.Corridor);
        }

        using var tiles = lvl.CollectCircle(tgt.Pos, 1, andCenter: true);
        List<Pos> mudTiles = [];
        foreach (var p in tiles)
        {
            if (lvl[p].IsPassable) mudTiles.Add(p);
        }

        if (mudTiles.Count > 0)
        {
            var area = new MudArea(unit, 4) { TileSet = [.. mudTiles] };
            lvl.CreateArea(area);
        }
    }

    public static readonly TransmuteRockToMud Instance = new(8, 8);
}

public class BleedRetaliation(int stacks) : LogicBrick
{
    public override string Id => $"golem:bleed_retal+{stacks}";
    public override string? PokedexDescription => $"Inflicts {stacks} bleed when struck in melee";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not IUnit owner) return;
        if (ctx.Source is not IUnit attacker || attacker.IsDM || !ctx.Melee) return;
        if (attacker.Pos.ChebyshevDist(owner.Pos) > 1) return;
        if (!BleedBuff.TryApplyBleed(owner, attacker, stacks)) return;
        g.YouObserveSelf(attacker, "You cut yourself on jagged obsidian!", null);
    }

    public static readonly BleedRetaliation S2 = new(2);
}

public class ObsidianSpray(int range, int cd)
    : CooldownAction("Obsidian Spray", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Cone, unit, dir, MaxRange, ConsoleColor.DarkRed, "obsidian shards", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "obsidian spray");
            ctx.Damage.Add(new DamageRoll { Formula = d(3, 8), Type = DamageTypes.Piercing, HalfOnSave = true });
            DoDamage(ctx);
            if (!ctx.Check!.Result)
                BleedBuff.TryApplyBleed(unit, victim, 2);
        });
    }

    public static readonly ObsidianSpray Instance = new(3, 8);
}

public class ObsidianDeathExplosion : LogicBrick
{
    public static readonly ObsidianDeathExplosion Instance = new();
    public override string Id => "golem:obsidian_death";
    public override string? PokedexDescription => "Explodes into obsidian shards on death";

    protected override void OnDeath(Fact fact, PHContext context)
    {
        if (fact.Entity is not IUnit unit) return;
        g.YouObserve(unit, $"{unit:The} shatters into razor-sharp shards!", "an explosion of glass");
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Burst, unit, Pos.Zero, 1, ConsoleColor.DarkRed, "obsidian shards", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "obsidian shards");
            ctx.Damage.Add(new DamageRoll { Formula = d(3, 8), Type = DamageTypes.Piercing, HalfOnSave = true });
            DoDamage(ctx);
            BleedBuff.TryApplyBleed(unit, victim, 4);
        });
    }
}

public class ShockBeam(int range, int cd)
    : CooldownAction("Shock Beam", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Line, unit, dir, MaxRange, ConsoleColor.Yellow, "electricity", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "shock");
            ctx.Damage.Add(new DamageRoll { Formula = d(4, 8), Type = DamageTypes.Shock, HalfOnSave = true });
            DoDamage(ctx);
        });
    }

    public static readonly ShockBeam Instance = new(5, 16);
}

public class ShockRetaliation(Dice damage) : LogicBrick
{
    public override string Id => "golem:shock_retaliation";
    public override string? PokedexDescription => $"Retaliates with {damage} shock when damaged";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not IUnit unit) return;
        if (ctx.Source is not { } attacker || attacker == unit) return;
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "discharge")} electricity at {attacker:the}!", "a sharp crackle");
        using var retalCtx = PHContext.Create(unit, Target.From(attacker));
        retalCtx.Damage.Add(new DamageRoll { Formula = damage, Type = DamageTypes.Shock });
        DoDamage(retalCtx);
    }

    public static readonly ShockRetaliation Instance = new(d(2, 8));
}

public class MindThrust(int range, int cd)
    : CooldownAction("Mind Thrust", TargetingType.Unit, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit is not { } victim) return;
        int dc = unit.GetSpellDC();
        bool explode = g.Rn2(10) == 0;
        Dice damage = explode ? d(6, 6) : d(3, 6);

        if (explode)
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "unleash")} a psychic blast at {victim:the}!", "a skull-splitting shriek");
        else
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "thrust")} psychic energy at {victim:the}!", "a piercing hum");

        using var ctx = PHContext.Create(unit, Target.From(victim));
        CheckWill(ctx, dc, "mind thrust");
        ctx.Damage.Add(new DamageRoll { Formula = damage, Type = DamageTypes.Psychic, HalfOnSave = true });
        DoDamage(ctx);
    }

    public static readonly MindThrust Instance = new(6, 2);
}

public class Petrification() : AfflictionBrick(dc: 20, tag: "petrification")
{
    public static readonly Petrification Instance = new();
    public override string Id => "petrification";

    public override string AfflictionName => "Petrification";
    public override int MaxStage => 5;
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Lethal;
    public override DiceFormula TickInterval => d(3);
    public override string? ImmunityKey => CommonQueries.PetrificationImmune;

    public static bool TryApply(IUnit source, IUnit target)
    {
        if (target.Has(CommonQueries.PetrificationImmune)) return false;
        target.AddFact(Instance, source);
        return true;
    }

    protected override void DoPeriodicEffect(Fact fact, IUnit unit, int stage)
    {
        if (unit.Has(CommonQueries.PetrificationImmune))
        {
            fact.Remove();
            return;
        }
        if (stage >= MaxStage)
        {
            g.YouObserveSelf(unit, "You turn to stone!", $"{unit:The} turns to stone!");
            g.Done($"Petrified to death");
            return;
        }
        if (unit.IsPlayer) g.pline(stage switch
        {
            1 => "Your skin feels stiff.",
            2 => "Your joints are hardening.",
            3 => "Your limbs are turning grey.",
            4 => "You can barely move!",
            _ => null
        } ?? "");
    }

    protected override object? DoQuery(int stage, string key, string? arg) => null;
}

public class FossilSummon(int cd)
    : CooldownAction("Raise Fossil", TargetingType.Unit, _ => cd, maxRange: 5, tags: AbilityTags.Harmful)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;

        // 30% fizzle per existing skeleton cat within 5 tiles
        foreach (var m in lvl.LiveUnits.OfType<Monster>())
        {
            if (m.Def == Bestiary.Cats.Leopard && m.IsCreature(CreatureTypes.Undead) && unit.Pos.ChebyshevDist(m.Pos) <= 5)
                if (g.Rn2(100) < 30) return "fizzle";
        }

        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit is not { } tgt) return;
        Pos tgtPos = tgt.Pos;

        g.YouObserve(unit, $"{unit:The} {VTense(unit, "raise")} a skeletal leopard from the ground!", "bones rattling together");
        g.Defer(() =>
        {
            if (!lvl.FirstFreeAdjacent(tgtPos, out var spot)) return;
            var mon = Monster.Spawn(Bestiary.Cats.Leopard, "fossil_summon", SkeletonTemplate.Instance);
            lvl.PlaceUnit(mon, spot);
        });
    }

    public static readonly FossilSummon Instance = new(5);
}

public class DeathExplosion(DamageType type, Dice damage, int radius, ConsoleColor color) : LogicBrick
{
    public override string Id => $"golem:death_explosion+{damage.Serialize()}/{type.SubCat}";
    public override string? PokedexDescription => $"Explodes on death ({type.SubCat})";

    static string ExplosionName(DamageType dt) => dt.SubCat switch
    {
        "cold" => "frost",
        "fire" => "flames",
        "shock" => "lightning",
        "acid" => "acid",
        _ => dt.SubCat,
    };

    protected override void OnDeath(Fact fact, PHContext context)
    {
        if (fact.Entity is not IUnit unit) return;
        string name = ExplosionName(type);
        g.YouObserve(unit, $"{unit:The} explodes in a burst of {name}!", $"an explosion of {name}");
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Burst, unit, Pos.Zero, radius, color, name, victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, type.SubCat);
            ctx.Damage.Add(new DamageRoll { Formula = damage, Type = type, HalfOnSave = true });
            DoDamage(ctx);
        }, AreaSystem.OnTile(type));
    }

    public static readonly DeathExplosion Cold_3d6_R1 = new(DamageTypes.Cold, d(3, 6), 1, ConsoleColor.Cyan);
    public static readonly DeathExplosion Blunt_8d8_R1 = new(DamageTypes.Blunt, d(8, 8), 1, ConsoleColor.Gray);
    public static readonly DeathExplosion Fire_6d8_R1 = new(DamageTypes.Fire, d(6, 8), 1, ConsoleColor.Red);
    public static readonly DeathExplosion Fire_10d8_R1 = new(DamageTypes.Fire, d(10, 8), 1, ConsoleColor.Red);
}

public class JunkSwarm(Pos where) : Swarm("Junk Swarm", new('µ', ConsoleColor.DarkYellow), 1, where)
{
    public class JunkSwarmOnDeath : LogicBrick
    {
        public static readonly JunkSwarmOnDeath Instance = new();
        public override string Id => "golem:junk_swarm_on_death";
        protected override void OnDeath(Fact fact, PHContext context)
        {
            if (fact.Entity is not IUnit unit) return;
            g.YouObserve(unit, $"{unit:The}'s body collapses into a swarm of junk!");
            lvl.CreateSwarm(new JunkSwarm(unit.Pos));
        }
    }

    protected override void SwarmUnit(IUnit unit)
    {
        using var ctx = PHContext.Create(DungeonMaster.Mook, Target.From(unit));
        CheckReflex(ctx, 12, "junk swarm");
        g.YouObserveSelf(unit, $"A junk swarm pricks at you!", $"a swarm of rusted metal rises up over {unit:the}", "rusty squeaks, a muffled scream");
        ctx.Damage.Add(new()
        {
            Formula = d(4) + 2,
            Type = DamageTypes.Slashing,
            HalfOnSave = true,
        });
        DoDamage(ctx);
    }
}

public class InubrixPhasing : LogicBrick
{
    public static readonly InubrixPhasing Instance = new();
    public override string Id => "golem:inubrix_phasing";
    public override string? PokedexDescription => "Advantage vs targets wearing metal armor";

    protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
        if (ctx.Target?.Unit is not { } target) return;
        if (target.Equipped.TryGetValue(ItemSlots.BodySlot, out var armor) && Golems.IsFerromagnetic(armor))
            ctx.Check!.Advantage++;
    }
}

public class InubrixPhaseStrip : LogicBrick
{
    public static readonly InubrixPhaseStrip Instance = new();
    public override string Id => "golem:inubrix_strip";
    public override string? PokedexDescription => "10% chance per equipped item to unequip on hit";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result || !ctx.Melee) return;
        if (ctx.Target?.Unit is not { } victim) return;
        var attacker = (IUnit)fact.Entity;
        foreach (var (slot, item) in victim.Equipped.ToArray())
        {
            if (g.Rn2(10) != 0) continue;
            if (!g.DoUnequip(victim, item, free: true, consensual: false)) continue;
            g.YouObserve(victim, $"{attacker:The} {VTense(attacker, "disrupt")} {victim:possessive} {item}!");
        }
    }
}

public class NegativeBreath(int range, int cd)
    : CooldownAction("Negative Energy Breath", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Line, unit, dir, MaxRange, ConsoleColor.DarkMagenta, "negative energy", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckWill(ctx, dc, "negative energy");
            ctx.Damage.Add(new DamageRoll { Formula = d(4, 8), Type = DamageTypes.Negative, HalfOnSave = true });
            DoDamage(ctx);
        });
    }

    public static readonly NegativeBreath Instance = new(6, 8);
}

public class DeathFumes : LogicBrick
{
    public static readonly DeathFumes Instance = new();
    public override string Id => "golem:death_fumes";
    public override string? PokedexDescription => "Emits poison cloud on death";

    protected override void OnDeath(Fact fact, PHContext context)
    {
        if (fact.Entity is not IUnit unit) return;
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "dissolve")} into a toxic cloud!", "a hiss of escaping gas");
        int dc = unit.GetSpellDC();
        using var tiles = lvl.CollectCircle(unit.Pos, 2, andCenter: true);
        PoisonCloudArea area = new(unit, dc, 4);
        foreach (var p in tiles)
            if (lvl[p].IsPassable) area.TileSet.Add(p);
        if (area.TileSet.Count > 0)
            lvl.CreateArea(area);
    }
}

public class HealsAndHastesFromElement(DamageType type) : LogicBrick
{
    public override string Id => $"heals_hastes_from+{type.SubCat}";
    public override string? PokedexDescription => $"Heals from {type.SubCat} and gains haste";

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
        bool any = false;
        foreach (var roll in ctx.Damage)
            if (roll.Type == type) { roll.IsAttuned = true; any = true; }
        if (any && fact.Entity is IUnit unit)
            unit.AddFact(GolemHasteBuff.Instance, null, 3);
    }

    public static readonly HealsAndHastesFromElement Fire = new(DamageTypes.Fire);
}

public class PrismaticSpray(int range, int cd)
    : CooldownAction("Prismatic Spray", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    static readonly string[] RayNames = ["fire", "acid", "lightning", "poison", "stone", "prismatic light", "prismatic light"];
    static readonly ConsoleColor[] RayColors = [ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.DarkGreen, ConsoleColor.Blue, ConsoleColor.DarkMagenta, ConsoleColor.Magenta];

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();

        int ray1 = g.Rn2(8);
        int ray2 = -1;
        if (ray1 == 7)
        {
            ray1 = g.Rn2(7);
            ray2 = g.Rn2(7);
        }

        BreathAttack.CollectBreath(BreathShape.Cone, unit, dir, MaxRange, RayColors[ray1], RayNames[ray1], victim =>
        {
            ApplyRay(unit, victim, dc, ray1);
        }, verb: "emit");

        if (ray2 >= 0)
        {
            BreathAttack.CollectBreath(BreathShape.Cone, unit, dir, MaxRange, RayColors[ray2], RayNames[ray2], victim =>
            {
                ApplyRay(unit, victim, dc, ray2);
            }, verb: "emit");
        }
    }

    static void ApplyRay(IUnit source, IUnit victim, int dc, int ray)
    {
        using var ctx = PHContext.Create(source, Target.From(victim));
        switch (ray)
        {
            case 0: // Red — fire
                CheckReflex(ctx, dc, "red ray");
                ctx.Damage.Add(new DamageRoll { Formula = d(6, 6), Type = DamageTypes.Fire, HalfOnSave = true });
                DoDamage(ctx);
                break;
            case 1: // Orange — acid
                CheckReflex(ctx, dc, "orange ray");
                ctx.Damage.Add(new DamageRoll { Formula = d(6, 6), Type = DamageTypes.Acid, HalfOnSave = true });
                DoDamage(ctx);
                break;
            case 2: // Yellow — shock
                CheckReflex(ctx, dc, "yellow ray");
                ctx.Damage.Add(new DamageRoll { Formula = d(6, 6), Type = DamageTypes.Shock, HalfOnSave = true });
                DoDamage(ctx);
                break;
            case 3: // Green — poison (30% current HP)
                CheckFort(ctx, dc, "green ray");
                int dmg = Math.Max(1, victim.HP.Current * 3 / 10);
                if (ctx.Check!.Result) dmg /= 2;
                ctx.Damage.Add(new DamageRoll { Formula = dmg, Type = DamageTypes.Poison });
                DoDamage(ctx);
                break;
            case 4: // Blue — petrification
                if (!CheckFort(ctx, dc, "blue ray"))
                    Petrification.TryApply(source, victim);
                break;
            case 5: // Indigo — confusion
                if (!CheckWill(ctx, dc, "indigo ray"))
                    victim.AddFact(ConfusedBuff.Instance, source, 3);
                break;
            case 6: // Violet — blind + daze
                if (!CheckWill(ctx, dc, "violet ray"))
                {
                    victim.AddFact(BlindBuff.Instance.Timed(), source, 4);
                    if (!victim.Has(CommonQueries.DazeImmune))
                        victim.AddFact(DazedBuff.Instance, source, 1);
                }
                break;
        }
    }

    public static readonly PrismaticSpray Instance = new(3, 10);
}

public class EarthquakeStomp(int radius, int cd)
    : CooldownAction("Earthquake Stomp", TargetingType.None, _ => cd, tags: AbilityTags.Harmful)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        if (target.Unit == null) return "no target";
        if (unit.Pos.ChebyshevDist(target.Unit.Pos) > radius) return "too far";
        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "slam")} the ground!", "a thunderous crash");
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Burst, unit, Pos.Zero, radius, ConsoleColor.DarkYellow, "rubble", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "earthquake");
            ctx.Damage.Add(new DamageRoll { Formula = d(6, 8), Type = DamageTypes.Blunt, HalfOnSave = true });
            DoDamage(ctx);
        }, AreaSystem.OnTile(DamageTypes.Blunt));
    }

    public static readonly EarthquakeStomp Instance = new(2, 8);
}

public class Trample() : ActionBrick("Trample", TargetingType.Unit)
{
    public override ActionCost GetCost(IUnit unit, object? data, Target target) => unit.LandMove;

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        if (target.Unit == null) return "no target";
        Pos dir = (target.Unit.Pos - unit.Pos).Signed;
        Pos next = unit.Pos + dir;
        if (!lvl.InBounds(next)) return "blocked";
        var blocker = lvl.UnitAt(next);
        if (blocker == null) return "no blocker";
        return new(true, Plan: (dir, next, blocker));
    }

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        if (plan is not (Pos dir, Pos next, IUnit blocker)) return;

        Pos[] perps = [new(-dir.Y, dir.X), new(dir.Y, -dir.X)];
        bool pushed = false;
        foreach (var p in perps)
        {
            Pos side = next + p;
            if (lvl.InBounds(side) && lvl[side].IsPassable && lvl.NoUnit(side))
            {
                lvl.MoveUnit(blocker, side, true);
                pushed = true;
                break;
            }
        }

        if (!pushed) return;

        using var ctx = PHContext.Create(unit, Target.From(blocker));
        ctx.Damage.Add(new DamageRoll { Formula = d(4, 8), Type = DamageTypes.Blunt });
        g.YouObserve(blocker, $"{unit:The} {VTense(unit, "trample")} {blocker:the}!");
        DoDamage(ctx);

        lvl.MoveUnit(unit, next, true);
    }

    public static readonly Trample Instance = new();
}

public class ViridiumBlight() : AfflictionBrick(18, "disease")
{
    public static readonly ViridiumBlight Instance = new();
    public override string Id => "viridium_blight";

    public override string AfflictionName => "Viridium Blight";
    public override int MaxStage => 6;
    public override DiceFormula TickInterval => d(20, 15);

    protected override void DoPeriodicEffect(Fact fact, IUnit unit, int stage)
    {
        if (unit.IsPlayer) g.pline(stage switch
        {
            1 => "Your skin feels numb.",
            2 => "Pale patches appear on your skin.",
            3 => "Your extremities tingle painfully.",
            4 => "Your flesh is rotting.",
            5 => "Chunks of flesh slough off.",
            6 => "You are falling apart.",
            _ => null
        } ?? "");
    }

    protected override object? DoQuery(int stage, string key, string? arg) => key switch
    {
        "stat/Str" => new Modifier(ModifierCategory.StatusPenalty, -stage, "viridium blight"),
        "stat/Dex" => new Modifier(ModifierCategory.StatusPenalty, -stage, "viridium blight"),
        "stat/Con" => new Modifier(ModifierCategory.StatusPenalty, -stage, "viridium blight"),
        "stat/Int" => new Modifier(ModifierCategory.StatusPenalty, -stage, "viridium blight"),
        "stat/Wis" => new Modifier(ModifierCategory.StatusPenalty, -stage, "viridium blight"),
        "stat/Cha" => new Modifier(ModifierCategory.StatusPenalty, -stage, "viridium blight"),
        _ => null
    };
}

public class RandomAfflictionOnHit : LogicBrick
{
    public static readonly RandomAfflictionOnHit Instance = new();
    public override string Id => "golem:random_affliction";
    public override string? PokedexDescription => "Inflicts a random affliction on hit";

    static readonly AfflictionBrick[] Afflictions =
    [
        SnakeVenomGreater.DC14,
        SpiderVenom.DC14,
        FoodPoisoning.Instance,
        ViridiumBlight.Instance,
    ];

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result || !ctx.Melee) return;
        if (ctx.Target?.Unit is not { } victim) return;
        var affliction = Afflictions.Pick();
        victim.AddFact(affliction, ctx.Source);
    }
}

public class PetrificationExplosion : LogicBrick
{
    public static readonly PetrificationExplosion Instance = new();
    public override string Id => "golem:petrification_explosion";
    public override string? PokedexDescription => "Petrification burst on death";

    protected override void OnDeath(Fact fact, PHContext context)
    {
        if (fact.Entity is not IUnit unit) return;
        g.YouObserve(unit, $"{unit:The} shatters into toxic dust!", "a cloud of green dust");
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Burst, unit, Pos.Zero, 2, ConsoleColor.DarkGreen, "toxic dust", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            if (!CheckFort(ctx, dc, "petrification"))
                Petrification.TryApply(unit, victim);
        });
    }
}

public class SilenceAura(int radius) : LogicBrick
{
    public static readonly SilenceAura Instance = new(2);
    public override string Id => "golem:silence_aura";
    public override bool IsActive => true;
    public override string? PokedexDescription => $"Silence aura (radius {radius})";

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        if (unit.CannotAct) return;
        using var tiles = lvl.CollectCircle(unit.Pos, radius, andCenter: false);
        foreach (var p in tiles)
        {
            var victim = lvl.UnitAt(p);
            if (victim != null && victim != unit)
                victim.AddFact(SilencedBuff.Instance.Timed(), unit, 1);
        }
    }
}

public class SpellSunder : LogicBrick
{
    public static readonly SpellSunder Instance = new();
    public override string Id => "golem:spell_sunder";
    public override string? PokedexDescription => "25% chance on hit to drain a spell slot";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result || !ctx.Melee) return;
        if (ctx.Target?.Unit is not { } victim) return;
        if (g.Rn2(4) != 0) return;

        var attacker = (IUnit)fact.Entity;
        List<(int level, ChargePool pool)> available = [];
        for (int i = 1; i <= 10; i++)
        {
            var pool = victim.GetPool($"spell_l{i}");
            if (pool != null && pool.Current > 0)
                available.Add((i, pool));
        }
        if (available.Count == 0) return;

        var (slotLevel, picked) = available.Pick();
        picked.Current--;
        g.YouObserve(victim, $"{attacker:The} {VTense(attacker, "sunder")} {victim:possessive} magic!");

        using var dmgCtx = PHContext.Create(attacker, Target.From(victim));
        dmgCtx.Damage.Add(new DamageRoll { Formula = d(slotLevel * 2, 6), Type = DamageTypes.Force });
        DoDamage(dmgCtx);
    }
}

public class GolemDragonBreath(int range, int cd)
    : CooldownAction("Dragon Breath", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    static readonly (DamageType type, ConsoleColor color)[] Elements =
    [
        (DamageTypes.Fire, ConsoleColor.Red),
        (DamageTypes.Cold, ConsoleColor.Cyan),
        (DamageTypes.Shock, ConsoleColor.Yellow),
        (DamageTypes.Acid, ConsoleColor.Green),
    ];

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();
        var (type, color) = Elements.Pick();

        BreathAttack.CollectBreath(BreathShape.Line, unit, dir, MaxRange, color, type.SubCat, victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, type.SubCat);
            ctx.Damage.Add(new DamageRoll { Formula = d(8, 8), Type = type, HalfOnSave = true });
            DoDamage(ctx);
        });
    }

    public static readonly GolemDragonBreath Instance = new(6, 12);
}

public class BerserkBuff : LogicBrick
{
    public static readonly BerserkBuff Instance = new();
    public override string Id => "golem:berserk";
    public override bool IsBuff => true;
    public override string? BuffName => "Berserk";
    public override StackMode StackMode => StackMode.Reject;

    protected override void OnBeforeDamageRoll(Fact fact, PHContext ctx)
    {
        if (ctx.AttackType != AttackType.Melee) return;
        ctx.Damage.Add(new DamageRoll { Formula = 8, Type = DamageTypes.Blunt });
    }
}

public class BerserkTrigger : LogicBrick
{
    public static readonly BerserkTrigger Instance = new();
    public override string Id => "golem:berserk_trigger";
    public override string? PokedexDescription => "Goes berserk when damaged";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not IUnit unit) return;
        if (unit.HasFact(BerserkBuff.Instance)) return;
        int missing = unit.HP.Max - unit.HP.Current;
        if (g.Rn2(Math.Max(1, missing)) > unit.HP.Max / 30)
        {
            unit.AddFact(BerserkBuff.Instance, null);
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "go")} berserk!", "a furious roar");
        }
    }
}

public class MithralReachAttack()
    : CooldownAction("Lunge", TargetingType.Unit, _ => 0, maxRange: 3, tags: AbilityTags.Harmful)
{
    static readonly WeaponDef MithralBlade = new()
    {
        id = "mithral_blade",
        Name = "mithral blade",
        BaseDamage = d(2, 8),
        Style = WeaponStyle.Exotic,
        Grip = WeaponGrip.Exotic,
        DamageType = DamageTypes.Slashing,
        Glyph = new('/', ConsoleColor.White),
        Weight = -1,
        Price = -1,
    };

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit is not { } victim) return;
        var item = Item.Create(MithralBlade);
        DoWeaponAttack(unit, victim, item);
    }

    public static readonly MithralReachAttack Instance = new();
}

public class MithralSpringBack(double threshold) : LogicBrick
{
    public static readonly MithralSpringBack Instance = new(0.2);
    public override string Id => "golem:mithral_spring";
    public override string? PokedexDescription => "Leaps back when taking a heavy hit";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not IUnit unit) return;
        if (ctx.Source is not { } attacker) return;
        if (ctx.TotalDamageDealt < unit.HP.Max * threshold) return;

        Pos dir = (unit.Pos - attacker.Pos).Signed;
        Pos two = unit.Pos + dir + dir;
        Pos one = unit.Pos + dir;

        if (lvl.InBounds(two) && lvl[two].IsPassable && lvl.NoUnit(two))
        {
            lvl.MoveUnit(unit, two, true);
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "spring")} back!");
        }
        else if (lvl.InBounds(one) && lvl[one].IsPassable && lvl.NoUnit(one))
        {
            lvl.MoveUnit(unit, one, true);
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "spring")} back!");
        }
    }
}

public class CannonKnockback : LogicBrick
{
    public static readonly CannonKnockback Instance = new();
    public override string Id => "golem:cannon_knockback";

    protected override void OnDamageDone(Fact fact, PHContext ctx)
    {
        if (ctx.Source is not { } attacker || ctx.Target?.Unit is not { } victim) return;
        if (victim.IsDead) return;
        int dc = attacker.GetSpellDC();
        using var fctx = PHContext.Create(attacker, Target.From(victim));
        if (CheckFort(fctx, dc, "knockback")) return;

        Pos dir = (victim.Pos - attacker.Pos).Signed;
        Pos dest = victim.Pos + dir;
        if (lvl.InBounds(dest) && lvl[dest].IsPassable && lvl.NoUnit(dest))
        {
            lvl.MoveUnit(victim, dest, true);
            g.YouObserve(victim, $"{victim:The} {VTense(victim, "stagger")} back!");
        }
        else
        {
            using var dmgCtx = PHContext.Create(attacker, Target.From(victim));
            dmgCtx.Damage.Add(new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Blunt });
            g.YouObserve(victim, $"{victim:The} {VTense(victim, "slam")} into an obstacle!");
            DoDamage(dmgCtx);
        }
    }
}

public class CannonShot(int range, int cd)
    : CooldownAction("Cannon Shot", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    static readonly WeaponDef Cannonball = new()
    {
        id = "cannonball",
        Name = "cannonball",
        BaseDamage = d(6, 6),
        Style = WeaponStyle.Exotic,
        Grip = WeaponGrip.Exotic,
        DamageType = DamageTypes.Blunt,
        Glyph = new('●', ConsoleColor.Gray),
        Launcher = "cannon",
        Weight = -1,
        Price = -1,
        Components = [CannonKnockback.Instance],
    };

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        var item = Item.Create(Cannonball);
        g.YouObserve(unit, $"{unit:The} fires a cannon!", "a thunderous boom");
        DoThrow(unit, item, dir, AttackType.Thrown);
    }

    public static readonly CannonShot Instance = new(8, 2);
}

public class CannonSplash(int range, int cd)
    : CooldownAction("Cannon Splash", TargetingType.Unit, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit is not { } tgt) return;
        g.YouObserve(unit, $"{unit:The} fires an explosive shell at {tgt:the}!", "a deafening blast");
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Burst, tgt, Pos.Zero, 1, ConsoleColor.DarkYellow, "shrapnel", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "cannon splash");
            ctx.Damage.Add(new DamageRoll { Formula = d(12, 6), Type = DamageTypes.Blunt, HalfOnSave = true });
            DoDamage(ctx);
        }, AreaSystem.OnTile(DamageTypes.Blunt));
    }

    public static readonly CannonSplash Instance = new(4, 12);
}

public class IncendiaryBreath(int range, int cd)
    : CooldownAction("Incendiary Breath", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();
        FirePatchArea area = new(unit, 4, d(3, 6));

        BreathAttack.CollectBreath(BreathShape.Cone, unit, dir, MaxRange, ConsoleColor.Red, "fire", victim =>
        {
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, "incendiary breath");
            ctx.Damage.Add(new DamageRoll { Formula = d(4, 6), Type = DamageTypes.Fire, HalfOnSave = true });
            DoDamage(ctx);
        }, onTile: p => { if (lvl[p].IsPassable) area.TileSet.Add(p); });

        if (area.TileSet.Count > 0)
            lvl.CreateArea(area);
    }

    public static readonly IncendiaryBreath Instance = new(3, 10);
}

public class ShadowAura : LogicBrick
{
    public static readonly ShadowAura Instance = new();
    public override string Id => "golem:shadow_aura";
    public override bool IsActive => true;
    public override string? PokedexDescription => "Permanently extinguishes light in radius 2";

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        if (unit.CannotAct) return;
        using var tiles = lvl.CollectCircle(unit.Pos, 2, andCenter: true);
        foreach (var p in tiles)
            lvl.BaseLit[p] = false;
    }
}

public class ShadowedDebuff : LogicBrick
{
    public static readonly ShadowedDebuff Instance = new();
    public override string Id => "shadowed";
    public override bool IsBuff => true;
    public override string? BuffName => "Shadowed";
    public override StackMode StackMode => StackMode.Reject;
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Severe;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "stat/Str" => new Modifier(ModifierCategory.StatusPenalty, -4, "shadowed"),
        "light_radius" => new Modifier(ModifierCategory.CircumstancePenalty, -2, "shadowed"),
        _ => null,
    };
}

public class ShadowBreath(int range, int cd)
    : CooldownAction("Shadow Breath", TargetingType.Direction, _ => cd, maxRange: range, tags: AbilityTags.Harmful)
{
    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = target.Pos!.Value;
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Line, unit, dir, MaxRange, ConsoleColor.DarkGray, "shadow", victim =>
        {
            bool drained = victim.HasFact(ShadowedDebuff.Instance);
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckWill(ctx, dc, "shadow breath");
            ctx.Damage.Add(new DamageRoll { Formula = drained ? d(6, 8) : d(2, 8), Type = DamageTypes.Anarchic, HalfOnSave = true });
            DoDamage(ctx);
            if (!ctx.Check!.Result)
                victim.AddFact(ShadowedDebuff.Instance, unit);
        });
    }

    public static readonly ShadowBreath Instance = new(6, 8);
}

public class BloodConstrict(Dice damage) : LogicBrick
{
    public override string Id => "golem:blood_constrict";
    public override bool IsActive => true;
    public override string? PokedexDescription => $"Constrict {damage}, heals from constrict damage";

    protected override void OnRoundStart(Fact fact)
    {
        var unit = fact.Entity as IUnit;
        if (unit?.Grabbing is not { } victim) return;

        using var ctx = PHContext.Create(unit, Target.From(victim));
        ctx.Damage.Add(new DamageRoll { Formula = damage, Type = DamageTypes.Blunt });
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "crush")} {victim:the}!");
        DoDamage(ctx);

        if (ctx.TotalDamageDealt > 0)
            g.DoHeal(unit, unit, ctx.TotalDamageDealt, magical: false);
    }

    public static readonly BloodConstrict Instance = new(d(2, 8));
}

public class BerserkAttack(int retargetPct) : ActionBrick("berserk_attack")
{
    public override string? PokedexDescription => $"{retargetPct}% chance to attack random adjacent";

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => unit.IsAdjacent(target);

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        IUnit victim = target.Unit!;
        if (g.Rn2(100) < retargetPct)
        {
            List<IUnit> adjacent = [];
            foreach (var pos in unit.Pos.Neighbours())
            {
                if (lvl.UnitAt(pos) is { } u && u != unit)
                    adjacent.Add(u);
            }
            if (adjacent.Count > 0)
            {
                victim = adjacent.Pick();
                g.YouObserve(unit, $"{unit:The} {VTense(unit, "lash")} out wildly!");
            }
        }
        DoWeaponAttack(unit, victim, unit.GetWieldedItem(), attackBonus: 2);
    }

    public static readonly BerserkAttack Chance30 = new(30);
}

public class SplinterBurst(Dice damage, int radius) : LogicBrick
{
    public override string Id => "golem:splinter_burst";
    public override string? PokedexDescription => $"50% chance on taking damage: {damage} slashing burst (radius {radius})";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not IUnit unit) return;
        if (g.Rn2(2) != 0) return;

        g.YouObserve(unit, $"{unit:The} {VTense(unit, "splinter")} violently!", "a crack of splintering wood");
        int dc = unit.GetSpellDC();

        BreathAttack.CollectBreath(BreathShape.Burst, unit, Pos.Zero, radius, ConsoleColor.DarkYellow, "splinters", victim =>
        {
            using var dmgCtx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(dmgCtx, dc, "slashing");
            dmgCtx.Damage.Add(new DamageRoll { Formula = damage, Type = DamageTypes.Slashing, HalfOnSave = true });
            DoDamage(dmgCtx);
        }, AreaSystem.OnTile(DamageTypes.Slashing));
    }

    public static readonly SplinterBurst Instance = new(d(2, 6), 1);
}

public class BonePrison(int cd, int range)
    : CooldownAction("Bone Prison", TargetingType.Unit, _ => cd, tags: AbilityTags.Harmful, maxRange: range)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        if (lvl.Traps.ContainsKey(target.Unit!.Pos)) return "already trapped";
        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit == null) return;
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "hurl")} a cage of bones at {target.Unit:the}!", "rattling bones");
        var trap = new WebTrap(lvl.Depth) { PlayerSeen = true };
        lvl.Traps[target.Unit.Pos] = trap;
        trap.Trigger(target.Unit, null);
    }

    public static readonly BonePrison Instance = new(8, 4);
}

public class SpawnOnDeath(string id, string ifSee, string? ifHear, Func<MonsterDef> what) : LogicBrick
{
    public override string Id => $"spawn_on_death+{id}";
    public override string? PokedexDescription => "Spawns a lesser form on death";

    protected override void OnDeath(Fact fact, PHContext context)
    {
        if (fact.Entity is not IUnit unit) return;
        Pos deathPos = unit.Pos;
        MonsterDef def = what();
        g.Defer(() =>
        {
            Pos pos = deathPos;
            if (!lvl[deathPos].IsPassable || !lvl.NoUnit(deathPos))
                pos = deathPos + Pos.AllDirs.Pick();

            if (!lvl[pos].IsPassable || !lvl.NoUnit(pos)) return;
            g.YouObserve(pos, ifSee, ifHear);
            var mon = Monster.Spawn(def, "spawn_on_death");
            lvl.PlaceUnit(mon, pos);
        });
    }
}

public class QuintalPair : LogicBrick<QuintalPair.State>
{
    public static readonly QuintalPair Instance = new();
    public override string Id => "golem:quintal_pair";
    const int DeathCooldown = 12;

    public class State
    {
        public IUnit? Partner;
        public bool IsPrimary;
        public int SpawnCount;
        public Pos GoalPos;
        public int SpawnCooldown;
    }

    protected override void OnDeath(Fact fact, PHContext context)
    {
        var state = fact.As<State>();
        if (state.Partner is { IsDead: false } partner)
        {
            var partnerFact = partner.FindFact(Instance);
            if (partnerFact != null)
                partnerFact.As<State>().SpawnCooldown = DeathCooldown;
        }
    }
}

public class QuantiumBrain : MonsterBrain
{
    static readonly Pos[] Axes = [Pos.N, Pos.NE, Pos.E, Pos.SE];

    public override bool DoTurn(Monster m)
    {
        var pair = m.FindFact(QuintalPair.Instance);
        if (pair == null) return false;
        var state = pair.As<QuintalPair.State>();
        Log.Write($"quantium: {m} at {m.Pos} primary={state.IsPrimary} partner={state.Partner} partnerDead={state.Partner?.IsDead} cd={state.SpawnCooldown}");

        if (state.SpawnCooldown > 0) state.SpawnCooldown--;

        // try to spawn partner
        if (state.Partner is not { IsDead: false } && state.SpawnCooldown <= 0)
        {
            Log.Write($"quantium: {m} trying to spawn partner");
            if (TrySpawn(m, state))
            {
                m.Energy -= ActionCosts.OneAction.Value;
                return true;
            }
        }

        // move toward sandwich position
        Pos goal;
        if (state.IsPrimary && state.Partner is { IsDead: false } partner)
        {
            goal = PickGoal(m.Pos, partner.Pos, upos, m);
            Log.Write($"quantium: {m} primary goal={goal} (me={m.Pos} partner={partner.Pos} player={upos})");
            var partnerPair = partner.FindFact(QuintalPair.Instance);
            if (partnerPair != null)
            {
                var partnerGoal = PickGoal(partner.Pos, m.Pos, upos, m);
                partnerPair.As<QuintalPair.State>().GoalPos = partnerGoal;
                Log.Write($"quantium: assigned secondary goal={partnerGoal}");
            }
        }
        else if (!state.IsPrimary && state.GoalPos != Pos.Zero)
        {
            goal = state.GoalPos;
            Log.Write($"quantium: {m} secondary goal={goal}");
        }
        else
        {
            goal = upos;
            Log.Write($"quantium: {m} fallback goal={goal}");
        }

        Pos? partnerPos = state.Partner is { IsDead: false } pp ? pp.Pos : null;
        bool moved = StepToward(m, goal, partnerPos);
        Log.Write($"quantium: {m} moved={moved} now at {m.Pos} dist_to_player={m.Pos.ChebyshevDist(upos)}");

        // melee if adjacent to player
        if (m.Pos.ChebyshevDist(upos) == 1)
        {
            Log.Write($"quantium: {m} melee attack");
            DoWeaponAttack(m, u, m.GetWieldedItem());
        }

        // beam fires on secondary's turn when both alive and close
        if (!state.IsPrimary && state.Partner is { IsDead: false } beamPartner
            && m.Pos.ChebyshevDist(beamPartner.Pos) <= 4)
        {
            Draw.DrawCurrent();
            FireBeam(m, beamPartner);
        }

        int cost = ActionCosts.OneAction.Value;
        if (!state.IsPrimary && partnerPos is { } p && m.Pos.ChebyshevDist(p) > 4)
            cost /= 2;
        m.Energy -= cost;
        return true;
    }

    static bool TrySpawn(Monster m, QuintalPair.State state)
    {
        if (!lvl.FirstFreeAdjacent(m.Pos, out var spot)) return false;

        state.IsPrimary = true;
        state.SpawnCount++;
        state.SpawnCooldown = 0;

        g.YouObserve(m, $"{m:The} {VTense(m, "call")} forth a twin!", "a deep rumbling");
        int spawnCount = state.SpawnCount;
        IUnit spawner = m;
        g.Defer(() =>
        {
            var mon = Monster.Spawn(Golems.Quantium, "quintal_pair");
            mon.Initiative = -1;
            lvl.PlaceUnit(mon, spot);

            int hpPct = Math.Max(10, 100 - 5 * spawnCount);
            mon.HP.Current = mon.HP.Max * hpPct / 100;

            var monPair = mon.FindFact(QuintalPair.Instance);
            if (monPair != null)
            {
                var monState = monPair.As<QuintalPair.State>();
                monState.Partner = spawner;
                monState.IsPrimary = false;
                monState.SpawnCount = spawnCount;
            }
            state.Partner = mon;
        });
        return true;
    }

    static Pos PickGoal(Pos me, Pos ally, Pos player, Monster m)
    {
        Pos bestGoal = player;
        int bestScore = int.MaxValue;

        foreach (var axis in Axes)
        {
            Pos a = player + axis;
            Pos b = player - axis;

            int score1 = ScoreAssignment(me, a, ally, b, player, m);
            int score2 = ScoreAssignment(me, b, ally, a, player, m);

            Log.Write($"quantium:   axis={axis} a={a} b={b} score1={score1} score2={score2}");

            if (score1 < bestScore) { bestScore = score1; bestGoal = a; }
            if (score2 < bestScore) { bestScore = score2; bestGoal = b; }
        }

        Log.Write($"quantium:   picked goal={bestGoal} score={bestScore}");
        return bestGoal;
    }

    static int ScoreAssignment(Pos unit, Pos goal, Pos ally, Pos allyGoal, Pos player, Monster m)
    {
        Func<Pos, bool> blocked = p => p == player || lvl.UnitAt(p) != null;
        int d1 = Pathfinding.PathCost(lvl, unit, goal, m, blocked);
        int d2 = Pathfinding.PathCost(lvl, ally, allyGoal, m, blocked);
        if (d1 == int.MaxValue || d2 == int.MaxValue) return int.MaxValue;
        return Math.Max(d1, d2);
    }

    static bool StepToward(Monster m, Pos goal, Pos? partnerPos)
    {
        if (m.Pos == goal) return false;

        Pos best = Pos.Invalid;
        int bestDist = int.MaxValue;

        foreach (var dir in Pos.AllDirs.Shuffled())
        {
            Pos candidate = m.Pos + dir;
            if (!lvl.InBounds(candidate)) continue;
            if (!lvl.CanMoveTo(m.Pos, candidate, m)) continue;
            if (partnerPos is { } pp && candidate.ChebyshevDist(pp) <= 1) continue;

            var blocker = lvl.UnitAt(candidate);
            if (blocker != null)
            {
                if (blocker is not Monster victim || victim.Def.BaseLevel + 8 > m.Def.BaseLevel)
                    continue;
            }

            int dist = candidate.ChebyshevDist(goal);
            if (dist < bestDist) { bestDist = dist; best = candidate; }
        }

        if (best == Pos.Invalid) return false;
        if (lvl.UnitAt(best) is Monster mook)
            Evaporate(m, mook);
        lvl.MoveUnit(m, best);
        return true;
    }

    static void Evaporate(Monster m, Monster victim)
    {
        g.YouObserve(victim, $"{m:The} {VTense(m, "crush")} {victim:the} underfoot!", "a sickening crunch");
        DoDie(victim);
    }

    static readonly (DamageType type, ConsoleColor color)[] BeamElements =
    [
        (DamageTypes.Fire, ConsoleColor.Red),
        (DamageTypes.Cold, ConsoleColor.Cyan),
        (DamageTypes.Shock, ConsoleColor.Yellow),
        (DamageTypes.Acid, ConsoleColor.Green),
        (DamageTypes.Sonic, ConsoleColor.Magenta),
    ];

    static void FireBeam(IUnit secondary, IUnit primary)
    {
        g.YouObserve(secondary, "Energy crackles between the quantium golems!", "a deafening hum");

        List<Pos> tiles = Pos.LineBetween(secondary.Pos, primary.Pos);
        if (tiles.Count == 0) return;

        // animate all 5 beams alternating direction
        for (int i = 0; i < BeamElements.Length; i++)
        {
            var (_, color) = BeamElements[i];
            Pos from = i % 2 == 0 ? secondary.Pos : primary.Pos;
            Pos to = i % 2 == 0 ? primary.Pos : secondary.Pos;
            Draw.AnimateBeam(from, to, new Glyph('Ϟ', color), delayMs: 20, pulse: true, includeTo: false);
        }

        // single damage pass per victim
        int dc = secondary.GetSpellDC();
        foreach (var tile in tiles)
        {
            var victim = lvl.UnitAt(tile);
            if (victim == null || victim == secondary || victim == primary) continue;
            using var ctx = PHContext.Create(secondary, Target.From(victim));
            CheckReflex(ctx, dc, "prismatic beam");
            foreach (var (type, _) in BeamElements)
                ctx.Damage.Add(new DamageRoll { Formula = d(5, 6), Type = type, HalfOnSave = true });
            DoDamage(ctx);
        }
    }
}

public static class Golems
{
    public static readonly MonsterFamily Family = new("golem");

    internal static bool IsFerromagnetic(Item item) => item.Material is Materials.Iron or Materials.ColdIron or Materials.Adamantine;

    // TODO: SR system
    static readonly LogicBrick[] CommonBricks = [ConstructTraits.Instance];
    static readonly LogicBrick[] CommonBricksPetriImmune = [ConstructTraits.WithPetrification];
    static readonly ConstructTraits ConstructTraitsBleedable = ConstructTraits.WithoutBleed;

    static MonsterDef G(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 8, int ac = 0, int ab = 0, int dmg = 0,
        int spawnWeight = 10, UnitSize size = UnitSize.Large,
        ActionCost? speed = null, WeaponDef? unarmed = null,
        int maxDepth = 99, GroupSize group = GroupSize.None,
        MonFlags extraFlags = MonFlags.None,
        LogicBrick[]? common = null) => new()
        {
            id = $"golem_{id}",
            Name = name,
            Family = Family,
            CreatureType = CreatureTypes.Construct,
            Glyph = new('\'', color),
            HpPerLevel = hp,
            AC = ac,
            AttackBonus = ab,
            Unarmed = unarmed ?? NaturalWeapons.Slam_1d6,
            LandMove = speed ?? ActionCosts.LandMove20,
            Size = size,
            BaseLevel = level,
            SpawnWeight = spawnWeight,
            MaxDepth = maxDepth,
            GroupSize = group,
            BrainFlags = MonFlags.NoCorpse | extraFlags,
            MoralAxis = MoralAxis.Neutral,
            EthicalAxis = EthicalAxis.Neutral,
            Components = [..common ?? CommonBricks, ..components,
            new GrantAction(new NaturalAttack(unarmed ?? NaturalWeapons.Slam_1d6))],
        };

    // --- CR 3-5: early golems ---

    public static readonly MonsterDef Wax = G("wax", "wax golem", 3, ConsoleColor.DarkYellow,
        [
            MeleeDamageRider.Fire_1d4,
            SlowedByElement.Fire,
            HealsFromElement.Cold,
            VulnerableToElement.Fire,
        ], size: UnitSize.Medium, unarmed: NaturalWeapons.Slam_1d4);

    public static readonly MonsterDef Carrion = G("carrion", "carrion golem", 4, ConsoleColor.DarkGreen,
        [
            SlowedByElement.Cold,
            SlowedByElement.Fire,
            CarrionAura.Instance,
            // TODO: disease on hit, might be a bit rough for a lvl4?
            // TODO: electricity hastes
        ], unarmed: NaturalWeapons.Slam_1d6);

    public static readonly MonsterDef Junk = G("junk", "junk golem", 4, ConsoleColor.Gray,
        [
            JunkSwarm.JunkSwarmOnDeath.Instance,
        ], size: UnitSize.Medium, unarmed: NaturalWeapons.Slam_1d4);


    // Not sure this one is really viable
    // public static readonly MonsterDef Mask = G("mask", "mask golem", 4, ConsoleColor.White,
    //     [
    //         // TODO: ranged mind control, swarm form, sees invisible
    //     ], size: UnitSize.Medium, unarmed: NaturalWeapons.Slam_1d4);

    static readonly BreathAttack IceBreath = new(BreathShape.Line, DamageTypes.Cold, ConsoleColor.Cyan, _ => 10, lvl => d(Math.Max(1, lvl / 2), 6), 6);

    public static readonly MonsterDef Ice = G("ice", "ice golem", 5, ConsoleColor.Cyan,
        [
            Thorns.Cold_1d4,
            SlowedByElement.Shock,
            HealsFromElement.Cold,
            DeathExplosion.Cold_3d6_R1,
            VulnerableToElement.Fire,
            new GrantAction(IceBreath),
        ], unarmed: NaturalWeapons.Slam_1d6);

    // --- CR 6-7: mid-early ---

    public static readonly MonsterDef Blood = G("blood", "blood golem", 6, ConsoleColor.DarkRed,
        [
            GrabOnHit.Instance,
            BloodConstrict.Instance,
        ], unarmed: NaturalWeapons.Slam_1d6,
        common: [ConstructTraitsBleedable]);

    public static readonly MonsterDef Wood = G("wood", "wood golem", 6, ConsoleColor.DarkYellow,
        [
            HealsFromElement.Cold,
            SplinterBurst.Instance,
            VulnerableToElement.Fire,
        ], unarmed: NaturalWeapons.Slam_1d6);

    public static readonly MonsterDef Flesh = G("flesh", "flesh golem", 7, ConsoleColor.DarkGreen,
        [
            SlowedByElement.Cold,
            SlowedByElement.Fire,
            HealsFromElement.Shock,
            new GrantAction(BerserkAttack.Chance30),
        ], unarmed: NaturalWeapons.Slam_2d6);

    // --- CR 8: mid ---

    public static readonly MonsterDef BoneShard = G("bone_shard", "bone shard golem", 8, ConsoleColor.White,
        [], hp: 4, ac: -2, unarmed: NaturalWeapons.Slam_1d6);

    public static readonly MonsterDef Bone = G("bone", "bone golem", 8, ConsoleColor.White,
        [
            new GrantAction(BonePrison.Instance),
            new SpawnOnDeath("bone_golem", "bones rise, and reassemble!", "bones rattling together", () => BoneShard),
        ], unarmed: NaturalWeapons.Slam_1d6, extraFlags: MonFlags.ToleratesUndead);

    public static readonly MonsterDef Glass = G("glass", "glass golem", 8, ConsoleColor.Blue,
        [
            SlowedByElement.Cold,
            HealsFromElement.Fire,
            new QueryBrick("reflection", (IUnit unit) => $"bounces off {unit:possessive} polished surface"),
            BleedOnHit.S2,
        ], unarmed: NaturalWeapons.Slam_1d6);

    public static readonly MonsterDef Marrowstone = G("marrowstone", "marrowstone golem", 8, ConsoleColor.DarkGray,
        [
            NecroticAura.Instance,
            new GrantAction(NecroticBurst.Instance),
        ], unarmed: NaturalWeapons.Slam_1d6, common: CommonBricksPetriImmune);

    // --- CR 9: mid ---

    public static readonly MonsterDef Sand = G("sand", "sand golem", 9, ConsoleColor.Yellow,
        [
            GrabOnHit.Instance,
            Constrict.Medium,
            DisarmOnHit.Instance,
            new GrantAction(SandBlast.Instance),
            VulnerableToElement.Shock,
        ], unarmed: NaturalWeapons.Slam_2d6, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Coral = G("coral", "coral golem", 9, ConsoleColor.DarkCyan,
        [
            BleedOnHit.S3,
            RegenBrick.Fire,
            new GrantAction(CoralSpike.Instance),
        ], unarmed: NaturalWeapons.Slam_1d6, ac: -1, hp: 6); //coral has quite good offsense and regen so nerf raw stats a little

    public static readonly MonsterDef Alchemical = G("alchemical", "alchemical golem", 9, ConsoleColor.Green,
        [
            MeleeDamageRider.Random_3d4,
            new GrantAction(AlchemicalBomb.Instance),
            VulnerableToElement.Sonic,
        ], unarmed: NaturalWeapons.Slam_2d6);

    // --- CR 10: mid-high ---

    public static readonly MonsterDef Clay = G("clay", "clay golem", 10, ConsoleColor.DarkYellow,
        [
            HealsFromElement.Acid,
            CursedWoundsOnHit.Instance,
            new GrantAction(GolemHaste.Instance),
        ], unarmed: NaturalWeapons.Slam_2d6, size: UnitSize.Large, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Lead = G("lead", "lead golem", 10, ConsoleColor.DarkGray,
        [
            HealsFromElement.Shock,
            PoisonCloudOnMeleeHit.Instance,
        ], unarmed: NaturalWeapons.Slam_2d6, speed: ActionCosts.LandMove15, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Magnetite = G("magnetite", "magnetite golem", 10, ConsoleColor.Gray,
        [
            HealsFromElement.Shock,
            MagneticGrabOnHit.Instance,
            Constrict.Heavy,
            new GrantAction(MagneticPull.Instance),
        ], unarmed: NaturalWeapons.Slam_2d6, common: CommonBricksPetriImmune);

    public static readonly MonsterDef EquineBone = G("equine_bone", "equine bone golem", 10, ConsoleColor.White,
        [
            new GrantAction(BoneCharge.Instance),
            new SpawnOnDeath("equine_bone", "The bones collapse and reassemble into a smaller form!", "bones clattering", () => BoneShard),
        ], unarmed: NaturalWeapons.Stomp_1d10, speed: ActionCosts.StandardLandMove, extraFlags: MonFlags.ToleratesUndead);

    // --- CR 11: high ---

    public static readonly MonsterDef Panthereon = G("panthereon", "panthereon", 11, ConsoleColor.DarkYellow,
        [
            HealsFromElement.Shock,
            CursedWoundsOnHit.Instance,
            TrueSeeingBuff.Instance,
            new GrantAction(GolemHaste.Instance),
            new GrantAction(EyeBeam.Instance),
        ], unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef Stone = G("stone", "stone golem", 11, ConsoleColor.Gray,
        [
            SlowOnHit.Instance,
            new GrantAction(TransmuteRockToMud.Instance),
        ], unarmed: NaturalWeapons.Slam_2d6, speed: ActionCosts.LandMove15, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Crystal = G("crystal", "crystal golem", 11, ConsoleColor.Cyan,
        [
            HealsFromElement.Fire,
            new QueryBrick("reflection", (IUnit unit) => $"bounces off {unit:possessive} crystalline surface"),
            new GrantAction(MindThrust.Instance),
        ], unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef Robot = G("robot", "robot golem", 11, ConsoleColor.Blue,
        [
            ShockRetaliation.Instance,
            new GrantAction(GolemHaste.Instance),
            new GrantAction(ShockBeam.Instance),
        ], unarmed: NaturalWeapons.Slam_2d6, common: CommonBricksPetriImmune);

    // --- CR 12: high ---

    public static readonly MonsterDef Clockwork = G("clockwork", "clockwork golem", 12, ConsoleColor.DarkGray,
        [
            GrabOnHit.Instance,
            Constrict.Crushing,
            DeathExplosion.Blunt_8d8_R1,
        ], unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef Obsidian = G("obsidian", "obsidian golem", 12, ConsoleColor.DarkRed,
        [
            BleedOnHit.S3,
            BleedRetaliation.S2,
            ObsidianDeathExplosion.Instance,
            new GrantAction(ObsidianSpray.Instance),
        ], unarmed: NaturalWeapons.Slam_2d6, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Fossil = G("fossil", "fossil golem", 12, ConsoleColor.DarkYellow,
        [
            Petrification.Instance.OnHit(),
            new GrantAction(FossilSummon.Instance),
        ], unarmed: NaturalWeapons.Slam_2d6, extraFlags: MonFlags.ToleratesUndead, common: CommonBricksPetriImmune);

    // --- CR 13-14: very high ---

    public static readonly MonsterDef Iron = G("iron", "iron golem", 13, ConsoleColor.Cyan,
        [
            SlowedByElement.Shock,
            HealsFromElement.Fire,
            SimpleDR.Universal.DR10,
            new GrantAction(PoisonBreath.Instance),
        ], unarmed: NaturalWeapons.Slam_4d8, size: UnitSize.Large, speed: ActionCosts.LandMove15, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Shadow = G("shadow", "shadow golem", 14, ConsoleColor.DarkGray,
        [
            ShadowAura.Instance,
            MeleeDamageRider.Anarchic_2d8,
            new GrantAction(ShadowBreath.Instance),
        ], unarmed: NaturalWeapons.Slam_2d6, size: UnitSize.Huge);

    public static readonly MonsterDef Brass = G("brass", "brass golem", 14, ConsoleColor.Yellow,
        [
            SlowedByElement.Cold,
            HealsFromElement.Fire,
            TrueSeeingBuff.Instance,
            DeathExplosion.Fire_6d8_R1,
            new GrantAction(IncendiaryBreath.Instance),
        ], unarmed: NaturalWeapons.Slam_2d8, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Inubrix = G("inubrix", "inubrix golem", 14, ConsoleColor.DarkCyan,
        [
            SlowedByElement.Fire,
            HealsFromElement.Cold,
            InubrixPhasing.Instance,
            InubrixPhaseStrip.Instance,
            new GrantAction(NegativeBreath.Instance),
        ], unarmed: NaturalWeapons.Claw_1d8, common: CommonBricksPetriImmune);

    // --- CR 15-16: boss tier ---

    public static readonly MonsterDef Cannon = G("cannon", "cannon golem", 15, ConsoleColor.Red,
        [
            new GrantAction(CannonSplash.Instance),
            new GrantAction(CannonShot.Instance),
        ], unarmed: NaturalWeapons.Slam_2d8, speed: 0, size: UnitSize.Huge);

    public static readonly MonsterDef Gold = G("gold", "gold golem", 15, ConsoleColor.Yellow,
        [
            SlowedByElement.Cold,
            HealsAndHastesFromElement.Fire,
            DeathFumes.Instance,
            new GrantAction(PrismaticSpray.Instance),
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Mithral = G("mithral", "mithral golem", 16, ConsoleColor.White,
        [
            MithralSpringBack.Instance,
            new GrantAction(MithralReachAttack.Instance),
        ], ac: 1, unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge, speed: ActionCosts.StandardLandMove, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Dragonhide = G("dragonhide", "dragonhide golem", 16, ConsoleColor.Red,
        [
            BerserkTrigger.Instance,
            new GrantAction(GolemDragonBreath.Instance),
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    // --- CR 17+: endgame ---

    public static readonly MonsterDef Behemoth = G("behemoth", "behemoth golem", 17, ConsoleColor.DarkGray,
        [
            HealsFromElement.Acid,
            new GrantAction(EarthquakeStomp.Instance),
            new GrantAction(Trample.Instance),
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Gargantuan, speed: ActionCosts.LandMove15);

    // probably not meaningful
    // public static readonly MonsterDef Ioun = G("ioun", "ioun golem", 17, ConsoleColor.Blue,
    //     [
    //         // TODO: sockets ioun stones, steals ioun stones, force missile surge
    //     ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    public static readonly MonsterDef Noqual = G("noqual", "noqual golem", 18, ConsoleColor.Green,
        [
            SlowedByElement.Shock,
            SilenceAura.Instance,
            SpellSunder.Instance,
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Viridium = G("viridium", "viridium golem", 18, ConsoleColor.DarkGreen,
        [
            RandomAfflictionOnHit.Instance,
            PetrificationExplosion.Instance,
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    public static readonly MonsterDef Adamantine = G("adamantine", "adamantine golem", 19, ConsoleColor.Magenta,
        [
            // TODO: indestructible (fast healing at 0hp, needs vorpal), armor sunder on crit
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge, common: CommonBricksPetriImmune);

    public static readonly MonsterDef Quantium = G("quantium", "quantium golem", 20, ConsoleColor.Magenta,
        [
            SlowedByElement.Cold,
            SlowedByElement.Shock,
            QuintalPair.Instance,
            // TODO: beam between pair (5d6 × 5 energy types)
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Gargantuan, common: CommonBricksPetriImmune)
        .WithBrain(new QuantiumBrain());

    public static readonly MonsterDef Quintessence = G("quintessence", "quintessence golem", 20, ConsoleColor.Magenta,
        [
            // TODO: energy drain, soul siphon, fast healing 20, fly
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    // TODO: Adamantine and Quintessence, they need a bunch of support but we have golem fatigue already
    public static readonly MonsterDef[] All =
    [
        // Mask,  - not mask
        // Ioun, 
        Wax, Carrion, Junk, Ice,
        Blood, Wood, Flesh,
        Bone, Glass, Marrowstone,
        Sand, Coral, Alchemical,
        Clay, Lead, Magnetite, EquineBone,
        Panthereon, Stone, Crystal, Robot,
        Clockwork, Obsidian, Fossil,
        Iron, Shadow, Brass, Inubrix,
        Cannon, Gold, Mithral, Dragonhide,
        Behemoth, Noqual, Viridium,
        Adamantine, Quantium, Quintessence,
    ];
}