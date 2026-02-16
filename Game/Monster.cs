namespace Pathhack.Game;

public enum MoralAxis { Evil = -1, Neutral = 0, Good = 1 }
public enum EthicalAxis { Chaotic = -1, Neutral = 0, Lawful = 1 }

public static class AxisExts
{
  public static EthicalAxis Binary(this EthicalAxis a) => a == EthicalAxis.Chaotic ? EthicalAxis.Chaotic : EthicalAxis.Lawful;
  public static MoralAxis Binary(this MoralAxis a) => a == MoralAxis.Evil ? MoralAxis.Evil : MoralAxis.Good;
}

public static class CreatureTypes
{
  public const string Humanoid = "humanoid";
  public const string Beast = "beast";
  public const string Undead = "undead";
  public const string Construct = "construct";
  public const string Elemental = "elemental";
  public const string Outsider = "outsider";
  public const string Aberration = "aberration";
  public const string Dragon = "dragon";
  public const string Fey = "fey";
  public const string Plant = "plant";
  public const string Ooze = "ooze";
  public const string Giant = "giant";
}

public static class CreatureTags
{
  public const string Fire = "fire";
  public const string Cold = "cold";
  public const string Air = "air";
  public const string Earth = "earth";
  public const string Water = "water";
  public const string Acid = "acid";
  public const string Incorporeal = "incorporeal";
  public const string Swarm = "swarm";
  public const string Aquatic = "aquatic";
  public const string Flying = "flying";
  public const string Swimming = "swimming";
  public const string Phasing = "phasing";
  public const string Mindless = "mindless";
}

public static class CreatureSubtypes
{
  public const string Demon = "demon";
  public const string Devil = "devil";
  public const string Angel = "angel";
  public const string Shapechanger = "shapechanger";
  public const string Goblinoid = "goblinoid";
  public const string Orc = "orc";
  public const string Elf = "elf";
  public const string Dwarf = "dwarf";
  public const string Giant = "giant";
}

public enum GroupSize { None, Small, SmallMixed, Large, LargeMixed }

[Flags]
public enum MonFlags
{
    None            = 0,
    PrefersCasting  = 1 << 0,
    Stalks          = 1 << 1,
    WaitsForPlayer  = 1 << 2,
    Cowardly        = 1 << 3,
    NoCorpse        = 1 << 4,
}

public enum Approach { Undirected, Approach, Flee }

public abstract class MonsterBrain
{
  public abstract bool DoTurn(Monster m);
}

// Item ac is added on based on rolled equipment.
public record struct MonsterAC(int Combined, int FlatFooted)
{
  public static implicit operator MonsterAC(int ac) => new(ac, ac);
}

public class MonsterDef : BaseDef
{
  public MonsterBrain? Brain;
  public required string Name;
  public required Glyph Glyph;
  public required int HpPerLevel;
  public required MonsterAC AC;
  public required int AttackBonus;
  public int DamageBonus = 0;
  public int StrMod = 0;
  public string[] CanUse = [];
  public ActionCost LandMove = 12;
  public required WeaponDef Unarmed;
  public UnitSize Size = UnitSize.Small;
  public int BaseLevel = 1;
  public int SpawnWeight = 10;
  public int MinDepth = 1;
  public bool IsUnique = false;
  public bool Peaceful = false;
  public bool Stationary = false;
  public int MaxDepth = 99;
  public required string Family;
  public Action<Monster>? OnChat;
  public required MoralAxis MoralAxis;
  public required EthicalAxis EthicalAxis;
  public required string CreatureType = CreatureTypes.Humanoid;
  public HashSet<string> Subtypes = [];
  public int StartingRot = 0;
  public GroupSize GroupSize = GroupSize.None;
  public MonFlags BrainFlags = MonFlags.None;
  public Func<MonsterDef>? GrowsInto;

  string? _creatureTypeKey;
  public string CreatureTypeKey => _creatureTypeKey ??= Subtypes.Count == 0
      ? CreatureType
      : $"{CreatureType}/{string.Join('.', Subtypes)}";

  public virtual int Nutrition => Size switch
  {
      UnitSize.Tiny => 50,
      UnitSize.Small => 150,
      UnitSize.Medium => 400,
      UnitSize.Large => 700,
      UnitSize.Huge => 1200,
      UnitSize.Gargantuan => 2000,
      _ => 200
  };
}

