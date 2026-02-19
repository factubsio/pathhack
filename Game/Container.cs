namespace Pathhack.Game;

public class ContainerBrick() : VerbResponder(ItemVerb.Apply)
{
    public static readonly ContainerBrick Instance = new();
    public override string Id => "container";

    public class State
    {
        public List<Item> Contents = [];
        public bool Locked;
        public bool Trapped;
    }

    public override object? CreateData() => new State();

    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "weight" => X(fact).Contents.Sum(i => i.EffectiveWeight),
        _ => null
    };

    protected override void OnVerb(Fact fact, ItemVerb verb)
    {
        if (fact.Entity is not Item item) return;
        var state = X(fact);

        if (state.Locked)
        {
            g.pline($"{item:The} is locked.");
            return;
        }

        if (state.Trapped)
        {
            state.Trapped = false;
            // TODO: chest trap effects
            g.pline("You trigger a trap!");
            return;
        }

        DoLoot(item, state);
    }

    static void DoLoot(Item item, State state)
    {
        if (u.CanSee)
            foreach (var i in state.Contents)
                i.Knowledge |= ItemKnowledge.Seen;

        bool hasContents = state.Contents.Count > 0;
        bool hasInventory = u.Inventory.Count > 0;

        if (!hasContents && !hasInventory)
        {
            g.pline($"{item:The} is empty.");
            g.pline("You don't have anything to put in.");
            return;
        }

        string prompt = hasContents ? "Do what?" : $"{item:The} is empty. Do what?";

        Menu<string> actionMenu = new();
        actionMenu.Add(prompt, LineStyle.Heading);
        actionMenu.Add("");
        if (hasContents) actionMenu.Add('o', $"Take something out of {item:the}", "out");
        if (hasInventory) actionMenu.Add('i', $"Put something into {item:the}", "in");
        if (hasContents && hasInventory) actionMenu.Add('b', "Both of the above", "both");
        if (hasContents) actionMenu.Add('t', "Tip out all contents", "tip");

        List<string> actions = actionMenu.Display(MenuMode.PickOne);
        if (actions.Count == 0) return;

        string action = actions[0];
        bool lootOut = action is "out" or "both";
        bool lootIn = action is "in" or "both";
        bool acted = false;

        if (action == "tip")
            acted |= TipOver(item, state);

        if (lootOut)
            acted |= TakeOut(item, state);

        if (lootIn)
            acted |= PutIn(item, state);

        if (acted)
            u.Energy -= ActionCosts.OneAction.Value;
    }

    /// <summary>
    /// dNH-style category filter menu. Returns null if cancelled, or filtered item list.
    /// </summary>
    static List<Item>? PickCategories(string verb, IEnumerable<Item> source, bool chooseAll)
    {
        var categories = source.Select(i => i.Def.Class).Distinct().ToList();
        bool hasBlessedKnown = source.Any(i => i.Knowledge.HasFlag(ItemKnowledge.BUC) && i.BUC == BUC.Blessed);
        bool hasCursedKnown = source.Any(i => i.Knowledge.HasFlag(ItemKnowledge.BUC) && i.BUC == BUC.Cursed);
        bool hasUncursedKnown = source.Any(i => i.Knowledge.HasFlag(ItemKnowledge.BUC) && i.BUC == BUC.Uncursed);
        bool hasBucUnknown = source.Any(i => !i.Knowledge.HasFlag(ItemKnowledge.BUC));
        int bucCount = (hasBlessedKnown ? 1 : 0) + (hasCursedKnown ? 1 : 0) + (hasUncursedKnown ? 1 : 0) + (hasBucUnknown ? 1 : 0);

        // Single category + ≤1 BUC type → skip menu
        if (categories.Count <= 1 && bucCount <= 1)
            return [.. source];

        Menu<object> catMenu = new();
        catMenu.Add($"{verb} what type of objects?", LineStyle.Heading);
        catMenu.Add("");

        char let = 'a';
        if (categories.Count > 1)
        {
            catMenu.Add(let++, "All types", "all");
        }
        foreach (char c in ItemClasses.Order.Where(categories.Contains))
            catMenu.Add(let++, c, $"{Input.ClassDisplayName(c)}  ('{c}')", (object)c);

        if (chooseAll)
            catMenu.Add('A', "Auto-select every item", "auto");
        if (hasBlessedKnown) catMenu.Add('B', "Items known to be Blessed", "blessed");
        if (hasCursedKnown) catMenu.Add('C', "Items known to be Cursed", "cursed");
        if (hasUncursedKnown) catMenu.Add('U', "Items known to be Uncursed", "uncursed");
        if (hasBucUnknown) catMenu.Add('X', "Items of unknown B/C/U status", "unknown");

        List<object> picked = catMenu.Display(MenuMode.PickAny);
        if (picked.Count == 0) return null;

        if (picked.Contains("auto"))
            return [.. source];

        bool allTypes = picked.Contains("all");
        HashSet<char> cats = picked.OfType<char>().ToHashSet();
        bool filterBlessed = picked.Contains("blessed");
        bool filterCursed = picked.Contains("cursed");
        bool filterUncursed = picked.Contains("uncursed");
        bool filterUnknown = picked.Contains("unknown");
        bool anyBuc = filterBlessed || filterCursed || filterUncursed || filterUnknown;

        return source.Where(i =>
        {
            bool classMatch = allTypes || cats.Contains(i.Def.Class);
            if (!anyBuc) return classMatch;
            bool bucKnown = i.Knowledge.HasFlag(ItemKnowledge.BUC);
            bool bucMatch = (filterBlessed && bucKnown && i.BUC == BUC.Blessed)
                         || (filterCursed && bucKnown && i.BUC == BUC.Cursed)
                         || (filterUncursed && bucKnown && i.BUC == BUC.Uncursed)
                         || (filterUnknown && !bucKnown);
            return classMatch || bucMatch;
        }).ToList();
    }

    static bool TipOver(Item container, State state)
    {
        if (state.Contents.Count == 0) return false;
        g.pline($"You tip over {container:the}.");
        foreach (var item in state.Contents)
            lvl.PlaceItem(item, upos);
        state.Contents.Clear();
        return true;
    }

    static bool TakeOut(Item container, State state)
    {
        if (state.Contents.Count == 0)
        {
            g.pline($"{container:The} is empty.");
            return false;
        }

        List<Item>? items = PickCategories("Take out", state.Contents, chooseAll: true);
        if (items == null) return false;

        // Item select
        Menu<Item> itemMenu = new();
        itemMenu.Add("Take out what?", LineStyle.Heading);
        char let = 'a';
        foreach (var item in items.OrderBy(i => ItemClasses.Order.IndexOf(i.Def.Class)))
            itemMenu.Add(let++, item.DisplayNameWeighted, item, item.Def.Class);

        List<Item> toTake = itemMenu.Display(MenuMode.PickAny);
        if (toTake.Count == 0) return false;

        foreach (var item in toTake)
        {
            state.Contents.Remove(item);
            u.Inventory.Add(item);
            g.pline($"{item.InvLet} - {item.DisplayNameWeighted}.");
        }
        return true;
    }

    static bool PutIn(Item container, State state)
    {
        List<Item>? items = PickCategories("Put in", u.Inventory, chooseAll: false);
        if (items == null) return false;

        // Item select
        Menu<Item> itemMenu = new();
        itemMenu.Add("Put in what?", LineStyle.Heading);
        foreach (var item in items.OrderBy(i => ItemClasses.Order.IndexOf(i.Def.Class)).ThenBy(i => i.InvLet))
        {
            if (item == container)
                continue;
            itemMenu.Add(item.InvLet, item.DisplayNameWeighted, item, item.Def.Class);
        }

        List<Item> toPut = itemMenu.Display(MenuMode.PickAny);
        if (toPut.Count == 0) return false;

        bool acted = false;
        foreach (var item in toPut)
        {
            if (u.Equipped.ContainsValue(item))
            {
                g.pline($"You are wearing {item:the}.");
                continue;
            }
            u.Inventory.Remove(item);
            state.Contents.Add(item);
            g.pline($"You put {item:the} into {container:the}.");
            acted = true;
        }
        return acted;
    }

    static State X(Fact fact) => (State)fact.Data!;

    internal static void AddItemTo(Fact? inv, Item? item)
    {
        if (item == null) return;
        if (inv == null) return;

        X(inv).Contents.Add(item);
    }
}

[GenerateAll("All", typeof(ItemDef))]
public static partial class Containers
{
    public static readonly ItemDef Chest = new()
    {
        id = "chest",
        Name = "chest",
        Glyph = new(ItemClasses.Tool, ConsoleColor.DarkYellow),
        Weight = 250,
        Price = 16,
        Components = [ContainerBrick.Instance],
    };

    public static readonly ItemDef LargeBox = new()
    {
        id = "large_box",
        Name = "large box",
        Glyph = new(ItemClasses.Tool, ConsoleColor.DarkYellow),
        Weight = 175,
        Price = 8,
        Components = [ContainerBrick.Instance],
    };

    public static readonly ItemDef Sack = new()
    {
        id = "sack",
        Name = "sack",
        Glyph = new(ItemClasses.Tool, ConsoleColor.DarkYellow),
        Weight = 15,
        Price = 2,
        Components = [ContainerBrick.Instance],
    };
}
