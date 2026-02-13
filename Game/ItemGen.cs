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
    ];
    
    static int TotalWeight => ClassWeights.Sum(x => x.weight);

    public static Item? GenerateForShop(ShopType type, int depth) => type switch
    {
        ShopType.Weapon => GenerateWeapon(depth),
        ShopType.Armor => GenerateArmor(depth),
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

    public static BUC RollBUC(int chance = 10)
    {
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
        Item item = new(def) { BUC = RollBUC(bucChance) };
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
            ApplyRune(item, Runes.NullFundamental, fundamental: true);
            return;
        }
        
        genLog.Add($"striking r{roll}={quality}");
        ApplyRune(item, Runes.Striking(quality), fundamental: true);
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
            
            RuneDef rune = category switch
            {
                0 => Runes.Flaming(quality),
                1 => Runes.Frost(quality),
                _ => Runes.Shock(quality),
            };
            
            ApplyRune(item, rune, fundamental: false);
        }
    }

    private static int RollQuality(int depth)
    {
        int d = Math.Clamp(depth, 0, ItemGenTables.Quality.Length - 1);
        return ItemGenTables.Quality[d][g.Rn2(100)];
    }

    public static void ApplyRune(Item item, RuneDef runeDef, bool fundamental)
    {
        var rune = new Rune(runeDef);
        foreach (var c in runeDef.Components)
            rune.Facts.Add(item.AddFact(c));

        if (fundamental)
            item.Fundamental = rune;
        else
            item.PropertyRunes.Add(rune);
    }
}

public static class Runes
{
    public static readonly RuneDef NullFundamental = new()
    {
        Slot = RuneSlot.Fundamental,
        IsNull = true,
        DisplayName = "null",
        Quality = -1,
        Description = "broken",
    };

    public static RuneDef Bonus(int quality) => new()
    {
        Slot = RuneSlot.Fundamental,
        Components = [new BonusRune(quality)],
        DisplayName = "accurate",
        Quality = quality,
        Description = $"+{quality}d4 accuracy"
    };

    public static RuneDef Striking(int quality) => new()
    {
        Slot = RuneSlot.Fundamental,
        Components = [new StrikingRune(quality)],
        DisplayName = "striking",
        Quality = quality,
        Description = $"+{quality} dice damage"
    };

    public static RuneDef Flaming(int quality) => new()
    {
        Slot = RuneSlot.Property,
        Components = [new ElementalRune(DamageTypes.Fire, quality)],
        DisplayName = "flaming",
        Quality = quality,
        Description = $"+{quality}d6 fire damage"
    };

    public static RuneDef Frost(int quality) => new()
    {
        Slot = RuneSlot.Property,
        Components = [new ElementalRune(DamageTypes.Cold, quality)],
        DisplayName = "freezing",
        Quality = quality,
        Description = $"+{quality}d6 frost damage"
    };

    public static RuneDef Shock(int quality) => new()
    {
        Slot = RuneSlot.Property,
        Components = [new ElementalRune(DamageTypes.Shock, quality)],
        DisplayName = "shocking",
        Quality = quality,
        Description = $"+{quality}d6 shock damage"
    };
}

public class BonusRune(int dice) : LogicBrick
{
    public int Dice => dice;

    protected override void OnBeforeAttackRoll(Fact fact, PHContext context)
    {
        if (!fact.IsEquipped() || context.Weapon != fact.Entity) return;
        context.Check!.Modifiers.AddModifier(new(ModifierCategory.ItemBonus, context.Weapon.Potency + d(dice, 4).Roll(), "bonus rune"));
    }
}

public class StrikingRune(int extraDice) : LogicBrick
{
    public int ExtraDice => extraDice;
    
    protected override void OnBeforeDamageRoll(Fact fact, PHContext context)
    {
        if (!fact.IsEquipped() || context.Weapon != fact.Entity) return;
        if (context.Weapon?.Def is not WeaponDef wdef) return;
        if (context.Damage.Count == 0 || context.Damage[0].Formula != wdef.BaseDamage) return;
        context.Damage[0].ExtraDice = extraDice;
    }
}

public class ElementalRune(DamageType type, int quality) : LogicBrick
{
    public DamageType Type => type;
    public int Quality => quality;

    protected override void OnBeforeDamageRoll(Fact fact, PHContext context)
    {
        if (!fact.IsEquipped()) return;
        if (context.Weapon != fact.Entity) return;
        
        context.Damage.Add(new DamageRoll
        {
            Formula = new Dice(quality, 6),
            Type = type
        });
    }
}
