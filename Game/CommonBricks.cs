namespace Pathhack.Game;

// Add damage to attacks with weapons
public class WeaponDamageRider(string name, DamageType type, int faces) : LogicBrick
{
  public override StackMode StackMode => StackMode.Extend;

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
