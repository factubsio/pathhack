namespace Pathhack.Game;

public class QuiverDef : ItemDef
{
    public required Dice Capacity;

    // the "group" of weapons that can launch ammo from this quiver
    public required string WeaponProficiency;

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

    public static void ShootFrom(IUnit unit, Item quiver, Pos dir)
    {
        var qd = (QuiverDef)quiver.Def;
        var ammo = Item.Create(qd.Ammo);
        quiver.Charges--;
        g.YouObserveSelf(unit, $"You shoot!", $"{unit:The} {VTense(unit, "shoot")} {ammo:an}!", "a twang");
        DoThrow(unit, ammo, dir, AttackType.Ammo, range: 8);
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
        WeaponProficiency = Proficiencies.Bow,

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

    public static readonly QuiverDef BlowgunDarts = new()
    {
        Name = "blowgun dart quiver",
        id = "quiv:blowgun_dart",
        CanHavePotency = true,
        Glyph = new(')'),
        DefaultEquipSlot = ItemSlots.Quiver,
        Capacity = d(3) + 3,
        Components = [],
        Price = 20,
        Material = Materials.Leather,
        WeaponProficiency = Proficiencies.Blowgun,
        Ammo = new()
        {
            Price = 0,
            Weight = -1,
            BaseDamage = d(3),
            Profiency = Proficiencies.Blowgun,
            Glyph = new(')'),
            DamageType = DamageTypes.Piercing,
            Name = "blowgun dart",
        },
    };
}
