using System.Diagnostics.CodeAnalysis;

namespace Pathhack.Game;

// TODO: Add MapDepth(branch, actualDepth) -> 1-20 to handle:
//   - Branch difficulty offsets (crypt +2, easy branch -2)
//   - Depth compression for long dungeons
//   - Tables stay fixed 1-20, mapping handles the weirdness

public static class ItemGen
{
    // Class probabilities (dNH style)
    static readonly (int weight, Func<int, Item?> gen)[] ClassWeights = [
        (16, GeneratePotion),
        (16, GenerateScroll),
        (10, GenerateWeapon),
        (10, GenerateArmor),
        (4, GenerateWand),
        (4, GenerateBottle),
        (4, GenerateRing),
        (4, GenerateBoots),
        (4, GenerateGloves),
    ];
    
    static int TotalWeight => ClassWeights.Sum(x => x.weight);

    static readonly ItemDef[] ArmorShopPool = [..MundaneArmory.RandomAllArmors, ..MagicBoots.RandomAll, ..MagicGloves.RandomAll];

    public static Item? GenerateForShop(ShopType type, int depth) => type switch
    {
        ShopType.Weapon => GenerateWeapon(depth),
        ShopType.Armor => GenerateItem(ArmorShopPool.Pick(), depth),
        ShopType.Potion => GeneratePotion(depth),
        ShopType.Scroll => GenerateScroll(depth),
        ShopType.Ring => GenerateRing(depth),
        _ => GenerateRandomItem(depth),
    };

    public static Item? GenerateRandomItem(int depth)
    {
        int roll = g.Rn2(TotalWeight);
        foreach (var (weight, gen) in ClassWeights)
        {
            roll -= weight;
            if (roll < 0)
                return gen(depth);
        }
        return null;
    }

    static Item? PickFrom(ItemDef[] pool, int depth) =>
        pool.Length == 0 ? null : GenerateItem(pool.Pick(), depth);

    public static Item? GeneratePotion(int depth) => PickFrom(Potions.RandomAll, depth);
    public static Item? GenerateWand(int depth) => PickFrom(Wands.RandomAll, depth);
    public static Item? GenerateBottle(int depth) => PickFrom(Bottles.RandomAll, depth);
    public static Item? GenerateScroll(int depth) => PickFrom(Scrolls.RandomAll, depth);
    public static Item? GenerateWeapon(int depth) => PickFrom(MundaneArmory.RandomAllWeapons, depth);
    public static Item? GenerateArmor(int depth) => PickFrom(MundaneArmory.RandomAllArmors, depth);
    public static Item? GenerateQuiver(int depth) => PickFrom(MundaneQuivers.RandomQuivers, depth);
    public static Item? GenerateRing(int depth) => PickFrom(MagicRings.RandomAll, depth);
    public static Item? GenerateBoots(int depth) => PickFrom(MagicBoots.RandomAll, depth);
    public static Item? GenerateGloves(int depth) => PickFrom(MagicGloves.RandomAll, depth);

    public static bool TryGeneratePotion(int depth, [NotNullWhen(true)] out Item? item) => (item = PickFrom(Potions.RandomAll, depth)) != null;
    public static bool TryGenerateWand(int depth, [NotNullWhen(true)] out Item? item) => (item = PickFrom(Wands.RandomAll, depth)) != null;
    public static bool TryGenerateBottle(int depth, [NotNullWhen(true)] out Item? item) => (item = PickFrom(Bottles.RandomAll, depth)) != null;
    public static bool TryGenerateScroll(int depth, [NotNullWhen(true)] out Item? item) => (item = PickFrom(Scrolls.RandomAll, depth)) != null;
    public static bool TryGenerateWeapon(int depth, [NotNullWhen(true)] out Item? item) => (item = PickFrom(MundaneArmory.RandomAllWeapons, depth)) != null;
    public static bool TryGenerateArmor(int depth, [NotNullWhen(true)] out Item? item) => (item = PickFrom(MundaneArmory.RandomAllArmors, depth)) != null;
    public static bool TryGenerateQuiver(int depth, [NotNullWhen(true)] out Item? item) => (item = PickFrom(MundaneQuivers.RandomQuivers, depth)) != null;
    public static bool TryGenerateRing(int depth, [NotNullWhen(true)] out Item? item) => (item = PickFrom(MagicRings.RandomAll, depth)) != null;
    public static bool TryGenerateBoots(int depth, [NotNullWhen(true)] out Item? item) => (item = PickFrom(MagicBoots.RandomAll, depth)) != null;
    public static bool TryGenerateGloves(int depth, [NotNullWhen(true)] out Item? item) => (item = PickFrom(MagicGloves.RandomAll, depth)) != null;

