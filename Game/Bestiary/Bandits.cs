
namespace Pathhack.Game.Bestiary;

public class AmbushTrap(int depth) : Trap(TrapType.Ambush, depth, detectDelta: -2, escapeDelta: 0, escapeBonus: 0)
{
    public override MoveMode TriggeredBy => MoveMode.Walk;
    public override Glyph Glyph => new('^', ConsoleColor.DarkGray);

    public override bool Trigger(IUnit? unit, Item? item)
    {
        if (unit?.IsPlayer == true)
        {
            using var ctx = PHContext.Create(DungeonMaster.Mook, Target.From(unit));

            // If you know there's a trap here it's easier to avoid the
            // ambush... tbh should all traps have this as a helper; later
            int seenBefore = PlayerSeen ? -4 : 1;
            if (CreateAndDoCheck(ctx, "perception", DetectDC + seenBefore, "trap"))
            {
                g.pline("You narrowly avoid an ambush.");
                return true;
            }
        }
        else
        {
            return false;
        }

        int count = g.RnRange(2, 4);
        int spawned = 0;
        foreach (var dir in Pos.AllDirs.Shuffled())
        {
            if (spawned >= count) break;
            Pos pos = unit.Pos + dir;
            if (!lvl.InBounds(pos) || !lvl.CanMoveTo(pos, pos, unit) || lvl.UnitAt(pos) != null) continue;
            if (MonsterSpawner.SpawnAndPlace(lvl, "ambush", null, allowTemplate: false, pos: pos, filter: IsAmbusher, noGroup: true))
                spawned++;
        }

        if (spawned > 0)
        {
            string we = spawned > 1 ? "we" : "I";
            g.YouObserveSelf(unit, "Shadowy figures rise from the... shadows!", $"{unit:The} is getting mugged!", $"\"Your money or... actually {we}'ll take both\"");
            return true;
        }
        else
        {
            return g.YouObserve(unit, $"Looks like {unit:the} stepped into an ambush... but no one's home.");
        }
    }

    private static bool IsAmbusher(MonsterDef m) => m.Family == "bandit" && m != Bandits.Cutpurse;
}

public class LongRangeAdvantage() : LogicBrick
{
    public static readonly LongRangeAdvantage Instance = new();
    public override string Id => "bandit:long_range_adv";

    protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
        if (fact.Entity is IUnit unit && ctx.Target?.Unit is IUnit t
            && unit.Pos.ChebyshevDist(t.Pos) > 5)
        {
            if (g.Rn2(5) == 0) g.YouObserve(unit, $"{unit:The} lines up a long range shot.");
            ctx.Check!.Advantage++;
        }
    }
}

public class StealCrestsOnHit : LogicBrick
{
    public static readonly StealCrestsOnHit Instance = new();
    public override string Id => "bandit:steal_crests";

    public override AbilityTags Tags => AbilityTags.Mental; // it requires a bit of thought to steal money imo

    protected override void OnDamageDone(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not Monster m || !ctx.Melee) return;
        if (ctx.Target?.Unit is not Player p || p.Gold <= 0) return;
        if (m.HasFact(FencingBuff.Instance)) return; // already fencing

        int pct = g.RnRange(5, 30);
        long stolen = Math.Max(1, p.Gold * pct / 100);
        p.Gold -= stolen;
        m.Gold += stolen;

        g.pline($"Your purse seems lighter!");

        // fence time: ~1 round per 50 gold, min 3, max 20
        int fenceTime = Math.Clamp((int)(stolen / 50), 3, 20);
        g.Defer(() => m.AddFact(FencingBuff.Instance, fenceTime));
    }
}

public class FencingBuff : LogicBrick
{
    public static readonly FencingBuff Instance = new();
    public override string Id => "fencing";
    public override bool IsActive => true;
    public override bool IsBuff => true;
    public override string? BuffName => "Fencing";
    public override StackMode StackMode => StackMode.Reject;

    public override AbilityTags Tags => AbilityTags.Mental; // it requires a bit of thought to steal money imo

    protected override object? OnQuery(Fact fact, string key, string? arg) => key.TrueWhen("fleeing");

    protected override void OnRoundEnd(Fact fact)
    {
        if (fact.Entity is Monster m && !m.IsDead && fact.ExpiresAt - g.CurrentRound == 4)
        {
            g.YouObserve(m, $"{m:The} is trying to fence your hard earned {EconomySystem.Coins}!", "whispers of illicit trade");
        }
    }

    protected override void OnFactRemoved(Fact fact)
    {
        // timer expired — cutpurse escapes with the gold
        if (fact.Entity is Monster m && !m.IsDead)
        {
            m.Gold = 0;
            g.YouObserve(m, $"{m:The} disappears into the shadows with your {EconomySystem.Coins}!", "the clinking of coins getting ever fainter");
            m.IsDead = true;
        }
    }
}

