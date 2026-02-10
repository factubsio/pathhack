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
    new("dungeon", "Dungeon", (18, 22)) {
        Levels = [
            new("sanctuary", ["sanctuary_1"], FromBottom, 0, NoBranchEntrance: true),
            new("bigroom", ["bigroom_rect", "bigroom_oval"], FromTop, 12, 4, Required: false),
        ]
    },
    
    // Quest (5 levels)
    new("quest", "Quest", (5, 5)) {
        Parent = "dungeon",
        EntranceDepth = (8, 10),
        Levels = []
    },
    
    // Easy meaty (7 levels)
    new("meaty1", "Meaty 1", (7, 7)) {
        Parent = "dungeon",
        EntranceDepth = (2, 4),
        Levels = []
    },
    
    // Hard meaty (7 levels)
    new("meaty2", "Meaty 2", (7, 7)) {
        Parent = "dungeon",
        EntranceDepth = (11, 13),
        Levels = []
    },
    
    // Hard meaty (7 levels)
    new("meaty3", "Meaty 3", (7, 7)) {
        Parent = "dungeon",
        EntranceDepth = (14, 16),
        Levels = []
    },
    
    // Mini branches (1 level each, underleveled challenge)
    new("mini1", "Mini 1", (1, 1)) {
        Parent = "dungeon",
        EntranceDepth = (4, 16),
        Levels = []
    },
    new("mini2", "Mini 2", (1, 1)) {
        Parent = "dungeon",
        EntranceDepth = (4, 16),
        Levels = []
    },
    new("mini3", "Mini 3", (1, 1)) {
        Parent = "dungeon",
        EntranceDepth = (4, 16),
        Levels = []
    },
];

bool ConsumeFlag(ref string[] args, string flag)
{
    if (!args.Contains(flag)) return false;
    args = args.Where(a => a != flag).ToArray();
    return true;
}

LevelGen.ForceRiver = ConsumeFlag(ref args, "--river");
LevelGen.ForceMiniVault = ConsumeFlag(ref args, "--mivault");

int testDepth = 1;
int depthIdx = Array.FindIndex(args, a => a == "--depth");
if (depthIdx >= 0 && depthIdx + 1 < args.Length && int.TryParse(args[depthIdx + 1], out var depthArg))
{
    testDepth = depthArg;
    args = args.Where((_, i) => i != depthIdx && i != depthIdx + 1).ToArray();
}

if (args.Length > 0 && args[0] == "--test-dungeon")
{
    int startSeed = 0;
    if (args.Length > 1 && int.TryParse(args[1], out var s)) startSeed = s;
    
    LevelGen.TestMode = true;
    LevelGen.QuietLog = true;
    Console.WriteLine($"Testing dungeon generation from seed {startSeed}...");
    for (int i = startSeed; ; i++)
    {
        try
        {
            g.Branches = DungeonResolver.Resolve(templates, i, log: false);
            var dungeon = g.Branches["dungeon"];
            LevelGen.Generate(new LevelId(dungeon, testDepth), i);
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

if (args.Length > 1 && (args[0] == "--gen-dungeon" || args[0] == "--debug-dungeon") && int.TryParse(args[1], out var genSeed))
{
    LevelGen.TestMode = true;
    LevelGen.QuietLog = args[0] == "--gen-dungeon";
    g.Branches = DungeonResolver.Resolve(templates, genSeed, log: false);
    var dungeon = g.Branches["dungeon"];
    LevelGen.Generate(new LevelId(dungeon, testDepth), genSeed);
    Console.WriteLine($"Generated seed {genSeed}, see levelgen_dungeon_1.log");
    return;
}

if (args.Length > 2 && args[0] == "--gen-dungeons" && int.TryParse(args[1], out var startGen) && int.TryParse(args[2], out var endGen))
{
    LevelGen.TestMode = true;
    LevelGen.QuietLog = true;
    using var shared = new StreamWriter("levelgen_all.log") { AutoFlush = true };
    LevelGen.SharedLog = shared;
    for (int i = startGen; i < endGen; i++)
    {
        shared.WriteLine($"=== Seed {i} ===");
        g.Branches = DungeonResolver.Resolve(templates, i, log: false);
        var dungeon = g.Branches["dungeon"];
        LevelGen.Generate(new LevelId(dungeon, testDepth), i);
    }
    LevelGen.SharedLog = null;
    Console.WriteLine($"Generated seeds {startGen}-{endGen - 1}, see levelgen_all.log");
    return;
}

if (args.Length > 0 && args[0] == "--monsters")
{
    MonsterTable.Print(args.Length > 1 ? args[1] : null);
    return;
}

if (args.Length > 0 && args[0] == "--items")
{
    MonsterTable.PrintItems();
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

    if (args.Length >= 2 && args[0] == "--seed" && int.TryParse(args[1], out var forcedSeed))
        g.Seed = forcedSeed;
    else
    {
        byte[] seed = new byte[4];
        Random.Shared.NextBytes(seed);
        g.Seed = BitConverter.ToInt32(seed);
    }

    // g.DebugMode = true;
    // g.Seed = 12345;

    Log.Write($"Game seed: {g.Seed}");
    ItemDb.Reset(g.Seed);
    g.Branches = DungeonResolver.Resolve(templates, g.Seed);
    var dungeon = g.Branches["dungeon"];

    u = Create(creation.Class!, creation.Deity!, creation.Ancestry!);
    u.Initiative = int.MaxValue;
    Input.DoLevelUp(); // Level 1

    u.RecalculateMaxHp();
    u.HP.Current = u.HP.Max;
    Log.Write($"hp => {u.HP.Max}");


    LevelId startId = new(dungeon, 1);
    Level startLevel = LevelGen.Generate(startId, g.Seed);


    // DEBUG: spawn cats for testing
    // foreach (var cat in Cats.All)
    //     if (startLevel.FindLocation(p => startLevel.NoUnit(p) && !startLevel[p].IsStairs) is { } pos)
    //         startLevel.PlaceUnit(Monster.Spawn(cat), pos);

    // DEBUG: spawn orb weavers for testing
    // for (int i = 0; i < 3; i++)
    //     if (startLevel.FindLocation(p => startLevel.NoUnit(p) && !startLevel[p].IsStairs) is { } pos)
    //         startLevel.PlaceUnit(Monster.Spawn(Spiders.OrbWeaver), pos);

    g.Levels[startId] = startLevel;
    g.CurrentLevel = startLevel;
    u.Level = startId;

    // u.XP = 980;
    lvl.PlaceUnit(u, (lvl.BranchUp ?? lvl.StairsUp)!.Value);

    // TEST: lock one L1 slot

    Draw.DrawCurrent();

    GameLoop.Run();

    break; // for now, exit after one game
}

Console.Clear();
Console.WriteLine("bye");
