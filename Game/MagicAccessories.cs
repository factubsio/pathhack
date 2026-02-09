using System.Reflection.Metadata;

namespace Pathhack.Game;

public class WarningBuff(int range) : LogicBrick
{
    public static readonly WarningBuff Range8 = new(8);

    public override bool RequiresEquipped => true;

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "warning" ? range : null;
}

public class DetectCreatureBuff(string creatureType, int range) : LogicBrick
{
    public static readonly DetectCreatureBuff Undead8 = new(CreatureTypes.Undead, 8);
    public static readonly DetectCreatureBuff Beast8 = new(CreatureTypes.Beast, 8);

    public override bool RequiresEquipped => true;

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "detect_creature" && arg?.StartsWith(creatureType) == true ? range : null;
}

public class RamBlast() : CooldownAction("Ram Blast", TargetingType.Unit, _ => 40, 6)
{
    public static readonly RamBlast Instance = new();

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        if (!base.CanExecute(unit, data, target, out whyNot)) return false;

        if (unit.IsPlayer) return true; // player manual target!

        if (target.Unit is not { } tgt) { whyNot = "no target"; return false; }
        if (unit.Pos.ChebyshevDist(tgt.Pos) > MaxRange) { whyNot = "too far"; return false; }
        whyNot = "";
        return true;
    }

    private static readonly DamageRoll SlamDamage = new()
    {
        Formula = d(6),
        Type = DamageTypes.Blunt,
    };

    protected override void Execute(IUnit unit, Target target)
    {
        var defender = target.Unit!;
        using var ctx = PHContext.Create(unit, target);

        g.pline($"A ball of force hits {defender:the}!");

        DamageRoll dmg = new() { Formula = d(Math.Max(unit.EffectiveLevel / 3, 1), 6) + 2, Type = DamageTypes.Force, HalfOnSave = true };
        ctx.Damage.Add(dmg);

        bool saved = CheckFort(ctx, unit.GetSpellDC() - 2, "blast");
        DoDamage(ctx);

        if (!saved && !defender.IsDead)
        {
            // Push 1 tile away from caster
            Pos dir = (defender.Pos - unit.Pos).Signed;
            Pos pushTo = defender.Pos + dir;
            if (lvl.CanMoveTo(defender.Pos, pushTo, defender, true))
            {
                var into = lvl.UnitAt(pushTo);
                if (into != null)
                {
                    g.pline($"{defender:The} {VTense(defender, "slam")} into {into:an}!");
                    unit.Energy -= 2;
                    into.Energy -= 2;
                    using (var slamDmg = PHContext.Create(unit, target))
                    {
                        slamDmg.Damage.Add(SlamDamage);
                        DoDamage(slamDmg);
                    }
                    using (var intoDmg = PHContext.Create(unit, Target.From(into)))
                    {
                        intoDmg.Damage.Add(SlamDamage);
                        DoDamage(intoDmg);
                    }
                }
                else
                {
                    g.pline($"{defender:The} {VTense(defender, "slide")} backwards.");
                    lvl.MoveUnit(defender, pushTo, free: true);
                }
            }
            else
            {
                g.pline($"{defender:The} {VTense(defender, "slam")} into a {lvl[pushTo].Type!}");
                using var slamDmg = PHContext.Create(unit, target);
                slamDmg.Damage.Add(SlamDamage);
                DoDamage(slamDmg);
                unit.Energy -= 4;
            }
        }
    }
}

public static class MagicAccessories
{
    public static readonly ItemDef RingOfFeatherstep = new()
    {
        id = "ring_of_featherstep",
        Name = "ring of featherstep",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Green),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Grants immunity to difficult terrain.",
        Components = [FeatherStepBuff.Instance.WhenEquipped()],
    };

    public static readonly ItemDef SpiritsightRing = new()
    {
        id = "spiritsight_ring",
        Name = "spiritsight ring",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Magenta),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "This ring tickles your finger whenever a creature is nearby.",
        Components = [WarningBuff.Range8],
    };

    public static readonly ItemDef GrimRing = new()
    {
        id = "grim_ring",
        Name = "grim ring",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkGray),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Sculpted with the visage of a grinning skull. While wearing it, you can detect the presence of undead creatures.",
        Components = [DetectCreatureBuff.Undead8],
    };

    public static readonly ItemDef RingOfTheWild = new()
    {
        id = "ring_of_the_wild",
        Name = "ring of the wild",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkGreen),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Carved with images of beasts. While wearing it, you can detect the presence of nearby beasts.",
        Components = [DetectCreatureBuff.Beast8],
    };

    public static readonly ItemDef RingOfTheRam = new()
    {
        id = "ring_of_the_ram",
        Name = "ring of the ram",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkRed),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Shaped to look like the head of a ram, with curling horns. Can unleash a ram-shaped blast of force.",
        Components = [RamBlast.Instance.WhenEquipped()],
    };

    public static readonly ItemDef[] AllRings = [RingOfFeatherstep, SpiritsightRing, GrimRing, RingOfTheWild, RingOfTheRam];

    static MagicAccessories()
    {
        for (int i = 0; i < AllRings.Length; i++)
            AllRings[i].AppearanceIndex = i;
    }
}
