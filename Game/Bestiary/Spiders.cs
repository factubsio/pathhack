namespace Pathhack.Game.Bestiary;

public class WebImmunity : LogicBrick
{
    public static readonly WebImmunity Instance = new();
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

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        if (!base.CanExecute(unit, data, target, out whyNot)) return false;
        
        whyNot = "can't see target";
        if (unit is Monster m && !m.CanSeeYou) return false;

        whyNot = "no target";
        if (target.Pos is not { } tgtPos) return false;
        var delta = tgtPos - unit.Pos;
        if (delta == Pos.Zero) return false;

        whyNot = "too far";
        if (unit.Pos.ChebyshevDist(tgtPos) > Range) return false;

        whyNot = "not in line";
        var signed = delta.Signed;
        if (signed.X * delta.Y != signed.Y * delta.X) return false; // not on 8-dir line

        whyNot = "";
        return true;
    }

    protected override void Execute(IUnit unit, Target target)
    {
        Pos dir = (target.Pos!.Value - unit.Pos).Signed;
        Pos pos = unit.Pos;
        Pos last = pos;
        Pos animStart = pos;
        const int range = 6;

        void Animate()
        {
            int frames = last.ChebyshevDist(animStart);
            if (frames > 0)
                Draw.AnimateProjectile(animStart, last, new Glyph('*', ConsoleColor.White), 150 / frames);
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
    public static readonly SpiderVenom DC10 = new(10);
    public static readonly SpiderVenom DC11 = new(11);
    public static readonly SpiderVenom DC12 = new(12);
    public static readonly SpiderVenom DC13 = new(13);
    public static readonly SpiderVenom DC14 = new(14);
    public static readonly SpiderVenom DC15 = new(15);
    public static readonly SpiderVenom DC17 = new(17);

    public override string AfflictionName => "Spider Venom";
    public override int MaxStage => 13;
    public override DiceFormula TickInterval => d(6, 6) + 10;
    public override int? AutoCureMax => 1200;

    protected override void DoPeriodicEffect(IUnit unit, int stage)
    {
        if (stage == 1)
            g.pline($"{unit:The} {VTense(unit, "feel")} woozy from spider venom!");
        
        if (stage >= 5)
        {
            int duration = (stage - 3) / 2;
            unit.AddFact(ParalyzedBuff.Instance.Timed(), duration);
            g.pline($"{unit:The} {VTense(unit, "seize")} up!");
        }
    }

    protected override object? DoQuery(int stage, string key, string? arg) => key switch
    {
        Check.Reflex => new Modifier(ModifierCategory.StatusPenalty, -(stage + 1) / 2, "spider venom"),
        "speed_bonus" when stage >= 3 => new Modifier(ModifierCategory.StatusPenalty, -2 * ((stage - 1) / 2), "spider venom"),
        _ => null
    };
}

public class PhaseShift : LogicBrick
{
    public static readonly PhaseShift Instance = new();
    public override string? PokedexDescription => "50% miss chance (phase shift)";

    protected override void OnBeforeDefendRoll(Fact fact, PHContext ctx)
    {
        if (g.Rn2(100) < 50)
        {
            ctx.Check!.ForcedResult = false;
            g.pline($"{fact.Entity:The} wasn't there!");
        }
    }
}

public static class Spiders
{
    public static readonly MonsterDef OrbWeaver = new()
    {
        id = "orb_weaver",
        Name = "orb weaver",
        Glyph = new('s', ConsoleColor.Yellow),
        HpPerLevel = 5,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d3,
        Size = UnitSize.Small,
        BaseLevel = 1,
        SpawnWeight = 2,
        MinDepth = 1,
        MaxDepth = 6,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Family = "spider",
        GroupSize = GroupSize.SmallMixed,
        Components = [
            WebImmunity.Instance,
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d3)),
            new GrantAction(WebSpit.Instance),
        ],
    };

    public static readonly MonsterDef ScarletSpider = new()
    {
        id = "scarlet_spider",
        Name = "scarlet spider",
        Glyph = new('s', ConsoleColor.Red),
        HpPerLevel = 4,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = -2,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d3,
        Size = UnitSize.Tiny,
        BaseLevel = 0,
        SpawnWeight = 2,
        MinDepth = 1,
        MaxDepth = 4,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Family = "spider",
        Components = [
            WebImmunity.Instance,
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d3)),
            SpiderVenom.DC10.OnHit(),
        ],
    };

    public static readonly MonsterDef GiantCrabSpider = new()
    {
        id = "giant_crab_spider",
        Name = "giant crab spider",
        Glyph = new('s', ConsoleColor.DarkYellow),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = -1,
        DamageBonus = 2,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d4,
        Size = UnitSize.Small,
        BaseLevel = 1,
        SpawnWeight = 2,
        MinDepth = 1,
        MaxDepth = 5,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Family = "spider",
        Components = [
            WebImmunity.Instance,
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d4)),
            new GrantAction(WebSpit.Instance),
        ],
    };

    public static readonly MonsterDef GiantSpider = new()
    {
        id = "giant_spider",
        Name = "giant spider",
        Glyph = new('s', ConsoleColor.Gray),
        HpPerLevel = 8,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d6,
        Size = UnitSize.Medium,
        BaseLevel = 2,
        SpawnWeight = 2,
        MinDepth = 2,
        MaxDepth = 7,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Family = "spider",
        Components = [
            WebImmunity.Instance,
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d6)),
            SpiderVenom.DC11.OnHit(),
        ],
    };

    public static readonly MonsterDef GiantBlackWidow = new()
    {
        id = "giant_black_widow",
        Name = "giant black widow",
        Glyph = new('s', ConsoleColor.DarkRed),
        HpPerLevel = 10,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d6,
        Size = UnitSize.Large,
        BaseLevel = 4,
        SpawnWeight = 2,
        MinDepth = 4,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Family = "spider",
        Components = [
            WebImmunity.Instance,
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d6)),
            new GrantAction(WebSpit.Instance),
            SpiderVenom.DC13.OnHit(),
        ],
    };

    public static readonly MonsterDef PhaseSpider = new()
    {
        id = "phase_spider",
        Name = "phase spider",
        Glyph = new('s', ConsoleColor.Cyan),
        HpPerLevel = 10,
        AC = -1,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d8,
        Size = UnitSize.Large,
        BaseLevel = 5,
        SpawnWeight = 1,
        MinDepth = 6,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Family = "spider",
        Components = [
            WebImmunity.Instance,
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d8)),
            SpiderVenom.DC13.OnHit(),
            PhaseShift.Instance,
        ],
    };

    public static readonly MonsterDef OgreSpider = new()
    {
        id = "ogre_spider",
        Name = "ogre spider",
        Glyph = new('S', ConsoleColor.DarkGray),
        HpPerLevel = 10,
        AC = 1,
        AttackBonus = 0,
        DamageBonus = 2,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_2d6,
        Size = UnitSize.Huge,
        BaseLevel = 6,
        SpawnWeight = 1,
        MinDepth = 7,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Family = "spider",
        Components = [
            WebImmunity.Instance,
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d6)),
            SpiderVenom.DC14.OnHit(),
        ],
    };

    public static readonly MonsterDef GiantTarantula = new()
    {
        id = "giant_tarantula",
        Name = "giant tarantula",
        Glyph = new('S', ConsoleColor.DarkYellow),
        HpPerLevel = 12,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_2d6,
        Size = UnitSize.Gargantuan,
        BaseLevel = 8,
        SpawnWeight = 1,
        MinDepth = 9,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Family = "spider",
        Components = [
            WebImmunity.Instance,
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d6)),
            SpiderVenom.DC15.OnHit(),
        ],
    };

    public static readonly MonsterDef GoliathSpider = new()
    {
        id = "goliath_spider",
        Name = "goliath spider",
        Glyph = new('S', ConsoleColor.Magenta),
        HpPerLevel = 12,
        AC = -1,
        AttackBonus = -2,
        DamageBonus = 0,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Bite_2d10,
        Size = UnitSize.Gargantuan,
        BaseLevel = 11,
        SpawnWeight = 1,
        MinDepth = 12,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Family = "spider",
        Components = [
            WebImmunity.Instance,
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_2d10)),
            SpiderVenom.DC17.OnHit(),
        ],
    };

    public static readonly MonsterDef[] All = [
        OrbWeaver, ScarletSpider, GiantCrabSpider, GiantSpider, GiantBlackWidow,
        PhaseSpider, OgreSpider, GiantTarantula, GoliathSpider,
    ];
}
