namespace Pathhack.Game.Bestiary;

public static class AllMonsters
{
    public static readonly MonsterDef[] All = [
        .. Derro.All,
        .. Goblins.All,
        .. Kobolds.All,
        .. Gremlins.All,
        .. MiscMonsters.All,
        .. Cats.All,
        .. Snakes.All,
        .. Spiders.All,
        .. Dragons.All,
        .. Trolls.All,
        .. Elementals.All,
        .. Bandits.All,
        .. Boggards.All,
        .. Trees.All,
        .. CharauKa.All,
        .. Mephits.All,
        .. Golems.All,
        .. Ants.All,
    ];

    public static readonly MonsterDef[] ActuallyAll = [
        .. All,
        .. Hippos.All,
        DummyThings.Dummy,
    ];
}
