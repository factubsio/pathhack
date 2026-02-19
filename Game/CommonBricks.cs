namespace Pathhack.Game;

// Add damage to attacks with weapons
public class WeaponDamageRider(string name, DamageType type, Dice dice) : LogicBrick
{
  public override string Id => $"rider+{type.SubCat}/{dice.Serialize()}";
  public override StackMode StackMode => StackMode.ExtendDuration;

  public static readonly WeaponDamageRider UnholyD4 = new("Unholy Weapon", DamageTypes.Unholy, d(4));
  public static readonly WeaponDamageRider UnholyD8 = new("Unholy Weapon", DamageTypes.Unholy, d(8));
  public static readonly WeaponDamageRider HolyD4 = new("Holy Weapon", DamageTypes.Holy,       d(4));
  public static readonly WeaponDamageRider HolyD8 = new("Holy Weapon", DamageTypes.Holy,       d(8));
  public static readonly WeaponDamageRider FreezeD4 = new("Freezing Weapon", DamageTypes.Cold, d(4));
  public static readonly WeaponDamageRider FreezeD8 = new("Freezing Weapon", DamageTypes.Cold, d(8));
  public static readonly WeaponDamageRider ShockD4 = new("Shocking Weapon", DamageTypes.Shock, d(4));
  public static readonly WeaponDamageRider ShockD8 = new("Shocking Weapon", DamageTypes.Shock, d(8));
  public static readonly WeaponDamageRider FlamingD4 = new("Flaming Weapon", DamageTypes.Fire, d(4));
  public static readonly WeaponDamageRider FlamingD8 = new("Flaming Weapon", DamageTypes.Fire, d(8));

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

  protected override object? OnQuery(Fact fact, string key, string? arg) => key.TrueWhen(Key);

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
      Formula = dice,
      Type = type,
    });
  }
}

public static class RampHelper
{
  public static T Ramp<T>(this int level, T[] values) => level switch
  {
      _ when level <= 5 => values[0],
      _ when level <= 10 => values[1],
      _ when level <= 15 => values[2],
      _ => values[3],

  };
}

public class EnergyResist(DamageType type, int amount) : LogicBrick
{
  static readonly int[] RampValues = [5, 10, 15, 20];

  public override string Id => amount == 0 ? $"energy_res+{type.SubCat}" : $"energy_res+{type.SubCat}/{amount}";
  public override string? PokedexDescription => amount == 0 ? $"Resist {type.SubCat} (scaling)" : $"Resist {type.SubCat} {amount}";

  int Resolve(Fact fact) => amount > 0 ? amount : (fact.Entity as IUnit)?.EffectiveLevel.Ramp(RampValues) ?? 5;

  [BrickInstances]
  public class Ramp(DamageType type)
  {
    public readonly EnergyResist Dynamic = new(type, 0);
    public readonly EnergyResist DR5 =  new(type, 5);
    public readonly EnergyResist DR10 = new(type, 10);
    public readonly EnergyResist DR15 = new(type, 15);
    public readonly EnergyResist DR20 = new(type, 20);
    public readonly EnergyResist Immune = new(type, 999);

    public EnergyResist Lookup(int level) => level.Ramp([DR5, DR10, DR15, DR20]);
  }

  public static readonly Ramp Fire = new(DamageTypes.Fire);
  public static readonly Ramp Shock = new(DamageTypes.Shock);
  public static readonly Ramp Cold = new(DamageTypes.Cold);
  public static readonly Ramp Acid = new(DamageTypes.Acid);
  public static readonly Ramp Sonic = new(DamageTypes.Sonic);

  protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
  {
    int dr = Resolve(fact);
    foreach (var roll in ctx.Damage)
      if (roll.Type == type) roll.ApplyDR(dr);
  }

  public static EnergyResist For(DamageType type, int level) => 
      RampFor(type).Lookup(level);

  public static EnergyResist Dynamic(DamageType type) =>
      RampFor(type).Dynamic;

  public static Ramp RampFor(DamageType type) => type.SubCat switch
  {
    DamageTypes.E_Fire => Fire,
    DamageTypes.E_Cold => Cold,
    DamageTypes.E_Shock => Shock,
    DamageTypes.E_Acid => Acid,
    DamageTypes.E_Sonic => Sonic,
    _ => throw new NotSupportedException(),
  };
}


public class SimpleDR(int amount, string bypass) : LogicBrick
{
  public override string Id => $"dr+{bypass}/{amount}";
  [BrickInstances]
  public class SimpleDRRamp(string bypass)
  {
    public readonly SimpleDR DR5 = new(5, bypass);
    public readonly SimpleDR DR10 = new(10, bypass);
    public readonly SimpleDR DR15 = new(15, bypass);
    public readonly SimpleDR DR20 = new(20, bypass);

    public SimpleDR Lookup(int level) => level.Ramp([DR5, DR10, DR15, DR20]);
  }

  protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
  {
    ctx.Damage.ApplyDRUnless(amount, bypass, true);
  }

