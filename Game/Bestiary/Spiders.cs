namespace Pathhack.Game.Bestiary;

public class WebImmunity : LogicBrick
{
    public static readonly WebImmunity Instance = new();
    public override string Id => "spider:web_immune";
    public override string? PokedexDescription => "Immune to webs";

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "web_immunity" ? true : null;

    protected override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (context.Check?.Tag == "web" && context.IsCheckingOwnerOf(fact))
            context.Check.ForceSuccess();
    }
}

public class WebSpit(int cooldown = 120) : CooldownAction("spit web", TargetingType.Direction, _ => cooldown)
{
    public static readonly WebSpit Instance = new();
    const int Range = 6;

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var basePlan = base.CanExecute(unit, data, target);
        if (!basePlan) return basePlan;

        if (unit is Monster m && !m.CanSeeYou) return new(false, "can't see target");

        if (target.Pos is not { } tgtPos) return new(false, "no target");
        var delta = tgtPos - unit.Pos;
        if (delta == Pos.Zero) return new(false, "no target");

        if (unit.Pos.ChebyshevDist(tgtPos) > Range) return new(false, "too far");

        var signed = delta.Signed;
        if (signed.X * delta.Y != signed.Y * delta.X) return new(false, "not in line");

        return true;
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        Pos dir = (target.Pos!.Value - unit.Pos).Signed;
        Pos pos = unit.Pos;
        Pos last = pos;
        Pos animStart = pos;
        const int range = 6;

        void Animate()
        {
            Draw.AnimateProjectile(animStart, last, new Glyph('¤', ConsoleColor.White));
            animStart = last;
        }

        for (int i = 0; i < range; i++)
        {
            pos += dir;
            if (!lvl.InBounds(pos) || !lvl.CanMoveTo(last, pos, null)) break;
            last = pos;

            var hit = lvl.UnitAt(pos);
            if (hit == null) continue;

            Animate();

            if (hit.Has("web_immunity")) continue;

            int dc = unit.GetSpellDC() - 2;
            using var ctx = PHContext.Create(unit, Target.From(hit));
            if (CheckReflex(ctx, dc, "web"))
            {
                g.YouObserve(hit, $"{hit:The} {VTense(hit, "dodge")} the web!");
                continue;
            }

            g.YouObserve(hit, $"{hit:The} {VTense(hit, "get")} caught in a web!");
            var trap = new WebTrap(lvl.Depth) { PlayerSeen = true };
            hit.TrappedIn = trap;
            hit.EscapeAttempts = 0;
            lvl.Traps[hit.Pos] = trap;
            return;
        }

        Animate();

        // land on ground
        if (lvl.Traps.TryGetValue(last, out var existing) && existing.Type == TrapType.Pit)
        {
            g.YouObserve(last, $"A ball of webbing harmlessly falls into a {existing.Type.ToString().ToLower()}.");
            return;
        }
        var groundTrap = new WebTrap(lvl.Depth) { PlayerSeen = true };
        g.YouObserve(last, "A web splats on the ground.");
        lvl.Traps[last] = groundTrap;
    }
}

// TODO: Dream Spider (hallu venom, needs hallu system)
// TODO: Giant Tarantula hair barrage (cone attack)

public class SpiderVenom(int dc) : AfflictionBrick(dc, "poison")
{
    public override string Id => $"spider:venom+{DC}";
    public override AbilityTags Tags => AbilityTags.Biological;
    public static readonly SpiderVenom DC10 = new(10);
    public static readonly SpiderVenom DC11 = new(11);
    public static readonly SpiderVenom DC12 = new(12);
    public static readonly SpiderVenom DC13 = new(13);
    public static readonly SpiderVenom DC14 = new(14);
    public static readonly SpiderVenom DC15 = new(15);
    public static readonly SpiderVenom DC17 = new(17);

    public static readonly SpiderVenom DC100 = new(100); // for testing

    public override string AfflictionName => "Spider Venom";
    public override int MaxStage => 13;
    public override DiceFormula TickInterval => d(6, 6) + 10;
    public override int? AutoCureMax => 1200;

    protected override void DoPeriodicEffect(Fact fact, IUnit unit, int stage)
    {
        if (stage == 1 && unit.IsPlayer) //FIXME YouObserveSelf?
            g.pline($"{unit:The} {VTense(unit, "feel")} woozy from spider venom!");

        if (stage >= 5)
        {
            int duration = (stage - 3) / 2;
            unit.AddFact(ParalyzedBuff.Instance.Timed(), null, duration);
            g.YouObserve(unit, $"{unit:The} {VTense(unit, "seize")} up!");
        }
    }

    protected override object? DoQuery(int stage, string key, string? arg) => key switch
    {
        Check.Reflex => new Modifier(ModifierCategory.StatusPenalty, -(stage + 1) / 2, "spider venom"),
        CommonQueries.SpeedModifiersFlat when stage >= 3 => new Modifier(ModifierCategory.StatusPenalty, -2 * ((stage - 1) / 2), "spider venom"),
        _ => null
    };
}

