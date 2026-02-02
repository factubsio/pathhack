namespace Pathhack.Game;

public abstract class MonsterBrain
{
  public abstract bool DoTurn(Monster m);
}

public class MonsterDef : BaseDef
{
  public MonsterBrain? Brain;
  public required string Name;
  public required Glyph Glyph;
  public required int HP;
  public required int AC;
  public required int AttackBonus;
  public int DamageBonus = 0;
  public int StrMod = 0;
  public string[] CanUse = [];
  public ActionCost LandMove = 12;
  public required WeaponDef Unarmed;
  public UnitSize Size = UnitSize.Small;
  public int CR = 0;
  public int SpawnWeight = 1;
  public int MinDepth = 1;
  public bool IsUnique = false;
  public int MaxDepth = 99;
  public string? Family;
}

public enum MonsterTemplate { Normal, Elite, Weak }

public class Monster : Unit<MonsterDef>, IFormattable
{
  private Monster(MonsterDef def) : base(def) { }

  public static readonly Monster DM = new(new()
  {
    Name = "DM",
    Glyph = Glyph.Null,
    HP = 1,
    AC = 1,
    AttackBonus = 0,
    Unarmed = NaturalWeapons.Fist,
  });

  public override bool IsDM => this == DM;

  public MonsterTemplate Template { get; private set; } = MonsterTemplate.Normal;
  int TemplateBonus => Template switch { MonsterTemplate.Elite => 2, MonsterTemplate.Weak => -2, _ => 0 };

  public override int NaturalRegen => 5;
  public override int StrMod => Def.StrMod;

  // where monster thinks player is (perfect vision for now)
  public Pos? ApparentPlayerPos { get; private set; }

  public bool IsAsleep;

  // anti-oscillation: last N positions
  const int TrackSize = 5;
  readonly Pos[] _track = new Pos[TrackSize];
  int _trackIdx;

  void RecordMove(Pos p)
  {
    _track[_trackIdx] = p;
    _trackIdx = (_trackIdx + 1) % TrackSize;
  }

  bool WasRecentlyAt(Pos p)
  {
    foreach (var t in _track)
      if (t == p) return true;
    return false;
  }

  public bool CanSeeYou
  {
    get
    {
      // use player's LOS (symmetric) - if player could see us, we could see them
      // also need player to be lit (or monster has special vision)
      bool inLOS = lvl.HasLOS(Pos);
      bool playerLit = lvl.IsLit(upos);

      // TODO: infravision, see invisible, etc.
      return inLOS && playerLit;
    }
  }

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

    // can't see - chance to forget if at stale pos or random
    if (ApparentPlayerPos is { } last)
    {
      // if we're adjacent to where we thought player was but they're not there, or 1/100
      if ((Pos.ChebyshevDist(last) <= 1 && last != upos) || g.Rn2(100) == 0)
        ApparentPlayerPos = null;
    }
  }


  public override bool IsPlayer => false;

  public override int GetAC() => Def.AC + TemplateBonus;
  public override ActionCost LandMove => Def.LandMove;
  public override int GetAttackBonus(WeaponDef weapon) => Def.AttackBonus + TemplateBonus;
  public override int GetDamageBonus() => Def.DamageBonus + TemplateBonus;
  public override int GetSpellDC() => 10 + Def.CR;
  protected override WeaponDef GetUnarmedDef() => Def.Unarmed;
  public override Glyph Glyph => Def.Glyph;

  public override string ToString() => Def.Name;
  public string ToString(string? format, IFormatProvider? provider) => format switch
  {
    "the" => Def.IsUnique ? Def.Name : Def.Name.The(),
    "The" => Def.IsUnique ? Def.Name : Def.Name.The().Capitalize(),
    "an" => Def.IsUnique ? Def.Name : Def.Name.An(),
    "An" => Def.IsUnique ? Def.Name : Def.Name.An().Capitalize(),
    "own" => "his",
    "Own" => "His",
    _ => Def.Name
  };

  public static Monster Spawn(MonsterDef def, MonsterTemplate template = MonsterTemplate.Normal)
  {
    Monster m = new(def) { Template = template };
    int hpMod = template switch { MonsterTemplate.Elite => def.HP / 10 + 1, MonsterTemplate.Weak => -(def.HP / 10 + 1), _ => 0 };
    m.HP.Reset(Math.Max(1, def.HP + hpMod));
    using var ctx = PHContext.Create(m, Target.None);
    LogicBrick.FireOnSpawn(m, ctx);
    return m;
  }

  internal void DoTurn()
  {
    if (IsAsleep)
    {
      UpdateApparentPos();
      if (ApparentPlayerPos == null) { Energy = 0; return; }
      IsAsleep = false;
    }

    if (Def.Brain?.DoTurn(this) == true) return;

    // try any action that can execute
    Target playerTarget = new(u, upos);
    foreach (var action in Actions)
    {
      var data = ActionData.GetValueOrDefault(action);
      if (action.CanExecute(this, data, playerTarget, out var _))
      {
        Log.Write($"{this} uses {action.Name}");
        action.Execute(this, data, playerTarget);
        Energy -= ActionCosts.OneAction.Value;
        return;
      }
    }

    if (Def.LandMove.Value <= 0) return;

    UpdateApparentPos();
    Pos mp = Pos;

    // move toward goal, or wander if none
    Pos? best = null;
    int bestScore = int.MaxValue;

    foreach (var dir in Pos.AllDirs)
    {
      Pos candidate = mp + dir;
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
    }
  }

  public override bool IsAwareOf(Trap trap) => (trap.Type & KnownTraps) != 0;
  public override void ObserveTrap(Trap trap) => KnownTraps |= trap.Type;

  public override int CasterLevel => Math.Clamp(Def.CR, 1, 20);

  public TrapType KnownTraps;

}
