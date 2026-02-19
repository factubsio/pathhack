namespace Pathhack.Game.Bestiary;

public class ThrowBranch(int cd, int range)
    : CooldownAction("Throw Branch", TargetingType.None, _ => cd, tags: AbilityTags.Harmful)
{
    static readonly WeaponDef BranchProjectile = new()
    {
        id = "thrown_branch",
        Name = "branch",
        BaseDamage = d(6),
        Profiency = Proficiencies.Unarmed,
        DamageType = DamageTypes.Blunt,
        Glyph = new('/', ConsoleColor.DarkYellow),
        Launcher = "tree",
        Weight = -1,
        Price = -1,
    };

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var basePlan = base.CanExecute(unit, data, target);
        if (!basePlan) return basePlan;
        if (target.Unit == null) return "no target";
        if (!unit.Pos.IsCompassFrom(target.Unit.Pos)) return "not compass";
        if (unit.Pos.ChebyshevDist(target.Unit.Pos) > range) return "too far";
        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit == null) return;
        var dir = (target.Unit.Pos - unit.Pos).Signed;
        var item = Item.Create(BranchProjectile);
        g.YouObserve(unit, $"{unit:The} hurls a branch!", "a whoosh of wood");
        DoThrow(unit, item, dir, AttackType.Thrown);
    }
}

public class TossMonster(int cd)
    : CooldownAction("Toss", TargetingType.None, _ => cd, tags: AbilityTags.Harmful)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var basePlan = base.CanExecute(unit, data, target);
        if (!basePlan) return basePlan;
        if (target.Unit == null) return "no target";
        if (!unit.Pos.IsCompassFrom(target.Unit.Pos)) return "not compass";

        var ammo = FindAmmo(unit);
        if (ammo == null) return "nothing to throw";
        return new(true, Plan: ammo);
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit == null) return;
        var ammo = plan as Monster ?? FindAmmo(unit);
        if (ammo == null) return;

        Pos dir = (target.Unit.Pos - unit.Pos).Signed;
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "hurl")} {ammo:the} at {target.Unit:the}!", "a scream and a thud");

        Glyph glyph = ammo.OwnGlyph ?? ammo.Def.Glyph;
        ammo.HiddenFromRender = true;
        Draw.DrawCurrent();
        Draw.AnimateProjectile(unit.Pos, target.Unit.Pos, glyph);

        using var ctx = PHContext.Create(unit, Target.From(target.Unit));
        if (DoAttackRoll(ctx, 0))
        {
            ctx.Damage.Add(new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Blunt });
            DoDamage(ctx);

            if (!ammo.IsDead)
            {
                using var ctx2 = PHContext.Create(unit, Target.From(ammo));
                ctx2.Damage.Add(new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Blunt });
                DoDamage(ctx2);
            }

            if (!ammo.IsDead)
                LandMonster(ammo, target.Unit.Pos - dir, target.Unit.Pos);
        }
        else
        {
            g.YouObserve(unit, $"{ammo:The} sails past!", "a whoosh");
            if (!ammo.IsDead)
                LandMonster(ammo, target.Unit.Pos + dir, target.Unit.Pos);
        }
    }

    static void LandMonster(Monster m, Pos preferred, Pos near)
    {
        try
        {
            if (lvl.InBounds(preferred) && lvl.CanMoveTo(preferred, preferred, m) && lvl.UnitAt(preferred) == null)
            {
                lvl.MoveUnit(m, preferred, true);
                return;
            }
            // Fallback: find any open tile adjacent to the target
            foreach (var d in Pos.AllDirs.Shuffled())
            {
                Pos alt = near + d;
                if (lvl.InBounds(alt) && lvl.CanMoveTo(alt, alt, m) && lvl.UnitAt(alt) == null)
                {
                    lvl.MoveUnit(m, alt, true);
                    return;
                }
            }
        }
        finally
        {
            m.HiddenFromRender = false;
            Draw.DrawCurrent();
        }
        // No valid landing — stays put
    }

    static Monster? FindAmmo(IUnit unit)
    {
        if (unit is not Monster me) return null;
        foreach (var m in unit.Pos.Neighbours().Select(lvl.UnitAt).OfType<Monster>().Where(m => m.Def.Size < me.Def.Size))
            return m;
        return null;
    }
}