    public static BUC RollBUC(int chance = 10, int bias = 0)
    {
        BUC RollInternal()
        {
            if (bias <= -2) return BUC.Cursed;
            if (bias >= 2) return BUC.Blessed;
            if (bias == -1 && g.Rn2(10) != 0) return BUC.Cursed;
            if (bias == 1 && g.Rn2(10) != 0) return BUC.Blessed;
            if (g.Rn2(chance) != 0) return BUC.Uncursed;
            return g.Rn2(2) == 0 ? BUC.Cursed : BUC.Blessed;
        }

        BUC baseBuc = RollInternal();
        if (NoCursedAllowed && baseBuc == BUC.Cursed) baseBuc = BUC.Uncursed;
        if (NoBlessedAllowed && baseBuc == BUC.Blessed) baseBuc = BUC.Uncursed;
        return baseBuc;
    }

    public static Item GenerateItem(ItemDef def, int depth = 1, int? maxPotency = null, bool propertyRunes = true)
    {
        int bucChance = def switch
        {
            WeaponDef => 10,
            ArmorDef => 10,
            ScrollDef => 10,
            PotionDef => 10,
            _ => 5, // rings, tools, gems
        };
        Item item = new(def) { BUC = RollBUC(bucChance, def.BUCBias) };
        List<string> genLog = [];
        
        if (def is WeaponDef)
        {
            item.Potency = RollPotency(depth, genLog, maxPotency);
            RollFundamental(item, depth, genLog);
            if (propertyRunes)
                RollPropertyRunes(item, depth, genLog);
        }
        else if (def is ArmorDef)
        {
            item.Potency = RollPotency(depth, genLog, maxPotency);
        }
        else
        {
            if (def.CanHavePotency)
            {
                item.Potency = RollPotency(depth, genLog, maxPotency);
            }

            if (def is WandDef wand)
            {
                item.MaxCharges = wand.MaxCharges;
                item.Charges = g.RnRange(wand.MaxCharges / 2 - 1, wand.MaxCharges - 1);
            }
            else if (def is QuiverDef quiver)
            {
                item.MaxCharges = quiver.Capacity.Roll();
                item.Charges = item.MaxCharges;
            }
        }

        if (genLog.Count > 0)
            Log.Write($"objgen: d{depth}: {item.DisplayName} [{string.Join(", ", genLog)}]");

        LogicBrick.FireOnSpawn(item, PHContext.Create(null, Target.None));
        return item;
    }

    public static int RollPotency(int depth, List<string>? genLog, int? force = null)
    {
        if (force.HasValue && force.Value < 0) return -force.Value;
        if (force.HasValue) return g.Rn2(force.Value + 1);
        
        int d = Math.Clamp(depth, 0, ItemGenTables.Potency.Length - 1);
        int roll = g.Rn2(100);
        int result = ItemGenTables.Potency[d][roll];
        if (result > 0) genLog?.Add($"potency r{roll}={result}");
        return result;
    }

    private static void RollFundamental(Item item, int depth, List<string> genLog, int charLevel = 1)
    {
        int d = Math.Clamp(depth, 0, ItemGenTables.Fundamental.Length - 1);
        int roll = g.Rn2(100);
        int quality = ItemGenTables.Fundamental[d][roll];
        
        if (quality == 0)
        {
            ApplyRune(item, NullFundamental.Instance, fundamental: true);
            return;
        }
        
        genLog.Add($"striking r{roll}={quality}");
        ApplyRune(item, StrikingRune.Of(quality), fundamental: true);
    }

    private static void RollPropertyRunes(Item item, int depth, List<string> genLog)
    {
        int d = Math.Clamp(depth, 0, ItemGenTables.Fill.Length - 1);
        HashSet<int> usedCategories = [];
        
        for (int slot = 0; slot < item.Potency; slot++)
        {
            int fillRoll = g.Rn2(100);
            if (ItemGenTables.Fill[d][fillRoll] == 0) continue;
            
            int category = g.Rn2(3); // fire, frost, shock for now
            if (!usedCategories.Add(category)) continue;
            
            int qualRoll = g.Rn2(100);
            int quality = ItemGenTables.Quality[d][qualRoll];
            
            string[] names = ["flaming", "frost", "shock"];
            genLog.Add($"{names[category]} f{fillRoll} q{qualRoll}={quality}");
            
            RuneBrick rune = category switch
            {
                0 => ElementalRune.Flaming(quality),
                1 => ElementalRune.Frost(quality),
                _ => ElementalRune.Shock(quality),
            };
            
            ApplyRune(item, rune, fundamental: false);
        }
    }

    private static int RollQuality(int depth)
    {
        int d = Math.Clamp(depth, 0, ItemGenTables.Quality.Length - 1);
        return ItemGenTables.Quality[d][g.Rn2(100)];
    }

    public static void ApplyRune(Item item, RuneBrick rune, bool fundamental)
    {
        var fact = item.AddFact(rune);
        if (fundamental)
            item.Fundamental = fact;
        else
            item.PropertyRunes.Add(fact);
    }

