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
}
