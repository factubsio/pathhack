namespace Pathhack.Game;

public class BottleDef : ItemDef
{
    public required SpellBrickBase Spell;

    public BottleDef()
    {
        Glyph = new(ItemClasses.Potion, ConsoleColor.Cyan);
        AppearanceCategory = Game.AppearanceCategory.Bottle;
        Stackable = true;
        Weight = 10;
        Material = Materials.Glass;
    }
}

[GenerateAll("All", typeof(BottleDef))]
public static partial class Bottles
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

    private static SpellBrick Bomb(string name, DamageType type, ConsoleColor color) => new(name, 1,
        $"(1 + CL/3)d6 burst (3x3) of {type.SubCat}. Reflex save for half.",
        $"Nd6 {type.SubCat} burst",
        (c, t) =>
        {
            if (t.Pos is not { } center) return;
            int progression = c.Query<int>("bomb_progression", null, MergeStrategy.Min, 3);
            int dice = Math.Max(1, c.CasterLevel / progression);
            using var area = lvl.CollectCircle(center, 1, andCenter: true);
            Draw.AnimateFlash(area, new Glyph('φ', color));
            int dc = c.GetSpellDC();
            foreach (var pos in area)
            {
                if (lvl.UnitAt(pos).IsNullOrDead()) continue;
                var victim = lvl.UnitAt(pos)!;
                using var ctx = PHContext.Create(c, Target.From(victim));
                CheckReflex(ctx, dc, type.SubCat);
                ctx.Damage.Add(new() { Formula = d(dice, 6), Type = type, HalfOnSave = true });
                DoDamage(ctx);
            }
            AreaSystem.AffectTiles(lvl, type, area);
        }, TargetingType.Pos, maxRange: 4);

    public static readonly SpellBrick AlchemistFire = Bomb("Alchemist's fire", DamageTypes.Fire, ConsoleColor.Red);
    public static readonly SpellBrick AlchemistAcid = Bomb("Alchemist's acid", DamageTypes.Acid, ConsoleColor.Green);
    public static readonly SpellBrick AlchemistFrost = Bomb("Alchemist's frost", DamageTypes.Cold, ConsoleColor.Cyan);
    public static readonly SpellBrick AlchemistShock = Bomb("Alchemist's shock", DamageTypes.Shock, ConsoleColor.Yellow);

    public static readonly BottleDef AlchemistFireBottle = Brew(AlchemistFire);
    public static readonly BottleDef AlchemistAcidBottle = Brew(AlchemistAcid);
    public static readonly BottleDef AlchemistFrostBottle = Brew(AlchemistFrost);
    public static readonly BottleDef AlchemistShockBottle = Brew(AlchemistShock);

    public static void DoEffect(BottleDef def, IUnit user, Pos pos)
    {
        var dm = DungeonMaster.As(user, dc: -4);
        if (def.Spell.Targeting is TargetingType.Unit or TargetingType.None)
        {
            if (lvl.UnitAt(pos) is { } tgt)
            {
                def.Spell.Execute(dm, null, Target.From(tgt));
                def.SetKnown();
            }
        }
        else
        {
            def.Spell.Execute(dm, null, Target.From(pos));
            def.SetKnown();
        }
    }
}
