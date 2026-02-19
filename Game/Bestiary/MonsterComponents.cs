namespace Pathhack.Game.Bestiary;

public class Ferocity : LogicBrick<DataFlag>
{
    public override string Id => "ferocity";
    public override string? PokedexDescription => "Ferocity (survives first lethal blow at 1 HP)";

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        if (X(fact)) return; // already used
        var unit = ctx.Target.Unit!;
        if (unit.HP.Current > 0) return; // not dying
        
        unit.HP.Current = 1;
        X(fact).On = true;
        g.pline($"{unit:The} refuses to fall!");
    }
}

public class Equip(ItemDef itemDef) : LogicBrick
{
    public override string Id => $"equip+{itemDef.id}";
    public override AbilityTags Tags => AbilityTags.FirstSpawnOnly;
    protected override void OnSpawn(Fact fact, PHContext context)
    {
        var item = ItemGen.GenerateItem(itemDef);
        context.Source!.Inventory.Add(item);
        context.Source!.Equip(item);
    }
}

public record OutfitItem(ItemDef Item, int Chance = 100, DiceFormula? Count = null);

public record Outfit(int Weight, params OutfitItem[] Items);

public class EquipSet(params Outfit[] outfits) : LogicBrick
{
    public override string Id => "equip_set";
    public override AbilityTags Tags => AbilityTags.FirstSpawnOnly;
    public static EquipSet OneOf(params ItemDef[] items) =>
        new([.. items.Select(i => new Outfit(1, new OutfitItem(i)))]);

    public static EquipSet Roll(ItemDef item, int chance) =>
        new(new Outfit(1, new OutfitItem(item, chance)));

    public static EquipSet WithCount(ItemDef item, DiceFormula count) =>
        new(new Outfit(1, new OutfitItem(item, Count: count)));

    public static EquipSet Weighted(params (int weight, ItemDef? item)[] entries) =>
        new([.. entries.Select(e => e.item != null ? new Outfit(e.weight, new OutfitItem(e.item)) : new Outfit(e.weight))]);

    protected override void OnSpawn(Fact fact, PHContext context)
    {
        int total = outfits.Sum(o => o.Weight);
        int roll = g.Rn2(total);
        int acc = 0;
        Outfit? picked = null;
        foreach (var o in outfits)
        {
            acc += o.Weight;
            if (roll < acc) { picked = o; break; }
        }
        if (picked == null) return;

        foreach (var oi in picked.Items)
        {
            if (oi.Chance < 100 && g.Rn2(100) >= oi.Chance) continue;
            var item = ItemGen.GenerateItem(oi.Item);
            if (oi.Count is { } countFormula) item.Count = countFormula.Roll();
            context.Source!.Inventory.Add(item);
            context.Source!.Equip(item);
        }
    }
}

public static class NaturalWeapons
{
    public static readonly WeaponDef Fist = new()
    {
        Name = "fist",
        BaseDamage = d(2),
        DamageType = DamageTypes.Blunt,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Unarmed,
        Material = "flesh",
        Price = -1,
    };

    public static readonly WeaponDef Bite_1d3 = new()
    {
        id = "bite_1d3",
        Name = "bite",
        BaseDamage = d(3),
        DamageType = DamageTypes.Piercing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "tooth",
        Price = -1,
    };

    public static readonly WeaponDef Bite_1d4 = new()
    {
        id = "bite_1d4",
        Name = "bite",
        BaseDamage = d(4),
        DamageType = DamageTypes.Piercing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "tooth",
        Price = -1,
    };

    public static readonly WeaponDef Bite_1d6 = new()
    {
        id = "bite_1d6",
        Name = "bite",
        BaseDamage = d(6),
        DamageType = DamageTypes.Piercing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "tooth",
        Price = -1,
    };

    public static readonly WeaponDef Bite_1d8 = new()
    {
        id = "bite_1d8",
        Name = "bite",
        BaseDamage = d(8),
        DamageType = DamageTypes.Piercing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "tooth",
        Price = -1,
    };

    public static readonly WeaponDef Bite_2d6 = new()
    {
        id = "bite_2d6",
        Name = "bite",
        BaseDamage = d(2, 6),
        DamageType = DamageTypes.Piercing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "tooth",
        Price = -1,
    };

    public static readonly WeaponDef Bite_2d8 = new()
    {
        id = "bite_2d8",
        Name = "jaws",
        BaseDamage = d(2, 8),
        DamageType = DamageTypes.Piercing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "tooth",
        Price = -1,
    };

    public static readonly WeaponDef Bite_2d10 = new()
    {
        id = "bite_2d10",
        Name = "jaws",
        BaseDamage = d(2, 10),
        DamageType = DamageTypes.Piercing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "tooth",
        Price = -1,
    };

    public static readonly WeaponDef Stomp_1d8 = new()
    {
        id = "stomp_1d8",
        Name = "stomp",
        BaseDamage = d(8),
        DamageType = DamageTypes.Blunt,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "keratin",
        Price = -1,
    };

