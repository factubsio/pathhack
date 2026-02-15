namespace Pathhack.Game;

public class BottleDef : ItemDef
{
    public required SpellBrickBase Spell;

    public BottleDef()
    {
        Glyph = new(ItemClasses.Potion, ConsoleColor.Cyan);
        AppearanceCategory = Game.AppearanceCategory.Bottle;
        Stackable = true;
    }
}

public static class Bottles
{
    private static BottleDef Brew(SpellBrickBase spell)
    {
        if (spell.Targeting == TargetingType.Direction) throw new NotSupportedException("cannot brew a directional spell");
        return new()
        {
            Name = $"bottle of {spell.Name.ToLower()}",
            Spell = spell,
            Price = spell.Level switch
            {
                1 => 80,
                2 => 160,
                3 => 300,
                4 => 500,
                5 => 800,
                _ => 10000,
            },
        };
    }

    public static readonly BottleDef FalseLifeLesser = Brew(BasicLevel1Spells.FalseLifeLesser);
    public static readonly BottleDef Grease = Brew(BasicLevel1Spells.Grease);

    public static readonly BottleDef SoundBurst = Brew(BasicLevel2Spells.SoundBurst);
    public static readonly BottleDef HoldPerson = Brew(BasicLevel2Spells.HoldPerson);

    public static readonly BottleDef Fireball = Brew(BasicLevel3Spells.Fireball);
    public static readonly BottleDef FalseLife = Brew(BasicLevel3Spells.FalseLife);

    public static readonly BottleDef[] All = [Grease, SoundBurst, HoldPerson, Fireball, FalseLife, FalseLifeLesser];

    static Bottles()
    {
        for (int i = 0; i < All.Length; i++)
            All[i].AppearanceIndex = i;
    }

    public static void DoEffect(BottleDef def, IUnit user, Pos pos)
    {
        if (def.Spell.Targeting is TargetingType.Unit or TargetingType.None)
        {
            if (lvl.UnitAt(pos) is { } tgt)
            {
                def.Spell.Execute(DungeonMaster.AsLevel(user.EffectiveLevel - 4), null, Target.From(tgt));
                def.SetKnown();
            }
        }
        else
        {
            def.Spell.Execute(DungeonMaster.AsLevel(user.EffectiveLevel - 4), null, Target.From(pos));
            def.SetKnown();
        }
    }
}
