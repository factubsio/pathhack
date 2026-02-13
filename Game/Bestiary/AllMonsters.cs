namespace Pathhack.Game.Bestiary;

public static class AllMonsters
{
    public static readonly MonsterDef[] All = [
        .. Goblins.All,
        .. Kobolds.All,
        .. Gremlins.All,
        .. MiscMonsters.All,
        .. Cats.All,
        .. Snakes.All,
        .. Spiders.All,
        .. Dragons.All,
        .. Trolls.All,
    ];

    public static readonly MonsterDef[] ActuallyAll = [
        .. All,
        .. Hippos.All,
        .. Derro.All,
        DummyThings.Dummy,
    ];
}
