namespace Pathhack.Game.Bestiary;

// --- Gremlin Components ---

// Pugwampi: curse player's weapon when in range (pooled charge)
public class CurseWeaponInRange(int range) : ActionBrick("Curse Weapon")
{
    public static readonly CurseWeaponInRange Instance = new(3);
    const int CurseDuration = 10;

    public const string Resource = "pugwampi_curse";

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        if (!unit.HasCharge(Resource, out var whyNot)) return new(false, whyNot);

        int dist = unit.Pos.ChebyshevDist(u.Pos);
        if (dist > range) return new(false, "out of range");

        var weapon = u.GetWieldedItem();
        if (weapon == null) return new(false, "no weapon");
        if (weapon.Def is WeaponDef w && w.Category == WeaponCategory.Unarmed) return new(false, "unarmed");

        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        if (!unit.TryUseCharge(Resource)) return;
        var weapon = u.GetWieldedItem();
        weapon.AddFact(WeaponCurse.Instance, duration: CurseDuration);
        g.YouObserve(unit, $"{unit:The} curses your {weapon}!", $"something curse your {weapon}!", 100);
    }
}

// Curse fact applied to weapon: -2 attack for duration
public class WeaponCurse : LogicBrick
{
    public static readonly WeaponCurse Instance = new();
    public override string Id => "gremlin:weapon_curse";
    public override bool IsBuff => true;
    public override bool IsActive => true;
    public override string? BuffName => "Pugwampi's Ill Fortune";

    protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not Item item) return;
        if (ctx.Source != item.Holder) return;
        if (ctx.Weapon != item) return;
        ctx.Check!.Modifiers.Untyped(-2, "cursed");
    }
}

public class JinkinCurse : LogicBrick
{
    public static readonly JinkinCurse Instance = new();
    public override string Id => "gremlin:jinkin_curse";
    public override bool IsBuff => true;
    public override bool IsActive => true;
    public override string? BuffName => "Jinkin's Ill Fortune";

    protected override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (context.IsCheckingOwnerOf(fact))
        {
            context.Check!.Disadvantage++;
            fact.Remove();
        }
    }
}

// FilthFever: periodic damage/penalty until cured
public class FilthFever : LogicBrick
{
    public override string Id => "gremlin:filth_fever";
    public override string? BuffName => "Filth Fever";
    public static readonly FilthFever Instance = new();
    public override bool IsBuff => true;
    public override bool IsActive => true;

    protected override void OnRoundStart(Fact fact)
    {
        // TODO: periodic Con damage or HP drain
    }
}

// Very Drunk Jinkin: 30% dodge
public class DrunkenDodge : LogicBrick
{
    public static readonly DrunkenDodge Instance = new();
    public override string Id => "gremlin:drunken_dodge";
    public override string? PokedexDescription => "30% dodge (drunken)";

    protected override void OnBeforeDefendRoll(Fact fact, PHContext ctx)
    {
        if (g.Rn2(100) < 30)
        {
            ctx.Check!.ForcedResult = false;
        }
    }
}