public class ConfusingMoan(int cd, int range)
    : CooldownAction("Confusing Moan", TargetingType.None, _ => cd, tags: AbilityTags.Harmful)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var basePlan = base.CanExecute(unit, data, target);
        if (!basePlan) return basePlan;
        if (target.Unit == null) return "no target";
        if (unit.Pos.ChebyshevDist(target.Unit.Pos) > range) return "too far";
        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit == null) return;
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "let")} out an eerie moan!", "an unsettling drone");
        int dc = unit.GetSpellDC();
        using var ctx = PHContext.Create(unit, Target.From(target.Unit));
        if (!CheckWill(ctx, dc, "confusing moan"))
            target.Unit.AddFact(ConfusedBuff.Instance, duration: 3);
    }
}

public class DazeAura : LogicBrick
{
    public static readonly DazeAura Instance = new();
    public override string Id => "daze_aura";
    public override bool IsActive => true;

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not IUnit unit || !unit.Allows("can_act")) return;
        int dc = unit.GetSpellDC();
        foreach (var tgt in unit.Pos.Neighbours().Select(lvl.UnitAt))
        {
            if (tgt == null || tgt.Has(CommonQueries.DazeImmune)) continue;
            using var ctx = PHContext.Create(unit, Target.From(tgt));
            if (!CheckWill(ctx, dc, "daze aura"))
                tgt.AddFact(DazedBuff.Instance, 1);
        }
    }
}

public class AnimateTrees(int chance, MonsterDef[] pool) : LogicBrick
{
    public override string Id => "animate_trees";
    public override bool IsActive => true;

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not Monster unit || !unit.CanSeeYou || !unit.Allows("can_act")) return;

        int trees = unit.Pos.Neighbours().Count(p => lvl.InBounds(p) && lvl[p].Type == TileType.Tree);
        int effectiveChance = trees > 0 ? chance / 2 : chance;
        if (g.Rn2(effectiveChance) != 0) return;

        var pos = Pos.Zero;
        for (int i = 0; i < 2 + trees; i++)
        {
            Pos p = unit.Pos + new Pos(g.RnRange(-3, 3), g.RnRange(-3, 3));
            if (p != unit.Pos && lvl.InBounds(p) && lvl.NoUnit(p) && lvl.CanMoveTo(p, p, unit))
            { pos = p; break; }
        }
        if (pos == Pos.Zero) return;

        var def = pool.Pick();
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "animate")} a nearby tree!", "the creaking of wood");
        g.Defer(() => MonsterSpawner.SpawnAndPlace(lvl, "treant", def, allowTemplate: false, pos: pos));
    }
}

public class PlaceVineSnare(int cd, int range)
    : CooldownAction("Vine Snare", TargetingType.None, _ => cd, tags: AbilityTags.Harmful)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var basePlan = base.CanExecute(unit, data, target);
        if (!basePlan) return basePlan;
        if (target.Unit == null) return "no target";
        if (unit.Pos.ChebyshevDist(target.Unit.Pos) > range) return "too far";
        if (lvl.Traps.ContainsKey(target.Unit.Pos)) return "already trapped";
        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit == null) return;
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "send")} vines snaking toward {target.Unit:the}!", "rustling vines");
        var trap = new WebTrap(lvl.Depth) { PlayerSeen = true };
        lvl.Traps[target.Unit.Pos] = trap;
        trap.Trigger(target.Unit, null);
    }
}

