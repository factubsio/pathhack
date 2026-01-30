using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Pathhack.Game.Bestiary;

// --- Gremlin Components ---

// Pugwampi: curse player's weapon when in range (pooled charge)
public class CurseWeaponInRange(int range) : ActionBrick
{
    public static readonly CurseWeaponInRange Instance = new(3);
    const int CurseDuration = 10;

    public override string Name => "Curse Weapon";
    public override TargetingType Targeting => TargetingType.None;

    public const string Resource = "pugwampi_curse";

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "";
        if (!unit.HasCharge(Resource, out whyNot)) return false;

        int dist = unit.Pos.ChebyshevDist(u.Pos);
        if (dist > range) { whyNot = "out of range"; return false; }

        var weapon = u.GetWieldedItem();
        if (weapon == null) { whyNot = "no weapon"; return false; }
        if (weapon.Def is WeaponDef w && w.Category == WeaponCategory.Unarmed) { whyNot = "unarmed"; return false; }

        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        if (!unit.TryUseCharge(Resource)) return;
        var weapon = u.GetWieldedItem();
        weapon.AddFact(new WeaponCurse(CurseDuration));
        g.pline($"{unit:The} curses your {weapon}!");
    }
}

// Curse fact applied to weapon: -2 attack for duration
public class WeaponCurse(int duration) : TimedBrick(duration)
{
    public override string? BuffName => "Pugwampi's Ill Fortune";

    public override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not Item item) return;
        if (ctx.Source != item.Holder) return;
        if (ctx.Weapon != item) return;
        ctx.Check!.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, -2, "cursed"));
    }

}

public class JinkinCurse() : TimedBrick(10)
{
    public override string? BuffName => "Jinkin's Ill Fortune";

    public override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (context.Source == fact.Entity)
        {
            context.Check!.Disadvantage++;
            fact.Remove();
        }
    }
}

// Prone: -2 AC, half speed, 2 rounds
public class Prone() : TimedBrick(2)
{
    public override string? BuffName => "Hamstrung";

    public override object? OnQuery(Fact fact, string key, string? arg)
    {
        if (key == "ac") return new Modifier(ModifierCategory.UntypedStackable, -2, "prone");
        if (key == "speed_mult") return 0.5;
        return null;
    }
}

// FilthFever: periodic damage/penalty until cured
public class FilthFever : LogicBrick
{
    public override string? BuffName => "Filth Fever";
    public static readonly FilthFever Instance = new();
    public override bool IsBuff => true;
    public override bool IsActive => true;

    public override void OnRoundStart(Fact fact, PHContext ctx)
    {
        // TODO: periodic Con damage or HP drain
    }
}

// Very Drunk Jinkin: 30% dodge
public class DrunkenDodge : LogicBrick
{
    public static readonly DrunkenDodge Instance = new();

    public override void OnBeforeDefendRoll(Fact fact, PHContext ctx)
    {
        if (g.Rn2(100) < 30)
        {
            ctx.Check!.ForcedResult = false;
        }
    }
}

// Very Drunk Jinkin: vomit acid
public class VomitAcid : ActionBrick
{
    public static readonly VomitAcid Instance = new();
    public override string Name => "Vomit";
    public override TargetingType Targeting => TargetingType.Unit;

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "no adjacent target";
        // TODO: check adjacent
        return false;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        // TODO: acid damage to target
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
        Glyph = new('m', ConsoleColor.Blue),
        HP = 4,
        AC = 11,
        AttackBonus = 1,
        DamageBonus = -2,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        CR = -1,
        SpawnWeight = 3,
        MinDepth = 1,
        MaxDepth = 3,
        Components = [
            new EquipSet(
                new Outfit(1, new OutfitItem(MundaneArmory.Dart, 100, d(2) + 2)),
                new Outfit(1, new OutfitItem(MundaneArmory.Dagger))
            ),
            new GrantAction(AttackWithWeapon.Instance),
            new GrantAction(new NaturalAttack(NaturalWeapons.Fist)),
        ],
    };

    public static readonly MonsterDef Pugwampi = new()
    {
        id = "pugwampi",
        Name = "pugwampi",
        Glyph = new('m', ConsoleColor.Yellow),
        HP = 6,
        AC = 12,
        AttackBonus = 2,
        DamageBonus = -1,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Bite_1d3,
        Size = UnitSize.Small,
        CR = 0,
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
        Glyph = new('m', ConsoleColor.Magenta),
        HP = 8,
        AC = 14,
        AttackBonus = 4,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Claw_1d4,
        Size = UnitSize.Small,
        CR = 1,
        Components = [
            new ApplyFactOnAttackHit<JinkinCurse>(() => new()),
            ..CommonEquip
        ],
    };

    public static readonly MonsterDef Nuglub = new()
    {
        id = "nuglub",
        Name = "nuglub",
        Glyph = new('m', ConsoleColor.Red),
        HP = 12,
        AC = 15,
        AttackBonus = 5,
        DamageBonus = 2,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d3,
        Size = UnitSize.Small,
        CR = 2,
        Components = [
            new ApplyFactOnAttackHit<Prone>(() => new()),
            ..CommonEquip,
        ],
    };

    public static readonly MonsterDef Grimple = new()
    {
        id = "grimple",
        Name = "grimple",
        Glyph = new('m', ConsoleColor.DarkGreen),
        HP = 4,
        AC = 12,
        AttackBonus = 2,
        DamageBonus = -2,
        LandMove = ActionCosts.LandMove20,
        // TODO: flight
        Unarmed = NaturalWeapons.Bite_1d3,
        Size = UnitSize.Small,
        CR = -1,
        Components = [
            new ApplyFactOnAttackHit<FilthFever>(() => new()),
            ..CommonEquip,
        ],
    };

    public static readonly MonsterDef VeryDrunkJinkin = new()
    {
        id = "drunk_jinkin",
        Name = "very drunk jinkin",
        Glyph = new('m', ConsoleColor.DarkMagenta),
        HP = 6,
        AC = 10,
        AttackBonus = 2,
        DamageBonus = -1,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Claw_1d4,
        Size = UnitSize.Small,
        CR = -1,
        Brain = new VeryDrunkJinkinBrain(),
        Components = [
            DrunkenDodge.Instance,
        ],
    };

    public static readonly MonsterDef[] All = [Mitflit, Pugwampi, Jinkin, Nuglub, Grimple, VeryDrunkJinkin];
}

public class Nauseated(int time) : TimedBrick(time)
{
    public override string? BuffName => "Nauseated";
    public override void OnBeforeCheck(Fact fact, PHContext context)
    {
        if (context.Source == fact.Entity)
        {
            context.Check!.Disadvantage++;
            fact.Remove();
        }
    }
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
                if (CreateAndDoCheck(ctx, "reflex_save", 10, "Vomit spray", true))
                {
                    g.pline($"{m:The} throws up but misses {tgt:the}.");
                    return true;
                }
                else
                {
                    g.pline($"{m:The} throws up all over {tgt:the}.");
                }

                ctx.Damage.Add(new()
                {
                    Formula = d(6) + 2,
                    Type = DamageTypes.Acid,
                });
                DoDamage(ctx);

                // yucky yucky
                if (CreateAndDoCheck(ctx, "fortitude_save", 14, "Nauseated", false)) return true;
                g.pline($"{tgt:The} can barely hold {tgt:own} own lunch down.");
                tgt.AddUniqueFact(x => x.Brick is Nauseated, () => new Nauseated(4));
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
                    g.Attack(m, tgt, m.GetWieldedItem());
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
                    g.pline($"{m:The} bumps {m:own} head against the wall!");
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
