namespace Pathhack.Serdes;

/// <summary>
/// Marks a type as containing readonly LogicBrick fields that should be
/// auto-registered in MasonryYard when accessed via a static field of this type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class BrickInstancesAttribute : Attribute;
