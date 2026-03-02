namespace Pathhack.Game;

public class ShieldDef : ArmorDef
{
    public required int BaseBlockChance; // 0-100

    public ShieldDef()
    {
        DefaultEquipSlot = ItemSlots.Hand;
        Proficiency = Proficiencies.Shield;
        Glyph = new(ItemClasses.Armor);
    }
}

public class ShieldBrick(int acBonus, int baseBlock) : LogicBrick
{
    public override string Id => $"shield+{acBonus}/{baseBlock}";

    protected override void OnUnequip(Fact fact, PHContext context) => context.Source?.RemoveStack(RaisedShieldBuff.Instance);

    protected override object? OnQuery(Fact fact, string key, string? arg)
    {
        if (!fact.IsEquipped()) return null;
        var item = fact.Entity as Item;
        var potency = item?.Potency ?? 0;
        var degradation = item?.Degradation ?? 0;
        return key switch
        {
            "ac" => new Modifier(ModifierCategory.CircumstanceBonus, Math.Max(1, acBonus + potency - degradation), "shield"),
            "shield_block_chance" => EffectiveBlockChance(fact, degradation),
            _ => null,
        };
    }

    int EffectiveBlockChance(Fact fact, int degradation)
    {
        int chance = baseBlock - degradation * 7;
        if (fact.Entity is Item { Holder: Player p })
        {
            var prof = p.GetProficiency(Proficiencies.Shield);
            chance = prof switch
            {
                ProficiencyLevel.Untrained => chance / 2,
                ProficiencyLevel.Trained => chance,
                ProficiencyLevel.Expert => chance + 5,
                ProficiencyLevel.Master => chance + 12,
                ProficiencyLevel.Legendary => chance + 20,
                _ => chance,
            };
        }
        return Math.Max(0, chance);
    }
}

public class RaisedShieldBuff : LogicBrick
{
    public static readonly RaisedShieldBuff Instance = new();
    public override string Id => "raised_shield";
    public override bool IsBuff => true;
    public override string? BuffName => "Shield Raised";
    public override StatusDisplay StatusDisplayPriority => StatusDisplay.Buff;
    public override StackMode StackMode => StackMode.ExtendDuration;

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not IUnit unit) return;
        int blockChance = unit.Query("shield_block_chance", null, MergeStrategy.Max, 0);
        if (blockChance <= 0) return;
        if (g.Rn2(100) >= blockChance) return;

        // Block all damage
        foreach (var dmg in ctx.Damage.Where(d => d.Type.Category == "phys"))
            dmg.Negate();

        // Find the equipped shield for messaging and degradation
        Item? shield = FindEquippedShield(unit);
        if (shield != null)
        {
            g.YouObserveSelf(unit,
                $"Your {shield:bare} blocks the attack!",
                $"{unit:The}'s {shield:bare} blocks the attack!");

            // 33% chance to degrade shield on block
            if (g.Rn2(3) == 0)
            {
                var result = shield.TryDegrade();
                if (result != DegradeResult.None)
                    Item.PrintDegrade(unit, shield, result);
            }
        }
    }

    public static Item? FindEquippedShield(IUnit unit)
    {
        foreach (var item in unit.Equipped.Values)
            if (item.Def is ShieldDef) return item;
        return null;
    }
}

public class RaiseShieldAction() : CooldownAction("Raise Shield", TargetingType.None, _ => 10)
{
    public static readonly RaiseShieldAction Instance = new();

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        var shield = RaisedShieldBuff.FindEquippedShield(unit);
        if (shield == null) return;

        unit.AddFact(RaisedShieldBuff.Instance, null, 2);
        g.YouObserveSelf(unit,
            $"You raise your {shield:bare}!",
            $"{unit:The} {VTense(unit, "raise")} {unit:own} {shield:bare}!");
    }
}

[GenerateAll("AllShields", typeof(ShieldDef))]
public static partial class Shields
{
    public static readonly ShieldDef Buckler = new()
    {
        id = "buckler",
        Name = "buckler",
        ACBonus = 1,
        Proficiency = Proficiencies.Shield,
        ArmorType = ArmorTypes.Buckler,
        BaseBlockChance = 10,
        Glyph = new(ItemClasses.Armor, ConsoleColor.DarkYellow),
        Components = [
                new ShieldBrick(1, 10), RaiseShieldAction.Instance.WhenEquipped()],
        Weight = 30,
        Price = 50,
        Material = Materials.Wood,
    };

    public static readonly ShieldDef LightShield = new()
    {
        id = "light_shield",
        Name = "light shield",
        ACBonus = 1,
        Proficiency = Proficiencies.Shield,
        ArmorType = ArmorTypes.LightShield,
        BaseBlockChance = 20,
        Glyph = new(ItemClasses.Armor, ConsoleColor.Gray),
        Components = [new ShieldBrick(1, 20), RaiseShieldAction.Instance.WhenEquipped()],
        Weight = 60,
        Price = 100,
    };

    public static readonly ShieldDef MediumShield = new()
    {
        id = "medium_shield",
        Name = "medium shield",
        ACBonus = 2,
        Proficiency = Proficiencies.Shield,
        ArmorType = ArmorTypes.MediumShield,
        BaseBlockChance = 30,
        Glyph = new(ItemClasses.Armor, ConsoleColor.White),
        Components = [new ShieldBrick(2, 30), RaiseShieldAction.Instance.WhenEquipped()],
        Weight = 90,
        Price = 150,
    };

    public static readonly ShieldDef HeavyShield = new()
    {
        id = "heavy_shield",
        Name = "heavy shield",
        ACBonus = 2,
        Proficiency = Proficiencies.Shield,
        ArmorType = ArmorTypes.HeavyShield,
        BaseBlockChance = 40,
        Glyph = new(ItemClasses.Armor, ConsoleColor.DarkGray),
        Components = [new ShieldBrick(2, 40), RaiseShieldAction.Instance.WhenEquipped()],
        Weight = 130,
        Price = 200,
    };

    public static readonly ShieldDef TowerShield = new()
    {
        id = "tower_shield",
        Name = "tower shield",
        ACBonus = 3,
        Proficiency = Proficiencies.Shield,
        ArmorType = ArmorTypes.TowerShield,
        BaseBlockChance = 50,
        Glyph = new(ItemClasses.Armor, ConsoleColor.DarkCyan),
        Components = [new ShieldBrick(3, 50), RaiseShieldAction.Instance.WhenEquipped()],
        Weight = 180,
        Price = 300,
        Material = Materials.Wood,
    };
}
