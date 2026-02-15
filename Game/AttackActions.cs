namespace Pathhack.Game;

public class AttackWithWeapon() : ActionBrick("attack_with_weapon")
{
    public static readonly AttackWithWeapon Instance = new();

    enum Act { Melee, Throw, Equip }
    record struct Decision(Act Act, Item? Item = null);

    public override ActionPlan CanExecute(IUnit unit, object? data, Target target)
    {
        if (target.Unit == null) return new(false, "no target");
        int dist = unit.Pos.ChebyshevDist(target.Unit.Pos);

        // fast path: adjacent + wielding a weapon
        var wielded = unit.Equipped.GetValueOrDefault(ItemSlots.HandSlot);
        if (dist == 1 && wielded != null)
            return new(true, Plan: new Decision(Act.Melee));

        // pre-check throw geometry
        bool canThrow = dist is >= 2 and <= 10
            && target.Unit.Pos.IsCompassFrom(unit.Pos)
            && unit is Monster { CanSeeYou: true };
        string? throwableId = canThrow ? unit.Query<string>("throwable", null, MergeStrategy.Replace, null!) : null;

        // single inventory scan
        Item? bestThrow = null;
        Item? bestEquip = null;
        foreach (var item in unit.Inventory)
        {
            if (item.Def is not WeaponDef wep) continue;

            if (canThrow && (wep.Launcher != null || (throwableId != null && wep.id == throwableId)))
                if (bestThrow == null || bestThrow == wielded)
                    bestThrow = item;

            if (bestEquip == null || wep.BaseDamage.Average() > ((WeaponDef)bestEquip.Def).BaseDamage.Average())
                bestEquip = item;
        }

        if (bestThrow != null) return new(true, Plan: new Decision(Act.Throw, bestThrow));
        if (bestEquip != null && bestEquip != wielded)
        {
            Log.Verbose("aww", $"[AWW] {unit}: will equip {bestEquip.Def.Name}");
            return new(true, Plan: new Decision(Act.Equip, bestEquip));
        }
        
        // Last resort punch them
        if (dist == 1) return new(true, Plan: new Decision(Act.Melee));
        return "nothing to do";
    }

    public override void Execute(IUnit unit, object? data, Target target, object? plan = null)
    {
        if (plan is not Decision d)
        {
            Log.Verbose("aww", $"[AWW] {unit}: no decision plan!");
            return;
        }

        Log.Verbose("aww", $"[AWW] {unit}: {d.Act} {d.Item?.Def.Name ?? "none"}");

        switch (d.Act)
        {
            case Act.Melee:
                DoWeaponAttack(unit, target.Unit!, unit.GetWieldedItem());
                break;

            case Act.Throw:
            {
                var item = d.Item!;
                Pos dir = (target.Unit!.Pos - unit.Pos).Signed;
                Item toThrow;
                if (item.Count > 1)
                    toThrow = item.Split(1);
                else
                {
                    toThrow = item;
                    unit.Inventory.Remove(item);
                }
                DoThrow(unit, toThrow, dir);
                break;
            }

            case Act.Equip:
                unit.Equip(d.Item!);
                break;
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