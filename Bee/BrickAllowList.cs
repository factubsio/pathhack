using System.Collections.Generic;

namespace Bee;

public static class BrickAllowList
{
    public static HashSet<string> InlineTypes = new()
    {
        "QueryBrick", "GrantAction", "GrantSpell", "GrantPool", "GrantProficiency",
        "GrantBlessingBrick", "Equip", "EquipSet",
        "TimedFact", "ApplyWhenEquipped", "GrantWhenEquipped",
        "ApplyAfflictionOnHit", "ApplyFactOnAttackHit",
    };

    /// <summary>Hooks that are declared but not called by the game loop.</summary>
    public static HashSet<string> UnsupportedHooks = new()
    {
        "OnTurnStart",
        "OnTurnEnd",
    };

    /// <summary>Hooks that are deprecated with a reason.</summary>
    public static Dictionary<string, string> DeprecatedHooks = new()
    {
        // ["OnFoo"] = "use OnBar instead",
    };

    /// <summary>Classes allowed to override unsupported/deprecated hooks (e.g. logging).</summary>
    public static HashSet<string> HookOverrideAllowList = new()
    {
        "BrickStatsHook",
    };
}
