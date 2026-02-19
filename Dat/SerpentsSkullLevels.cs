namespace Pathhack.Dat;

public static class SerpentsSkullLevels
{
    public static readonly SpecialLevel ShoreBeached = new("ss_shore_beached", """
,,,,,,,,,,,,,,±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±±,,,,,
,,,,,,,,,,,,,,,,±±±±±±±±±±±±±±±±±±±±±±±±±±±±±,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,,,,,,,,,,,,,,,,,,1111111111,,±±±±±±±±,,,,,22222222,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,,,,,,,,,,,,,,,,,,+111111111,,,,,,,±±±222222222222+,,,,,,,,,,,,,,,,,,,,,,
~~~~~~~~~~,,,,,,,,,,,,,,,1111111111,,,,,2+2222222222222222,,,,,,,,,,,,,,,,,,,,,,
~~~~~~~~~~~~,,,,,,,,,,,~~1111111111,,22222222222222222222,,,,,,,,,,,,,,,,,,,,,,,
~~~~~~~~~~~~~~~~~~,,,,~~~1111111111,,2222222222222222222,,,,,,,,,,,,,,,,,,,,,,,,
,,,,~~~~0000000000,,,~~~~1111111111,,,2222222222222222,,,,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,0000000000+00~~~~111111111+,,,,22222222222222,,,,,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,0000000000000000~1111111111~~~~~2222222222,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,+000000000000000~111111111~~~~~~~22222222,,,,,,~~~~~~~~,,,,,,,,,,,,,,,,,
,,,,,,,,0000000000000000~111111111~~~~~~~~~2222~~~~~~~~~~~~~~~~~,,,,,,,,,,,,,,,,
,,,,,,,,000000000000000~~~1111111~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~,,,,,,,,,,,,
,,,,,,~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~,,,,,,,,,
,,,,,~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~,,,,,,,,,
,,,,,~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~,,,,,,,,
,,,,,~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~,,,>,,
,,,,,,,~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~,,,,
,,<,,,,,~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~,,,
,,,,,,,,~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
,,,,,,,,~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
""",



        PostRender: b =>
        {
            b.Level.Outdoors = true;
            b.Context.FloorTile = TileType.Grass;
            b.Level.SpawnFlags |= SpawnFlags.Anywhere;
            b.Level.WallColor = ConsoleColor.DarkYellow;
            foreach (var room in b.Level.Rooms)
                room.Flags |= RoomFlags.Lit;
            b.Stair(b['<'], TileType.BranchUp);
            b.Stair(b['>'], TileType.StairsDown);

            for (int r = 0; r < 3; r++)
            {
                var reason = $"skull/skel_{r}";
                var room = b.Level.Rooms[r];
                for (int i = 0; i < g.RnRange(2, 4); i++)
                {
                    var pos = b.Level.FindLocationInRoom(room, b.Level.NoUnit);
                    if (pos == null) continue;
                    MonsterSpawner.SpawnAndPlace(b.Level,reason, null, true, pos: pos);
                }

                var chestPos = b.Level.FindLocationInRoom(room, _ => true);
                if (chestPos != null)
                {
                    var chest = Item.Create(Containers.Chest);
                    b.Level.PlaceItem(chest, chestPos.Value);
                    var inv = chest.FindFactOfType<ContainerBrick>();

                    for (int i = 0; i < g.RnRange(1, 4); i++)
                    {
                        ContainerBrick.AddItemTo(inv, ItemGen.GenerateRandomItem(b.Level.EffectiveDepth + 2));
                    }

                }
            }
        })
    {
        HasPortalToParent = true,
        HasStairsDown = true,
        SolidRooms = [0, 1, 2],
    };

