using System.Reflection.Metadata.Ecma335;

namespace Pathhack.Game.Bestiary;

public class Ferocity : LogicBrick<DataFlag>
{
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
    public static EquipSet OneOf(params ItemDef[] items) =>
        new([.. items.Select(i => new Outfit(1, new OutfitItem(i)))]);

    public static EquipSet Roll(ItemDef item, int chance) =>
        new(new Outfit(1, new OutfitItem(item, chance)));

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
    };

    public static readonly WeaponDef Bite_1d3 = new()
    {
        Name = "bite",
        BaseDamage = d(3),
        DamageType = DamageTypes.Piercing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
    };

    public static readonly WeaponDef Claw_1d4 = new()
    {
        Name = "claw",
        BaseDamage = d(4),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
    };

    public static readonly WeaponDef Claw_1d6 = new()
    {
        Name = "claw",
        BaseDamage = d(6),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        Category = WeaponCategory.Natural,
    };

    public static readonly WeaponDef DogSlicer = new()
    {
        Name = "dogslicer",
        BaseDamage = d(6),
        DamageType = DamageTypes.Slashing,
        Profiency = Proficiencies.Unarmed,
        MeleeVerb = "swings",
    };
}

public class SayOnDeath(string message) : LogicBrick
{
    protected override void OnDeath(Fact fact, PHContext ctx) => LoreDump(message);
}

public class DropOnDeath(ItemDef def) : LogicBrick
{
    protected override void OnDeath(Fact fact, PHContext ctx) => lvl.PlaceItem(Item.Create(def), ctx.Target.Unit!.Pos);
}

public class GenerateDropOnDeath(ItemDef item) : LogicBrick
{
  protected override void OnDeath(Fact fact, PHContext ctx) => lvl.PlaceItem(ItemGen.GenerateItem(item), ctx.Target.Unit!.Pos);
}
