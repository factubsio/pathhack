namespace Pathhack.Game.Bestiary;

public static class Cats
{
    public static readonly MonsterFamily Family = new("cat");

    static MonsterDef C(string id, string name, int level, ConsoleColor color,
        LogicBrick[] components, int hp = 8, int ac = 0, int ab = 0, int dmg = 0,
        UnitSize size = UnitSize.Medium, int maxDepth = 99,
        ActionCost? speed = null, WeaponDef? unarmed = null,
        GroupSize group = GroupSize.None)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = Family,
            CreatureType = CreatureTypes.Beast,
            Glyph = new('f', color),
            HpPerLevel = hp,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            LandMove = speed ?? ActionCosts.StandardLandMove,
            Unarmed = unarmed ?? NaturalWeapons.Bite_1d6,
            Size = size,
            BaseLevel = level,
            MaxDepth = maxDepth,
            GroupSize = group,
            MoralAxis = MoralAxis.Neutral,
            EthicalAxis = EthicalAxis.Neutral,
            Components = components,
        };
    }

#pragma warning disable BEE008 // full attack is preferred over quick bite in this case
    public static readonly MonsterDef Cheetah = C("cheetah", "cheetah", 2, ConsoleColor.Yellow,
        [new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d4, NaturalWeapons.Claw_1d2, NaturalWeapons.Claw_1d2)),
         new GrantAction(QuickBite.Instance)],
        hp: 5, ac: -1, ab: -1, maxDepth: 6, speed: (ActionCost)6, unarmed: NaturalWeapons.Bite_1d4);
#pragma warning restore BEE008

    public static readonly MonsterDef Leopard = C("leopard", "leopard", 2, ConsoleColor.Yellow,
        [new GrantAction(Pounce.Instance),
         new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d4, NaturalWeapons.Claw_1d2, NaturalWeapons.Claw_1d2))],
        hp: 6, dmg: -1, maxDepth: 6);

    public static readonly MonsterDef Panther = C("panther", "panther", 3, ConsoleColor.DarkGray,
        [new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d6, NaturalWeapons.Claw_1d2, NaturalWeapons.Claw_1d2))],
        hp: 7, ac: -1, maxDepth: 7, speed: ActionCosts.LandMove25);

    public static readonly MonsterDef Lion = C("lion", "lion", 3, ConsoleColor.DarkYellow,
        [new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d2, NaturalWeapons.Claw_1d2))],
        ac: 1, size: UnitSize.Large, maxDepth: 8, speed: ActionCosts.LandMove20,
        unarmed: NaturalWeapons.Bite_1d8, group: GroupSize.Small);

    public static readonly MonsterDef Tiger = C("tiger", "tiger", 4, ConsoleColor.Red,
        [new GrantAction(Pounce.Instance),
         new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d3, NaturalWeapons.Claw_1d3))],
        ac: 1, ab: 1, size: UnitSize.Large, maxDepth: 10, unarmed: NaturalWeapons.Bite_1d8);

    public static readonly MonsterDef Smilodon = C("smilodon", "smilodon", 7, ConsoleColor.Magenta,
        [new GrantAction(Pounce.Instance),
         new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_2d6, NaturalWeapons.Claw_1d4, NaturalWeapons.Claw_1d4))],
        ac: 1, ab: 1, dmg: 1, size: UnitSize.Large, maxDepth: 15, unarmed: NaturalWeapons.Bite_2d6);

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

        g.YouObserve(unit, $"{unit:The} pounces!");
        lvl.MoveUnit(unit, landing, true);

        var fullAttack = unit.Actions.OfType<FullAttack>().FirstOrDefault();
        if (fullAttack != null)
            fullAttack.Execute(unit, null, target);
        else
            DoWeaponAttack(unit, tgt, unit.GetWieldedItem());
    }
}
