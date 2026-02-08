# Temporary HP

Temp HP is a buffer absorbed before real HP. Not a buff/brick - core system.

## Stacking

Diminishing returns formula:
```
efficiency = incoming / (incoming + current² / incoming)
added = incoming * efficiency
```

- First application: 100% efficiency
- Subsequent: diminishing
- 5 apps of 20 ≈ 45, 10 apps ≈ 58

## Decay

- Infinite duration while undamaged
- Once damaged, decays 33% per round
- Minimum decay of ~5 HP/round so it fully drains

## Damage Order

1. Temp HP absorbs first
2. Then protection pools
3. Then DR
4. Then real HP