    public static readonly SpecialLevel ShoreDebris = new("ss_shore_debris", """
,,,,,,,,,,,±±±±±±±±±±±±,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,±±±±,,,,,,,,,,,,,,,,,,,,,
,>,,,,,,,,±±±±±±±±±±±±±,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,±±±±±,,,,,,,,,,,±±±±,,,,,
,,,,,,,,,,±±±±±±±±±±±±,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,±±±±±,,,,,,,,,,±±±±±±±±±,
±±±±±±±,,,±±±±±±±±,,,,,,,,,,,,,,,,,,,,,,,,,,,,,±±±,,,,,,,±±±,,,,,,,,,±±±±±±±±±±,
±±±±±±±±,,,±±±±±±±,,,,,,,,,,,,,,,,,,,,,,,,,,±±±±±±,,,,,,,,,,,,,,,,,,,±±±±±±±±±±,
±±±±±±±±,,,,±±±±±,,,,,,,,,±±±,,,,,,,,,,,,,,,±±±±±±,,,,,,,,,,,,,,,,,,,±±±±±±±±±,,
±±±±±±±±,,,,,,,,,,,,,,,,,,±±±±,,,,,,,,,,,,,,±±±±,,,,,,,,,,,,,,,,,,,,,,,±±±,±±±,,
±±±±±±±±,,,,,,,,,,,,,,,,,±±±±±,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
±±±±±±±,,,,,,,,,,,,,,,,,,±±±±±,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
±±±±±±,,,,,,,,,,,,,,,,,,,±±±,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
±±±±±,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
±±±,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
,,,,,,,,~~~,,,,,,,,,,,,~~~,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,
,,x,,,~~~~~~,,,,,,,,,,~~~~~~,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,<~~
,,,,,~~~~~~~~~~,,,,,,,~~~~~~~~~~~~,,,,,,~~~,,,,,,,,,,,,,,,,,,~~~,,,,,,,,~~~~~~~~
,,,~~~~~~~~~~~~~,,,,,~~~~~~~~~~~~~~~,,,~~~~~~~~~~~~~~~~~~,,,~~~~~~~~~,,~~~~~~~~~
,,~~~~~~~~~~~~~~~,,,~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
""",
        PostRender: b =>
        {
            b.Level.Outdoors = true;
            b.Context.FloorTile = TileType.Grass;
            b.Level.SpawnFlags |= SpawnFlags.Anywhere;
            foreach (var room in b.Level.Rooms)
                room.Flags |= RoomFlags.Lit;
            b.Stair(b['<'], TileType.BranchUp);
            b.Stair(b['>'], TileType.StairsDown);

            var right = b['<'] + Pos.W * 4;
            var left = b['x'];

            HashSet<int> pilesX = [];

            Glyph wreckage = new('≠', ConsoleColor.DarkYellow);
            string[] flotsam = [
                "some flotsam",
                "a broken spar",
                "smashed chests, empty",
                "discarded planks",
                "a rotted figurehead",
                "a tangled net",
                "a barnacle-crusted hull fragment",
                "a snapped mast",
                "waterlogged rope",
                "a shattered lantern",
                "a salt-stained sail",
                "splintered oars",
                "a rusted anchor chain",
                "a cracked ship's wheel",
                "a sodden logbook, illegible",
            ];

            for (int i = 0; i < LevelGen.RnRange(4, 7); i++)
            {
                while (true)
                {
                    int x = LevelGen.RnRange(left.X, right.X);
                    if (pilesX.Contains(x)) continue;
                    int y = left.Y - LevelGen.RnRange(0, 7) + 2;
                    Pos lootPos = new(x, y);
                    if (b.Level[lootPos].Type == TileType.Water) continue;

                    pilesX.Add(x - 1);
                    pilesX.Add(x);
                    pilesX.Add(x + 1);
                    Pos[] n = [..lootPos.Neighbours()];
                    foreach (var p in n.Shuffled().Take(LevelGen.RnRange(2, 4)))
                    {
                        b.Level.GetOrCreateState(p).Feature = new("wreckage", wreckage, flotsam.Pick());
                    }
                    for (int j = 0; j < LevelGen.RnRange(1, 2); j++)
                    {
                        var item = ItemGen.GenerateRandomItem(b.Level.EffectiveDepth + 2);
                        if (item != null)
                            b.Level.PlaceItem(item, lootPos);
                    }
                    break;
                }
            }

            // Joyful and fun beach!
            MonsterSpawner.SpawnAndPlace(b.Level, "zombies!", null, allowTemplate: true);
            MonsterSpawner.SpawnAndPlace(b.Level, "zombies!", null, allowTemplate: true);
            MonsterSpawner.SpawnAndPlace(b.Level, "zombies!", null, allowTemplate: true);
            MonsterSpawner.SpawnAndPlace(b.Level, "zombies!", null, allowTemplate: true);
            MonsterSpawner.SpawnAndPlace(b.Level, "zombies!", null, allowTemplate: true);
            MonsterSpawner.SpawnAndPlace(b.Level, "zombies!", null, allowTemplate: true);
        })
    {
        HasPortalToParent = true,
        HasStairsDown = true,
    };
}

public class ShoreBehaviour : ILevelRuntimeBehaviour
{
    static readonly MonsterDef[][] Families = [Snakes.All, Spiders.All, Cats.All];
    static readonly SkeletonTemplate Skeleton = new();
    static readonly ZombieTemplate Zombie = new();

    [BehaviourId("ss_shore")]
    public static readonly ShoreBehaviour Instance = new();

    public SpawnPick? PickMonster(Level level, int effectiveLevel, string reason)
    {
        var template = PickTemplate(level);
        if (reason.StartsWith("skull/skel"))
        {
            var candidates = AllMonsters.All.Where(m => m.BaseLevel <= effectiveLevel && m.CreatureType == CreatureTypes.Humanoid).ToList();
            var def = MonsterSpawner.PickWeighted(candidates);
            if (def != null)
                return new(def, template);
            else
                return new(Template: template);
        }

        int roll = g.Rn2(10);

        // 30%: family pick + undead
        if (roll < 3)
        {
            var family = Families.Pick();
            var candidates = family.Where(m => m.BaseLevel <= effectiveLevel).ToList();
            var def = MonsterSpawner.PickWeighted(candidates);
            if (def != null) return new(def, template);
        }

        // 20%: undead template only, let global pick the def
        if (roll < 5)
            return new(Template: template);

        // 50%: normal
        return null;
    }

    static MonsterTemplate PickTemplate(Level level)
    {
        var resolved = level.Id.Branch.ResolvedLevels[level.Id.Depth - 1];
        return resolved.Template == SerpentsSkullLevels.ShoreBeached ? Skeleton : Zombie;
    }
}
