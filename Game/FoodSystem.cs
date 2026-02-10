namespace Pathhack.Game;

public enum HungerState { Satiated, Normal, Hungry, Weak, Fainting }

public static class Hunger
{
    public const int Max = 2000;
    public const int Satiated = 1500;
    public const int FullWarning = 1500; // 75% of max
    public const int Hungry = 500;
    public const int Weak = 200;
    public const int Fainting = 50;

    public static HungerState GetState(int nutrition) => nutrition switch
    {
        >= Satiated => HungerState.Satiated,
        >= Hungry => HungerState.Normal,
        >= Weak => HungerState.Hungry,
        >= Fainting => HungerState.Weak,
        _ => HungerState.Fainting
    };

    public static string GetLabel(HungerState state) => state switch
    {
        HungerState.Satiated => "Satiated",
        HungerState.Normal => "",
        HungerState.Hungry => "Hungry",
        HungerState.Weak => "Weak",
        HungerState.Fainting => "Fainting",
        _ => ""
    };

    public static void Tick(Player p)
    {
        var before = GetState(p.Nutrition);
        p.Nutrition = Math.Max(0, p.Nutrition - 1);
        var after = GetState(p.Nutrition);

        if (after != before)
        {
            var msg = after switch
            {
                HungerState.Hungry => "You are beginning to feel hungry.",
                HungerState.Weak => "You feel weak from hunger.",
                HungerState.Fainting => "You feel faint from lack of food.",
                HungerState.Normal when before == HungerState.Satiated => "You no longer feel stuffed.",
                _ => null
            };
            if (msg != null) g.pline(msg);
        }
    }
}

public class Activity
{
    public string Type = "";
    public int Progress;
    public Item? Target;
    public bool CanChoke;
    public bool FullWarned;
    public int StoredNutrition; // for quick cook (corpse destroyed on start)
    
    public int TotalTime => Type switch
    {
        "eat" when Target?.CorpseOf is { } m => m.Size switch
        {
            UnitSize.Tiny => 1,
            UnitSize.Small => 2,
            UnitSize.Medium => 4,
            UnitSize.Large => 6,
            UnitSize.Huge => 10,
            UnitSize.Gargantuan => 15,
            _ => 3
        },
        "eat" => (Target?.Def as ConsumableDef)?.EatTime ?? 1,
        "cook_quick" => 4,
        "cook_careful" => CarefulCookTime,
        _ => 1
    };
    
    int CarefulCookTime => Target?.CorpseOf?.Size switch
    {
        UnitSize.Tiny => 10,
        UnitSize.Small => 15,
        UnitSize.Medium => 20,
        UnitSize.Large => 30,
        UnitSize.Huge => 40,
        UnitSize.Gargantuan => 50,
        _ => 20
    };
    
    public bool Interruptible => Type switch
    {
        "eat" => true,
        "cook_careful" => IsInCookPhase,
        _ => false
    };
    
    // Careful cook: 2 cycles of (40% prep + 60% cook)
    // e.g. 20 turns: 0-3 prep, 4-9 cook, 10-13 prep, 14-19 cook
    bool IsInCookPhase
    {
        get
        {
            int cycleLen = CarefulCookTime / 2;
            int prepLen = cycleLen * 2 / 5; // 40% prep
            int inCycle = Progress % cycleLen;
            return inCycle >= prepLen;
        }
    }
    
    public bool Done => Progress >= TotalTime;
    
    public bool Tick() => Type switch
    {
        "eat" => TickEat(),
        "cook_quick" => TickCookQuick(),
        "cook_careful" => TickCookCareful(),
        _ => false
    };
    
    public void OnInterrupt()
    {
        var msg = Type switch
        {
            "eat" when Target != null => $"You stop eating {Grammar.DoNameOne(Target)}.",
            "cook_careful" when Target != null => $"You stop cooking {Grammar.DoNameOne(Target)}.",
            _ => "You stop."
        };
        g.pline(msg);
    }

    public static void SpillVomit(IUnit? unit, Pos pos)
    {
        var area = new GreaseArea("vomit", unit, 12, 8) { Tiles = [pos] };
        lvl.CreateArea(area);
    }