public class DazeOnHit : LogicBrick
{
    public static readonly DazeOnHit Instance = new();
    public override string Id => "bandit:daze_on_hit";

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (!ctx.Check!.Result || !ctx.Melee) return;
        if (ctx.Target?.Unit is { } target && !target.Has(CommonQueries.DazeImmune))
            target.AddFact(DazedBuff.Instance, 1);
    }
}

public class BanditHealAlly(Dice heal, int range, string pool, int cd)
    : CooldownAction("Heal Ally", TargetingType.None, _ => cd, tags: AbilityTags.Beneficial)
{
    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        var basePlan = base.CanExecute(unit, data, target);
        if (!basePlan) return basePlan;
        if (!unit.HasCharge(pool, out var whyNot)) return new(false, whyNot);
        return FindAlly(unit) is { } ally ? new ActionPlan(true, Plan: ally) : "no wounded ally";
    }

    protected override void Execute(IUnit unit, Target target, object? plan = null)
    {
        var ally = plan as Monster ?? FindAlly(unit);
        if (ally == null) return;
        unit.TryUseCharge(pool);
        g.DoHeal(unit, ally, heal.Roll());
        g.YouObserve(unit, $"{unit:The} heals {ally:the}!", "a prayer");
    }

    Monster? FindAlly(IUnit unit)
    {
        string? family = (unit as Monster)?.Def.Family;
        Monster? best = null;
        double bestPct = 1.0;
        foreach (var m in lvl.LiveUnits)
        {
            if (m is not Monster mon || mon == unit) continue;
            if (family != null && mon.Def.Family != family) continue;
            if (mon.Pos.ChebyshevDist(unit.Pos) > range) continue;
            double pct = (double)mon.HP.Current / mon.HP.Max;
            if (pct >= 0.5) continue;
            if (pct < bestPct) { best = mon; bestPct = pct; }
        }
        return best;
    }
}

public static class Bandits
{
    // --- Equipment sets ---

    static readonly EquipSet LightWeapon = EquipSet.OneOf(MundaneArmory.Dagger, MundaneArmory.Shortsword);
    static readonly EquipSet MediumWeapon = EquipSet.OneOf(MundaneArmory.Shortsword, MundaneArmory.Longsword, MundaneArmory.Scimitar);
    static readonly EquipSet HeavyWeapon = EquipSet.OneOf(MundaneArmory.Longsword, MundaneArmory.Battleaxe, MundaneArmory.Flail);
    static readonly EquipSet LightArmor = EquipSet.Roll(MundaneArmory.LeatherArmor, 70);
    static readonly EquipSet MediumArmor = EquipSet.OneOf(MundaneArmory.LeatherArmor, MundaneArmory.ChainShirt);
    static readonly EquipSet HeavyArmor = EquipSet.OneOf(MundaneArmory.Breastplate, MundaneArmory.SplintMail);

    static MonsterDef B(string id, string name, int level, int maxDepth, ConsoleColor color,
        LogicBrick[] components, int hp = 5, int ac = 0, int ab = 0, int dmg = 0,
        GroupSize group = GroupSize.SmallMixed, int spawnWeight = 10,
        MonFlags flags = MonFlags.None, Func<MonsterDef>? growsInto = null)
    {
        return new MonsterDef
        {
            id = id,
            Name = name,
            Family = "bandit",
            CreatureType = CreatureTypes.Humanoid,
            Glyph = new('@', color),
            HpPerLevel = hp,
            AC = ac,
            AttackBonus = ab,
            DamageBonus = dmg,
            Unarmed = NaturalWeapons.Fist,
            Size = UnitSize.Medium,
            BaseLevel = level,
            MinDepth = 1,
            MaxDepth = maxDepth,
            GroupSize = group,
            SpawnWeight = spawnWeight,
            MoralAxis = MoralAxis.Evil,
            EthicalAxis = EthicalAxis.Neutral,
            BrainFlags = flags,
            Components = components,
            GrowsInto = growsInto,
        };
    }

    // --- Mundane ---

