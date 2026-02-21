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
        Weight = 7;
    }
}

[GenerateAll("All", typeof(WandDef))]
public static partial class Wands
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
    public static readonly WandDef AcidArrow = Crate(BasicLevel2Spells.AcidArrow);
    public static readonly WandDef ScorchingRay = Crate(BasicLevel2Spells.ScorchingRay);

    public static void DoEffect(WandDef def, IUnit user, Pos dir)
    {
        def.Spell.Execute(DungeonMaster.As(user, -4), null, Target.From(dir));
        if (!def.IsKnown() && g.YouObserve(user, $"{user:The} {VTense(user, "zap")} a {def.Name}"))
        {
            def.SetKnown();
        }
    }
}