    internal static IDisposable LockNoBlessed() => LockRule(r => r.NoBlessed = true);
    internal static IDisposable LockNoCursed() => LockRule(r => r.NoCursed = true);

    internal static void DisposeRule()
    {
        rules.Pop();
    }

    private static ItemGenRules LockRule(Action<ItemGenRules> r)
    {
        ItemGenRules rule = new();
        r(rule);
        rules.Push(rule);
        return rule;
    }

    private static bool NoCursedAllowed => rules.Count > 0 && rules.Peek().NoCursed;
    private static bool NoBlessedAllowed => rules.Count > 0 && rules.Peek().NoBlessed;

    private static readonly Stack<ItemGenRules> rules = [];
}

public sealed class ItemGenRules : IDisposable
{
    public void Dispose() => ItemGen.DisposeRule();
    public bool NoCursed;
    public bool NoBlessed;
}

public class NullFundamental() : RuneBrick("null", -1, RuneSlot.Fundamental)
{
    public override string Id => "rune_null";
    public static readonly NullFundamental Instance = new();
}

public class BonusRune(int quality) : RuneBrick("accurate", quality, RuneSlot.Fundamental)
{
    public override string Id => $"r_fund:bonus/{Quality}";
    public override string Description => $"+{Quality}d4 accuracy";

    protected override void OnBeforeAttackRoll(Fact fact, PHContext context)
    {
        if (!fact.IsEquipped() || context.Weapon != fact.Entity) return;
        context.Check!.Modifiers.AddModifier(new(ModifierCategory.ItemBonus, context.Weapon.Potency + d(Quality, 4).Roll(), "bonus rune"));
    }

    public static readonly BonusRune Q1 = new(1), Q2 = new(2), Q3 = new(3), Q4 = new(4);
    public static BonusRune Of(int quality) => quality switch { 1 => Q1, 2 => Q2, 3 => Q3, 4 => Q4, _ => Q1 };
}

public class StrikingRune(int quality) : RuneBrick("striking", quality, RuneSlot.Fundamental)
{
    public override string Id => $"r_fund:striking/{Quality}";
    public override string Description => $"+{Quality} dice damage";

    protected override void OnBeforeDamageRoll(Fact fact, PHContext context)
    {
        if (!fact.IsEquipped() || context.Weapon != fact.Entity) return;
        if (context.Weapon?.Def is not WeaponDef wdef) return;
        if (context.Damage.Count == 0 || context.Damage[0].Formula != wdef.BaseDamage) return;
        context.Damage[0].ExtraDice = Quality;
    }

    public static readonly StrikingRune Q1 = new(1), Q2 = new(2), Q3 = new(3), Q4 = new(4);
    public static StrikingRune Of(int quality) => quality switch { 1 => Q1, 2 => Q2, 3 => Q3, 4 => Q4, _ => Q1 };
}

public class ElementalRune : RuneBrick
{
    readonly DamageType _type;
    public override string Id => $"r_prop:{_type.SubCat}/{Quality}";

    ElementalRune(string displayName, DamageType type, int quality) : base(displayName, quality, RuneSlot.Property)
        => _type = type;

    public override string Description => $"+{Quality}d6 {_type.SubCat} damage";

    protected override void OnBeforeDamageRoll(Fact fact, PHContext context)
    {
        if (!fact.IsEquipped()) return;
        if (context.Weapon != fact.Entity) return;

        context.Damage.Add(new DamageRoll
        {
            Formula = new Dice(Quality, 6),
            Type = _type
        });
    }

    public static readonly ElementalRune
        Flaming1 = new("flaming", DamageTypes.Fire, 1), Flaming2 = new("flaming", DamageTypes.Fire, 2),
        Flaming3 = new("flaming", DamageTypes.Fire, 3), Flaming4 = new("flaming", DamageTypes.Fire, 4),
        Frost1 = new("freezing", DamageTypes.Cold, 1), Frost2 = new("freezing", DamageTypes.Cold, 2),
        Frost3 = new("freezing", DamageTypes.Cold, 3), Frost4 = new("freezing", DamageTypes.Cold, 4),
        Shock1 = new("shocking", DamageTypes.Shock, 1), Shock2 = new("shocking", DamageTypes.Shock, 2),
        Shock3 = new("shocking", DamageTypes.Shock, 3), Shock4 = new("shocking", DamageTypes.Shock, 4);

    public static ElementalRune Flaming(int q) => q switch { 1 => Flaming1, 2 => Flaming2, 3 => Flaming3, 4 => Flaming4, _ => Flaming1 };
    public static ElementalRune Frost(int q) => q switch { 1 => Frost1, 2 => Frost2, 3 => Frost3, 4 => Frost4, _ => Frost1 };
    public static ElementalRune Shock(int q) => q switch { 1 => Shock1, 2 => Shock2, 3 => Shock3, 4 => Shock4, _ => Shock1 };
}
