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
        (3, GenerateRing),
        (2, GenerateBoots),
        (2, GenerateGloves),
    ];
    
    static int TotalWeight => ClassWeights.Sum(x => x.weight);

    static readonly ItemDef[] ArmorShopPool = [..MundaneArmory.AllArmors, ..MagicAccessories.AllBoots, ..MagicAccessories.AllGloves];

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

    public static Item? GeneratePotion(int depth)
    {
        if (Potions.All.Length == 0) return null;
        var def = Potions.All[g.Rn2(Potions.All.Length)];
        return GenerateItem(def, depth);
    }

    public static Item? GenerateScroll(int depth)
    {
        if (Scrolls.All.Length == 0) return null;
        var def = Scrolls.All[g.Rn2(Scrolls.All.Length)];
        return GenerateItem(def, depth);
    }

    public static Item? GenerateWeapon(int depth)
    {
        var def = PickWeaponDef(depth);
        return GenerateItem(def, depth);
    }

    public static Item? GenerateArmor(int depth)
    {
        var def = MundaneArmory.AllArmors.Pick();
        return GenerateItem(def, depth);
    }

    static Item? GenerateRing(int depth)
    {
        if (MagicAccessories.AllRings.Length == 0) return null;
        var def = MagicAccessories.AllRings[g.Rn2(MagicAccessories.AllRings.Length)];
        return GenerateItem(def, depth);
    }

    static Item? GenerateBoots(int depth)
    {
        if (MagicAccessories.AllBoots.Length == 0) return null;
        var def = MagicAccessories.AllBoots[g.Rn2(MagicAccessories.AllBoots.Length)];
        return GenerateItem(def, depth);
    }

    static Item? GenerateGloves(int depth)
    {
        if (MagicAccessories.AllGloves.Length == 0) return null;
        var def = MagicAccessories.AllGloves[g.Rn2(MagicAccessories.AllGloves.Length)];
        return GenerateItem(def, depth);
    }

    public static BUC RollBUC(int chance = 10, int bias = 0)
    {
        if (bias <= -2) return BUC.Cursed;
        if (bias >= 2) return BUC.Blessed;
        if (bias == -1 && g.Rn2(10) != 0) return BUC.Cursed;
        if (bias == 1 && g.Rn2(10) != 0) return BUC.Blessed;
        if (g.Rn2(chance) != 0) return BUC.Uncursed;
        return g.Rn2(2) == 0 ? BUC.Cursed : BUC.Blessed;
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
        else if (def.CanHavePotency)
        {
            item.Potency = RollPotency(depth, genLog, maxPotency);
        }

        if (genLog.Count > 0)
            Log.Write($"objgen: d{depth}: {item.DisplayName} [{string.Join(", ", genLog)}]");

        LogicBrick.FireOnSpawn(item, PHContext.Create(null, Target.None));
        return item;
    }

    public static WeaponDef PickWeaponDef(int depth)
    {
        return MundaneArmory.AllWeapons.Pick();
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
