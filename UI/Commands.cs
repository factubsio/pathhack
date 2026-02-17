using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Pathhack.UI;

public static partial class Input
{
    static char? _sellResponse;

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

    static string CompressRuns(ReadOnlySpan<char> chars)
    {
        if (chars.Length == 0) return "";
        StringBuilder sb = new();
        int i = 0;
        while (i < chars.Length)
        {
            int run = 1;
            while (i + run < chars.Length && chars[i + run] == chars[i] + run)
                run++;
            if (run > 3)
            {
                sb.Append(chars[i]);
                sb.Append('-');
                sb.Append(chars[i + run - 1]);
            }
            else
            {
                for (int j = 0; j < run; j++)
                    sb.Append(chars[i + j]);
            }
            i += run;
        }
        return sb.ToString();
    }


    static bool PickItem(string verb, Func<Item, bool> filter, [NotNullWhen(true)] out Item? item)
    {
        string prompt = $"What do you want to {verb}?";
        string ifNone = $"You don't have anything to {verb}.";
        string ifWrong = $"That's a silly thing to {verb}";
        var valid = u.Inventory.Where(filter).ToList();
        item = null;
        if (valid.Count == 0)
        {
            g.pline(ifNone);
            return false;
        }

        var letters = CompressRuns(valid.Select(i => i.InvLet).OrderBy(c => c).ToArray());
        var fullPrompt = $"{prompt} [{letters} or ?*]";

        while (true)
        {
            g.pline(fullPrompt);
            var key = NextKey();
            Draw.ClearTopLine();

            if (key.Key == ConsoleKey.Escape)
            {
                return false;
            }
            if (key.KeyChar == '?')
            {
                var menu = new Menu<Item>();
                menu.Add(prompt, LineStyle.Heading);
                BuildItemList(menu, valid, u);
                item = menu.Display(MenuMode.PickOne).FirstOrDefault();
                break;
            }
            else if (key.KeyChar == '*')
            {
                var menu = new Menu<Item>();
                menu.Add(prompt, LineStyle.Heading);
                BuildItemList(menu, u.Inventory, u);
                item = menu.Display(MenuMode.PickOne).FirstOrDefault();
                break;
            }
            else
            {
                if (!u.Inventory.TryGet(key.KeyChar, out item))
                {
                    Draw.ClearTopLine();
                    const string notExist = "You don't have that object.";
                    g.pline(notExist);
                    Draw.More(false, notExist.Length, 0);
                }
                else
                {
                    break;
                }
            }
        }
        Draw.ClearTopLine();

        if (item != null)
        {
            if (filter(item)) return true;
            g.pline(ifWrong);
        }

        return false;
    }

