namespace Pathhack.Dat;

public static class CryptLevels
{
    public static readonly MonsterDef Asar = new()
    {
        id = "asar",
        Name = "Asar",
        Glyph = new('Z', ConsoleColor.Magenta),
        HP = 30,
        AC = 16,
        AttackBonus = 2,
        DamageBonus = 1,
        LandMove = ActionCosts.LandMove20,
        Unarmed = NaturalWeapons.Claw_1d6,
        Size = UnitSize.Medium,
        CR = 4,
        SpawnWeight = 0, // unique, don't spawn randomly
        IsUnique = true,
        MoralAxis = MoralAxis.Evil,
        EthicalAxis = EthicalAxis.Neutral,
        Components = [
            new GrantAction(new NaturalAttack(NaturalWeapons.Claw_1d4)),
            new DropOnDeath(QuestItems.Everflame),
            new SayOnDeath("""
            [fg=darkcyan]As the bandit lord crumbles to dust, his voice rasps one final time[/fg]:
            "The Tyrant's curse...  it turned the flame against itself. It fears what it once protected.
            The brighter it burns, the further you are from the truth."
            """)
        ],
    };

    public static readonly SpecialLevel EverflameEnd = new("everflame_tomb", """
        11111     222222222     333333333 44444
        1...1     2.......2     3.......3 4...4
        1.<.+#####+...S...+#####+...S...+#+.B.4
        1...1     2.......2     3.......3 4...4
        11111     222222222     333333333 44444
        """,
        PostRender: b =>
        {
            b.Stair(b['<'], TileType.BranchUp);
            b.Monster(Asar, b['B']);
            foreach (var p in b.Marks('S'))
                b.Monster(MiscMonsters.Skeleton, p);
            b.Level.NoInitialSpawns = true;
        })
    {
        HasPortalToParent = true,
    };
}