// --- Monster Definitions ---
public static class Gremlins
{
    private static readonly LogicBrick[] CommonEquip = [
        EquipSet.Roll(MundaneArmory.Dagger, 50),
        new GrantAction(AttackWithWeapon.Instance),
        new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d3)),
    ];
    public static readonly MonsterDef Mitflit = new()
    {
        id = "mitflit",
        Name = "mitflit",
        Family = "gremlin",
        CreatureType = CreatureTypes.Fey,
        Glyph = new('m', ConsoleColor.Blue),
        HpPerLevel = 5,
        AC = -1,
        AttackBonus = -1,
        DamageBonus = -2,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = -1,
        MinDepth = 1,
        MaxDepth = 3,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            new EquipSet(
                new Outfit(1, new OutfitItem(MundaneArmory.Dart, 100, d(2) + 2)),
                new Outfit(1, new OutfitItem(MundaneArmory.Dagger))
            ),
            new GrantAction(AttackWithWeapon.Instance),
        ],
    };

    public static readonly MonsterDef Pugwampi = new()
    {
        id = "pugwampi",
        Name = "pugwampi",
        Family = "gremlin",
        CreatureType = CreatureTypes.Fey,
        Glyph = new('m', ConsoleColor.Yellow),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = -1,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Bite_1d3,
        Size = UnitSize.Small,
        BaseLevel = 0,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            new GrantPool(CurseWeaponInRange.Resource, 1, 10),
            new GrantAction(CurseWeaponInRange.Instance),
            ..CommonEquip
        ],
    };

    public static readonly MonsterDef Jinkin = new()
    {
        id = "jinkin",
        Name = "jinkin",
        Family = "gremlin",
        CreatureType = CreatureTypes.Fey,
        Glyph = new('m', ConsoleColor.Magenta),
        HpPerLevel = 6,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Claw_1d4,
        Size = UnitSize.Small,
        BaseLevel = 1,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            new ApplyFactOnAttackHit(JinkinCurse.Instance, duration: 10),
            ..CommonEquip
        ],
    };

    public static readonly MonsterDef Nuglub = new()
    {
        id = "nuglub",
        Name = "nuglub",
        Family = "gremlin",
        CreatureType = CreatureTypes.Fey,
        Glyph = new('m', ConsoleColor.Red),
        HpPerLevel = 7,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 2,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d3,
        Size = UnitSize.Small,
        BaseLevel = 2,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            new ApplyFactOnAttackHit(ProneBuff.Instance.Timed(), duration: 2),
            ..CommonEquip,
        ],
    };

    public static readonly MonsterDef Grimple = new()
    {
        id = "grimple",
        Name = "grimple",
        Family = "gremlin",
        CreatureType = CreatureTypes.Fey,
        Glyph = new('m', ConsoleColor.DarkGreen),
        HpPerLevel = 5,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = -2,
        LandMove = ActionCosts.LandMove20,
        // TODO: flight
        Unarmed = NaturalWeapons.Bite_1d3,
        Size = UnitSize.Small,
        BaseLevel = -1,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Components = [
            new ApplyFactOnAttackHit(FilthFever.Instance),
            ..CommonEquip,
        ],
    };

    public static readonly MonsterDef VeryDrunkJinkin = new()
    {
        id = "drunk_jinkin",
        Name = "very drunk jinkin",
        Family = "gremlin",
        CreatureType = CreatureTypes.Fey,
        Glyph = new('m', ConsoleColor.DarkMagenta),
        HpPerLevel = 6,
        AC = -2,
        AttackBonus = 0,
        DamageBonus = -1,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Claw_1d4,
        Size = UnitSize.Small,
        BaseLevel = -1,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Chaotic,
        Brain = new VeryDrunkJinkinBrain(),
        Components = [
            DrunkenDodge.Instance,
        ],
    };

    public static readonly MonsterDef[] All = [Mitflit, Pugwampi, Jinkin, Nuglub, Grimple, VeryDrunkJinkin];
}

public class VeryDrunkJinkinBrain : MonsterBrain
{
    // 10%: vomit on adjacent (reflex avoids, otherwise acid damage and nauseate on failed fort save)
    // 40%: stumble in random direction, 50/50 to attack if blocked
    // 50%: stupor (do nothing)
    public override bool DoTurn(Monster m)
    {
        m.Energy -= ActionCosts.OneAction.Value;

        int roll = g.Rn2(100);

        if (roll < 10)
        {
            Pos dir = Pos.AllDirs.Pick();
            Pos to = m.Pos + dir;

            // If we're gonna hurl may as well make use of it?
            if (upos.ChebyshevDist(m.Pos) == 1 && g.Rn2(3) == 0)
                to = upos;

            if (lvl.UnitAt(m.Pos + dir) is {} tgt)
            {
                using var ctx = PHContext.Create(m, Target.From(tgt));

                // dodge the acid bullet
                if (CheckReflex(ctx, 10, "acid"))
                {
                    g.YouObserve(m, $"{m:The} throws up but misses {tgt:the}.");
                    return true;
                }
                else
                {
                    g.YouObserve(m, $"{m:The} throws up all over {tgt:the}.");
                }

                ctx.Damage.Add(new()
                {
                    Formula = d(6) + 2,
                    Type = DamageTypes.Acid,
                });
                DoDamage(ctx);

                // yucky yucky
                if (CheckFort(ctx, 14, "nauseated")) return true;
                g.YouObserve(tgt, $"{tgt:The} can barely hold {tgt:own} own lunch down.");
                tgt.AddFact(NauseatedBuff.Instance.Timed(), duration: 4);
            }
        }
        else if (roll < 50)
        {
            Pos dir = Pos.AllDirs.Pick();
            Pos to = m.Pos + dir;
            if (lvl.UnitAt(to) is {} tgt)
            {
                // GET OUT OF MY WAY
                if (g.Rn2(2) == 0)
                    DoWeaponAttack(m, tgt, m.GetWieldedItem());
            }
            else if (lvl.CanMoveTo(m.Pos, to, m))
            {
                lvl.MoveUnit(m, to);
            }
            else if (lvl[to].Type == TileType.Wall)
            {
                // 33% chance we bump our head against the wall, oops
                if (g.Rn2(1) == 0)
                {
                    g.YouObserve(m, $"{m:The} bumps {m:own} head against the wall!");
                    using var ctx = PHContext.Create(m, Target.From(m));
                    ctx.DeathReason = "the bottle";
                    ctx.Damage.Add(new()
                    {
                        Formula = d(6) + 6,
                        Type = DamageTypes.Blunt,
                    });
                    DoDamage(ctx);
                }

            }
        }
        else { } //stupor

        return true;
    }
}
