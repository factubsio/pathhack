namespace Pathhack.Game;

public class PotencyACBuff : LogicBrick
{
    public static readonly PotencyACBuff Instance = new();
    public override bool RequiresEquipped => true;

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "ac" && fact.Entity is Item item
            ? new Modifier(ModifierCategory.CircumstanceBonus, Math.Max(1, item.Potency), "ring of protection")
            : null;
}

public class PotencyAttackBuff : LogicBrick
{
    public static readonly PotencyAttackBuff Instance = new();
    public override bool RequiresEquipped => true;

    protected override void OnBeforeAttackRoll(Fact fact, PHContext context)
    {
        if (fact.Entity is Item item)
            context.Check!.Modifiers.AddModifier(new(ModifierCategory.ItemBonus, Math.Max(1, item.Potency), "ring of accurate strikes"));
    }
}

public class PotencyEnergyResist(DamageType type) : LogicBrick
{
    public static readonly PotencyEnergyResist Fire = new(DamageTypes.Fire);
    public static readonly PotencyEnergyResist Cold = new(DamageTypes.Cold);
    public static readonly PotencyEnergyResist Shock = new(DamageTypes.Shock);
    public static readonly PotencyEnergyResist Acid = new(DamageTypes.Acid);

    public override bool RequiresEquipped => true;

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not Item item) return;
        int dr = Math.Max(1, item.Potency) * 5;
        foreach (var roll in ctx.Damage)
            if (roll.Type == type) roll.ApplyDR(dr);
    }
}

public class FreeActionBuff : LogicBrick
{
    public static readonly FreeActionBuff Instance = new();
    public override bool RequiresEquipped => true;

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "paralyzed" or "slowed" or "webbed" => false,
        _ => null
    };
}

public class SaveAdvantageBuff(string saveKey) : LogicBrick
{
    public static readonly SaveAdvantageBuff Reflex = new(Check.Reflex);
    public static readonly SaveAdvantageBuff Fortitude = new(Check.Fort);
    public static readonly SaveAdvantageBuff Will = new(Check.Will);

    public override bool RequiresEquipped => true;

    protected override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (context.IsCheckingOwnerOf(fact) && context.Check!.Key == saveKey)
            context.Check.Advantage++;
    }
}

public class FastHealingBuff : LogicBrick
{
    public static readonly FastHealingBuff Instance = new();
    public override bool IsActive => true;
    public override bool RequiresEquipped => true;

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is Item { Holder: { } unit })
            g.DoHeal(unit, unit, 1);
    }
}

public class TeleportationCurseBuff : LogicBrick
{
    public static readonly TeleportationCurseBuff Instance = new();
    public override bool IsActive => true;
    public override bool RequiresEquipped => true;

    protected override void OnRoundStart(Fact fact)
    {
        if (fact.Entity is not Item { Holder: { } unit }) return;
        if (g.Rn2(50) != 0) return;

        var dest = lvl.FindLocation(p => lvl[p].IsPassable && lvl.NoUnit(p));
        if (dest == null) return;

        g.YouObserveSelf(unit, "You are suddenly somewhere else!", $"{unit:The} disappears!");
        lvl.MoveUnit(unit, dest.Value);
    }
}

public class InvisibilityRingBuff : LogicBrick<InvisibilityRingBuff.State>
{
    public static readonly InvisibilityRingBuff Instance = new();

    public class State { public bool Suppressed; }

    public override bool IsActive => true;
    public override bool RequiresEquipped => true;

    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "invisible" && !X(fact).Suppressed ? true : null;

    protected override void OnAfterAttackRoll(Fact fact, PHContext context)
    {
        if (fact.Entity is Item { Holder: { } holder } && context.Source == holder)
            X(fact).Suppressed = true;
    }

