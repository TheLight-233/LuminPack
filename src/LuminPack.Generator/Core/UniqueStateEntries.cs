using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Gives concrete generated state entries unique names without changing their operation bodies.</summary>
internal static class UniqueStateEntries
{
    /// <summary>Names one concrete binary, JSON or width operation.</summary>
    /// <param name="operation">The original state operation name.</param>
    /// <param name="payload">The exact payload spelling.</param>
    /// <returns>A payload-specific method name.</returns>
    internal static string Name(string operation, string payload)
        => operation + "_" + RootContainerProjection.Hash(payload.Replace("global::", string.Empty).Replace(" ", string.Empty));

    /// <summary>Renames declarations and binds generated calls using their actual payload types.</summary>
    /// <param name="root">The fully projected operation contribution.</param>
    /// <param name="compilation">The original consumer compilation.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <returns>The same operation bodies under unique state names.</returns>
    internal static CompilationUnitSyntax Apply(CompilationUnitSyntax root, Compilation compilation, MetaInfo metadata)
    {
        var tree = CSharpSyntaxTree.Create(root, metadata.ParseOptions);
        var model = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        return (CompilationUnitSyntax)new Rewriter(model, null).Visit(tree.GetRoot());
    }

    /// <summary>Retargets a completed convenience block whose payload is already known.</summary>
    /// <param name="source">The concrete convenience members.</param>
    /// <param name="payload">Their exact payload spelling.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <returns>The members with unique concrete state calls.</returns>
    internal static string Facade(string source, string payload, MetaInfo metadata)
    {
        var root = CSharpSyntaxTree.ParseText("class Container {" + source + "}", metadata.ParseOptions).GetCompilationUnitRoot();
        var rewritten = (CompilationUnitSyntax)new Rewriter(null, payload).Visit(root);
        return string.Join("\n", rewritten.Members.OfType<ClassDeclarationSyntax>().Single().Members
            .Select(member => GeneratedSourceIndentation.At(member, 12)));
    }

    /// <summary>Recognizes only the state-operation families eligible for renaming.</summary>
    /// <param name="name">A method identifier.</param>
    /// <returns>Whether it is a binary, JSON or width operation.</returns>
    private static bool IsOperation(string name)
        => name is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or "MeasureRecord";

    /// <summary>Rewrites statically identified state operations only.</summary>
    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _model;
        private readonly string _payload;

        /// <summary>Captures either semantic operation binding or one concrete facade payload.</summary>
        /// <param name="model">The temporary operation model, or null for a known facade.</param>
        /// <param name="payload">The facade payload, or null for semantic operation binding.</param>
        internal Rewriter(SemanticModel model, string payload)
        {
            _model = model;
            _payload = payload;
        }

        /// <summary>Renames a concrete state declaration while retaining modifiers and its body.</summary>
        /// <param name="node">An operation declaration.</param>
        /// <returns>The original or uniquely named declaration.</returns>
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var result = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
            if (_model == null || !IsOperation(node.Identifier.ValueText) ||
                _model.GetDeclaredSymbol(node) is not IMethodSymbol method ||
                method.ContainingType.ToDisplayString() != "LuminPack.LuminPackSerializer" || method.Parameters.Length != 2 ||
                method.Parameters[0].RefKind != RefKind.Ref || LuminPackExtensionGenerator.ContainsTypeParameter(method.Parameters[1].Type))
                return result;
            return result.WithIdentifier(SyntaxFactory.Identifier(Name(node.Identifier.ValueText, FormatterTypeName.Get(method.Parameters[1].Type)))
                .WithTriviaFrom(result.Identifier));
        }

        /// <summary>Retargets a concrete state call without changing argument evaluation or passing.</summary>
        /// <param name="node">An operation invocation.</param>
        /// <returns>The invocation with an exact state-operation name.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var result = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            var name = node.Expression switch
            {
                MemberAccessExpressionSyntax access when access.Expression.ToString() == "global::LuminPack.LuminPackSerializer" => access.Name,
                SimpleNameSyntax simple when _model?.GetSymbolInfo(node).Symbol is IMethodSymbol method &&
                    method.ContainingType.ToDisplayString() == "LuminPack.LuminPackSerializer" => simple,
                _ => null,
            };
            if (name == null || !IsOperation(name.Identifier.ValueText) ||
                node.ArgumentList.Arguments.Count != 2 || !node.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) ||
                name is GenericNameSyntax { TypeArgumentList.Arguments.Count: > 1 })
                return result;
            string payload = _payload;
            if (_model != null)
            {
                var type = _model.GetTypeInfo(node.ArgumentList.Arguments[1].Expression).Type;
                if (type == null || type.TypeKind == TypeKind.Error || LuminPackExtensionGenerator.ContainsTypeParameter(type))
                    return result;
                payload = FormatterTypeName.Get(type);
            }
            if (payload == null)
                return result;
            var identifier = SyntaxFactory.Identifier(Name(name.Identifier.ValueText, payload)).WithTriviaFrom(name.Identifier);
            return result.Expression is MemberAccessExpressionSyntax rewrittenAccess
                ? result.WithExpression(rewrittenAccess.WithName(rewrittenAccess.Name.WithIdentifier(identifier)))
                : result.WithExpression(((SimpleNameSyntax)result.Expression).WithIdentifier(identifier));
        }
    }
}
