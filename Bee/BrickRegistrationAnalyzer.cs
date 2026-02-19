using System.Collections.Concurrent;
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
    public const string NonConstPropertyDiagnosticId = "BEE006";
    public const string WhenEquippedConflictDiagnosticId = "BEE007";

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
        "'{0}' overrides {1} but IsActive is not true in the type hierarchy — round hooks only fire on active bricks",
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

    static readonly DiagnosticDescriptor NonConstPropertyRule = new(
        NonConstPropertyDiagnosticId,
        "LogicBrick tracked property must be a constant expression",
        "'{0}.{1}' must be a simple expression body (=> constant) for static analysis",
        "MasonryYard",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    static readonly DiagnosticDescriptor WhenEquippedConflictRule = new(
        WhenEquippedConflictDiagnosticId,
        ".WhenEquipped() on brick with RequiresEquipped",
        "'{0}' has RequiresEquipped=true — .WhenEquipped() will silently never fire (fact lands on unit, not item)",
        "MasonryYard",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule, RampRule, RoundHookRule, UnsupportedHookRule, DeprecatedHookRule, NonConstPropertyRule, WhenEquippedConflictRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationCtx =>
        {
            ConcurrentDictionary<INamedTypeSymbol, BrickInfo> db = new(SymbolEqualityComparer.Default);

            // BEE001: inline construction
            compilationCtx.RegisterSyntaxNodeAction(ctx => AnalyzeObjectCreation(ctx,
                (ObjectCreationExpressionSyntax)ctx.Node), SyntaxKind.ObjectCreationExpression);
            compilationCtx.RegisterSyntaxNodeAction(ctx => AnalyzeImplicitCreation(ctx,
                (ImplicitObjectCreationExpressionSyntax)ctx.Node), SyntaxKind.ImplicitObjectCreationExpression);

            // BEE002: field modifiers
            compilationCtx.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);

            // BEE003-006: class-level checks using BrickInfo DB
            compilationCtx.RegisterSyntaxNodeAction(ctx => AnalyzeBrickClass(ctx, db), SyntaxKind.ClassDeclaration);

            // BEE007: .WhenEquipped() on RequiresEquipped brick
            compilationCtx.RegisterSyntaxNodeAction(ctx => AnalyzeWhenEquippedCall(ctx, db), SyntaxKind.InvocationExpression);
        });
    }

    // --- BEE001 ---

    static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext ctx, ObjectCreationExpressionSyntax creation)
    {
        var typeInfo = ctx.SemanticModel.GetTypeInfo(creation);
        CheckInlineConstruction(ctx, typeInfo.Type, creation.Type?.ToString());
    }

    static void AnalyzeImplicitCreation(SyntaxNodeAnalysisContext ctx, ImplicitObjectCreationExpressionSyntax creation)
    {
        var typeInfo = ctx.SemanticModel.GetTypeInfo(creation);
        CheckInlineConstruction(ctx, typeInfo.Type, typeInfo.Type?.Name);
    }

    static void CheckInlineConstruction(SyntaxNodeAnalysisContext ctx, ITypeSymbol? type, string? typeName)
    {
        if (type == null || typeName == null) return;
        if (!DerivesFrom(type, "LogicBrick")) return;
        if (BrickAllowList.InlineTypes.Contains(type.Name)) return;
        if (IsStaticFieldInitializer(ctx.Node)) return;

        ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Node.GetLocation(), typeName));
    }

    // --- BEE002 ---

    static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext ctx)
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

    // --- BEE007: .WhenEquipped() conflict ---

    static void AnalyzeWhenEquippedCall(SyntaxNodeAnalysisContext ctx, ConcurrentDictionary<INamedTypeSymbol, BrickInfo> db)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access) return;
        if (access.Name.Identifier.Text != "WhenEquipped") return;

        var receiverType = ctx.SemanticModel.GetTypeInfo(access.Expression).Type as INamedTypeSymbol;
        if (receiverType == null || !DerivesFrom(receiverType, "LogicBrick")) return;

        var info = BrickInfo.Build(receiverType, db);
        if (info.RequiresEquipped)
            ctx.ReportDiagnostic(Diagnostic.Create(WhenEquippedConflictRule, invocation.GetLocation(), receiverType.Name));
    }

    // --- BEE003-006: class-level analysis ---

    static void AnalyzeBrickClass(SyntaxNodeAnalysisContext ctx, ConcurrentDictionary<INamedTypeSymbol, BrickInfo> db)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl);
        if (symbol == null || !DerivesFrom(symbol, "LogicBrick")) return;

        var info = BrickInfo.Build(symbol, db);

        // BEE006: tracked properties must be constant expressions
        CheckNonConstProperties(ctx, classDecl, symbol);

        // BEE003: round hooks require IsActive
        CheckRoundHooks(ctx, classDecl, symbol, info);

        // BEE004/005: unsupported/deprecated hooks
        CheckUnsupportedHooks(ctx, classDecl, symbol);
    }

    static void CheckNonConstProperties(SyntaxNodeAnalysisContext ctx, ClassDeclarationSyntax classDecl, INamedTypeSymbol symbol)
    {
        foreach (var member in classDecl.Members)
        {
            if (member is not PropertyDeclarationSyntax prop) continue;
            if (!prop.Modifiers.Any(SyntaxKind.OverrideKeyword)) continue;

            string name = prop.Identifier.Text;
            if (name is not ("IsActive" or "StackMode" or "RequiresEquipped" or "IsBuff")) continue;

            // Must be expression-bodied with a resolvable constant
            var expr = prop.ExpressionBody?.Expression;
            if (expr == null || BrickInfo.TryResolveExpression(expr) == null)
                ctx.ReportDiagnostic(Diagnostic.Create(NonConstPropertyRule, prop.Identifier.GetLocation(), symbol.Name, name));
        }
    }

    static void CheckRoundHooks(SyntaxNodeAnalysisContext ctx, ClassDeclarationSyntax classDecl, INamedTypeSymbol symbol, BrickInfo info)
    {
        // Only check hooks declared on THIS class (not inherited)
        foreach (var member in classDecl.Members)
        {
            if (member is not MethodDeclarationSyntax method) continue;
            if (!method.Modifiers.Any(SyntaxKind.OverrideKeyword)) continue;
            string name = method.Identifier.Text;
            if (name is not ("OnRoundStart" or "OnRoundEnd")) continue;

            // Check the full hierarchy via BrickInfo
            if (!info.IsActive)
                ctx.ReportDiagnostic(Diagnostic.Create(RoundHookRule, classDecl.Identifier.GetLocation(), symbol.Name, name));
        }
    }

    static void CheckUnsupportedHooks(SyntaxNodeAnalysisContext ctx, ClassDeclarationSyntax classDecl, INamedTypeSymbol symbol)
    {
        if (BrickAllowList.HookOverrideAllowList.Contains(symbol.Name)) return;

        foreach (var member in classDecl.Members)
        {
            if (member is not MethodDeclarationSyntax method) continue;
            if (!method.Modifiers.Any(SyntaxKind.OverrideKeyword)) continue;
            string name = method.Identifier.Text;

            if (BrickAllowList.UnsupportedHooks.Contains(name))
                ctx.ReportDiagnostic(Diagnostic.Create(UnsupportedHookRule, method.Identifier.GetLocation(), symbol.Name, name));
            else if (BrickAllowList.DeprecatedHooks.TryGetValue(name, out var reason))
                ctx.ReportDiagnostic(Diagnostic.Create(DeprecatedHookRule, method.Identifier.GetLocation(), symbol.Name, name, reason));
        }
    }

    // --- Helpers ---

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
            if (current is MethodDeclarationSyntax or ClassDeclarationSyntax)
                return false;
            current = current.Parent;
        }
        return false;
    }
}
