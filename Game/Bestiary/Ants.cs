namespace Pathhack.Game.Bestiary;

public static class Ants
{
    public static readonly MonsterFamily Family = new("ant");

    static MonsterDef A(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 8, int ac = 0, int ab = 0, int dmg = 0,
        UnitSize size = UnitSize.Medium, int maxDepth = 99,
        GroupSize group = GroupSize.Small,
        WeaponDef? unarmed = null, ActionCost? speed = null,
        bool isHider = false, string? revealMessage = null, string? revealSound = null)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = Family,
            CreatureType = CreatureTypes.Beast,
            Subtypes = [],
            Glyph = new('a', color),
            HpPerLevel = hp,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            LandMove = speed ?? ActionCosts.ardLandMove45,
            Unarmed = unarmed ?? NaturalWeapons.Bite_1d4,
            Size = size,
            BaseLevel = level,
            MaxDepth = maxDepth,
            GroupSize = group,
            MoralAxis = MoralAxis.Neutral,
            EthicalAxis = EthicalAxis.Neutral,
            IsHider = isHider,
            RevealMessage = revealMessage,
            RevealSound = revealSound,
            Components = components,
        };
    }

    public static readonly MonsterDef Worker = A("worker_ant", "worker ant", 1, ConsoleColor.Gray,
        [new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d3))],
        hp: 5, size: UnitSize.Small, group: GroupSize.LargeMixed,
        unarmed: NaturalWeapons.Bite_1d3);

    public static readonly MonsterDef GiantAnt = A("giant_ant", "giant ant", 2, ConsoleColor.DarkYellow,
        [
            new GrantAction(new FullAttack("ant", NaturalWeapons.Bite_1d4, NaturalWeapons.Sting_1d4.WithRider(MeleeDamageRider.Acid_1d4))),
            EnergyResist.Acid.DR5,
        ], speed: ActionCosts.LandMove25);

    public static readonly MonsterDef FireAnt = A("fire_ant", "fire ant", 3, ConsoleColor.Red,
        [
            new GrantAction(new FullAttack("fire_ant", NaturalWeapons.Bite_1d4, NaturalWeapons.Sting_1d4.WithRider(MeleeDamageRider.Fire_1d4))),
            EnergyResist.Fire.DR5
         ]);

    public static readonly MonsterDef KnightAnt = A("knight_ant", "knight ant", 4, ConsoleColor.Blue,
        [
            new GrantAction(new FullAttack("knight_ant", NaturalWeapons.Bite_1d6, NaturalWeapons.Sting_1d6)),
            new QueryBrick("tremorsense", 4),
            KnightAntFormation.Instance,
            SimpleDR.Universal.DR5,
        ],
        size: UnitSize.Large, ac: 1);

    public static readonly MonsterDef AntLion = A("ant_lion", "ant lion", 5, ConsoleColor.DarkMagenta,
        [
            new GrantAction(new FullAttack("ant_lion", NaturalWeapons.Bite_2d6, NaturalWeapons.Claw_1d2, NaturalWeapons.Claw_1d2)),
            new QueryBrick("tremorsense", 8)
        ],
        group: GroupSize.None, size: UnitSize.Large,
        isHider: true, revealMessage: "erupts from the ground!", revealSound: "the ground cracking open",
        unarmed: NaturalWeapons.Bite_2d6);

    public static readonly MonsterDef MegaponAnt = A("megapon_ant", "megapon ant", 6, ConsoleColor.DarkGray,
        [
            new GrantAction(new FullAttack("megapon", NaturalWeapons.Bite_2d6, NaturalWeapons.Sting_1d6)),
            GrabOnHit.Instance,
            Constrict.Medium,
            SimpleDR.Universal.DR5,
        ],
        size: UnitSize.Huge, ac: 1, dmg: 1, speed: ActionCosts.LandMove15, group: GroupSize.None);

    public static readonly MonsterDef AntQueen = A("ant_queen", "ant queen", 9, ConsoleColor.Magenta,
        [
            new GrantAction(new FullAttack("queen",
                NaturalWeapons.Bite_2d6,
                NaturalWeapons.Sting_1d6.WithRider(MeleeDamageRider.Acid_1d4),
                NaturalWeapons.Sting_1d6.WithRider(MeleeDamageRider.Fire_1d4))),
            AntSpawner.Instance,
            SimpleDR.Universal.DR10,
            EnergyResist.Fire.DR10,
            EnergyResist.Acid.DR10,
        ],
        hp: 8, size: UnitSize.Large, group: GroupSize.None,
        speed: ActionCosts.LandMove10, ac: 2);

    public static readonly MonsterDef Egg = A("ant_egg", "ant egg", 2, ConsoleColor.Green, [
        AntEgg.Instance,
    ], hp: 2, speed: 0, group: GroupSize.None);

    public static readonly MonsterDef[] All = [
        Worker, GiantAnt, FireAnt, KnightAnt, MegaponAnt, AntLion, AntQueen,
    ];

    public static readonly MonsterDef[] AllHatchable = [
        Worker, GiantAnt, FireAnt, KnightAnt, MegaponAnt, AntLion,
    ];
}

