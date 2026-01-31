using System.Runtime.Intrinsics.Arm;
using Pathhack.Dat;
using static Pathhack.Map.DepthAnchor;

List<BranchTemplate> templates = [
    new("dungeon", "Dungeon", (3, 3)) {
        Levels = [
            new("sanctuary", ["sanctuary_1"], FromBottom, 0),
            // new("challenge", ["challenge_a", "challenge_b"], RelativeTo, -1, 1, "sanctuary"),
            new("bigroom", ["bigroom_rect", "bigroom_oval"], FromTop, 1, 1, Required: false),
        ]
    },
    new("crypt", "Crypt of the Everflame", (1, 1)) {
        Parent = "dungeon",
        EntranceDepth = (1, 2),
        Levels = [
            new("crypt_end", ["everflame_tomb"], FromBottom, 0),
        ]
    }
];

if (args.Length > 0)
{
    var field = typeof(TestLevel).GetField(args[0]);
    if (field?.GetValue(null) is SpecialLevel sl)
        LevelGen.ForcedLevel1 = sl;
}
else
{
    // bubble dbeug
    // LevelGen.ForcedLevel1 = EndShrineLevels.EndShrine1;
}

using var _noCursor = new HideCursor();

var creation = new CharCreation();
if (!creation.Run()) return;

// Log.EnabledTags.Add("movement");

while (true)
{
    ResetGameState();

    byte[] seed = new byte[4];
    Random.Shared.NextBytes(seed);

    g.Seed = BitConverter.ToInt32(seed);

    // g.DebugMode = true;
    // g.Seed = 12345;

    g.Branches = DungeonResolver.Resolve(templates, g.Seed);
    var dungeon = g.Branches["dungeon"];

    LevelId startId = new(dungeon, 1);
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
