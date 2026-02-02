namespace Pathhack.Game;

public static class ItemGen
{
    public static Item GenerateItem(ItemDef def, int depth = 1, int? maxPotency = null, bool propertyRunes = true)
    {
        Item item = new(def);
        
        if (def is WeaponDef)
        {
            item.Potency = RollPotency(maxPotency);
            RollFundamental(item, depth);
            if (propertyRunes)
                RollPropertyRunes(item, depth);
        }

        LogicBrick.FireOnSpawn(item, PHContext.Create(null, Target.None));
        return item;
    }

    public static WeaponDef PickWeaponDef(int depth)
    {
        return MundaneArmory.AllWeapons.Pick();
    }

    public static int RollPotency(int? force = 3)
    {
        if (force.HasValue && force.Value < 0) return -force.Value;
        int max = force ?? 3;
        int roll = g.Rne(max + 1) - 1; // 0 to max, geometric
        return Math.Min(roll, max);
    }

    private static void RollFundamental(Item item, int depth, int charLevel = 1)
    {
        // 75% at depth 1, 25% at depth 30 (linear)
        int blockerChance = Math.Max(25, 75 - (depth - 1) * 50 / 29);
        int roll = g.Rn2(100);

        if (roll < blockerChance)
        {
            ApplyRune(item, Runes.NullFundamental, fundamental: true);
            return;
        }
        
        if (roll < 50)
            return; // empty
        
        // pick bonus vs striking
        bool isStriking = g.Rn2(2) == 0;
        int quality = RollQuality();
        
        RuneDef rune = isStriking
            ? Runes.Striking(quality)
            : Runes.Bonus(quality);
        
        ApplyRune(item, rune, fundamental: true);
    }

    private static void RollPropertyRunes(Item item, int depth)
    {
        int fillChance = 25;
        HashSet<int> usedCategories = [];
        
        while (item.HasEmptyPropertySlot && fillChance > 0)
        {
            if (g.Rn2(100) >= fillChance) break;
            
            int category = g.Rn2(3); // fire, frost, shock for now
            
            // duplicate category penalizes and skips
            if (!usedCategories.Add(category))
            {
                fillChance -= 2;
                continue;
            }
            
            int quality = RollQuality();
            
            RuneDef rune = category switch
            {
                0 => Runes.Flaming(quality),
                1 => Runes.Frost(quality),
                _ => Runes.Shock(quality),
            };
            
            ApplyRune(item, rune, fundamental: false);
            fillChance -= 6;
        }
    }

    private static int RollQuality()
    {
        return g.Rne(4); // 1-4 geometric
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

    private static Item GenerateWeapon(int depth)
    {
        var def = PickWeaponDef(depth);
        var item = GenerateItem(def);
        item.Potency = RollPotency(depth);
        RollFundamental(item, depth);
        RollPropertyRunes(item, depth);
        return item;
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
