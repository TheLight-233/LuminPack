using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Projects the managed-reference cursor of the generated text onto the pointer cursor of compatibility targets.</summary>
/// <remarks>On netstandard2.1 the advancing states expose <c>byte* Cursor</c> instead of <c>ref byte Cursor</c>, with managed input
/// kept pinned by the caller. This ports the original <c>BinaryCursorProjection</c> to the statement shapes Next emits: a cursor
/// reference becomes <c>Unsafe.AsRef&lt;byte&gt;(state.Cursor)</c>, a cursor store or load dereferences the pointer, and a cursor
/// re-assignment becomes pointer arithmetic. Methods that use the pointer gain the <c>unsafe</c> modifier. A consumer that
/// disallows unsafe code addresses the same byte through <c>Buffer</c> and <c>Position</c> instead. Text without a state cursor is
/// returned unchanged.</remarks>
internal static class PointerCursorProjection
{
    private const string Unsafe = "global::System.Runtime.CompilerServices.Unsafe";
    private const string Marshal = "global::System.Runtime.InteropServices.MemoryMarshal";

    private static readonly System.Text.RegularExpressions.Regex ScopedModifier = new(@"\bscoped\s+(?=(?:ref|in)\b)",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Projects one generated file.</summary>
    /// <param name="source">The generated text.</param>
    /// <param name="allowUnsafe">Whether the consumer allows unsafe code.</param>
    /// <param name="numericIntPtr">Whether <c>IntPtr</c> compares as <c>nint</c>, which needs C# 11 and a runtime with numeric
    /// IntPtr; compatibility targets lack the latter, so <c>Unsafe.ByteOffset</c> results are cast to <c>nint</c>.</param>
    /// <returns>The projected text, or the original text when it uses no state cursor.</returns>
    internal static string Apply(string source, bool allowUnsafe, bool numericIntPtr)
    {
        // Compatibility targets declare no `scoped` parameters, as payload files without .NET 8 already omit them.
        source = ScopedModifier.Replace(source, string.Empty);
        bool readers = source.IndexOf("Reader(ref ", StringComparison.Ordinal) >= 0;
        if (!readers && source.IndexOf(".Cursor", StringComparison.Ordinal) < 0 &&
            (numericIntPtr || source.IndexOf("ByteOffset(", StringComparison.Ordinal) < 0))
            return source;
        SyntaxNode root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)).GetRoot();
        SyntaxNode projected = new Methods(allowUnsafe, numericIntPtr).Visit(root);
        if (readers)
            projected = new PinnedReaders(allowUnsafe).Visit(projected);
        return ReferenceEquals(projected, root) ? source : projected.ToFullString();
    }

    /// <summary>Applies only the input pinning of reader construction, for text that cannot see the compiler settings.</summary>
    /// <param name="source">The generated text.</param>
    /// <returns>The text whose readers read a stationary pooled copy of managed input.</returns>
    internal static string PinReaders(string source)
    {
        if (source.IndexOf("Reader(ref ", StringComparison.Ordinal) < 0)
            return source;
        SyntaxNode root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)).GetRoot();
        return new PinnedReaders(allowUnsafe: false).Visit(root).ToFullString();
    }

    /// <summary>Keeps managed input stationary for the lifetime of a pointer-cursor reader, as the original
    /// <c>PinnedReaderProjection</c> did: a fixed region when unsafe code is allowed, otherwise a pooled pinned copy.</summary>
    private sealed class PinnedReaders : CSharpSyntaxRewriter
    {
        private readonly bool _allowUnsafe;
        private bool _pinned;
        private bool _async;
        private int _next;

        internal PinnedReaders(bool allowUnsafe) => _allowUnsafe = allowUnsafe;

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            bool previous = _pinned, previousAsync = _async;
            _pinned = false;
            _async = node.Modifiers.Any(SyntaxKind.AsyncKeyword);
            var result = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
            if (_pinned && !result.Modifiers.Any(SyntaxKind.UnsafeKeyword))
                result = result.AddModifiers(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword).WithTrailingTrivia(SyntaxFactory.Space));
            _pinned = previous;
            _async = previousAsync;
            return result;
        }

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            bool previous = _pinned, previousAsync = _async;
            _pinned = false;
            _async = node.Modifiers.Any(SyntaxKind.AsyncKeyword);
            var result = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
            if (_pinned && !result.Modifiers.Any(SyntaxKind.UnsafeKeyword))
                result = result.AddModifiers(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword).WithTrailingTrivia(SyntaxFactory.Space));
            _pinned = previous;
            _async = previousAsync;
            return result;
        }

        /// <summary>Wraps the rest of a block after a reader over `ref input` in the input's stationary lifetime.</summary>
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
                StatementSyntax lifetime;
                if (_allowUnsafe)
                {
                    string stationary = identity + "Span";
                    var stationaryCreation = creation.WithArgumentList(creation.ArgumentList.WithArguments(arguments.Replace(arguments[0],
                        arguments[0].WithExpression(SyntaxFactory.IdentifierName(stationary)))));
                    var statements = new List<StatementSyntax>
                    {
                        SyntaxFactory.ParseStatement("var " + stationary + " = new global::System.ReadOnlySpan<byte>(" + identity + ", " + input + ".Length);"),
                        declaration.ReplaceNode(creation, stationaryCreation),
                    };
                    statements.AddRange(rest);
                    var pinned = (FixedStatementSyntax)SyntaxFactory.ParseStatement("fixed (byte* " + identity + " = " + input + ") { }");
                    lifetime = pinned.WithStatement(SyntaxFactory.Block(statements));
                    if (_async)
                        lifetime = SyntaxFactory.UnsafeStatement(SyntaxFactory.Block(lifetime));
                    else
                        _pinned = true;
                }
                else
                {
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
                    lifetime = SyntaxFactory.Block(SyntaxFactory.ParseStatement("var " + owner + " = global::LuminPack.Utility.LuminBufferWriterPool.Rent();"),
                        SyntaxFactory.TryStatement(SyntaxFactory.Block(statements), default, cleanup));
                }
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
        private readonly bool _allowUnsafe;
        private readonly bool _numericIntPtr;

        internal Methods(bool allowUnsafe, bool numericIntPtr)
        {
            _allowUnsafe = allowUnsafe;
            _numericIntPtr = numericIntPtr;
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var states = States(node);
            var cursor = new Cursors(states, _allowUnsafe && !node.Modifiers.Any(SyntaxKind.AsyncKeyword), _numericIntPtr);
            var result = (MethodDeclarationSyntax)cursor.Visit(node);
            if (cursor.UsedPointer && !result.Modifiers.Any(SyntaxKind.UnsafeKeyword))
                result = result.WithModifiers(result.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword).WithTrailingTrivia(SyntaxFactory.Space)));
            return result;
        }

        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var cursor = new Cursors(States(node), _allowUnsafe, _numericIntPtr);
            var result = (ConstructorDeclarationSyntax)cursor.Visit(node);
            if (cursor.UsedPointer && !result.Modifiers.Any(SyntaxKind.UnsafeKeyword))
                result = result.WithModifiers(result.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword).WithTrailingTrivia(SyntaxFactory.Space)));
            return result;
        }

        /// <summary>Collects the parameters and locals of a member that hold an advancing state.</summary>
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
        private readonly bool _pointer;
        private readonly bool _numericIntPtr;
        internal bool UsedPointer;

        internal Cursors(HashSet<string> states, bool pointer, bool numericIntPtr)
        {
            _states = states;
            _pointer = pointer;
            _numericIntPtr = numericIntPtr;
        }

        /// <summary>Gets the state of a <c>state.Cursor</c> access.</summary>
        private string? State(ExpressionSyntax? expression) =>
            expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Cursor" } access &&
            _states.Contains(access.Expression.ToString()) ? access.Expression.ToString() : null;

        /// <summary>A managed reference to the cursor's byte.</summary>
        private string Reference(string state)
        {
            if (_pointer)
            {
                UsedPointer = true;
                return Unsafe + ".AsRef<byte>(" + state + ".Cursor)";
            }
            return Unsafe + ".Add(ref " + Marshal + ".GetReference(" + state + ".Buffer), (nint)(uint)" + state + ".Position)";
        }

        public override SyntaxNode? VisitRefExpression(RefExpressionSyntax node)
        {
            // A reference to the cursor, such as a ref local initializer or a ref-assigned value.
            if (State(node.Expression) is { } state)
                return node.WithExpression(Parse(Reference(state), node.Expression));
            return base.VisitRefExpression(node);
        }

        public override SyntaxNode? VisitArgument(ArgumentSyntax node)
        {
            if (node.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || node.RefKindKeyword.IsKind(SyntaxKind.InKeyword))
                if (State(node.Expression) is { } state)
                    return node.WithExpression(Parse(Reference(state), node.Expression));
            return base.VisitArgument(node);
        }

        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            if (State(node.Left) is { } state)
            {
                if (node.Right is RefExpressionSyntax target)
                {
                    // An advance over the cursor itself is pointer arithmetic; another target is converted to an address or offset.
                    if (target.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Add" } add } advance &&
                        add.Expression.ToString().EndsWith("Unsafe", StringComparison.Ordinal) && advance.ArgumentList.Arguments.Count == 2 &&
                        State(advance.ArgumentList.Arguments[0].Expression) == state)
                    {
                        var count = (ExpressionSyntax)Visit(advance.ArgumentList.Arguments[1].Expression)!;
                        if (_pointer)
                        {
                            UsedPointer = true;
                            return Parse(state + ".Cursor += (nint)(" + count + ")", node);
                        }
                        return Parse(state + ".Position += (int)(" + count + ")", node);
                    }
                    var visited = (ExpressionSyntax)Visit(target.Expression)!;
                    if (_pointer)
                    {
                        UsedPointer = true;
                        return Parse(state + ".Cursor = (byte*)" + Unsafe + ".AsPointer(ref " + visited + ")", node);
                    }
                    return Parse(state + ".Position = (int)" + Unsafe + ".ByteOffset(ref " + Marshal + ".GetReference(" + state + ".Buffer), ref " + visited + ")", node);
                }
                // A store through the cursor writes its byte.
                var value = (ExpressionSyntax)Visit(node.Right)!;
                return node.WithLeft(Parse(Load(state), node.Left)).WithRight(value);
            }
            return base.VisitAssignmentExpression(node);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // Any remaining cursor use reads its byte.
            if (State(node) is { } state)
                return Parse(Load(state), node);
            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            // Without numeric IntPtr an IntPtr result does not compare with integer literals; the identity cast to nint does.
            if (!_numericIntPtr && visited.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ByteOffset" } offset &&
                offset.Expression.ToString().EndsWith("Unsafe", StringComparison.Ordinal) && node.Parent is BinaryExpressionSyntax)
                return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(SyntaxFactory.ParseTypeName("nint"), visited.WithoutTrivia()))
                    .WithTriviaFrom(node);
            return visited;
        }

        /// <summary>The cursor's byte as an assignable value.</summary>
        private string Load(string state)
        {
            if (_pointer)
            {
                UsedPointer = true;
                return "(*" + state + ".Cursor)";
            }
            return Reference(state);
        }

        private static ExpressionSyntax Parse(string text, SyntaxNode original) =>
            SyntaxFactory.ParseExpression(text).WithTriviaFrom(original);
    }
}
