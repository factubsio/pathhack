namespace Pathhack.Game.Bestiary;

public static class Cats
{
    public static readonly MonsterDef Cheetah = new()
    {
        id = "cheetah",
        Name = "cheetah",
        Glyph = new('f', ConsoleColor.Yellow),
        HpPerLevel = 5,
        AC = -1,
        AttackBonus = 1,
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
            new GrantAction(QuickBite.Instance),
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d4, NaturalWeapons.Claw_1d3, NaturalWeapons.Claw_1d3)),
        ],
    };

    public static readonly MonsterDef Leopard = new()
    {
        id = "leopard",
        Name = "leopard",
        Glyph = new('f', ConsoleColor.Yellow),
        HpPerLevel = 6,
        AC = 0,
        AttackBonus = 1,
        DamageBonus = 1,
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
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d6, NaturalWeapons.Claw_1d4, NaturalWeapons.Claw_1d4)),
        ],
    };

    public static readonly MonsterDef Panther = new()
    {
        id = "panther",
        Name = "panther",
        Glyph = new('f', ConsoleColor.DarkGray),
        HpPerLevel = 7,
        AC = 0,
        AttackBonus = 2,
        DamageBonus = 2,
        LandMove = ActionCosts.StandardLandMove,
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
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d6, NaturalWeapons.Claw_1d4, NaturalWeapons.Claw_1d4)),
        ],
    };

    public static readonly MonsterDef Lion = new()
    {
        id = "lion",
        Name = "lion",
        Glyph = new('f', ConsoleColor.DarkYellow),
        HpPerLevel = 10,
        AC = 1,
        AttackBonus = 1,
        DamageBonus = 3,
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
        Components = [
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d6, NaturalWeapons.Claw_1d6)),
        ],
    };

    public static readonly MonsterDef Tiger = new()
    {
        id = "tiger",
        Name = "tiger",
        Glyph = new('f', ConsoleColor.Red),
        HpPerLevel = 10,
        AC = 1,
        AttackBonus = 2,
        DamageBonus = 4,
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
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_1d8, NaturalWeapons.Claw_1d6, NaturalWeapons.Claw_1d6)),
        ],
    };

    public static readonly MonsterDef Smilodon = new()
    {
        id = "smilodon",
        Name = "smilodon",
        Glyph = new('f', ConsoleColor.Magenta),
        HpPerLevel = 12,
        AC = 2,
        AttackBonus = 3,
        DamageBonus = 5,
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
            new GrantAction(new FullAttack("maul", NaturalWeapons.Bite_2d6, NaturalWeapons.Claw_1d8, NaturalWeapons.Claw_1d8)),
        ],
    };

    public static readonly MonsterDef[] All = [Cheetah, Leopard, Panther, Lion, Tiger, Smilodon];
}

public class QuickBite : ActionBrick
{
    public static readonly QuickBite Instance = new();
    static readonly Item Weapon = Item.Create(NaturalWeapons.Bite_1d4);
    QuickBite() : base("quick_bite") { }

    public override ActionCost GetCost(IUnit unit, object? data, Target target) => new(6);

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "not adjacent";
        return ActionHelpers.IsAdjacent(unit, target);
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        g.Attack(unit, target.Unit!, Weapon);
    }
}

public class Pounce : ActionBrick
{
    public static readonly Pounce Instance = new();
    const int Range = 2;

    Pounce() : base("pounce", TargetingType.Unit) { }

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

public class FullAttack : ActionBrick
{
    readonly Item[] _weapons;
    readonly string[] _attackIds;

    public FullAttack(string name, params WeaponDef[] attacks) : base($"full:{name}:{string.Join(",", attacks.Select(a => a.id ?? a.Name))}")
    {
        _weapons = [.. attacks.Select(Item.Create)];
        _attackIds = [.. attacks.Select(a => a.id ?? a.Name)];
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        var tgt = target.Unit!;
        foreach (var weapon in _weapons)
        {
            if (tgt.IsDead) break;
            g.Attack(unit, tgt, weapon);
        }
    }

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "not adjacent";
        return ActionHelpers.IsAdjacent(unit, target);
    }
}
