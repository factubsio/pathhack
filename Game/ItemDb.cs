namespace Pathhack.Game;

[Flags]
public enum ItemKnowledge { None = 0, Seen = 1, BUC = 4, PropRunes = 8, PropQuality = 16, PropPotency = 32, PropChecked = 64, Props = PropRunes | PropQuality | PropPotency }

public enum AppearanceCategory { Potion, Scroll, Amulet, Boots, Gloves, Cloak, Ring, Bottle, Wand, Spellbook, Bag, Rune }

public record Appearance(string Name, ConsoleColor Color, string? Material = null);

public class ItemDb
{
    public static ItemDb Instance { get; private set; } = new();

    static readonly Dictionary<AppearanceCategory, Appearance[]> Pools = new()
    {
        [AppearanceCategory.Potion] = [
            new("milky potion", ConsoleColor.White),
            new("fizzy potion", ConsoleColor.Yellow),
            new("murky potion", ConsoleColor.DarkGray),
            new("bubbly potion", ConsoleColor.Cyan),
            new("smoky potion", ConsoleColor.Gray),
            new("glowing potion", ConsoleColor.Magenta),
            new("viscous potion", ConsoleColor.DarkGreen),
            new("oily potion", ConsoleColor.DarkYellow),
            new("ruby potion", ConsoleColor.Red),
            new("pink potion", ConsoleColor.Magenta),
            new("emerald potion", ConsoleColor.Green),
            new("brilliant blue potion", ConsoleColor.Blue),
            new("effervescent potion", ConsoleColor.Gray),
            new("sparkling potion", ConsoleColor.Cyan),
            new("dark potion", ConsoleColor.DarkGray),
            new("golden potion", ConsoleColor.Yellow),
            new("slimy potion", ConsoleColor.DarkGreen),
            new("curdled potion", ConsoleColor.White),
            new("iridescent potion", ConsoleColor.Cyan),
            new("tar-black potion", ConsoleColor.DarkGray),
            new("lumpy potion", ConsoleColor.DarkYellow),
            new("pungent potion", ConsoleColor.Green),
            new("swirling potion", ConsoleColor.Magenta),
            new("gritty potion", ConsoleColor.Gray),
        ],
        [AppearanceCategory.Bottle] = [
            new("luminous bottle", ConsoleColor.Yellow),
            new("cloudy bottle", ConsoleColor.Gray),
            new("shimmering bottle", ConsoleColor.Cyan),
            new("dark bottle", ConsoleColor.DarkMagenta),
            new("warm bottle", ConsoleColor.DarkRed),
            new("humming bottle", ConsoleColor.Green),
            new("frosted bottle", ConsoleColor.White),
            new("sparkling bottle", ConsoleColor.DarkYellow),
            new("viscous bottle", ConsoleColor.DarkGreen),
            new("rattling bottle", ConsoleColor.Gray),
            new("sealed bottle", ConsoleColor.DarkGray),
            new("smoking bottle", ConsoleColor.Red),
        ],
        [AppearanceCategory.Wand] = [
            new("glass wand", ConsoleColor.White),
            new("balsa wand", ConsoleColor.DarkYellow),
            new("crystal wand", ConsoleColor.Cyan),
            new("maple wand", ConsoleColor.DarkYellow),
            new("oak wand", ConsoleColor.DarkYellow),
            new("ebony wand", ConsoleColor.DarkGray),
            new("marble wand", ConsoleColor.White),
            new("tin wand", ConsoleColor.Gray),
            new("brass wand", ConsoleColor.Yellow),
            new("copper wand", ConsoleColor.DarkYellow),
            new("silver wand", ConsoleColor.White),
            new("platinum wand", ConsoleColor.White),
            new("iridium wand", ConsoleColor.Cyan),
            new("zinc wand", ConsoleColor.Gray),
            new("iron wand", ConsoleColor.DarkGray),
            new("steel wand", ConsoleColor.Gray),
            new("hexagonal wand", ConsoleColor.DarkGray),
            new("runed wand", ConsoleColor.DarkGray),
            new("curved wand", ConsoleColor.DarkGray),
            new("forked wand", ConsoleColor.DarkYellow),
            new("jeweled wand", ConsoleColor.Magenta),
            new("ceramic wand", ConsoleColor.DarkCyan),
            new("pine wand", ConsoleColor.DarkYellow),
            new("spiked wand", ConsoleColor.DarkGray),
        ],
        [AppearanceCategory.Scroll] = [
            new("scroll labelled ZELGO MER", ConsoleColor.White),
            new("scroll labelled FOOBIE BLETCH", ConsoleColor.White),
            new("scroll labelled TEMOV", ConsoleColor.White),
            new("scroll labelled GARVEN DEH", ConsoleColor.White),
            new("scroll labelled READ ME", ConsoleColor.White),
            new("scroll labelled XIXAXA XOXAXA", ConsoleColor.White),
            new("scroll labelled PRATYAVAYAH", ConsoleColor.White),
            new("scroll labelled ELBIB YLOH", ConsoleColor.White),
            new("scroll labelled JUYED AWK YACC", ConsoleColor.White),
            new("scroll labelled NR 9", ConsoleColor.White),
            new("scroll labelled DAIYEN FOOELS", ConsoleColor.White),
            new("scroll labelled LEP GEX VEN ZEA", ConsoleColor.White),
            new("scroll labelled PRIRUTSENIE", ConsoleColor.White),
            new("scroll labelled VERR YED HORRE", ConsoleColor.White),
            new("scroll labelled VENZAR BORGAVVE", ConsoleColor.White),
            new("scroll labelled HACKEM MUCHE", ConsoleColor.White),
            new("scroll labelled ANDOVA BEGARIN", ConsoleColor.White),
            new("scroll labelled KIRJE", ConsoleColor.White),
            new("scroll labelled VELOX NEB", ConsoleColor.White),
            new("scroll labelled KERNOD WEL", ConsoleColor.White),
            new("scroll labelled ELAM EBOW", ConsoleColor.White),
            new("scroll labelled DUAM XNAHT", ConsoleColor.White),
            new("scroll labelled THARR", ConsoleColor.White),
            new("scroll labelled YUM YUM", ConsoleColor.White),
            new("scroll labelled KWAH KWEH", ConsoleColor.White),
            new("scroll labelled NIHIL VERUM", ConsoleColor.White),
            new("scroll labelled ZLORFIK", ConsoleColor.White),
            new("scroll labelled GNUSTO REZROV", ConsoleColor.White),
            new("scroll labelled VAS MANI", ConsoleColor.White),
            new("scroll labelled ETAOIN SHRDLU", ConsoleColor.White),
            new("scroll labelled MAPIRO MAHAMA DIROMAT", ConsoleColor.White),
            new("scroll labelled ASHPD ASHPD", ConsoleColor.White),
        ],
        [AppearanceCategory.Amulet] = [
            new("circular amulet", ConsoleColor.Gray),
            new("jade amulet", ConsoleColor.Green),
            new("spherical amulet", ConsoleColor.Cyan),
            new("triangular amulet", ConsoleColor.Yellow),
            new("copper amulet", ConsoleColor.DarkYellow),
            new("silver amulet", ConsoleColor.White),
            new("oval amulet", ConsoleColor.Gray),
            new("pyramidal amulet", ConsoleColor.DarkYellow),
            new("teardrop amulet", ConsoleColor.Cyan),
            new("square amulet", ConsoleColor.DarkGray),
            new("concave amulet", ConsoleColor.Gray),
            new("convex amulet", ConsoleColor.Gray),
            new("pentagonal amulet", ConsoleColor.DarkCyan),
            new("hexagonal amulet", ConsoleColor.DarkGray),
            new("octagonal amulet", ConsoleColor.White),
            new("warped amulet", ConsoleColor.DarkMagenta),
            new("cardioid amulet", ConsoleColor.Red),
            new("oblong amulet", ConsoleColor.Gray),
            new("crescent amulet", ConsoleColor.Yellow),
            new("star-shaped amulet", ConsoleColor.White),
        ],
        [AppearanceCategory.Boots] = [
            new("leather boots", ConsoleColor.DarkYellow),
            new("riding boots", ConsoleColor.Gray),
            new("fur-lined boots", ConsoleColor.White),
            new("combat boots", ConsoleColor.DarkGray),
            new("mud boots", ConsoleColor.DarkYellow),
            new("jackboots", ConsoleColor.DarkRed),
            new("hiking boots", ConsoleColor.Green),
            new("iron-shod boots", ConsoleColor.DarkGray, "iron"),
        ],
        [AppearanceCategory.Gloves] = [
            new("leather gloves", ConsoleColor.DarkYellow),
            new("padded gloves", ConsoleColor.Gray),
            new("riding gloves", ConsoleColor.White),
            new("gauntlets", ConsoleColor.DarkGray),
            new("fencing gloves", ConsoleColor.DarkCyan),
            new("iron gauntlets", ConsoleColor.DarkGray, "iron"),
            new("silk gloves", ConsoleColor.Magenta),
            new("fingerless gloves", ConsoleColor.DarkYellow),
        ],
        [AppearanceCategory.Cloak] = [
            new("tattered cloak", ConsoleColor.DarkGray),
            new("opera cloak", ConsoleColor.Red),
            new("ornate cloak", ConsoleColor.Magenta),
            new("faded cloak", ConsoleColor.Gray),
            new("hooded cloak", ConsoleColor.DarkGray),
            new("oilskin cloak", ConsoleColor.DarkYellow),
            new("dusty cloak", ConsoleColor.Gray),
            new("velvet cloak", ConsoleColor.DarkMagenta),
            new("fur-trimmed cloak", ConsoleColor.White),
            new("patchwork cloak", ConsoleColor.DarkYellow),
            new("waxed cloak", ConsoleColor.DarkGreen),
            new("threadbare cloak", ConsoleColor.Gray),
        ],
        [AppearanceCategory.Spellbook] = [
            new("parchment spellbook", ConsoleColor.DarkYellow),
            new("vellum spellbook", ConsoleColor.White),
            new("ragged spellbook", ConsoleColor.DarkGray),
            new("dog eared spellbook", ConsoleColor.Gray),
            new("mottled spellbook", ConsoleColor.DarkYellow),
            new("stained spellbook", ConsoleColor.DarkRed),
            new("cloth spellbook", ConsoleColor.White),
            new("leather spellbook", ConsoleColor.DarkYellow),
            new("white spellbook", ConsoleColor.White),
            new("pink spellbook", ConsoleColor.Magenta),
            new("red spellbook", ConsoleColor.Red),
            new("orange spellbook", ConsoleColor.DarkYellow),
            new("yellow spellbook", ConsoleColor.Yellow),
            new("velvet spellbook", ConsoleColor.Magenta),
            new("light green spellbook", ConsoleColor.Green),
            new("dark green spellbook", ConsoleColor.DarkGreen),
            new("cyan spellbook", ConsoleColor.Cyan),
            new("light blue spellbook", ConsoleColor.Blue),
            new("dark blue spellbook", ConsoleColor.DarkBlue),
            new("indigo spellbook", ConsoleColor.DarkCyan),
            new("magenta spellbook", ConsoleColor.Magenta),
            new("purple spellbook", ConsoleColor.DarkMagenta),
            new("dusty spellbook", ConsoleColor.Gray),
            new("bronze spellbook", ConsoleColor.DarkYellow),
            new("worm-eaten spellbook", ConsoleColor.DarkYellow),
            new("charred spellbook", ConsoleColor.DarkGray),
            new("gilt-edged spellbook", ConsoleColor.Yellow),
            new("waterlogged spellbook", ConsoleColor.DarkCyan),
            new("iron-clasped spellbook", ConsoleColor.DarkGray),
            new("glowing spellbook", ConsoleColor.Cyan),
            new("blood-spattered spellbook", ConsoleColor.DarkRed),
            new("moss-covered spellbook", ConsoleColor.DarkGreen),
        ],
        [AppearanceCategory.Ring] = [
            new("iron ring", ConsoleColor.DarkGray, "iron"),
            new("silver ring", ConsoleColor.White, "silver"),
            new("gold ring", ConsoleColor.Yellow, "gold"),
            new("copper ring", ConsoleColor.DarkYellow, "copper"),
            new("jade ring", ConsoleColor.Green, "jade"),
            new("ruby ring", ConsoleColor.Red, "ruby"),
            new("sapphire ring", ConsoleColor.Blue, "sapphire"),
            new("opal ring", ConsoleColor.Cyan, "opal"),
            new("obsidian ring", ConsoleColor.DarkGray, "obsidian"),
            new("pearl ring", ConsoleColor.White, "pearl"),
            new("amethyst ring", ConsoleColor.Magenta, "amethyst"),
            new("onyx ring", ConsoleColor.DarkGray, "onyx"),
            new("topaz ring", ConsoleColor.Yellow, "topaz"),
            new("emerald ring", ConsoleColor.Green, "emerald"),
            new("bone ring", ConsoleColor.White, "bone"),
            new("bronze ring", ConsoleColor.DarkYellow, "bronze"),
            new("moonstone ring", ConsoleColor.Cyan, "moonstone"),
            new("granite ring", ConsoleColor.Gray, "granite"),
            new("coral ring", ConsoleColor.Red, "coral"),
            new("ivory ring", ConsoleColor.White, "ivory"),
            new("tin ring", ConsoleColor.Gray, "tin"),
            new("brass ring", ConsoleColor.DarkYellow, "brass"),
            new("wooden ring", ConsoleColor.DarkYellow, "wood"),
            new("agate ring", ConsoleColor.DarkRed, "agate"),
        ],
        [AppearanceCategory.Bag] = [
            new("leather bag", ConsoleColor.DarkYellow),
            new("cloth bag", ConsoleColor.DarkYellow),
            new("greasy bag", ConsoleColor.DarkYellow),
            new("burlap bag", ConsoleColor.DarkYellow),
            new("canvas bag", ConsoleColor.DarkYellow),
        ],
        [AppearanceCategory.Rune] = [
            new("KEN-TYR-ISA", ConsoleColor.Red),
            new("KEN-TYR-SOL", ConsoleColor.Green),
            new("KEN-TYR-FEHU", ConsoleColor.Blue),
            new("KEN-TYR-GEBO", ConsoleColor.Cyan),
            new("KEN-TYR-JERA", ConsoleColor.Magenta),
            new("KEN-TYR-DAGAZ", ConsoleColor.Yellow),
            new("KEN-ISA-SOL", ConsoleColor.White),
            new("KEN-ISA-FEHU", ConsoleColor.DarkCyan),
            new("KEN-ISA-GEBO", ConsoleColor.DarkYellow),
            new("KEN-ISA-JERA", ConsoleColor.DarkMagenta),
            new("KEN-ISA-DAGAZ", ConsoleColor.Red),
            new("KEN-SOL-FEHU", ConsoleColor.Green),
            new("KEN-SOL-GEBO", ConsoleColor.Blue),
            new("KEN-SOL-JERA", ConsoleColor.Cyan),
            new("KEN-SOL-DAGAZ", ConsoleColor.Magenta),
            new("KEN-FEHU-GEBO", ConsoleColor.Yellow),
            new("KEN-FEHU-JERA", ConsoleColor.White),
            new("KEN-FEHU-DAGAZ", ConsoleColor.DarkCyan),
            new("KEN-GEBO-JERA", ConsoleColor.DarkYellow),
            new("KEN-GEBO-DAGAZ", ConsoleColor.DarkMagenta),
            new("KEN-JERA-DAGAZ", ConsoleColor.Red),
            new("TYR-ISA-SOL", ConsoleColor.Green),
            new("TYR-ISA-FEHU", ConsoleColor.Blue),
            new("TYR-ISA-GEBO", ConsoleColor.Cyan),
            new("TYR-ISA-JERA", ConsoleColor.Magenta),
            new("TYR-ISA-DAGAZ", ConsoleColor.Yellow),
            new("TYR-SOL-FEHU", ConsoleColor.White),
            new("TYR-SOL-GEBO", ConsoleColor.DarkCyan),
            new("TYR-SOL-JERA", ConsoleColor.DarkYellow),
            new("TYR-SOL-DAGAZ", ConsoleColor.DarkMagenta),
            new("TYR-FEHU-GEBO", ConsoleColor.Red),
            new("TYR-FEHU-JERA", ConsoleColor.Green),
            new("TYR-FEHU-DAGAZ", ConsoleColor.Blue),
            new("TYR-GEBO-JERA", ConsoleColor.Cyan),
            new("TYR-GEBO-DAGAZ", ConsoleColor.Magenta),
            new("TYR-JERA-DAGAZ", ConsoleColor.Yellow),
            new("ISA-SOL-FEHU", ConsoleColor.White),
            new("ISA-SOL-GEBO", ConsoleColor.DarkCyan),
            new("ISA-SOL-JERA", ConsoleColor.DarkYellow),
            new("ISA-SOL-DAGAZ", ConsoleColor.DarkMagenta),
            new("ISA-FEHU-GEBO", ConsoleColor.Red),
            new("ISA-FEHU-JERA", ConsoleColor.Green),
            new("ISA-FEHU-DAGAZ", ConsoleColor.Blue),
            new("ISA-GEBO-JERA", ConsoleColor.Cyan),
            new("ISA-GEBO-DAGAZ", ConsoleColor.Magenta),
            new("ISA-JERA-DAGAZ", ConsoleColor.Yellow),
            new("SOL-FEHU-GEBO", ConsoleColor.White),
            new("SOL-FEHU-JERA", ConsoleColor.DarkCyan),
            new("SOL-FEHU-DAGAZ", ConsoleColor.DarkYellow),
            new("SOL-GEBO-JERA", ConsoleColor.DarkMagenta),
            new("SOL-GEBO-DAGAZ", ConsoleColor.Red),
            new("SOL-JERA-DAGAZ", ConsoleColor.Green),
            new("FEHU-GEBO-JERA", ConsoleColor.Blue),
            new("FEHU-GEBO-DAGAZ", ConsoleColor.Cyan),
            new("FEHU-JERA-DAGAZ", ConsoleColor.Magenta),
            new("GEBO-JERA-DAGAZ", ConsoleColor.Yellow),
        ],
    };