public class SpawnFirePatches(int cd, int range) 
    : CooldownAction("Fire Patch", TargetingType.None, _ => cd, tags: AbilityTags.Harmful)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var basePlan = base.CanExecute(unit, data, target);
        if (!basePlan) return basePlan;
        if (target.Unit == null) return "no target";
        if (unit.Pos.ChebyshevDist(target.Unit.Pos) > range) return "too far";
        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        if (target.Unit == null) return;
        g.YouObserve(unit, $"{unit:The} {VTense(unit, "ignite")} the ground beneath {target.Unit:the}!", "a whoosh of flames");
        var area = new FirePatchArea(unit, 6) { Tiles = [target.Unit.Pos] };
        lvl.CreateArea(area);
    }
}

public class GallowsRaise(int range) : LogicBrick<GallowsRaise.State>
{
    public override string Id => "gallows_raise";
    public override bool IsActive => true;

    public class State
    {
        public List<(MonsterDef Def, Pos Pos, int SpawnRound)> Pending = [];
    }

    static readonly ZombieTemplate Zombie = new();

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not Monster unit || !unit.Allows("can_act")) return;
        var state = X(fact);

        // Spawn ripe zombies
        for (int i = state.Pending.Count - 1; i >= 0; i--)
        {
            var (def, pos, round) = state.Pending[i];
            if (g.CurrentRound < round) continue;
            state.Pending.RemoveAt(i);
            if (lvl.NoUnit(pos))
            {
                g.Defer(() =>
                {
                    int bonus = def.CreatureType == CreatureTypes.Undead ? -4 : -2;
                    var mon = Monster.Spawn(def, "gallows", Zombie, depthBonus: bonus, firstTimeSpawn: false);
                    lvl.PlaceUnit(mon, pos);
                });
            }
        }

        // Consume corpses
        for (int i = lvl.Corpses.Count - 1; i >= 0; i--)
        {
            var (corpse, pos) = lvl.Corpses[i];
            if (unit.Pos.ChebyshevDist(pos) > range) continue;
            if (corpse.CorpseOf == null || !Zombie.CanApplyTo(corpse.CorpseOf)) continue;

            var def = corpse.CorpseOf;
            lvl.RemoveItem(corpse, pos);
            g.YouObserve(unit, $"The corpse of {def.Name:the} begins to twitch...", "an unsettling creaking");
            state.Pending.Add((def, pos, g.CurrentRound + 4));
            break; // one per round
        }
    }
}

public class HeartrotDisease() : AfflictionBrick(16, "fortitude")
{
    public static readonly HeartrotDisease Instance = new();
    public override bool IsActive => true;
    public override string Id => "heartrot";
    public override string AfflictionName => "Heartrot";
    public override int MaxStage => 4;
    public override DiceFormula TickInterval => d(25, 10);

    protected override void DoPeriodicEffect(IUnit unit, int stage)
    {
        var msg = stage switch
        {
            1 => "Dark veins spread across your skin.",
            2 => "Your thoughts feel sluggish.",
            3 => "Your body aches with rot.",
            4 => "You can feel the rot in your bones.",
            _ => null
        };
        if (msg != null && unit.IsPlayer) g.pline(msg);
    }

    protected override object? DoQuery(int stage, string key, string? arg) => key switch
    {
        "wis" => new Modifier(ModifierCategory.StatusPenalty, -stage, "heartrot"),
        "con" when stage >= 2 => new Modifier(ModifierCategory.StatusPenalty, -(stage / 2), "heartrot"),
        _ => null
    };

    protected override void OnCured(IUnit unit)
    {
        if (unit.IsPlayer) g.pline("The rot recedes from your body.");
    }

    protected override void OnRoundEnd(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        var stage = Stage(fact);
        if (stage < 2 || g.Rn2(stage >= 4 ? 3 : 6) != 0) return;
        if (unit.Has(CommonQueries.DazeImmune)) return;
        unit.AddFact(DazedBuff.Instance, 1);
    }
}

