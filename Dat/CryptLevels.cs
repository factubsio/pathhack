namespace Pathhack.Dat;

public static class CryptLevels
{
    public static readonly SpecialLevel EverflameEnd = new("everflame_tomb", """
        11111     222222222     333333333 44444
        1...1     2.......2     3.......3 4...4
        1.<.+#####+...S...+#####+...S...+#+.B.4
        1...1     2.......2     3.......3 4...4
        11111     222222222     333333333 44444
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.BranchUp);
            b.Monster(MiscMonsters.Asar, b['B']);
            foreach (var p in b.Marks('S'))
                b.Monster(MiscMonsters.Skeleton, p);
            b.Level.NoInitialSpawns = true;
        })
    {
        HasPortalToParent = true,
    };
}