    static void BuildItemList(Menu<Item> menu, IEnumerable<Item> items, IUnit? unit = null, bool useInvLet = true)
    {
        var sorted = items
            .OrderBy(i => ItemClasses.Order.IndexOf(i.Def.Class))
            .ThenBy(i => i.InvLet);
        

        char? lastClass = null;
        char autoLet = 'a';
        var canSee = unit?.IsPlayer == true && unit.CanSee;
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
        if (!PickItem("drop", _ => true, out var item)) return;
        _sellResponse = null;
        g.pline($"You drop {item.InvLet} - {item}.");
        DoDrop(u, item);
        TrySellToShop(item);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void DropItems()
    {
        if (u.Inventory.Count == 0) return;
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

    static void Fire()
    {
        if (u.Quiver == null)
        {
            g.pline("You have nothing readied.");
            return;
        }
        Item toThrow;
        int? range = null;

        AttackType type = AttackType.Thrown;

        var weapon = u.GetWieldedItem();
        var quiver = u.Quiver.Def as QuiverDef;
        if (quiver != null && quiver.WeaponProficiency != (weapon.Def as WeaponDef)?.Profiency)
        {
            g.pline($"That is a silly way to fire {u.Quiver:an}.");
            return;
        }
        if (!PromptDirection(out var dir)) return;

        if (quiver != null)
        {
            if (u.Quiver.Charges == 0)
            {
                g.pline($"Your {u.Quiver} is empty!");
                return;
            }
            ArcherySystem.ShootFrom(u, u.Quiver, dir);
            u.Energy -= ActionCosts.OneAction.Value;
            return;
        }
        else
        {
            if (u.Quiver.Count > 1)
                toThrow = u.Quiver.Split(1);
            else
            {
                toThrow = u.Quiver;
                u.Inventory.Remove(toThrow);
                u.Quiver = null;
            }
        }

        DoThrow(u, toThrow, dir, type, range: range);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void ZapWand()
    {
        if (!PickItem("zap", IsZappable, out var wand)) return;

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
            if (!PromptDirection(out var dir)) return;

            wand.Charges--;
            Wands.DoEffect(def, u, dir);
        }
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void Throw()
    {
        // TODO: throw things that aren't potions!!!
        if (!PickItem("throw", IsQuaffable, out var item)) return;

        if (!PromptDirection(out var dir)) return;

        Item toThrow;
        if (item.Count > 1)
            toThrow = item.Split(1);
        else
        {
            toThrow = item;
            u.Inventory.Remove(toThrow);
        }
        DoThrow(u, toThrow, dir, AttackType.Thrown);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void SetQuiver()
    {
        if (!PickItem("ready", i => i.Def is QuiverDef || i.Def is WeaponDef { Launcher: not null }, out var item)) return;

        if (u.Quiver == item)
        {
            g.pline("Readying an already readied item is readtacular but pointless.");
            return;
        }

        u.Quiver = item;
        if (u.Quiver != null)
        {

            if (u.Quiver.Def is QuiverDef q)
            {
                // Punish scumming quiver swaps.

                g.pline("You ready {0:the}.", u.Quiver);
                // Note: it's a bid odd cos you "ready (5), but some splip out",
                // which means you actually have (3), I am not sure which order is better
                int lost = g.Rn2(g.Rn2(u.Quiver.Charges));
                if (lost > 0)
                {
                    u.Quiver.Charges -= lost;
                    if (lost > 2)
                    {
                        g.pline($"But a few {q.Ammo.Name.Plural()} slip out!");
                    }
                    else if(lost == 2)
                    {
                        g.pline($"But a couple of {q.Ammo.Name.Plural()} slip out!");
                    }
                    else
                    {
                        g.pline($"But {q.Ammo:an} slips out!");
                    }
                }

                u.Energy -= ActionCosts.OneAction.Value;
            }
        }
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
                name += $" (weapon in {HandStr(item)})";
            menu.Add(item.InvLet, name, item);
        }
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        var result = g.DoEquip(u, picked[0]);
        if (result == EquipResult.Cursed) return;
        if (picked[0] == null)
            g.pline("You are empty handed.");
        else
            g.pline($"{picked[0]!.InvLet} - {picked[0]!.Def.Name} (weapon in {HandStr(picked[0]!)}).");
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

    public static string EquipDescription(Item item, EquipSlot? slot) => slot?.Type switch
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

    // Like GetDirection but supports </>, slightly odd?
    public static Pos? PickDirection()
    {
        var key = NextKey();
        if (key.KeyChar == '>') return Pos.Down;
        if (key.KeyChar == '<') return Pos.Up;
        return GetDirection(key.Key);
    }

    static void Apply()
    {
        if (!PickItem("use or apply", IsApplyable, out var item)) return;

        LogicBrick.FireOnVerb(item, ItemVerb.Apply);
    }

    static void Quaff()
    {
        if (!PickItem("drink", IsQuaffable, out var potion))
        {
            return;
        }

        if (potion.Def is BottleDef or PotionDef)
        {
            string useType = potion.Def is BottleDef ? "bottle" : "potion";
            g.pline($"You drink {potion.SingleName.An()}.");
            Log.Structured("use", $"{"quaff":action}{potion.Def.Name:item}{useType:type}");
            if (potion.Def is BottleDef bottle)
                Bottles.DoEffect(bottle, u, upos);
            else
                Potions.DoEffect((PotionDef)potion.Def, u);
        }
        else
        {
            LogicBrick.FireOnVerb(potion, ItemVerb.Quaff);
        }

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
        
        var corpses = lvl.ItemsAt(upos).Where(i => i.CorpseOf != null).ToList();
        
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
                u.CurrentActivity = new CookQuickActivity(corpse);
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
                u.CurrentActivity = new CookCarefulActivity(corpse);
            }
            u.Energy -= ActionCosts.OneAction.Value;
            return;
        }
        
        if (!PickItem("eat", IsEdible, out var food)) return;
        
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
        
        u.CurrentActivity = new EatActivity(food, canchoke);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    static void ReadScroll()
    {
        bool blind = !u.CanSee;

        Func<Item, bool> filter = IsReadable;
        if (blind)
        {
            filter = item => IsReadable(item) && item.Def.IsKnown();
        }

        if (!PickItem("read", filter, out var item))
        {
            return;
        }

        // FIXME: not always a scroll
        Log.Structured("use", $"{"read":action}{item.Def.Name:item}{"scroll":type}");

        if (item.Def is ScrollDef scroll)
        {
            g.pline(blind
                ? "As you pronounce the formula on it, the scroll disappears."
                : "As you read the scroll, it disappears.");
            u.Inventory.Consume(item);
            Scrolls.DoEffect(scroll, u, PickItemToIdentify, item.BUC);
        }

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

    // Note: IsSubjectOf is null safe
    static bool IsReadable(Item item) => (item.Def is ScrollDef or BookDef) || item.FindBrickOfType<VerbResponder>().IsSubjectOf(ItemVerb.Read);
    static bool IsQuaffable(Item item) => (item.Def is PotionDef or BottleDef) || item.FindBrickOfType<VerbResponder>().IsSubjectOf(ItemVerb.Quaff);
    static bool IsZappable(Item item) => item.Def is WandDef || item.FindBrickOfType<VerbResponder>().IsSubjectOf(ItemVerb.Zap);
    // Eatable???
    static bool IsEdible(Item item) => item.Def is ConsumableDef || item.FindBrickOfType<VerbResponder>().IsSubjectOf(ItemVerb.Eat);

    static bool IsApplyable(Item item) => item.FindBrickOfType<VerbResponder>().IsSubjectOf(ItemVerb.Apply);

    static void ZapSpell()
    {
        if (!u.Allows("can_speak"))
        {
            g.pline($"You are currently silenced!");
            return;
        }

        if (u.Spells.Count == 0)
        {
            g.pline("You don't know any spells right now.");
            return;
        }
        var menu = new Menu<ActionBrick>();
        menu.Add("Choose which spell to cast", LineStyle.Heading);
        char let = 'a';
        foreach (var level in u.Spells.GroupBy(s => s.Level))
        {
            menu.Add($"Level {level.Key}:", LineStyle.SubHeading);
            foreach (var spell in level)
            {
                var data = u.ActionData.GetValueOrDefault(spell);
                var spellPlan = spell.CanExecute(u, data, Target.None);
                string status = spellPlan ? "" : $" ({spellPlan.WhyNot})";
                menu.Add(let++, spell.Name + status, spell);
            }
        }
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        Log.Structured("cast", $"{picked[0].Name:spell}{picked[0].Targeting.ToString():targeting}");
        ResolveTargetAndExecute(picked[0]);
    }

    static void ShowAbilities()
    {
        if (u.Actions.Count == 0)
        {
            g.pline("You have no abilities.");
            return;
        }
        var menu = new Menu<ActionBrick>();
        menu.Add("Use which ability?", LineStyle.Heading);
        char let = 'a';
        foreach (var action in u.Actions.Distinct())
        {
            var data = u.ActionData.GetValueOrDefault(action);
            var result = action.CanExecute(u, data, Target.None);
            bool ready = result;
            string whyNot = result.WhyNot;
            var toggle = action.IsToggleOn(data);
            string status = toggle switch
            {
              ToggleState.NotAToggle => "",
              ToggleState.Off => " [off]",
              ToggleState.On => " [on]",
              _ => "???",
            };
            status += ready ? "" : $" ({whyNot})";
            menu.Add(let++, action.Name + status, action);
        }
        var picked = menu.Display(MenuMode.PickOne);
        if (picked.Count == 0) return;
        ResolveTargetAndExecute(picked[0]);
    }

    static void PickupItem()
    {
        var items = lvl.ItemsAt(upos);
        if (items.Count == 0) return;
        if (u.CanSee)
            foreach (var item in items)
                item.Knowledge |= ItemKnowledge.Seen;
        List<Item> toPickup;
        if (items.Count == 1)
        {
            toPickup = [items[0]];
        }
        else
        {
            var menu = new Menu<Item>();
            menu.Add("Pick up what?", LineStyle.Heading);
            BuildItemList(menu, items, useInvLet: false);
            toPickup = menu.Display(MenuMode.PickAny);
            if (toPickup.Count == 0) return;
        }
        foreach (var item in toPickup)
        {
            int price = g.DoPickup(u, item);
            if (price > 0)
            {
                g.pline($"The list price of {item:the,noprice} is {price.Crests()}.");
                item.UnitPrice = price;
            }
            g.pline($"{(item.Def.Class == '$' ? '$' : item.InvLet)} - {item}.");
        }

        u.Energy -= ActionCosts.OneAction.Value;
    }
}
