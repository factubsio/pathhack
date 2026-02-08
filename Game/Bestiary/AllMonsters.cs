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
    ];
}
