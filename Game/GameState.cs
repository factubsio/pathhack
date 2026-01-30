using System.Diagnostics;

namespace Pathhack.Game;

public class GameOverException : Exception;

public static class Perf
{
    static readonly Dictionary<string, (long Ticks, int Count)> _timings = [];
    static readonly Dictionary<string, (long Ticks, int Count)> _roundTimings = [];
    static readonly Stopwatch _sw = new();
    static readonly Stopwatch _pause = new();
    static int _roundCount;

    public static void Start() => _sw.Restart();

    public static void Stop(string label)
    {
        _sw.Stop();
        if (_timings.TryGetValue(label, out var t))
            _timings[label] = (t.Ticks + _sw.ElapsedTicks, t.Count + 1);
        else
            _timings[label] = (_sw.ElapsedTicks, 1);

        if (_roundTimings.TryGetValue(label, out var rt))
            _roundTimings[label] = (rt.Ticks + _sw.ElapsedTicks, rt.Count + 1);
        else
            _roundTimings[label] = (_sw.ElapsedTicks, 1);
    }

    public static void Pause() => _pause.Start();
    public static void Resume() => _pause.Stop();

    public static void StartRound() => _roundTimings.Clear();

    public static void EndRound()
    {
        _roundCount++;
        if (_roundTimings.Count == 0) return;
        using var w = new StreamWriter("perf_round.log", append: true);
        w.WriteLine($"=== Round {_roundCount} ===");
        foreach (var (label, (ticks, count)) in _roundTimings.OrderByDescending(x => x.Value.Ticks))
        {
            double ms = ticks * 1000.0 / Stopwatch.Frequency;
            w.WriteLine($"  {label}: {ms:F2}ms, {count} calls");
        }
        double pauseMs = _pause.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
        w.WriteLine($"  (paused: {pauseMs:F2}ms)");
        _pause.Reset();
    }

    public static void Dump()
    {
        using var w = new StreamWriter("perf.log");
        foreach (var (label, (ticks, count)) in _timings.OrderByDescending(x => x.Value.Ticks))
        {
            double ms = ticks * 1000.0 / Stopwatch.Frequency;
            w.WriteLine($"{label}: {ms:F2}ms total, {count} calls, {ms / count:F3}ms avg");
        }
    }
}

public static class Log
{
    static StreamWriter? _writer;
    static StreamWriter Writer => _writer ??= new("game.log", append: false);

    public static void Write(string msg)
    {
        Writer.WriteLine($"[R{g.CurrentRound}] {msg}");
        Writer.Flush();
    }

    public static void Write(string fmt, params object[] args) => Write(string.Format(fmt, args));

    public static HashSet<string> EnabledTags = [];

    public static void Verbose(string tag, string msg)
    {
        if (!EnabledTags.Contains(tag)) return;
        Writer.WriteLine($"[R{g.CurrentRound}] [{tag}] {msg}");
        Writer.Flush();
    }

    public static void Verbose(string tag, string fmt, params object[] args) => Verbose(tag, string.Format(fmt, args));
}

public class GameState
{
    public static GameState g { get; private set; } = new();
    public static Level lvl => g.CurrentLevel!;

    private int _seed;
    public int Seed
    {
        get => _seed;
        set
        {
            _seed = value;
            _rng = new(value);
        }
    }
    public bool Running { get; set; } = true;
    public string? DeathReason { get; set; }

    public void Done(string reason)
    {
        DeathReason = reason;
        Running = false;
        Perf.Dump();
        Console.Clear();
        Console.SetCursorPosition(0, 0);

        if (reason == "Won")
        {
            Fireworks.Play();
        }
        else
        {
            Console.WriteLine("GAME OVER");
            Console.WriteLine();
            Console.WriteLine(reason);
            Console.WriteLine();
            Console.WriteLine("Press any key...");
            Console.ReadKey(true);
        }
        throw new GameOverException();
    }

    public bool DebugMode { get; set; }
    public Dictionary<string, Branch> Branches { get; set; } = [];
    public Dictionary<LevelId, Level> Levels { get; } = [];
    public Level? CurrentLevel { get; set; }
    public HashSet<IEntity> PendingFactCleanup { get; } = [];
    public HashSet<IEntity> ActiveEntities { get; } = [];

    private Random _rng = new();

    ///<summary> [0, n) </summary>
    public int Rn2(int n) => _rng.Rn2(n);
    /// [0, n) + y
    public int Rn1(int x, int y) => _rng.Rn1(x, y);
    /// [min, max]
    public int RnRange(int min, int max) => _rng.RnRange(min, max);
    /// geometric
    public int Rne(int n) => _rng.Rne(n);

