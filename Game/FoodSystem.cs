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
        int rate = p.Query<int>("hunger_rate", null, MergeStrategy.Max, 1);
        p.Nutrition = Math.Max(0, p.Nutrition - rate);
        var after = GetState(p.Nutrition);

        if (after != before)
        {
            var msg = after switch
            {
                HungerState.Hungry => "You are beginning to feel hungry.",
                HungerState.Weak => "You feel weak from hunger.",
                HungerState.Fainting => "You feel faint from lack of food.",
                _ => null
            };
            if (msg != null) g.pline(msg);
        }
    }
}

public abstract class Activity(string name, Item? targetItem = null)
{
    public string Name => name;
    public Item? TargetItem => targetItem;
    public int Progress;

    public abstract int TotalTime { get; }
    public virtual bool Interruptible => false;
    public bool Done => Progress >= TotalTime;

    public abstract bool Tick();

    public virtual void OnInterrupt()
    {
        g.pline("You stop.");
    }
}

public class EatActivity : Activity
{
    readonly Item _food;
    readonly bool _canChoke;
    bool _fullWarned;

    public EatActivity(Item food, bool canChoke) : base("eat", food)
    {
        _food = food;
        _canChoke = canChoke;
    }

    public override int TotalTime => _food.CorpseOf is { } m ? m.Size switch
    {
        UnitSize.Tiny => 1,
        UnitSize.Small => 2,
        UnitSize.Medium => 4,
        UnitSize.Large => 6,
        UnitSize.Huge => 10,
        UnitSize.Gargantuan => 15,
        _ => 3
    } : (_food.Def as ConsumableDef)?.EatTime ?? 1;

    public override bool Interruptible => true;

    public override void OnInterrupt() =>
        g.pline($"You stop eating {Grammar.DoNameOne(_food)}.");

    public override bool Tick()
    {
        var beforeState = Hunger.GetState(u.Nutrition);

        Progress++;
        int remaining = TotalTime - Progress + 1;
        int gained = _food.RemainingNutrition / Math.Max(1, remaining);
        _food.Eaten += gained;
        u.Nutrition += gained;

        var afterState = Hunger.GetState(u.Nutrition);

        if (afterState == HungerState.Satiated && beforeState != HungerState.Satiated)
            g.pline("You feel satiated.");

        if (!_fullWarned && u.Nutrition >= Hunger.FullWarning && !Done)
        {
            _fullWarned = true;
            g.pline("You're having a hard time getting all of it down.");
            if (!Input.YesNo("Continue eating?"))
            {
                g.pline("You stop eating.");
                return false;
            }
        }

        if (_canChoke && u.Nutrition >= Hunger.Max)
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
                CookingUtil.DoVomit(u, "You stuff yourself and then vomit voluminously.",
                    $"{u:The} stuffs {u:own} face and vomits!", "You hear someone retching.");
            }
        }

        u.Nutrition = Math.Min(Hunger.Max, u.Nutrition);

        if (Done)
        {
            g.pline($"You finish eating {Grammar.DoNameOne(_food)}.");
            var flavorMsg = (_food.Def as ConsumableDef)?.FlavorMessage;
            if (flavorMsg != null) g.pline(flavorMsg);
            u.Inventory.Consume(_food);
            return false;
        }

        return true;
    }
}

public class CookQuickActivity(Item corpse) : Activity("cook_quick")
{
    readonly int _nutrition = corpse.CorpseOf!.Nutrition / 10;
    // TODO: remember rot timer for food poisoning check

    public override int TotalTime => 4;

    public override bool Tick()
    {
        Progress++;
        CookingUtil.CookingEffects(attractChance: 20, spawnChance: 100);

        if (Done)
        {
            g.pline("You finish cooking.");
            u.Nutrition = Math.Min(Hunger.Max, u.Nutrition + _nutrition);
            return false;
        }
        return true;
    }
}

public class CookCarefulActivity : Activity
{
    readonly Item _corpse;

    public CookCarefulActivity(Item corpse) : base("cook_careful", corpse)
    {
        _corpse = corpse;
        Progress = corpse.Eaten; // resume from where we left off
    }

    public override int TotalTime => _corpse.CorpseOf?.Size switch
    {
        UnitSize.Tiny => 10,
        UnitSize.Small => 15,
        UnitSize.Medium => 20,
        UnitSize.Large => 30,
        UnitSize.Huge => 40,
        UnitSize.Gargantuan => 50,
        _ => 20
    };

    // Careful cook: 2 cycles of (40% prep + 60% cook)
    public override bool Interruptible
    {
        get
        {
            int cycleLen = TotalTime / 2;
            int prepLen = cycleLen * 2 / 5;
            int inCycle = Progress % cycleLen;
            return inCycle >= prepLen;
        }
    }

