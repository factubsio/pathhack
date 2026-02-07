using Pathhack.Dat;
using static Pathhack.Map.DepthAnchor;

DateTime lastCtrlC = DateTime.UnixEpoch;

Console.CancelKeyPress += (o, e) =>
{
    var now = DateTime.Now;
    if ((now - lastCtrlC).TotalMilliseconds > 160)
        e.Cancel = true;
    lastCtrlC = now;
};

List<BranchTemplate> templates = [
    new("dungeon", "Dungeon", (6, 9)) {
        Levels = [
            new("sanctuary", ["sanctuary_1"], FromBottom, 0),
            // new("challenge", ["challenge_a", "challenge_b"], RelativeTo, -1, 1, "sanctuary"),
            new("bigroom", ["bigroom_rect", "bigroom_oval"], FromTop, 3, 6, Required: false),
        ]
    },
    new("crypt", "Crypt of the Everflame", (1, 1)) {
        Parent = "dungeon",
        EntranceDepth = (2, 5),
        Levels = [
            new("crypt_end", ["everflame_tomb"], FromBottom, 0),
        ]
    },
    new("trunau", "Trunau Quest", (2, 2)) {
        Parent = "dungeon",
        EntranceDepth = (1, 1),
        Levels = [
            new("trunau_home", ["trunau_home"], FromTop, 1),
            new("trunau_siege", ["trunau_siege"], FromTop, 2),
            // new("trunau_tomb", ["trunau_tomb"], FromTop, 3),
            // new("redlake_outer", ["redlake_outer"], FromTop, 4),
            // new("redlake_inner", ["redlake_inner"], FromBottom, 0),
        ]
    }
];

if (args.Length > 0 && args[0] == "--test-dungeon")
{
    Console.WriteLine("Testing dungeon generation...");
    for (int i = 0; ; i++)
    {
        try
        {
            g.Branches = DungeonResolver.Resolve(templates, i, log: false);
            var dungeon = g.Branches["dungeon"];
            LevelGen.Generate(new LevelId(dungeon, 1), i);
            if (i % 100 == 0) Console.Write($"\r{i} seeds OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nFailed at seed {i}: {ex.Message}");
            break;
        }
    }
    return;
}

if (args.Length > 0 && args[0] == "--monsters")
{
    MonsterTable.Print();
    return;
}

if (args.Length > 0)
{
    var field = typeof(TestLevel).GetField(args[0]);
    if (field?.GetValue(null) is SpecialLevel sl)
        LevelGen.ForcedLevel1 = sl;
}
else
{
    // bubble dbeug
    // LevelGen.ForcedLevel1 = TestLevel.OneRoom;
}

using var _noCursor = new HideCursor();

using var _rec = TtyRec.Start("game.rec");
using var _plog = new StreamWriter("pline.log") { AutoFlush = true };

// SampleTests.RegisterAll();
// TestRunner.Run();
// return;

var creation = new CharCreation();
if (!creation.Run()) return;

// Log.EnabledTags.Add("movement");

while (true)
{
    ResetGameState();
    g.PlineLog = _plog;

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

    // DEBUG: spawn cats for testing
    // foreach (var cat in Cats.All)
    //     if (startLevel.FindLocation(p => startLevel.NoUnit(p) && !startLevel[p].IsStairs) is { } pos)
    //         startLevel.PlaceUnit(Monster.Spawn(cat), pos);

    // DEBUG: spawn snakes for testing
    foreach (var snake in Snakes.All)
        if (startLevel.FindLocation(p => startLevel.NoUnit(p) && !startLevel[p].IsStairs) is { } pos)
            startLevel.PlaceUnit(Monster.Spawn(snake), pos);

    g.Levels[startId] = startLevel;

    u = Create(creation.Class!, creation.Deity!, creation.Ancestry!);
    u.Level = startId;
    u.Initiative = int.MaxValue;

    Input.DoLevelUp(); // Level 1

    g.CurrentLevel = startLevel;

    u.RecalculateMaxHp();
    u.HP.Current = u.HP.Max;
    Log.Write($"hp => {u.HP.Max}");

    u.XP = 980;
    lvl.PlaceUnit(u, (lvl.BranchUp ?? lvl.StairsUp)!.Value);

    Draw.DrawCurrent();

    GameLoop.Run();

    break; // for now, exit after one game
}

Console.Clear();
Console.WriteLine("bye");