    public static int DoRoll(DiceFormula dice, Modifiers mods, string label)
    {
        int baseRoll = dice.Roll();
        int total = baseRoll + mods.Calculate();
        var parts = mods.Stackable.Concat(mods.Unstackable.Values)
            .Where(m => m.Value != 0)
            .Select(m => m.Value > 0 ? $"+{m.Value} ({m.Label})" : $"{m.Value} ({m.Label})");
        string modStr = string.Join(" ", parts);
        Log.Write("{0}: {1}={2} {3}= {4}", label, dice, baseRoll, modStr, total);
        return total;
    }

    public static bool DoCheck(PHContext ctx, string label)
    {
        var check = ctx.Check!;

        ctx.Source?.FireAllFacts(x => x.OnBeforeCheck, ctx);
        ctx.Target.Unit?.FireAllFacts(x => x.OnBeforeCheck, ctx);

        int net = check.Advantage - check.Disadvantage;
        bool hasAdv = net > 0;
        bool hasDis = net < 0;

        int roll1 = d(20).Roll();
        int roll2 = (hasAdv || hasDis) ? d(20).Roll() : roll1;
        int baseRoll = hasAdv ? Math.Max(roll1, roll2)
                     : hasDis ? Math.Min(roll1, roll2)
                     : roll1;

        check.Roll = baseRoll + check.Modifiers.Calculate();

        var parts = check.Modifiers.Stackable.Concat(check.Modifiers.Unstackable.Values)
            .Where(m => m.Value != 0)
            .Select(m => m.Value > 0 ? $"+{m.Value} ({m.Label})" : $"{m.Value} ({m.Label})");
        string modStr = string.Join(" ", parts);
        string advStr = hasAdv ? " (adv)" : hasDis ? " (dis)" : "";
        Log.Write("{0}: d20={1}{2} {3}= {4} vs DC {5}", label, baseRoll, advStr, modStr, check.Roll, check.DC);

        return check.ForcedResult ?? (check.Roll >= check.DC);
    }

    public static bool CreateAndDoCheck(PHContext ctx, string modifierKey, int dc, string label, bool silent = true)
    {
        var target = ctx.Target.Unit!;
        Check check = new() { DC = dc };
        ctx.Check = check;

        check.Modifiers.AddAll(target.QueryModifiers(modifierKey));

        bool didSave = DoCheck(ctx, label);
        if (didSave && !silent)
        {
            var verb = VTense(target, "save");
            g.pline($"{target:The} {verb} versus {label}.");
            Log.Write($"  {target:the} {verb} versus {label}.");
        }

        return didSave;
    }

    public List<string> Messages { get; } = [];
    public List<string> MessageHistory { get; } = [];

    public void pline(string msg)
    {
        Messages.Add(msg);
        MessageHistory.Add(msg);
    }
    public void pline(string fmt, params object[] args) => pline(string.Format(fmt, args));

    public void YouObserve(IUnit source, string? ifSee, string? sound = null, int hearRadius = 6)
    {
        bool canSee = lvl.IsVisible(source.Pos);
        bool canHear = sound != null && upos.ChebyshevDist(source.Pos) <= hearRadius;

        if (canSee && ifSee != null)
            pline(ifSee, source);
        else if (canHear)
            pline("You hear {0}.", sound!);
    }

    public int CurrentRound;

    public void DoRound()
    {
        if (!(CurrentLevel is { } lvl)) return;

        Perf.StartRound();
        lvl.Units.Sort((a, b) => b.Initiative.CompareTo(a.Initiative));

        LevelId startLevel = u.Level;

        // OnRoundStart for active entities and all units
        foreach (var entity in ActiveEntities)
            entity.FireWithCtx(x => x.OnRoundStart, PHContext.Create(null, Target.None));
        foreach (var unit in lvl.Units)
            unit.FireWithCtx(x => x.OnRoundStart, PHContext.Create(unit, Target.None));

        foreach (var Unit in lvl.Units)
        {
            Unit.Energy += 12;
        }

        foreach (var unit in lvl.Units)
        {
            if (unit.IsDead) continue;
            while (unit.Energy > 1)
            {
                if (u.Level != startLevel) break;

                Perf.Start();
                FovCalculator.Compute(lvl, upos, u.DarkVisionRadius);
                Draw.DrawCurrent();
                Perf.Stop("Draw");
                if (unit.IsPlayer)
                {
                    Perf.Start();
                    UI.Input.PlayerTurn();
                    Perf.Stop("PlayerTurn");
                    if (u.Level != startLevel) break;
                }
                else
                {
                    Perf.Start();
                    MonsterTurn(unit);
                    Perf.Stop("MonsterTurn");
                }
                Perf.Start();
                FovCalculator.Compute(lvl, upos, u.DarkVisionRadius);
                Draw.DrawCurrent();
                Perf.Stop("Draw");

                CleanupFacts();
            }
            if (u.Level != startLevel) break;
        }

        // OnRoundEnd for all units
        foreach (var unit in lvl.Units)
        {
            unit.FireWithCtx(x => x.OnRoundEnd, PHContext.Create(unit, Target.None));
            unit.TickPools();
        }

        lvl.Units.RemoveAll(x => x.IsDead);

        foreach (var unit in lvl.Units)
        {
            int regen = unit.NaturalRegen;
            while (regen > 0)
            {
                if (Rn2(30) < regen)
                    unit.HP += 1;
                regen -= 30;
            }

        }

        MonsterSpawner.TryRuntimeSpawn(lvl);
        TickActiveEntities();
        CleanupFacts();
        UI.Draw.DrawCurrent();
        Perf.EndRound();
        CurrentRound++;
    }

