namespace Pathhack.Game.Bestiary;

public class Thorns(Dice damage, DamageType type) : LogicBrick
{
    protected override void OnDamageTaken(Fact fact, PHContext context)
    {
        if (context.Source is not IUnit attacker) return;
        var defender = context.Target.Unit!;

        Target target = new(attacker, attacker.Pos);
        using var ctx = PHContext.Create(defender, target);
        ctx.Damage.Add(new() { Formula = damage, Type = type });

        g.pline($"The thorns pierce you!");
        DoDamage(ctx);
    }
}

public static class MiscMonsters
{
    public static readonly MonsterDef Rat = new()
    {
        id = "rat",
        Name = "rat",
        Glyph = new('r', ConsoleColor.DarkGray),
        HpPerLevel = 4,
        AC = -2,
        AttackBonus = -1,
        DamageBonus = 0,
        LandMove = ActionCosts.StandardLandMove,
        Unarmed = NaturalWeapons.Bite_1d3,
        Size = UnitSize.Tiny,
        BaseLevel = 0,
        SpawnWeight = 4,
        MinDepth = 1,
        MaxDepth = 2,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Neutral,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d3)),
        ],
    };

    public static readonly MonsterDef Skeleton = new()
    {
        id = "skeleton",
        Name = "skeleton",
        Glyph = new('Z', ConsoleColor.White),
        HpPerLevel = 8,
        AC = 0,
        AttackBonus = 1,
        DamageBonus = 0,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Claw_1d4,
        Size = UnitSize.Medium,
        BaseLevel = 1,
        SpawnWeight = 2,
        MinDepth = 2,
        MaxDepth = 4,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Neutral,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Claw_1d4)),
        ],
    };

    public static readonly MonsterDef ThornBush = new()
    {
        id = "thorn_bush",
        Name = "thorn bush",
        Glyph = new('{', ConsoleColor.Green),
        HpPerLevel = 6,
        AC = -2,
        AttackBonus = -2,
        DamageBonus = -2,
        LandMove = 0,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Small,
        BaseLevel = 1,
        SpawnWeight = 1,
        MinDepth = 1,
        MaxDepth = 5,
        MoralAxis = MoralAxis.Neutral,
        CreatureType = CreatureTypes.Plant,
        EthicalAxis = EthicalAxis.Neutral,
        Components = [
            new Thorns(d(2), DamageTypes.Piercing),
        ],
    };

    public static readonly MonsterDef[] All = [Rat, Skeleton, ThornBush];
}