    public static void DoVomit(IUnit unit, string self, string? see = null, string? hear = null)
    {
        g.YouObserveSelf(unit, self, see, hear);
        
        bool vomited = false;
        // 1 in 3 to try to vomit on a neighbour
        if (g.Rn2(3) == 0)
        {
            foreach (var pos in unit.Pos.Neighbours().Where(p => lvl.InBounds(p) && lvl[p].IsPassable))
            {
                if (g.Rn2(5) > 0) continue;
                SpillVomit(unit, pos);
                vomited = true;
            }
        }
        // Oops, you made a mess!
        if (!vomited)
            SpillVomit(unit, unit.Pos);
    }

    bool TickEat()
    {
        if (Target == null) return false;
        
        var beforeState = Hunger.GetState(u.Nutrition);
        
        Progress++;
        int remaining = TotalTime - Progress + 1;
        int gained = Target.RemainingNutrition / Math.Max(1, remaining);
        Target.Eaten += gained;
        u.Nutrition += gained;
        
        var afterState = Hunger.GetState(u.Nutrition);
        
        // Announce satiation when we cross the threshold
        if (afterState == HungerState.Satiated && beforeState != HungerState.Satiated)
            g.pline("You feel satiated.");
        
        // Warn at 75% (once)
        if (!FullWarned && u.Nutrition >= Hunger.FullWarning && !Done)
        {
            FullWarned = true;
            g.pline("You're having a hard time getting all of it down.");
            if (!Input.YesNo("Continue eating?"))
            {
                g.pline("You stop eating.");
                return false;
            }
        }
        
        // Choke check
        if (CanChoke && u.Nutrition >= Hunger.Max)
        {
            if (g.Rn2(20) == 0)
            {
                g.pline("You choke on your food.");
                g.pline("You die...");
                g.Done("choked on food");
                return false;
            }
        else
            {
                u.Nutrition = Math.Max(0, u.Nutrition - 1000);
                DoVomit(u, "You stuff yourself and then vomit voluminously.",
                    $"{u:The} stuffs {u:own} face and vomits!", "You hear someone retching.");
            }
        }
        
        u.Nutrition = Math.Min(Hunger.Max, u.Nutrition);
        
        if (Done)
        {
            g.pline($"You finish eating {Grammar.DoNameOne(Target)}.");
            
            // Flavor messages
            var flavorMsg = (Target.Def as ConsumableDef)?.FlavorMessage;
            if (flavorMsg != null) g.pline(flavorMsg);
            
            u.Inventory.Consume(Target);
            return false;
        }
        
        return true;
    }
    
    bool TickCookQuick()
    {
        Progress++;
        CookingEffects(attractChance: 20, spawnChance: 100);
        
        if (Done)
        {
            g.pline("You finish cooking.");
            u.Nutrition = Math.Min(Hunger.Max, u.Nutrition + StoredNutrition);
            return false;
        }
        return true;
    }
    
    bool TickCookCareful()
    {
        Progress++;
        Target!.Eaten = Progress;
        CookingEffects(attractChance: 8, spawnChance: 50);
        
        if (Done)
        {
            g.pline($"You finish cooking {Grammar.DoNameOne(Target)}.");
            
            // Food poisoning from rotten corpses
            if (Foods.IsTainted(Target))
            {
                u.AddFact(FoodPoisoning.Instance, count: 2);
                g.pline("Your tummy starts rumbling.");
            }
            else if (Foods.IsSpoiled(Target))
            {
                using var ctx = PHContext.Create(Monster.DM, Pathhack.Game.Target.From(u));
                if (!CheckFort(ctx, 11, "food poisoning"))
                {
                    u.AddFact(FoodPoisoning.Instance, count: 1);
                    g.pline("You are not sure that was a great idea.");
                }
            }
            
            int nutrition = Target.CorpseOf!.Nutrition / 4;
            u.Nutrition = Math.Min(Hunger.Max, u.Nutrition + nutrition);
            lvl.RemoveItem(Target, upos);
            return false;
        }
        return true;
    }
    
    static void CookingEffects(int attractChance, int spawnChance)
    {
        // Attract nearby monsters
        if (g.Rn2(attractChance) == 0)
        {
            AttractMonsters(10);
            return;
        }
        
        // Spawn threat
        if (g.Rn2(spawnChance) == 0)
        {
            SpawnCookingThreat();
            return;
        }
    }
    
