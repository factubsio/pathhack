namespace Pathhack.Game;

public class QuiverDef : ItemDef
{
    public required Dice Capacity;
    public required string Launcher;

    public required WeaponDef Ammo;
}

public static class ArcherySystem
{
    public static void TryReload(Item? item, bool boost)
    {
        if (item?.Def is not QuiverDef) return;

        // TODO: base on brick query?
        bool doCharge = g.Rn2(boost ? 2 : 3) == 0;

        if (doCharge) item.Charge(1);
    }
}

[GenerateAll("Quivers", typeof(QuiverDef))]
public static partial class MundaneQuivers
{
    private static QuiverDef ArrowQuiver(string name, Dice capacity, DiceFormula dmg, int price, params LogicBrick[] components) => new()
    {
        Name = $"{name} quiver",
        id = $"quiv:{name}",
        CanHavePotency = true,
        Glyph = new(')'),
        DefaultEquipSlot = ItemSlots.Quiver,
        Capacity = capacity,
        Components = components,
        Price = price,
        Material = Materials.Leather,
        Launcher = Proficiencies.Bow,

        Ammo = new()
        {
            Price = 0,
            Weight = -1,
            BaseDamage = dmg,
            Profiency = Proficiencies.Bow,
            Glyph = new(')'),
            DamageType =  DamageTypes.Piercing,
            Name = name,
        }
    };

    public static readonly QuiverDef BasicArrows = ArrowQuiver(
            name: "arrow",
            capacity: d(4) + 3,
            dmg: d(8),
            price: 100);
}
