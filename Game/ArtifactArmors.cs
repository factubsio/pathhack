namespace Pathhack.Game;

[GenerateAll("All", typeof(ItemDef))]
public static partial class ArtifactArmors
{
    // === Seven League Boots, but worse (boots of speed) ===
    // Reduced movement cost + 2 bonus energy per round.

    public static readonly ItemDef FiveLeagueBoots = new()
    {
        id = "five_league_boots",
        Name = "Five League Boots",
        Glyph = new(ItemClasses.Armor, ConsoleColor.Magenta),
        DefaultEquipSlot = ItemSlots.Feet,
        Weight = 15,
        Price = 5000,
        IsUnique = true,
        ArtifactBaseId = "boots_of_speed",
        PokedexDescription = "Even speedier than speed boots. Reduces movement cost AND grants bonus energy each round, what a deal!",
        Components = [new FiveLeagueBrick()],
    };

    public class FiveLeagueBrick : LogicBrick
    {
        public override string Id => "seven_league";
        public override bool RequiresEquipped => true;
        public override bool IsActive => true;

        protected override object? OnQuery(Fact fact, string key, string? arg) =>
            key == CommonQueries.SpeedModifiersFlat
                ? new Modifier(ModifierCategory.ItemBonus, 3, "five league boots")
                : null;

        protected override void OnRoundStart(Fact fact)
        {
            if (fact.Entity is not Item { Holder: IUnit unit } || unit.CannotAct) return;
            unit.Energy += 2;
        }
    }

    // === Gauntlets of the Artificer (gauntlets of dexterity) ===
    // Dex bonus + prevents equipment degradation.

    public static readonly ItemDef GauntletsOfTheArtificer = new()
    {
        id = "gauntlets_of_the_artificer",
        Name = "Gauntlets of the Artificer",
        Glyph = new(ItemClasses.Armor, ConsoleColor.Magenta),
        DefaultEquipSlot = ItemSlots.Hands,
        Weight = 10,
        Price = 5000,
        IsUnique = true,
        ArtifactBaseId = "gauntlets_of_dexterity",
        PokedexDescription = "So dexterous, you can dodge even item damage.",
        Components = [PotencyDexBuff.Instance, new QueryBrickWhenEquipped("degrade_immune", true)],
    };

    // === Mirrorbright (heavy shield) ===
    // Reflection + confusion immunity + hallucination immunity.

    public static readonly ShieldDef Mirrorbright = new()
    {
        id = "mirrorbright",
        Name = "Mirrorbright",
        ACBonus = 2,
        Proficiency = Proficiencies.Shield,
        ArmorType = ArmorTypes.HeavyShield,
        BaseBlockChance = 40,
        Glyph = new(ItemClasses.Armor, ConsoleColor.Magenta),
        Weight = 130,
        Price = 5000,
        IsUnique = true,
        ArtifactBaseId = "heavy_shield",
        PokedexDescription = "Reflects stuff and that includes the whacky bad stuff in your mind.",
        Components =
        [
            new ShieldBrick(2, 40),
            RaiseShieldAction.Instance.WhenEquipped(),
            new MirrorbrightBrick(),
        ],
    };

    public class MirrorbrightBrick : LogicBrick
    {
        public override string Id => "mirrorbright_brick";
        public override bool RequiresEquipped => true;

        protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
        {
            "reflection" => (Func<IUnit, string>)(unit =>
            {
                string name = fact.Entity is Item item ? $"{item:bare}" : "shield";
                return $"reflects off {unit:possessive} {name}";
            }),
            CommonQueries.ConfusionImmune => true,
            CommonQueries.HallucinationImmune => true,
            _ => null,
        };
    }

    // === Shield of Yggdrasil (medium shield) ===
    // Regeneration + poison immunity.

    public static readonly ShieldDef ShieldOfYggdrasil = new()
    {
        id = "shield_of_yggdrasil",
        Name = "Shield of Yggdrasil",
        ACBonus = 2,
        Proficiency = Proficiencies.Shield,
        ArmorType = ArmorTypes.MediumShield,
        BaseBlockChance = 30,
        Glyph = new(ItemClasses.Armor, ConsoleColor.Magenta),
        Weight = 90,
        Price = 5000,
        IsUnique = true,
        ArtifactBaseId = "medium_shield",
        PokedexDescription = "Elves like wood and regen and don't like poison, so that's what this shield is.",
        Material = Materials.Wood,
        Components =
        [
            new ShieldBrick(2, 30),
            RaiseShieldAction.Instance.WhenEquipped(),
            RegenBrick.Always.WhenEquipped(),
            new QueryBrickWhenEquipped(CommonQueries.PoisonImmune, true),
        ],
    };
}
