using System.Diagnostics;
using System.Formats.Tar;

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

    public record struct Awareness(bool CanTarget, bool Disadvantage, bool Visual);

    public static Awareness GetAwareness(IUnit viewer, IUnit target)
    {
        // monster<->monster always full awareness (for now)
        if (!viewer.IsPlayer && !target.IsPlayer)
            return new(true, false, true);

        int tremor = viewer.Query<int>("tremorsense", null, MergeStrategy.Max, 0);
        bool hasTremor = tremor > 0 && viewer.Pos.ChebyshevDist(target.Pos) <= tremor;

        bool blind = !viewer.Can("can_see");
        bool targetInvis = target.Has("invisible") && !viewer.Has("see_invisible");
        bool targetInDark = !lvl.IsLit(target.Pos) && !viewer.Has("darkvision");

        // LOS check for visual detection (player-centric)
        bool hasLOS = viewer.IsPlayer ? lvl.HasLOS(target.Pos) : lvl.HasLOS(viewer.Pos);

        bool visual = !blind && !targetInvis && !targetInDark && hasLOS;
        bool canTarget = visual || hasTremor;
        bool disadvantage = canTarget && !visual && !hasTremor;

        // blind_fight removes disadvantage but not visual
        if (disadvantage && blind && viewer.Has("blind_fight"))
            disadvantage = false;

        return new(canTarget, disadvantage, visual);
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
    public Dictionary<string, Branch> Branches { get; set; } = [];
    public Dictionary<LevelId, Level> Levels { get; } = [];
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

        check.Roll = baseRoll + check.Modifiers.Calculate();

        var parts = check.Modifiers.Stackable.Concat(check.Modifiers.Unstackable.Values)
            .Where(m => m.Value != 0)
            .Select(m => m.Value > 0 ? $"+{m.Value} ({m.Label})" : $"{m.Value} ({m.Label})");
        string modStr = string.Join(" ", parts);
        string advStr = hasAdv ? " (adv)" : hasDis ? " (dis)" : "";
        string tag = ctx.Source == u ? "pcheck" : "mcheck";
        Log.Write($"{tag}: {label} d20={baseRoll}{advStr} {modStr}= {check.Roll} vs DC {check.DC}");

        return check.Result;
    }

    public static bool CreateAndDoCheck(PHContext ctx, string modifierKey, int dc, string label)
    {
        var target = ctx.Target.Unit!;
        Check check = new() { DC = dc, Tag = label, Key = modifierKey };
        ctx.Check = check;

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
        if (!(CurrentLevel is { } lvl)) return;

        Perf.StartRound();
        Draw.ResetRoundStats();
        lvl.SortUnitsByInitiative();

        LevelId startLevel = u.Level;

        foreach (var Unit in lvl.LiveUnits)
        {
            Unit.Energy += 12;
        }

        // OnRoundStart for active entities and all units
        Perf.Start();
        foreach (var entity in ActiveEntities)
            LogicBrick.FireOnRoundStart(entity, PHContext.Create(null, Target.None));
        foreach (var unit in lvl.LiveUnits)
        {
            LogicBrick.FireOnRoundStart(unit, PHContext.Create(unit, Target.None));
        }
        Perf.Stop("OnRoundStart");

        foreach (var unit in lvl.LiveUnits)
        {
            // Check for dead here in case somethign kills itself
            while (unit.Energy > 1 && !unit.IsDead)
            {
                if (u.Level != startLevel) break;

                // Paralysis check (And merge so immunity can override)
                if (unit.Query("paralyzed", null, MergeStrategy.And, false))
                {
                    if (unit.IsPlayer)
                        g.pline("You are paralyzed!");
                    unit.Energy -= ActionCosts.OneAction.Value;
                    continue;
                }

                FovCalculator.Compute(lvl, upos, u.DarkVisionRadius);

                Perf.Start();
                Draw.DrawCurrent();
                Perf.Stop("Draw");

                if (unit.IsPlayer)
                {
                    Perf.Start();
                    Input.PlayerTurn();
                    Perf.Stop("PlayerTurn");
                    if (u.Level != startLevel) break;
                }
                else
                {
                    Perf.Start();
                    MonsterTurn(unit);
                    Perf.Stop("MonsterTurn");
                }
                FovCalculator.Compute(lvl, upos, u.DarkVisionRadius);

                Perf.Start();
                Draw.DrawCurrent();
                Perf.Stop("Draw");

                CleanupFacts();
            }
            if (u.Level != startLevel) break;
        }

        // OnRoundEnd for all units
        Perf.Start();
        u.RecalculateMaxHp();
        Hunger.Tick(u);
        foreach (var unit in lvl.LiveUnits)
        {
            unit.ExpireFacts();
            LogicBrick.FireOnRoundEnd(unit, PHContext.Create(unit, Target.None));
            unit.TickPools();
            unit.TickTempHp();
        }
        foreach (var entity in ActiveEntities)
        {
            entity.ExpireFacts();
            LogicBrick.FireOnRoundEnd(entity, PHContext.Create(null, Target.None));
        }
        foreach (var area in lvl.Areas)
            area.Tick();
        lvl.Areas.RemoveAll(x => g.CurrentRound >= x.ExpiresAt);
        
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

        UI.Draw.DrawCurrent();
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
        pline("A map coalesces in your mind.");
    }

    public void Attack(IUnit attacker, IUnit defender, Item with, bool thrown = false, int attackBonus = 0)
    {
        var weapon = with.Def as WeaponDef;
        Target target = new(defender, defender.Pos);
        using var ctx = PHContext.Create(attacker, target);
        ctx.Weapon = with;
        ctx.Melee = !thrown;

        string verb = thrown
            ? "throws"
            : weapon?.MeleeVerb ?? "attacks with";

        Check check = new() { DC = defender.GetAC(), Tag = "attack" };
        ctx.Check = check;

        if (weapon != null)
        {
            bool improvised = thrown && weapon.Launcher == null;
            if (improvised)
                check.Modifiers.Untyped(-2, "improvised");
            else
                check.Modifiers.Untyped(attacker.GetAttackBonus(weapon), "atk");
            check.Modifiers.Mod(ModifierCategory.ItemBonus, with.Potency, "potency");
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

        bool hit = DoCheck(ctx, $"{attacker} attacks {defender}");

        LogicBrick.FireOnAfterAttackRoll(attacker, ctx);
        LogicBrick.FireOnAfterDefendRoll(defender, ctx);

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
                    pline($"{with:The} hits you!");
                else
                    pline($"{with:The} hits {defender:the}.");
            }
            else if (attacker.IsPlayer)
            {
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
            defender.MissesTaken++;
            Log.Write($"entity: {defender.Id}: miss");
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

        LogicBrick.FireOnBeforeDamageRoll(source, ctx);
        LogicBrick.FireOnBeforeDamageIncomingRoll(target, ctx);

        int damage = 0;
        foreach (var dmg in ctx.Damage)
        {
            if (dmg.Negated) continue;

            if (dmg.HalfOnSave && ctx.Check?.Result == true) dmg.Halve();
            if (dmg.DoubleOnFail && ctx.Check?.Result == false) dmg.Double();

            int rolled = DoRoll(dmg.Formula, dmg.Modifiers, $"  {dmg} damage");
            damage += dmg.Resolve(rolled);

            var tags = dmg.Tags.Count > 0 ? string.Join(",", dmg.Tags) : "none";
            Log.Write($"    tags=[{tags}] dr={dmg.DR} prot={dmg.Protection} used={dmg.ProtectionUsed}");
        }
        
        // this can happen if all damage instances were negated
        if (damage == 0) return;

        Log.Write($"  {target:The} takes {damage} total damage");
        target.LastDamagedOnTurn = g.CurrentRound;
        target.HitsTaken++;
        target.DamageTaken += damage;
        Log.Write($"entity: {target.Id}: hit {damage} dmg");
        
        damage = target.AbsorbTempHp(damage);
        if (damage == 0)
        {
            Log.Write($"  temp HP absorbed all damage");
            return;
        }
        
        target.HP -= damage;

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
                        g.GainExp(20 * Math.Max(1, m.EffectiveLevel), m.Def.Name);
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
                    g.DoDrop(target, item);

                // drop corpse
                if (target is Monster m2 && !m2.Def.NoCorpse && CorpseChance(m2.Def))
                {
                    var corpse = Item.Create(Foods.Corpse);
                    corpse.CorpseOf = m2.Def;
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

    public void DoDrop(IUnit unit, Item item)
    {
        unit.Inventory.Remove(item);
        if (unit is Player p && p.Quiver == item)
            p.Quiver = null;
        lvl.PlaceItem(item, unit.Pos);
        Log.Write("drop: {0}", item.Def.Name);
    }

    public Pos DoThrow(IUnit thrower, Item item, Pos dir, Pos? from = null)
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
        int total = 150;
        int frames = last.ChebyshevDist(from ?? thrower.Pos);
        if (frames > 0)
        {
            int perFrame = total / frames;
            Draw.AnimateProjectile(from ?? thrower.Pos, last, item.Glyph, perFrame);
        }
        if (hit != null)
            Attack(thrower, hit, item, thrown: true);
        lvl.PlaceItem(item, last);
        return last;
    }

    public void DoPickup(IUnit unit, Item item)
    {
        lvl.RemoveItem(item, unit.Pos);

        unit.Inventory.Add(item);
        Log.Write("pickup: {0}", item.Def.Name);
    }

    const int XpMultiplier = 3;

    public void GainExp(int amount, string? source = null)
    {
        amount *= XpMultiplier;
        u.XP += amount;
        Log.Write($"exp: +{amount} (total {u.XP}) XL={u.CharacterLevel} DL={lvl.Id.Depth} src={source ?? "?"}");
    }

    static bool CorpseChance(MonsterDef def)
    {
        if (def.Size >= UnitSize.Large) return true;
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
        Attack(unit, grabber, unit is Player p ? p.GetWieldedItem() : ((Monster)unit).GetWieldedItem());
        return StruggleResult.Failed;
    }

    public void DoEquip(IUnit unit, Item? item, EquipSlot? slotOverride = null)
    {
        if (item == null)
        {
            // bare hands
            unit.Unequip(ItemSlots.HandSlot);
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
        }
        Log.Write($"Portal: after level={u.Level} pos={upos}");
    }
}