    static void AttractMonsters(int radius)
    {
        foreach (var mon in lvl.LiveUnits.OfType<Monster>())
        {
            if (mon.Pos.ChebyshevDist(upos) > radius) continue;
            if (mon.IsAsleep)
            {
                mon.IsAsleep = false;
                if (lvl.IsVisible(mon.Pos))
                    g.pline($"{mon:The} stirs, smelling food.");
            }
        }
    }
    
    static void SpawnCookingThreat()
    {
        // Hippo check first
        if (g.Rn2(500) < u.HippoCounter)
        {
            SpawnHippos();
            return;
        }

        var pos = lvl.FindLocation(p => 
            lvl.NoUnit(p) && p.ChebyshevDist(upos) is >= 5 and <= 10 && lvl[p].IsPassable);
        if (pos == null) return;
        
        var def = CookingThreats[g.Rn2(CookingThreats.Length)];
        var spawnPos = pos.Value;
        Log.Write($"SpawnCookingThreat: {def.Name} at {spawnPos}");
        g.Defer(() =>
        {
            var mon = Monster.Spawn(def, "hungry kitty");
            lvl.PlaceUnit(mon, spawnPos);
            g.pline("Something approaches, drawn by the smell!");
        });
    }

    static void SpawnHippos()
    {
        int count = g.Rne(2);
        int effectiveLevel = (lvl.Id.Depth + u.CharacterLevel) / 2;
        int maxTier = Math.Min(effectiveLevel / 3, u.HippoCounter);
        int tier = Math.Min(g.Rne(3), maxTier + 1) - 1;
        var def = Hippos.All[tier];

        // THERE CAN BE ONLY ONE (could check for TheHungriest but this might be nicer?)
        if (def.IsUnique) count = 1;

        // Urgathoa loves it when you stuff yourself
        bool friends = u.Deity == Pantheon.Urgathoa;

        int spawned = 0;
        var dirs = Pos.CardinalDirs.Shuffled();

        foreach (var dir in dirs)
        {
            if (spawned >= count) break;
            var pos = FindHippoSpawn(dir);
            if (pos == null) continue;
            var spawnPos = pos.Value;
            g.Defer(() =>
            {
                var mon = Monster.Spawn(def, "HUNGRY HUNGRY HIPPOS");
                if (friends)
                    mon.Peaceful = g.Rn2(2) == 0;
                lvl.PlaceUnit(mon, spawnPos);
            });
            spawned++;
        }

        if (spawned > 0)
        {
            string[] msgs = spawned switch
            {
                1 => ["A hippo charges in, drawn by the smell!",
                      "You hear thundering hooves. A hippo appears!",
                      "An angry hippo bursts onto the scene!",
                      "A hippo smells your cooking and wants in on it."],
                2 => ["A pair of hippos charge in!",
                      "Two hippos barrel toward you!",
                      "Hippos! Two of them!"],
                3 => ["Multiple hippos converge on your position!",
                      "A stampede of hippos approaches!",
                      "Three hippos charge in from different directions!"],
                _ => ["You are surrounded by hungry hippos!",
                      "Hippos charge from all directions!",
                      "It's a full hippo assault!",
                      "HUNGRY HUNGRY HIPPOS!"]
            };
            g.Defer(() => g.pline(msgs.Pick()));
        }
        else
        {
            g.pline("You thought for a moment you could smell hippo musk.");
            u.HippoCounter = (int)(u.HippoCounter * 1.2);
        }
    }

    static Pos? FindHippoSpawn(Pos dir)
    {
        // They need a free adjacent square `_`: you are cooking on `@`
        // the hippo `q` spawns 2..4 away, but he needs an unblocked path to his yums
        // 
        //      @_..q

        var adjacent = upos + dir;
        if (!lvl.InBounds(adjacent) || !lvl[adjacent].IsPassable || !lvl.NoUnit(adjacent)) return null;

        // Hippos charge from cardinal directions like in the game
        for (int dist = 4; dist >= 2; dist--)
        {
            var pos = upos + dir * dist;
            if (lvl.InBounds(pos) && lvl[pos].IsPassable && lvl.NoUnit(pos))
                return pos;
        }
        return null;
    }
    