    void TickActiveEntities()
    {
        foreach (var entity in ActiveEntities)
            entity.FireWithCtx(x => x.OnRoundEnd, PHContext.Create(null, Target.None));
        CleanupFacts();
    }

    void MonsterTurn(IUnit mon)
    {
        if (mon is not Monster m) { mon.Energy = 0; return; }

        m.DoTurn();


        // nothing to do
        mon.Energy = 0;
    }

    public void GoToLevel(LevelId id, SpawnAt where, Pos? whereExactly = null)
    {
        Log.Write($"LevelChange {u.Level} -> {id}");
        if (CurrentLevel is { } old)
        {
            old.Units.Remove(u);
            old.LastExitTurn = CurrentRound;
        }

        if (!Levels.TryGetValue(id, out Level? level))
        {
            level = LevelGen.Generate(id, Seed);
            MonsterSpawner.SpawnInitialMonsters(level, u.CharacterLevel);
            Levels[id] = level;
            if (level.FirstIntro != null)
            {
                LoreDump(level.FirstIntro);
            }
        }
        else
        {
            if (level.ReturnIntro != null)
            {
                LoreDump(level.ReturnIntro);
            }
            if (level.LastExitTurn > 0)
            {
                long delta = CurrentRound - level.LastExitTurn;
                MonsterSpawner.CatchUpSpawns(level, delta);
            }
        }

        u.Level = id;
        CurrentLevel = level;
        upos = where switch
        {
            SpawnAt.Explicit => whereExactly ?? level.StairsUp!.Value,
            SpawnAt.StairsUp => (level.StairsUp ?? level.BranchUp)!.Value,
            SpawnAt.StairsDown => (level.StairsDown ?? level.BranchDown)!.Value,
            SpawnAt.BranchUp => (level.BranchUp ?? level.StairsUp)!.Value,
            SpawnAt.BranchDown => (level.BranchDown ?? level.StairsDown)!.Value,
            SpawnAt.RandomLegal => level.FindLocation(p => level.CanMoveTo(Pos.Invalid, p, u) && level.NoUnit(p)) ?? Pos.Zero,
            SpawnAt.RandomAny => level.FindLocation(level.NoUnit) ?? Pos.Zero,
            _ => throw new NotImplementedException(),
        };
        level.Units.Add(u);
        FovCalculator.Compute(level, upos, u.DarkVisionRadius);
    }

    public static void LoreDump(string message)
    {
        using var overlay = Draw.Overlay.Activate();
        Draw.Overlay.FullScreen = true;
        int y = RichText.Write(Draw.Overlay, 2, 2, 52, message);
        Draw.OverlayWrite(2, y + 2, "press (space) to continue");
        Draw.Blit();
        while (Console.ReadKey(true).Key != ConsoleKey.Spacebar)
            Draw.Blit();
    }

    public void DoHeal(IUnit source, IUnit target, DiceFormula formula)
    {
        using var ctx = PHContext.Create(source, new Target(target, target.Pos));
        ctx.HealFormula = formula;

        source.FireAllFacts(x => x.OnBeforeHealGiven, ctx);
        target.FireAllFacts(x => x.OnBeforeHealReceived, ctx);

        int roll = ctx.HealFormula.Roll() + ctx.HealModifiers.Calculate();
        int actual = target.HP.Heal(roll);
        ctx.HealedAmount = actual;

        target.FireAllFacts(x => x.OnAfterHealReceived, ctx);

        Log.Write("{0} heals {1} for {2} ({3} actual)", source, target, roll, actual);
    }

