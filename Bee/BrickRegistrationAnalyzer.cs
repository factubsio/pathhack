using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bee;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class BrickRegistrationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "BEE001";
    public const string RampDiagnosticId = "BEE002";
    public const string RoundHookDiagnosticId = "BEE003";
    public const string UnsupportedHookDiagnosticId = "BEE004";
    public const string DeprecatedHookDiagnosticId = "BEE005";

    static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "LogicBrick must be a static field or whitelisted type",
        "'{0}' is constructed inline but is not a whitelisted brick type — it should be readonly!",
        "MasonryYard",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    static readonly DiagnosticDescriptor RampRule = new(
        RampDiagnosticId,
        "[BrickInstances] field must be static readonly",
        "Field '{0}' has [BrickInstances] type but is not static readonly",
        "MasonryYard",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    static readonly DiagnosticDescriptor RoundHookRule = new(
        RoundHookDiagnosticId,
        "LogicBrick overrides OnRoundStart/OnRoundEnd without IsActive",
        "'{0}' overrides {1} but does not override IsActive — round hooks only fire on active bricks",
        "MasonryYard",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    static readonly DiagnosticDescriptor UnsupportedHookRule = new(
        UnsupportedHookDiagnosticId,
        "LogicBrick overrides unsupported hook",
        "'{0}' overrides {1} which is not currently called by the game loop",
        "MasonryYard",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    static readonly DiagnosticDescriptor DeprecatedHookRule = new(
        DeprecatedHookDiagnosticId,
        "LogicBrick overrides deprecated hook",
        "'{0}' overrides {1} which is deprecated — {2}",
        "MasonryYard",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray.Create(Rule, RampRule, RoundHookRule, UnsupportedHookRule, DeprecatedHookRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeImplicitCreation, SyntaxKind.ImplicitObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeClassForRoundHooks, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeClassForUnsupportedHooks, SyntaxKind.ClassDeclaration);
    }

    void AnalyzeObjectCreation(SyntaxNodeAnalysisContext ctx)
    {
        var creation = (ObjectCreationExpressionSyntax)ctx.Node;
        var typeInfo = ctx.SemanticModel.GetTypeInfo(creation);
        Check(ctx, typeInfo.Type, creation.Type?.ToString());
    }

    void AnalyzeImplicitCreation(SyntaxNodeAnalysisContext ctx)
    {
        var creation = (ImplicitObjectCreationExpressionSyntax)ctx.Node;
        var typeInfo = ctx.SemanticModel.GetTypeInfo(creation);
        Check(ctx, typeInfo.Type, typeInfo.Type?.Name);
    }

    void Check(SyntaxNodeAnalysisContext ctx, ITypeSymbol? type, string? typeName)
    {
        if (type == null || typeName == null) return;
        if (!DerivesFrom(type, "LogicBrick")) return;
        if (BrickAllowList.InlineTypes.Contains(type.Name)) return;
        if (IsStaticFieldInitializer(ctx.Node)) return;

        ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Node.GetLocation(), typeName));
    }

    static bool DerivesFrom(ITypeSymbol? type, string baseName)
    {
        var current = type?.BaseType;
        while (current != null)
        {
            if (current.Name == baseName) return true;
            current = current.BaseType;
        }
        return false;
    }

    static bool IsStaticFieldInitializer(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is FieldDeclarationSyntax field)
                return field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
            if (current is PropertyDeclarationSyntax prop)
                return prop.Modifiers.Any(SyntaxKind.StaticKeyword);
            // Stop walking at method/class boundaries
            if (current is MethodDeclarationSyntax or ClassDeclarationSyntax)
                return false;
            current = current.Parent;
        }
        return false;
    }

    void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext ctx)
    {
        var fieldDecl = (FieldDeclarationSyntax)ctx.Node;
        bool isStatic = fieldDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
        bool isReadonly = fieldDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
        if (isStatic && isReadonly) return;

        foreach (var variable in fieldDecl.Declaration.Variables)
        {
            var symbol = ctx.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
            if (symbol == null) continue;
            if (!symbol.Type.GetAttributes().Any(a => a.AttributeClass?.Name == "BrickInstancesAttribute")) continue;
            ctx.ReportDiagnostic(Diagnostic.Create(RampRule, variable.GetLocation(), symbol.Name));
        }
    }

    void AnalyzeClassForRoundHooks(SyntaxNodeAnalysisContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl);
        if (symbol == null || !DerivesFrom(symbol, "LogicBrick")) return;

        bool hasRoundHook = false;
        string? hookName = null;
        bool hasIsActive = false;

        foreach (var member in classDecl.Members)
        {
            if (member is not MethodDeclarationSyntax method) continue;
            if (!method.Modifiers.Any(SyntaxKind.OverrideKeyword)) continue;
            var name = method.Identifier.Text;
            if (name is "OnRoundStart" or "OnRoundEnd") { hasRoundHook = true; hookName = name; }
        }

        if (!hasRoundHook) return;

        foreach (var member in classDecl.Members)
        {
            if (member is not PropertyDeclarationSyntax prop) continue;
            if (prop.Modifiers.Any(SyntaxKind.OverrideKeyword) && prop.Identifier.Text == "IsActive")
            { hasIsActive = true; break; }
        }

        if (!hasIsActive)
            ctx.ReportDiagnostic(Diagnostic.Create(RoundHookRule, classDecl.Identifier.GetLocation(), symbol.Name, hookName));
    }

    void AnalyzeClassForUnsupportedHooks(SyntaxNodeAnalysisContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl);
        if (symbol == null || !DerivesFrom(symbol, "LogicBrick")) return;
        if (BrickAllowList.HookOverrideAllowList.Contains(symbol.Name)) return;

        foreach (var member in classDecl.Members)
        {
            if (member is not MethodDeclarationSyntax method) continue;
            if (!method.Modifiers.Any(SyntaxKind.OverrideKeyword)) continue;
            var name = method.Identifier.Text;

            if (BrickAllowList.UnsupportedHooks.Contains(name))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(UnsupportedHookRule, method.Identifier.GetLocation(), symbol.Name, name));
            }
            else if (BrickAllowList.DeprecatedHooks.TryGetValue(name, out var reason))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(DeprecatedHookRule, method.Identifier.GetLocation(), symbol.Name, name, reason));
            }
        }
    }
}
