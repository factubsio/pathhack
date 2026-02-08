namespace Pathhack.UI;

public static partial class Input
{
    static void OpenDoor(CommandArg arg)
    {
        if (arg is not DirArg(var d)) return;
        Pos target = upos + d;
        if (lvl.InBounds(target) && lvl[target].Type == TileType.Door)
        {
            if (lvl.OpenDoor(target))
            {
                u.Energy -= ActionCosts.OneAction.Value;
            }
            else
            {
                if (lvl.IsDoorOpen(target))
                    g.pline("It's already open.");
                else if (lvl.IsDoorBroken(target))
                    g.pline("It's already broken beyond repair.");
                else if (lvl.IsDoorLocked(target))
                    g.pline("It's locked.");
            }
        }
        else
        {
            g.pline("There is no door there.");
        }
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
        foreach (var item in sorted)
        {
            if (item.Def.Class != lastClass)
            {
                lastClass = item.Def.Class;
                menu.Add(ClassDisplayName(lastClass.Value), LineStyle.SubHeading);
            }
            char let = useInvLet ? item.InvLet : autoLet++;
            string name = item.DisplayName;
            if (unit?.Equipped.ContainsValue(item) == true)
                name += item.Def is ArmorDef ? " (being worn)" : " (weapon in hand)";
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
        g.DoDrop(u, picked[0]);
        g.pline($"You drop {picked[0].InvLet} - {picked[0]}.");
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
        foreach (var item in toDrop)
            g.DoDrop(u, item);
        g.pline("You drop {0} item{1}.", toDrop.Count, toDrop.Count == 1 ? "" : "s");
        u.Energy -= ActionCosts.OneAction.Value;
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
        g.DoThrow(u, toThrow, dir);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void SetQuiver()
    {
        var throwable = u.Inventory.Where(i => i.Def is WeaponDef w && w.Range > 1).ToList();
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
        g.DoEquip(u, picked[0]);
        if (picked[0] == null)
            g.pline("You are empty handed.");
        else
            g.pline("{0} - {1} (weapon in hand).", picked[0]!.InvLet, picked[0]!.Def.Name);
    }

    static void WearArmor()
    {
        var armors = u.Inventory.Where(i => i.Def is ArmorDef).ToList();
        if (armors.Count == 0)
        {
            g.pline("You have no armor.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Wear what?", LineStyle.Heading);
        BuildItemList(menu, armors, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        var armor = picked[0];
        g.DoEquip(u, armor);
        g.pline("{0} - {1} (being worn).", armor.InvLet, armor.Def.Name);
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
        g.DoEquip(u, item);
        g.pline("{0} - {1} (being worn).", item.InvLet, item.DisplayName);
    }

    static void TakeOff()
    {
        var equipped = u.Equipped.Values.Where(i => i.Def is ArmorDef).ToList();
        if (equipped.Count == 0)
        {
            g.pline("You have no armor equipped.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Take off what?", LineStyle.Heading);
        BuildItemList(menu, equipped, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        g.DoUnequip(u, picked[0]);
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
        g.DoUnequip(u, picked[0]);
        g.pline("You remove {0}.", DoName(picked[0]));
    }

    static void Quaff()
    {
        var potions = u.Inventory.Where(i => i.Def is PotionDef).ToList();
        if (potions.Count == 0)
        {
            g.pline("You have no potions.");
            return;
        }
        var menu = new Menu<Item>();
        menu.Add("Quaff what?", LineStyle.Heading);
        BuildItemList(menu, potions, u);
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;

        var potion = picked[0];
        var def = (PotionDef)potion.Def;
        
        g.pline($"You drink {potion.SingleName.An()}.");
        Potions.DoEffect(def, u);

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
        if (!u.Can("can_see"))
        {
            g.pline("You can't read while blind!");
            return;
        }

        var scrolls = u.Inventory.Where(i => i.Def is ScrollDef).ToList();
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

        g.pline("As you read the scroll, it disappears.");
        u.Inventory.Consume(scroll);
        Scrolls.DoEffect(def, u, PickItemToIdentify);

        u.Energy -= ActionCosts.OneAction.Value;
    }

    static Item? PickItemToIdentify()
    {
        var unidentified = u.Inventory
            .Where(i => i.Def.AppearanceCategory != null && !ItemDb.Instance.IsIdentified(i.Def))
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
            AppearanceCategory.Scroll => Scrolls.All.Cast<ItemDef>(),
            _ => []
        };
        foreach (var def in all)
            if (ItemDb.Instance.IsIdentified(def))
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