    public static readonly MonsterDef Cutpurse = B("bandit_cutpurse", "cutpurse", 1, 5, ConsoleColor.DarkGray,
        hp: 4, ac: -1, dmg: -1, flags: MonFlags.Cowardly, growsInto: () => Bandit!,
        components: [
            new Equip(MundaneArmory.Dagger),
            StealCrestsOnHit.Instance,
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Bandit = B("bandit", "bandit", 2, 7, ConsoleColor.White,
        growsInto: () => Thug!,
        components: [
            LightWeapon,
            LightArmor,
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Thug = B("bandit_thug", "bandit thug", 3, 9, ConsoleColor.Red,
        hp: 6, ac: 1, dmg: 1, growsInto: () => Lord!,
        components: [
            MediumWeapon,
            MediumArmor,
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    // --- Ranged ---

    public static readonly MonsterDef Archer = B("bandit_archer", "bandit archer", 3, 9, ConsoleColor.Green,
        components: [
            new Equip(MundaneArmory.Longbow),
            new Equip(MundaneQuivers.BasicArrows),
            new Equip(MundaneArmory.Dagger),
            LightArmor,
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Sniper = B("bandit_sniper", "bandit sniper", 6, 12, ConsoleColor.Green,
        ab: 1, dmg: 1,
        components: [
            new Equip(MundaneArmory.Longbow),
            new Equip(MundaneQuivers.BasicArrows),
            new Equip(MundaneArmory.Dagger),
            LightArmor,
            LongRangeAdvantage.Instance,
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    // --- Specialists ---

    public static readonly MonsterDef Pugilist = B("bandit_pugilist", "bandit pugilist", 4, 10, ConsoleColor.Gray,
        ac: 1,
        components: [
            DazeOnHit.Instance,
            new GrantAction(AttackWithWeapon.Instance),
            new GrantAction(new FullAttack("pugilist", NaturalWeapons.Fist, NaturalWeapons.Fist)),
        ]);

    public static readonly MonsterDef Bruiser = B("bandit_bruiser", "bandit bruiser", 4, 10, ConsoleColor.Red,
        hp: 8, ac: 1, dmg: 1, growsInto: () => Lord!,
        components: [
            HeavyWeapon,
            HeavyArmor,
            new ApplyFactOnAttackHit(ProneBuff.Instance.Timed(), duration: 2),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    // --- Casters ---

    public static readonly MonsterDef Acolyte = B("bandit_acolyte", "bandit acolyte", 5, 11, ConsoleColor.DarkYellow,
        hp: 5, ab: -1, growsInto: () => Cleric!,
        components: [
            new Equip(MundaneArmory.Club),
            new GrantPool("bandit_heal", 2, 15),
            new GrantAction(new BanditHealAlly(d(6) + 2, 5, "bandit_heal", 8)),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef HedgeWizard = B("bandit_hedge_wizard", "bandit hedge wizard", 5, 11, ConsoleColor.DarkCyan,
        hp: 4, ac: -1, ab: -1, flags: MonFlags.PrefersCasting, growsInto: () => Mage!,
        components: [
            new Equip(MundaneArmory.Quarterstaff),
            new GrantPool("spell_l1", 2, 20),
            new GrantSpell(BasicLevel1Spells.MagicMissile),
            new GrantSpell(BasicLevel1Spells.Grease),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Mage = B("bandit_mage", "bandit mage", 7, 13, ConsoleColor.Cyan,
        hp: 4, ac: -1, ab: -1, flags: MonFlags.PrefersCasting,
        components: [
            new Equip(MundaneArmory.Quarterstaff),
            new GrantPool("spell_l1", 2, 15),
            new GrantPool("spell_l2", 1, 25),
            new GrantSpell(BasicLevel1Spells.MagicMissile),
            new GrantSpell(BasicLevel1Spells.BurningHands),
            new GrantSpell(BasicLevel1Spells.Shield),
            new GrantSpell(BasicLevel2Spells.ScorchingRay),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Cleric = B("bandit_cleric", "bandit cleric", 7, 13, ConsoleColor.Yellow,
        hp: 6, ac: 1, flags: MonFlags.PrefersCasting,
        components: [
            new Equip(MundaneArmory.Mace),
            MediumArmor,
            new GrantPool("bandit_heal", 3, 12),
            new GrantPool("spell_l1", 2, 15),
            new GrantPool("spell_l2", 1, 25),
            new GrantAction(new BanditHealAlly(d(8) + 4, 5, "bandit_heal", 6)),
            new GrantSpell(BasicLevel1Spells.BurningHands),
            new GrantSpell(BasicLevel2Spells.HoldPerson),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    // --- Leaders ---

    public static readonly MonsterDef Lord = B("bandit_lord", "bandit lord", 8, 14, ConsoleColor.Blue,
        hp: 7, ac: 2, ab: 1, dmg: 1, spawnWeight: 5, growsInto: () => Boss!,
        components: [
            HeavyWeapon,
            HeavyArmor,
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef Boss = B("bandit_boss", "bandit boss", 10, 14, ConsoleColor.Magenta,
        hp: 8, ac: 2, ab: 1, dmg: 2, spawnWeight: 3,
        components: [
            new Equip(MundaneArmory.Longsword),
            new Equip(MundaneArmory.Breastplate),
            new GrantAction(AttackWithWeapon.Instance),
        ]);

    public static readonly MonsterDef[] All = [
        Cutpurse, Bandit, Thug, Archer, Pugilist, Bruiser,
        Acolyte, HedgeWizard, Sniper, Mage, Cleric, Lord, Boss,
    ];
}
