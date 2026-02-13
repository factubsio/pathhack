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

    public static readonly MonsterDef Balloon = new()
    {
        id = "balloon",
        Name = "training balloon",
        CreatureType = CreatureTypes.Construct,
        Family = "construct",
        Glyph = new('\'', ConsoleColor.White),
        AttackBonus = 0,
        HpPerLevel = 1,
        AC = -10,
        LandMove = 0,
        Unarmed = NaturalWeapons.Fist,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        Components =
        [
            RegenBrick.Always,
        ],
    };

    public static readonly MonsterDef Dummy = new()
    {
        id = "dummy",
        Name = "training dummy",
        CreatureType = CreatureTypes.Construct,
        Family = "construct",
        Glyph = new('\'', ConsoleColor.DarkYellow),
        AttackBonus = 0,
        HpPerLevel = 100,
        AC = 10,
        LandMove = 0,
        Unarmed = NaturalWeapons.Fist,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        Components =
        [
            new GrantPool("spell_l1", 3, 20),
            new GrantPool("spell_l2", 2, 30),
            new GrantSpell(Spells.BasicLevel1Spells.CureLightWounds),
            new GrantSpell(Spells.BasicLevel1Spells.Light),
            new GrantSpell(Spells.BasicLevel1Spells.AcidArrow),
        ],
    };
}
