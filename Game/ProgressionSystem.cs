using Pathhack.UI;

namespace Pathhack.Game;

public enum AvailabilityState { Now, Later, Never }

public readonly record struct Availability(AvailabilityState State, string Reason = "")
{
    public static readonly Availability Now = new(AvailabilityState.Now);
    public static readonly Availability Never = new(AvailabilityState.Never);
    public static Availability Later(string reason) => new(AvailabilityState.Later, reason);
}

public class FeatDef : BaseDef, ISelectable
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public FeatType Type;
    public int Level = 1;
    public Func<Player, Availability>? Prereq;
    public Func<Player, string?>? NotAvailableBecause;
}

public class FeatSelection
{
    public required string Label;
    public required IEnumerable<FeatDef> Options;
    public int Count = 1;
}

public class Choice
{
    public required LogicBrick[] Options;
    public int Count = 1;
}

public class LevelEntry
{
    public LogicBrick[] Grants = [];
    public FeatSelection[] Selections = [];
}

public class ClassDef : BaseDef, ISelectable
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int HpPerLevel;
    public required AbilityStat KeyAbility;
    public required ValueStatBlock<int> StartingStats;
    public LevelEntry?[] Progression = [];
    public IEnumerable<FeatDef> ClassFeats = [];
    public Action<Player>? GrantStartingEquipment;
}

public enum FeatType { Class, General, Ancestry, AttributeBoost }

// Spell slots by character level (index 0 = level 1)
// Each inner array is [L1, L2, L3, L4, L5]
public static class MediumCaster
{
    public static readonly int[][] Slots = [
        [2, 0, 0, 0, 0], // 1
        [2, 0, 0, 0, 0], // 2
        [2, 0, 0, 0, 0], // 3
        [2, 0, 0, 0, 0], // 4
        [2, 1, 0, 0, 0], // 5
        [2, 1, 0, 0, 0], // 6
        [3, 1, 0, 0, 0], // 7
        [3, 1, 0, 0, 0], // 8
        [3, 1, 1, 0, 0], // 9
        [3, 2, 1, 0, 0], // 10
        [3, 2, 1, 0, 0], // 11
        [3, 2, 2, 0, 0], // 12
        [3, 2, 2, 1, 0], // 13
        [4, 2, 2, 1, 0], // 14
        [4, 3, 2, 1, 0], // 15
        [4, 3, 3, 1, 0], // 16
        [4, 3, 3, 1, 1], // 17
        [4, 3, 3, 2, 1], // 18
        [4, 3, 3, 2, 1], // 19
        [4, 3, 3, 2, 1], // 20
    ];
}

public static class Progression
{
    // XP table matched to NH kill counts with linear XP formula
    // Assumes avg mob level ≈ player level, 60*L XP per kill
    static readonly int[] XpTable = [
        0,      // 0 (unused)
        0,      // 1
        300,    // 2
        600,    // 3
        1000,   // 4
        1500,   // 5
        2200,   // 6
        3000,   // 7
        6000,   // 8  (~20 kills)
        9000,   // 9  (~28 kills)
        12000,  // 10 (~35 kills)
        16000,  // 11
        20000,  // 12
        25000,  // 13
        30000,  // 14
        36000,  // 15
        42000,  // 16
        50000,  // 17
        58000,  // 18
        68000,  // 19
        80000,  // 20
    ];

    public const int MaxLevel = 20;

    public static int XpForLevel(int level) => level < 1 ? 0 : level > MaxLevel ? XpTable[MaxLevel] : XpTable[level];

    public static int LevelForXp(int xp)
    {
        for (int i = MaxLevel; i >= 1; i--)
            if (xp >= XpTable[i]) return i;
        return 1;
    }

    public static FeatType[] FeatsAtLevel(int level) => level switch
    {
        2 => [FeatType.Class],
        3 => [FeatType.General],
        4 => [FeatType.Class],
        5 => [FeatType.AttributeBoost],
        6 => [FeatType.Class],
        7 => [FeatType.Ancestry],
        8 => [FeatType.Class],
        9 => [FeatType.General],
        10 => [FeatType.Class],
        11 => [FeatType.AttributeBoost],
        12 => [FeatType.Class],
        13 => [FeatType.Ancestry],
        14 => [FeatType.Class],
        15 => [FeatType.General],
        16 => [FeatType.AttributeBoost],
        17 => [FeatType.Ancestry],
        18 => [FeatType.Class],
        19 => [FeatType.General],
        20 => [FeatType.Class],
        _ => [],
    };


    public static bool HasPendingLevelUp(Player p) => p.CharacterLevel < LevelForXp(p.XP);
}