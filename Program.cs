using Pathhack.Dat;
using Pathhack.Game.Classes;

DateTime lastCtrlC = DateTime.UnixEpoch;

Console.CancelKeyPress += (o, e) =>
{
    var now = DateTime.Now;
    if ((now - lastCtrlC).TotalMilliseconds > 160)
        e.Cancel = true;
    else
    {
        Console.CursorVisible = true;
        Console.Write("\x1b[0m");
        TtyRec.Stop();
    }
    lastCtrlC = now;
};

List<BranchTemplate> templates = [
    // --- child branches (leaf-first) ---

    new("quest", "Quest", Color: ConsoleColor.Magenta) {
        Linear = [new(Count: 5)],
    },

    new("meaty1", "Meaty 1", Color: ConsoleColor.DarkYellow,
        DefaultAlgorithmPool: [CaveAlgorithm.Worley, CaveAlgorithm.WorleyWarren, CaveAlgorithm.CA]) {
        Linear = [
            new(new LevelTemplate("jungle_shore", Behaviour: ShoreBehaviour.Instance, Variants: ["ss_shore_beached", "ss_shore_debris"])),
            new(new LevelTemplate("deep_jungle", Algorithm: CaveAlgorithm.OutdoorCAOpen)),
            new(Count: 5),
        ],
    },

    new("meaty2", "Meaty 2", Color: ConsoleColor.Red) {
        Linear = [new(Count: 7)],
    },

    new("meaty3", "Meaty 3", Color: ConsoleColor.DarkCyan) {
        Linear = [new(Count: 7)],
    },

    new("mini1", "Mini 1", Color: ConsoleColor.Blue) {
        Linear = [new()],
    },

    new("mini2", "Mini 2", Color: ConsoleColor.Blue) {
        Linear = [new()],
    },

    new("mini3", "Mini 3", Color: ConsoleColor.Blue) {
        Linear = [new()],
    },

    // --- main branch ---

    new("dungeon", "Dungeon", Color: ConsoleColor.Yellow) {
        DepthRange = (18, 22),
        Constraints = [
            new(new LevelTemplate("sanctuary", Variants: ["sanctuary_1"], NoBranchEntrance: true), Depth: (-1, -1)),
            new(new LevelTemplate("bigroom", Variants: ["bigroom_rect", "bigroom_oval"]), Depth: (11, 15), Probability: 0),
            new(BranchId: "quest", Depth: (7, 9)),
            new(BranchId: "meaty1", Depth: (1, 3)),
            new(BranchId: "meaty2", Depth: (10, 12)),
            new(BranchId: "meaty3", Depth: (13, 15)),
            new(BranchId: "mini1", Depth: (3, 15)),
            new(BranchId: "mini2", Depth: (3, 15)),
            new(BranchId: "mini3", Depth: (3, 15)),
        ],
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
bool doSweep = ConsumeFlag(ref args, "--sweep");

MasonryYard.RegisterAll();

int testDepth = 1;
int depthIdx = Array.FindIndex(args, a => a == "--depth");
if (depthIdx >= 0 && depthIdx + 1 < args.Length && int.TryParse(args[depthIdx + 1], out var depthArg))
{
    testDepth = depthArg;
    args = args.Where((_, i) => i != depthIdx && i != depthIdx + 1).ToArray();
}

int algoIdx = Array.FindIndex(args, a => a == "--algo");
if (algoIdx >= 0 && algoIdx + 1 < args.Length && Enum.TryParse<CaveAlgorithm>(args[algoIdx + 1], true, out var algoArg))
{
    LevelGen.ForceAlgorithm = algoArg;
    args = args.Where((_, i) => i != algoIdx && i != algoIdx + 1).ToArray();
}

if (args.Length > 0 && args[0] == "--resolve")
{
    int seed = 0;
    if (args.Length > 1 && int.TryParse(args[1], out var rs)) seed = rs;
    var branches = DungeonResolver.Resolve(templates, seed, log: false);
    foreach (var branch in branches.Values)
    {
        Console.WriteLine($"\n=== {branch.Name} ({branch.MaxDepth} floors, entry={branch.Entry}) ===");
        int i = 0;
        while (i < branch.ResolvedLevels.Count)
        {
            var l = branch.ResolvedLevels[i];
            if (l.TemplateId == null && l.BranchDown == null && l.BranchUp == null)
            {
                int j = i;
                while (j < branch.ResolvedLevels.Count && branch.ResolvedLevels[j].TemplateId == null
                    && branch.ResolvedLevels[j].BranchDown == null && branch.ResolvedLevels[j].BranchUp == null) j++;
                Console.WriteLine($"  {i}{(j - 1 > i ? $"-{j - 1}" : "")}: default ({j - i})");
                i = j;
            }
            else
            {
                List<string> parts = [];
                if (l.TemplateId != null) parts.Add(l.TemplateId);
                if (l.Algorithm != null) parts.Add($"algo:{l.Algorithm}");
                if (l.BranchDown != null) parts.Add($"-> {l.BranchDown}");
                if (l.BranchUp != null) parts.Add($"<- {l.BranchUp}");
                Console.WriteLine($"  {i}: {string.Join(", ", parts)}");
                i++;
            }
        }
    }
    return;
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
        LevelGen.ParamSweep = doSweep ? (i - startGen) / 10 : -1;
        shared.WriteLine($"=== Seed {i} (sweep {LevelGen.ParamSweep}) ===");
        g.Branches = DungeonResolver.Resolve(templates, i, log: false);
        var dungeon = g.Branches["dungeon"];
        LevelGen.Generate(new LevelId(dungeon, testDepth), i);
    }
    LevelGen.SharedLog = null;
    Console.WriteLine($"Generated seeds {startGen}-{endGen - 1}, see levelgen_all.log");
    return;
}

if (args.Length > 1 && args[0] == "--test-family")
{
    string family = args[1];
    var monsters = AllMonsters.All.Where(m => m.Family == family).OrderBy(m => m.BaseLevel).ToArray();
    if (monsters.Length == 0)
    {
        Console.WriteLine($"No monsters with family '{family}'");
        return;
    }
    LevelGen.MenagerieMonsters = monsters;
}

LevelGen.MonitorAttached = ConsumeFlag(ref args, "--debug-server");

if (args.Length > 0 && args[0] == "--monsters")
{
    MonsterTable.Print(args.Length > 1 ? args[1] : null);
    return;
}

if (args.Length > 0 && args[0] == "--items")
{
    MonsterTable.PrintItems(args.Length > 1 ? args[1] : null);
    return;
}

if (args.Length > 0 && args[0] == "--spells")
{
    MonsterTable.PrintSpells(args.Length > 1 ? args[1] : null);
    return;
}

if (args.Length > 0 && args[0] == "--bricks")
{
    MonsterTable.PrintBricks(args.Length > 1 ? args[1] : null);
    return;
}

using var _noCursor = new HideCursor();

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

Draw.Init();
using var _rec = TtyRec.Start("game.rec");
using var _plog = new StreamWriter("pline.log") { AutoFlush = true };

var creation = new CharCreation();
if (LevelGen.MonitorAttached)
{
    creation.Class = ClassDefs.Warpriest;
    creation.Deity = Pantheon.Iomedae;
    creation.Ancestry = Ancestries.Human;
}
else if (!creation.Run()) return;

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

    Log.Write($"Game seed: {g.Seed}");
    Config.Load();
    ItemDb.Reset(g.Seed);
    g.Branches = DungeonResolver.Resolve(templates, g.Seed);
    var dungeon = g.Branches["dungeon"];

    u = Create(creation.Class!, creation.Deity!, creation.Ancestry!);
    if (u.Class.id == "developer")
    {
        g.DebugMode = true;
        Input.InitDebugCommands();
    }

    u.Initiative = int.MaxValue;
    if (LevelGen.MonitorAttached)
        Progression.AutoAdvanceLevel(u);
    else
        Input.DoLevelUp(); // Level 1

    u.RecalculateMaxHp();
    u.HP.Current = u.HP.Max;
    Log.Write($"hp => {u.HP.Max}");

    LevelId startId = new(dungeon, 1);
    dungeon.Discovered = true;
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

    if (LevelGen.MonitorAttached) PHMonitor.Init();

    // MonsterDef MONSTER_UNDER_TEST = 

    // // DEBUG: spawn test dummy near player
    // if (MONSTER_UNDER_TEST != null)
    // {
    //     var dummyPos = upos.Neighbours().FirstOrDefault(p => startLevel.InBounds(p) && startLevel.NoUnit(p) && startLevel[p].IsPassable);
    //     if (dummyPos != default)
    //         startLevel.PlaceUnit(Monster.Spawn(MONSTER_UNDER_TEST, "debug"), dummyPos);
    // }

    Draw.DrawCurrent();

    GameLoop.Run();

    break; // for now, exit after one game
}

Console.Clear();
Console.WriteLine("bye");
