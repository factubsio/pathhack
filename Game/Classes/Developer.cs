namespace Pathhack.Game.Classes;

public class MagicMapping() : ActionBrick("Magic Mapping")
{
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "";
        return true;
    }
    
    public override void Execute(IUnit unit, object? data, Target target)
    {
        for (int y = 0; y < lvl.Height; y++)
        for (int x = 0; x < lvl.Width; x++)
        {
            Pos p = new(x, y);
            lvl.UpdateMemory(p, includeItems: false);
        }
        g.pline("A map coalesces in your mind.");
    }
}

public class BlindSelf() : ActionBrick("Blind Self")
{
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "";
        return true;
    }
    
    public override void Execute(IUnit unit, object? data, Target target)
    {
        unit.AddFact(BlindBuff.Instance.Timed(), duration: 5);
        g.pline("You blind yourself!");
    }
}

public class GreaseArea(string name, IUnit? source, int dc, int duration) : Area(duration)
{
    public override string Name => name;
    public override Glyph Glyph => new('~', ConsoleColor.DarkYellow);
    public override bool IsDifficultTerrain => true;

    protected override void OnEnter(IUnit unit)
    {
        if (unit.HasFact(ProneBuff.Instance)) return;

        using var ctx = PHContext.Create(source, Target.From(unit));
        var slips = VTense(unit, "slip");
        if (!CreateAndDoCheck(ctx, "reflex_save", dc, "difficult_terrain"))
        {
            g.pline($"{unit:The} {slips} on some {name} and {VTense(unit, "fall")}!");
            unit.AddFact(ProneBuff.Instance.Timed(), 1);
        }
        else
        {
            g.pline($"{unit:The} {slips} on some {name} but {VTense(unit, "keep")} {unit:own} balance.");
        }
    }

    protected override void OnTick()
    {
        foreach (var unit in Occupants)
        {
            if (unit.HasFact(ProneBuff.Instance)) unit.Energy -= unit.LandMove.Value;
        }
    }
}

public class GreaseAround() : ActionBrick("grease test")
{
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "";
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        var area = new GreaseArea("Grease", unit, 14, 6) { Tiles = [..unit.Pos.Neighbours().Where(p => !lvl[p].IsStructural)] };
        lvl.Areas.Add(area);
    }
}

public class PoisonSelf() : ActionBrick("Poison Self")
{
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "";
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        unit.AddFact(new SpiderVenom(100));
        g.pline("You inject yourself with spider venom!");
    }
}

public class GrantProtection() : ActionBrick("Grant Protection")
{
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "";
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        unit.AddFact(ProtectionBrick.Fire, count: 20);
        unit.AddFact(ProtectionBrick.Cold, count: 20);
        unit.AddFact(ProtectionBrick.Shock, count: 20);
        unit.AddFact(ProtectionBrick.Acid, count: 20);
        unit.AddFact(ProtectionBrick.Phys, count: 20);
        g.pline("You are protected from the elements!");
    }
}

public class GrantTempHp() : ActionBrick("Grant Temp HP")
{
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "";
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        unit.GrantTempHp(10);
        g.pline("You gain temporary hit points!");
    }
}

public static partial class ClassDefs
{
    public static ClassDef Developer => new()
    {
        id = "developer",
        Name = "Developer",
        Description = "Knows the [fg=cyan]source[/fg]. [b]Debug mode[/b] enabled. Can see hidden things and break the rules.",
        HpPerLevel = 99,
        KeyAbility = AbilityStat.Int,
        StartingStats = new()
        {
            Str = 10,
            Dex = 10,
            Con = 10,
            Int = 10,
            Wis = 10,
            Cha = 10,
        },
        Progression = [
            new() // Level 1
            {
                Grants = [
                    new GrantProficiency(Proficiencies.Unarmed, ProficiencyLevel.Legendary),
                    new GrantProficiency(Proficiencies.HeavyBlade, ProficiencyLevel.Legendary),
                    new GrantProficiency(Proficiencies.LightArmor, ProficiencyLevel.Legendary),
                    new GrantProficiency(Proficiencies.MediumArmor, ProficiencyLevel.Legendary),
                    new GrantProficiency(Proficiencies.HeavyArmor, ProficiencyLevel.Legendary),
                ],
            },
        ],
        GrantStartingEquipment = p =>
        {
            var sword = ItemGen.GenerateItem(MundaneArmory.Longsword, 100, -3);
            p.Inventory.Add(sword);
            
            // Test striking rune
            var strikingSword = Item.Create(MundaneArmory.Longsword);
            strikingSword.Potency = 1;
            ItemGen.ApplyRune(strikingSword, Runes.Striking(1), fundamental: true);
            p.Inventory.Add(strikingSword);
            
            // Test bonus rune
            var bonusSword = Item.Create(MundaneArmory.Longsword);
            bonusSword.Potency = 1;
            ItemGen.ApplyRune(bonusSword, Runes.Bonus(1), fundamental: true);
            p.Inventory.Add(bonusSword);
            
            p.Inventory.Add(Item.Create(MundaneArmory.Longsword));
            p.Inventory.Add(Item.Create(MundaneArmory.Dagger));
            p.Inventory.Add(Item.Create(MundaneArmory.LeatherArmor));
            p.Inventory.Add(Item.Create(MundaneArmory.ChainShirt));
            p.Inventory.Add(Item.Create(MundaneArmory.Scythe));
            p.Inventory.Add(Item.Create(MundaneArmory.Whip));
            p.Inventory.Add(Item.Create(Potions.FalseLife));
            p.Inventory.Add(Item.Create(Potions.LesserInvisibility, 4));
            foreach (var def in DummyThings.All)
                p.Inventory.Add(Item.Create(def));
            
            var darts = Item.Create(DummyThings.Dart);
            darts.Count = 10;
            p.Inventory.Add(darts);
            p.Quiver = darts;
            
            var darts2 = Item.Create(DummyThings.Dart);
            darts2.Count = 5;
            darts2.Potency = 1;
            p.Inventory.Add(darts2);
            
            p.AddAction(new MagicMapping());
            p.AddAction(new BlindSelf());
            p.AddAction(new GreaseAround());
            p.AddAction(new PoisonSelf());
            p.AddAction(new GrantProtection());
            p.AddAction(new GrantTempHp());
            foreach (var blessing in Blessings.All)
                blessing.ApplyMinor(p);
            g.DebugMode = true;
            Log.EnabledTags.Add("cone");
            Log.EnabledTags.Add("sun");
        },
    };
}