    static readonly MonsterDef[] CookingThreats = [
        MiscMonsters.Rat,
        Cats.Cheetah,
    ];
    
    public static Activity Eat(Item food, bool canChoke) => new()
    {
        Type = "eat",
        Target = food,
        CanChoke = canChoke
    };
    
    public static Activity CookQuick(Item corpse) => new()
    {
        Type = "cook_quick",
        StoredNutrition = corpse.CorpseOf!.Nutrition / 10
        // TODO: remember rot timer for food poisoning check
    };
    
    public static Activity CookCareful(Item corpse) => new()
    {
        Type = "cook_careful",
        Target = corpse,
        Progress = corpse.Eaten // resume from where we left off
    };
}

public class ConsumableDef : ItemDef
{
    public int Nutrition = 100;
    public int EatTime = 1; // turns to eat
    public string? FlavorMessage;

    public ConsumableDef()
    {
        Glyph = new(ItemClasses.Food, ConsoleColor.DarkYellow);
        Stackable = true;
    }
}

public static class Foods
{
    public static readonly ConsumableDef Ration = new()
    {
        Name = "food ration",
        Nutrition = 800,
        EatTime = 5,
        Weight = 10,
        Price = 40,
    };

    public static readonly ConsumableDef Apple = new()
    {
        Name = "apple",
        Nutrition = 50,
        EatTime = 1,
        Weight = 2,
        FlavorMessage = "Core dumped.",
        Price = 20,
    };

    public static readonly ConsumableDef[] All = [Ration, Apple];

    public static readonly ItemDef Corpse = new()
    {
        Name = "corpse",
        Glyph = new(ItemClasses.Food, ConsoleColor.Red),
        Stackable = false,
        Price = 5,
    };

    public const int RotTime = 200;
    public const int RotSpoiled = 50;
    public const int RotTainted = 150;

    public static bool IsSpoiled(Item item) => item.RotTimer >= RotSpoiled;
    public static bool IsTainted(Item item) => item.RotTimer >= RotTainted;

    /// <summary>Tick corpse rot. Returns true if rotted away.</summary>
    public static bool TickCorpse(Item item, IUnit? holder, Pos? floorPos)
    {
        if (item.CorpseOf == null) return false;
        
        // Don't rot while being cooked
        if (u.CurrentActivity?.Target == item) return false;
        
        item.RotTimer++;
        if (item.RotTimer < RotTime) return false;

        if (holder?.IsPlayer == true)
            g.pline($"Your {item.CorpseOf.Name} corpse rots away!");

        return true;
    }
}


public class FoodPoisoning() : AfflictionBrick(11, "fortitude")
{
    public static readonly FoodPoisoning Instance = new();

    public override string AfflictionName => "Food Poisoning";
    public override int MaxStage => 5;
    public override DiceFormula TickInterval => d(30, 10);

    protected override void DoPeriodicEffect(IUnit unit, int stage)
    {
        var msg = stage switch
        {
            1 => "Your tummy starts rumbling.",
            2 => "You feel nauseous.",
            3 => "Your guts are churning.",
            4 => "You feel violently ill.",
            5 => "You can barely stand from the cramps.",
            _ => null
        };
        if (msg != null && unit.IsPlayer)
            g.pline(msg);
    }

    protected override object? DoQuery(int stage, string key, string? arg) => key switch
    {
        "str" => new Modifier(ModifierCategory.StatusPenalty, -((stage + 1) / 2), "food poisoning"),
        "con" when stage >= 2 => new Modifier(ModifierCategory.StatusPenalty, -(stage / 2), "food poisoning"),
        _ => null
    };

    protected override void OnCured(IUnit unit)
    {
        if (unit.IsPlayer)
            g.pline("Your tummy finally feels better!");
    }

    protected override void OnRoundEnd(Fact fact)
    {
        if (fact.Entity is not IUnit unit) return;
        var stage = Stage(fact);
        if (stage < 3) return;

        int chance = stage >= 5 ? 20 : 50;
        if (g.Rn2(chance) != 0) return;

        if (fact.Entity is Player p)
            p.Nutrition = Math.Max(0, p.Nutrition - 100);
        Activity.DoVomit((fact.Entity as IUnit)!,
            "You vomit!",
            $"{unit:The} {VTense(unit, "vomit")}!",
            "You hear retching.");
    }
}
