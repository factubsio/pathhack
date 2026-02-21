namespace Pathhack.Game;

// TODO: monster AI should use weapon.Reach for melee range (dist <= reach instead of dist == 1)
public class AttackWithWeapon() : ActionBrick("attack_with_weapon")
{
    public static readonly AttackWithWeapon Instance = new();

    enum Act { Melee, Throw, Equip, Shoot }
    record struct Decision(Act Act, Item? Weapon = null, Item? Ammo = null);

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        if (target.Unit == null) return new(false, "no target");
        int dist = unit.Pos.ChebyshevDist(target.Unit.Pos);
        bool compass = target.Unit.Pos.IsCompassFrom(unit.Pos);
        bool canSee = unit is Monster { CanSeeYou: true };
        var wielded = unit.Equipped.GetValueOrDefault(ItemSlots.MainHandSlot);
        // fast path: adjacent + wielding a melee weapon — skip inventory scan
        if (dist == 1 && wielded?.Def is WeaponDef { NotForWhacking: false })
            return new(true, Plan: new Decision(Act.Melee, wielded));

        bool canSwap = wielded == null || unit.CanLetGoOf(wielded);
        string? throwableId = canSee && compass && dist >= 2
            ? unit.Query<string>("throwable", null, MergeStrategy.Replace, null!) : null;

        // single inventory scan
        Item? bestQuiver = null; double bestQuiverDmg = 0;
        Item? bestBow = null;
        Item? bestMelee = null; double bestMeleeDmg = 0;
        Item? bestThrow = null; bool throwIsWielded = false;
        string? launcherProf = null;

        foreach (var item in unit.Inventory)
        {
            if (item.Def is QuiverDef qd && item.Charges > 0)
            {
                double avg = qd.Ammo.BaseDamage.Average();
                if (avg > bestQuiverDmg) { bestQuiver = item; bestQuiverDmg = avg; launcherProf = qd.WeaponProficiency; }
            }
            else if (item.Def is WeaponDef wep)
            {
                // melee candidate
                if (canSwap && !wep.NotForWhacking)
                {
                    double avg = wep.BaseDamage.Average();
                    if (avg > bestMeleeDmg) { bestMelee = item; bestMeleeDmg = avg; }
                }

                // throw candidate
                if (throwableId != null || wep.Launcher != null)
                {
                    if (wep.Launcher != null || wep.id == throwableId)
                    {
                        if (bestThrow == null || throwIsWielded)
                            { bestThrow = item; throwIsWielded = item == wielded; }
                    }
                }
            }
        }

        // bow match: find best bow for our quiver (after scan so order doesn't matter)
        if (launcherProf != null)
        {
            if (wielded?.Def is WeaponDef ww && ww.Profiency == launcherProf)
                bestBow = wielded;
            else
                foreach (var item in unit.Inventory)
                    if (item.Def is WeaponDef wb && wb.Profiency == launcherProf) { bestBow = item; break; }
        }

        // 1: shoot if ready (quiver + wielding matching bow + compass)
        if (bestQuiver != null && bestBow == wielded && wielded != null && compass && canSee)
            return new(true, Plan: new Decision(Act.Shoot, bestBow, bestQuiver));

        // 2: adjacent — prefer melee
        if (dist == 1)
        {
            if (wielded?.Def is WeaponDef { NotForWhacking: false })
                return new(true, Plan: new Decision(Act.Melee, wielded));
            if (bestMelee != null)
                return new(true, Plan: new Decision(Act.Equip, bestMelee));
            if (unit.Actions.Any(a => a is NaturalAttack or FullAttack))
                return "has natural attack";
            return new(true, Plan: new Decision(Act.Melee, unit.GetWieldedItem())); // unarmed
        }

        // 3: dist 2+ — ranged options
        if (bestThrow != null && canSee && compass)
            return new(true, Plan: new Decision(Act.Throw, bestThrow));

        // 4: equip bow if we have quiver+bow but aren't ready to shoot
        if (bestQuiver != null && bestBow != null && bestBow != wielded && canSwap)
            return new(true, Plan: new Decision(Act.Equip, bestBow));

        return "nothing to do";
    }

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        if (plan is not Decision d)
        {
            Log.Verbose("aww", $"[AWW] {unit}: no decision plan!");
            return;
        }

        Log.Verbose("aww", $"[AWW] {unit}: {d.Act} {d.Weapon?.Def.Name ?? "none"}");

        switch (d.Act)
        {
            case Act.Melee:
                DoWeaponAttack(unit, target.Unit!, d.Weapon!);
                break;

            case Act.Throw:
            {
                var item = d.Weapon!;
                Pos dir = (target.Unit!.Pos - unit.Pos).Signed;
                Item toThrow;
                if (item.Count > 1)
                    toThrow = item.Split(1);
                else
                {
                    toThrow = item;
                    unit.Inventory.Remove(item);
                }
                DoThrow(unit, toThrow, dir, AttackType.Thrown);
                break;
            }

            case Act.Equip:
                unit.Unequip(ItemSlots.MainHandSlot);
                unit.Unequip(ItemSlots.OffHandSlot);
                unit.Equip(d.Weapon!);
                g.YouObserve(unit, $"{unit:The} switches to {unit:own} {d.Weapon!}");
                break;

            case Act.Shoot:
            {
                Pos dir = (target.Unit!.Pos - unit.Pos).Signed;
                ArcherySystem.ShootFrom(unit, d.Ammo!, dir);
                break;
            }
        }
    }
}

public class NaturalAttack(WeaponDef weapon) : ActionBrick("attack_with_nat")
{
    public WeaponDef Weapon => weapon;
    public override string? PokedexDescription => weapon.BaseDamage.ToString();
    readonly Item _item = new(weapon);

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => unit.IsAdjacent(target);

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null) =>
        DoWeaponAttack(unit, target.Unit!, _item);
}

public class FullAttack(string name, params WeaponDef[] attacks) : ActionBrick($"full:{name}:{string.Join(",", attacks.Select(a => a.id ?? a.Name))}")
{
    readonly Item[] _weapons = [.. attacks.Select(Item.Create)];

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        var tgt = target.Unit!;
        for (int i = 0; i < _weapons.Length; i++)
        {
            Item? weapon = _weapons[i];
            if (tgt.IsDead) break; // rampage to others here!!!
            DoWeaponAttack(unit, tgt, weapon, attackBonus: i > 0 ? -5 : 0);
        }
    }

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target) => unit.IsAdjacentPlan(target);
}