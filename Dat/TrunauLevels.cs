namespace Pathhack.Dat;

public class SiegeEngineTracker(List<Pos> stairs) : LogicBrick
{
    public override string Id => "_siege_engine";
    int _remaining = stairs.Count;

    protected override void OnDeath(Fact fact, PHContext ctx)
    {
        _remaining--;
        if (_remaining > 0) return;

        Pos? best = null;
        int bestDist = int.MaxValue;
        foreach (var p in stairs)
        {
            int dist = p.ChebyshevDist(upos);
            if (dist < bestDist) { best = p; bestDist = dist; }
        }
        if (best is null) return;

        lvl.Set(best.Value, TileType.StairsDown);
        g.pline("The way forward opens!");
    }
}

public class SpawnOnDamage(MonsterDef[] pool, int chance = 50) : LogicBrick
{
    public override string Id => "_spawn_on_damage";
    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (g.Rn2(100) >= chance) return;
        var def = MonsterSpawner.PickWeighted(pool);
        if (def == null) return;
        var ownerPos = ctx.Target.Unit!.Pos;
        g.Defer(() =>
        {
            for (int tries = 0; tries < 10; tries++)
            {
                var pos = ownerPos + Pos.AllDirs.Pick();
                if (lvl.InBounds(pos) && lvl.NoUnit(pos) && lvl[pos].IsPassable)
                {
                    var mon = Monster.Spawn(def, "orc defending catapault");
                    lvl.PlaceUnit(mon, pos);
                    g.pline($"{mon:An} rushes to defend!");
                    break;
                }
            }
        });
    }
}

public static class TrunauLevels
{
    static readonly MonsterDef Villager = new()
    {
        Name = "villager",
        Family = "human",
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('@', ConsoleColor.White),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        Unarmed = NaturalWeapons.Fist,
        SpawnWeight = 0,
        Peaceful = true,
        MoralAxis = MoralAxis.Good,
        EthicalAxis = EthicalAxis.Neutral,
        OnChat = _ =>
        {
            g.pline(g.Rn2(3) switch
            {
                0 => "Talk to Halgra, she knows what to do.",
                1 => $"Orcs at the gate? We put our faith in {u.Deity.Name}!",
                2 => "Could be worse, no Uruk Hai...",
                _ => "I want to get off mr bubbles's wild ride.",
            });
        },
    };

    static List<Pos> _exitPath = [];

    static readonly MonsterDef QuestGiver = new()
    {
        Name = "Chief Defender Halgra",
        Family = "human",
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('@', ConsoleColor.Yellow),
        HpPerLevel = 10,
        AC = 2,
        AttackBonus = 0,
        Unarmed = NaturalWeapons.Fist,
        SpawnWeight = 0,
        Peaceful = true,
        IsUnique = true,
        Stationary = true,
        MoralAxis = MoralAxis.Good,
        EthicalAxis = EthicalAxis.Lawful,
        OnChat = _ => {
            LoreDump("""
[fg=yellow]Chief Defender Halgra[/fg] looks up from her maps.

"You've come at a dark hour. Orc scouts have been spotted massing in the hills - far more than usual. Something's driving them."

She taps a worn parchment. "There's an ancient tomb beneath Trunau. Most folk have forgotten it exists. But the orcs haven't. They're after something down there - a relic from the old wars."

"I need you on the walls when the attack comes. After we break their assault, you'll descend into the tomb and retrieve that relic before they do."

"Trunau's survival depends on it."
""");
            if (_exitPath.Count >= 1)
            {
                var mid = _exitPath[0];
                lvl.PlaceDoor(mid + Pos.S, DoorState.Open);
                lvl.Set(mid + Pos.S * 2, TileType.Corridor);
                lvl.Set(mid + Pos.S * 3, TileType.Corridor);
                lvl.PlaceDoor(mid + Pos.S * 4, DoorState.Open);
                g.pline("Halgra gestures toward the Southern passage.");
                _exitPath.Clear();
            }
        },
    };

