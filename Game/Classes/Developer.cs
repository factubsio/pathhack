namespace Pathhack.Game.Classes;

public class DebugMap() : ActionBrick("Magic Mapping")
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;
    
    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        g.DoMapLevel();
        foreach (var trap in lvl.Traps.Values)
            u.ObserveTrap(trap);
    }
}

public class BlindSelf() : ActionBrick("Blind Self")
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;
    
    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        unit.AddFact(BlindBuff.Instance.Timed(), duration: 5);
        g.pline("You blind yourself!");
    }
}

public class GreaseAround() : ActionBrick("grease test")
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        var area = new GreaseArea("Grease", unit, 14, 6) { Tiles = [..unit.Pos.Neighbours().Where(p => !lvl[p].IsStructural)] };
        lvl.CreateArea(area);
    }
}

public class PoisonSelf() : ActionBrick("Poison Self")
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        unit.AddFact(new SpiderVenom(100));
        g.pline("You inject yourself with spider venom!");
    }
}

public class GrantProtection() : ActionBrick("Grant Protection")
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
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
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        unit.GrantTempHp(10);
        g.pline("You gain temporary hit points!");
    }
}

public class LearnDungeon() : ActionBrick("Learn Dungeon")
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        foreach (var branch in g.Branches.Values)
            branch.Discovered = true;
        g.pline("You learn the layout of the dungeon.");
    }
}

public class GenAllLevels() : ActionBrick("Gen All Levels")
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        Branch branch = u.Level.Branch;
        int count = 0;
        for (int d = 1; d <= branch.MaxDepth; d++)
        {
            LevelId id = new(branch, d);
            if (!g.Levels.ContainsKey(id))
            {
                g.Levels[id] = LevelGen.Generate(id, g.Seed);
                count++;
            }
        }
        g.pline($"Generated {count} levels of {branch.Name}.");
    }
}

public class MakeWater() : ActionBrick("Make Water", TargetingType.Pos)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        if (target.Pos is not { } pos) return;
        lvl.Set(pos, TileType.Water);
        g.pline("Water springs forth!");
    }
}

public class IdentifyAll() : ActionBrick("Identify All")
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        foreach (var item in unit.Inventory)
            item.Identify();
        g.pline("Everything in your pack glows briefly.");
    }
}

public class HealFull() : ActionBrick("Heal Full")
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        unit.HP.Current = unit.HP.Max;
        g.pline("You feel completely restored.");
    }
}

public class Probe() : ActionBrick("Probe", TargetingType.Unit, maxRange: 5)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => true;

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        if (target.Unit is not { } tgt) return;
        g.pline($"{tgt:The}: HP {tgt.HP.Current}/{tgt.HP.Max} AC {tgt.GetAC()} L{tgt.EffectiveLevel}");
        var buffs = tgt.ActiveBuffNames.ToList();
        if (buffs.Count > 0) g.pline($"  Buffs: {string.Join(", ", buffs)}");
        if (tgt.Inventory.Count > 0) g.pline($"  Inv: {string.Join(", ", tgt.Inventory.Select(i => i.ToString()))}");
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
            p.AddAction(new LearnDungeon());
            p.AddAction(new GenAllLevels());
            p.AddAction(new MakeWater());
            p.AddAction(new IdentifyAll());
            p.AddAction(new HealFull());
            p.AddAction(new Probe());
            foreach (var blessing in Blessings.All)
                blessing.ApplyMinor(p);
        },
    };
}
