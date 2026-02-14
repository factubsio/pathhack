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

    private static bool PerRoundPerf = true;


    public static void StartRound()
    {
        if (perRound == null)
        {
            global.Start();
            perRound = new StreamWriter("perf_round.log");
        }
        roundStart = global.Elapsed.TotalSeconds;
        if (PerRoundPerf)
            perRound.WriteLine($">>>: BEGIN {_roundCount} {roundStart}");
        _roundTimings.Clear();
    }

    private static double roundStart = 0;
    private static readonly Stopwatch global = new();
    private static TextWriter perRound = null!;

    public static void EndRound()
    {
        if (!PerRoundPerf) return;

        perRound.WriteLine($"=== END {_roundCount} (elapsed:{global.Elapsed.TotalSeconds - roundStart:F3}) ===");
        perRound.WriteLine($"blit stats: written={Draw.TotalBytesWritten}, damaged={Draw.DamagedCellCount}");

        if (_roundTimings.Count == 0) return;
        foreach (var (label, (ticks, count)) in _roundTimings.OrderByDescending(x => x.Value.Ticks))
        {
            double ms = ticks * 1000.0 / Stopwatch.Frequency;
            perRound.WriteLine($"  {label}: {ms:F2}ms, {count} calls");
        }
        double pauseMs = _pause.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
        perRound.WriteLine($"  (paused: {pauseMs:F2}ms)");
        _pause.Reset();

        _roundCount++;
    }

    public static void Dump()
    {
        using var w = new StreamWriter("perf.log");
        foreach (var (label, (ticks, count)) in _timings.OrderByDescending(x => x.Value.Ticks))
        {
            double ms = ticks * 1000.0 / Stopwatch.Frequency;
            w.WriteLine($"{label}: {ms:F2}ms total, {count} calls, {ms / count:F3}ms avg");
        }

        perRound.Close();
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
    public StreamWriter? PlineLog;

    public record struct Awareness(bool CanTarget, bool Disadvantage, PlayerPerception Perception)
    {
        public bool Visual => Perception == PlayerPerception.Visible;
    }

    public static Awareness GetAwareness(IUnit viewer, IUnit target)
    {
        // monster<->monster: simplified visual check
        if (!viewer.IsPlayer && !target.IsPlayer)
        {
            bool adjacent = viewer.Pos.ChebyshevDist(target.Pos) <= 1;
            bool mBlind = !viewer.Allows("can_see");
            bool mTargetInvis = target.Has("invisible");
            bool mTargetInDark = !lvl.IsLit(target.Pos) && !viewer.Has("darkvision");
            bool mLOS = adjacent || FovCalculator.IsPathClear(lvl, viewer.Pos, target.Pos);
            bool mVisual = !mBlind && !mTargetInvis && !mTargetInDark && mLOS;
            if (!mVisual && adjacent && g.Rn2(8) == 0)
                return new(true, false, PlayerPerception.Detected);
            return new(mVisual, false, mVisual ? PlayerPerception.Visible : PlayerPerception.None);
        }

        int tremor = viewer.Query<int>("tremorsense", null, MergeStrategy.Max, 0);
        bool hasTremor = tremor > 0 && viewer.Pos.ChebyshevDist(target.Pos) <= tremor;

        bool blind = !viewer.Allows("can_see");
        bool targetInvis = target.Has("invisible") && !viewer.Has("see_invisible");
        bool targetInDark = !lvl.IsLit(target.Pos) && !viewer.Has("darkvision");

        // LOS check for visual detection (player-centric)
        bool hasLOS = viewer.IsPlayer ? lvl.HasLOS(target.Pos) : lvl.HasLOS(viewer.Pos);

        bool visual = !blind && !targetInvis && !targetInDark && hasLOS;
        
        // Determine perception level: visual > tremor > specific warning > generic warning
        PlayerPerception perception;
        if (visual)
            perception = PlayerPerception.Visible;
        else if (hasTremor)
            perception = PlayerPerception.Detected;
        else
        {
            // specific detection (detect_undead, detect_beast, etc.)
            perception = PlayerPerception.None;
            if (target is Monster m)
            {
                int detectRange = viewer.Query<int>("detect_creature", m.CreatureTypeKey, MergeStrategy.Max, 0);
                if (detectRange > 0 && viewer.Pos.ChebyshevDist(target.Pos) <= detectRange)
                    perception = PlayerPerception.Warned;
            }
            
            if (perception == PlayerPerception.None)
            {
                int warningRange = viewer.Query<int>("warning", null, MergeStrategy.Max, 0);
                if (warningRange > 0 && viewer.Pos.ChebyshevDist(target.Pos) <= warningRange)
                    perception = PlayerPerception.Unease;
            }
        }

        bool canTarget = visual || hasTremor;
        bool disadvantage = canTarget && !visual && !hasTremor;

        // blind_fight removes disadvantage but not visual
        if (disadvantage && blind && viewer.Has("blind_fight"))
            disadvantage = false;

        return new(canTarget, disadvantage, perception);
    }

    public static bool CanSee(IUnit viewer, IUnit target) => GetAwareness(viewer, target).Visual;

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
        BlackBox.Record();
        Dump.DumpLog();
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
            Input.NextKey();
        }
        throw new GameOverException();
    }

    public bool DebugMode { get; set; }
    public bool SeeAllMonsters { get; set; }
    public bool GlobalHatred { get; set; }
    public Dictionary<string, Branch> Branches { get; set; } = [];
    public Dictionary<LevelId, Level> Levels { get; } = [];
    public Dictionary<string, int> Vanquished { get; } = [];
    public Level? CurrentLevel { get; set; }
    public HashSet<IEntity> PendingFactCleanup { get; } = [];
    public HashSet<IEntity> ActiveEntities { get; } = [];
    public List<Action> DeferredActions { get; } = [];

    public void Defer(Action action) => DeferredActions.Add(action);

    void FlushDeferred()
    {
        foreach (var action in DeferredActions)
            action();
        DeferredActions.Clear();
    }

    private Random _rng = new();

    ///<summary> [0, n) </summary>
    public int Rn2(int n) => _rng.Rn2(n);
    /// [0, n) + y
    public int Rn1(int x, int y) => _rng.Rn1(x, y);
    /// [min, max]
    public int RnRange(int min, int max) => _rng.RnRange(min, max);
    /// geometric
    public int Rne(int n) => _rng.Rne(n);
    public void Shuffle<T>(Span<T> values) => _rng.Shuffle(values);

    public static int DoRoll(DiceFormula dice, Modifiers mods, string label)
    {
        int baseRoll = dice.Roll();
        int modValue = mods.Calculate();
        int total = baseRoll + modValue;
        Log.Write($"{label}: {dice}:{baseRoll} + {mods}:{modValue} = {total}");
        return total;
    }

    public static string FormatMods(Modifiers mods)
    {
        var parts = mods.Stackable.Concat(mods.Unstackable.Values)
            .Where(m => m.Value != 0)
            .Select(m =>
            {
                string code = m.Why?.Length >= 2 ? m.Why[..2] : "??";
                string sign = m.Value > 0 ? "+" : "";
                return $"{sign}{m.Value}{code}";
            });
        return string.Join(" ", parts);
    }

    public static string FormatDamage(PHContext ctx)
    {
        var parts = ctx.Damage
            .Where(d => !d.Negated)
            .Select(d => $"{d.Formula}={d.Rolled} {d.Type.SubCat}");
        return string.Join(" ", parts);
    }

    public static bool DoCheck(PHContext ctx, string label)
    {
        var check = ctx.Check!;

        LogicBrick.FireOnBeforeCheck(ctx.Source, ctx);
        LogicBrick.FireOnBeforeCheck(ctx.Target.Unit, ctx);

        int net = check.Advantage - check.Disadvantage;
        bool hasAdv = net > 0;
        bool hasDis = net < 0;

        int roll1 = d(20).Roll();
        int roll2 = (hasAdv || hasDis) ? d(20).Roll() : roll1;
        int baseRoll = hasAdv ? Math.Max(roll1, roll2)
                     : hasDis ? Math.Min(roll1, roll2)
                     : roll1;

        check.BaseRoll = baseRoll;
        check.Roll1 = roll1;
        check.Roll2 = (hasAdv || hasDis) ? roll2 : null;

        check.Roll = baseRoll + check.Modifiers.Calculate();

        if (!ctx.SilentCheck)
        {
            string pm = ctx.Target.Unit?.IsPlayer == true ? "P" : "M";
            string result = check.Result ? "✓" : "✗";
            string modStr = FormatMods(check.Modifiers);
            string advStr = hasAdv ? " adv" : hasDis ? " dis" : "";
            Log.Write($"{pm} {result} {check.Roll} ({check.RollStr}) vs {check.DC}: {label} {modStr}");
        }

        return check.Result;
    }

    public static bool CheckFort(PHContext ctx, int dc, string label) => CreateAndDoCheck(ctx, Check.Fort, dc, label);
    public static bool CheckReflex(PHContext ctx, int dc, string label) => CreateAndDoCheck(ctx, Check.Reflex, dc, label);
    public static bool CheckWill(PHContext ctx, int dc, string label) => CreateAndDoCheck(ctx, Check.Will, dc, label);

    public static bool CreateAndDoCheck(PHContext ctx, string modifierKey, int dc, string label)
    {
        var target = ctx.Target.Unit!;
        Check check = new() { DC = dc, Tag = label, Key = modifierKey };
        ctx.Check = check;

        Log.Write($"check from:{ctx.Source} to:{ctx.Target}");
        check.Modifiers.AddAll(target.QueryModifiers(modifierKey));

        bool didSave = DoCheck(ctx, label);

        return didSave;
    }

    public List<string> MessageHistory { get; } = [];

    public void pline(string msg)
    {
        Draw.DrawMessage(msg);
        PlineLog?.WriteLine($"[{CurrentRound}] {msg}");
    }
    internal void plineu(IUnit user, string msg)
    {
        if (user.IsPlayer) pline(msg);
    }

    public void pline(string fmt, params object[] args) => pline(string.Format(fmt, args));

    public void YouObserveSelf(IUnit source, string ifSelf, string? ifSee, string? sound = null, int hearRadius = 6)
    {
        if (source.IsPlayer)
        {
            pline(ifSelf);
            return;
        }

        bool canSee = lvl.IsVisible(source.Pos);
        bool canHear = sound != null && upos.ChebyshevDist(source.Pos) <= hearRadius;

        if (canSee && ifSee != null)
            pline(ifSee, source);
        else if (canHear)
            pline("You hear {0}.", sound!);
    }

    public bool YouObserve(IUnit source, string? ifSee, string? sound = null, int hearRadius = 6)
    {
        bool canSee = u.Allows("can_see") && lvl.IsVisible(source.Pos);
        bool canHear = sound != null && upos.ChebyshevDist(source.Pos) <= hearRadius;

        if (canSee && ifSee != null)
            pline(ifSee, source);
        else if (canHear)
            pline("You hear {0}.", sound!);
        
        return canSee;
    }

    public void YouObserve(Pos pos, string? ifSee, string? sound = null, int hearRadius = 6)
    {
        bool canSee = lvl.IsVisible(pos);
        bool canHear = sound != null && upos.ChebyshevDist(pos) <= hearRadius;

        if (canSee && ifSee != null)
            pline(ifSee);
        else if (canHear)
            pline("You hear {0}.", sound!);
    }

    public int CurrentRound;

    public void DoRound()
    {
        if (!(CurrentLevel is { } _)) return;

        Perf.StartRound();
        Draw.ResetRoundStats();

        // === Player phase ===
        u.Energy += 12;

        Perf.Start();
        foreach (var entity in ActiveEntities)
            LogicBrick.FireOnRoundStart(entity);
        LogicBrick.FireOnRoundStart(u);
        Perf.Stop("OnRoundStart");

        while (u.Energy > 1 && !u.IsDead)
        {
            // Paralysis check (And merge so immunity can override)
            if (u.Query("paralyzed", null, MergeStrategy.And, false))
            {
                g.pline("You are paralyzed!");
                u.Energy -= ActionCosts.OneAction.Value;
                continue;
            }

            FovCalculator.Compute(lvl, upos, u.DarkVisionRadius);

            Perf.Start();
            Draw.DrawCurrent();
            Perf.Stop("Draw");

            Perf.Start();
            Input.PlayerTurn();
            Perf.Stop("PlayerTurn");
        }

        // === Monster phase (runs on whatever level we're now on) ===
        lvl.SortUnitsByInitiative();

        foreach (var unit in lvl.LiveUnits)
        {
            if (unit.IsPlayer) continue;
            unit.Energy += 12;
            LogicBrick.FireOnRoundStart(unit);
        }

        foreach (var unit in lvl.LiveUnits)
        {
            if (unit.IsPlayer) continue;
            while (unit.Energy > 1 && !unit.IsDead)
            {
                if (unit.Query("paralyzed", null, MergeStrategy.And, false))
                {
                    unit.Energy -= ActionCosts.OneAction.Value;
                    continue;
                }

                Perf.Start();
                MonsterTurn(unit);
                Perf.Stop("MonsterTurn");
            }
        }

        FovCalculator.Compute(lvl, upos, u.DarkVisionRadius);

        Perf.Start();
        Draw.DrawCurrent();
        Perf.Stop("Draw");

        CleanupFacts();

        // OnRoundEnd for all units
        Perf.Start();
        u.RecalculateMaxHp();
        Hunger.Tick(u);
        foreach (var unit in lvl.LiveUnits)
        {
            unit.ExpireFacts();
            LogicBrick.FireOnRoundEnd(unit);
            unit.TickPools();
            unit.TickTempHp();
        }
        foreach (var entity in ActiveEntities)
        {
            entity.ExpireFacts();
            LogicBrick.FireOnRoundEnd(entity);
        }

        foreach (var area in lvl.AllAreas)
            area.Tick();
        lvl.CleanupAreas();

        // tick corpses on floor
        for (int i = lvl.Corpses.Count - 1; i >= 0; i--)
        {
            var (corpse, pos) = lvl.Corpses[i];
            if (Foods.TickCorpse(corpse, null, pos))
            {
                lvl.RemoveItem(corpse, pos);
            }
        }
        
        // tick corpses in inventories
        foreach (var unit in lvl.LiveUnits)
        {
            for (int i = unit.Inventory.Count - 1; i >= 0; i--)
            {
                var item = unit.Inventory[i];
                if (item.CorpseOf != null && Foods.TickCorpse(item, unit, null))
                    unit.Inventory.RemoveAt(i);
            }
        }
        
        CleanupFacts();
        Perf.Stop("OnRoundEnd");

        // Ambient room sounds
        var specialRooms = lvl.Rooms.Where(r => r.Type != RoomType.Ordinary).ToList();
        if (specialRooms.Count > 0 && Rn2(200) == 0)
        {
            var room = specialRooms.Pick();
            var msg = room.Type switch
            {
                RoomType.GoblinNest => "You hear chanting.",
                RoomType.GremlinParty => "You hear snoring.",
                RoomType.GremlinPartyBig => "You hear a lot of snoring.",
                _ => null
            };
            if (msg != null) pline(msg);
        }

        lvl.ReapDead();
        FlushDeferred();

        foreach (var unit in lvl.LiveUnits)
        {
            int regen = unit.NaturalRegen;
            while (regen > 0)
            {
                if (Rn2(30) < regen)
                    unit.HP += 1;
                regen -= 30;
            }

        }

        Perf.Start();
        MonsterSpawner.TryRuntimeSpawn(lvl);
        Perf.Stop("Runtime spawn");

        // Discover branches whose stairs are visible
        if (lvl.BranchDown is { } bd && lvl.BranchDownTarget is { } bdt && (upos == bd || lvl.IsVisible(bd)))
            bdt.Branch.Discovered = true;
        if (lvl.BranchUp is { } bu && lvl.BranchUpTarget is { } but && (upos == bu || lvl.IsVisible(bu)))
            but.Branch.Discovered = true;

        UI.Draw.DrawCurrent();
        Perf.Start();
        BlackBox.Record();
        Perf.Stop("BlackBox");
        Perf.EndRound();
        CurrentRound++;
    }

    void MonsterTurn(IUnit mon)
    {
        if (mon is not Monster m) { mon.Energy = 0; return; }

        if (!m.DoTurn())
            mon.Energy = Math.Min(mon.Energy, 0);
    }

    public void GoToLevel(LevelId id, SpawnAt where, Pos? whereExactly = null)
    {
        Log.Write($"LevelChange {u.Level} -> {id}");
        if (CurrentLevel is { } old)
        {
            old.RemoveUnit(u);
            old.LastExitTurn = CurrentRound;
        }

        if (!Levels.TryGetValue(id, out Level? level))
        {
            level = LevelGen.Generate(id, Seed);
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
        level.PlaceUnit(u, upos);
        FovCalculator.Compute(level, upos, u.DarkVisionRadius);
        Draw.DrawCurrent();
    }

    public static void LoreDump(string message)
    {
        using var overlay = Draw.Overlay.Activate();
        Draw.Overlay.FullScreen = true;
        int y = RichText.Write(Draw.Overlay, 2, 2, 52, message);
        Draw.OverlayWrite(2, y + 2, "press (space) to continue");
        Draw.Blit();
        while (Input.NextKey().Key != ConsoleKey.Spacebar)
            Draw.Blit();
    }

    public void DoHeal(IUnit source, IUnit target, DiceFormula formula)
    {
        using var ctx = PHContext.Create(source, new Target(target, target.Pos));
        ctx.HealFormula = formula;

        LogicBrick.FireOnBeforeHealGiven(source, ctx);
        LogicBrick.FireOnBeforeHealReceived(target, ctx);

        int roll = ctx.HealFormula.Roll() + ctx.HealModifiers.Calculate();
        int actual = target.HP.Heal(roll);
        ctx.HealedAmount = actual;

        LogicBrick.FireOnAfterHealReceived(target, ctx);

        Log.Write("{0} heals {1} for {2} ({3} actual)", source, target, roll, actual);
    }

    public void DoMapLevel()
    {
        for (int y = 0; y < lvl.Height; y++)
        for (int x = 0; x < lvl.Width; x++)
            lvl.UpdateMemory(new(x, y), includeItems: false);

        if (lvl.BranchDown is { } bd && lvl.BranchDownTarget is { } bdt)
            bdt.Branch.Discovered = true;
        if (lvl.BranchUp is { } bu && lvl.BranchUpTarget is { } but)
            but.Branch.Discovered = true;

        pline("A map coalesces in your mind.");
    }

    public static bool DoAttackRoll(PHContext ctx, int attackBonus = 0)
    {
        IUnit attacker = ctx.Source!;
        IUnit defender = ctx.Target.Unit!;

        Check check = new() { DC = defender.GetAC(), Tag = "attack" };
        ctx.Check = check;

        if (ctx.Weapon?.Def is WeaponDef weapon)
        {
            bool improvised = !ctx.Melee && weapon.Launcher == null;
            if (improvised)
                check.Modifiers.Untyped(-2, "improvised");
            else
                check.Modifiers.Untyped(attacker.GetAttackBonus(weapon), "atk");
            check.Modifiers.Mod(ModifierCategory.ItemBonus, ctx.Weapon!.Potency, "potency");
        }
        else if (ctx.Spell != null)
        {
            check.Modifiers.Untyped(attacker.GetSpellAttackBonus(ctx.Spell), "atk");
        }

        if (attackBonus != 0)
            check.Modifiers.Untyped(attackBonus, "multi_atk");

        LogicBrick.FireOnBeforeAttackRoll(attacker, ctx);
        LogicBrick.FireOnBeforeDefendRoll(defender, ctx);

        // awareness-based advantage/disadvantage
        var atkAwareness = GetAwareness(attacker, defender);
        if (atkAwareness.Disadvantage)
            check.Disadvantage++;
        var defAwareness = GetAwareness(defender, attacker);
        if (!defAwareness.CanTarget)
            check.Advantage++;  // unseen attacker

        // The to hit roll looks backwards, but it is check where the attacker
        // is trying to beat the defender's AC roll, so the attacker *is* the target
        // Now we swap the source and target for the damage roll
        ctx.Source = defender;
        ctx.Target = Target.From(attacker);
        bool hit = DoCheck(ctx, $"{attacker} attacks {defender}");
        ctx.Source = attacker;
        ctx.Target = Target.From(defender);
        // But restore afterwards ^ otherwise everything else looks the wrong way round

        LogicBrick.FireOnAfterAttackRoll(attacker, ctx);
        LogicBrick.FireOnAfterDefendRoll(defender, ctx);

        return hit;
    }

    public static bool DoWeaponAttack(IUnit attacker, IUnit defender, Item with, bool thrown = false, int attackBonus = 0)
    {
        var weapon = with.Def as WeaponDef;
        using var ctx = PHContext.Create(attacker, Target.From(defender));
        ctx.Weapon = with;
        ctx.Melee = !thrown;
        ctx.SilentCheck = true;
        ctx.SilentDamage = true;

        var hit = DoAttackRoll(ctx, attackBonus);
        var check = ctx.Check!;

        string verb = thrown
            ? "throws"
            : weapon?.MeleeVerb ?? "attacks with";

        if (hit)
        {
            DamageRoll dmg = new()
            {
                Formula = weapon?.BaseDamage ?? d(2),
                Type = weapon?.DamageType ?? DamageTypes.Blunt
            };
            dmg.Modifiers.Untyped(attacker.GetDamageBonus(), "str_inherent");
            ctx.Damage.Add(dmg);
            if (weapon != null)
            {
                dmg.Tags.Add(weapon.Material);
                dmg.Tags.Add(weapon.DamageType.SubCat);
            }

            if (thrown)
            {
                if (defender.IsPlayer)
                    g.pline($"{with:The} hits you!");
                else if (attacker.IsPlayer)
                    g.pline($"{with:The} hits {defender:the}.");
                else
                    g.YouObserve(attacker, $"{with:The} hits {defender:the}.");
            }
            else if (attacker.IsPlayer)
            {
                g.pline($"You hit the {defender}.");
            }
            else if (defender.IsPlayer)
            {
                if (weapon?.Category == WeaponCategory.Item)
                    g.pline($"{attacker:The} {verb} its {with}!  {attacker:The} hits!");
                else
                    g.pline($"{attacker:The} hits!");
            }
            else
            {
                if (weapon?.Category == WeaponCategory.Item)
                    g.YouObserve(attacker, $"{attacker:The} {verb} its {with}! {attacker:The} hits {defender:the}.");
                else
                    g.YouObserve(attacker, $"{attacker:The} hits {defender:the}.");
            }
            DoDamage(ctx);

            // Combined attack log
            string tag = attacker.IsPlayer ? "P ✓" : "M ✓";
            string modStr = FormatMods(check.Modifiers);
            string dmgStr = FormatDamage(ctx);
            Log.Write($"{tag} {check.Roll} ({check.BaseRoll}) vs {check.DC}: {attacker} → {defender} {modStr} | {dmgStr} ({ctx.HpBefore}→{ctx.HpAfter})");
        }
        else
        {
            defender.MissesTaken++;
            
            // Combined miss log
            string tag = attacker == u ? "P ✗" : "M ✗";
            string modStr = FormatMods(check.Modifiers);
            Log.Write($"{tag} {check.Roll} ({check.BaseRoll}) vs {check.DC}: {attacker} → {defender} {modStr}");
            
            if (thrown)
            {
                if (defender.IsPlayer)
                    g.pline($"{with:The} misses you.");
                else if (attacker.IsPlayer)
                    g.pline($"{with:The} misses {defender:the}.");
                else
                    g.YouObserve(attacker, $"{with:The} misses {defender:the}.");
            }
            else if (attacker.IsPlayer)
                g.pline($"You miss the {defender}.");
            else if (defender.IsPlayer)
            {
                if (weapon?.Category == WeaponCategory.Item)
                    g.pline($"{attacker:The} {verb} its {with}!  {attacker:The} misses.");
                else
                    g.pline($"{attacker:The} misses.");
            }
            else
            {
                if (weapon?.Category == WeaponCategory.Item)
                    g.YouObserve(attacker, $"{attacker:The} {verb} its {with}!  {attacker:The} misses {defender:the}.");
                else
                    g.YouObserve(attacker, $"{attacker:The} misses {defender:the}.");
            }
        }
        return hit;
    }

    public static void DoDamage(PHContext ctx)
    {
        var source = ctx.Source!;
        var target = ctx.Target.Unit!;

        LogicBrick.FireOnBeforeDamageRoll(source, ctx);
        LogicBrick.FireOnBeforeDamageIncomingRoll(target, ctx);

        if (source.IsPlayer && target is Monster angry && angry.Peaceful)
        {
            g.YouObserve(angry, $"{angry:The} gets angry!", $"angry shouting!");
            angry.Peaceful = false;
        }

        int damage = 0;
        foreach (var dmg in ctx.Damage)
        {
            if (dmg.Negated) continue;

            if (dmg.HalfOnSave && ctx.Check?.Result == true) dmg.Halve();
            if (dmg.DoubleOnFail && ctx.Check?.Result == false) dmg.Double();

            int rolled = DoRoll(dmg.Formula.WithExtra(dmg.ExtraDice), dmg.Modifiers, $"  {dmg} damage");
            damage += dmg.Resolve(rolled);

            if (!ctx.SilentDamage)
            {
                var tags = dmg.Tags.Count > 0 ? string.Join(",", dmg.Tags) : "none";
                Log.Write($"    tags=[{tags}] dr={dmg.DR} prot={dmg.Protection} used={dmg.ProtectionUsed}");
            }
        }
        
        // this can happen if all damage instances were negated
        if (damage == 0) return;

        ctx.HpBefore = target.HP.Current;
        target.LastDamagedOnTurn = g.CurrentRound;
        target.HitsTaken++;
        target.DamageTaken += damage;
        
        damage = target.AbsorbTempHp(damage);
        if (damage == 0)
        {
            if (!ctx.SilentDamage) Log.Write($"  temp HP absorbed all damage");
            return;
        }
        
        target.HP -= damage;
        ctx.TotalDamageDealt = damage;
        ctx.HpAfter = target.HP.Current;

        if (!ctx.SilentDamage)
        {
            Log.Write($"  {target:The} takes {damage} total damage");
            Log.Write($"entity: {target.Id}: hit {damage} dmg hp: {ctx.HpBefore} -> {ctx.HpAfter}");
        }

        if (target is Monster mon && mon.IsAsleep && g.Rn2(100) != 0) mon.IsAsleep = false;

        if (target.IsPlayer)
            Movement.Stop();

        LogicBrick.FireOnDamageDone(source, ctx);
        LogicBrick.FireOnDamageTaken(target, ctx);

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
                    {
                        g.GainExp(20 * Math.Max(1, m.EffectiveLevel), m.Def.Name);
                        g.Vanquished[m.Def.Name] = g.Vanquished.GetValueOrDefault(m.Def.Name) + 1;
                    }
                }
                else if (ctx.DeathReason != null)
                    g.YouObserve(target, $"{target:The} dies by {ctx.DeathReason}!");
                else if (source.IsDM)
                    g.YouObserve(target, $"{target:The} dies!");
                else if (source == target)
                    g.YouObserve(source, $"{source:The} dies!");
                else
                    g.YouObserve(source, $"{source:The} kills {target:the}!");

                using (var death = PHContext.Create(source, Target.From(target)))
                    LogicBrick.FireOnDeath(target, death);

                Log.Write($"entity: {target.Id}: death ({target.HitsTaken} hits, {target.MissesTaken} misses, {target.DamageTaken} dmg)");

                // drop inventory
                foreach (var item in target.Inventory.ToList())
                    DoDrop(target, item);

                // drop corpse
                if (target is Monster m2 && !m2.NoCorpse && ShouldGenerateCorpse(m2, out var doRespawn))
                {
                    var corpse = Item.Create(Foods.Corpse);
                    corpse.CorpseOf = m2.Def;
                    if (m2.IsCreature(CreatureTypes.Undead))
                        corpse.RotTimer = Foods.RotTainted;
                    else
                        corpse.RotTimer = m2.Def.StartingRot;

                    if (doRespawn)
                        corpse.RotTimer = -g.RnRange(15, 30);

                    lvl.PlaceItem(corpse, target.Pos);
                }

                // release grab
                if (target.Grabbing is { } victim)
                {
                    victim.GrabbedBy = null;
                    target.Grabbing = null;
                }
                if (target.GrabbedBy is { } grabber)
                {
                    grabber.Grabbing = null;
                    target.GrabbedBy = null;
                }

                target.IsDead = true;
                lvl.GetOrCreateState(target.Pos).Unit = null;
            }
        }
    }

    public static void DoDrop(IUnit unit, Item item)
    {
        unit.Inventory.Remove(item);
        if (unit is Player p && p.Quiver == item)
            p.Quiver = null;
        lvl.PlaceItem(item, unit.Pos);


        if (unit.IsPlayer && lvl.RoomAt(unit.Pos) is { } room && room.Type == RoomType.Shop)
        {
            room.Resident?.FindFact(ShopkeeperBrick.Instance)?.As<ShopState>()?.Give(item);
        }

        Log.Write("drop: {0}", item.Def.Name);
    }

    public static Pos DoThrow(IUnit thrower, Item item, Pos dir, Pos? from = null)
    {
        Pos pos = from ?? thrower.Pos;
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
        Draw.AnimateProjectile(from ?? thrower.Pos, last, item.Glyph);
        if (hit != null)
            DoWeaponAttack(thrower, hit, item, thrown: true);
        lvl.PlaceItem(item, last);
        return last;
    }

    public int DoPickup(IUnit unit, Item item)
    {
        lvl.RemoveItem(item, unit.Pos);

        var maybeRoom = lvl.RoomAt(unit.Pos);
        Log.Write($"picking up: {unit.IsPlayer}, room:{maybeRoom != null}, shop:{maybeRoom?.Type == RoomType.Shop}");
        int price = 0;
        if (unit.IsPlayer && maybeRoom is {} room && room.Type == RoomType.Shop)
        {
            price = room.Resident?.FindFact(ShopkeeperBrick.Instance)?.As<ShopState>()?.Take(item) ?? 0;
        }

        if (item.Def == MiscItems.SilverCrest && unit is Player p)
        {
            p.Gold += item.Count;
        }
        else
        {
            unit.Inventory.Add(item);
            Log.Write("pickup: {0}", item.Def.Name);
        }

        return price;
    }

    const double XpMultiplier = 2.4;

    static readonly string[] LevelUpNags = [
        "9 out of 10 dentists recommend levelling up. (#levelup)",
        "You could level up, but standing around works too. (#levelup)",
        "Fun fact: unspent experience points do not accrue interest. (#levelup)",
        "Your XP bar is full. This is not a drill. (#levelup)",
        "A nearby sign reads: LEVEL UP OR DIE TRYING. (#levelup)",
        "You feel like you're forgetting something. It's #levelup.",
        "Somewhere, a disappointed mentor shakes their head. (#levelup)",
        "You trip over your unspent experience. (#levelup)",
    ];

    public void GainExp(int amount, string? source = null)
    {
        bool wasPending = Progression.HasPendingLevelUp(u);
        amount = (int)(amount * XpMultiplier);
        u.XP += amount;
        Log.Write($"exp: +{amount} (total {u.XP}) XL={u.CharacterLevel} DL={lvl.Id.Depth} src={source ?? "?"}");
        if (!wasPending && Progression.HasPendingLevelUp(u))
            pline(LevelUpNags.Pick());
    }

    static bool ShouldGenerateCorpse(Monster m, out bool doRespawn)
    {
        var def = m.Def;

        // respawn 90% of the time, seems fair?
        doRespawn = m.Query("respawn_from_corpse", null, MergeStrategy.And, false) && g.Rn2(10) != 0;

        // if we are respawning we have to leave a corpse
        if (doRespawn || def.Size >= UnitSize.Large) return true;
        int denom = 2 + (def.SpawnWeight < 2 ? 1 : 0) + (def.Size == UnitSize.Tiny ? 1 : 0);
        return g.Rn2(denom) == 0;
    }

    public enum StruggleResult { Escaped, Failed }

    /// <summary>
    /// Attempt to escape a grab. Returns Escaped if free, Failed if still grabbed (and attacks grabber).
    /// </summary>
    public StruggleResult DoStruggle(IUnit unit, int dc)
    {
        var grabber = unit.GrabbedBy!;
        using var ctx = PHContext.Create(grabber, Target.From(unit));
        
        if (CreateAndDoCheck(ctx, "athletics", dc, "escape grab"))
        {
            pline($"{unit:The} {VTense(unit, "break")} free from {grabber:the}!");
            unit.GrabbedBy = null;
            grabber.Grabbing = null;
            return StruggleResult.Escaped;
        }
        
        pline($"{unit:The} {VTense(unit, "struggle")} against {grabber:the}!");
        DoWeaponAttack(unit, grabber, unit is Player p ? p.GetWieldedItem() : ((Monster)unit).GetWieldedItem());
        return StruggleResult.Failed;
    }

    public enum EquipResult { Ok, Cursed, NoSlot }

    public EquipResult DoEquip(IUnit unit, Item? item, EquipSlot? slotOverride = null)
    {
        if (item == null)
        {
            if (unit.Unequip(ItemSlots.HandSlot) == UnequipResult.Cursed)
            {
                if (unit.IsPlayer) WeldMsg(unit);
                return EquipResult.Cursed;
            }
        }
        else
        {
            EquipSlot slot = slotOverride ?? new(item.Def.DefaultEquipSlot, "_");
            if (unit.Unequip(slot) == UnequipResult.Cursed)
            {
                if (unit.IsPlayer)
                {
                    var existing = unit.Equipped[slot];
                    if (existing.Def is WeaponDef)
                        WeldMsg(unit);
                    else
                        g.pline("You can't.  It is cursed.");
                }
                return EquipResult.Cursed;
            }
            var result = unit.Equip(item);
            if (result == null)
            {
                unit.Energy -= ActionCosts.OneAction.Value;
                return EquipResult.NoSlot;
            }
            if (item.IsCursed && item.Def is WeaponDef)
            {
                item.Knowledge |= ItemKnowledge.BUC;
                g.pline($"The {item.Def.Name} welds itself to your {HandStr(item)}!");
            }
        }
        unit.Energy -= ActionCosts.OneAction.Value;
        return EquipResult.Ok;
    }

    static string HandStr(Item item) =>
        item.Def is WeaponDef { Hands: > 1 } ? "hands" : "hand";

    static void WeldMsg(IUnit unit)
    {
        var weapon = unit.GetWieldedItem();
        g.pline($"Your {weapon.Def.Name} is welded to your {HandStr(weapon)}!");
    }

    public bool DoUnequip(IUnit unit, Item item)
    {
        var slot = unit.Equipped.First(kv => kv.Value == item).Key;
        var r = unit.Unequip(slot);
        if (r == UnequipResult.Cursed)
        {
            if (unit.IsPlayer)
            {
                if (item.Def is WeaponDef)
                    WeldMsg(unit);
                else
                    g.pline("You can't.  It is cursed.");
            }
            return false;
        }
        unit.Energy -= ActionCosts.OneAction.Value;
        return true;
    }

    internal static void ResetGameState() => g = new();

    void CleanupFacts()
    {
        foreach (var entity in PendingFactCleanup)
            entity.CleanupMarkedFacts();
        PendingFactCleanup.Clear();
    }

    internal static void FlashLit(TileBitset moreLit) => FovCalculator.Compute(lvl, upos, u.DarkVisionRadius, moreLit);

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
            // This looks garbage but it works and it probably won't change.
            default:
                if (lvl.Traps.TryGetValue(upos, out var trap) && trap is HoleTrap hole)
                {
                    var below = HoleTrap.LevelBelow(u.Level);
                    if (below != null)
                    {
                        GoToLevel(below.Value, SpawnAt.RandomLegal);
                    }
                    else
                    {
                        pline("The floor vibrates ominously, but holds.");
                        return;
                    }
                }
                else return;
                break;
        }
        Log.Write($"Portal: after level={u.Level} pos={upos}");
        unit.Energy -= unit.LandMove.Value;
    }

}
