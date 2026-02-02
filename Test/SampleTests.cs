namespace Pathhack.Test;

using Pathhack.Dat;
using Pathhack.Game;
using Pathhack.Map;
using Pathhack.Game.Classes;
using static Pathhack.Map.DepthAnchor;
using static Pathhack.Game.GameState;
using static Pathhack.Test.TestRunner;

public static class SampleTests
{
    static readonly List<BranchTemplate> TestTemplates = [
        new("dungeon", "Test", (1, 1)) {
            Levels = [new("test_room", ["test"], FromTop, 1)]
        }
    ];

    public static void RegisterAll()
    {
        LevelGen.SpecialLevels["test"] = TestLevel.OneRoom;
        Register("player can move", PlayerCanMove);
    }

    static void Setup()
    {
        ResetGameState();
        g.Seed = 12345;
        g.Branches = DungeonResolver.Resolve(TestTemplates, g.Seed);
        var branch = g.Branches["dungeon"];
        LevelId id = new(branch, 1);
        
        Level level = LevelGen.Generate(id, g.Seed);
        g.Levels[id] = level;
        g.CurrentLevel = level;

        u = Create(ClassDefs.Developer, Pantheon.Iomedae, Ancestries.Human);
        u.Level = id;
        u.Initiative = int.MaxValue;

        lvl.PlaceUnit(u, lvl.StairsUp ?? lvl.Rooms[0].RandomInterior());
    }

    static void PlayerCanMove()
    {
        Setup();
        var startPos = u.Pos;

        // Draw.DrawCurrent();
        // g.CurrentRound = -1;
        // g.CurrentRound = 0;

        FeedKeys(ConsoleKey.L, ConsoleKey.L);
        g.DoRound();
        g.DoRound();

        AssertEq(startPos + Pos.E * 2, u.Pos, "player should have moved east");
    }
}
