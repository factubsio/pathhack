namespace Pathhack.Serdes;

/// <summary>
/// Marks a static class as having item fields that should be collected into an All* array.
/// Repeatable - one per item type to collect.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GenerateAllAttribute(string arrayName, Type itemType) : Attribute
{
    public string ArrayName => arrayName;
    public Type ItemType => itemType;
}

/// <summary>
/// Marks an item field as excluded from random generation pools.
/// Still included in All* arrays for wishing/debug.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class NotRandomlyGeneratedAttribute : Attribute;