    public static readonly SpecialLevel Home = new("trunau_home", """
,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,,,,,,,,,,,,,,,,,,P,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,2222222222222,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,2.....P.....2,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,,,,,,,,11111111111111,,,,,,,,,,,,,,,2...........2,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,,,,,,,,1......P.....1,,,,,,,,,,,,,,,2...........2,,,,,3333333333333,,,,,
,,,,,,,,,,,,,,,1............1,,,,,,,,,,,,,,,2...........2,,,,,3...........3,,,,,
,,,,,,,,,,,,,,,1............1,,,,,,,,,P,,,,,2...........2,,,,,3...........3,,,,,
,,,,,,,,,,,,,,,1............1,,,,,,,,,,,,,,,222222+222222,,,,,3...........3,,,,,
,,,,,,,,,,,,,,,1111111111+111,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,+.....Q.....3,,,,,
,,,,,,,,,P,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,3...........3,,,,,
,,,,,,<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,3...........3,,,,,
,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,3........._.3,,,,,
,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,555+55555555,,,,,,,,,,,,,,,3333333333333,,,,,
,,,,,,,,,,,,,6666666+66666,,,,,,,,,5....P.....5,,,,,,,,,,,,,,,,,,,,,P,,|||,,,,,,
,,,,,,,,,,,,,6...........6,,,,,,,,,5..........5,,,,,,,,,,,,,,,,,,,,,,,,|||,,,,,,
,,,,,,,,,,,,,6.....P.....6,,,,,,,,,5..........5,,,,,,,,,,P,,,,,,,,,,4444444444,,
,,,,,,,,,,,,,6...........6,,,,,,,,,555555555555,,,,,,,,,,,,,,,,,,,,,4......>.4,,
,,,,,,,,,,,,,6...........6,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,4........4,,
,,,,,,,,,,,,,6666666666666,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,4444444444,,
,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
""",
        PostRender: b =>
        {
            b.Level.Outdoors = true;
            b.Level.NoInitialSpawns = true;
            foreach (var room in b.Level.Rooms)
                room.Flags |= RoomFlags.Lit;
            b.Stair(b['<'], TileType.BranchUp);
            b.Stair(b['>'], TileType.StairsDown);
            
            foreach (var p in b.Marks('P'))
                b.Level.PlaceUnit(Monster.Spawn(Villager, "quest/villager"), p);
            
            b.Monster(QuestGiver, b['Q']);
            _exitPath = b.Marks('_');
        })
    {
        HasPortalToParent = true,
        HasStairsDown = true,
    };

    public static readonly SpecialLevel Siege = new("trunau_siege", """
        ..................................||............................................
        ..................................||..........................................>.
        ..................................||....A.................................S.....
        ..................................||............................................
        ..................................||............................................
        ..................................||............................................
        ..................................||............................................
        ..................................||............................................
        ..................................||............................................
        ................................................................................
        ..<..............................................................................
        ................................................................................
        ..................................||............................................
        ..................................||............................................
        ..................................||............................................
        ..................................||............................................
        ..................................||............................................
        ..................................||............................................
        ..................................||...............................A......B.....
        ..................................||..........................................>.
        ..................................||............................................
        """,
        PostRender: b =>
        {
            b.Level.NoInitialSpawns = true;
            b.Stair(b['<'], TileType.StairsUp);
            
            // spawn orcs to the right of the wall
            var spawnBounds = b.Marks('A');
            MonsterDef[] siegeOrcs = [Orcs.OrcScrapper, Orcs.OrcVeteran, Orcs.OrcCommander];
            int orcCount = LevelGen.RnRange(4, 8);
            for (int i = 0; i < orcCount; i++)
            {
                var def = MonsterSpawner.PickWeighted(siegeOrcs)!;
                for (int tries = 0; tries < 10; tries++)
                {
                    var pos = new Pos(g.RnRange(spawnBounds[0].X, spawnBounds[1].X), g.RnRange(spawnBounds[0].Y, spawnBounds[1].Y));
                    if (b.Level.NoUnit(pos))
                    {
                        b.Monster(def, pos);
                        break;
                    }
                }
            }
            var stairs = b.Marks('>');
            for (int i = 0; i < stairs.Count; i++)
                b.Level.Set(stairs[i], TileType.Rock);
            
            var tracker = new SiegeEngineTracker(stairs);
            var spawner = new SpawnOnDamage(siegeOrcs, 45);
            MonsterDef catapult = new()
            {
                id = "catapult",
                Name = "catapult",
                Family = "construct",
                CreatureType = CreatureTypes.Construct,
                Glyph = new('0', ConsoleColor.DarkYellow),
                HpPerLevel = 12,
                AC = -5,
                AttackBonus = 0,
                DamageBonus = 0,
                LandMove = 0,
                Unarmed = NaturalWeapons.Fist,
                Size = UnitSize.Huge,
                SpawnWeight = 0,
                MoralAxis = MoralAxis.Neutral,
                EthicalAxis = EthicalAxis.Neutral,
                Components = [tracker, spawner],
                IsUnique = true,
            };
            b.Monster(catapult, b['S']);
            b.Monster(catapult, b['B']);
        })
    {
        HasStairsUp = true,
        HasStairsDown = true,
    };

    public static readonly SpecialLevel RedlakeInner = new("redlake_inner", """
        #######################
        #.....................#
        #.....................#
        #.....................#
        #.....................#
        #..........B..........#
        #.....................#
        #.....................#
        #.....................#
        #.....................#
        #..........<..........#
        #######################
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.StairsUp);
            // TODO: place Grenseldek at B
        })
    {
        HasStairsUp = true,
    };

    public static readonly SpecialLevel Tomb = new("trunau_tomb", "",
        PostRender: b =>
        {
            // TODO: skeletons, spiders, Skreed
        });

    public static readonly SpecialLevel FortOuter = new("redlake_outer", "",
        PostRender: b =>
        {
            // TODO: orcs, trolls
        });
}