    public void Attack(IUnit attacker, IUnit defender, Item with, bool thrown = false)
    {
        var weapon = with.Def as WeaponDef;
        Target target = new(defender, defender.Pos);
        using var ctx = PHContext.Create(attacker, target);
        ctx.Weapon = with;

        string verb = thrown
            ? "throws"
            : weapon?.MeleeVerb ?? "attacks with";

        Check check = new() { DC = defender.GetAC() };
        ctx.Check = check;

        if (weapon != null)
        {
            bool improvised = thrown && weapon.Launcher == null;
            if (improvised)
                check.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, -2, "improvised"));
            else
                check.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, attacker.GetAttackBonus(weapon), "atk"));
            check.Modifiers.AddModifier(new(ModifierCategory.ItemBonus, with.Potency, "potency"));
        }

        attacker.FireAllFacts(x => x.OnBeforeAttackRoll, ctx);
        defender.FireAllFacts(x => x.OnBeforeDefendRoll, ctx);

        bool hit = DoCheck(ctx, $"{attacker} attacks {defender}");

        attacker.FireAllFacts(x => x.OnAfterAttackRoll, ctx);
        defender.FireAllFacts(x => x.OnAfterDefendRoll, ctx);

        if (hit)
        {
            DamageRoll dmg = new()
            {
                Formula = weapon?.BaseDamage ?? d(2),
                Type = weapon?.DamageType ?? DamageTypes.Blunt
            };
            dmg.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, attacker.GetDamageBonus(), "inherent"));
            ctx.Damage.Add(dmg);

            if (thrown)
            {
                if (defender.IsPlayer)
                    pline($"{with:The} hits you!");
                else
                    pline($"{with:The} hits {defender:the}.");
            }
            else if (attacker.IsPlayer)
            {
                int strDamage = Mod(u.Attributes.Str.Value);
                dmg.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, strDamage, "str"));
                pline($"You hit the {defender}.");
            }
            else if (defender.IsPlayer)
            {
                if (weapon?.Category == WeaponCategory.Item)
                    pline($"{attacker:The} {verb} its {with}!  {attacker:The} hits!");
                else
                    pline($"{attacker:The} hits!");
            }
            else
            {
                if (weapon?.Category == WeaponCategory.Item)
                    pline($"{attacker:The} {verb} its {with}! {attacker:The} hits {defender:the}.");
                else
                    pline($"{attacker:The} hits {defender:the}.");
            }
            DoDamage(ctx);
        }
        else
        {
            Log.Write("  miss");
            if (thrown)
            {
                if (defender.IsPlayer)
                    pline($"{with:The} misses you.");
                else
                    pline($"{with:The} misses {defender:the}.");
            }
            else if (attacker.IsPlayer)
                pline($"You miss the {defender}.");
            else if (defender.IsPlayer)
            {
                if (weapon?.Category == WeaponCategory.Item)
                    pline($"{attacker:The} {verb} its {with}!  {attacker:The} misses.");
                else
                    pline($"{attacker:The} misses.");
            }
            else
            {
                if (weapon?.Category == WeaponCategory.Item)
                    pline($"{attacker:The} {verb} its {with}!  {attacker:The} misses {defender:the}.");
                else
                    pline($"{attacker:The} misses {defender:the}.");
            }
        }
    }

    public static void DoDamage(PHContext ctx)
    {
        var source = ctx.Source!;
        var target = ctx.Target.Unit!;

        source.FireAllFacts(x => x.OnBeforeDamageRoll, ctx);
        target.FireAllFacts(x => x.OnBeforeDamageIncomingRoll, ctx);

        int damage = 0;
        foreach (var dmg in ctx.Damage)
        {
            int rolled = DoRoll(dmg.Formula, dmg.Modifiers, $"  {dmg} damage");
            rolled = (int)Math.Floor(rolled * dmg.Multiplier);
            damage += Math.Max(1, rolled);
        }
        Log.Write($"  {target:The} takes {damage} total damage");
        target.HP -= damage;

        if (target.IsPlayer)
            Movement.Stop();

        source.FireAllFacts(x => x.OnDamageDone, ctx);
        target.FireAllFacts(x => x.OnDamageTaken, ctx);

        if (target.HP.IsZero)
        {
            if (target.IsPlayer)
            {
                g.Done($"Killed by {source}!");
            }
            else
            {
                if (source.IsPlayer)
                {
                    g.pline($"You kill {target:the}!");
                    if (target is Monster m)
                        g.GainExp(10 * (1 << m.Def.CR));
                }
                else if (ctx.DeathReason != null)
                    g.pline($"{target:The} dies by {ctx.DeathReason}!");
                else if (source.IsDM)
                    g.pline($"{target:The} dies!");
                else if (source == target)
                    g.pline($"{source:The} dies!");
                else
                    g.pline($"{source:The} kills {target:the}!");

                using (var death = PHContext.Create(source, Target.From(target)))
                    target.FireAllFacts(f => f.OnDeath, death);

                // drop inventory
                foreach (var item in target.Inventory.ToList())
                    g.DoDrop(target, item);

                target.IsDead = true;
                lvl.GetOrCreateState(target.Pos).Unit = null;
            }
        }
    }

    public void DoDrop(IUnit unit, Item item)
    {
        unit.Inventory.Remove(item);
        if (unit is Player p && p.Quiver == item)
            p.Quiver = null;
        lvl.PlaceItem(item, unit.Pos);
        Log.Write("drop: {0}", item.Def.Name);
    }

    public void DoThrow(IUnit thrower, Item item, Pos dir)
    {
        Pos pos = thrower.Pos;
        Pos last = pos;
        IUnit? hit = null;
        int range = Math.Max(1, 4 + thrower.StrMod - item.Def.Weight / 40);
        for (int i = 0; i < range; i++)
        {
            pos += dir;
            if (!lvl.InBounds(pos) || !lvl.CanMoveTo(last, pos, null)) break;
            last = pos;
            hit = lvl.UnitAt(pos);
            if (hit != null) break;
        }
        int total = 150;
        int frames = last.ChebyshevDist(thrower.Pos);
        int perFrame = total / frames;
        Draw.AnimateProjectile(thrower.Pos, last, item.Def.Glyph, perFrame);
        if (hit != null)
            Attack(thrower, hit, item, thrown: true);
        lvl.PlaceItem(item, last);
    }

    public void DoPickup(IUnit unit, Item item)
    {
        lvl.RemoveItem(item, unit.Pos);

        // try merge with existing stack
        foreach (var existing in unit.Inventory)
        {
            if (existing.CanMerge(item))
            {
                existing.MergeFrom(item);
                Log.Write("pickup+merge: {0}", item.Def.Name);
                return;
            }
        }

        unit.Inventory.Add(item);
        Log.Write("pickup: {0}", item.Def.Name);
    }

    public void GainExp(int amount)
    {
        u.XP += amount;
        Log.Write("GainExp: +{0} (total {1})", amount, u.XP);
    }

    public void DoEquip(IUnit unit, Item? item, EquipSlot? slotOverride = null)
    {
        if (item == null)
        {
            // bare hands
            unit.Unequip(new(ItemSlots.Hand, "_"));
        }
        else
        {
            EquipSlot slot = slotOverride ?? new(item.Def.DefaultEquipSlot, "_");
            unit.Unequip(slot);
            unit.Equip(item);
        }
        unit.Energy -= ActionCosts.OneAction.Value;
    }

    public void DoUnequip(IUnit unit, Item item)
    {
        var slot = unit.Equipped.First(kv => kv.Value == item).Key;
        unit.Unequip(slot);
        unit.Energy -= ActionCosts.OneAction.Value;
    }

    internal static void ResetGameState() => g = new();

    void CleanupFacts()
    {
        foreach (var entity in PendingFactCleanup)
            entity.CleanupMarkedFacts();
        PendingFactCleanup.Clear();
    }

    internal void FlashLit(TileBitset moreLit)
    {
        FovCalculator.Compute(lvl, upos, u.DarkVisionRadius, moreLit);
    }

    // Assume unit is on a portal or stairs
    internal void Portal(IUnit unit)
    {
        if (!unit.IsPlayer) return; //for now monsters can't portal

        var tile = lvl[upos].Type;
        Log.Write($"Portal: tile={tile} pos={upos} level={u.Level}");
        Log.Write($"Portal: BranchUpTarget={lvl.BranchUpTarget} BranchDownTarget={lvl.BranchDownTarget}");
        switch (tile)
        {
            case TileType.StairsDown:
                GoToLevel(u.Level + 1, SpawnAt.StairsUp);
                break;
            case TileType.StairsUp when u.Level.Depth > 1:
                GoToLevel(u.Level - 1, SpawnAt.StairsDown);
                break;
            case TileType.BranchDown when lvl.BranchDownTarget is { } target:
                GoToLevel(target, SpawnAt.BranchUp);
                break;
            case TileType.BranchUp when lvl.BranchUpTarget is { } target:
                GoToLevel(target, SpawnAt.BranchDown);
                break;
        }
        Log.Write($"Portal: after level={u.Level} pos={upos}");
    }
}