    readonly Dictionary<AppearanceCategory, int[]> _shuffled = [];
    readonly HashSet<ItemDef> _identified = [];
    readonly Dictionary<ItemDef, string> _called = [];

    public void Initialize(int gameSeed)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{gameSeed}:appearances"));
        var rng = new Random(BitConverter.ToInt32(bytes, 0));
        _shuffled.Clear();
        _identified.Clear();
        _called.Clear();
        foreach (var (cat, pool) in Pools)
        {
            var indices = Enumerable.Range(0, pool.Length).ToArray();
            rng.Shuffle(indices);
            _shuffled[cat] = indices;
        }
    }

    public Appearance? GetAppearance(ItemDef def)
    {
        if (def.AppearanceCategory is not { } cat) return null;
        if (def.AppearanceIndex < 0) return null;
        if (!_shuffled.TryGetValue(cat, out var indices)) return null;
        var pool = Pools[cat];
        var idx = indices[def.AppearanceIndex % indices.Length];
        return pool[idx];
    }

    public bool IsIdentified(ItemDef def) => def.AppearanceCategory == null || _identified.Contains(def);
    public void Identify(ItemDef def) => _identified.Add(def);
    public IEnumerable<ItemDef> IdentifiedDefs => _identified;

    public string? GetCalledName(ItemDef def) => _called.GetValueOrDefault(def);
    public void SetCalledName(ItemDef def, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            _called.Remove(def);
        else
            _called[def] = name;
    }

    public static void Reset(int seed)
    {
        Instance = new ItemDb();
        Instance.Initialize(seed);
    }
}
