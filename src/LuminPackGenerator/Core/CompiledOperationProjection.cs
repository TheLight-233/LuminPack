using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Propagates compiled profiles through concrete, recursive and ordinary virtual operations.</summary>
internal sealed partial class CompiledOperationProjection : CSharpSyntaxRewriter
{
    private readonly Compilation _compilation;
    private readonly CompiledOptions _default;
    private readonly IReadOnlyDictionary<string, ITypeSymbol> _types;
    internal const string UnionRootAnnotation = "LuminPack.CompiledUnionRoot";

    /// <summary>Captures current type identities only for this syntax projection.</summary>
    /// <param name="compilation">The current consumer compilation.</param>
    /// <param name="fallback">The declaration profile for a facade or generic fallback.</param>
    internal CompiledOperationProjection(Compilation compilation, CompiledOptions fallback)
    {
        _compilation = compilation; _default = fallback;
        _types = CompilationTypeAnalysisCache.GetOrCreate(compilation).FormatterTypesByName;
    }

    /// <summary>Moves each complete operation body into a profile-generic operation, retaining thin declaration-default entries.</summary>
    /// <param name="node">A Serializer, dispatch, JSON cache or generated Union declaration.</param>
    /// <returns>The original declarations with one complete body per operation.</returns>
    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        => RewriteMembers(node);

    /// <summary>Preserves interface Union dispatch while adding a compiled document profile to its operation slots.</summary>
    /// <param name="node">The generated partial Union interface.</param>
    /// <returns>The original interface with compiled operation slots.</returns>
    public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        => RewriteMembers(node);

    /// <summary>Preserves value-type implementations of interface Union slots.</summary>
    /// <param name="node">The generated partial value type.</param>
    /// <returns>The same value type with ordinary compiled slots.</returns>
    public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        => RewriteMembers(node);

    /// <summary>Projects one generated operation container without changing its type or virtual dispatch policy.</summary>
    /// <param name="node">The original class or interface declaration.</param>
    /// <returns>The same declaration with its compiled operation family.</returns>
    private TypeDeclarationSyntax RewriteMembers(TypeDeclarationSyntax node)
    {
        var members = new List<MemberDeclarationSyntax>();
        foreach (var member in node.Members)
        {
            if (member is not MethodDeclarationSyntax method || method.ParameterList.Parameters.Count == 0 ||
                method.Body == null && method.ExpressionBody == null)
            { members.Add((MemberDeclarationSyntax)Visit(member)); continue; }
            string identifier = method.Identifier.ValueText;
            if (identifier == "Register" && method.TypeParameterList?.Parameters.Count == 1)
            {
                AppendRegistration(members, method);
                continue;
            }
            bool stateEntry = TryState(method.ParameterList.Parameters[0].Type?.ToString(), out bool writer);
            bool facade = !stateEntry && IsFacadeOperation(identifier);
            if (!stateEntry && !facade) { members.Add((MemberDeclarationSyntax)Visit(member)); continue; }
            bool serializer = identifier is "SerializeJson" or "DeserializeJson" or "Serialize" or "Deserialize" or "Sizeof" or
                "SerializeUnion" or "DeserializeUnion" or "DeserializeFresh" or "SerializeWithCompress" or "DeserializeWithCompress";
            bool slot = identifier.StartsWith("__LuminPackUnionSerializeJson_", StringComparison.Ordinal) ||
                identifier.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal) || identifier.StartsWith("__LuminPackUnionCalculateOffset_", StringComparison.Ordinal);
            bool lookup = identifier == "TryReadOrderedJsonProperty";
            if (!serializer && !slot && !lookup && !facade) { members.Add(method); continue; }

            if (slot)
            {
                AppendUnionSlots(members, method, node, writer);
                continue;
            }

            AppendOperation(members, method, node, stateEntry, writer, facade);
        }
        return node.WithMembers(SyntaxFactory.List(members));
    }

    /// <summary>Recognizes convenience operations sharing the same compiled profile as their state core.</summary>
    /// <param name="name">The generated operation name.</param>
    /// <returns>Whether the facade can propagate a stateless profile.</returns>
    private static bool IsFacadeOperation(string name) => name is "Serialize" or "Deserialize" or "Sizeof" or "SerializeJson" or "DeserializeJson" or
        "SerializeAsync" or "DeserializeAsync" or "SerializeCore" or "DeserializeCore" or "SerializeJsonCore" or "DeserializeJsonCore";

    /// <summary>Recognizes operation-state spellings without inspecting unrelated methods or function-pointer parameters.</summary>
    /// <param name="type">The generated first parameter's type spelling.</param>
    /// <param name="writer">Receives whether the state writes JSON.</param>
    /// <returns>Whether this is an actual Writer, Reader or Evaluator parameter.</returns>
    private static bool TryState(string type, out bool writer)
    {
        string name = type?.Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty);
        writer = name is "LuminPackJsonWriter" or "LuminPackWriter";
        return writer || name is "LuminPackJsonReader" or "LuminPackReader" or "LuminPackEvaluator";
    }

    /// <summary>Keeps fixed-value native bodies in their existing nongeneric method.</summary>
    /// <param name="method">The existing complete state operation.</param>
    /// <returns>Whether an ambient profile can affect its generated work.</returns>
    private static bool HasProfileWork(MethodDeclarationSyntax method) => method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
        call.Expression is MemberAccessExpressionSyntax access &&
        (access.Name.Identifier.ValueText.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal) ||
         access.Name.Identifier.ValueText.StartsWith("__LuminPackUnionCalculateOffset_", StringComparison.Ordinal) ||
         access.Name.Identifier.ValueText is "Serialize" or "Deserialize" or "Sizeof" ||
         access.Expression.ToString() == "global::LuminPack.Code.LuminPackFormatterRegistry" ||
         (CompiledOptionProjection.WriterMethods.Contains(access.Name.Identifier.ValueText) || CompiledOptionProjection.ReaderMethods.Contains(access.Name.Identifier.ValueText) ||
          CompiledOptionProjection.EvaluatorMethods.Contains(access.Name.Identifier.ValueText)) &&
         !(access.Name is GenericNameSyntax generic && generic.TypeArgumentList.Arguments[0].ToString().Contains("LuminPackFixedOptions"))));

}
