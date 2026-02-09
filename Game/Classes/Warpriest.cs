using System.IO.Pipes;

namespace Pathhack.Game.Classes;

public static class Fervor
{
    public const string Resource = "fervor";

    public class EnhanceWeaponAction() : ActionBrick("Fervor: Enhance Weapon")
    {
        public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => unit.HasCharge(Resource, out whyNot);

        public override void Execute(IUnit unit, object? data, Target target)
        {
            if (!unit.TryUseCharge(Resource)) return;

            Menu<string> menu = new();
            menu.Add("Enhance weapon:", LineStyle.Heading);

            menu.Add('a', "Flaming", "Flaming");
            menu.Add('b', "Freeze", "Freeze");
            menu.Add('c', "Shock", "Shock");

            if (u.MoralAxis == MoralAxis.Evil)
                menu.Add('d', "Unholy", "Unholy");
            else
                menu.Add('d', "Holy", "Holy");

            var picks = menu.Display(MenuMode.PickOne);
            if (picks.Count == 0) return;

            var weapon = unit.GetWieldedItem();

            switch (picks[0])
            {
                case "Flaming":
                    weapon.AddFact(WeaponDamageRider.FlamingD8, 12);
                    break;
                case "Freeze":
                    weapon.AddFact(WeaponDamageRider.FreezeD8, 12);
                    break;
                case "Shock":
                    weapon.AddFact(WeaponDamageRider.ShockD8, 12);
                    break;
                case "Holy":
                    weapon.AddFact(WeaponDamageRider.HolyD8, 12);
                    break;
                case "Unholy":
                    weapon.AddFact(WeaponDamageRider.UnholyD8, 12);
                    break;
            }
        }
    }

}

public class DivineFortitudeBrick : LogicBrick
{
    public static readonly DivineFortitudeBrick Instance = new();
    protected override void OnBeforeCheck(Fact fact, PHContext ctx)
    {
        if (!ctx.IsCheckingOwnerOf(fact)) return;
        if (ctx.Check == null || !ctx.Check.IsSave) return;
        int bonus = ctx.Target.Unit!.EffectiveLevel < 10 ? 1 : 2;
        ctx.Check!.Modifiers.Mod(ModifierCategory.StatusBonus, bonus, "divine fort");
    }
}

public class WeaponsOfFaithBrick : LogicBrick
{
    public static readonly WeaponsOfFaithBrick Instance = new();

  protected override void OnBeforeDamageRoll(Fact fact, PHContext context)
  {
    // TODO
    // Mark DR bypass on context.Damage
  }
}

public class SacredArmorBrick : LogicBrick
{
    public static readonly SacredArmorBrick Instance = new();
    protected override object? OnQuery(Fact fact, string key, string? arg)
    {
        if (fact.Entity is not Player) return null;
        if (key != "ac") return null;
        var armor = u.Equipped.GetValueOrDefault(ItemSlots.BodySlot);

        if (armor == null) return null; // query prc sacred fist = unarmed?

        if ((armor.Def as ArmorDef)?.Proficiency is not (Proficiencies.LightArmor or Proficiencies.MediumArmor)) return null;
        int bonus = u.EffectiveLevel < 10 ? 1 : 2;
        return new Modifier(ModifierCategory.StatusBonus, bonus, "sacred armor");
    }
}

public class SacredStrikeBrick : LogicBrick
{
    public static readonly SacredStrikeBrick Instance = new();
    protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not Player p) return;
        if (ctx.Source != p) return;
        if (ctx.Weapon?.Def is not WeaponDef w) return;
        if (w.Profiency != p.Deity?.FavoredWeapon) return;
        ctx.Check!.Modifiers.Untyped(2, "sacred strike");
    }
}

public class SacredWeapon : LogicBrick
{
    public static readonly SacredWeapon Instance = new();
    static readonly DiceFormula[] LevelScaling = [
        d(6),   // 1-4
        d(8),   // 5-9
        d(10),  // 10-14
        d(2,6), // 15-19
        d(2,8), // 20+
    ];

    static DiceFormula ScaledDie(int level) => LevelScaling[Math.Min(level / 5, LevelScaling.Length - 1)];

