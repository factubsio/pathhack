namespace Pathhack.Game.Classes;

public class DebugMap() : ActionBrick("Magic Mapping")
{
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "";
        return true;
    }
    
    public override void Execute(IUnit unit, object? data, Target target)
    {
        g.DoMapLevel();
        foreach (var trap in lvl.Traps.Values)
            u.ObserveTrap(trap);
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
        lvl.CreateArea(area);
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
                    new GrantPool("spell_l1", 5, 1),
                    new GrantPool("spell_l2", 5, 1),
                ],
            },
        ],
        GrantStartingEquipment = p =>
        {
            var sword = ItemGen.GenerateItem(MundaneArmory.Longsword, 100, -3);
            p.Inventory.Add(sword);

            p.AddSpell(BasicLevel1Spells.AcidArrow);
            
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

            p.Gold = 2000;
            
            p.Inventory.Add(Item.Create(MundaneArmory.Longsword));
            p.Inventory.Add(Item.Create(MundaneArmory.LeatherArmor));
            p.Inventory.Add(Item.Create(MagicAccessories.RingOfTheRam));
            MagicAccessories.RingOfTheRam.SetKnown();
            p.Inventory.Add(Item.Create(Potions.FalseLife, 4)).Identify();
            p.Inventory.Add(Item.Create(Potions.LesserInvisibility, 4)).Identify();
            p.Inventory.Add(Item.Create(Scrolls.Identify, 10)).Identify();
            p.Inventory.Add(Item.Create(Scrolls.Teleportation, 4)).Identify();
            Scrolls.Identify.SetKnown();
            foreach (var def in DummyThings.All)
                p.Inventory.Add(Item.Create(def));
            
            p.AddAction(new DebugMap());
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
