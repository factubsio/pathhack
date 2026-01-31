using System.Reflection.Metadata;

namespace Pathhack.Game.Classes;

public class SacredStrikeBrick : LogicBrick
{
    public override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
        if (fact.Entity is not Player p) return;
        if (ctx.Source != p) return;
        if (ctx.Weapon?.Def is not WeaponDef w) return;
        if (w.Profiency != p.Deity?.FavoredWeapon) return;
        ctx.Check!.Modifiers.AddModifier(new(ModifierCategory.UntypedStackable, 2, "sacred strike"));
    }
}

public class SacredWeapon : LogicBrick
{
    static readonly DiceFormula[] LevelScaling = [
        d(6),   // 1-4
        d(8),   // 5-9
        d(10),  // 10-14
        d(2,6), // 15-19
        d(2,8), // 20+
    ];

    static DiceFormula ScaledDie(int level) => LevelScaling[Math.Min(level / 5, LevelScaling.Length - 1)];

    public override void OnBeforeDamageRoll(Fact fact, PHContext ctx)
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
        Components = [new SacredStrikeBrick()]
    };
}

public static partial class ClassDefs
{
    public static readonly List<SpellBrickBase> WarpriestList = [
        CommonSpells.CureLightWounds,
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
                    new SacredWeapon(),
                    new GrantPool("spell_l1", 5, 20),
                    new GrantPool("spell_l2", 4, 40),
                    new GrantPool("spell_l3", 3, 60),
                    new GrantPool("spell_l4", 3, 100),
                    new GrantPool("spell_l5", 2, 150),
                    new GrantAction(new ConsumeSpell(1)),
                    new GrantAction(new ConsumeSpell(2)),
                    new GrantAction(new ConsumeSpell(3)),
                    new GrantAction(new ConsumeSpell(4)),
                    new GrantAction(new ConsumeSpell(5)),
                ],
                Selections = [
                    new() { Label = "Choose a blessing", Options = Blessings.All.Select(b => b.ToFeat()) },
                    // new() { Label = "Choose a cantrip", Options = Blessings.All.Select(b => b.ToFeat()) },
                    new() { Label = "Choose a spell", Options = WarpriestList.Select(b => b.ToFeat()) },
                ],
            },
        ],
        ClassFeats = [WarpriestFeats.SwiftBlessing, WarpriestFeats.TrulyBlessed, WarpriestFeats.SacredStrike],
        GrantStartingEquipment = p =>
        {
            // Deity's favored weapon
            var weaponDef = GetWeaponForProficiency(p.Deity!.FavoredWeapon);
            if (weaponDef != null)
            {
                var weapon = ItemGen.GenerateItem(weaponDef, depth: 1, maxPotency: 1, propertyRunes: false);
                p.Inventory.Add(weapon);
                p.AddFact(new GrantProficiency(p.Deity.FavoredWeapon, ProficiencyLevel.Trained));
            }
            p.Inventory.Add(Item.Create(MundaneArmory.LeatherArmor));
        },
    };

    static WeaponDef? GetWeaponForProficiency(string prof) => prof switch
    {
        Proficiencies.Longsword => MundaneArmory.Longsword,
        Proficiencies.Scimitar => MundaneArmory.Scimitar,
        Proficiencies.Rapier => MundaneArmory.Rapier,
        Proficiencies.Whip => MundaneArmory.Whip,
        Proficiencies.SpikedChain => MundaneArmory.SpikedChain,
        Proficiencies.Scythe => MundaneArmory.Scythe,
        Proficiencies.Falchion => MundaneArmory.Falchion,
        Proficiencies.Dagger => MundaneArmory.Dagger,
        Proficiencies.Unarmed => null,
        _ => null,
    };

}

public static class CommonSpells
{
    public static readonly SpellBrick CureLightWounds = new("Cure light wounds", 1, "heals for 1d6",
    (u, t) =>
    {
        if (t.Pos == null) return;
        var target = lvl.UnitAt(u.Pos + t.Pos.Value);
        if (target == null) return;

        int dice = (1 + u.CasterLevel) / 2;
        using var ctx = PHContext.Create(u, t);

        if (target.Has("undead") == true)
        {
            ctx.Damage.Add(new() {
                Formula = d(dice, 8) + 2,
                Type = DamageTypes.Magic,
            });
            DoDamage(ctx);
            g.pline($"The positive energy sears {target:the}!");
        }
        else
        {
            g.DoHeal(u, target, d(dice, 6));
            g.pline($"{target:The} {VTense(target, "look")} a little better.");
        }
    }, TargetingType.Direction);
}

internal class ConsumeSpell(int lvl) : ActionBrick($"consume spell {lvl}")
{
    private string Pool => $"spell_l{lvl}";
    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot) => unit.HasCharge(Pool, out whyNot);

    public override void Execute(IUnit unit, object? data, Target target)
    {
        unit.TryUseCharge(Pool);
    }
}