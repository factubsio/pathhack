namespace Pathhack.UI;

public static partial class Input
{
    static char? _sellResponse;

    static char? Ynaq(string prompt)
    {
        g.pline($"{prompt} [ynaq]");
        while (true)
        {
            var key = NextKey();
            if (key.KeyChar is 'y' or 'Y') return 'y';
            if (key.KeyChar is 'n' or 'N') return 'n';
            if (key.KeyChar is 'a' or 'A') return 'a';
            if (key.KeyChar is 'q' or 'Q') return 'q';
            if (key.Key == ConsoleKey.Escape) return 'q';
        }
    }

    static void PayShopkeeper()
    {
        var room = lvl.RoomAt(upos);
        if (room?.Type != RoomType.Shop || room.Resident == null)
        {
            g.pline("There is no shopkeeper here.");
            return;
        }
        var shop = room.Resident.FindFact(ShopkeeperBrick.Instance)?.As<ShopState>();
        if (shop == null) return;

        if (shop.Bill <= 0)
        {
            g.pline($"You do not owe {room.Resident:the} anything.");
            return;
        }
        if (u.Gold <= 0)
        {
            g.pline("You have no money.");
            return;
        }

        var unpaid = shop.UnpaidItems();
        bool itemize = unpaid.Count > 1 ? YesNo("Itemized billing?") : true;

        if (itemize)
        {
            foreach (var item in unpaid)
            {
                if (u.Gold <= 0) { g.pline("You have no money left."); break; }
                if (!YesNo($"{item:The,noprice} for {item.Price.Crests()}. Pay?")) continue;
                if (u.Gold < item.Price)
                {
                    g.pline($"You don't have enough to pay for {item:the,noprice}.");
                    continue;
                }
                int price = item.Price;
                u.Gold -= price;
                shop.Pay(price);
                item.Unpaid = false;
                item.UnitPrice = 0;
                shop.Stock[item].Unpaid = false;
                g.pline($"You bought {item:the} for {price.Crests()}.");
            }
        }
        else
        {
            if (u.Gold < shop.Bill)
            {
                g.pline($"You don't have enough to pay the bill of {shop.Bill.Crests()}.");
                return;
            }
            int toPay = shop.Bill;
            u.Gold -= toPay;
            shop.Pay(toPay);
            foreach (var item in unpaid)
            {
                item.Unpaid = false;
                item.UnitPrice = 0;
                shop.Stock[item].Unpaid = false;
            }
            g.pline($"You pay {toPay.Crests()}.");
        }

        if (shop.Bill <= 0)
            g.pline($"{room.Resident:The} says: \"Thank you for shopping!\"");
    }

    static void OpenDoor(CommandArg arg)
    {
        if (arg is not DirArg(var d)) return;
        Pos target = upos + d;
        DoOpenDoor(target);
    }

    static void Chat(CommandArg arg)
    {
        if (arg is not DirArg(var d)) return;
        Pos target = upos + d;
        if (lvl.UnitAt(target) is Monster m)
        {
            if (m.Def.OnChat != null)
                m.Def.OnChat(m);
            else
                g.pline($"{m:The} has nothing to say.");
        }
        else
        {
            g.pline("There is no one there.");
        }
    }

    static void ShowInventory()
    {
        var menu = new Menu<Item>();
        if (!u.Inventory.Any())
        {
            menu.Add("You have nothing.");
            menu.Display();
            return;
        }
        
        int weight = u.Inventory.Sum(i => i.Def.Weight);
        int maxWeight = 500; // TODO: calc from str
        int slots = u.Inventory.Count();
        int maxSlots = 52;
        menu.Add($"Inventory: {weight}/{maxWeight} weight ({slots}/{maxSlots} slots)", LineStyle.Heading);
        BuildItemList(menu, u.Inventory, u);
        
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        
        // TODO: add menu here later (option based?)
        Pokedex.ShowItemEntry(picked[0]);
    }

