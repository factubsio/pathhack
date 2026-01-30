using Pathhack.Dat;
using Pathhack.Game;
using Pathhack.Game.Classes;
using Pathhack.Map;
using Pathhack.UI;

if (args.Length > 0)
{
    var field = typeof(TestLevel).GetField(args[0]);
    if (field?.GetValue(null) is SpecialLevel sl)
        LevelGen.ForcedLevel1 = sl;
}

using var _noCursor = new HideCursor();

var creation = new CharCreation();
if (!creation.Run()) return;

while (true)
{
    ResetGameState();

    g.Seed = 12345;
    // g.DebugMode = true;

    int depth = 10;
    Branch branch = new("Main", depth);
    g.Branches[branch.Name] = branch;

    LevelId startId = new(branch, 1);
    Level startLevel = LevelGen.Generate(startId, g.Seed);
    MonsterSpawner.SpawnInitialMonsters(startLevel);
    g.Levels[startId] = startLevel;

    u = Create(creation.Class!, creation.Deity!, creation.Ancestry!);
    u.Level = startId;
    u.Initiative = int.MaxValue;

    Input.DoLevelUp(); // Level 1

    g.CurrentLevel = startLevel;
    u.XP = 980;
    lvl.PlaceUnit(u, (lvl.BranchUp ?? lvl.StairsUp)!.Value);

    Draw.DrawCurrent();

    GameLoop.Run();

    break; // for now, exit after one game
}

Console.Clear();
Console.WriteLine("bye");
