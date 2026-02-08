namespace Pathhack.Game;

// Add damage to attacks with weapons
public class WeaponDamageRider(string name, DamageType type, int faces) : LogicBrick
{
  public override StackMode StackMode => StackMode.ExtendDuration;

  public static readonly WeaponDamageRider UnholyD4 = new("Unholy Weapon", DamageTypes.Unholy, 4);
  public static readonly WeaponDamageRider UnholyD8 = new("Unholy Weapon", DamageTypes.Unholy, 8);
  public static readonly WeaponDamageRider HolyD4 = new("Holy Weapon", DamageTypes.Holy, 4);
  public static readonly WeaponDamageRider HolyD8 = new("Holy Weapon", DamageTypes.Holy, 8);
  public static readonly WeaponDamageRider FreezeD4 = new("Freezing Weapon", DamageTypes.Cold, 4);
  public static readonly WeaponDamageRider FreezeD8 = new("Freezing Weapon", DamageTypes.Cold, 8);
  public static readonly WeaponDamageRider ShockD4 = new("Shocking Weapon", DamageTypes.Shock, 4);
  public static readonly WeaponDamageRider ShockD8 = new("Shocking Weapon", DamageTypes.Shock, 8);
  public static readonly WeaponDamageRider FlamingD4 = new("Flaming Weapon", DamageTypes.Fire, 4);
  public static readonly WeaponDamageRider FlamingD8 = new("Flaming Weapon", DamageTypes.Fire, 8);

  private string OnStr(IUnit unit, string weapon) => type.SubCat switch
  {
    "fire" => $"Flames surround {unit:own} {weapon}.",
    "cold" => $"Icicles swirl round {unit:own} {weapon}.",
    "shock" => $"{unit:Own} {weapon} start to crackle.",
    "holy" => $"{unit:Own} {weapon} glow gold.",
    "unholy" => $"{unit:Own} {weapon} glow black.",
    _ => "??",
  };

  private string OffStr(IUnit unit, string weapon) => type.SubCat switch
  {
    "fire" => $"The flames surrounding {unit:own} {weapon} die out.",
    "cold" => $"Icicles around {unit:own} {weapon} start melting.",
    "shock" => $"{unit:Own} {weapon} stops crackling.",
    "holy" => $"{unit:Own} {weapon} stops glowing gold.",
    "unholy" => $"{unit:Own} {weapon} stops glowing black.",
    _ => "??",
  };

  private string Key => type.SubCat switch
  {
    "fire" => "flaming",
    "cold" => "freeze",
    "shock" => "shock",
    "holy" => "holy",
    "unholy" => "unholy",
    _ => "___",
  };

  public override bool IsBuff => true;
  public override string? BuffName => name;
  public override bool IsActive => true;

  protected override object? OnQuery(Fact fact, string key, string? arg) => key == Key ? true : null;

  protected override void OnFactAdded(Fact fact)
  {
    if (fact.Entity is Item item && item.Holder?.IsPlayer == true)
    {
      bool isUnarmed = item.Def is WeaponDef w && w.Profiency == Proficiencies.Unarmed;
      string weaponName = isUnarmed ? "fists" : item.Def.Name;
      if (item.Has(Key))
        g.pline($"{item.Holder:Own} {weaponName} seems more energised.");
      else
        g.pline(OnStr(u, weaponName));
    }
  }

  protected override void OnFactRemoved(Fact fact)
  {
    if (fact.Entity is Item item && item.Holder is { IsPlayer: true })
    {
      bool isUnarmed = item.Def is WeaponDef w && w.Profiency == Proficiencies.Unarmed;
      string weaponName = isUnarmed ? "fists" : item.Def.Name;
      if (item.Has(Key))
        g.pline($"{item.Holder:Own} {weaponName} seems slightly less energised.");
      else
        g.pline(OffStr(u, weaponName));
    }
  }

  protected override void OnBeforeDamageRoll(Fact fact, PHContext context)
  {
    if (context.Weapon != fact.Entity) return;

    context.Damage.Add(new DamageRoll
    {
      Formula = d(faces),
      Type = type,
    });
  }
}