    static void BuildItemList(Menu<Item> menu, IEnumerable<Item> items, IUnit? unit = null, bool useInvLet = true)
    {
        var sorted = items
            .OrderBy(i => ItemClasses.Order.IndexOf(i.Def.Class))
            .ThenBy(i => i.InvLet);
        

        char? lastClass = null;
        char autoLet = 'a';
        var canSee = unit?.Allows("can_see") == true && unit.IsPlayer;
        foreach (var item in sorted)
        {
            if (!item.Knowledge.HasFlag(ItemKnowledge.Seen) && canSee)
            {
                item.Knowledge |= ItemKnowledge.Seen;
            }

            if (item.Def.Class != lastClass)
            {
                lastClass = item.Def.Class;
                menu.Add(ClassDisplayName(lastClass.Value), LineStyle.SubHeading);
            }
            char let = useInvLet ? item.InvLet : autoLet++;
            string name = item.DisplayName;
            var equippedKv = unit?.Equipped.FirstOrDefault(kv => kv.Value == item);
            if (equippedKv is { Key: var slot } && slot != default)
                name += " " + EquipDescription(item, slot);
            if (item == u.Quiver)
                name += " (quivered)";
            menu.Add(let, name, item, item.Def.Class);
        }
    }

    static string ClassDisplayName(char c) => c switch
    {
        ItemClasses.Weapon => "Weapons",
        ItemClasses.Armor => "Armor",
        ItemClasses.Food => "Comestibles",
        ItemClasses.Potion => "Potions",
        ItemClasses.Scroll => "Scrolls",
        ItemClasses.Spellbook => "Spellbooks",
        ItemClasses.Wand => "Wands",
        ItemClasses.Ring => "Rings",
        ItemClasses.Amulet => "Amulets",
        ItemClasses.Tool => "Tools",
        ItemClasses.Gem => "Gems",
        ItemClasses.Gold => "Coins",
        _ => "Other",
    };

