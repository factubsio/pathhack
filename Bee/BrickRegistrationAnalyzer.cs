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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, RampRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeImplicitCreation, SyntaxKind.ImplicitObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
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
}