public class SimpleDR(int amount, string bypass) : LogicBrick
{
  public class SimpleDRRamp(string bypass)
  {
    public readonly SimpleDR DR5 = new(5, bypass);
    public readonly SimpleDR DR10 = new(10, bypass);
    public readonly SimpleDR DR15 = new(15, bypass);
    public readonly SimpleDR DR20 = new(20, bypass);

    public SimpleDR Lookup(int level) => level switch {
      _ when level <= 5 => DR5,
      _ when level <= 10 => DR10,
      _ when level <= 15 => DR15,
      _  => DR20,
    };
  }

  public static readonly SimpleDRRamp Slashing = new("slashing");
  public static readonly SimpleDRRamp Blunt = new("blunt");
  public static readonly SimpleDRRamp Piercing = new("piercing");

  public static readonly SimpleDRRamp Silver = new("silver");
  public static readonly SimpleDRRamp Adamantine = new("adamantine");
  public static readonly SimpleDRRamp ColdIron = new("cold_iron");

  public static readonly SimpleDRRamp Good = new("good");
  public static readonly SimpleDRRamp Evil = new("evil");
  public static readonly SimpleDRRamp Chaotic = new("chaotic");
  public static readonly SimpleDRRamp Neutral = new("neutral");

  protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
  {
    ctx.Damage.ApplyDRUnless(amount, bypass);
  }

  public override string? PokedexDescription => $"DR {amount}/{bypass}";
}

public class ComplexDR(int amount, string[]? or = null, string[]? and = null) : LogicBrick
{
  public int Amount { get; init; } = amount;
  public string[]? Or { get; init; } = or;
  public string[]? And { get; init; } = and;

  bool Bypassed(HashSet<string> tags)
  {
    if (Or != null && Or.Any(tags.Contains)) return true;
    if (And != null && And.All(tags.Contains)) return true;
    return false;
  }

  protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
  {
    foreach (var roll in ctx.Damage)
    {
      if (Bypassed(roll.Tags)) continue;
      roll.ApplyDR(Amount);
    }
  }

}
public static class DRHelper
{
  public static void ApplyDRUnless(this List<DamageRoll> rolls, int amount, string bypass)
  {
    foreach (var roll in rolls)
      if (!roll.Has(bypass)) roll.ApplyDR(amount);
  }
}

public class ProtectionBrick(DamageType type) : LogicBrick
{
    public static readonly ProtectionBrick Fire = new(DamageTypes.Fire);
    public static readonly ProtectionBrick Cold = new(DamageTypes.Cold);
    public static readonly ProtectionBrick Shock = new(DamageTypes.Shock);
    public static readonly ProtectionBrick Acid = new(DamageTypes.Acid);
    public static readonly ProtectionBrick Phys = new(new DamageType("phys", "_"));

    bool Matches(DamageRoll roll) => type.SubCat == "_" 
        ? roll.Type.Category == type.Category 
        : roll.Type == type;

    string Label => type.SubCat == "_" ? type.Category : type.SubCat;

    public override bool IsBuff => true;
    public override string? BuffName => $"Prot {Label}";
    public override StackMode StackMode => StackMode.ExtendStacks;
    public override int MaxStacks => 9999;
    public override FactDisplayMode DisplayMode => FactDisplayMode.Name | FactDisplayMode.Stacks;

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
        foreach (var roll in ctx.Damage)
            if (Matches(roll))
                roll.Protection = fact.Stacks;
    }

    protected override void OnDamageTaken(Fact fact, PHContext ctx)
    {
        int used = 0;
        foreach (var roll in ctx.Damage)
            if (Matches(roll))
                used += roll.ProtectionUsed;

        if (used <= 0) return;

        if (fact.Entity is IUnit { IsPlayer: true })
            g.pline($"Your protection absorbs {used} {Label} damage.");

        ((IUnit)fact.Entity).RemoveStack(this, used);
    }
}