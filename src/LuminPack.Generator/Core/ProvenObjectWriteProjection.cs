using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Outlines the original complete capacity path after proving a pure object record already fits.</summary>
internal sealed class ProvenObjectWriteProjection : CSharpSyntaxRewriter
{
    private readonly Compilation _compilation;
    private readonly MetaInfo _metadata;
    private readonly IReadOnlyDictionary<string, ITypeSymbol> _types;

    /// <summary>Captures only this generation's canonical model and target information.</summary>
    /// <param name="compilation">The original consumer compilation.</param>
    /// <param name="metadata">The consumer's target and language settings.</param>
    /// <param name="types">The canonical formatter symbols.</param>
    internal ProvenObjectWriteProjection(Compilation compilation, MetaInfo metadata, IReadOnlyDictionary<string, ITypeSymbol> types)
    { _compilation = compilation; _metadata = metadata; _types = types; }

    /// <summary>Selects a complete original object or Union-payload writer without changing its dispatch.</summary>
    /// <param name="node">The localized operation.</param>
    /// <returns>The proof and original cold operation, or the untouched method.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (!_metadata.IsNet8 || !_metadata.AllowUnsafe || _metadata.IsForUnity || !ProvenWriteProfiles.AreCanonical(_compilation) ||
            _metadata.ParseOptions.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", StringComparison.Ordinal)) ||
            node.Identifier.ValueText is not ("RootWrite" or "RootWriteUnion") || node.Body == null ||
            node.ParameterList.Parameters.Count != 2 ||
            !_types.TryGetValue(node.ParameterList.Parameters[1].Type.ToString().TrimEnd('?'), out var payload) ||
            payload is not INamedTypeSymbol element || !Eligible(element)) return node;
        return Rewrite(node) ?? node;
    }

    /// <summary>Excludes observable captures, alternate field contracts and unsupported inheritance.</summary>
    /// <param name="element">The exact canonical concrete payload.</param>
    /// <returns>Whether its original direct field captures are pure.</returns>
    private bool Eligible(INamedTypeSymbol element)
    {
        if (element is not { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } ||
            !SymbolEqualityComparer.Default.Equals(element.ContainingAssembly, _compilation.Assembly) ||
            !CompilationTypeAnalysisCache.GetOrCreate(_compilation).IsPackable(element)) return false;
        for (var current = element; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            if (current.IsGenericType || !SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, _compilation.Assembly) ||
                current.GetMembers().OfType<IMethodSymbol>().Any(method => method.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ContainingNamespace.ToDisplayString() == "LuminPack.Attribute" &&
                    attribute.AttributeClass.Name is "LuminPackOnSerializingAttribute" or "LuminPackOnSerializedAttribute" or
                        "LuminPackOnDeserializingAttribute" or "LuminPackOnDeserializedAttribute"))) return false;
        }
        var data = LuminPackSourceGenerator.GetSerializationMetadata(element, _compilation, _metadata);
        return data.generatorType == GeneratorType.Object && !data.isUnion && data.callBackMethods.Count == 0 &&
            !data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) &&
            !CompiledOptions.Resolve(element, _compilation).Input.Enabled &&
            data.fields.Any(field => field.TypeSymbol.SpecialType == SpecialType.System_String) &&
            data.fields.All(field => !field.IsPrivate && !field.isProperty && !field.IsVolatile && !field.IsCompress &&
                (field.TypeSymbol.SpecialType == SpecialType.System_String ||
                 field.TypeSymbol is not INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } &&
                 EmitterTypeTraits.CanUseUnmanagedSerialization(field.TypeSymbol)));
    }

    /// <summary>Retains every original store and completion while separating a declined read-only proof.</summary>
    /// <param name="method">The complete original operation.</param>
    /// <returns>A rewritten complete operation, or null for any unrecognized shape.</returns>
    private static MethodDeclarationSyntax Rewrite(MethodDeclarationSyntax method)
    {
        var body = method.Body;
        if (body.DescendantNodes().Any(node => node is LocalFunctionStatementSyntax or TryStatementSyntax or LockStatementSyntax or
            ForStatementSyntax or CommonForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or
            GotoStatementSyntax or LabeledStatementSyntax or AwaitExpressionSyntax)) return null;
        string state = method.ParameterList.Parameters[0].Identifier.ValueText;
        string input = method.ParameterList.Parameters[1].Identifier.ValueText;
        var calls = body.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var reserves = calls.Where(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ReserveRecord" }).ToArray();
        if (reserves.Length != 1 || reserves[0].ArgumentList.Arguments.Count != 2 ||
            reserves[0].Expression is not MemberAccessExpressionSyntax reserveAccess || reserveAccess.Expression.ToString() != state ||
            reserves[0].Parent?.Parent is not AssignmentExpressionSyntax { Parent: ExpressionStatementSyntax reserveStatement }) return null;
        var declarations = body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().ToArray();
        var snapshot = declarations.SingleOrDefault(declaration => declaration.Declaration.Type is IdentifierNameSyntax { Identifier.ValueText: "var" } &&
            declaration.Declaration.Variables.Count == 1 && declaration.Declaration.Variables[0].Initializer?.Value.ToString() == input);
        if (snapshot == null) return null;
        string owner = snapshot.Declaration.Variables[0].Identifier.ValueText;
        var nulls = body.DescendantNodes().OfType<IfStatementSyntax>().Where(condition =>
            condition.Condition.ToString().Replace(" ", string.Empty) == owner + "isnull").ToArray();
        if (nulls.Length != (method.Identifier.ValueText == "RootWrite" ? 1 : 0) ||
            body.DescendantNodes().OfType<ReturnStatementSyntax>().Count() != nulls.Length) return null;
        var captures = declarations.Where(declaration => declaration.Declaration.Type is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.StringKeyword } &&
            declaration.Declaration.Variables.Count == 1 && declaration.Declaration.Variables[0].Initializer?.Value is MemberAccessExpressionSyntax member &&
            member.Expression.ToString() == owner).ToArray();
        var bounds = reserves[0].ArgumentList.Arguments[1].Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
            .Where(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "RecordBound" }).ToArray();
        if (captures.Length == 0 || bounds.Length != captures.Length ||
            calls.Count(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CommitRecords" }) != 1) return null;
        var profiles = new List<string>();
        var replacements = new Dictionary<InvocationExpressionSyntax, ExpressionSyntax>();
        foreach (var bound in bounds)
        {
            if (bound.Expression is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver, Name: GenericNameSyntax generic } ||
                receiver.Identifier.ValueText != state || generic.TypeArgumentList.Arguments.Count != 1 || bound.ArgumentList.Arguments.Count != 1) return null;
            string name = bound.ArgumentList.Arguments[0].Expression.ToString();
            if (!captures.Any(capture => capture.Declaration.Variables[0].Identifier.ValueText == name)) return null;
            profiles.Add(generic.TypeArgumentList.Arguments[0].ToString());
            replacements.Add(bound, SyntaxFactory.ParseExpression("(4L + (" + name + " is null ? 0L : (long)" + name + ".Length * 2))"));
        }
        foreach (var call in calls)
        {
            if (call.Expression is not MemberAccessExpressionSyntax access) return null;
            string receiver = access.Expression.ToString(), name = access.Name.Identifier.ValueText;
            if (receiver == state)
            {
                if (name is "ReserveRecord" or "RecordBound" or "WriteReservedRecordString" or "CommitRecords") continue;
                if (name == "Write" && nulls.Any(branch => branch.DescendantNodes().Contains(call))) continue;
                return null;
            }
            if (receiver is "Unsafe" or "global::System.Runtime.CompilerServices.Unsafe" && name is "As" or "AsRef" or "ByteOffset" or "SizeOf" or "Add") continue;
            if (receiver is "LuminPackWriter" or "global::LuminPack.Core.LuminPackWriter" && name is "WriteReserved" or "WriteReservedBlock") continue;
            return null;
        }
        string Name(string basis)
        {
            while (method.DescendantTokens().Any(token => token.ValueText == basis)) basis += "_";
            return basis;
        }
        string helperName = Name("__writeOriginalObject"), boundName = Name("__objectRecordBound");
        var boundExpression = reserves[0].ArgumentList.Arguments[1].Expression.ReplaceNodes(bounds, (old, _) => replacements[old]);
        boundExpression = (ExpressionSyntax)new WideBound().Visit(boundExpression);
        var removed = new HashSet<SyntaxNode> { snapshot, reserveStatement };
        foreach (var capture in captures) removed.Add(capture);
        foreach (var branch in nulls) removed.Add(branch);
        var hot = ((BlockSyntax)new ProvenBody(removed).Visit(body)).AddStatements(SyntaxFactory.ReturnStatement());
        string Profile(string profile) => "(typeof(" + profile + ") == typeof(global::LuminPack.Option.LuminPackUtf16LengthOptions) || " +
            "typeof(" + profile + ") == typeof(global::LuminPack.Option.LuminPackDefaultOptions) || " +
            "typeof(" + profile + ") == typeof(global::LuminPack.Option.LuminPackFixedOptions<global::LuminPack.Option.LuminPackUtf16LengthOptions>) || " +
            "typeof(" + profile + ") == typeof(global::LuminPack.Option.LuminPackFixedOptions<global::LuminPack.Option.LuminPackDefaultOptions>))";
        var proof = new List<StatementSyntax> { snapshot };
        proof.AddRange(captures);
        proof.Add(SyntaxFactory.ParseStatement("long " + boundName + " = " + boundExpression.WithoutTrivia() + ";"));
        proof.Add(SyntaxFactory.IfStatement(SyntaxFactory.ParseExpression(state + ".RecordRoom() >= " + boundName), hot));
        var profileGate = SyntaxFactory.ParseExpression("global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8 && " +
            string.Join(" && ", profiles.Distinct().Select(Profile)));
        var excluded = SyntaxFactory.IfStatement(SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
            SyntaxFactory.ParenthesizedExpression(profileGate)), body.AddStatements(SyntaxFactory.ReturnStatement()));
        var guard = SyntaxFactory.IfStatement(SyntaxFactory.ParseExpression(input + " is not null"), SyntaxFactory.Block(proof));
        var helper = (LocalFunctionStatementSyntax)SyntaxFactory.ParseStatement(
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void " + helperName + "() {}");
        helper = helper.WithParameterList(method.ParameterList).WithBody(body);
        return method.WithBody(SyntaxFactory.Block(excluded, guard,
            SyntaxFactory.ParseStatement(helperName + "(ref " + state + ", " + input + ");"), helper));
    }

    /// <summary>Widens the complete positive bound before any checked-int arithmetic.</summary>
    private sealed class WideBound : CSharpSyntaxRewriter
    {
        /// <summary>Preserves the expression without its earlier integer narrowing.</summary>
        /// <param name="node">The original checked bound.</param>
        /// <returns>The visited widened expression.</returns>
        public override SyntaxNode VisitCheckedExpression(CheckedExpressionSyntax node) => Visit(node.Expression);

        /// <summary>Widens each sum, including fixed prefixes before the first string.</summary>
        /// <param name="node">An original arithmetic expression.</param>
        /// <returns>A widened sum or an untouched other operation.</returns>
        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            var result = (BinaryExpressionSyntax)base.VisitBinaryExpression(node);
            return result.IsKind(SyntaxKind.AddExpression) ? result.WithLeft(SyntaxFactory.CastExpression(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)), SyntaxFactory.ParenthesizedExpression(result.Left))) : result;
        }
    }

    /// <summary>Removes only original nodes discharged by the complete proof.</summary>
    private sealed class ProvenBody : CSharpSyntaxRewriter
    {
        private readonly HashSet<SyntaxNode> _removed;

        /// <summary>Captures the exact redundant statement identities.</summary>
        /// <param name="removed">Pure captures and the discharged reservation/null branch.</param>
        internal ProvenBody(HashSet<SyntaxNode> removed) => _removed = removed;

        /// <summary>Retains all original kernels, address proofs and cursor completion.</summary>
        /// <param name="node">An original operation node.</param>
        /// <returns>The original visited node, or null for a discharged statement.</returns>
        public override SyntaxNode Visit(SyntaxNode node) => node != null && _removed.Contains(node) ? null : base.Visit(node);
    }
}
