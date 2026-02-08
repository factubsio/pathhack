# Damage Reduction & Protection

## Damage Order

1. Roll damage
2. Apply halve/double
3. Protection absorbs (drains pool)
4. DR reduces remainder
5. Apply to HP

## Protection (Pool-based)

Finite pool that absorbs damage of matching type. Pool drains as damage absorbed.

```csharp
ProtectionBrick.Fire   // fire only
ProtectionBrick.Cold   // cold only
ProtectionBrick.Shock  // shock only
ProtectionBrick.Acid   // acid only
ProtectionBrick.Phys   // all physical (slash/pierce/blunt)
```

- Uses `StackMode.ExtendStacks` - new application takes max, doesn't add
- No duration - lasts until pool depleted
- Set in `OnBeforeDamageIncomingRoll`, drained in `OnDamageTaken`
- `DamageRoll.Protection` = pool available, `DamageRoll.ProtectionUsed` = actually absorbed

## DR (Flat Reduction)

Reduces each hit by flat amount unless bypassed.

```csharp
SimpleDR.Slashing.DR5   // DR 5/slashing
SimpleDR.Silver.DR10    // DR 10/silver
ComplexDR(10, or: ["good", "silver"])  // DR 10/good or silver
```

- Applied via `DamageRoll.ApplyDR(amount)`
- Takes highest if multiple sources (doesn't stack)
- Checked against `DamageRoll.Tags` for bypass

## Who Applies DR

The defender's LogicBricks apply DR in `OnBeforeDamageIncomingRoll`. Each brick checks if the damage roll matches its criteria (e.g. lacks a bypass tag) and calls `roll.ApplyDR(amount)`. The `DamageRoll.Total` getter then subtracts DR after protection.
