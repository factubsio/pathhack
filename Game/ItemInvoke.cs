namespace Pathhack.Game;

public static class ItemInvoke
{
    public static void Invoke(Item item)
    {
        bool handled = true;
        switch (item.Def.id)
        {
            case "everflame":
                InvokeEverflame(item);
                break;
            default:
                g.pline("Nothing happens.");
                handled = false;
                break;
        }
        if (handled)
            u.Energy -= ActionCosts.OneAction.Value;
    }

    static void InvokeEverflame(Item item)
    {
        var feature = lvl.GetState(upos)?.Feature;
        if (lvl.Branch.Name != "Dungeon" || lvl.Depth != g.Branches["dungeon"].MaxDepth)
        {
            g.pline("The Everflame seems inert, perhaps this is a mcguffin return situation?");
        }
        else
        {
            if (feature?.Id != "shrine")
            {
                g.pline("The Everflame flickers but nothing happens.");
                return;
            }
            g.pline("You raise the Everflame before the shrine. Light floods the chamber!");
            g.Done("Won");
        }
    }
}
