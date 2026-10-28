using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Selects container bounds while template symbols are available; cached output retains syntax only.</summary>
internal static class BinaryCollectionCapacity
{
    internal const string ReservedAnnotation = "LuminPack.ReservedCapacity";
    internal const string ReservationAnnotation = "LuminPack.CollectionReservation";
    internal const string WriterAnnotation = "LuminPack.BinaryWriterReceiver";
    internal const string GuaranteedAnnotation = "LuminPack.GuaranteedRecordCapacity";
    internal const string WriterConstructionAnnotation = "LuminPack.BinaryWriterConstruction";

    /// <summary>Reserves statically bounded payloads once and preserves a single collection traversal.</summary>
    /// <param name="original">The bound template loop.</param>
    /// <param name="rewritten">The same loop with frozen local and call identities.</param>
    /// <param name="model">The temporary operation semantic model.</param>
    /// <param name="rewrite">Projects an original expression to frozen syntax.</param>
    /// <param name="localName">Obtains an already bound, stable local identity.</param>
    /// <param name="identity">An operation-local reservation identity.</param>
    /// <returns>A reserved traversal or the original fallback when no safe bound can be expressed.</returns>
    internal static StatementSyntax Rewrite(StatementSyntax original, StatementSyntax rewritten, SemanticModel model,
        Func<ExpressionSyntax, ExpressionSyntax> rewrite, Func<ISymbol, string> localName, int identity)
    {
        ExpressionSyntax source;
        ITypeSymbol collection;
        if (original is ForEachStatementSyntax each)
        {
            source = each.Expression;
            collection = model.GetTypeInfo(source).Type;
        }
        else
        {
            // Indexed dictionary/hash-set and queue paths retain their original finite range and filtering.
            var method = original.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method?.ParameterList.Parameters.Count != 2) return rewritten;
            var payload = method.ParameterList.Parameters[1];
            source = method.Body?.DescendantNodes().OfType<IdentifierNameSyntax>().FirstOrDefault(name => name.Identifier.ValueText == payload.Identifier.ValueText);
            if (source == null) return rewritten;
            collection = (model.GetDeclaredSymbol(payload) as IParameterSymbol)?.Type;
            // A view traversal behind a set or dictionary interface walks the exact collection its view was taken from.
            if (ViewSource(original, model) is { } narrowed)
            {
                source = narrowed;
                collection = model.GetTypeInfo(narrowed).Type;
            }
        }
        if (!TryGetCollection(collection, out var elements, out string countMember, out _)) return rewritten;
        var bounds = new List<string>();
        bool strings = false;
        foreach (var element in elements)
        {
            if (element.SpecialType == SpecialType.System_String) { strings = true; bounds.Add(null); }
            else if (EmitterTypeTraits.TryGetElementUpperBound(element, out string bound)) bounds.Add(bound);
            else return rewritten;
        }
        // Complete string operations reserve their own records during the only traversal.
        // A length prescan doubles dictionary work and cannot safely predict callback mutations.
        if (strings) return rewritten;
        var calls = original.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call =>
            call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "writer" } }).ToArray();
        // Do not touch raw bulk loops, evaluator loops, or traversal-only loops.
        if (!calls.Any(call => ((MemberAccessExpressionSyntax)call.Expression).Name.Identifier.ValueText is
            "WriteValue" or "WriteValueWithCompress" or "WriteString" or "CheckBuffer")) return rewritten;
        if (original.DescendantNodes().Any(node => node is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax)) return rewritten;

        string writer = rewrite(((MemberAccessExpressionSyntax)calls[0].Expression).Expression).ToString();
        string prefix = BinaryOperationPlan.Prefix + "capacity" + identity + "_";
        string count = prefix + "count", total = prefix + "bytes";
        string sourceText = rewrite(source).ToString();
        string countExpression = sourceText + "." + countMember;
        if (collection is INamedTypeSymbol { Name: "PriorityQueue", TypeArguments.Length: 2 })
        {
            // The heap accessor captures the finite node range; do not substitute a later queue Count.
            if (original is not ForStatementSyntax { Condition: BinaryExpressionSyntax condition } ||
                !condition.IsKind(SyntaxKind.LessThanExpression) || condition.Right is not IdentifierNameSyntax limit ||
                model.GetSymbolInfo(limit).Symbol is not ILocalSymbol { Type.SpecialType: SpecialType.System_Int32 }) return rewritten;
            countExpression = rewrite(limit).ToString();
        }
        else
        {
            foreach (var scope in original.Ancestors().OfType<BlockSyntax>())
            {
                var captured = scope.Statements.OfType<LocalDeclarationStatementSyntax>().SelectMany(statement => statement.Declaration.Variables)
                    .LastOrDefault(variable => variable.Span.End < original.SpanStart && variable.Initializer?.Value.ToString() == source + "." + countMember);
                if (captured == null) continue;
                string name = localName(model.GetDeclaredSymbol(captured));
                if (name != null) countExpression = name;
                break;
            }
        }
        var preamble = new List<StatementSyntax>
        {
            SyntaxFactory.ParseStatement("int " + count + " = " + countExpression + ";")
        };
        string fixedBound = string.Join(" + ", bounds.Where(bound => bound != null).Select(bound => "(long)(" + bound + ")"));
        preamble.Add(SyntaxFactory.ParseStatement("long " + total + " = " + count + " * (" + fixedBound + ");"));
        preamble.Add(SyntaxFactory.IfStatement(SyntaxFactory.ParseExpression("writer.TryEnsureCapacity(" + total + ")"),
            SyntaxFactory.Block(rewritten).WithAdditionalAnnotations(new SyntaxAnnotation(ReservedAnnotation)),
            SyntaxFactory.ElseClause(SyntaxFactory.Block(rewritten))));
        return (StatementSyntax)new CapacityNames(writer).Visit(SyntaxFactory.Block(preamble).WithAdditionalAnnotations(new SyntaxAnnotation(ReservationAnnotation)));
    }

    /// <summary>Finds the local or parameter whose hash-set or dictionary view an indexed traversal walks.</summary>
    /// <param name="loop">The template traversal.</param>
    /// <param name="model">The temporary operation semantic model.</param>
    /// <returns>The view's source variable declared before the loop in the same block, or null.</returns>
    private static IdentifierNameSyntax ViewSource(StatementSyntax loop, SemanticModel model)
    {
        if (loop.Parent is not BlockSyntax block) return null;
        var view = block.Statements.TakeWhile(statement => statement != loop).SelectMany(statement => statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
            .LastOrDefault(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name } &&
                name.StartsWith("Get", StringComparison.Ordinal) && name.EndsWith("View", StringComparison.Ordinal) &&
                (name.Contains("Set") || name.Contains("Dictionary")));
        return view?.ArgumentList.DescendantNodes().OfType<IdentifierNameSyntax>()
            .LastOrDefault(name => model.GetSymbolInfo(name).Symbol is ILocalSymbol or IParameterSymbol);
    }

    /// <summary>Accepts only known count contracts; string prescans exclude user collection implementations.</summary>
    /// <param name="collection">The declared traversal type.</param>
    /// <param name="elements">The independently serialized item types.</param>
    /// <param name="countMember">The original count or length member.</param>
    /// <param name="repeatable">Whether reading string lengths requires no user iterator.</param>
    /// <returns>Whether the traversal has a supported finite count contract.</returns>
    private static bool TryGetCollection(ITypeSymbol collection, out ITypeSymbol[] elements, out string countMember, out bool repeatable)
    {
        elements = Array.Empty<ITypeSymbol>(); countMember = "Count"; repeatable = false;
        if (collection is IArrayTypeSymbol array)
        { elements = new[] { array.ElementType }; countMember = "Length"; repeatable = true; return true; }
        if (collection is not INamedTypeSymbol named) return false;
        string space = named.ContainingNamespace.ToDisplayString();
        if (space == "System" && named.Name is "Span" or "ReadOnlySpan" && named.TypeArguments.Length == 1)
        { elements = named.TypeArguments.ToArray(); countMember = "Length"; repeatable = true; return true; }
        if (!space.StartsWith("System.Collections.", StringComparison.Ordinal)) return false;
        if (named.TypeKind == TypeKind.Interface) return false;
        // Concurrent enumerations may change between Count, prescan and traversal; retain their checked paths.
        if (space == "System.Collections.Concurrent") return false;
        // The fallback enumerator exposes the queue's finite Count through its framework-owned view.
        if (named.Name == "UnorderedItemsCollection" && named.ContainingType is { Name: "PriorityQueue", TypeArguments.Length: 2 } priorityQueue)
            named = priorityQueue;
        bool dictionary = named.Name is "PriorityQueue" or "Dictionary" or "SortedDictionary" or "SortedList" or "ImmutableDictionary" or
            "ImmutableSortedDictionary" or "FrozenDictionary" or "IDictionary" or "IReadOnlyDictionary" or "IImmutableDictionary";
        bool sequence = named.Name is "List" or "HashSet" or "SortedSet" or "Queue" or "Stack" or "LinkedList" or
            "ImmutableArray" or "ImmutableList" or "ImmutableHashSet" or "ImmutableSortedSet" or "ImmutableQueue" or "ImmutableStack" or
            "FrozenSet" or "ICollection" or "IList" or "IReadOnlyCollection" or "IReadOnlyList" or "ISet" or "IImmutableList" or "IImmutableSet";
        if (dictionary && named.TypeArguments.Length == 2) elements = named.TypeArguments.ToArray();
        else if (sequence && named.TypeArguments.Length == 1) elements = named.TypeArguments.ToArray();
        else return false;
        if (named.Name != "ImmutableArray" && !named.GetMembers("Count").Any() && !named.AllInterfaces.Any(iface => iface.GetMembers("Count").Any())) return false;
        repeatable = named.TypeKind != TypeKind.Interface && named.Name is not ("LinkedList" or "SortedSet" or "SortedDictionary" or "SortedList" or "PriorityQueue");
        if (named.Name == "ImmutableArray") countMember = "Length";
        return true;
    }

    /// <summary>Marks only the newly emitted reservation locals for collision-free operation expansion.</summary>
    private sealed class CapacityNames : CSharpSyntaxRewriter
    {
        private readonly string _writer;
        /// <summary>Captures the reserved compiler-local prefix.</summary>
        /// <param name="writer">The frozen identity of this traversal's actual writer parameter.</param>
        internal CapacityNames(string writer) => _writer = writer;
        /// <summary>Preserves existing semantic identities and marks newly generated local tokens.</summary>
        /// <param name="token">A token in the generated reservation.</param>
        /// <returns>The unchanged or explicitly compiler-marked token.</returns>
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken)) return base.VisitToken(token);
            if (token.Parent is IdentifierNameSyntax name && name.Parent is MemberAccessExpressionSyntax access && access.Name == name ||
                token.Parent?.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax) return base.VisitToken(token);
            if (token.HasAnnotations(BinaryOperationPlan.NameAnnotation)) return base.VisitToken(token);
            if (token.ValueText == "writer") return BinaryOperationPlan.Identity(_writer, token).WithAdditionalAnnotations(new SyntaxAnnotation(WriterAnnotation));
            return token.ValueText.StartsWith(BinaryOperationPlan.Prefix, StringComparison.Ordinal) ? BinaryOperationPlan.Identity(token.ValueText, token) : base.VisitToken(token);
        }
    }
}
