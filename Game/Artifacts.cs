namespace Pathhack.Game;

[GenerateAll("All", typeof(WeaponDef))]
public static partial class GeneratableArtifacts
{
    // === Obliteration (earth breaker) ===
    // On hit: +2 stacks of AC debuff on target. On miss: +1 stack. Max 10. Penalty = -(stacks/2).

    public static readonly WeaponDef Obliteration = new()
    {
        id = "obliteration",
        Name = "Obliteration",
        BaseDamage = d(2, 6),
        Style = WeaponStyle.Impact, Grip = WeaponGrip.Great,
        WeaponType = WeaponTypes.EarthBreaker,
        DamageType = DamageTypes.Blunt,
        Hands = 2,
        MeleeVerb = "smash",
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Magenta),
        Weight = 140,
        Price = 5000,
        Components = [new ObliterationBrick()],
        IsUnique = true,
        PokedexDescription = "Shatters defenses. Hit: -2 AC stacks on target. Miss: -1 stack. Max 10 stacks, penalty = stacks/2.",
    };

    public class ObliterationDebuff : LogicBrick
    {
        public static readonly ObliterationDebuff Instance = new();
        public override string Id => "obliteration_debuff";
        public override bool IsBuff => true;
        public override string? BuffName => "Shattered";
        public override StackMode StackMode => StackMode.ExtendStacks;

        protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
        {
            "ac" => new Modifier(ModifierCategory.UntypedStackable, -(fact.Stacks / 2), "obliteration"),
            _ => null
        };
    }

    public class ObliterationBrick : LogicBrick
    {
        public override string Id => "obliteration_brick";

        protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
        {
            if (ctx.Weapon != fact.Entity) return;
            var target = ctx.Target?.Unit;
            if (target == null) return;

            int add = ctx.Check!.Result ? 2 : 1;
            var existing = target.FindFact(ObliterationDebuff.Instance);
            if (existing != null && existing.Stacks >= 10) return;

            for (int i = 0; i < add; i++)
            {
                if ((existing?.Stacks ?? 0) >= 10) break;
                target.AddFact(ObliterationDebuff.Instance, ctx.Source);
                existing ??= target.FindFact(ObliterationDebuff.Instance);
            }
        }
    }

    // === Gluttonous Scythe ===
    // Extra 1d4 damage vs living, heal wielder for the d4 amount.

    public static readonly WeaponDef GluttonousScythe = new()
    {
        id = "gluttonous_scythe",
        Name = "Gluttonous Scythe",
        BaseDamage = d(10),
        Style = WeaponStyle.Carve, Grip = WeaponGrip.Polearm,
        WeaponType = WeaponTypes.Scythe,
        DamageType = DamageTypes.Slashing,
        Hands = 2,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.DarkRed),
        Weight = 100,
        Price = 5000,
        Components = [new GluttonousBrick()],
        IsUnique = true,
        PokedexDescription = "Drains life from the living. Deals 1d4 bonus magic damage and heals the wielder for the same. No effect on undead or constructs.",
    };

    public class GluttonousBrick : LogicBrick<GluttonousBrick.State>
    {
        public class State { public DamageRoll? Pending; }
        public override string Id => "gluttonous_brick";

        protected override void OnBeforeDamageRoll(Fact fact, PHContext ctx)
        {
            if (ctx.Weapon != fact.Entity) return;
            var target = ctx.Target?.Unit;
            if (target == null || target.IsCreature(CreatureTypes.Undead) || target.IsCreature(CreatureTypes.Construct)) return;

            var roll = new DamageRoll { Formula = d(4), Type = DamageTypes.Magic };
            X(fact).Pending = roll;
            ctx.Damage.Add(roll);
        }

        protected override void OnDamageDone(Fact fact, PHContext ctx)
        {
            var state = X(fact);
            if (state.Pending == null) return;
            var roll = state.Pending;
            state.Pending = null;

            if (roll.Negated) return;
            int healed = roll.Total;
            if (healed <= 0) return;

            var wielder = ctx.Source!;
            g.DoHeal(wielder, wielder, healed);
        }
    }

    // === Ovinrbaane, Enemy of All Enemies (greatsword) ===
    // +3 attack, +2d6 damage. On hit: wielder will save DC 12 or dazed. Cursed.

    public static readonly WeaponDef Ovinrbaane = new()
    {
        id = "ovinrbaane",
        Name = "Ovinrbaane",
        BaseDamage = d(2, 6),
        Style = WeaponStyle.Carve, Grip = WeaponGrip.Great,
        WeaponType = WeaponTypes.Greatsword,
        DamageType = DamageTypes.Slashing,
        Hands = 2,
        MeleeVerb = "cleave",
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Red),
        Weight = 80,
        Price = 5000,
        BUCBias = -2,
        Components = [new OvinrbaaneBrick()],
        IsUnique = true,
        PokedexDescription = "Enemy of all enemies. +3 attack, +2d6 magic damage. On hit: wielder must save Will DC 12 or be dazed. Cursed.",
    };

    public class OvinrbaaneBrick : LogicBrick
    {
        public override string Id => "ovinrbaane_brick";

        protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
        {
            if (ctx.Weapon != fact.Entity) return;
            ctx.Check!.Modifiers.Untyped(3, "ovinrbaane");
        }

        protected override void OnBeforeDamageRoll(Fact fact, PHContext ctx)
        {
            if (ctx.Weapon != fact.Entity) return;
            ctx.Damage.Add(new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Magic });
        }

        protected override void OnDamageDone(Fact fact, PHContext ctx)
        {
            if (ctx.Weapon != fact.Entity) return;
            var wielder = ctx.Source!;
            if (wielder.Has(CommonQueries.DazeImmune)) return;

            using var inner = PHContext.Create(DungeonMaster.Mook, Target.From(wielder));
            if (!CheckWill(inner, 12, "ovinrbaane"))
            {
                g.YouObserveSelf(wielder,
                    "Ovinrbaane's fury overwhelms you!",
                    $"{wielder:The} staggers, eyes glazed!");
                wielder.AddFact(DazedBuff.Instance, wielder, 1);
            }
        }
    }

    // === Dawnflower's Kiss (scimitar) ===
    // Fire damage rider on melee. +2 attack vs undead. Apply: directional beam, 2d6 fire + 2d6 holy vs undead.
    // TODO: better holy-hater tracking beyond just undead

    public static readonly WeaponDef DawnflowersKiss = new()
    {
        id = "dawnflowers_kiss",
        Name = "Dawnflower's Kiss",
        BaseDamage = d(6),
        Style = WeaponStyle.Carve, Grip = WeaponGrip.Heavy,
        WeaponType = WeaponTypes.Scimitar,
        DamageType = DamageTypes.Slashing,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Yellow),
        Weight = 40,
        Price = 5000,
        Components =
        [
            new WeaponDamageRider("Dawnflower's Flame", DamageTypes.Fire, d(6)),
            new DawnflowerAttackBrick(),
            new DawnflowerBeam(),
        ],
        IsUnique = true,
        PokedexDescription = "Sacred blade of Sarenrae. +1d6 fire on hit, +2 attack vs undead. Apply: beam of dawn's light (2d6 fire, +2d6 holy vs undead).",
    };

    public class DawnflowerAttackBrick : LogicBrick
    {
        public override string Id => "dawnflower_attack";

        protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
        {
            if (ctx.Weapon != fact.Entity) return;
            if (ctx.Target?.Unit?.IsCreature(CreatureTypes.Undead) == true)
                ctx.Check!.Modifiers.Untyped(2, "dawnflower");
        }
    }

    public class DawnflowerBeam() : VerbResponder(ItemVerb.Apply)
    {
        class DawnflowerBeamIsSleeping : LogicBrick
        {
            public override string Id => "dawnflower_beam:sleeping";
            public static readonly DawnflowerBeamIsSleeping Instance = new();
        }

        public override string Id => "dawnflower_beam";

        protected override void OnVerb(Fact fact, ItemVerb verb)
        {
            if (fact.Entity is not Item item) return;
            var wielder = item.Holder;
            if (wielder == null || !wielder.IsPlayer) return;

            if (item.HasFact(DawnflowerBeamIsSleeping.Instance))
            {
                g.pline($"You feel {item:the} is ignoring you.");
                return;
            }

            g.pline("Fire in which direction?");
            var dir = Input.PickDirection();
            if (dir == null) return;

            item.AddFact(DawnflowerBeamIsSleeping.Instance, null, 30);
            g.pline("Dawn's light shoots forth!");

            foreach (var step in Beam.Fire(wielder.Pos, dir.Value, canBounce: false, g.RnRange(6, 10)))
            {
                var target = lvl.UnitAt(step.Pos);
                if (target != null)
                {
                    Draw.AnimateBeam(step.SegmentStart, step.Pos, new Glyph('*', ConsoleColor.Yellow), pulse: true);
                    using var ctx = PHContext.Create(wielder, Target.From(target));
                    ctx.Damage.Add(new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Fire });
                    if (target.IsCreature(CreatureTypes.Undead))
                    {
                        ctx.Damage.Add(new DamageRoll { Formula = d(2, 6), Type = DamageTypes.Holy });
                        g.YouObserve(target, $"{target:The} burns in the light!", "a sizzle, a scream");
                    }
                    else
                    {
                        g.YouObserve(target, $"The light hits {target:the}!", "a sizzle");
                    }
                    DoDamage(ctx);

                    break;
                }

                if (step.IsLast)
                    Draw.AnimateBeam(step.SegmentStart, step.Pos, new Glyph('*', ConsoleColor.Yellow), pulse: true);
            }

            wielder.Energy -= ActionCosts.OneAction.Value;
        }
    }

    // === Sovereign (mace) ===
    // First hit on new target: wielder gains +1 stack buff (AC/attack/saves). Max 4 stacks, ~4 round duration per stack.

    public static readonly WeaponDef Sovereign = new()
    {
        id = "sovereign",
        Name = "Sovereign",
        BaseDamage = d(6),
        Style = WeaponStyle.Impact, Grip = WeaponGrip.Heavy,
        WeaponType = WeaponTypes.Mace,
        DamageType = DamageTypes.Blunt,
        Glyph = new(ItemClasses.Weapon, ConsoleColor.Cyan),
        Weight = 40,
        Price = 5000,
        Components = [new SovereignBrick()],
        IsUnique = true,
        PokedexDescription = "Rewards conquest. First hit on a new target grants +1 AC/attack/saves (max 4 stacks, ~4 rounds each).",
    };

    public class SovereignMarker : LogicBrick
    {
        public static readonly SovereignMarker Instance = new();
        public override string Id => "sovereign_marked";
        public override StackMode StackMode => StackMode.Reject;
    }

    public class SovereignBuff : LogicBrick
    {
        public static readonly SovereignBuff Instance = new();
        public override string Id => "sovereign_buff";
        public override bool IsBuff => true;
        public override string? BuffName => "Sovereign";
        public override StackMode StackMode => StackMode.Stack;

        protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
        {
            "ac" => new Modifier(ModifierCategory.UntypedStackable, fact.Stacks, "sovereign"),
            "fortitude_save" => new Modifier(ModifierCategory.UntypedStackable, fact.Stacks, "sovereign"),
            "reflex_save" => new Modifier(ModifierCategory.UntypedStackable, fact.Stacks, "sovereign"),
            "will_save" => new Modifier(ModifierCategory.UntypedStackable, fact.Stacks, "sovereign"),
            _ => null
        };

        protected override void OnBeforeAttackRoll(Fact fact, PHContext ctx)
        {
            if (ctx.Source != fact.Entity) return;
            ctx.Check!.Modifiers.Untyped(fact.Stacks, "sovereign");
        }
    }

    public class SovereignBrick : LogicBrick
    {
        public override string Id => "sovereign_brick";

        protected override void OnDamageDone(Fact fact, PHContext ctx)
        {
            if (ctx.Weapon != fact.Entity) return;
            var target = ctx.Target?.Unit;
            if (target == null) return;

            if (target.FindFact(SovereignMarker.Instance) != null) return;

            var wielder = ctx.Source!;
            var existing = wielder.FindFact(SovereignBuff.Instance);
            if (existing != null && existing.Stacks >= 4) return;

            target.AddFact(SovereignMarker.Instance, wielder, 4);
            wielder.AddFact(SovereignBuff.Instance.Timed(), wielder, 4);

            g.YouObserveSelf(wielder,
                "You feel Sovereign's authority grow!",
                $"{wielder:The} radiates authority!");
        }
    }
}