    public static readonly WeaponDef Stomp_1d10 = new()
    {
        id = "stomp_1d10",
        Name = "stomp",
        BaseDamage = d(10),
        DamageType = DamageTypes.Blunt,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "keratin",
        Price = -1,
    };

    public static readonly WeaponDef Claw_1d2 = new()
    {
        id = "claw_1d2",
        Name = "claw",
        BaseDamage = d(2),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "keratin",
        Price = -1,
    };

    public static readonly WeaponDef Claw_1d3 = new()
    {
        id = "claw_1d3",
        Name = "claw",
        BaseDamage = d(3),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "keratin",
        Price = -1,
    };

    public static readonly WeaponDef Claw_1d4 = new()
    {
        id = "claw_1d4",
        Name = "claw",
        BaseDamage = d(4),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "keratin",
        Price = -1,
    };

    public static readonly WeaponDef Claw_1d6 = new()
    {
        id = "claw_1d6",
        Name = "claw",
        BaseDamage = d(6),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "keratin",
        Price = -1,
    };

    public static readonly WeaponDef Claw_1d8 = new()
    {
        id = "claw_1d8",
        Name = "claw",
        BaseDamage = d(8),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        Material = "keratin",
        Price = -1,
    };

    public static readonly WeaponDef DogSlicer = new()
    {
        Name = "dogslicer",
        BaseDamage = d(6),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        MeleeVerb = "swings",
        Price = -1,
    };

    public static readonly WeaponDef Slam_1d4 = new()
    {
        id = "slam_1d4",
        Name = "slam",
        BaseDamage = d(4),
        DamageType = DamageTypes.Blunt,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        MeleeVerb = "slams",
        Price = -1,
    };

    public static readonly WeaponDef Slam_1d6 = new()
    {
        id = "slam_1d6",
        Name = "slam",
        BaseDamage = d(6),
        DamageType = DamageTypes.Blunt,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        MeleeVerb = "slams",
        Price = -1,
    };

    public static readonly WeaponDef Slam_2d6 = new()
    {
        id = "slam_2d6",
        Name = "slam",
        BaseDamage = d(2, 6),
        DamageType = DamageTypes.Blunt,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        MeleeVerb = "slams",
        Price = -1,
    };

    public static readonly WeaponDef Slam_2d8 = new()
    {
        id = "slam_2d8",
        Name = "slam",
        BaseDamage = d(2, 8),
        DamageType = DamageTypes.Blunt,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        MeleeVerb = "slams",
        Price = -1,
    };

    public static readonly WeaponDef Branch_1d6 = new()
    {
        id = "branch_1d6",
        Name = "branch",
        BaseDamage = d(6),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        MeleeVerb = "slashes",
        Price = -1,
    };

    public static readonly WeaponDef Branch_1d8 = new()
    {
        id = "branch_1d8",
        Name = "branch",
        BaseDamage = d(8),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        MeleeVerb = "slashes",
        Price = -1,
    };

    public static readonly WeaponDef Branch_2d6 = new()
    {
        id = "branch_2d6",
        Name = "branch",
        BaseDamage = d(2, 6),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        MeleeVerb = "slashes",
        Price = -1,
    };

    public static readonly WeaponDef Tendril_1d4 = new()
    {
        id = "tendril_1d4",
        Name = "tendril",
        BaseDamage = d(4),
        DamageType = DamageTypes.Blunt,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        MeleeVerb = "lashes",
        Price = -1,
    };

    public static readonly WeaponDef Tendril_1d6 = new()
    {
        id = "tendril_1d6",
        Name = "tendril",
        BaseDamage = d(6),
        DamageType = DamageTypes.Blunt,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        MeleeVerb = "lashes",
        Price = -1,
    };

    public static readonly WeaponDef Vine_1d6 = new()
    {
        id = "vine_1d6",
        Name = "vine",
        BaseDamage = d(6),
        DamageType = DamageTypes.Blunt,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
        MeleeVerb = "lashes",
        Price = -1,
    };
}

public class SayOnDeath(string message) : LogicBrick
{
    public override string Id => $"say_death+{message}";
    protected override void OnDeath(Fact fact, PHContext ctx) => LoreDump(message);
}

public class DropOnDeath(ItemDef def) : LogicBrick
{
    public override string Id => $"drop_death+{def.id}";
    protected override void OnDeath(Fact fact, PHContext ctx) => lvl.PlaceItem(Item.Create(def), ctx.Target.Unit!.Pos);
}

public class GenerateDropOnDeath(ItemDef item) : LogicBrick
{
  public override string Id => $"gen_drop_death+{item.id}";
  protected override void OnDeath(Fact fact, PHContext ctx) => lvl.PlaceItem(ItemGen.GenerateItem(item), ctx.Target.Unit!.Pos);
}