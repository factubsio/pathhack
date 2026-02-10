namespace Pathhack.Game;

public class ShopkeeperBrain : MonsterBrain
{
    public override bool DoTurn(Monster m)
    {
        // ANGRY
        if (!m.Peaceful) return false;

        var fact = m.FindFact(ShopkeeperBrick.Instance);

        if (fact?.Data is not ShopState shop) return false;

        Pos goal = shop.Bill > 0 && upos.ChebyshevDist(shop.Door) <= 2 
            ? shop.Block 
            : shop.Home;

        if (m.Pos == goal)
        {
            m.Energy = 0;
            return true;
        }

        Pos? best = null;
        int bestDist = m.Pos.ChebyshevDist(goal);
        foreach (var dir in Pos.AllDirs)
        {
            Pos next = m.Pos + dir;
            if (!lvl.CanMoveTo(m.Pos, next, m)) continue;
            if (lvl.UnitAt(next) != null) continue;
            int dist = next.ChebyshevDist(goal);
            if (dist < bestDist) { best = next; bestDist = dist; }
        }

        if (best is { } target)
        {
            lvl.MoveUnit(m, target);
            return true;
        }
        return false;
    }
}

public class ShopItem
{
    public bool Unpaid = false;
    public int PricedAt = -500;
}

public class ShopState
{
    public Pos Block;
    public Pos Door;
    public Pos Home;

    public Monster Shopkeeper = null!;
    public Room Room = null!;

    public Dictionary<Item, ShopItem> Stock = [];
    public int Bill;

    public static double GetMaterialPriceMod(string material) => material switch
    {
        _ => 1,
    };

    public static int GetQualityPrice(int quality) => quality switch
    {
        1 => 500,
        2 => 1500,
        3 => 4500,
        4 => 13500,
        _ => 0,
    };

    public static int GetSellPrice(Item item)
    {
        var price = item.Def.Price;
        price += (int)(GetQualityPrice(item.Potency) * 0.4);
        foreach (var rune in item.PropertyRunes)
        {
            price += (int)(GetQualityPrice(rune.Def.Quality) * 0.4);
        }
        price = (int)(price * GetMaterialPriceMod(item.Material));
        return price;
    }

    public int Take(Item item)
    {
        if (Stock.TryGetValue(item, out var state) && !state.Unpaid)
        {
            state.Unpaid = true;
            item.Unpaid = true;
            DoPrice(item, state);
            Bill += item.Price;
            Log.Write($"took {item}, bill -> {Bill}");
            return item.UnitPrice;
        }
        return 0;
    }

    public int Give(Item item)
    {
        Log.Write($"giving {item} to shop");
        if (Stock.TryGetValue(item, out var state) && state.Unpaid)
        {
            state.Unpaid = false;
            item.Unpaid = false;
            Bill -= item.Price;
            Log.Write($"dropped {item}, bill -> {Bill}");
            return Bill;
        }
        return -1;
    }

    public int SellOffer(Item item)
    {
        if (item.Stolen) return 0;
        if (item.Def.Price <= 0) return 0;
        return GetSellPrice(item) / 2;
    }

    public void CompleteSale(Item item)
    {
        var state = new ShopItem();
        Stock[item] = state;
        DoPrice(item, state);
    }

    public int Pay(int amount)
    {
        int toPay = Math.Min(amount, Bill);
        Bill -= toPay;
        return toPay;
    }

    public List<Item> UnpaidItems() => 
        Stock.Where(kv => kv.Value.Unpaid).Select(kv => kv.Key).ToList();

    private void DoPrice(Item item, ShopItem state)
    {
        if (g.CurrentRound - state.PricedAt > 400)
        {
            state.PricedAt = g.CurrentRound;
            item.UnitPrice = GetSellPrice(item);
        }
    }

    public void DoPrice(Item item)
    {
        if (Stock.TryGetValue(item, out var state))
            DoPrice(item, state);
    }
}

public class ShopkeeperBrick : LogicBrick<ShopState>
{
    internal static readonly ShopkeeperBrick Instance = new();

    protected override void OnDeath(Fact fact, PHContext context)
    {
        var state = X(fact);
        state.Room.Resident = null;
        foreach (var item in state.Stock.Keys)
        {
            if (item.Unpaid || u.Deity.Alignment[0] == 'L')
            {
                item.Stolen = true;
                item.Unpaid = false;
            }
            item.UnitPrice = 0;
        }
    }
}


public static class EconomySystem
{
    public static readonly MonsterDef Shopkeeper = new()
    {
        Name = "shopkeeper",
        Family = "human",
        CreatureType = CreatureTypes.Humanoid,
        Glyph = new('@', ConsoleColor.White),
        HpPerLevel = 15,
        AC = 0,
        AttackBonus = 1,
        DamageBonus = 0,
        Unarmed = NaturalWeapons.Fist,
        Size = UnitSize.Medium,
        BaseLevel = 8,
        SpawnWeight = 0,
        Peaceful = true,
        MoralAxis = MoralAxis.Neutral,
        EthicalAxis = EthicalAxis.Lawful,
        Brain = new ShopkeeperBrain(),
        Components = [
            EquipSet.OneOf(MundaneArmory.Longsword, MundaneArmory.Falchion),
            new Equip(MundaneArmory.Breastplate),
            new GrantAction(AttackWithWeapon.Instance),
        ]
    };

    public static string Crests(this int amount) => amount == 1 ? $"one {Coin}" : $"{amount} {Coins}";
    public const string Coin = "crest";
    public const string Coins = "crests";
}
