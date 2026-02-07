# Making a Monster Family

## File Structure

Create `Game/Bestiary/{Family}.cs`. Define components/abilities first, then monster defs, then the `All` array.

## Monster Definition

```csharp
public static readonly MonsterDef Example = new()
{
    id = "example",                    // unique, snake_case
    Name = "example",                  // display name
    Glyph = new('e', ConsoleColor.Red),
    HpPerLevel = 6,                    // Tiny=4, Small=6, Medium=8, Large=10, Huge=12
    AC = 0,                            // delta from LevelDC (0=baseline, +2=tanky, -2=squishy)
    AttackBonus = 0,                   // delta from LevelDC-5 (0=baseline)
    DamageBonus = 0,                   // flat bonus to damage
    LandMove = ActionCosts.StandardLandMove,  // 12=normal, 18=slow, 10=fast, 8=faster, 6=veryfast
    Unarmed = NaturalWeapons.Fist,     // fallback weapon
    Size = UnitSize.Medium,
    BaseLevel = 1,                     // determines DC scaling and spawn eligibility
    SpawnWeight = 1,                   // higher = more common
    MoralAxis = MoralAxis.Evil,
    EthicalAxis = EthicalAxis.Chaotic,
    CreatureType = CreatureTypes.Beast,
    SubTypes = [CreatureSubtypes.Blah...], // where appropriate
    Components = [ ... ],
};
```

## Common Components

### Basic Attacks
```csharp
new GrantAction(AttackWithWeapon.Instance),           // melee with equipped weapon
new GrantAction(new NaturalAttack(NaturalWeapons.Bite_1d6)),  // natural attack
```

### Equipment
```csharp
new Equip(MundaneArmory.Dagger),                      // always has
EquipSet.Roll(MundaneArmory.LeatherArmor, 50),        // 50% chance
EquipSet.OneOf(MundaneArmory.Dagger, MundaneArmory.Spear),  // pick one
```

### Resource Pools (for limited-use abilities)
```csharp
new GrantPool("fire_breath", 2, 50),  // name, max charges, regen rate
```

### Apply Effect on Hit
```csharp
new ApplyFactOnAttackHit(PoisonBuff.Instance, duration: 10),
new ApplyFactOnAttackHit(ProneBuff.Instance.Timed(), duration: 2),
```

### Passive Effects
```csharp
DrunkenDodge.Instance,  // just add the LogicBrick directly
```

## Custom Actions

```csharp
public class MyAbility : ActionBrick
{
    public static readonly MyAbility Instance = new();
    MyAbility() : base("my_ability") { }  // snake_case id

    public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
    {
        whyNot = "reason";
        // check conditions
        return true;
    }

    public override void Execute(IUnit unit, object? data, Target target)
    {
        // do the thing
    }
}
```

### With Charges
```csharp
public override bool CanExecute(IUnit unit, object? data, Target target, out string whyNot)
{
    if (!unit.HasCharge("pool_name", out whyNot)) return false;
    // other checks
    return true;
}

public override void Execute(IUnit unit, object? data, Target target)
{
    unit.TryUseCharge("pool_name");
    // do the thing
}
```

## Custom Debuffs/Buffs

```csharp
public class MyDebuff : LogicBrick
{
    public static readonly MyDebuff Instance = new();
    public override bool IsBuff => true;
    public override bool IsActive => true;  // if needs OnRoundStart/End
    public override string? BuffName => "My Debuff";

    protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
    {
        if (ctx.Source != fact.Entity) return;
        ctx.Check!.Modifiers.Untyped(-2, "debuff");
    }
}
```

### Common Hooks
- `OnBeforeAttackRoll` - modify attack rolls
- `OnBeforeDefendRoll` - modify defense (AC checks)
- `OnBeforeCheck` - modify any check (saves, skills)
- `OnBeforeDamageRoll` - add/modify damage
- `OnDamageTaken` - react to taking damage
- `OnRoundStart/OnRoundEnd` - periodic effects

## Custom Brain (weird AI) // should be rare

```csharp
public class WeirdBrain : MonsterBrain
{
    public override bool DoTurn(Monster m)
    {
        m.Energy -= ActionCosts.OneAction.Value;
        // custom behavior
        return true;  // true = did something, false = nothing to do
    }
}
```

Then in def: `Brain = new WeirdBrain(),`

## Full Attack (multiple natural attacks)

```csharp
new GrantAction(new FullAttack("maul",  //or flavour name
    NaturalWeapons.Bite_1d6, 
    NaturalWeapons.Claw_1d4, 
    NaturalWeapons.Claw_1d4)),
```

## Pounce (leap + full attack)

```csharp
new GrantAction(Pounce.Instance),
new GrantAction(new FullAttack("maul", ...)),  // pounce uses this
```

## Register in AllMonsters

```csharp
// Game/Bestiary/AllMonsters.cs
public static readonly MonsterDef[] All = [
    .. Goblins.All,
    .. Cats.All,
    .. YourFamily.All,  // add this
];
```

## Test

```bash
dotnet run -- --monsters
```

## Examples

- **Cats.cs** - full attacks, pounce, variable move costs
- **Gremlins.cs** - debuffs on hit, custom brain, pooled abilities
- **Goblins.cs** - fire breath, war chant buff, family coordination
- **Derro.cs** - ranged abilities (telekinesis, daze)
