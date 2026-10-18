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

    /// <summary>Builds an entry reservation and a separate checked loop for unrepresentable reservation sizes.</summary>
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
            // Indexed dictionary/hash-set paths retain their original sparse filtering and mutation checks.
            var method = original.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method?.ParameterList.Parameters.Count != 2) return rewritten;
            var payload = method.ParameterList.Parameters[1];
            source = method.Body?.DescendantNodes().OfType<IdentifierNameSyntax>().FirstOrDefault(name => name.Identifier.ValueText == payload.Identifier.ValueText);
            if (source == null) return rewritten;
            collection = (model.GetDeclaredSymbol(payload) as IParameterSymbol)?.Type;
        }
        if (!TryGetCollection(collection, out var elements, out string countMember, out bool repeatable)) return rewritten;
        var bounds = new List<string>();
        bool strings = false;
        foreach (var element in elements)
        {
            if (element.SpecialType == SpecialType.System_String) { strings = true; bounds.Add(null); }
            else if (EmitterTypeTraits.TryGetElementUpperBound(element, out string bound)) bounds.Add(bound);
            else return rewritten;
        }
        if (strings && !repeatable) return rewritten;
        // A managed formatter can replace later dictionary keys from a callback after their lengths
        // were scanned. The existing mutation exception must remain memory-safe before it is raised.
        if (strings && elements.Any(element => element.SpecialType != SpecialType.System_String &&
            !EmitterTypeTraits.CanUseUnmanagedSerialization(element))) return rewritten;
        // Live linked/hash-entry traversals can grow from an element callback. Their Count is not a bound
        // on that traversal, even when each individual reference record has a size guarantee.
        if (original is WhileStatementSyntax && collection is INamedTypeSymbol live &&
            live.ContainingNamespace.ToDisplayString() != "System.Collections.Immutable" &&
            elements.Any(element => element.SpecialType != SpecialType.System_String && !EmitterTypeTraits.CanUseUnmanagedSerialization(element))) return rewritten;
        var calls = original.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call =>
            call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "writer" } }).ToArray();
        // Do not touch raw bulk loops, evaluator loops, or traversal-only loops.
        if (!calls.Any(call => ((MemberAccessExpressionSyntax)call.Expression).Name.Identifier.ValueText is
            "WriteValue" or "WriteValueWithCompress" or "WriteString" or "CheckBuffer")) return rewritten;
        if (original.DescendantNodes().Any(node => node is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax)) return rewritten;

        string writer = rewrite(((MemberAccessExpressionSyntax)calls[0].Expression).Expression).ToString();
        string prefix = BinaryOperationPlan.Prefix + "capacity" + identity + "_";
        string count = prefix + "count", total = prefix + "bytes", item = prefix + "item";
        string sourceText = rewrite(source).ToString();
        string countExpression = sourceText + "." + countMember;
        foreach (var scope in original.Ancestors().OfType<BlockSyntax>())
        {
            var captured = scope.Statements.OfType<LocalDeclarationStatementSyntax>().SelectMany(statement => statement.Declaration.Variables)
                .LastOrDefault(variable => variable.Span.End < original.SpanStart && variable.Initializer?.Value.ToString() == source + "." + countMember);
            if (captured == null) continue;
            string name = localName(model.GetDeclaredSymbol(captured));
            if (name != null) countExpression = name;
            break;
        }
        var preamble = new List<StatementSyntax>
        {
            SyntaxFactory.ParseStatement("int " + count + " = " + countExpression + ";")
        };
        string fixedBound = string.Join(" + ", bounds.Where(bound => bound != null).Select(bound => "(long)(" + bound + ")"));
        if (strings)
        {
            string scale = prefix + "scale", header = prefix + "header", characters = prefix + "characters";
            var stringCall = calls.Select(call => (MemberAccessExpressionSyntax)call.Expression)
                .FirstOrDefault(access => access.Name is GenericNameSyntax && access.Name.Identifier.ValueText == "WriteString");
            string profile = stringCall?.Name is GenericNameSyntax configured ? configured.TypeArgumentList.Arguments[0].ToString() : null;
            preamble.Add(SyntaxFactory.ParseStatement("int " + scale + " = writer." + (profile == null ? "StringCapacityScale" : "GetStringCapacityScale<" + profile + ">()") + ";"));
            preamble.Add(SyntaxFactory.ParseStatement("int " + header + " = writer.StringRecordLength" + (profile == null ? "" : "<" + profile + ">") + "();"));
            preamble.Add(SyntaxFactory.ParseStatement("long " + characters + " = 0;"));
            var parts = new List<string>();
            for (int index = 0; index < elements.Length; index++)
                if (bounds[index] == null)
                    parts.Add("(long)(" + item + (elements.Length == 2 ? index == 0 ? ".Key" : ".Value" : "") + "?.Length ?? 0)");
            // At most two Int32-length strings per Int32-count entry fit in Int64 without overflow.
            // Test representability once after the scan rather than branching on every string.
            preamble.Add(SyntaxFactory.ParseStatement("foreach (var " + item + " in " + sourceText + ") { " + characters + " += " + string.Join(" + ", parts) + "; }"));
            preamble.Add(SyntaxFactory.ParseStatement("long " + total + " = " + characters + " > int.MaxValue ? long.MaxValue : " + count + " * (" + (fixedBound.Length == 0 ? "0L" : fixedBound) +
                " + (long)" + header + " * " + parts.Count + ") + " + characters + " * " + scale + ";"));
        }
        else preamble.Add(SyntaxFactory.ParseStatement("long " + total + " = " + count + " * (" + fixedBound + ");"));
        string cursor = "writer.GetCurrentSpanIndex()";
        var check = calls.FirstOrDefault(call => ((MemberAccessExpressionSyntax)call.Expression).Name.Identifier.ValueText == "CheckBuffer" && call.ArgumentList.Arguments.Count == 1);
        if (check != null) cursor = rewrite(check.ArgumentList.Arguments[0].Expression).ToString();
        else
        {
            var text = calls.FirstOrDefault(call => ((MemberAccessExpressionSyntax)call.Expression).Name.Identifier.ValueText == "WriteString" &&
                call.ArgumentList.Arguments.Count >= 2 && call.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword));
            if (text != null) cursor = rewrite(text.ArgumentList.Arguments[0].Expression).ToString();
        }
        // The only run-time choice is arithmetic/capacity representability, never element-type probing.
        StatementSyntax fallback = rewritten;
        if (!strings)
        {
            var reserve = SyntaxFactory.ParseStatement("writer.EnsureCollectionCapacity(" + cursor + ", checked((int)(" + fixedBound + "))); ");
            fallback = rewritten switch
            {
                ForEachStatementSyntax traversal => traversal.WithStatement(SyntaxFactory.Block(reserve, traversal.Statement)),
                ForStatementSyntax loop => loop.WithStatement(SyntaxFactory.Block(reserve, loop.Statement)),
                WhileStatementSyntax loop => loop.WithStatement(SyntaxFactory.Block(reserve, loop.Statement)),
                _ => rewritten
            };
        }
        else if (fixedBound.Length != 0 && elements.Length == 2)
        {
            // An oversized aggregate can still contain small individual records. Reserve the complete
            // cold-path entry, including a raw tail after a string, rather than trusting threshold headroom.
            string entry = rewritten is ForEachStatementSyntax pair ? pair.Identifier.ValueText : null;
            var declaration = original.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault(variable => variable.Identifier.ValueText == "entry" && variable.Initializer?.Value is RefExpressionSyntax);
            if (entry == null && declaration?.Initializer?.Value is RefExpressionSyntax reference)
                entry = rewrite(reference.Expression).ToString();
            if (entry != null)
            {
                string entryBound = fixedBound;
                for (int index = 0; index < elements.Length; index++)
                    if (bounds[index] == null) entryBound += " + (long)(" + entry + (index == 0 ? ".Key" : ".Value") +
                        "?.Length ?? 0) * writer.StringCapacityScale + writer.StringRecordLength()";
                var reserve = SyntaxFactory.ParseStatement("writer.EnsureCollectionCapacity(" + cursor + ", checked((int)(" + entryBound + "))); ");
                fallback = rewritten switch
                {
                    ForEachStatementSyntax traversal => traversal.WithStatement(SyntaxFactory.Block(reserve, traversal.Statement)),
                    ForStatementSyntax loop => loop.WithStatement(SyntaxFactory.Block(reserve, loop.Statement)),
                    _ => rewritten
                };
            }
        }
        preamble.Add(SyntaxFactory.IfStatement(SyntaxFactory.ParseExpression("writer.TryReserveCollection(" + cursor + ", " + total + ")"),
            SyntaxFactory.Block(rewritten).WithAdditionalAnnotations(new SyntaxAnnotation(ReservedAnnotation)), SyntaxFactory.ElseClause(SyntaxFactory.Block(fallback))));
        return (StatementSyntax)new CapacityNames(writer).Visit(SyntaxFactory.Block(preamble).WithAdditionalAnnotations(new SyntaxAnnotation(ReservationAnnotation)));
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
        bool dictionary = named.Name is "Dictionary" or "SortedDictionary" or "SortedList" or "ImmutableDictionary" or
            "ImmutableSortedDictionary" or "FrozenDictionary" or "IDictionary" or "IReadOnlyDictionary" or "IImmutableDictionary";
        bool sequence = named.Name is "List" or "HashSet" or "SortedSet" or "Queue" or "Stack" or "LinkedList" or
            "ImmutableArray" or "ImmutableList" or "ImmutableHashSet" or "ImmutableSortedSet" or "ImmutableQueue" or "ImmutableStack" or
            "FrozenSet" or "ICollection" or "IList" or "IReadOnlyCollection" or "IReadOnlyList" or "ISet" or "IImmutableList" or "IImmutableSet";
        if (dictionary && named.TypeArguments.Length == 2) elements = named.TypeArguments.ToArray();
        else if (sequence && named.TypeArguments.Length == 1) elements = named.TypeArguments.ToArray();
        else return false;
        if (named.Name != "ImmutableArray" && !named.GetMembers("Count").Any() && !named.AllInterfaces.Any(iface => iface.GetMembers("Count").Any())) return false;
        repeatable = named.TypeKind != TypeKind.Interface && named.Name is not ("LinkedList" or "SortedSet" or "SortedDictionary" or "SortedList");
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
