namespace Pathhack.Game;

public class WandDef : ItemDef
{
    public readonly SpellBrickBase Spell;
    public readonly int MaxCharges;

    public WandDef(SpellBrickBase spell)
    {
        Spell = spell;
        MaxCharges = spell.Targeting == TargetingType.None ? 15 : 8;
        Glyph = new(ItemClasses.Wand, ConsoleColor.Magenta);
        AppearanceCategory = Game.AppearanceCategory.Wand;
        Stackable = false;
    }
}

public static class Wands
{
    // Put the spell a box, a rod shaped box
    private static WandDef Crate(SpellBrickBase spell)
    {
        if (spell.Targeting is TargetingType.Pos or TargetingType.Unit) throw new NotSupportedException("cannot create pos/unit wand");

        return new(spell)
        {
            Name = $"wand of {spell.Name.ToLower()}",
            Price = spell.Level switch
            {
                // reusable so expensive, yolo
                1 => 2 * 80,
                2 => 2 * 160,
                3 => 2 * 300,
                4 => 2 * 500,
                5 => 2 * 800,
                _ => 2 * 10000,
            },
        };
    }

    public static readonly WandDef MagicMissile = Crate(BasicLevel1Spells.MagicMissile);
    public static readonly WandDef BurningHands = Crate(BasicLevel1Spells.BurningHands);
    public static readonly WandDef CureLightWounds = Crate(BasicLevel1Spells.CureLightWounds);
    public static readonly WandDef AcidArrow = Crate(BasicLevel1Spells.AcidArrow);
    public static readonly WandDef ScorchingRay = Crate(BasicLevel2Spells.ScorchingRay);

    public static readonly WandDef[] All = [MagicMissile, BurningHands, CureLightWounds, AcidArrow, ScorchingRay];

    static Wands()
    {
        for (int i = 0; i < All.Length; i++)
            All[i].AppearanceIndex = i;
    }

    public static void DoEffect(WandDef def, IUnit user, Pos dir)
    {
        def.Spell.Execute(DungeonMaster.AsLevel(user.EffectiveLevel - 4).At(user.Pos), null, Target.From(dir));
        def.SetKnown();
    }
}