    protected override void OnBeforeDamageRoll(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not Player player) return;
        if (ctx.Source != player) return;
        if (player.Deity == null) return;
        if (ctx.Weapon?.Def is not WeaponDef wdef) return;
        if (ctx.Damage.Count == 0) return;
        if (wdef.Profiency != player.Deity.FavoredWeapon) return;

        var baseDie = wdef.BaseDamage;
        var stepped = baseDie.StepUp();
        var scaled = ScaledDie(player.CharacterLevel);

        // Take best of stepped or scaled
        var best = stepped.Average() >= scaled.Average() ? stepped : scaled;
        if (best.Average() <= baseDie.Average()) return;

        // Replace first damage roll (weapon base)
        ctx.Damage[0].Formula = best;
    }
}

public static class WarpriestFeats
{
    public static readonly FeatDef SwiftBlessing = new()
    {
        id = "swift_blessing",
        Name = "Swift Blessing",
        Description = "Using a blessing is a free action.",
        Type = FeatType.Class,
        Level = 2,
        Components = [new QueryBrick("blessing_free_action", true)]
    };

    public static readonly FeatDef TrulyBlessed = new()
    {
        id = "truly_blessed",
        Name = "Truly Blessed",
        Description = "Blessing cooldowns are reduced by 25%.",
        Type = FeatType.Class,
        Level = 2,
        Components = [new QueryBrick("blessing_cooldown_reduction", true)]
    };

    public static readonly FeatDef SacredStrike = new()
    {
        id = "sacred_strike",
        Name = "Sacred Strike",
        Description = "+2 attack bonus with your deity's favored weapon.",
        Type = FeatType.Class,
        Level = 2,
        Components = [SacredStrikeBrick.Instance]
    };

    public static readonly FeatDef DivineFortitude = new()
    {
        id = "divine_fort",
        Name = "Divine Fortitude",
        Description = "+1 Status bonus to all saves, increasing to +2 at level 10.",
        Type = FeatType.Class,
        Level = 2,
        Components = [DivineFortitudeBrick.Instance],
    };

    public static readonly FeatDef SacredArmor = new()
    {
        id = "sacred_armor",
        Name = "Sacred Armor",
        Description = "+1 Status bonus to AC, increasing to +2 at level 10.",
        Type = FeatType.Class,
        Level = 2,
        Components = [SacredArmorBrick.Instance],
    };

    public static readonly FeatDef WeaponsOfFaith = new()
    {
        id = "weapons_of_faith",
        Name = "Weapons of Faith",
        Description = $"Your weapon attacks are treated as Good/Evil and Lawful/Chaotic for the purposes of bypassing DR.",
        Type = FeatType.Class,
        Level = 4,
        Components = [WeaponsOfFaithBrick.Instance],
    };
}

public static partial class ClassDefs
{
    public static readonly List<SpellBrickBase> WarpriestList = [
        BasicLevel1Spells.CureLightWounds,
        BasicLevel1Spells.BurningHands,
        BasicLevel1Spells.MagicMissile,
        BasicLevel1Spells.Light,
        BasicLevel1Spells.Shield,
        BasicLevel1Spells.Grease,
        BasicLevel1Spells.AcidArrow,
    ];

