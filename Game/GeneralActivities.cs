namespace Pathhack.Game;

public class DigActivity(Pos target) : Activity("dig")
{
    public override int TotalTime => lvl[target].Type switch
    {
        TileType.Rock => 4,
        TileType.Wall => 8,
        _ => 6
    };

    public override bool Interruptible => true;

    public override void OnInterrupt() => g.pline("You stop digging.");

    public override bool Tick()
    {
        Progress++;

        if (!lvl.IsDiggable(target))
        {
            g.pline("This wall is too hard to dig through.");
            return false;
        }

        if (Done)
        {
            g.pline("You finish digging.");
            lvl.Set(target, TileType.Corridor);
            return false;
        }

        return true;
    }
}

public class DigDownActivity(Pos target, bool alreadyInPit) : Activity("dig down")
{
    public override int TotalTime => alreadyInPit ? 4 : 8;
    public override bool Interruptible => true;
    public override void OnInterrupt() => g.pline("You stop digging.");

    public override bool Tick()
    {
        Progress++;
        if (Done)
        {
            Trap t;
            if (alreadyInPit)
            {
                g.pline("You dig a hole through the floor.");
                t = new HoleTrap(TrapType.Hole, lvl.EffectiveDepth) { PlayerSeen = true };
            }
            else
            {
                g.pline("You dig a pit.");
                t = new PitTrap(lvl.EffectiveDepth) { PlayerSeen = true };
            }

            lvl.Traps[target] = t;
            t.Trigger(u, null);
            return false;
        }
        return true;
    }
}

public static class DiggingUtil
{
    /// <summary>Warns player, returns false. Digging is not blocked.</summary>
    public static void ShopkeeperWarns(Pos digPos)
    {
        var room = lvl.RoomAt(digPos);
        if (room?.Type != RoomType.Shop || room.Resident is not { } shk) return;
        g.pline($"{shk:The} shouts: \"Do not damage my shop!\"");
    }

    public static void DoDig(Item item, Pos dir)
    {
        if (dir == Pos.Down)
        {
            ShopkeeperWarns(upos);
            bool inAPit = u.TrappedIn?.Type == TrapType.Pit;
            if (!inAPit && lvl.Traps.TryGetValue(upos, out var trap) && trap.Type == TrapType.Pit)
            {
                if (u.Has(CreatureTags.Flying))
                {
                    // You can fly in
                    inAPit = true;
                }
                else
                {
                    g.pline("You can't reach the bottom of the pit.");
                    return;
                }
            }
            g.pline("You start digging downward.");
            u.CurrentActivity = new DigDownActivity(upos, inAPit);
            u.Energy -= ActionCosts.OneAction.Value;
        }
        else if (dir == Pos.Up)
        {
            // TODO - when size, if super big, "you can barely touch the ceiling"
            if (u.Has(CreatureTags.Flying))
                g.pline("You almost reach the ceiling!");
            else
                g.pline("You can't reach the ceiling.");
        }
        else
        {
            Pos target = upos + dir;

            // the orderin ghere is important, we need to check out of
            // bounds FIRST for not accessing bad array index, then passable
            // for the thin air, since passable tiles are themselves not
            // diggable, so the IsDiggable has to happen after.
            if (!lvl.InBounds(target))
            {
                g.pline("This wall is too hard to dig into.");
            }
            else if (lvl[target].IsPassable)
            {
                g.pline($"You swing your {item} through thin air.");
                u.Energy -= ActionCosts.OneAction.Value;
            }
            else if (!lvl.IsDiggable(target))
            {
                g.pline("This wall is too hard to dig into.");
            }
            else
            {
                ShopkeeperWarns(target);
                g.pline("You start digging.");
                u.CurrentActivity = new DigActivity(target);
                u.Energy -= ActionCosts.OneAction.Value;
            }
        }

    }
}

// I am a digger implement
public class DiggerIdentity : LogicBrick
{
    public static readonly DiggerIdentity Instance = new();
    public override string Id => "is_digger";
    protected override object? OnQuery(Fact fact, string key, string? arg) => key.TrueWhen(Q);

    public const string Q = "digger";
}

// apply -> dig
public class DiggerVerb() : VerbResponder(ItemVerb.Apply)
{
    public override string Id => "digger";

    protected override void OnVerb(Fact fact, ItemVerb verb)
    {
        if (fact.Entity is not Item item) return;

        g.pline("In what direction?");
        var dir = Input.PickDirection();
        if (dir == null) return;

        // this delegates most of the work to DiggingUtil in case we have some other item that is like invoke or I dunno
        DiggingUtil.DoDig(item, dir.Value);
    }

    public static readonly DiggerVerb Instance = new();
}

public class ReachAttackVerb() : VerbResponder(ItemVerb.Apply)
{
    public override string Id => "reach_attack";

    protected override void OnVerb(Fact fact, ItemVerb verb)
    {
        if (fact.Entity is not Item item || item.Def is not WeaponDef wep) return;

        if (u.GetWieldedItem() != item)
        {
            if (g.DoEquip(u, item, free: true) != EquipResult.Ok) return;
        }

        var tgt = UI.Input.PickTargetInRange(wep.Reach, filter: m => m.Perception >= PlayerPerception.Detected);
        if (tgt == null) return;

        DoWeaponAttack(u, tgt, item);
        u.Energy -= ActionCosts.OneAction.Value;
    }

    public static readonly ReachAttackVerb Instance = new();
}
