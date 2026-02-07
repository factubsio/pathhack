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

public static class Progression
{
    // Exponential XP table, 1-indexed (index 0 unused)
    static readonly int[] XpTable = [
        0,      // 0 (unused)
        0,      // 1
        1000,   // 2
        3000,   // 3
        6000,   // 4
        10000,  // 5
        15000,  // 6
        21000,  // 7
        28000,  // 8
        36000,  // 9
        45000,  // 10
        55000,  // 11
        66000,  // 12
        78000,  // 13
        91000,  // 14
        105000, // 15
        120000, // 16
        136000, // 17
        153000, // 18
        171000, // 19
        190000, // 20
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
