namespace Pathhack.Game;

public static class DummyThings
{
    public static readonly ItemDef Ration = new() { id = "ration", Name = "food ration", Glyph = new(ItemClasses.Food, ConsoleColor.DarkYellow) };
    public static readonly ItemDef Apple = new() { id = "apple", Name = "apple", Glyph = new(ItemClasses.Food, ConsoleColor.Red) };
    public static readonly ItemDef HealPotion = new() { id = "heal_pot", Name = "potion of healing", Glyph = new(ItemClasses.Potion, ConsoleColor.Magenta) };
    public static readonly ItemDef SpeedPotion = new() { id = "speed_pot", Name = "potion of speed", Glyph = new(ItemClasses.Potion, ConsoleColor.Cyan) };
    public static readonly ItemDef TeleScroll = new() { id = "tele_scroll", Name = "scroll of teleportation", Glyph = new(ItemClasses.Scroll, ConsoleColor.White) };
    public static readonly ItemDef IdScroll = new() { id = "id_scroll", Name = "scroll of identify", Glyph = new(ItemClasses.Scroll, ConsoleColor.White) };
    public static readonly ItemDef FireWand = new() { id = "fire_wand", Name = "wand of fire", Glyph = new(ItemClasses.Wand, ConsoleColor.Red) };
    public static readonly ItemDef ColdWand = new() { id = "cold_wand", Name = "wand of cold", Glyph = new(ItemClasses.Wand, ConsoleColor.Blue) };
    public static readonly ItemDef ProtRing = new() { id = "prot_ring", Name = "ring of protection", Glyph = new(ItemClasses.Ring, ConsoleColor.Yellow) };
    public static readonly ItemDef StrRing = new() { id = "str_ring", Name = "ring of strength", Glyph = new(ItemClasses.Ring, ConsoleColor.Green) };

    public static readonly ItemDef Blindfold = new()
    {
        id = "blindfold",
        Name = "blindfold",
        Glyph = new(ItemClasses.Amulet, ConsoleColor.DarkGray),
        DefaultEquipSlot = ItemSlots.Face,
        Components = [ApplyWhenEquipped.For(BlindBuff.Instance)],
    };

    public static readonly WeaponDef Dart = new()
    {
        id = "dart",
        Name = "dart",
        BaseDamage = d(4),
        Profiency = Proficiencies.Thrown,
        WeaponType = WeaponTypes.Dart,
        DamageType = DamageTypes.Piercing,
        Range = 4,
        Stackable = true,
    };

    public static readonly ItemDef[] All = [Ration, Apple, HealPotion, SpeedPotion, TeleScroll, IdScroll, FireWand, ColdWand, ProtRing, StrRing, Blindfold];

    public static readonly MonsterDef Dummy = new()
    {
        id = "dummy",
        Name = "training dummy",
        Glyph = new('d', ConsoleColor.DarkYellow),
        AttackBonus = 0,
        HP = 100,
        AC = 10,
        LandMove = 0,
        Unarmed = Bestiary.NaturalWeapons.Fist,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
    };
}