public static class Trees
{
    static MonsterDef T(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 8, int ac = 0, int ab = 0, int dmg = 0,
        int spawnWeight = 10, UnitSize size = UnitSize.Huge,
        ActionCost? speed = null, WeaponDef? unarmed = null,
        MonFlags flags = MonFlags.None)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = "tree",
            CreatureType = CreatureTypes.Plant,
            Glyph = new('±', color),
            HpPerLevel = hp,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            Unarmed = unarmed ?? NaturalWeapons.Slam_1d6,
            Size = size,
            BaseLevel = level,
            MinDepth = level,
            MaxDepth = 99,
            SpawnWeight = spawnWeight,
            MoralAxis = MoralAxis.Neutral,
            EthicalAxis = EthicalAxis.Neutral,
            BrainFlags = MonFlags.NoCorpse | flags,
            LandMove = speed ?? 0,
            Components = components,
        };
    }

    // --- Low tier ---

    public static readonly MonsterDef Sapling = T("sapling", "sapling", 3, ConsoleColor.Yellow,
        size: UnitSize.Small, hp: 5, unarmed: NaturalWeapons.Slam_1d4,
        components: [
            new GrantAction(new NaturalAttack(NaturalWeapons.Slam_1d4)),
        ]);

    public static readonly MonsterDef ScytheTree = T("scythe_tree", "scythe tree", 5, ConsoleColor.Red,
        speed: 36, unarmed: NaturalWeapons.Branch_1d6,
        components: [
            SimpleDR.Slashing.Lookup(5),
            new GrantAction(new FullAttack("scythe", NaturalWeapons.Branch_1d6, NaturalWeapons.Branch_1d6, NaturalWeapons.Branch_1d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Branch_1d6)),
        ]);

    // --- Mid tier ---

    public static readonly MonsterDef ConchTree = T("conch_tree", "conch tree", 7, ConsoleColor.Cyan,
        unarmed: NaturalWeapons.Bite_1d6,
        components: [
            new GrantAction(new ThrowBranch(cd: 3, range: 6)),
            MeleeDamageRider.Acid_2d6,
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d6)),
        ]);

    public static readonly MonsterDef Shambler = T("shambler", "shambling mound", 8, ConsoleColor.DarkYellow,
        speed: 22, unarmed: NaturalWeapons.Slam_1d6,
        components: [
            RegenBrick.Fire,
            GrabOnHit.Instance,
            new GrantAction(new TonguePull(20, verb: "snare", flavor: "with its vines", sound: "a rustling of vines")),
            new GrantAction(new FullAttack("shamble", NaturalWeapons.Slam_1d6, NaturalWeapons.Tendril_1d4, NaturalWeapons.Tendril_1d4)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Slam_1d6)),
        ]);

    public static readonly MonsterDef Tendriculos = T("tendriculos", "tendriculos", 9, ConsoleColor.DarkRed,
        unarmed: NaturalWeapons.Bite_1d8,
        components: [
            RegenBrick.Fire,
            GrabOnHit.Instance,
            DazeOnHit.Instance,
            new GrantAction(new TonguePull(18, verb: "snare", flavor: "with its tendrils", sound: "a rustling of vines")),
            new GrantAction(new FullAttack("tendrils", NaturalWeapons.Bite_1d8, NaturalWeapons.Tendril_1d4, NaturalWeapons.Tendril_1d4)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d8)),
        ]);

    public static readonly MonsterDef Ironbark = T("ironbark", "ironbark tree", 10, ConsoleColor.Gray,
        unarmed: NaturalWeapons.Slam_2d6,
        components: [
            SimpleDR.Adamantine.Lookup(10),
            new GrantAction(new TossMonster(5)),
            new GrantAction(new ThrowBranch(cd: 3, range: 8)),
            new GrantAction(new FullAttack("ironbark", NaturalWeapons.Slam_2d6, NaturalWeapons.Slam_2d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Slam_2d6)),
        ]);

    public static readonly MonsterDef Quickwood = T("quickwood", "quickwood", 11, ConsoleColor.DarkRed,
        speed: ActionCosts.StandardLandMove, unarmed: NaturalWeapons.Branch_1d8,
        components: [
            SimpleDR.Slashing.Lookup(11),
            new GrantAction(new FullAttack("quickwood", NaturalWeapons.Branch_1d8, NaturalWeapons.Branch_1d8, NaturalWeapons.Branch_1d8)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Branch_1d8)),
        ]);

    // --- Aura / caster ---

    public static readonly MonsterDef Jinmenju = T("jinmenju", "jinmenju", 12, ConsoleColor.DarkCyan,
        speed: 36, unarmed: NaturalWeapons.Slam_1d6,
        components: [
            DazeAura.Instance,
            new GrantAction(new ConfusingMoan(cd: 6, range: 5)),
            new GrantAction(new FullAttack("jinmenju", NaturalWeapons.Slam_1d6, NaturalWeapons.Slam_1d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Slam_1d6)),
        ]);

    public static readonly MonsterDef ApprenticeWitchTree = T("apprentice_witch_tree", "apprentice witch tree", 9, ConsoleColor.DarkCyan,
        speed: 36, unarmed: NaturalWeapons.Tendril_1d6,
        flags: MonFlags.PrefersCasting,
        components: [
            SimpleDR.Slashing.Lookup(9),
            new GrantPool("spell_l1", 2, 20),
            new GrantSpell(BasicLevel1Spells.MagicMissile),
            new GrantAction(new FullAttack("witch", NaturalWeapons.Tendril_1d6, NaturalWeapons.Tendril_1d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Tendril_1d6)),
        ]);

    public static readonly MonsterDef WitchTree = T("witch_tree", "witch tree", 13, ConsoleColor.Cyan,
        speed: 36, unarmed: NaturalWeapons.Tendril_1d6,
        flags: MonFlags.PrefersCasting,
        components: [
            SimpleDR.Slashing.Lookup(13),
            new GrantPool("spell_l1", 2, 15),
            new GrantPool("spell_l2", 1, 20),            new GrantSpell(BasicLevel1Spells.MagicMissile),
            new GrantSpell(BasicLevel2Spells.HoldPerson),
            new GrantSpell(BasicLevel2Spells.ScorchingRay),
            new GrantAction(new FullAttack("witch", NaturalWeapons.Tendril_1d6, NaturalWeapons.Tendril_1d6, NaturalWeapons.Tendril_1d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Tendril_1d6)),
        ]);

    public static readonly MonsterDef CroneWitchTree = T("crone_witch_tree", "crone witch tree", 17, ConsoleColor.Magenta,
        speed: 36, unarmed: NaturalWeapons.Tendril_1d6,
        flags: MonFlags.PrefersCasting,
        components: [
            SimpleDR.Slashing.Lookup(17),
            new GrantPool("spell_l1", 2, 12),
            new GrantPool("spell_l2", 2, 15),
            new GrantPool("spell_l3", 2, 20),
            new GrantSpell(BasicLevel1Spells.MagicMissile),
            new GrantSpell(BasicLevel2Spells.HoldPerson),
            new GrantSpell(BasicLevel2Spells.ScorchingRay),
            new GrantSpell(BasicLevel3Spells.Fireball),
            new GrantSpell(BasicLevel3Spells.VampiricTouch),
            new GrantAction(new FullAttack("witch", NaturalWeapons.Tendril_1d6, NaturalWeapons.Tendril_1d6, NaturalWeapons.Tendril_1d6, NaturalWeapons.Tendril_1d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Tendril_1d6)),
        ]);

    // --- Treant line ---

    public static readonly MonsterDef Treant = T("treant", "treant", 13, ConsoleColor.DarkGreen,
        speed: 18, size: UnitSize.Huge, unarmed: NaturalWeapons.Slam_2d6,
        components: [
            SimpleDR.Slashing.Lookup(13),
            new AnimateTrees(10, [Sapling, ScytheTree]),
            new GrantAction(new FullAttack("treant", NaturalWeapons.Slam_2d6, NaturalWeapons.Slam_2d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Slam_2d6)),
        ]);

    public static readonly MonsterDef JungleTreant = T("jungle_treant", "jungle treant", 14, ConsoleColor.Green,
        speed: 18, size: UnitSize.Huge, unarmed: NaturalWeapons.Slam_2d6,
        components: [
            SimpleDR.Slashing.Lookup(14),
            new AnimateTrees(9, [Sapling, ScytheTree, Quickwood]),
            new GrantAction(new PlaceVineSnare(cd: 8, range: 3)),
            new GrantAction(new FullAttack("treant", NaturalWeapons.Slam_2d6, NaturalWeapons.Slam_2d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Slam_2d6)),
        ]);

    public static readonly MonsterDef BonfireTreant = T("bonfire_treant", "bonfire treant", 16, ConsoleColor.DarkGreen,
        speed: 18, size: UnitSize.Huge, unarmed: NaturalWeapons.Slam_2d6,
        components: [
            SimpleDR.Slashing.Lookup(16),
            EnergyResist.Dynamic(DamageTypes.Fire),
            new AnimateTrees(7, [Sapling, ScytheTree]),
            new GrantAction(new SpawnFirePatches(cd: 2, range: 4)),
            new GrantAction(new FullAttack("treant", NaturalWeapons.Slam_2d6, NaturalWeapons.Slam_2d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Slam_2d6)),
        ]);

    // --- High tier ---

    public static readonly MonsterDef GallowsTree = T("gallows_tree", "gallows tree", 15, ConsoleColor.DarkGray,
        speed: 0, unarmed: NaturalWeapons.Slam_2d6,
        components: [
            SimpleDR.Slashing.Lookup(15),
            new GallowsRaise(3),
            new GrantAction(new FullAttack("gallows", NaturalWeapons.Slam_2d6, NaturalWeapons.Slam_2d6, NaturalWeapons.Slam_2d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Slam_2d6)),
        ]);

    public static readonly MonsterDef HeartrotTree = T("heartrot_tree", "heartrot tree", 17, ConsoleColor.Red,
        speed: 36, unarmed: NaturalWeapons.Slam_2d6,
        components: [
            SimpleDR.Slashing.Lookup(17),
            HeartrotDisease.Instance.OnHit(),
            new GrantAction(new FullAttack("heartrot", NaturalWeapons.Slam_2d6, NaturalWeapons.Tendril_1d6, NaturalWeapons.Tendril_1d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Slam_2d6)),
        ]);

    public static readonly MonsterDef TarantulaTree = T("tarantula_tree", "tarantula tree", 19, ConsoleColor.White,
        speed: ActionCosts.StandardLandMove, size: UnitSize.Gargantuan, unarmed: NaturalWeapons.Slam_2d8,
        components: [
            SimpleDR.Slashing.Lookup(19),
            // TODO: entangle, encage, trample
            new GrantAction(new FullAttack("tarantula", NaturalWeapons.Vine_1d6, NaturalWeapons.Vine_1d6, NaturalWeapons.Vine_1d6, NaturalWeapons.Vine_1d6)),
            new GrantAction(new NaturalAttack(NaturalWeapons.Slam_2d8)),
        ]);

    public static readonly MonsterDef[] All = [
        Sapling, ScytheTree,
        ConchTree, Shambler, Tendriculos, Ironbark, Quickwood,
        Jinmenju, ApprenticeWitchTree, WitchTree, CroneWitchTree,
        Treant, JungleTreant, BonfireTreant,
        GallowsTree, HeartrotTree, TarantulaTree,
    ];
}
