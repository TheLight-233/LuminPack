using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Projects generated cursor references onto the selected compatibility asset.</summary>
internal static class CompatibleCursorProjection
{
    private const string Unsafe = "global::System.Runtime.CompilerServices.Unsafe";
    private const string Marshal = "global::System.Runtime.InteropServices.MemoryMarshal";

    private static readonly System.Text.RegularExpressions.Regex ScopedModifier = new(@"\bscoped\s+(?=(?:ref|in)\b)",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Adapts generated cursor references, ref assignments and native offsets for compatibility targets.</summary>
    /// <param name="source">The generated text.</param>
    /// <param name="legacyPointer">Whether the referenced Core predates span cursors.</param>
    /// <returns>The projected generated file.</returns>
    internal static string Apply(string source, bool legacyPointer)
    {
        source = ScopedModifier.Replace(source, string.Empty);
        bool pin = legacyPointer && source.IndexOf("Reader(ref ", StringComparison.Ordinal) >= 0;
        if (!pin && source.IndexOf(".Cursor", StringComparison.Ordinal) < 0 &&
            source.IndexOf("ByteOffset(", StringComparison.Ordinal) < 0)
            return source;
        SyntaxNode root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)).GetRoot();
        SyntaxNode projected = new Methods(legacyPointer).Visit(root);
        if (pin)
            projected = new PinnedReaders().Visit(projected);
        return ReferenceEquals(projected, root) ? source : projected.ToFullString();
    }

    /// <summary>Retains facade signatures during call discovery and defers reader bodies until the Core reference is known.</summary>
    /// <param name="source">The fixed compatibility facade with unscoped parameters.</param>
    /// <returns>The facade declarations and their Core-dependent reader implementations.</returns>
    internal static (string Declarations, string Readers) SplitFacade(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)).GetCompilationUnitRoot();
        if (root.Members.Count != 1 || root.Members[0] is not NamespaceDeclarationSyntax scope ||
            scope.Members.Count != 1 || scope.Members[0] is not ClassDeclarationSyntax facade)
            return (source, string.Empty);

        var readers = new List<MethodDeclarationSyntax>();
        var declarations = new List<MemberDeclarationSyntax>(facade.Members.Count);
        foreach (MemberDeclarationSyntax member in facade.Members)
        {
            if (member is not MethodDeclarationSyntax { Body: { } body } method ||
                !ReaderConstruction(body))
            {
                declarations.Add(member);
                continue;
            }

            var partial = method.WithModifiers(method.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space)));
            declarations.Add(partial.WithBody(null).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(method.GetTrailingTrivia())));
            readers.Add(partial.WithLeadingTrivia(method.GetLeadingTrivia().Where(trivia => !trivia.HasStructure)));
        }

        if (readers.Count == 0)
            return (source, string.Empty);
        var declarationRoot = root.ReplaceNode(facade, facade.WithMembers(SyntaxFactory.List(declarations)));
        var readerRoot = root.ReplaceNode(facade, facade.WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
            .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(readers)));
        return (declarationRoot.ToFullString(), readerRoot.ToFullString());
    }

    /// <summary>Recognizes a facade directly binding a reader to an input span.</summary>
    /// <param name="body">The method body.</param>
    /// <returns>Whether the method needs to select the input lifetime for the referenced Core.</returns>
    private static bool ReaderConstruction(BlockSyntax body)
        => body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Any(creation =>
            creation.Type.ToString() is "LuminPackReader" or "LuminPackJsonReader" &&
            creation.ArgumentList is { Arguments.Count: > 0 } arguments &&
            arguments.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword));

    /// <summary>Publishes facade reader bodies for targets without ref fields.</summary>
    /// <param name="source">The fixed facade reader implementations.</param>
    /// <param name="legacyPointer">Whether the referenced Core needs its input pinned.</param>
    /// <returns>The implementation source with the matching input lifetime.</returns>
    internal static string FacadeReaders(string source, bool legacyPointer)
    {
        if (legacyPointer)
        {
            var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)).GetRoot();
            source = new PinnedReaders().Visit(root)!.ToFullString();
        }
        return "#if !NET8_0_OR_GREATER\n" + source + "\n#endif\n";
    }

    /// <summary>Keeps input stationary when compiling against an older Core with pointer cursors.</summary>
    private sealed class PinnedReaders : CSharpSyntaxRewriter
    {
        private int _next;

        /// <summary>Wraps the rest of a block after a reader over `ref input` in the input's stationary lifetime.</summary>
        /// <param name="node">The generated block that may construct a reader.</param>
        /// <returns>The block with a pooled copy for the legacy reader, when needed.</returns>
        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            var block = (BlockSyntax)base.VisitBlock(node)!;
            for (int index = 0; index < block.Statements.Count; index++)
            {
                if (block.Statements[index] is not LocalDeclarationStatementSyntax declaration || declaration.Declaration.Variables.Count != 1 ||
                    declaration.Declaration.Variables[0].Initializer?.Value is not ObjectCreationExpressionSyntax creation ||
                    creation.Type.ToString().Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty) is not ("LuminPackReader" or "LuminPackJsonReader") ||
                    creation.ArgumentList == null || creation.ArgumentList.Arguments.Count == 0 ||
                    !creation.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
                    continue;
                string input = creation.ArgumentList.Arguments[0].Expression.ToString();
                string identity = "__luminPinnedInput" + _next++.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var arguments = creation.ArgumentList.Arguments;
                var rest = new List<StatementSyntax>();
                for (int later = index + 1; later < block.Statements.Count; later++)
                    rest.Add(block.Statements[later]);
                string owner = identity + "Owner", span = identity + "Span";
                var nativeCreation = creation.WithArgumentList(creation.ArgumentList.WithArguments(arguments.Replace(arguments[0],
                    arguments[0].WithExpression(SyntaxFactory.IdentifierName(span)))));
                var statements = new List<StatementSyntax>
                {
                    SyntaxFactory.ParseStatement(owner + ".LoadSpan(" + input + ");"),
                    SyntaxFactory.ParseStatement("var " + span + " = (global::System.ReadOnlySpan<byte>)" + owner + ".GetSpan();"),
                    declaration.ReplaceNode(creation, nativeCreation),
                };
                statements.AddRange(rest);
                var cleanup = SyntaxFactory.FinallyClause(SyntaxFactory.Block(SyntaxFactory.ParseStatement(
                    "global::LuminPack.Utility.LuminBufferWriterPool.Return(" + owner + ");")));
                var lifetime = SyntaxFactory.Block(SyntaxFactory.ParseStatement("var " + owner + " = global::LuminPack.Utility.LuminBufferWriterPool.Rent();"),
                    SyntaxFactory.TryStatement(SyntaxFactory.Block(statements), default, cleanup));
                var kept = new List<StatementSyntax>(index + 1);
                for (int earlier = 0; earlier < index; earlier++)
                    kept.Add(block.Statements[earlier]);
                kept.Add(lifetime);
                return block.WithStatements(SyntaxFactory.List(kept));
            }
            return block;
        }
    }

    /// <summary>Recognizes the advancing state types by their unqualified spelling.</summary>
    /// <param name="type">The declared type text.</param>
    /// <returns>Whether the type owns an advancing cursor.</returns>
    private static bool IsState(string? type)
    {
        if (type == null)
            return false;
        int dot = type.LastIndexOf('.');
        return (dot < 0 ? type : type.Substring(dot + 1)) is "LuminPackWriter" or "LuminPackReader" or "LuminPackJsonWriter" or "LuminPackJsonReader";
    }

    /// <summary>Rewrites each method and local function over the states it declares.</summary>
    private sealed class Methods : CSharpSyntaxRewriter
    {
        private readonly bool _legacyPointer;

        /// <summary>Selects the representation declared by the referenced Core.</summary>
        /// <param name="legacyPointer">Whether the cursor is a pointer in an older Core reference.</param>
        internal Methods(bool legacyPointer) => _legacyPointer = legacyPointer;

        /// <inheritdoc />
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
            => new Cursors(States(node), _legacyPointer).Visit(node);

        /// <inheritdoc />
        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            => new Cursors(States(node), _legacyPointer).Visit(node);

        /// <summary>Collects the parameters and locals of a member that hold an advancing state.</summary>
        /// <param name="member">The method or constructor containing state variables.</param>
        /// <returns>The identifiers whose cursor accesses can be projected.</returns>
        private static HashSet<string> States(SyntaxNode member)
        {
            var states = new HashSet<string>(StringComparer.Ordinal);
            foreach (SyntaxNode node in member.DescendantNodesAndSelf())
            {
                if (node is ParameterSyntax parameter && IsState(parameter.Type?.ToString()))
                    states.Add(parameter.Identifier.ValueText);
                else if (node is VariableDeclaratorSyntax variable && variable.Parent is VariableDeclarationSyntax declaration &&
                    (IsState(declaration.Type.ToString()) ||
                        variable.Initializer?.Value is ObjectCreationExpressionSyntax creation && IsState(creation.Type.ToString()) ||
                        // An alias such as `ref var operationState = ref reader` names the same state.
                        (variable.Initializer?.Value is RefExpressionSyntax alias ? alias.Expression : variable.Initializer?.Value) is IdentifierNameSyntax source &&
                        states.Contains(source.Identifier.ValueText)))
                    states.Add(variable.Identifier.ValueText);
            }
            return states;
        }
    }

    /// <summary>Rewrites the cursor uses of one member.</summary>
    private sealed class Cursors : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _states;
        private readonly bool _legacyPointer;

        /// <summary>Captures the cursor representation and state aliases for this member.</summary>
        /// <param name="states">The state names in this member.</param>
        /// <param name="legacyPointer">Whether the cursor is a pointer in an older Core reference.</param>
        internal Cursors(HashSet<string> states, bool legacyPointer)
        {
            _states = states;
            _legacyPointer = legacyPointer;
        }

        /// <summary>Gets the state of a <c>state.Cursor</c> access.</summary>
        /// <param name="expression">The expression that may access a cursor field.</param>
        /// <returns>The state identifier, or null for other expressions.</returns>
        private string? State(ExpressionSyntax? expression) =>
            expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Cursor" } access &&
            _states.Contains(access.Expression.ToString()) ? access.Expression.ToString() : null;

        /// <summary>Builds a managed reference to the cursor's byte.</summary>
        /// <param name="state">The state holding a cursor.</param>
        /// <returns>The corresponding expression in generated source.</returns>
        private string Reference(string state) => _legacyPointer
            ? Unsafe + ".Add(ref " + Marshal + ".GetReference(" + state + ".Buffer), (nint)(uint)" + state + ".Position)"
            : Marshal + ".GetReference(" + state + ".Cursor)";

        /// <inheritdoc />
        public override SyntaxNode? VisitRefExpression(RefExpressionSyntax node)
        {
            // A reference to the cursor, such as a ref local initializer or a ref-assigned value.
            if (State(node.Expression) is { } state)
                return node.WithExpression(Parse(Reference(state), node.Expression));
            return base.VisitRefExpression(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitArgument(ArgumentSyntax node)
        {
            if (node.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || node.RefKindKeyword.IsKind(SyntaxKind.InKeyword))
                if (State(node.Expression) is { } state)
                    return node.WithExpression(Parse(Reference(state), node.Expression));
            return base.VisitArgument(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            if (State(node.Left) is { } state)
            {
                if (node.Right is RefExpressionSyntax target)
                {
                    var visited = (ExpressionSyntax)Visit(target.Expression)!;
                    if (!_legacyPointer)
                        return Parse(state + ".Cursor = " + Marshal + ".CreateSpan(ref " + visited + ", 1)", node);
                    return Parse(state + ".Position = (int)" + Unsafe + ".ByteOffset(ref " + Marshal + ".GetReference(" + state + ".Buffer), ref " + visited + ")", node);
                }
                // A store through the cursor writes its byte.
                var value = (ExpressionSyntax)Visit(node.Right)!;
                return node.WithLeft(Parse(Reference(state), node.Left)).WithRight(value);
            }
            return base.VisitAssignmentExpression(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // Any remaining cursor use reads its byte.
            if (State(node) is { } state)
                return Parse(Reference(state), node);
            return base.VisitMemberAccessExpression(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            // Without numeric IntPtr an IntPtr result does not compare with integer literals; the identity cast to nint does.
            if (visited.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ByteOffset" } offset &&
                offset.Expression.ToString().EndsWith("Unsafe", StringComparison.Ordinal) && node.Parent is BinaryExpressionSyntax)
                return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(SyntaxFactory.ParseTypeName("nint"), visited.WithoutTrivia()))
                    .WithTriviaFrom(node);
            return visited;
        }

        /// <summary>Parses a replacement expression with the original trivia.</summary>
        /// <param name="text">The projected expression text.</param>
        /// <param name="original">The expression whose trivia is preserved.</param>
        /// <returns>The parsed expression.</returns>
        private static ExpressionSyntax Parse(string text, SyntaxNode original) =>
            SyntaxFactory.ParseExpression(text).WithTriviaFrom(original);
    }
}
