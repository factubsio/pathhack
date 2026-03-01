namespace Pathhack.Knowledge;

public static class AfflictionPages
{
    public static readonly MechanicsPage SnakeVenom = new()
    {
        Name = "Snake Venom",
        Description = """
            [fg=Cyan]Poison[/] — Fortitude save. Ticks every ~15 rounds. Can auto-cure.

            A mild venom from lesser snakes. Deals 1d4 poison damage per tick.

            At stage 2+: -2 Str penalty.
            """,
    };

    public static readonly MechanicsPage VirulentSnakeVenom = new()
    {
        Name = "Virulent Snake Venom",
        Description = """
            [fg=Cyan]Poison[/] — Fortitude save. Ticks every ~15 rounds. Can auto-cure.

            A potent venom from greater snakes. Damage escalates with stage: 1d6 → 1d8 → 2d6.

            Stage 1+: -2 Str.
            Stage 2+: -2 Con (scaling to -4 at stage 4).
            Stage 3+: -2 Dex.
            Stage 4+: Str and Con penalties increase to -4.

            5 stages total — very dangerous if left untreated.
            """,
    };

    public static readonly MechanicsPage SpiderVenom = new()
    {
        Name = "Spider Venom",
        Description = """
            [fg=Cyan]Poison[/] — Fortitude save. Ticks every ~46 rounds. Can auto-cure.

            A neurotoxic venom that progressively slows and [link=Paralyzed]paralyzes[/].

            Reflex penalty scales with stage. At stage 3+, movement speed is reduced. At stage 5+, periodic [link=Paralyzed]paralysis[/] sets in, increasing in duration with stage.

            13 stages — slow-ticking but devastating if it reaches high stages.
            """,
    };

    public static readonly MechanicsPage PetrificationAffliction = new()
    {
        Name = "Petrification",
        Description = """
            [fg=Red]Petrification[/] — Fortitude save (DC 20). Ticks every ~2 rounds.

            Progressively turns you to stone. At stage 5, you are permanently petrified — this is lethal.

            Ticks very fast. Some creatures are immune to petrification. Carry a cure if you expect to face petrifying enemies.
            """,
    };

    public static readonly MechanicsPage ViridiumBlightAffliction = new()
    {
        Name = "Viridium Blight",
        Description = """
            [fg=DarkGreen]Disease[/] — Fortitude save (DC 18). Ticks every ~160 rounds.

            A wasting disease from viridium golems. Penalizes ALL stats by an amount equal to the current stage.

            6 stages. Slow-ticking but crippling at high stages — every attribute degrades simultaneously.
            """,
    };

    public static readonly MechanicsPage FoodPoisoningAffliction = new()
    {
        Name = "Food Poisoning",
        Description = """
            [fg=Cyan]Poison[/] — Fortitude save (DC 11). Ticks every ~175 rounds.

            From eating tainted food. Penalizes Str and Con.

            At stage 3+, periodic vomiting drains nutrition. At stage 5, vomiting becomes frequent. Low DC but slow to clear — keep food handy.
            """,
    };

    public static readonly MechanicsPage HeartrotAffliction = new()
    {
        Name = "Heartrot",
        Description = """
            [fg=DarkGreen]Disease[/] — Fortitude save (DC 16). Ticks every ~135 rounds.

            A rotting disease spread by blighted trees. Penalizes Wis (scaling with stage) and Con (from stage 2+).

            At stage 2+, periodically causes [link=Dazed]daze[/]. At stage 4, daze chance increases significantly.

            4 stages. The daze procs make this more dangerous than the stat penalties suggest.
            """,
    };

    public static readonly MechanicsPage[] All =
    [
        SnakeVenom, VirulentSnakeVenom, SpiderVenom,
        PetrificationAffliction, ViridiumBlightAffliction,
        FoodPoisoningAffliction, HeartrotAffliction,
    ];
}