public class AntEgg : LogicBrick<ScalarData<int>>
{
    public override string Id => "ant:egg";

    public static readonly AntEgg Instance = new();

    public override bool IsActive => true;

    public override string? PokedexDescription => "If left unmolested will spawn a fresh new ant.";

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not Monster unit) return;

        // Ignore Cannotact, an egg is an egg

        int val = X(fact).Value++;
        var at = unit.Pos;

        if (val > 5 && g.Rn2(3) == 0)
        {
            DoRemoveFromPlay(unit);

            g.Defer(() =>
            {
                if (!lvl.NoUnit(at))
                {
                    g.YouObserve(at, "The egg splits open, but nothing emerges.", "a cracking sound");
                    return;
                }

                g.YouObserve(at, "The egg splits open, something emerges!", "a cracking sound, even MORE legs");
                var def = MonsterSpawner.PickWeighted(Ants.AllHatchable);
                MonsterSpawner.SpawnAndPlace(lvl, "ant:egg_hatch", def, false, at, true);
            });
        }
    }
}

public class AntSpawner : LogicBrick<ScalarData<int>>
{
    public override string Id => "ant:spawner";
    public override bool IsActive => true;
    public override string? PokedexDescription => "Periodically lays eggs, that may hatch into MORE ants.";
    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not Monster unit) return;
        if (unit.CannotAct) return;

        if (!lvl.HasLOS(unit.Pos) || unit.Pos.ChebyshevDist(upos) > 10) return;

        var dat = X(fact);
        dat.Value++;
        if (g.Rn2(dat) < 9) return;
        X(fact).Value = 0;

        g.Defer(() =>
        {
            if (unit.IsDead) return;

            int count = g.RnRange(1, 3);
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                if (!lvl.RandomFreeAdjacent(unit.Pos, out var spawnPos)) continue;
                MonsterSpawner.SpawnAndPlace(lvl, "ant:lay", Ants.Egg, false, spawnPos, true);
                spawned++;
            }

            string msg = spawned switch
            {
                0 => "grunts, strains, but produces nothing... try more fibre?",
                1 => "lays an egg",
                _ => $"lays {count} eggs",
            };

            g.YouObserve(unit, $"{unit:The} {msg}", "a slopping sound");
        });
    }

    public static readonly AntSpawner Instance = new();
}

public class KnightAntFormation : LogicBrick
{
    public override string Id => "ant:knight_formation";
    public override bool IsActive => true;
    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not Monster unit) return;
        if (unit.CannotAct) return;

        foreach (var mon in lvl.AdjacentUnits(unit.Pos).OfType<Monster>().Where(m => m.Def.Family == unit.Def.Family))
            mon.AddFact(FormationBuff.Instance, unit, 2);
    }

    class FormationBuff : LogicBrick
    {
        public override string Id => "ant:formation_buff";
        public override StackMode StackMode => StackMode.ExtendDuration;
        public override bool IsBuff => true;
        public override string? BuffName => Id;

        public static readonly FormationBuff Instance = new();

        protected override void OnBeforeDefendRoll(Fact fact, PHContext context) => context.Check!.Advantage++;
    }

    public static readonly KnightAntFormation Instance = new();
}