    public static ClassDef Warpriest => new()
    {
        id = "warpriest",
        Name = "Warpriest",
        Description = "Capable of calling upon the power of the gods in the form of blessings and spells, warpriests blend divine magic with martial skill. They are unflinching bastions of their faith, shouting gospel as they pummel foes into submission, and never shy away from a challenge to their beliefs. While clerics might be subtle and use diplomacy to accomplish their aims, warpriests aren’t above using violence whenever the situation warrants it. In many faiths, warpriests form the core of the church’s martial forces—reclaiming lost relics, rescuing captured clergy, and defending the church’s tenets from all challenges.",
        HpPerLevel = 8,
        KeyAbility = AbilityStat.Wis,
        StartingStats = new()
        {
            Str = 14,
            Dex = 10,
            Con = 12,
            Int = 10,
            Wis = 16,
            Cha = 10,
        },
        Progression = [
            new() // Level 1
            {
                Grants = [
                    new GrantProficiency(Proficiencies.Unarmed, ProficiencyLevel.Trained),
                    new GrantProficiency(Proficiencies.LightArmor, ProficiencyLevel.Trained),
                    new GrantProficiency(Proficiencies.MediumArmor, ProficiencyLevel.Trained),
                    new GrantProficiency("spell_attack", ProficiencyLevel.Trained),
                    new SacredWeapon(),
                    new GrantPool("spell_l1", 2, 20),
                ],
                Selections = [
                    new() { Label = "Choose a blessing", Options = Blessings.All.Select(b => b.ToFeat()) },
                    new() { Label = "Choose a spell", Options = WarpriestList.Select(b => b.ToFeat()) },
                ],
            },
            null, // 2
            new()// 3
            {
                Selections = [
                    new() { Label = "Choose a spell", Options = WarpriestList.Select(b => b.ToFeat()) },
                ],
            }, 
            null, // 4
            new() // 5
            {
                Grants = [
                    new GrantPool("spell_l2", 1, 40),
                    new GrantPool(Fervor.Resource, 1, 60),
                    new GrantAction(new Fervor.EnhanceWeaponAction()),
                ],
                Selections = [
                    new() { Label = "Choose a spell", Options = WarpriestList.Select(b => b.ToFeat()) },
                ],
            },
            null, // 6
            new() { Grants = [new GrantPool("spell_l1", 1, 20)] }, // 7
            null, // 8
            new() { Grants = [new GrantPool("spell_l3", 1, 60)] }, // 9
            new() { Grants = [new GrantPool("spell_l2", 1, 40)] }, // 10
            null, // 11
            new() { Grants = [new GrantPool("spell_l3", 1, 60)] }, // 12
            new() { Grants = [new GrantPool("spell_l4", 1, 100)] }, // 13
            new() { Grants = [new GrantPool("spell_l1", 1, 20)] }, // 14
            new() { Grants = [new GrantPool("spell_l2", 1, 40)] }, // 15
            new() { Grants = [new GrantPool("spell_l3", 1, 60)] }, // 16
            new() { Grants = [new GrantPool("spell_l5", 1, 150), new GrantPool("spell_l4", 1, 100)] }, // 17
            new() { Grants = [new GrantPool("spell_l4", 1, 100)] }, // 18
            null, // 19
            null, // 20
        ],
        ClassFeats = [WarpriestFeats.SwiftBlessing, WarpriestFeats.TrulyBlessed, WarpriestFeats.SacredStrike, WarpriestFeats.DivineFortitude, WarpriestFeats.SacredArmor],
        GrantStartingEquipment = p =>
        {
            // Deity's favored weapon
            var weaponDef = GetWeaponForType(p.Deity!.FavoredWeapon);
            if (weaponDef != null)
            {
                var weapon = ItemGen.GenerateItem(weaponDef, depth: 1, maxPotency: 1, propertyRunes: false);
                p.Inventory.Add(weapon);
                p.Equip(weapon);
                p.AddFact(new GrantProficiency(p.Deity.FavoredWeapon, ProficiencyLevel.Trained));
            }
            var armor = Item.Create(MundaneArmory.LeatherArmor);
            p.Inventory.Add(armor);
            p.Equip(armor);
            
            // Starting potions
            p.Inventory.Add(Item.Create(Potions.Healing, 2));
            p.Inventory.Add(Item.Create(Potions.Speed));
            p.Inventory.Add(Item.Create(Potions.Paralysis, 2));
            
            // Starting scrolls
            p.Inventory.Add(Item.Create(Scrolls.MagicMapping));
            p.Inventory.Add(Item.Create(Scrolls.Identify, 2));

            p.Inventory.Add(Item.Create(Foods.Ration, 2));
            p.Inventory.Add(Item.Create(Foods.Apple, 2));
        },
    };

    static WeaponDef? GetWeaponForType(string type) => type switch
    {
        WeaponTypes.Longsword => MundaneArmory.Longsword,
        WeaponTypes.Scimitar => MundaneArmory.Scimitar,
        WeaponTypes.Rapier => MundaneArmory.Rapier,
        WeaponTypes.Whip => MundaneArmory.Whip,
        WeaponTypes.SpikedChain => MundaneArmory.SpikedChain,
        WeaponTypes.Scythe => MundaneArmory.Scythe,
        WeaponTypes.Falchion => MundaneArmory.Falchion,
        WeaponTypes.Dagger => MundaneArmory.Dagger,
        WeaponTypes.Unarmed => null,
        _ => null,
    };

}