    public override void OnInterrupt() =>
        g.pline($"You stop cooking {Grammar.DoNameOne(_corpse)}.");

    public override bool Tick()
    {
        Progress++;
        _corpse.Eaten = Progress;
        CookingUtil.CookingEffects(attractChance: 8, spawnChance: 50);

        if (Done)
        {
            g.pline($"You finish cooking {Grammar.DoNameOne(_corpse)}.");

            if (Foods.IsTainted(_corpse))
            {
                u.AddFact(FoodPoisoning.Instance, count: 2);
                g.pline("Your tummy starts rumbling.");
            }
            else if (Foods.IsSpoiled(_corpse))
            {
                using var ctx = PHContext.Create(DungeonMaster.Mook, Target.From(u));
                if (!CheckFort(ctx, 11, "food poisoning"))
                {
                    u.AddFact(FoodPoisoning.Instance, count: 1);
                    g.pline("You are not sure that was a great idea.");
                }
            }

            int nutrition = _corpse.CorpseOf!.Nutrition / 4;
            u.Nutrition = Math.Min(Hunger.Max, u.Nutrition + nutrition);
            lvl.RemoveItem(_corpse, upos);
            return false;
        }
        return true;
    }
}

public static class CookingUtil
{
    public static void SpillVomit(IUnit? unit, Pos pos)
    {
        var area = new GreaseArea("vomit", unit, 12, 8) { Tiles = [pos] };
        lvl.CreateArea(area);
    }

    public static void DoVomit(IUnit unit, string self, string? see = null, string? hear = null)
    {
        g.YouObserveSelf(unit, self, see, hear);

        bool vomited = false;
        if (g.Rn2(3) == 0)
        {
            foreach (var pos in unit.Pos.Neighbours().Where(p => lvl.InBounds(p) && lvl[p].IsPassable))
            {
                if (g.Rn2(5) > 0) continue;
                SpillVomit(unit, pos);
                vomited = true;
            }
        }
        if (!vomited)
            SpillVomit(unit, unit.Pos);
    }

    public static void CookingEffects(int attractChance, int spawnChance)
    {
        if (g.Rn2(attractChance) == 0)
        {
            AttractMonsters(10);
            return;
        }
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
        if (g.Rn2(500) < u.HippoCounter)
        {
            SpawnHippos();
            u.HippoCounter /= 3;
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

        if (def.IsUnique) count = 1;

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
        var adjacent = upos + dir;
        if (!lvl.InBounds(adjacent) || !lvl[adjacent].IsPassable || !lvl.NoUnit(adjacent)) return null;

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
    public static bool TickCorpse(Item corpseItem, IUnit? holder, Pos? floorPos)
    {
        if (corpseItem.CorpseOf == null) return false;
        
        // Don't rot while being cooked
        if (u.CurrentActivity?.TargetItem == corpseItem) return false;
        
        corpseItem.RotTimer++;

        // Respawn: negative timer ticking up to 0
        if (corpseItem.RotTimer == 0 && corpseItem.CorpseOf is { } def)
        {
            Pos origin = floorPos ?? holder!.Pos;
            Pos? spawnPos = null;
            if (lvl.NoUnit(origin))
            {
                spawnPos = origin;
            }
            else
            {
                foreach (var n in origin.Neighbours())
                {
                    if (lvl.InBounds(n) && lvl[n].IsPassable && lvl.NoUnit(n))
                    {
                        spawnPos = n;
                        break;
                    }
                }
            }
            if (spawnPos != null)
            {
                g.Defer(() =>
                {
                    var mon = Monster.Spawn(def, "respawn", firstTimeSpawn: false);
                    lvl.PlaceUnit(mon, spawnPos.Value);
                    // pick up any equipment at the corpse tile
                    foreach (var loot in lvl.ItemsAt(origin))
                    {
                        // Don't add your own corspe??
                        if (loot == corpseItem) continue;
                        mon.Inventory.Add(loot);
                        mon.Equip(loot);
                    }

                    lvl.RemoveAllItems(origin);

                    g.YouObserve(spawnPos.Value, $"{mon:The} rises from the dead!");
                });
                return true;
            }
            // couldn't spawn, missed our chance
            return false;
        }

        if (corpseItem.RotTimer < RotTime) return false;

        if (holder?.IsPlayer == true)
            g.pline($"Your {corpseItem.CorpseOf.Name} corpse rots away!");

        return true;
    }
}


public class FoodPoisoning() : AfflictionBrick(11, "fortitude")
{
    public override string Id => "food_poisoning";
    public static readonly FoodPoisoning Instance = new();
    public override bool IsActive => true;

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
        CookingUtil.DoVomit((fact.Entity as IUnit)!,
            "You vomit!",
            $"{unit:The} {VTense(unit, "vomit")}!",
            "You hear retching.");
    }
}