public abstract class MonsterTemplate(string id)
{
  public string Id => id;
  public abstract bool CanApplyTo(MonsterDef def);
  public abstract int LevelBonus(MonsterDef def, int level);
  public virtual IEnumerable<LogicBrick> GetComponents(MonsterDef def) => def.Components;
  public virtual void ModifySpawn(Monster m) { }

  public static readonly List<MonsterTemplate> All = [
    new ZombieTemplate(),
    new SkeletonTemplate(),
  ];

}

public enum PlayerPerception { None, Guess, Unease, Warned, Detected, Visible }

public class Monster : Unit<MonsterDef>, IFormattable
{
  private Monster(MonsterDef def, IEnumerable<LogicBrick> components) : base(def, components) { }

  public PlayerPerception Perception;

  public static bool Hates(Monster a, Monster b)
  {
    if (a == b) return false;
    if (g.GlobalHatred) return true;
    bool aUndead = a.IsCreature(CreatureTypes.Undead);
    bool bUndead = b.IsCreature(CreatureTypes.Undead);
    if (aUndead != bUndead) return true;
    return false;
  }

  public override bool IsDM => false;

  public override int NaturalRegen => 3 + EffectiveLevel;
  public override int StrMod => Def.StrMod;

  // These are copied from def by default but we modify them if we need (typically through the template)
  public string? OwnCreatureType;
  public HashSet<string>? OwnSubtypes;
  public MoralAxis? OwnMoralAxis;
  public EthicalAxis? OwnEthicalAxis;
  public Glyph? OwnGlyph;
  public MonFlags? OwnBrainFlags;

  public MonFlags EffectiveBrainFlags => OwnBrainFlags ?? Def.BrainFlags;
  public bool HasBrainFlag(MonFlags flag) => (EffectiveBrainFlags & flag) != 0;

  public override MoralAxis MoralAxis => Query("moral_axis", null, MergeStrategy.Replace, OwnMoralAxis ?? Def.MoralAxis);
  public override EthicalAxis EthicalAxis => Query("ethical_axis", null, MergeStrategy.Replace, OwnEthicalAxis ?? Def.EthicalAxis);
  public override bool IsCreature(string? type = null, string? subtype = null) =>
    (type == null || (OwnCreatureType ?? Def.CreatureType) == type) && (subtype == null || (OwnSubtypes ?? Def.Subtypes).Contains(subtype));

  string? _creatureTypeKey;
  public string CreatureTypeKey => _creatureTypeKey ?? Def.CreatureTypeKey;

  // where monster's current target is (player, grudge, etc.)
  public Pos? TargetPos { get; private set; }
  public int TargetPosAge { get; private set; }
  public Approach Approach { get; private set; }

  void SetTargetPos(Pos pos)
  {
    TargetPos = pos;
    TargetPosAge = g.CurrentRound;
  }

  public bool IsApparentPosFresh => TargetPos != null && g.CurrentRound - TargetPosAge <= 3;

  public bool IsAsleep;

  // anti-oscillation: last N positions
  readonly Pos[] _track = [Pos.Invalid, Pos.Invalid, Pos.Invalid, Pos.Invalid, Pos.Invalid];
  int _trackIdx;

  void RecordMove(Pos p)
  {
    _track[_trackIdx] = p;
    _trackIdx = (_trackIdx + 1) % _track.Length;
  }

  public Pos PrevPos => _track[(_trackIdx - 1 + _track.Length) % _track.Length];

  bool WasRecentlyAt(Pos p) => _track.Any(x => x == p);

  public bool CanSeeYou => CanSee(this, u);

  void UpdateApparentPos()
  {
    // always knows (pet, grabber, etc)
    if (Has("always_knows_u") && !IsAsleep)
    {
      SetTargetPos(upos);
      return;
    }

    if (CanSeeYou && !IsAsleep)
    {
      SetTargetPos(upos);
      return;
    }

    // hearing: within 10 tiles, 1/7 chance (or always if keen ears)
    // blocked by stealth, requires LOS from player
    if (!u.Has("stealth") && Pos.ChebyshevDist(upos) <= 10 && lvl.HasLOS(Pos))
    {
      if (Has("keen_ears") || g.Rn2(7) == 0)
      {
        SetTargetPos(upos);
        return;
      }
    }

    // aggravate monster - player always detected
    if (u.Has("aggravate_monster"))
    {
      SetTargetPos(upos);
      return;
    }

    // adjacent stumble - 1/8 chance to notice adjacent player
    if (Pos.ChebyshevDist(upos) <= 1 && g.Rn2(8) == 0)
    {
      SetTargetPos(upos);
      return;
    }

    // can't see - chance to forget if at stale pos or random
    if (TargetPos is { } last)
    {
      // if we're adjacent to where we thought player was but they're not there, or 1/100
      if ((Pos.ChebyshevDist(last) <= 1 && last != upos) || g.Rn2(100) == 0)
        TargetPos = null;
    }
  }