public class PhaseShift : LogicBrick
{
    public static readonly PhaseShift Instance = new();
    public override string Id => "spider:phase_shift";
    public override string? PokedexDescription => "50% miss chance (phase shift)";

    protected override void OnBeforeDefendRoll(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not IUnit x) return;

        if (g.Rn2(100) < 50)
        {
            ctx.Check!.ForcedResult = false;
            g.YouObserve(x, $"{x:The} wasn't there!");
        }
    }
}

public static class Spiders
{
    public static readonly MonsterFamily Family = new("spider");

    static MonsterDef S(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 8, int ac = 0, int ab = 0, int dmg = 0,
        UnitSize size = UnitSize.Medium, int maxDepth = 99,
        GroupSize group = GroupSize.None, char glyph = 's',
        WeaponDef? unarmed = null, ActionCost? speed = null)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = Family,
            CreatureType = CreatureTypes.Beast,
            Glyph = new(glyph, color),
            HpPerLevel = hp,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            LandMove = speed ?? ActionCosts.StandardLandMove,
            Unarmed = unarmed ?? NaturalWeapons.Bite_1d6,
            Size = size,
            BaseLevel = level,
            MaxDepth = maxDepth,
            GroupSize = group,
            StartingRot = Foods.RotSpoiled,
            MoralAxis = MoralAxis.Neutral,
            EthicalAxis = EthicalAxis.Neutral,
            Components = [WebImmunity.Instance, .. components],
        };
    }

    public static readonly MonsterDef OrbWeaver = S("orb_weaver", "orb weaver", 1, ConsoleColor.Yellow,
        [new GrantAction(WebSpit.Instance), new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d3))],
        hp: 5, size: UnitSize.Small, maxDepth: 6, group: GroupSize.SmallMixed,
        unarmed: NaturalWeapons.Bite_1d3);

    public static readonly MonsterDef ScarletSpider = S("scarlet_spider", "scarlet spider", 0, ConsoleColor.Red,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d3)), SpiderVenom.DC10.OnHit()],
        hp: 4, ac: 1, ab: 1, dmg: -2, size: UnitSize.Tiny, maxDepth: 4,
        unarmed: NaturalWeapons.Bite_1d3);

    public static readonly MonsterDef GiantCrabSpider = S("giant_crab_spider", "giant crab spider", 1, ConsoleColor.DarkYellow,
        [new GrantAction(WebSpit.Instance), new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d4))],
        hp: 6, ab: -1, dmg: 2, size: UnitSize.Small, maxDepth: 5,
        unarmed: NaturalWeapons.Bite_1d4);

    public static readonly MonsterDef GiantSpider = S("giant_spider", "giant spider", 2, ConsoleColor.Gray,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d6)), SpiderVenom.DC11.OnHit()],
        maxDepth: 7);

    public static readonly MonsterDef GiantBlackWidow = S("giant_black_widow", "giant black widow", 4, ConsoleColor.DarkRed,
        [new GrantAction(WebSpit.Instance), new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d6)), SpiderVenom.DC13.OnHit()],
        size: UnitSize.Large);

    public static readonly MonsterDef PhaseSpider = S("phase_spider", "phase spider", 5, ConsoleColor.Cyan,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d8)), SpiderVenom.DC13.OnHit(), PhaseShift.Instance],
        ac: -1, size: UnitSize.Large, unarmed: NaturalWeapons.Bite_1d8);

    public static readonly MonsterDef OgreSpider = S("ogre_spider", "ogre spider", 6, ConsoleColor.DarkGray,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d6)), SpiderVenom.DC13.OnHit()],
        ac: 1, dmg: 1, size: UnitSize.Huge, glyph: 'S', unarmed: NaturalWeapons.Bite_2d6);

    public static readonly MonsterDef GiantTarantula = S("giant_tarantula", "giant tarantula", 8, ConsoleColor.DarkYellow,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d6)), SpiderVenom.DC14.OnHit()],
        ac: 1, size: UnitSize.Gargantuan, glyph: 'S', unarmed: NaturalWeapons.Bite_2d6);

    public static readonly MonsterDef GoliathSpider = S("goliath_spider", "goliath spider", 11, ConsoleColor.Magenta,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d10)), SpiderVenom.DC17.OnHit()],
        ac: -1, ab: -2, size: UnitSize.Gargantuan, glyph: 'S',
        speed: ActionCosts.LandMove25, unarmed: NaturalWeapons.Bite_2d10);

    public static readonly MonsterDef[] All = [
        OrbWeaver, ScarletSpider, GiantCrabSpider, GiantSpider, GiantBlackWidow,
        PhaseSpider, OgreSpider, GiantTarantula, GoliathSpider,
    ];
}
