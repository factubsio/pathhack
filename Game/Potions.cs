namespace Pathhack.Game;

public class PotionDef : ItemDef
{
    public PotionDef()
    {
        Glyph = new(ItemClasses.Potion, ConsoleColor.Magenta);
        AppearanceCategory = Game.AppearanceCategory.Potion;
        Stackable = true;
    }
}

public static class Potions
{
    public static readonly PotionDef Healing = new() { Name = "potion of healing", Price = 40 };
    public static readonly PotionDef Speed = new() { Name = "potion of speed", Price = 120 };
    public static readonly PotionDef Paralysis = new() { Name = "potion of paralysis", Price = 120 };
    public static readonly PotionDef Antivenom = new() { Name = "potion of antivenom", Price = 40 };
    public static readonly PotionDef Omen = new() { Name = "bottled omen", Price = 120 };
    public static readonly PotionDef Panacea = new() { Name = "panacea", Price = 500};
    public static readonly PotionDef FalseLife = new() { Name = "potion of false life", Price = 40 };
    public static readonly PotionDef LesserInvisibility = new() { Name = "potion of lesser invisibility", Price = 120 };

    public static readonly PotionDef[] All = [Healing, Speed, Paralysis, Antivenom, Omen, Panacea, FalseLife, LesserInvisibility];

    static Potions()
    {
        for (int i = 0; i < All.Length; i++)
            All[i].AppearanceIndex = i;
    }

    public static void DoEffect(PotionDef def, IUnit user)
    {
        switch (def)
        {
            case var _ when def == Healing:
                g.DoHeal(user, user, d(8) + 4);
                g.pline("You feel better.");
                def.SetKnown();
                break;
            case var _ when def == Speed:
                user.AddFact(SpeedBuff.Instance.Timed(), 20 + g.Rn2(20));
                g.pline("You speed up!");
                def.SetKnown();
                break;
            case var _ when def == Paralysis:
                user.AddFact(ParalyzedBuff.Instance.Timed(), 5 + g.Rn2(10));
                g.pline("You freeze in place!");
                def.SetKnown();
                break;
            case var _ when def == Antivenom:
                var poisons = user.QueryFacts("poison");
                if (poisons.Count > 0)
                {
                    poisons[0].Remove();
                    g.pline(poisons.Count > 1 ? "The venom subsides." : "Your blood clears.");
                    def.SetKnown();
                }
                else
                    g.pline("You feel briefly nauseous.");
                break;
            case var _ when def == Omen:
                user.AddFact(OmenBuff.Instance, 3);
                g.pline("You glimpse possible futures.");
                def.SetKnown();
                break;
            case var _ when def == Panacea:
                bool cured = false;
                foreach (var a in user.GetAllFacts(null))
                {
                    if (a.Brick is AfflictionBrick) { a.Remove(); cured = true; }
                }
                if (cured)
                {
                    g.pline("Your body is purified.");
                    def.SetKnown();
                }
                else
                    g.pline("You feel momentarily pure.");
                break;
            case var _ when def == FalseLife:
                int amount = d(user.EffectiveLevel, 2, 5).Roll();
                user.GrantTempHp(amount);
                g.pline("You feel temporarily invigorated.");
                def.SetKnown();
                break;
            case var _ when def == LesserInvisibility:
                user.AddFact(LesserInvisibilityBuff.Instance.Timed(), 20 + g.Rn2(20));
                g.pline("You fade from view.");
                def.SetKnown();
                break;
        }
    }
}

public class SpeedBuff : LogicBrick
{
    public static readonly SpeedBuff Instance = new();
    public override string Id => "speed";
    public override bool IsBuff => true;
    public override string? BuffName => "Haste";

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "speed_mult" => 1.5,
        _ => null
    };
}

public class OmenBuff : LogicBrick
{
    public static readonly OmenBuff Instance = new();
    public override string Id => "omen";
    public override bool IsBuff => true;
    public override string? BuffName => "Omen";
    public override StackMode StackMode => StackMode.Stack;
    public override int MaxStacks => 10;
    public override FactDisplayMode DisplayMode => FactDisplayMode.Name | FactDisplayMode.Stacks;

    protected override void OnBeforeCheck(Fact fact, PHContext ctx)
    {
        if (!ctx.IsCheckingOwnerOf(fact)) return;
        if (ctx.Check is not { IsSave: true }) return;

        ctx.Check.Advantage++;
        ((IUnit)fact.Entity).RemoveStack(this);
    }
}

public class LesserInvisibilityBuff : LogicBrick
{
    public static readonly LesserInvisibilityBuff Instance = new();
    public override string Id => "invisibility";
    public override bool IsBuff => true;
    public override string? BuffName => "Invisible";

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "invisible" => true,
        _ => null
    };

    protected override void OnAfterAttackRoll(Fact fact, PHContext ctx)
    {
        if (ctx.Source != fact.Entity) return;
        fact.Entity.RemoveStack(this.Timed());
        g.pline("You become visible!");
    }
}