    static void DropItem()
    {
        if (!u.Inventory.Any()) return;
        var menu = new Menu<Item>();
        menu.Add("Drop what?", LineStyle.Heading);
        BuildItemList(menu, u.Inventory);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        _sellResponse = null;
        g.pline($"You drop {picked[0].InvLet} - {picked[0]}.");
        DoDrop(u, picked[0]);
        TrySellToShop(picked[0]);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void DropItems()
    {
        if (!u.Inventory.Any()) return;
        var menu = new Menu<Item>();
        menu.Add("Drop what?", LineStyle.Heading);
        BuildItemList(menu, u.Inventory);
        var toDrop = menu.Display(MenuMode.PickAny);
        if (toDrop.Count == 0) return;
        _sellResponse = null;
        g.pline("You drop {0} item{1}.", toDrop.Count, toDrop.Count == 1 ? "" : "s");
        foreach (var item in toDrop)
        {
            DoDrop(u, item);
            TrySellToShop(item);
        }
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void TrySellToShop(Item item)
    {
        var room = lvl.RoomAt(upos);
        if (room?.Type != RoomType.Shop || room.Resident == null) return;
        var shop = room.Resident.FindFact(ShopkeeperBrick.Instance)?.As<ShopState>();
        if (shop == null) return;
        if (shop.Stock.ContainsKey(item)) return; // already shop item (returned unpaid)

        int offer = shop.SellOffer(item);
        if (offer <= 0)
        {
            g.pline($"{room.Resident:The} seems uninterested.");
            return;
        }

        char response = _sellResponse ?? Ynaq($"{room.Resident:The} offers {offer.Crests()} for {item:the,noprice}. Sell?") ?? 'q';
        if (response == 'a') _sellResponse = 'y';
        if (response == 'q') _sellResponse = 'n';

        if (response is 'y' or 'a')
        {
            u.Gold += offer;
            shop.CompleteSale(item);
            g.pline($"You sell {item:the,noprice} for {offer.Crests()}.");
        }
    }

    static void InvokeItem()
    {
        if (!u.Inventory.Any())
        {
            g.pline("You have nothing to invoke.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Invoke what?", LineStyle.Heading);
        BuildItemList(menu, u.Inventory);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        ItemInvoke.Invoke(picked[0]);
    }

    static void WaitTurn() => u.Energy -= ActionCosts.OneAction.Value;

    static void Fire(CommandArg arg)
    {
        if (arg is not DirArg(var dir)) return;
        if (u.Quiver == null)
        {
            g.pline("You have nothing readied.");
            return;
        }
        Item toThrow;
        if (u.Quiver.Count > 1)
            toThrow = u.Quiver.Split(1);
        else
        {
            toThrow = u.Quiver;
            u.Inventory.Remove(toThrow);
            u.Quiver = null;
        }
    DoThrow(u, toThrow, dir);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void ZapWand()
    {
        var wands = u.Inventory.Where(i => i.Def is WandDef).ToList();
        if (wands.Count == 0)
        {
            g.pline("You have no wands.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Zap which wand?", LineStyle.Heading);
        BuildItemList(menu, wands, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;

        var wand = picked[0];
        if (wand.Charges <= 0)
        {
            g.pline("Nothing happens.");
            u.Energy -= ActionCosts.OneAction.Value;
            return;
        }

        var def = (WandDef)wand.Def;
        if (def.Spell.Targeting == TargetingType.None)
        {
            wand.Charges--;
            Wands.DoEffect(def, u, Pos.Zero);
        }
        else
        {
            g.pline("In what direction?");
            var dir = GetDirection(NextKey().Key);
            if (dir == null) return;

            wand.Charges--;
            Wands.DoEffect(def, u, dir.Value);
        }
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void Throw()
    {
        var throwable = u.Inventory.Where(i => i.Def is PotionDef or BottleDef).ToList();
        if (throwable.Count == 0)
        {
            g.pline("You have nothing to throw.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Throw what?", LineStyle.Heading);
        BuildItemList(menu, throwable, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;

        g.pline("In what direction?");
        var dir = GetDirection(NextKey().Key);
        if (dir == null) return;

        var item = picked[0];
        Item toThrow;
        if (item.Count > 1)
            toThrow = item.Split(1);
        else
        {
            toThrow = item;
            u.Inventory.Remove(toThrow);
        }
        DoThrow(u, toThrow, dir.Value);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void SetQuiver()
    {
        var throwable = u.Inventory.Where(i => i.Def is WeaponDef w && (w.Range > 1 || w.Launcher != null)).ToList();
        if (throwable.Count == 0)
        {
            g.pline("You have nothing to ready.");
            return;
        }
        var menu = new Menu<Item?>();
        menu.Add("Ready what? (- for nothing)", LineStyle.Heading);
        menu.AddHidden('-', null);
        foreach (var item in throwable.OrderBy(i => i.InvLet))
        {
            string name = item.DisplayName;
            if (item == u.Quiver)
                name += " (quivered)";
            menu.Add(item.InvLet, name, item);
        }
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        u.Quiver = picked[0];
        if (u.Quiver != null)
            g.pline("You ready {0:the}.", u.Quiver);
        else
            g.pline("You empty your quiver.");
    }

    static void WieldWeapon()
    {
        var weapons = u.Inventory.Where(i => i.Def is WeaponDef).ToList();
        var menu = new Menu<Item?>();
        menu.Add("Wield what? (- for bare hands)", LineStyle.Heading);
        menu.AddHidden('-', null);
        foreach (var item in weapons.OrderBy(i => ItemClasses.Order.IndexOf(i.Def.Class)).ThenBy(i => i.InvLet))
        {
            string name = item.DisplayName;
            if (u.Equipped.ContainsValue(item))
                name += " (weapon in hand)";
            menu.Add(item.InvLet, name, item);
        }
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        var result = g.DoEquip(u, picked[0]);
        if (result == GameState.EquipResult.Cursed) return;
        if (picked[0] == null)
            g.pline("You are empty handed.");
        else
            g.pline($"{picked[0]!.InvLet} - {picked[0]!.Def.Name} (weapon in hand).");
    }

    static void WearArmor()
    {
        var armors = u.Inventory.Where(i => i.Def is ArmorDef
            || i.Def.DefaultEquipSlot is ItemSlots.Feet or ItemSlots.Hands).ToList();
        if (armors.Count == 0)
        {
            g.pline("You have nothing to wear.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Wear what?", LineStyle.Heading);
        BuildItemList(menu, armors, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        var armor = picked[0];
        var slot = g.DoEquip(u, armor);
        if (slot == GameState.EquipResult.Cursed) return;
        if (slot == GameState.EquipResult.NoSlot)
            g.pline("You can't wear that.");
        else
            g.pline($"{armor.InvLet} - {armor.DisplayName} (being worn).");
    }

    static void PutOnAccessory()
    {
        var accessories = u.Inventory.Where(i => i.Def.Class is ItemClasses.Ring or ItemClasses.Amulet).ToList();
        if (accessories.Count == 0)
        {
            g.pline("You have no accessories.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Put on what?", LineStyle.Heading);
        BuildItemList(menu, accessories, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        var item = picked[0];
        var slot = g.DoEquip(u, item);
        if (slot == GameState.EquipResult.Cursed) return;
        if (slot == GameState.EquipResult.NoSlot)
        {
            g.pline(item.Def.DefaultEquipSlot == ItemSlots.Ring
                ? "You have no free ring fingers."
                : "You are already wearing an amulet.");
        }
        else
            g.pline($"{item.InvLet} - {item.DisplayName} (being worn).");
    }

    static string EquipDescription(Item item, EquipSlot? slot) => slot?.Type switch
    {
        ItemSlots.Ring => $"(worn on {slot.Value.Slot} hand)",
        ItemSlots.Hand when item.Def is WeaponDef { Hands: 2 } => "(weapon in hands)",
        ItemSlots.Hand => "(weapon in hand)",
        _ => "(being worn)",
    };

    static void TakeOff()
    {
        var equipped = u.Equipped.Values.Where(i => i.Def is ArmorDef
            || i.Def.DefaultEquipSlot is ItemSlots.Feet or ItemSlots.Hands).ToList();
        if (equipped.Count == 0)
        {
            g.pline("You have nothing to take off.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Take off what?", LineStyle.Heading);
        BuildItemList(menu, equipped, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        if (g.DoUnequip(u, picked[0]))
            g.pline("You take off {0}.", DoName(picked[0]));
    }

    static void RemoveAccessory()
    {
        var equipped = u.Equipped.Values.Where(i => i.Def.Class is ItemClasses.Ring or ItemClasses.Amulet).ToList();
        if (equipped.Count == 0)
        {
            g.pline("You have no accessories equipped.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Remove what?", LineStyle.Heading);
        BuildItemList(menu, equipped, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        if (g.DoUnequip(u, picked[0]))
            g.pline("You remove {0}.", DoName(picked[0]));
    }

    static void Quaff()
    {
        var potions = u.Inventory.Where(i => i.Def is PotionDef or BottleDef).ToList();
        if (potions.Count == 0)
        {
            g.pline("You have nothing to drink.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Quaff what?", LineStyle.Heading);
        BuildItemList(menu, potions, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;

        var potion = picked[0];
        
        string useType = potion.Def is BottleDef ? "bottle" : "potion";
        g.pline($"You drink {potion.SingleName.An()}.");
        Log.Structured("use", $"{"quaff":action}{potion.Def.Name:item}{useType:type}");
        if (potion.Def is BottleDef bottle)
            Bottles.DoEffect(bottle, u, upos);
        else
            Potions.DoEffect((PotionDef)potion.Def, u);

        u.Inventory.Consume(potion);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void Eat()
    {
        if (u.CurrentActivity != null)
        {
            g.pline("You're already busy.");
            return;
        }
        
        var foods = u.Inventory.Where(i => i.IsFood).ToList();
        var corpses = lvl.ItemsAt(upos).Where(i => i.CorpseOf != null).ToList();
        
        if (foods.Count == 0 && corpses.Count == 0)
        {
            g.pline("You have nothing to eat.");
            return;
        }
        
        // If corpses on ground, offer cooking
        if (corpses.Count > 0)
        {
            Item? corpse = null;
            if (corpses.Count == 1)
                corpse = corpses[0];
            else
            {
                var cmenu = new Menu<Item>();
                cmenu.Add("Cook what?", LineStyle.Heading);
                for (int i = 0; i < corpses.Count; i++)
                    cmenu.Add((char)('a' + i), DoName(corpses[i]), corpses[i]);
                var cpicked = cmenu.Display(MenuMode.PickOne);
                if (cpicked.Count == 0) return;
                corpse = cpicked[0];
            }
            
            var cookMenu = new Menu<char>();
            cookMenu.Add("How to cook?", LineStyle.Heading);
            cookMenu.Add('a', "cook quickly (4 turns, 10% nutrition)", 'a');
            cookMenu.Add('b', "cook carefully (20 turns, 25% nutrition)", 'b');
            var choice = cookMenu.Display(MenuMode.PickOne);
            if (choice.Count == 0) return;
            
            if (choice[0] == 'a')
            {
                g.pline($"You start cooking {DoNameOne(corpse)}.");
                u.HippoCounter++;
                u.CurrentActivity = Activity.CookQuick(corpse);
                lvl.RemoveItem(corpse, upos);
            }
            else
            {
                bool isResuming = corpse.Eaten > 0;
                if (isResuming)
                    g.pline($"You continue cooking {DoNameOne(corpse)}.");
                else
                {
                    corpse.Eaten = 1;
                    g.pline($"You start carefully cooking {DoNameOne(corpse)}.");
                    u.HippoCounter++;
                }
                u.CurrentActivity = Activity.CookCareful(corpse);
            }
            u.Energy -= ActionCosts.OneAction.Value;
            return;
        }
        
        var menu = new Menu<Item>();
        menu.Add("Eat what?", LineStyle.Heading);
        foreach (var item in foods)
            menu.Add(item.InvLet, DoName(item), item);
        var foodPicked = menu.Display(MenuMode.PickOne);
        if (foodPicked.Count == 0) return;

        var food = foodPicked[0];
        
        // Split off one if it's a stack (unless already partially eaten)
        if (food.Count > 1 && food.Eaten == 0)
        {
            food = food.Split(1);
            food.Eaten = -1; // prevent merge back
            u.Inventory.Add(food);
        }
        
        bool canchoke = Hunger.GetState(u.Nutrition) == HungerState.Satiated;
        bool resuming = food.Eaten > 0;
        
        if (resuming)
            g.pline($"You continue eating {DoNameOne(food)}.");
        
        u.CurrentActivity = Activity.Eat(food, canchoke);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void ReadScroll()
    {
        var scrolls = u.Inventory.Where(i => i.Def is ScrollDef).ToList();
        bool blind = !u.Allows("can_see");

        if (blind)
        {
            scrolls = scrolls.Where(i => i.Def.IsKnown()).ToList();
            if (scrolls.Count == 0)
            {
                g.pline("You don't know any of the formulas!");
                return;
            }
        }

        if (scrolls.Count == 0)
        {
            g.pline("You have no scrolls.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Read what?", LineStyle.Heading);
        BuildItemList(menu, scrolls, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;

        var scroll = picked[0];
        var def = (ScrollDef)scroll.Def;

        g.pline(blind
            ? "As you pronounce the formula on it, the scroll disappears."
            : "As you read the scroll, it disappears.");
        u.Inventory.Consume(scroll);
        Log.Structured("use", $"{"read":action}{def.Name:item}{"scroll":type}");
        Scrolls.DoEffect(def, u, PickItemToIdentify, scroll.BUC);

        u.Energy -= ActionCosts.OneAction.Value;
    }

    static Item? PickItemToIdentify()
    {
        const ItemKnowledge full = ItemKnowledge.Seen | ItemKnowledge.Props | ItemKnowledge.BUC;
        var unidentified = u.Inventory
            .Where(i => !i.Def.IsKnown() || (i.Knowledge & full) != full)
            .ToList();
        if (unidentified.Count == 0)
        {
            g.pline("You have nothing to identify.");
            return null;
        }
        var menu = new Menu<Item>();
        menu.Add("Identify what?", LineStyle.Heading);
        BuildItemList(menu, unidentified, u);
        var picked = menu.Display(MenuMode.PickOne);
        return picked.Count > 0 ? picked[0] : null;
    }

    static void ShowDiscoveries()
    {
        var menu = new Menu();
        menu.Add("Discoveries", LineStyle.Heading);
        
        bool any = false;
        foreach (var (cat, label) in new[] { 
            (AppearanceCategory.Potion, "Potions"),
            (AppearanceCategory.Bottle, "Bottles"),
            (AppearanceCategory.Wand, "Wands"),
            (AppearanceCategory.Scroll, "Scrolls"),
            (AppearanceCategory.Amulet, "Amulets"),
            (AppearanceCategory.Boots, "Boots"),
            (AppearanceCategory.Gloves, "Gloves"),
            (AppearanceCategory.Cloak, "Cloaks"),
        })
        {
            var identified = GetIdentifiedInCategory(cat);
            if (identified.Count == 0) continue;
            
            any = true;
            menu.Add(label, LineStyle.SubHeading);
            foreach (var def in identified)
            {
                var app = ItemDb.Instance.GetAppearance(def);
                menu.Add($"  {def.Name} ({app?.Name})");
            }
        }
        
        if (!any)
            menu.Add("You haven't discovered anything yet.");
        
        menu.Display();
    }

    static List<ItemDef> GetIdentifiedInCategory(AppearanceCategory cat)
    {
        List<ItemDef> result = [];
        var all = cat switch
        {
            AppearanceCategory.Potion => Potions.All.Cast<ItemDef>(),
            AppearanceCategory.Bottle => Bottles.All.Cast<ItemDef>(),
            AppearanceCategory.Wand => Wands.All.Cast<ItemDef>(),
            AppearanceCategory.Scroll => Scrolls.All.Cast<ItemDef>(),
            _ => []
        };
        foreach (var def in all.Where(d => d.IsKnown()))
            result.Add(def);
        return result;
    }

    static void CallItem()
    {
        var menu = new Menu<char>();
        menu.Add("What do you wish to do?", LineStyle.Heading);
        menu.Add('a', "Name a monster", 'a');
        menu.Add('b', "Name an individual item", 'b');
        menu.Add('c', "Name all items of a certain type", 'c');
        menu.Add('d', "View discoveries", 'd');
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;

        switch (picked[0])
        {
            case 'a':
                g.pline("Not implemented yet.");
                break;
            case 'b':
                g.pline("Not implemented yet.");
                break;
            case 'c':
                CallItemType();
                break;
            case 'd':
                ShowDiscoveries();
                break;
        }
    }

    static void CallItemType()
    {
        // Pick an unidentified item from inventory to name its type
        var nameable = u.Inventory
            .Where(i => i.Def.AppearanceCategory != null && !ItemDb.Instance.IsIdentified(i.Def))
            .ToList();
        
        if (nameable.Count == 0)
        {
            g.pline("You have nothing to name.");
            return;
        }

        var menu = new Menu<Item>();
        menu.Add("Name what?", LineStyle.Heading);
        BuildItemList(menu, nameable, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;

        var item = picked[0];
        var name = PromptLine($"Call {item:an}");
        
        ItemDb.Instance.SetCalledName(item.Def, name);
        
        if (string.IsNullOrWhiteSpace(name))
            g.pline("Name removed.");
        else
            g.pline("Noted.");
    }
}
