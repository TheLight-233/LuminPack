using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Separates an already-fitting pure leaf record from its original complete capacity fallback.</summary>
internal sealed class ProvenLeafWriteProjection : CSharpSyntaxRewriter
{
    private readonly Compilation _compilation;
    private readonly MetaInfo _metadata;
    private readonly IReadOnlyDictionary<string, ITypeSymbol> _types;

    /// <summary>Captures canonical consumer metadata for this emission only.</summary>
    /// <param name="compilation">The unchanged consumer compilation.</param>
    /// <param name="metadata">Current language and target settings.</param>
    /// <param name="types">The canonical closed formatter symbols.</param>
    internal ProvenLeafWriteProjection(Compilation compilation, MetaInfo metadata, IReadOnlyDictionary<string, ITypeSymbol> types)
    { _compilation = compilation; _metadata = metadata; _types = types; }

    /// <summary>Selects a complete binary array writer with a canonical pure reference-element contract.</summary>
    /// <param name="node">An already localized generated method.</param>
    /// <returns>The same complete operation with a proven hot element and original cold element.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (!_metadata.IsNet8 || !_metadata.AllowUnsafe || _metadata.IsForUnity || !ProvenWriteProfiles.AreCanonical(_compilation) ||
            _metadata.ParseOptions.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", StringComparison.Ordinal)) ||
            node.Identifier.ValueText != "RootWrite" || node.Body == null || node.ParameterList.Parameters.Count != 2 ||
            !_types.TryGetValue(node.ParameterList.Parameters[1].Type.ToString().TrimEnd('?'), out var payload) ||
            payload is not IArrayTypeSymbol { Rank: 1, ElementType: INamedTypeSymbol element } || !Eligible(element)) return node;
        var loops = node.Body.DescendantNodes().OfType<ForEachStatementSyntax>().ToArray();
        if (loops.Length != 1 || loops[0].Statement is not BlockSyntax original ||
            loops[0].Type is not RefTypeSyntax) return node;
        var rewritten = RewriteElement(node, loops[0], original, loops[0].Identifier.ValueText, FormatterTypeName.Get(element), out var helper, out var profileGate);
        if (rewritten == null) return node;
        string name = "__writeOriginalSequence";
        while (node.DescendantTokens().Any(token => token.ValueText == name)) name += "_";
        var fallback = (LocalFunctionStatementSyntax)SyntaxFactory.ParseStatement(
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void " + name + "() {}");
        fallback = fallback.WithParameterList(node.ParameterList).WithBody((BlockSyntax)new ColdLabels(name).Visit(node.Body));
        string arguments = "ref " + node.ParameterList.Parameters[0].Identifier.ValueText + ", " + node.ParameterList.Parameters[1].Identifier.ValueText;
        var excluded = SyntaxFactory.IfStatement(SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SyntaxFactory.ParenthesizedExpression(profileGate)),
            SyntaxFactory.Block(SyntaxFactory.ParseStatement(name + "(" + arguments + ");"), SyntaxFactory.ReturnStatement()));
        return node.WithBody(node.Body.ReplaceNode(loops[0], rewritten).WithStatements(
            node.Body.ReplaceNode(loops[0], rewritten).Statements.Insert(0, excluded)).AddStatements(helper, fallback));
    }

    /// <summary>Excludes user-code observations, alternate storage contracts and nonordinary object framing.</summary>
    /// <param name="element">The exact canonical array element type.</param>
    /// <returns>Whether read-only field captures can precede a declined capacity proof.</returns>
    private bool Eligible(INamedTypeSymbol element)
    {
        if (element is not { TypeKind: TypeKind.Class, IsSealed: true, IsGenericType: false } ||
            element.BaseType?.SpecialType != SpecialType.System_Object ||
            !SymbolEqualityComparer.Default.Equals(element.ContainingAssembly, _compilation.Assembly) ||
            !CompilationTypeAnalysisCache.GetOrCreate(_compilation).IsPackable(element)) return false;
        var data = LuminPackSourceGenerator.GetSerializationMetadata(element, _compilation, _metadata);
        return data.generatorType == GeneratorType.Object && !data.isUnion && data.callBackMethods.Count == 0 &&
            !data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) &&
            !CompiledOptions.Resolve(element, _compilation).Input.Enabled && data.fields.Any(field => field.TypeSymbol.SpecialType == SpecialType.System_String) &&
            data.fields.All(field => !field.IsPrivate && !field.isProperty && !field.IsVolatile && !field.IsCompress &&
                (field.TypeSymbol.SpecialType == SpecialType.System_String ||
                 field.TypeSymbol is not INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } &&
                 EmitterTypeTraits.CanUseUnmanagedSerialization(field.TypeSymbol)));
    }

    /// <summary>Reuses the complete original expanded record after proving its entire existing raw bound fits.</summary>
    /// <param name="method">The owning method used to avoid every existing identifier.</param>
    /// <param name="loop">The original array traversal.</param>
    /// <param name="original">The original complete element operation.</param>
    /// <param name="item">The original array slot identity.</param>
    /// <param name="element">The canonical element spelling.</param>
    /// <param name="helper">Receives the original complete cold operation.</param>
    /// <param name="profileGate">Receives the exact framework-profile and architecture proof for the sequence entry.</param>
    /// <returns>The guarded traversal, or null when its original complete shape is not recognized.</returns>
    private static ForEachStatementSyntax RewriteElement(MethodDeclarationSyntax method, ForEachStatementSyntax loop,
        BlockSyntax original, string item, string element, out LocalFunctionStatementSyntax helper, out ExpressionSyntax profileGate)
    {
        helper = null;
        profileGate = null;
        var calls = original.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var reserve = calls.Where(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ReserveRecord" }).ToArray();
        var check = calls.Where(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CheckBuffer" }).ToArray();
        if (reserve.Length != 1 || check.Length != 1 || reserve[0].ArgumentList.Arguments.Count != 2 ||
            check[0].ArgumentList.Arguments.Count != 0 || check[0].Parent is not ExpressionStatementSyntax checkStatement ||
            reserve[0].Parent?.Parent is not AssignmentExpressionSyntax { Parent: ExpressionStatementSyntax reserveStatement }) return null;
        string state = ((MemberAccessExpressionSyntax)reserve[0].Expression).Expression.ToString();
        if (((MemberAccessExpressionSyntax)check[0].Expression).Expression.ToString() != state) return null;
        string Name(string basis)
        {
            while (method.DescendantTokens().Any(token => token.ValueText == basis)) basis += "_";
            return basis;
        }
        var declarations = original.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().ToArray();
        var alias = declarations.SingleOrDefault(declaration => declaration.Declaration.Type is RefTypeSyntax &&
            declaration.Declaration.Variables.Count == 1 && declaration.Declaration.Variables[0].Initializer?.Value is RefExpressionSyntax reference &&
            reference.Expression.ToString() == item);
        if (alias == null) return null;
        string aliasName = alias.Declaration.Variables[0].Identifier.ValueText;
        var snapshot = declarations.SingleOrDefault(declaration => declaration.Declaration.Type is IdentifierNameSyntax { Identifier.ValueText: "var" } &&
            declaration.Declaration.Variables.Count == 1 && declaration.Declaration.Variables[0].Initializer?.Value.ToString() == aliasName);
        if (snapshot == null) return null;
        string owner = snapshot.Declaration.Variables[0].Identifier.ValueText;
        var nulls = original.DescendantNodes().OfType<IfStatementSyntax>().Where(condition =>
            condition.Condition.ToString().Replace(" ", string.Empty) == owner + "isnull").ToArray();
        var captures = declarations.Where(declaration => declaration.Declaration.Type is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.StringKeyword } &&
            declaration.Declaration.Variables.Count == 1 && declaration.Declaration.Variables[0].Initializer?.Value is MemberAccessExpressionSyntax member &&
            member.Expression.ToString() == owner).ToArray();
        var bounds = reserve[0].ArgumentList.Arguments[1].Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
            .Where(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "RecordBound" }).ToArray();
        if (nulls.Length != 1 || captures.Length == 0 || bounds.Length != captures.Length ||
            original.DescendantNodes().OfType<GotoStatementSyntax>().Count() != 1 ||
            original.DescendantNodes().OfType<LabeledStatementSyntax>().Count() != 1 ||
            original.DescendantNodes().Any(node => node is LocalFunctionStatementSyntax or TryStatementSyntax or LockStatementSyntax)) return null;
        var profiles = new List<string>();
        var boundReplacements = new Dictionary<InvocationExpressionSyntax, ExpressionSyntax>();
        foreach (var bound in bounds)
        {
            if (bound.Expression is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver, Name: GenericNameSyntax generic } ||
                receiver.Identifier.ValueText != state || generic.TypeArgumentList.Arguments.Count != 1 || bound.ArgumentList.Arguments.Count != 1) return null;
            string name = bound.ArgumentList.Arguments[0].Expression.ToString();
            if (!captures.Any(capture => capture.Declaration.Variables[0].Identifier.ValueText == name)) return null;
            profiles.Add(generic.TypeArgumentList.Arguments[0].ToString());
            boundReplacements.Add(bound, SyntaxFactory.ParseExpression("(4L + (" + name + " is null ? 0L : (long)" + name + ".Length * 2))"));
        }
        var allowedStateCalls = new HashSet<string>(StringComparer.Ordinal) { "CheckBuffer", "ReserveRecord", "RecordBound", "WriteReservedRecordString", "CommitRecords", "Write" };
        if (calls.Any(call => call.Expression is MemberAccessExpressionSyntax access && access.Expression.ToString() == state &&
            !allowedStateCalls.Contains(access.Name.Identifier.ValueText))) return null;
        foreach (var call in calls)
        {
            if (call.Expression is not MemberAccessExpressionSyntax access) return null;
            string receiver = access.Expression.ToString(), name = access.Name.Identifier.ValueText;
            if (receiver == state)
            {
                if (name == "Write" && !nulls[0].DescendantNodes().Contains(call)) return null;
                continue;
            }
            if (receiver is "Unsafe" or "global::System.Runtime.CompilerServices.Unsafe" && name is "As" or "AsRef" or "ByteOffset" or "SizeOf" or "Add") continue;
            if (receiver is "LuminPackWriter" or "global::LuminPack.Core.LuminPackWriter" && name is "WriteReserved" or "WriteReservedBlock") continue;
            return null;
        }
        string helperName = Name("__writeOriginalLeaf"), boundName = Name("__leafRecordBound");
        string Profile(string profile) => "(typeof(" + profile + ") == typeof(global::LuminPack.Option.LuminPackUtf16LengthOptions) || " +
            "typeof(" + profile + ") == typeof(global::LuminPack.Option.LuminPackDefaultOptions) || " +
            "typeof(" + profile + ") == typeof(global::LuminPack.Option.LuminPackFixedOptions<global::LuminPack.Option.LuminPackUtf16LengthOptions>) || " +
            "typeof(" + profile + ") == typeof(global::LuminPack.Option.LuminPackFixedOptions<global::LuminPack.Option.LuminPackDefaultOptions>))";
        ExpressionSyntax boundExpression = reserve[0].ArgumentList.Arguments[1].Expression.ReplaceNodes(bounds, (old, _) => boundReplacements[old]);
        boundExpression = (ExpressionSyntax)new WideBound().Visit(boundExpression);
        var removed = new HashSet<SyntaxNode> { checkStatement, alias, snapshot, nulls[0], reserveStatement };
        foreach (var capture in captures) removed.Add(capture);
        var hot = (BlockSyntax)new HotElement(removed).Visit(original);
        profileGate = SyntaxFactory.ParseExpression("global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8 && " + string.Join(" && ", profiles.Distinct().Select(Profile)));
        var guard = (IfStatementSyntax)SyntaxFactory.ParseStatement("if (" + item + " is not null) { }");
        var fits = (IfStatementSyntax)SyntaxFactory.ParseStatement("if (" + state + ".RecordRoom() >= " + boundName + ") { }");
        fits = fits.WithStatement(hot.AddStatements(SyntaxFactory.ContinueStatement()));
        var fast = new List<StatementSyntax> { SyntaxFactory.ParseStatement("var " + owner + " = " + item + ";") };
        fast.AddRange(captures);
        fast.Add(SyntaxFactory.ParseStatement("long " + boundName + " = " + boundExpression.WithoutTrivia() + ";"));
        fast.Add(fits);
        guard = guard.WithStatement(SyntaxFactory.Block(fast));
        helper = (LocalFunctionStatementSyntax)SyntaxFactory.ParseStatement("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] " +
            "static void " + helperName + "(ref global::LuminPack.Core.LuminPackWriter " + state + ", ref " + element + " " + item + ") {}");
        helper = helper.WithBody(original);
        return loop.WithStatement(SyntaxFactory.Block(guard, SyntaxFactory.ParseStatement(helperName + "(ref " + state + ", ref " + item + ");")));
    }

    /// <summary>Widens every addition before evaluating the original complete bound.</summary>
    private sealed class WideBound : CSharpSyntaxRewriter
    {
        /// <summary>Retains complete positive terms without the original checked-int narrowing.</summary>
        /// <param name="node">The original checked reservation expression.</param>
        /// <returns>The same expression evaluated with widened additions.</returns>
        public override SyntaxNode VisitCheckedExpression(CheckedExpressionSyntax node) => Visit(node.Expression);

        /// <summary>Prevents a prefix of fixed terms from overflowing before a string term is reached.</summary>
        /// <param name="node">An original bound addition.</param>
        /// <returns>The widened sum or an unchanged nonaddition.</returns>
        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            var result = (BinaryExpressionSyntax)base.VisitBinaryExpression(node);
            return result.IsKind(SyntaxKind.AddExpression) ? result.WithLeft(SyntaxFactory.CastExpression(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)),
                SyntaxFactory.ParenthesizedExpression(result.Left))) : result;
        }
    }

    /// <summary>Removes only the checks, pure captures and null exit already discharged by the complete proof.</summary>
    private sealed class HotElement : CSharpSyntaxRewriter
    {
        private readonly HashSet<SyntaxNode> _removed;

        /// <summary>Captures exact original nodes rather than matching arbitrary user statements by text.</summary>
        /// <param name="removed">The proven redundant statement identities.</param>
        internal HotElement(HashSet<SyntaxNode> removed) => _removed = removed;

        /// <summary>Keeps every original fixed/string kernel, address proof and completion operation.</summary>
        /// <param name="node">A node in the original expanded element.</param>
        /// <returns>The retained node or null for an already discharged statement.</returns>
        public override SyntaxNode Visit(SyntaxNode node) => node != null && (_removed.Contains(node) || node is LabeledStatementSyntax)
            ? null : base.Visit(node);
    }

    /// <summary>Keeps original completion labels distinct from their enclosing specialized operation.</summary>
    private sealed class ColdLabels : CSharpSyntaxRewriter
    {
        private readonly string _prefix;

        /// <summary>Captures a collision-free cold-function identity.</summary>
        /// <param name="prefix">The unique helper identity.</param>
        internal ColdLabels(string prefix) => _prefix = prefix;

        /// <summary>Retains the original labeled statement under its distinct cold identity.</summary>
        /// <param name="node">An original completion label.</param>
        /// <returns>The same statement with a distinct label.</returns>
        public override SyntaxNode VisitLabeledStatement(LabeledStatementSyntax node)
            => ((LabeledStatementSyntax)base.VisitLabeledStatement(node)).WithIdentifier(SyntaxFactory.Identifier(_prefix + "_" + node.Identifier.ValueText));

        /// <summary>Preserves every original jump to its corresponding cold label.</summary>
        /// <param name="node">An original jump.</param>
        /// <returns>The same jump targeting its cold label.</returns>
        public override SyntaxNode VisitGotoStatement(GotoStatementSyntax node)
            => node.Expression is IdentifierNameSyntax target ? node.WithExpression(SyntaxFactory.IdentifierName(_prefix + "_" + target.Identifier.ValueText)) : node;
    }
}
