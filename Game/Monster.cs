using System.Net.NetworkInformation;

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
  public int SpawnWeight = 1;
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
  public bool NoCorpse = false;
  public GroupSize GroupSize = GroupSize.None;
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

  public static readonly Monster DM = new(new()
  {
    Name = "DM",
    Family = "dm",
    CreatureType = "bubble",
    Glyph = Glyph.Null,
    HpPerLevel = 1,
    AC = 1,
    AttackBonus = 0,
    Unarmed = NaturalWeapons.Fist,
    MoralAxis = MoralAxis.Neutral,
    EthicalAxis = EthicalAxis.Neutral,
  }, []);

  public override bool IsDM => this == DM;

  public override int NaturalRegen => 5;
  public override int StrMod => Def.StrMod;

  // These are copied from def by default but we modify them if we need (typically through the template)
  public string? OwnCreatureType;
  public HashSet<string>? OwnSubtypes;
  public MoralAxis? OwnMoralAxis;
  public EthicalAxis? OwnEthicalAxis;
  public Glyph? OwnGlyph;

  public override MoralAxis MoralAxis => Query("moral_axis", null, MergeStrategy.Replace, OwnMoralAxis ?? Def.MoralAxis);
  public override EthicalAxis EthicalAxis => Query("ethical_axis", null, MergeStrategy.Replace, OwnEthicalAxis ?? Def.EthicalAxis);
  public override bool IsCreature(string? type = null, string? subtype = null) =>
    (type == null || (OwnCreatureType ?? Def.CreatureType) == type) && (subtype == null || (OwnSubtypes ?? Def.Subtypes).Contains(subtype));

  string? _creatureTypeKey;
  public string CreatureTypeKey => _creatureTypeKey ?? Def.CreatureTypeKey;

  // where monster thinks player is
  public Pos? ApparentPlayerPos { get; private set; }

  public bool IsAsleep;

  // anti-oscillation: last N positions
  readonly Pos[] _track = [Pos.Invalid, Pos.Invalid, Pos.Invalid, Pos.Invalid, Pos.Invalid];
  int _trackIdx;

  void RecordMove(Pos p)
  {
    _track[_trackIdx] = p;
    _trackIdx = (_trackIdx + 1) % _track.Length;
  }

  bool WasRecentlyAt(Pos p) => _track.Any(x => x == p);

  public bool CanSeeYou => CanSee(this, u);

  void UpdateApparentPos()
  {
    // always knows (pet, grabber, etc)
    if (Has("always_knows_u") && !IsAsleep)
    {
      ApparentPlayerPos = upos;
      return;
    }

    if (CanSeeYou && !IsAsleep)
    {
      ApparentPlayerPos = upos;
      return;
    }

    // hearing: within 10 tiles, 1/7 chance (or always if keen ears)
    // blocked by stealth
    if (!u.Has("stealth") && Pos.ChebyshevDist(upos) <= 10)
    {
      if (Has("keen_ears") || g.Rn2(7) == 0)
      {
        ApparentPlayerPos = upos;
        return;
      }
    }

    // aggravate monster - player always detected
    if (u.Has("aggravate_monster"))
    {
      ApparentPlayerPos = upos;
      return;
    }

    // adjacent stumble - 1/8 chance to notice adjacent player
    if (Pos.ChebyshevDist(upos) <= 1 && g.Rn2(8) == 0)
    {
      ApparentPlayerPos = upos;
      return;
    }

    // can't see - chance to forget if at stale pos or random
    if (ApparentPlayerPos is { } last)
    {
      // if we're adjacent to where we thought player was but they're not there, or 1/100
      if ((Pos.ChebyshevDist(last) <= 1 && last != upos) || g.Rn2(100) == 0)
        ApparentPlayerPos = null;
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

  private int LevelDC => 12 + (Def.BaseLevel + TemplateBonusLevels + 1) / 2;

  // see tools/dc.md, this gives us what seems to be a reasonable curve
  private const int ATTACK_PENALTY_FUDGE = 5;

  public override ActionCost LandMove => Def.LandMove.Value - QueryModifiers("speed_bonus").Calculate();
  public override int GetAttackBonus(WeaponDef weapon) => LevelDC - ATTACK_PENALTY_FUDGE + Def.AttackBonus;
  public override int GetSpellAttackBonus(SpellBrickBase brick) => LevelDC - ATTACK_PENALTY_FUDGE + Def.AttackBonus;
  const int DamageFudge = 2;
  public override int GetDamageBonus() => Def.DamageBonus + TemplateBonusLevels + DamageFudge;
  public override int GetSpellDC() => LevelDC;
  protected override WeaponDef GetUnarmedDef() => Def.Unarmed;
  public override Glyph Glyph => OwnGlyph ?? Def.Glyph;

  public string RealName => TemplatedName ?? Def.Name;

  public override string ToString() => RealName;

  public string ToString(string? format, IFormatProvider? provider) => format switch
  {
    "the" => Def.IsUnique ? RealName : RealName.The(),
    "The" => Def.IsUnique ? RealName : RealName.The().Capitalize(),
    "an" => Def.IsUnique ? RealName : RealName.An(),
    "An" => Def.IsUnique ? RealName : RealName.An().Capitalize(),
    "own" => "his",
    "Own" => "His",
    _ => RealName,
  };

  public string? TemplatedName;
  const double HpMultiplier = 1.5;

  public static Monster Spawn(MonsterDef def, string reason, MonsterTemplate? template = null, int depthBonus = 0)
  {
    Monster m = new(def, template?.GetComponents(def) ?? def.Components);
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
    Log.Write($"entity: {m.Id}: spawn {def.Name} L{m.EffectiveLevel} ({reason})");
    return m;
  }

  internal bool DoTurn()
  {
    if (IsAsleep)
    {
      UpdateApparentPos();
      if (ApparentPlayerPos == null) { return false; }
      IsAsleep = false;
    }

    if (Def.Brain?.DoTurn(this) == true) return true;

    // peaceful monsters don't attack
    if (!Peaceful)
    {
      // try any action that can execute
      Target playerTarget = new(u, upos);
      foreach (var action in Actions)
      {
        var data = ActionData.GetValueOrDefault(action);
        if (action.CanExecute(this, data, playerTarget, out var _))
        {
          Log.Write($"{this} uses {action.Name}");
          action.Execute(this, data, playerTarget);
          Energy -= action.GetCost(this, data, playerTarget).Value;
          return true;
        }
      }
    }

    if (Def.LandMove.Value <= 0 || Def.Stationary) return false;

    if (!Peaceful)
      UpdateApparentPos();
    Pos mp = Pos;

    // move toward goal, or wander if none
    Pos? best = null;
    int bestScore = int.MaxValue;

    foreach (var dir in Pos.AllDirs)
    {
      Pos candidate = mp + dir;
      if (!lvl.InBounds(candidate)) continue;
      if (!lvl.CanMoveTo(mp, candidate, this)) continue;
      if (lvl.UnitAt(candidate) != null) continue;

      int dist = ApparentPlayerPos is { } goal ? candidate.ChebyshevDist(goal) : 0;
      int penalty = WasRecentlyAt(candidate) ? 100 : 0;
      int score = dist + penalty;

      // weighted tiebreaker: 2/3 chance to prefer cardinal over diagonal
      bool wins = score < bestScore 
          || (score == bestScore && g.Rn2(dir.IsDiagonal ? 6 : 3) == 0);
      
      if (wins)
      {
        best = candidate;
        bestScore = score;
      }
    }

    // wander: pick random if no goal
    if (ApparentPlayerPos is null && best is null)
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

  public TrapType KnownTraps;
  internal bool Peaceful;
}
