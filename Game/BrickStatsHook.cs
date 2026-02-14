using System.Collections.Concurrent;

namespace Pathhack.Game;

public class BrickStatsHook : LogicBrick
{
    public static readonly BrickStatsHook Instance = new();
    readonly ConcurrentDictionary<string, int> _counts = new();

    void Inc(string name) => _counts.AddOrUpdate(name, 1, (_, v) => v + 1);

    protected override object? OnQuery(Fact fact, string key, string? arg) { Inc("OnQuery"); return null; }
    protected override void OnFactAdded(Fact fact) => Inc("OnFactAdded");
    protected override void OnFactRemoved(Fact fact) => Inc("OnFactRemoved");
    protected override void OnStackAdded(Fact fact) => Inc("OnStackAdded");
    protected override void OnStackRemoved(Fact fact) => Inc("OnStackRemoved");
    protected override void OnRoundStart(Fact fact) => Inc("OnRoundStart");
    protected override void OnRoundEnd(Fact fact) => Inc("OnRoundEnd");
    protected override void OnBeforeDamageRoll(Fact fact, PHContext c) => Inc("OnBeforeDamageRoll");
    protected override void OnBeforeDamageIncomingRoll(Fact fact, PHContext c) => Inc("OnBeforeDamageIncomingRoll");
    protected override void OnDamageTaken(Fact fact, PHContext c) => Inc("OnDamageTaken");
    protected override void OnDamageDone(Fact fact, PHContext c) => Inc("OnDamageDone");
    protected override void OnBeforeAttackRoll(Fact fact, PHContext c) => Inc("OnBeforeAttackRoll");
    protected override void OnAfterAttackRoll(Fact fact, PHContext c) => Inc("OnAfterAttackRoll");
    protected override void OnBeforeDefendRoll(Fact fact, PHContext c) => Inc("OnBeforeDefendRoll");
    protected override void OnAfterDefendRoll(Fact fact, PHContext c) => Inc("OnAfterDefendRoll");
    protected override void OnBeforeCheck(Fact fact, PHContext c) => Inc("OnBeforeCheck");
    protected override void OnBeforeHealGiven(Fact fact, PHContext c) => Inc("OnBeforeHealGiven");
    protected override void OnBeforeHealReceived(Fact fact, PHContext c) => Inc("OnBeforeHealReceived");
    protected override void OnAfterHealReceived(Fact fact, PHContext c) => Inc("OnAfterHealReceived");
    protected override void OnEquip(Fact fact, PHContext c) => Inc("OnEquip");
    protected override void OnUnequip(Fact fact, PHContext c) => Inc("OnUnequip");
    protected override void OnSpawn(Fact fact, PHContext c) => Inc("OnSpawn");
    protected override void OnDeath(Fact fact, PHContext c) => Inc("OnDeath");
    protected override void OnBeforeSpellCast(Fact fact, PHContext c) => Inc("OnBeforeSpellCast");

    public void Dump()
    {
        var lines = _counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}: {kv.Value}");
        File.WriteAllLines("brick_stats.txt", lines);
        g.pline($"Wrote brick_stats.txt ({_counts.Count} hooks, {_counts.Values.Sum()} total calls)");
    }
}
