namespace Pathhack.Game;

public class FlamingWeapon : LogicBrick
{
    protected override object? OnQuery(Fact fact, string key, string? arg) => key switch
    {
        "flaming" => true,
        _ => null
    };

    protected override void OnBeforeDamageRoll(Fact fact, PHContext context)
    {
        if (!fact.IsEquipped()) return;
        if (context.Weapon != fact.Entity) return;
        
        context.Damage.Add(new DamageRoll
        {
            Formula = d(6),
            Type = DamageTypes.Fire
        });
    }
}
