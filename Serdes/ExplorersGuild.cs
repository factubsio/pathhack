namespace Pathhack.Serdes;

public record SpawnPick(MonsterDef? Def = null, MonsterTemplate? Template = null);

public interface ILevelRuntimeBehaviour
{
    SpawnPick? PickMonster(Level level, int effectiveLevel, string reason);
}

[AttributeUsage(AttributeTargets.Field)]
public class BehaviourIdAttribute(string id) : Attribute
{
    public string Id => id;
}
