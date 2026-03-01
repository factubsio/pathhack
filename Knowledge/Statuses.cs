namespace Pathhack.Knowledge;

public static class StatusPages
{
    public static readonly MechanicsPage Blind = new()
    {
        Name = "Blind",
        Description = """
            You cannot see. All attacks you make have disadvantage. All attacks against you have advantage.

            Blindness can come from multiple sources (spells, equipment, afflictions). Each source adds a stack — removing one source doesn't cure blindness if another remains.

            Darkvision and tremorsense still function while blind, but normal vision does not.
            """,
    };

    public static readonly MechanicsPage Prone = new()
    {
        Name = "Hamstrung",
        Description = """
            You suffer -2 AC and your movement speed is halved.

            Like blindness, hamstrung stacks from multiple sources.
            """,
    };

    public static readonly MechanicsPage Silenced = new()
    {
        Name = "Silenced",
        Description = """
            You cannot speak. This prevents verbal spellcasting and maintained spells require a Will save each round or they are lost.

            Stacks from multiple sources.
            """,
    };

    public static readonly MechanicsPage Paralyzed = new()
    {
        Name = "Paralyzed",
        Description = """
            You cannot act at all. You skip your entire turn.

            Extremely dangerous — being paralyzed in melee is often fatal. Some creatures are immune to paralysis.
            """,
    };

    public static readonly MechanicsPage Stunned = new()
    {
        Name = "Stunned",
        Description = """
            You cannot act. Similar to paralysis but typically shorter in duration.

            Some creatures are immune to stun.
            """,
    };

    public static readonly MechanicsPage Dazed = new()
    {
        Name = "Dazed",
        Description = """
            You cannot act for 1 round. After daze wears off, you gain [link=Daze Immunity]daze immunity[/] for 4 rounds, preventing chain-dazing.

            Daze is the standard "skip a turn" punishment — strong enough to matter, but the immunity window prevents it from being a death sentence.
            """,
    };

    public static readonly MechanicsPage DazeImmunityPage = new()
    {
        Name = "Daze Immunity",
        Description = """
            Granted automatically for 4 rounds after being [link=Dazed]dazed[/]. While active, you cannot be dazed again.
            """,
    };

    public static readonly MechanicsPage Nauseated = new()
    {
        Name = "Nauseated",
        Description = """
            Each time you make a check (attack, save, skill), you have disadvantage on that check and one stack of nausea is removed.

            Multiple stacks mean multiple checks are affected before it clears.
            """,
    };

    public static readonly MechanicsPage Fleeing = new()
    {
        Name = "Fleeing",
        Description = """
            You are overcome with fear and attempt to flee from the source. Monsters will run away from the player. The player loses direct control of movement.
            """,
    };

    public static readonly MechanicsPage Confused = new()
    {
        Name = "Confused",
        Description = """
            Your actions are randomized. You may attack allies, move randomly, or act normally — you don't get to choose.

            Confusion uses extend-duration stacking: re-applying extends the timer if the new duration is longer. Some creatures are immune to confusion.
            """,
    };

    public static readonly MechanicsPage Bleed = new()
    {
        Name = "Bleed",
        Description = """
            You take damage at the start of each round based on the number of bleed stacks (1d4 per stack, up to 10 stacks).

            Bleed is stopped by any magical healing. Some creatures (constructs, undead) are immune to bleed.

            Applying bleed requires the target to fail a Fortitude save.
            """,
    };

    public static readonly MechanicsPage CursedWounds = new()
    {
        Name = "Cursed Wounds",
        Description = """
            Your wounds refuse to close. Natural regeneration is completely suppressed and all healing received is negated.

            Cursed wounds are timed and will eventually expire. Extremely dangerous if sustained in a prolonged fight.
            """,
    };

    public static readonly MechanicsPage Afflictions = new()
    {
        Name = "Afflictions",
        Description = """
            Afflictions are progressive conditions like poisons and diseases. They have stages — higher stages mean worse effects.

            Periodically, you make a Fortitude save against the affliction's DC. Success reduces the stage by one. Failure increases it by one. At stage 0, you are cured. At max stage, you suffer the worst effects.

            If the same affliction is applied again, the higher DC wins.

            Some afflictions auto-cure after enough time has passed.
            """,
    };

    public static readonly MechanicsPage[] All =
    [
        Blind, Prone, Silenced, Paralyzed, Stunned, Dazed, DazeImmunityPage,
        Nauseated, Fleeing, Confused, Bleed, CursedWounds, Afflictions,
    ];
}