  public override bool IsPlayer => false;

  public int TemplateBonusLevels = 0;
  public int ItemBonusAC = 0;

  public override int GetAC()
  {
    // TODO: check for flat footed
    return LevelDC + Def.AC.Combined + ItemBonusAC;
  }

  private int LevelDC => DungeonMaster.DCForLevel(EffectiveLevel);

  // see tools/dc.md, this gives us what seems to be a reasonable curve
  private const int ATTACK_PENALTY_FUDGE = 5;

  public override ActionCost LandMove
  {
      get
      {
          int cost = Def.LandMove.Value - QueryModifiers("speed_bonus").Calculate();
          double mult = Query<double>("speed_mult", null, MergeStrategy.Replace, 1.0);
          return (int)(cost / mult);
      }
  }
  public override int GetAttackBonus(WeaponDef weapon) => LevelDC - ATTACK_PENALTY_FUDGE + Def.AttackBonus;
  public override int GetSpellAttackBonus(SpellBrickBase brick) => LevelDC - ATTACK_PENALTY_FUDGE + Def.AttackBonus;
  const int DamageFudge = 2;
  public override int GetDamageBonus() => Def.DamageBonus + TemplateBonusLevels + DamageFudge;
  public override int GetSpellDC() => LevelDC;
  protected override WeaponDef GetUnarmedDef() => Def.Unarmed;
  public override Glyph Glyph
  {
      get
      {
          var g = OwnGlyph ?? Def.Glyph;
          if (Peaceful) g = g with { Background = ConsoleColor.DarkYellow };
          return g;
      }
  }

  public string RealName => ProperName ?? TemplatedName ?? Def.Name;

  public override string ToString() => RealName;

  public override string ToString(string? format, IFormatProvider? provider) => format switch
  {
    "the" => ProperName ?? (Def.IsUnique ? RealName : RealName.The()),
    "The" => ProperName ?? (Def.IsUnique ? RealName : RealName.The().Capitalize()),
    "an" => ProperName ?? (Def.IsUnique ? RealName : RealName.An()),
    "An" => ProperName ?? (Def.IsUnique ? RealName : RealName.An().Capitalize()),
    "own" => "his",
    "Own" => "His",
    _ => RealName,
  };

  public string? TemplatedName;
  const double HpMultiplier = 1.5;

  public static Monster Spawn(MonsterDef def, string reason, MonsterTemplate? template = null, int depthBonus = 0, bool firstTimeSpawn = true)
  {
    IEnumerable<LogicBrick> components = template?.GetComponents(def) ?? def.Components;
    if (!firstTimeSpawn) components = components.Where(c => !c.Tags.HasFlag(AbilityTags.FirstSpawnOnly));
    Log.Muted = true;
    Monster m = new(def, components);
    m.Peaceful = def.Peaceful;
    m.TemplateBonusLevels = (template?.LevelBonus(def, m.EffectiveLevel) ?? 0) + depthBonus;
    m.HP.Reset((int)(Math.Max(1, def.HpPerLevel * m.EffectiveLevel - 1) * HpMultiplier));
    template?.ModifySpawn(m);
    if (m.OwnCreatureType != null || m.OwnSubtypes != null)
      m._creatureTypeKey = (m.OwnSubtypes ?? def.Subtypes) is { Count: > 0 } subs
          ? $"{m.OwnCreatureType ?? def.CreatureType}/{string.Join('.', subs)}"
          : m.OwnCreatureType ?? def.CreatureType;
    using var ctx = PHContext.Create(m, Target.None);
    LogicBrick.FireOnSpawn(m, ctx);
    if (g.DebugMode)
      m.HP.Current = m.HP.Current / 4;
    Log.Muted = false;
    string[] facts = m.LiveFacts.Select(f => f.Brick.Id).ToArray();
    string[] equip = m.Equipped.Values.Select(i => i.ToString()).ToArray();
    Log.Structured("spawn", $"{m.Id:id}{def.Name:name}{m.EffectiveLevel:level}{reason:reason}{facts:facts}{equip:equip}");
    return m;
  }

