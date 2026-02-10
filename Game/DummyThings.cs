namespace Pathhack.Game;

public static class DummyThings
{
    public static readonly ItemDef FireWand = new() { id = "fire_wand", Name = "wand of fire", Glyph = new(ItemClasses.Wand, ConsoleColor.Red), Price = 100, };
    public static readonly ItemDef ColdWand = new() { id = "cold_wand", Name = "wand of cold", Glyph = new(ItemClasses.Wand, ConsoleColor.Blue), Price = 100, };

    public static readonly ItemDef Blindfold = new()
    {
        id = "blindfold",
        Name = "blindfold",
        Glyph = new(ItemClasses.Amulet, ConsoleColor.DarkGray),
        DefaultEquipSlot = ItemSlots.Face,
        Components = [ApplyWhenEquipped.For(BlindBuff.Instance)],
        Price = 2,
    };

    public static readonly ItemDef[] All = [FireWand, ColdWand, Blindfold];

    public static readonly MonsterDef Dummy = new()
    {
        id = "dummy",
        Name = "training dummy",
        CreatureType = CreatureTypes.Construct,
        Family = "construct",
        Glyph = new('d', ConsoleColor.DarkYellow),
        AttackBonus = 0,
        HpPerLevel = 100,
        AC = 10,
        LandMove = 0,
        Unarmed = Bestiary.NaturalWeapons.Fist,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
    };
}
