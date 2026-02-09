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
        SpawnWeight = 2,
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
        SpawnWeight = 2,
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
        SpawnWeight = 2,
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
        SpawnWeight = 1,
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
        SpawnWeight = 1,
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
        SpawnWeight = 1,
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

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => unit.IsAdjacent(target, out whyNot);

    public override void Execute(IUnit unit, object? data, Target target) => g.Attack(unit, target.Unit!, Weapon);
}

public class Pounce() : ActionBrick("pounce", TargetingType.Unit) 
{
    public static readonly Pounce Instance = new();
    const int Range = 2;

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "no target";
        if (target.Unit is not { } tgt) return false;

        int dist = unit.Pos.ChebyshevDist(tgt.Pos);
        whyNot = "wrong range";
        if (dist != Range + 1) return false; // must be exactly 3 away to land adjacent

        Pos dir = (tgt.Pos - unit.Pos).Signed;
        Pos landing = tgt.Pos - dir;

        whyNot = "blocked";
        if (!lvl.CanMoveTo(unit.Pos, landing, unit) || lvl.UnitAt(landing) != null) return false;

        whyNot = "";
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        var tgt = target.Unit!;
        Pos dir = (tgt.Pos - unit.Pos).Signed;
        Pos landing = tgt.Pos - dir;

        g.pline($"{unit:The} pounces!");
        // The move is free, we pay the cost as the action
        lvl.MoveUnit(unit, landing, true);

        // Find and execute full attack
        var fullAttack = unit.Actions.OfType<FullAttack>().FirstOrDefault();
        if (fullAttack != null)
            fullAttack.Execute(unit, null, target);
        else
            g.Attack(unit, tgt, unit.GetWieldedItem());
    }
}

public class FullAttack(string name, params WeaponDef[] attacks) : ActionBrick($"full:{name}:{string.Join(",", attacks.Select(a => a.id ?? a.Name))}")
{
    readonly Item[] _weapons = [.. attacks.Select(Item.Create)];

    public override void Execute(IUnit unit, object? data, Target target)
    {
        var tgt = target.Unit!;
        for (int i = 0; i < _weapons.Length; i++)
        {
            Item? weapon = _weapons[i];
            if (tgt.IsDead) break; // rampage to others here!!!
            g.Attack(unit, tgt, weapon, attackBonus: i > 0 ? -5 : 0);
        }
    }

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => unit.IsAdjacent(target, out whyNot);
}