  internal bool DoTurn()
  {
    if (IsAsleep)
    {
      UpdateApparentPos();
      if (TargetPos == null) { return false; }
      IsAsleep = false;
    }

    if (Def.Brain?.DoTurn(this) == true) return true;

    if (!Peaceful)
      UpdateApparentPos();

    // compute approach state
    if (Peaceful)
      Approach = Approach.Undirected;
    else if (Has("fleeing"))
      Approach = Approach.Flee;
    else if (TargetPos != null)
      Approach = Approach.Approach;
    else
      Approach = Approach.Undirected;

    Log.Verbose("ai", $"[AI] {this} at {Pos}: targetPos={TargetPos} approach={Approach}");

    // grudge scan: when undirected, look for hated monsters
    if (Approach == Approach.Undirected)
    {
      int bestDist = int.MaxValue;
      foreach (var unit in lvl.LiveUnits)
      {
        if (unit is not Monster other) continue;
        bool hates = Hates(this, other);
        int dist = Pos.ChebyshevDist(other.Pos);
        bool canSee = dist <= 12 && CanSee(this, other);
        Log.Verbose("ai", $"[AI] {this}: scan {other} at {other.Pos} hates={hates} dist={dist} canSee={canSee}");
        if (!hates) continue;
        if (dist > 12 || dist >= bestDist) continue;
        if (!canSee) continue;
        bestDist = dist;
        SetTargetPos(other.Pos);
        Approach = Approach.Approach;
        Log.Verbose("ai", $"[AI] {this}: grudge target {other} at {other.Pos} dist={dist}");
      }
    }

    // compute goal for movement (may follow track instead of beelining)
    Pos? goal = TargetPos;
    if (goal != null && !CanSeeYou && Has("can_track"))
    {
      Pos? trail = u.GetTrack(Pos);
      if (trail != null) goal = trail;
    }

    // peaceful monsters don't attack
    if (!Peaceful && Approach != Approach.Flee)
    {
      // build hated target list for ability targeting
      _hatedTargets.Clear();
      foreach (var unit in lvl.LiveUnits)
      {
        if (unit is not Monster other || !Hates(this, other)) continue;
        int dist = Pos.ChebyshevDist(other.Pos);
        if (dist > 12) continue;
        if (!CanSee(this, other)) continue;
        _hatedTargets.Add((other, other.Pos));
      }
      // player is also a valid target, when we don't like him
      if (TargetPos != null && !Peaceful)
        _hatedTargets.Add((u, upos));

      Log.Verbose("ai", $"[AI] {this}: {_hatedTargets.Count} targets, goal={goal}");

      if (HasBrainFlag(MonFlags.PrefersCasting))
      {
        if (TryUseAbility(Spells.Shuffled()) || TryUseAbility(Actions)) return true;
      }
      else
      {
        if (TryUseAbility(Actions) || TryUseAbility(Spells.Shuffled())) return true;
      }
    }

    if (Def.LandMove.Value <= 0 || Def.Stationary) return false;

    Pos mp = Pos;

    Pos? best = null;
    int bestScore = int.MaxValue;

    foreach (var dir in Pos.AllDirs)
    {
      Pos candidate = mp + dir;
      if (!lvl.InBounds(candidate)) continue;
      if (!lvl.CanMoveTo(mp, candidate, this)) continue;
      if (lvl.UnitAt(candidate) != null) continue;

      int dist = goal is { } g2 ? candidate.ChebyshevDist(g2) : 0;
      if (Approach == Approach.Flee) dist = -dist;
      int penalty = WasRecentlyAt(candidate) ? 100 : 0;
      int score = dist + penalty;

      bool wins = score < bestScore 
          || (score == bestScore && g.Rn2(dir.IsDiagonal ? 6 : 3) == 0);
      
      if (wins)
      {
        best = candidate;
        bestScore = score;
      }
    }

    // wander: pick random if no goal
    if (goal is null && best is null)
    {
      var valid = new List<Pos>();
      foreach (var dir in Pos.AllDirs)
      {
        Pos candidate = mp + dir;
        if (lvl.CanMoveTo(mp, candidate, this) && lvl.UnitAt(candidate) == null)
          valid.Add(candidate);
      }
      if (valid.Count > 0)
        best = valid[g.Rn2(valid.Count)];
    }

    if (best is { } target)
    {
      RecordMove(mp);
      lvl.MoveUnit(this, target);
      return true;
    }

    return false;
  }

  static readonly List<(IUnit Unit, Pos Pos)> _hatedTargets = [];

