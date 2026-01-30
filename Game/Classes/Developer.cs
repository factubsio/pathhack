namespace Pathhack.Game.Classes;

public class MagicMapping : ActionBrick
{
    public override string Name => "Magic Mapping";
    
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
            foreach (var blessing in Blessings.All)
                blessing.ApplyMinor(p);
            g.DebugMode = true;
            Log.EnabledTags.Add("cone");
            Log.EnabledTags.Add("sun");
        },
    };
}
