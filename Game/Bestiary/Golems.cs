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

    public static readonly ConstructTraits Instance = new("construct:traits", AllImmunities);
    public static readonly ConstructTraits WithoutBleed = new("construct:traits_without_bleed_immune", WithoutBleedImmunity);

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
            unit.AddFact(SlowedByElementDebuff.Instance, ctx.Source, 3);
    }

    public static readonly SlowedByElement Fire = new(DamageTypes.Fire);
    public static readonly SlowedByElement Cold = new(DamageTypes.Cold);
    public static readonly SlowedByElement Shock = new(DamageTypes.Shock);
}

public class SlowedByElementDebuff : LogicBrick
{
    public static readonly SlowedByElementDebuff Instance = new();
    public override string Id => "slowed_by_element";
    public override bool IsBuff => true;
    public override string? BuffName => "Slowed";
    public override StackMode StackMode => StackMode.ExtendDuration;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.NumWhen("speed_mult", 0.5);
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

        foreach (var t in un.Pos.Neighbours())
        {
            if (lvl.UnitAt(t) is not {} tgt) continue;
            if (tgt.Has(CommonQueries.DazeImmune)) continue;
            tgt.AddFact(DazedBuff.Instance, un, 1);
        }
    }
}

public class DeathExplosion(DamageType type, Dice damage, int radius, ConsoleColor color) : LogicBrick
{
    public override string Id => $"golem:death_explosion+{type.SubCat}";
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
            if (victim == unit) return;
            using var ctx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(ctx, dc, type.SubCat);
            ctx.Damage.Add(new DamageRoll { Formula = damage, Type = type, HalfOnSave = true });
            DoDamage(ctx);
        }, AreaSystem.OnTile(type));
    }

    public static readonly DeathExplosion Cold_3d6_R1 = new(DamageTypes.Cold, d(3, 6), 1, ConsoleColor.Cyan);
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
            if (victim == unit) return;
            using var dmgCtx = PHContext.Create(unit, Target.From(victim));
            CheckReflex(dmgCtx, dc, "slashing");
            dmgCtx.Damage.Add(new DamageRoll { Formula = damage, Type = DamageTypes.Slashing, HalfOnSave = true });
            DoDamage(dmgCtx);
        }, AreaSystem.OnTile(DamageTypes.Slashing));
    }

    public static readonly SplinterBurst Instance = new(d(2, 6), 1);
}

public class BonePrison(int cd, int range)
    : CooldownAction("Bone Prison", TargetingType.None, _ => cd, tags: AbilityTags.Harmful)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var plan = base.CanExecute(unit, data, target);
        if (!plan) return plan;
        if (target.Unit == null) return "no target";
        if (unit.Pos.ChebyshevDist(target.Unit.Pos) > range) return "too far";
        if (lvl.Traps.ContainsKey(target.Unit.Pos)) return "already trapped";
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

public static class Golems
{
    public static readonly MonsterFamily Family = new("golem");

    // TODO: SR system
    static readonly LogicBrick[] CommonBricks = [ConstructTraits.Instance];
    static readonly ConstructTraits ConstructTraitsBleedable = ConstructTraits.WithoutBleed;