  bool TryUseAbility(IEnumerable<ActionBrick> abilities)
  {
    foreach (var action in abilities)
    {
      bool beneficial = action.Tags.HasFlag(AbilityTags.Beneficial);

      if (action.Tags.HasFlag(AbilityTags.Heal) && HP.Current > HP.Max / 2)
        continue;

      if (!beneficial && action.Tags.HasFlag(AbilityTags.Harmful) && g.Rn2(3) == 0)
        continue;

      if (beneficial)
      {
        Target? target = BeneficialTarget(action);
        if (target == null) continue;
        if (TryExecuteAbility(action, target)) return true;
        continue;
      }

      // try each hated target (includes player)
      foreach (var (tgtUnit, tgtPos) in _hatedTargets)
      {
        // don't waste spells on stale positions
        if (action is SpellBrickBase && !IsApparentPosFresh && tgtUnit.IsPlayer)
          continue;

        Target? target = HarmfulTarget(action, tgtUnit, tgtPos);
        if (target == null) continue;
        if (TryExecuteAbility(action, target)) return true;
      }
    }
    return false;
  }

  bool TryExecuteAbility(ActionBrick action, Target target)
  {
    var data = ActionData.GetValueOrDefault(action);
    var plan = action.CanExecute(this, data, target);
    if (!plan) return false;

    bool isSpell = action is SpellBrickBase;
    Log.Structured("action", $"{this:unit}{action.Name:action}{isSpell:spell}");
    if (isSpell) g.YouObserve(this, $"{this:The} casts {action.Name}!", SpellChants.Pick());

    action.Execute(this, data, target, plan.Plan);
    Energy -= action.GetCost(this, data, target).Value;
    return true;
  }

  // Beneficial: self-cast. Direction = (0,0), None = Target.None
  // TODO: target allies (adventurer parties)
  Target? BeneficialTarget(ActionBrick action) => action.Targeting switch
  {
    TargetingType.None => Target.None,
    TargetingType.Direction => Target.From(Pos.Zero),
    TargetingType.Unit => Target.From(this),
    TargetingType.Pos => Target.From(Pos),
    _ => null,
  };

  // Harmful: target any unit at a position
  Target? HarmfulTarget(ActionBrick action, IUnit targetUnit, Pos targetPos)
  {
    if (action.Targeting == TargetingType.Direction)
    {
      var delta = targetPos - Pos;
      if (delta.X != 0 && delta.Y != 0 && Math.Abs(delta.X) != Math.Abs(delta.Y))
        return null;
    }

    // Respect max range
    if (action.MaxRange > 0 && action.Targeting is not TargetingType.None && Pos.ChebyshevDist(targetPos) > action.MaxRange)
      return null;

    return action.Targeting switch
    {
      TargetingType.Direction => new(null, (targetPos - Pos).Signed),
      TargetingType.Unit when CanSee(this, targetUnit) => new(targetUnit, targetPos),
      TargetingType.Pos => new(null, targetPos),
      TargetingType.None => new(targetUnit, targetPos),
      _ => null,
    };
  }

  public override bool IsAwareOf(Trap trap) => (trap.Type & KnownTraps) != 0;
  public override void ObserveTrap(Trap trap) => KnownTraps |= trap.Type;

  // BLEH
  public override int CasterLevel => Math.Clamp(Def.BaseLevel, 1, 20);
  public override int EffectiveLevel => Math.Clamp(Def.BaseLevel + TemplateBonusLevels, 1, 30);

  public string CreatureTypeRendered {
    get {
      var subtypes = OwnSubtypes ?? Def.Subtypes;
      string sub = subtypes.Count > 0 ? $" [{string.Join(", ", subtypes)}]" : "";
      return $"{OwnCreatureType ?? Def.CreatureType}{sub} {OwnEthicalAxis ?? Def.EthicalAxis}/{OwnMoralAxis ?? Def.MoralAxis}";
    }
  }

  public bool NoCorpse => EffectiveBrainFlags.HasFlag(MonFlags.NoCorpse);

  public TrapType KnownTraps;
  internal bool Peaceful;

  static readonly string[] SpellChants =
  [
    "a mumbled incantation",
    "someone chanting nearby",
    "arcane words being spoken",
    "a low, rhythmic drone",
    "words that make your skin crawl",
    "syllables that hurt to hear",
    "a crackling of magical energy",
    "an unsettling hum in the air",
    "whispered words of power",
    "the air thickening with magic",
    "a faint smell of ozone",
    "something gathering power",
  ];
}