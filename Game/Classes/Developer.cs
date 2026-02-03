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
            lvl.UpdateMemory(p);
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
        unit.AddFact(new TimedBlind(), duration: 5);
        g.pline("You blind yourself!");
    }
}

public class GreaseFx : CellFx
{
    public static readonly GreaseFx Instance = new();

    public override void OnSpawn(IUnit unit)
    {
        unit.AddFact(ProneBuff.Instance);
    }

    public override void OnEnter(IUnit unit)
    {
        unit.AddFact(ProneBuff.Instance);
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
        foreach (Pos p in unit.Pos.Neighbours())
        {
            lvl.AddFx(p, GreaseFx.Instance, 3);
        }
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
            Str = 18,
            Dex = 18,
            Con = 18,
            Int = 18,
            Wis = 18,
            Cha = 18,
        },
        Progression = [
            new() // Level 1
            {
                Grants = [
                    new GrantProficiency(Proficiencies.Unarmed, ProficiencyLevel.Legendary),
                    new GrantProficiency(Proficiencies.Longsword, ProficiencyLevel.Legendary),
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
            foreach (var blessing in Blessings.All)
                blessing.ApplyMinor(p);
            g.DebugMode = true;
            Log.EnabledTags.Add("cone");
            Log.EnabledTags.Add("sun");
        },
    };
}