    static MonsterDef G(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 8, int ac = 0, int ab = 0, int dmg = 0,
        int spawnWeight = 10, UnitSize size = UnitSize.Large,
        ActionCost? speed = null, WeaponDef? unarmed = null,
        int maxDepth = 99, GroupSize group = GroupSize.None,
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
        BrainFlags = MonFlags.NoCorpse,
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
        ], unarmed: NaturalWeapons.Slam_1d6);

    public static readonly MonsterDef Glass = G("glass", "glass golem", 8, ConsoleColor.Blue,
        [
            SlowedByElement.Cold,
            HealsFromElement.Fire,
            new QueryBrick("reflection", (IUnit unit) => $"bounces off {unit:possessive} polished surface"),
            // bleed on hit
        ], unarmed: NaturalWeapons.Slam_1d6);

    public static readonly MonsterDef Marrowstone = G("marrowstone", "marrowstone golem", 8, ConsoleColor.DarkGray,
        [
            // TODO: necrotic aura (buffs undead), negative energy slam, kills create ghouls
        ], unarmed: NaturalWeapons.Slam_1d6);

    // --- CR 9: mid ---

    public static readonly MonsterDef Sand = G("sand", "sand golem", 9, ConsoleColor.Yellow,
        [
            GrabOnHit.Instance,
            Constrict.Medium,
            // TODO: disarm on hit, sand blast cone (fire+blunt+blind)
            VulnerableToElement.Shock,
        ], unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef Coral = G("coral", "coral golem", 9, ConsoleColor.DarkCyan,
        [
            // TODO: bleed, fast healing in water, water spells heal
        ], unarmed: NaturalWeapons.Slam_1d6);

    public static readonly MonsterDef Alchemical = G("alchemical", "alchemical golem", 9, ConsoleColor.Green,
        [
            // TODO: random element on hit, ranged bombs, splash damage when meleed
            VulnerableToElement.Sonic,
        ], unarmed: NaturalWeapons.Slam_2d6);

    // --- CR 10: mid-high ---

    public static readonly MonsterDef Clay = G("clay", "clay golem", 10, ConsoleColor.DarkYellow,
        [
            HealsFromElement.Acid,
            // TODO: cursed wounds (healing blocked), haste self, berserk
        ], unarmed: NaturalWeapons.Slam_2d6, size: UnitSize.Large);

    public static readonly MonsterDef Lead = G("lead", "lead golem", 10, ConsoleColor.DarkGray,
        [
            HealsFromElement.Shock,
            // TODO: poison cloud when hit through DR
        ], unarmed: NaturalWeapons.Slam_2d6, speed: ActionCosts.LandMove15);

    public static readonly MonsterDef Magnetite = G("magnetite", "magnetite golem", 10, ConsoleColor.Gray,
        [
            HealsFromElement.Shock,
            // TODO: magnetic pull aura, disarm on hit, grapple metal-wearers
        ], unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef EquineBone = G("equine_bone", "equine bone golem", 10, ConsoleColor.White,
        [
            // TODO: trample, staggering stomp AoE
        ], unarmed: NaturalWeapons.Stomp_1d10, speed: ActionCosts.LandMove25);

    // --- CR 11: high ---

    public static readonly MonsterDef Panthereon = G("panthereon", "panthereon", 11, ConsoleColor.DarkYellow,
        [
            HealsFromElement.Shock,
            // TODO: cursed wounds, eye beam (fire+blind), haste self, true seeing
        ], unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef Stone = G("stone", "stone golem", 11, ConsoleColor.Gray,
        [
            // TODO: slow aura burst, transmute rock to mud slows
        ], unarmed: NaturalWeapons.Slam_2d6, speed: ActionCosts.LandMove15);

    public static readonly MonsterDef Crystal = G("crystal", "crystal golem", 11, ConsoleColor.Cyan,
        [
            HealsFromElement.Fire,
            // TODO: psychic magic (mind thrust, explode head), psychic amplification aura
        ], unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef Robot = G("robot", "robot golem", 11, ConsoleColor.Blue,
        [
            // TODO: electricity shockwave AoE, rend constructs, electricity hastes+berserks
        ], unarmed: NaturalWeapons.Slam_2d6);

    // --- CR 12: high ---

    public static readonly MonsterDef Clockwork = G("clockwork", "clockwork golem", 12, ConsoleColor.DarkGray,
        [
            // TODO: adjacency damage aura, grind on grapple, death burst, grease hastes
        ], unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef Obsidian = G("obsidian", "obsidian golem", 12, ConsoleColor.DarkRed,
        [
            // TODO: heavy bleed, jagged body (bleed on melee contact), obsidian spray cone, death explosion
        ], unarmed: NaturalWeapons.Slam_2d6);

    public static readonly MonsterDef Fossil = G("fossil", "fossil golem", 12, ConsoleColor.DarkYellow,
        [
            // TODO: petrification on hit, transmute rock to mud slows
        ], unarmed: NaturalWeapons.Slam_2d6);

    // --- CR 13-14: very high ---

    public static readonly MonsterDef Iron = G("iron", "iron golem", 13, ConsoleColor.Cyan,
        [
            SlowedByElement.Shock,
            HealsFromElement.Fire,
            // TODO: poison breath, powerful blows
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Large, speed: ActionCosts.LandMove15);

    public static readonly MonsterDef Shadow = G("shadow", "shadow golem", 14, ConsoleColor.DarkGray,
        [
            // TODO: shadow aura (dims light, +AC), Str damage breath, stealth in dim light
        ], unarmed: NaturalWeapons.Slam_2d6, size: UnitSize.Huge);

    public static readonly MonsterDef Brass = G("brass", "brass golem", 14, ConsoleColor.Yellow,
        [
            SlowedByElement.Cold,
            HealsFromElement.Fire,
            // TODO: incendiary cloud breath, death explosion, sees invisible
        ], unarmed: NaturalWeapons.Slam_2d8);

    public static readonly MonsterDef Inubrix = G("inubrix", "inubrix golem", 14, ConsoleColor.DarkCyan,
        [
            SlowedByElement.Fire,
            HealsFromElement.Cold,
            // TODO: phases through metal armor, negative energy breath, razor claws
        ], unarmed: NaturalWeapons.Claw_1d8);

    // --- CR 15-16: boss tier ---

    public static readonly MonsterDef Cannon = G("cannon", "cannon golem", 15, ConsoleColor.Red,
        [
            // TODO: ranged cannon (6d6), blasting critical, water disables cannon
        ], unarmed: NaturalWeapons.Slam_2d8, speed: ActionCosts.LandMove15, size: UnitSize.Huge);

    public static readonly MonsterDef Gold = G("gold", "gold golem", 15, ConsoleColor.Yellow,
        [
            // TODO: prismatic surge on hit, death fumes, cold slows, fire reduces DR but hastes
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    public static readonly MonsterDef Mithral = G("mithral", "mithral golem", 16, ConsoleColor.White,
        [
            // TODO: fluid form (reach), quickness, evasion, Spring Attack
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge, speed: ActionCosts.LandMove25);

    public static readonly MonsterDef Dragonhide = G("dragonhide", "dragonhide golem", 16, ConsoleColor.Red,
        [
            // TODO: breath weapon (random type on spawn), berserk (2%/round, +8 str)
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    // --- CR 17+: endgame ---

    public static readonly MonsterDef Behemoth = G("behemoth", "behemoth golem", 17, ConsoleColor.DarkGray,
        [
            HealsFromElement.Acid,
            // TODO: earthquake stomp, trample (multi-tile deferred)
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Gargantuan, speed: ActionCosts.LandMove15);

    public static readonly MonsterDef Ioun = G("ioun", "ioun golem", 17, ConsoleColor.Blue,
        [
            // TODO: sockets ioun stones, steals ioun stones, force missile surge
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    public static readonly MonsterDef Noqual = G("noqual", "noqual golem", 18, ConsoleColor.Green,
        [
            // TODO: anti-magic aura, spell sunder on hit, spell absorption heals+hastes, electricity slows
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    public static readonly MonsterDef Viridium = G("viridium", "viridium golem", 18, ConsoleColor.DarkGreen,
        [
            // TODO: disease on all attacks (leprosy), heal spell damages it, remove disease staggers
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    public static readonly MonsterDef Adamantine = G("adamantine", "adamantine golem", 19, ConsoleColor.Magenta,
        [
            // TODO: indestructible (fast healing at 0hp, needs vorpal), armor sunder on crit
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    public static readonly MonsterDef Quantium = G("quantium", "quantium golem", 20, ConsoleColor.Magenta,
        [
            // TODO: 240ft line (30d6 mixed energy), paired spawn, cold/electricity slows
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Gargantuan);

    public static readonly MonsterDef Quintessence = G("quintessence", "quintessence golem", 20, ConsoleColor.Magenta,
        [
            // TODO: energy drain, soul siphon, fast healing 20, fly
        ], unarmed: NaturalWeapons.Slam_2d8, size: UnitSize.Huge);

    public static readonly MonsterDef[] All =
    [
        // Mask,  - not mask
        Wax, Carrion, Junk, Ice,
        Blood, Wood, Flesh,
        Bone, Glass, Marrowstone,
        Sand, Coral, Alchemical,
        Clay, Lead, Magnetite, EquineBone,
        Panthereon, Stone, Crystal, Robot,
        Clockwork, Obsidian, Fossil,
        Iron, Shadow, Brass, Inubrix,
        Cannon, Gold, Mithral, Dragonhide,
        Behemoth, Ioun, Noqual, Viridium,
        Adamantine, Quantium, Quintessence,
    ];
}
