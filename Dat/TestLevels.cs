namespace Pathhack.Dat;

public static class TestLevel
{
    public static readonly SpecialLevel OneRoom = new("test", """
        11111111111111111
        1...............1
        1...............1
        1...............1
        1...............1
        1...<.D.>.......1
        1...............1
        1...............1
        1...............1
        1...............1
        11111111111111111
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.BranchUp);
            b.Stair(b['>'], TileType.StairsDown);
            b.Monster(Derro.Strangler, b['D']);
            b.Level.SpawnFlags = SpawnFlags.None;
        });

    public static readonly SpecialLevel Mitflit = new("test_mitflit", """
        1111111111111
        1...........1
        1...........1
        1...<.M.>...1
        1...........1
        1...........1
        1111111111111
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.StairsUp);
            b.Stair(b['>'], TileType.StairsDown);
            b.Monster(Gremlins.Mitflit, b['M']);
            b.Level.SpawnFlags = SpawnFlags.None;
        });

    public static readonly SpecialLevel Pugwampi = new("test_pugwampi", """
        1111111111111
        1...........1
        1...........1
        1...<.M.>...1
        1...........1
        1...........1
        1111111111111
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.StairsUp);
            b.Stair(b['>'], TileType.StairsDown);
            b.Monster(Gremlins.Pugwampi, b['M']);
            b.Level.SpawnFlags = SpawnFlags.None;
        });

    public static readonly SpecialLevel Jinkin = new("test_jinkin", """
        1111111111111
        1...........1
        1...........1
        1...<.M.>...1
        1...........1
        1...........1
        1111111111111
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.StairsUp);
            b.Stair(b['>'], TileType.StairsDown);
            b.Monster(Gremlins.Jinkin, b['M']);
            b.Level.SpawnFlags = SpawnFlags.None;
        });

    public static readonly SpecialLevel Nuglub = new("test_nuglub", """
        1111111111111
        1...........1
        1...........1
        1...<.M.>...1
        1...........1
        1...........1
        1111111111111
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.StairsUp);
            b.Stair(b['>'], TileType.StairsDown);
            b.Monster(Gremlins.Nuglub, b['M']);
            b.Level.SpawnFlags = SpawnFlags.None;
        });

    public static readonly SpecialLevel Grimple = new("test_grimple", """
        1111111111111
        1...........1
        1...........1
        1...<.M.>...1
        1...........1
        1...........1
        1111111111111
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.StairsUp);
            b.Stair(b['>'], TileType.StairsDown);
            b.Monster(Gremlins.Grimple, b['M']);
            b.Level.SpawnFlags = SpawnFlags.None;
        });

    public static readonly SpecialLevel DrunkJinkin = new("test_drunk", """
        1111111111111
        1...........1
        1...........1
        1...<.M.>...1
        1...........1
        1...........1
        1111111111111
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.StairsUp);
            b.Stair(b['>'], TileType.StairsDown);
            foreach (var p in b.Marks('M'))
                b.Monster(Gremlins.VeryDrunkJinkin, p);
            b.Level.SpawnFlags = SpawnFlags.None;
        });

    public static readonly SpecialLevel PitTrap = new("test_pit", """
        1111111111111
        1...........1
        1...........1
        1...<.^M>...1
        1...........1
        1...........1
        1111111111111
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.StairsUp);
            b.Stair(b['>'], TileType.StairsDown);
            b.Level.SpawnFlags = SpawnFlags.None;
            b.Trap(new PitTrap(10), b['^']);
            b.Monster(Goblins.Basic, b['M']);
        });
}