    protected override void OnRoundEnd(Fact fact)
    {
        if (!X(fact).Suppressed) return;
        if (fact.Entity is not Item { Holder: { } unit }) return;

        if (!lvl.LiveUnits.OfType<Monster>().Any(m => m.CanSeeYou))
            X(fact).Suppressed = false;
    }
}

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

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var basePlan = base.CanExecute(unit, data, target);
        if (!basePlan) return basePlan;

        if (unit.IsPlayer) return true; // player manual target!

        if (target.Unit is not { } tgt) return new(false, "no target");
        if (unit.Pos.ChebyshevDist(tgt.Pos) > MaxRange) return new(false, "too far");
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
        Price = 300,
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
        Price = 200,
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
        Price = 200,
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
        Price = 200,
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
        Price = 400,
    };

    public static readonly ItemDef RingOfProtection = new()
    {
        id = "ring_of_protection",
        Name = "ring of protection",
        Glyph = new(ItemClasses.Ring, ConsoleColor.White),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "A simple band that wards the wearer from harm.",
        Components = [PotencyACBuff.Instance.WhenEquipped()],
        Price = 200,
        CanHavePotency = true,
    };

    public static readonly ItemDef RingOfSeeInvisible = new()
    {
        id = "ring_of_see_invisible",
        Name = "ring of see invisible",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Cyan),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "The wearer can perceive creatures and objects that are invisible.",
        Components = [new QueryBrick("see_invisible", true).WhenEquipped()],
        Price = 300,
    };

    public static readonly ItemDef RingOfStealth = new()
    {
        id = "ring_of_stealth",
        Name = "ring of stealth",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkGray),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Muffles the wearer's presence, making it harder to wake sleeping creatures.",
        Components = [new QueryBrick("stealth", true).WhenEquipped()],
        Price = 200,
    };

    public static readonly ItemDef RingOfFireResistance = new()
    {
        id = "ring_of_fire_resistance",
        Name = "ring of fire resistance",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Red),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Warm to the touch. Protects the wearer from fire.",
        Components = [PotencyEnergyResist.Fire.WhenEquipped()],
        Price = 300,
        CanHavePotency = true,
    };

    public static readonly ItemDef RingOfColdResistance = new()
    {
        id = "ring_of_cold_resistance",
        Name = "ring of cold resistance",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Blue),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Cool to the touch. Protects the wearer from cold.",
        Components = [PotencyEnergyResist.Cold.WhenEquipped()],
        Price = 300,
        CanHavePotency = true,
    };

    public static readonly ItemDef RingOfShockResistance = new()
    {
        id = "ring_of_shock_resistance",
        Name = "ring of shock resistance",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Yellow),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Tingles faintly. Protects the wearer from electricity.",
        Components = [PotencyEnergyResist.Shock.WhenEquipped()],
        Price = 300,
        CanHavePotency = true,
    };

    public static readonly ItemDef RingOfAcidResistance = new()
    {
        id = "ring_of_acid_resistance",
        Name = "ring of acid resistance",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkYellow),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Slightly pitted on the surface. Protects the wearer from acid.",
        Components = [PotencyEnergyResist.Acid.WhenEquipped()],
        Price = 300,
        CanHavePotency = true,
    };

    public static readonly ItemDef RingOfFreeAction = new()
    {
        id = "ring_of_free_action",
        Name = "ring of free action",
        Glyph = new(ItemClasses.Ring, ConsoleColor.White),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "The wearer cannot be paralyzed, slowed, or webbed.",
        Components = [FreeActionBuff.Instance.WhenEquipped()],
        Price = 500,
    };

    public static readonly ItemDef RingOfReflex = new()
    {
        id = "ring_of_reflex",
        Name = "ring of reflex",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkCyan),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Quickens the wearer's reactions.",
        Components = [SaveAdvantageBuff.Reflex.WhenEquipped()],
        Price = 300,
    };

    public static readonly ItemDef RingOfFortitude = new()
    {
        id = "ring_of_fortitude",
        Name = "ring of fortitude",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkRed),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Bolsters the wearer's constitution.",
        Components = [SaveAdvantageBuff.Fortitude.WhenEquipped()],
        Price = 300,
    };

    public static readonly ItemDef RingOfWill = new()
    {
        id = "ring_of_will",
        Name = "ring of will",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkMagenta),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Steels the wearer's mind against intrusion.",
        Components = [SaveAdvantageBuff.Will.WhenEquipped()],
        Price = 300,
    };

    public static readonly ItemDef RingOfFastHealing = new()
    {
        id = "ring_of_fast_healing",
        Name = "ring of fast healing",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Green),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Slowly mends the wearer's wounds.",
        Components = [FastHealingBuff.Instance.WhenEquipped()],
        Price = 400,
    };

    public static readonly ItemDef RingOfAccurateStrikes = new()
    {
        id = "ring_of_accurate_strikes",
        Name = "ring of accurate strikes",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkYellow),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Guides the wearer's hand in combat.",
        Components = [PotencyAttackBuff.Instance.WhenEquipped()],
        Price = 300,
        CanHavePotency = true,
    };

    public static readonly ItemDef RingOfTeleportControl = new()
    {
        id = "ring_of_teleport_control",
        Name = "ring of teleport control",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Magenta),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Allows the wearer to choose their destination when teleported.",
        Components = [new QueryBrick("teleport_control", true).WhenEquipped()],
        Price = 400,
    };

    public static readonly ItemDef RingOfTeleportation = new()
    {
        id = "ring_of_teleportation",
        Name = "ring of teleportation",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkMagenta),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Occasionally teleports the wearer to a random location.",
        Components = [TeleportationCurseBuff.Instance.WhenEquipped()],
        Price = 100,
    };

    public static readonly ItemDef RingOfInvisibility = new()
    {
        id = "ring_of_invisibility",
        Name = "ring of invisibility",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkGray),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Renders the wearer invisible. Attacking breaks the effect until unseen again.",
        Components = [InvisibilityRingBuff.Instance.WhenEquipped()],
        Price = 500,
    };

    public static readonly ItemDef RingOfAggravation = new()
    {
        id = "ring_of_aggravation",
        Name = "ring of aggravation",
        Glyph = new(ItemClasses.Ring, ConsoleColor.Red),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "Monsters always know where the wearer is.",
        Components = [new QueryBrick("aggravate_monster", true).WhenEquipped()],
        Price = 50,
    };

    public static readonly ItemDef RingOfHunger = new()
    {
        id = "ring_of_hunger",
        Name = "ring of hunger",
        Glyph = new(ItemClasses.Ring, ConsoleColor.DarkGreen),
        DefaultEquipSlot = ItemSlots.Ring,
        AppearanceCategory = AppearanceCategory.Ring,
        PokedexDescription = "The wearer feels constantly famished.",
        Components = [new QueryBrick("hunger_rate", 2).WhenEquipped()],
        Price = 50,
    };

    public static readonly ItemDef[] AllRings =
    [
        RingOfFeatherstep, SpiritsightRing, GrimRing, RingOfTheWild, RingOfTheRam,
        RingOfProtection, RingOfSeeInvisible, RingOfStealth,
        RingOfFireResistance, RingOfColdResistance, RingOfShockResistance, RingOfAcidResistance,
        RingOfFreeAction, RingOfReflex, RingOfFortitude, RingOfWill,
        RingOfFastHealing, RingOfAccurateStrikes,
        RingOfTeleportControl, RingOfTeleportation,
        RingOfInvisibility, RingOfAggravation, RingOfHunger,
    ];

    static MagicAccessories()
    {
        for (int i = 0; i < AllRings.Length; i++)
            AllRings[i].AppearanceIndex = i;
    }
}