  public override string? PokedexDescription => $"DR {amount}/{bypass}";

  public static readonly SimpleDRRamp Slashing = new(DamageTypes.P_Slashing);
  public static readonly SimpleDRRamp Blunt = new(DamageTypes.P_Blunt);
  public static readonly SimpleDRRamp Piercing = new(DamageTypes.P_Piercing);

  public static readonly SimpleDRRamp Silver = new(Materials.Silver);
  public static readonly SimpleDRRamp Adamantine = new(Materials.Adamantine);
  public static readonly SimpleDRRamp ColdIron = new(Materials.ColdIron);

  public static readonly SimpleDRRamp Good = new(DamageTypes.A_Good);
  public static readonly SimpleDRRamp Evil = new(DamageTypes.A_Evil);
  public static readonly SimpleDRRamp Chaotic = new(DamageTypes.A_Chaos);
  public static readonly SimpleDRRamp Lawful = new(DamageTypes.A_Law);
}

public static class DRHelper
{
  public static void ApplyDRUnless(this List<DamageRoll> rolls, int amount, string bypass, bool physicalOnly)
  {
    foreach (var roll in rolls)
      if ((!physicalOnly || roll.Type.Category == "phys") && !roll.Has(bypass)) roll.ApplyDR(amount);
  }
}

public class ProtectionBrick(DamageType type) : LogicBrick
{
    public override string Id => $"protection+{type.SubCat}";
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

public class ApplyFactOnAttackHit(LogicBrick toApply, int? duration = null) : LogicBrick
{
    public override string Id => $"on_attack_hit+{toApply.Id}";
    protected override void OnAfterAttackRoll(Fact fact, PHContext context)
    {
        if (context.Check!.Result)
            context.Target?.Unit?.AddFact(toApply, duration);
    }
}

/// <summary>Extra typed damage on all melee hits (not weapon-specific).</summary>
public class MeleeDamageRider(string name, DamageType type, Dice dice) : LogicBrick
{
    public static readonly MeleeDamageRider Shock_1d4 = new("shock", DamageTypes.Shock, d(4));
    public static readonly MeleeDamageRider Acid_2d6 = new("acid_2d6", DamageTypes.Acid, d(2, 6));

    public override string Id => $"melee_rider+{type.SubCat}/{dice.Serialize()}";
    public override string? PokedexDescription => $"{name} ({dice} {type.SubCat} on hit)";

    protected override void OnBeforeDamageRoll(Fact fact, PHContext ctx)
    {
        if (ctx.AttackType != AttackType.Melee) return;
        ctx.Damage.Add(new DamageRoll { Formula = dice, Type = type });
    }
}

/// <summary>Flat DR, no bypass (all physical damage reduced).</summary>
public class FlatDR(int amount) : LogicBrick
{
    public static readonly FlatDR DR2 = new(2);
    public static readonly FlatDR DR5 = new(5);
    public static readonly FlatDR DR10 = new(10);

    public override string Id => $"flat_dr/{amount}";
    public override string? PokedexDescription => $"DR {amount}";

    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext ctx)
    {
        foreach (var roll in ctx.Damage)
            if (roll.Type.Category == "phys") roll.ApplyDR(amount);
    }
}

public class QueryBrick(string queryKey, object value) : LogicBrick
{
    public override string Id => $"query+{queryKey}";
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == queryKey ? value : null;
}

public class QueryBrickWhenEquipped(string queryKey, object value) : LogicBrick
{
    public override string Id => $"equery+{queryKey}";
    public override bool RequiresEquipped => true;
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == queryKey ? value : null;
}

public class IdentifyOnEquip(string message) : LogicBrick
{
    public override string Id => $"id_equip+{message}";
    protected override void OnEquip(Fact fact, PHContext ctx)
    {
        if (fact.Entity is Item item && item.Holder != null)
        {
            if (g.YouObserve(item.Holder, message))
                item.Def.SetKnown();
        }
    }
}

public class GrantProficiency(string skill, ProficiencyLevel level, bool requiresEquipped = false) : LogicBrick
{
    public override string Id => $"proficiency+{skill}";
    public override bool RequiresEquipped => requiresEquipped;
    protected override object? OnQuery(Fact fact, string key, string? arg) =>
        key == "proficiency" && arg == skill ? (int)level : null;
}

public class ArmorBrick(int acBonus, int dexCap) : LogicBrick
{
    public override string Id => $"armor+{acBonus}/{dexCap}";
    protected override object? OnQuery(Fact fact, string key, string? arg)
    {
        if (!fact.IsEquipped()) return null;
        var potency = (fact.Entity as Item)?.Potency ?? 0;
        return key switch
        {
            "ac" => new Modifier(ModifierCategory.ItemBonus, acBonus + potency),
            "dex_cap" => dexCap,
            _ => null,
        };
    }
}

public static class LogicHelpers
{
    public static LogicBrick ModifierBrick(string key, ModifierCategory cat, int value, string why) =>
        new QueryBrick(key, new Modifier(cat, value, why));
}

