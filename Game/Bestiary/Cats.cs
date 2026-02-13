namespace Pathhack.Game.Bestiary;

public static class Cats
{
    public static readonly MonsterDef Cheetah = new()
    {
        id = "cheetah",
        Name = "cheetah",
        Family = "cat",
        Glyph = new('f', ConsoleColor.Yellow),
        HpPerLevel = 5,
        AC = -1,
        AttackBonus = -1,
        DamageBonus = 0,
        LandMove = 6,
        Unarmed = NaturalWeapons.Bite_1d4,
        Size = UnitSize.Medium,
        BaseLevel = 2,
        MinDepth = 2,
        MaxDepth = 6,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d4, NaturalWeapons.Claw_1d2, NaturalWeapons.Claw_1d2)),
            new GrantAction(QuickBite.Instance),
        ],
    };

    public static readonly MonsterDef Leopard = new()
    {
        id = "leopard",
        Name = "leopard",
        Family = "cat",
        Glyph = new('f', ConsoleColor.Yellow),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 0,
        DamageBonus = -1,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d6,
        Size = UnitSize.Medium,
        BaseLevel = 2,
        MinDepth = 2,
        MaxDepth = 6,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(Pounce.Instance),
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d4, NaturalWeapons.Claw_1d2, NaturalWeapons.Claw_1d2)),
        ],
    };

    public static readonly MonsterDef Panther = new()
    {
        id = "panther",
        Name = "panther",
        Family = "cat",
        Glyph = new('f', ConsoleColor.DarkGray),
        HpPerLevel = 7,
        AC = -1,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.LandMove25,
        Unarmed = NaturalWeapons.Bite_1d6,
        Size = UnitSize.Medium,
        BaseLevel = 3,
        MinDepth = 3,
        MaxDepth = 7,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d6, NaturalWeapons.Claw_1d2, NaturalWeapons.Claw_1d2)),
        ],
    };

    public static readonly MonsterDef Lion = new()
    {
        id = "lion",
        Name = "lion",
        Family = "cat",
        Glyph = new('f', ConsoleColor.DarkYellow),
        HpPerLevel = 10,
        AC = 1,
        AttackBonus = 0,
        DamageBonus = 0,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Bite_1d8,
        Size = UnitSize.Large,
        BaseLevel = 3,
        MinDepth = 4,
        MaxDepth = 8,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        GroupSize = GroupSize.Small,
        Components = [
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d2, NaturalWeapons.Claw_1d2)),
        ],
    };

    public static readonly MonsterDef Tiger = new()
    {
        id = "tiger",
        Name = "tiger",
        Family = "cat",
        Glyph = new('f', ConsoleColor.Red),
        HpPerLevel = 10,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d8,
        Size = UnitSize.Large,
        BaseLevel = 4,
        MinDepth = 5,
        MaxDepth = 10,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(Pounce.Instance),
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d3, NaturalWeapons.Claw_1d3)),
        ],
    };

    public static readonly MonsterDef Smilodon = new()
    {
        id = "smilodon",
        Name = "smilodon",
        Family = "cat",
        Glyph = new('f', ConsoleColor.Magenta),
        HpPerLevel = 12,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 1,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_2d6,
        Size = UnitSize.Large,
        BaseLevel = 7,
        MinDepth = 8,
        MaxDepth = 15,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        CreatureType = CreatureTypes.Beast,
        Components = [
            new GrantAction(Pounce.Instance),
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_2d6, NaturalWeapons.Claw_1d4, NaturalWeapons.Claw_1d4)),
        ],
    };

    public static readonly MonsterDef[] All = [Cheetah, Leopard, Panther, Lion, Tiger, Smilodon];
}

public class QuickBite() : ActionBrick("quick_bite")
{
    public static readonly QuickBite Instance = new();
    static readonly Item Weapon = Item.Create(NaturalWeapons.Bite_1d4);

    public override ActionCost GetCost(IUnit unit, object? data, Target target) => new(6);

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => unit.IsAdjacentPlan(target);

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null) => DoWeaponAttack(unit, target.Unit!, Weapon);
}

public class Pounce() : ActionBrick("pounce", TargetingType.Unit) 
{
    public static readonly Pounce Instance = new();
    const int Range = 2;

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        if (target.Unit is not { } tgt) return new(false, "no target");

        int dist = unit.Pos.ChebyshevDist(tgt.Pos);
        if (dist != Range + 1) return new(false, "wrong range");

        Pos dir = (tgt.Pos - unit.Pos).Signed;
        Pos landing = tgt.Pos - dir;

        if (!lvl.CanMoveTo(unit.Pos, landing, unit) || lvl.UnitAt(landing) != null) return new(false, "blocked");

        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        var tgt = target.Unit!;
        Pos dir = (tgt.Pos - unit.Pos).Signed;
        Pos landing = tgt.Pos - dir;

        g.pline($"{unit:The} pounces!");
        lvl.MoveUnit(unit, landing, true);

        var fullAttack = unit.Actions.OfType<FullAttack>().FirstOrDefault();
        if (fullAttack != null)
            fullAttack.Execute(unit, null, target);
        else
            DoWeaponAttack(unit, tgt, unit.GetWieldedItem());
    }
}
