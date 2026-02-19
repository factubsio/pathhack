using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bee;

public class BrickInfo
{
    public HashSet<string> OverriddenHooks { get; } = new();
    public Dictionary<string, string?> ResolvedProperties { get; } = new();

    /// <summary>True if the property is overridden anywhere in the chain (even if we can't resolve its value).</summary>
    public bool HasProperty(string name) => ResolvedProperties.ContainsKey(name);

    /// <summary>Resolved constant value of a property, or null if not overridden or not resolvable.</summary>
    public string? GetPropertyValue(string name) => ResolvedProperties.TryGetValue(name, out var v) ? v : null;

    public bool IsActive => GetPropertyValue("IsActive") == "true";
    public string? StackMode => GetPropertyValue("StackMode");
    public bool RequiresEquipped => GetPropertyValue("RequiresEquipped") == "true";

    static readonly string[] TrackedProperties = ["IsActive", "StackMode", "RequiresEquipped", "IsBuff"];

    static readonly HashSet<string> HookMethods = new()
    {
        "OnQuery", "OnFactAdded", "OnFactRemoved", "OnStackAdded", "OnStackRemoved",
        "OnRoundStart", "OnRoundEnd", "OnTurnStart", "OnTurnEnd",
        "OnBeforeDamageRoll", "OnBeforeDamageIncomingRoll", "OnDamageTaken", "OnDamageDone",
        "OnBeforeAttackRoll", "OnAfterAttackRoll", "OnBeforeDefendRoll", "OnAfterDefendRoll",
        "OnBeforeCheck", "OnBeforeHealGiven", "OnBeforeHealReceived", "OnAfterHealReceived",
        "OnEquip", "OnUnequip", "OnSpawn", "OnDeath", "OnBeforeSpellCast", "OnVerb",
    };

    public static BrickInfo Build(INamedTypeSymbol symbol, ConcurrentDictionary<INamedTypeSymbol, BrickInfo> cache)
    {
        if (cache.TryGetValue(symbol, out var existing))
            return existing;

        BrickInfo info = new();

        // Walk from current class up to (but not including) LogicBrick
        List<INamedTypeSymbol> chain = [];
        var current = symbol;
        while (current != null && current.Name != "LogicBrick")
        {
            chain.Add(current);
            current = current.BaseType;
        }

        // Process from most-base to most-derived (first override wins for value, all hooks collected)
        chain.Reverse();
        foreach (var type in chain)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IMethodSymbol method && method.IsOverride && HookMethods.Contains(method.Name))
                    info.OverriddenHooks.Add(method.Name);

                if (member is IPropertySymbol prop && prop.IsOverride && TrackedProperties.Contains(prop.Name))
                {
                    // Only set if not already resolved (most-base override wins)
                    if (!info.ResolvedProperties.ContainsKey(prop.Name))
                        info.ResolvedProperties[prop.Name] = TryResolveConstant(prop);
                }
            }
        }

        cache.TryAdd(symbol, info);
        return info;
    }

    /// <summary>Resolve a simple expression to a string value. Shared by BrickInfo and BEE006.</summary>
    public static string? TryResolveExpression(ExpressionSyntax expr) => expr switch
    {
        LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.TrueLiteralExpression) => "true",
        LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.FalseLiteralExpression) => "false",
        MemberAccessExpressionSyntax access when access.Expression is IdentifierNameSyntax => access.Name.Identifier.Text,
        _ => null,
    };

    static string? TryResolveConstant(IPropertySymbol prop)
    {
        // Find the syntax for this property
        var syntax = prop.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (syntax is not PropertyDeclarationSyntax propSyntax) return null;

        // Only handle expression-bodied: => value
        var expr = propSyntax.ExpressionBody?.Expression;
        if (expr == null) return null;

        return TryResolveExpression(expr);
    }
}
