using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Lowers absolute-position templates without exposing mutable integer storage from binary states.</summary>
internal sealed class BinaryCursorProjection : CSharpSyntaxRewriter
{
    private readonly bool _span;
    private readonly bool _pointer;

    /// <summary>Reads the advancing cursor representation from the selected Core reference.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">The fallback framework facts when the reference is missing.</param>
    internal BinaryCursorProjection(Compilation compilation, global::LuminPack.SourceGenerator.MetaInfo metadata)
    {
        _span = CursorField(compilation)?.Type is INamedTypeSymbol { Name: "Span", Arity: 1 };
        _pointer = UsesPointerCursor(compilation, metadata);
    }

    /// <summary>Identifies a Core reference with a legacy pointer cursor.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">The fallback framework facts when the reference is missing.</param>
    /// <returns>Whether the selected Core reference requires stationary input.</returns>
    internal static bool UsesPointerCursor(Compilation compilation, global::LuminPack.SourceGenerator.MetaInfo metadata)
        => CursorField(compilation) is { } field ? field.Type is IPointerTypeSymbol : !metadata.IsNet8;

    /// <summary>Identifies the targets on which writers can reassign the cursor by reference.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">The fallback framework facts when the reference is missing.</param>
    /// <returns>Whether the field itself is a managed reference.</returns>
    internal static bool UsesRefCursor(Compilation compilation, global::LuminPack.SourceGenerator.MetaInfo metadata)
        => CursorField(compilation) is { } field ? field.RefKind == RefKind.Ref : metadata.IsNet8;

    /// <summary>Finds the cursor field in the selected Core reference.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <returns>The field, when Core is referenced.</returns>
    private static IFieldSymbol CursorField(Compilation compilation)
        => compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter")?.GetMembers("Cursor").OfType<IFieldSymbol>().FirstOrDefault();

    /// <summary>Rewrites binary operation methods while leaving JSON's independent state untouched.</summary>
    /// <param name="node">One generated method.</param>
    /// <returns>The operation using the binary state's advancing cursor.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Body == null)
            return node;
        var states = new HashSet<string>(StringComparer.Ordinal);
        var readers = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<LocalDeclarationStatementSyntax>();
        foreach (var descendant in node.DescendantNodes())
        {
            if (descendant is ParameterSyntax parameter)
            {
                string type = parameter.Type?.ToString();
                if (IsBinary(type))
                    states.Add(parameter.Identifier.ValueText);
                if (IsBinaryReader(type))
                    readers.Add(parameter.Identifier.ValueText);
            }
            else if (descendant is VariableDeclaratorSyntax variable)
            {
                string type = (variable.Parent as VariableDeclarationSyntax)?.Type.ToString();
                if (IsBinary(type) || variable.Initializer?.Value is ObjectCreationExpressionSyntax creation && IsBinary(creation.Type.ToString()))
                    states.Add(variable.Identifier.ValueText);
                if (IsBinaryReader(type))
                    readers.Add(variable.Identifier.ValueText);
            }
            else if (descendant is LocalDeclarationStatementSyntax { Declaration.Type: RefTypeSyntax } declaration)
                candidates.Add(declaration);
        }
        if (states.Count == 0)
            return node;
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var declaration in candidates)
            if (declaration.Declaration.Type is RefTypeSyntax && declaration.Declaration.Variables.Count == 1 &&
                declaration.Declaration.Variables[0].Initializer?.Value is RefExpressionSyntax reference &&
                reference.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } &&
                access.Name.Identifier.ValueText == "GetCurrentSpanOffset" && states.Contains(access.Expression.ToString()))
                aliases[declaration.Declaration.Variables[0].Identifier.ValueText] = access.Expression.ToString();
        var positions = new Positions(states, aliases, _pointer, _span);
        var body = (BlockSyntax)positions.Visit(node.Body);
        body = (BlockSyntax)new BinaryHeaderProjection(states).Visit(body);
        body = (BlockSyntax)new Arguments(states).Visit(body);
        // Runs after header fusion, which still recognizes the original advance statements.
        var advances = new Advances(readers, _pointer, _span);
        return node.WithBody((BlockSyntax)advances.Visit(body));
    }

    /// <summary>Recognizes the binary state family independently from source qualification.</summary>
    /// <param name="name">The emitted type spelling.</param>
    /// <returns>Whether this type owns a binary advancing cursor.</returns>
    private static bool IsBinary(string name) => name?.Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty)
        is "LuminPackWriter" or "LuminPackReader" or "LuminPackJsonWriter" or "LuminPackJsonReader";

    /// <summary>Recognizes the binary Reader, whose lowered advances retain all-build input validation.</summary>
    /// <param name="name">The emitted type spelling.</param>
    /// <returns>Whether this type is the binary Reader.</returns>
    private static bool IsBinaryReader(string name) => name?.Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty) is "LuminPackReader";

    /// <summary>Replaces compiler-owned aliases with the actual cursor position.</summary>
    private sealed class Positions : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _states;
        private readonly Dictionary<string, string> _aliases;
        private readonly bool _pointer;
        private readonly bool _span;

        /// <summary>Captures this operation's state and unique generated aliases.</summary>
        /// <param name="states">Binary receiver identities.</param>
        /// <param name="aliases">Generated aliases and their owning receivers.</param>
        /// <param name="pointer">Whether direct storage uses a native pointer.</param>
        /// <param name="span">Whether the cursor is stored in a one-element span.</param>
        internal Positions(HashSet<string> states, Dictionary<string, string> aliases, bool pointer, bool span)
        {
            _states = states;
            _aliases = aliases;
            _pointer = pointer;
            _span = span;
        }

        /// <summary>Removes obsolete byref declarations after replacing their uses.</summary>
        /// <param name="node">The generated declaration.</param>
        /// <returns>The retained declaration or no declaration for an eliminated cursor alias.</returns>
        public override SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
            => node.Declaration.Type is RefTypeSyntax && node.Declaration.Variables.Count == 1 &&
                _aliases.ContainsKey(node.Declaration.Variables[0].Identifier.ValueText) ? null : base.VisitLocalDeclarationStatement(node);

        /// <summary>Reads and assigns the real state position instead of a shadow integer.</summary>
        /// <param name="node">A generated alias use.</param>
        /// <returns>The advancing state's position or the original identifier.</returns>
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (!_aliases.TryGetValue(node.Identifier.ValueText, out string state) ||
                node.Parent is MemberAccessExpressionSyntax member && member.Name == node ||
                node.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax or NameColonSyntax)
                return base.VisitIdentifierName(node);
            return SyntaxFactory.ParseExpression(state + ".Position").WithTriviaFrom(node);
        }

        /// <summary>Replaces old offset getters, including direct assignments and byref arguments.</summary>
        /// <param name="node">An emitted cursor accessor.</param>
        /// <returns>The binary position property or the original invocation.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax access && _states.Contains(access.Expression.ToString()) &&
                access.Name.Identifier.ValueText is "GetCurrentSpanOffset" or "GetCurrentSpanIndex")
                return SyntaxFactory.ParseExpression(access.Expression + ".Position").WithTriviaFrom(node);
            var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            if (rewritten.Expression is MemberAccessExpressionSyntax current)
            {
                string state = current.Expression.ToString();
                string name = current.Name.Identifier.ValueText;
                string cursor = _span ? "global::System.Runtime.InteropServices.MemoryMarshal.GetReference(" + state + ".Cursor)" : _pointer
                    ? "global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(" + state + ".Buffer), (nint)(uint)" + state + ".Position)"
                    : state + ".Cursor";
                if (_states.Contains(state))
                {
                    if (name == "GetCurrentSpanReference" || name == "GetSpanReference" && rewritten.ArgumentList.Arguments.Count == 1 &&
                        rewritten.ArgumentList.Arguments[0].Expression.ToString() == state + ".Position")
                    {
                        return SyntaxFactory.ParseExpression(cursor).WithTriviaFrom(node);
                    }
                    if (name == "GetSpanReference" && rewritten.ArgumentList.Arguments.Count == 1)
                        return SyntaxFactory.ParseExpression("global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(" + state + ".Buffer), (nint)(uint)(" + rewritten.ArgumentList.Arguments[0].Expression + "))").WithTriviaFrom(node);
                    if (name == "FlushCurrentIndex" && rewritten.ArgumentList.Arguments.Count == 1)
                        return SyntaxFactory.ParseExpression(state + ".Position = " + rewritten.ArgumentList.Arguments[0].Expression).WithTriviaFrom(node);
                }
                if (state is "global::LuminPack.Core.LuminPackWriter" or "global::LuminPack.Core.LuminPackReader" &&
                    name is "Write" or "Read" && rewritten.ArgumentList.Arguments.Count == 2 &&
                    _states.Contains(rewritten.ArgumentList.Arguments[0].Expression.ToString()))
                    return rewritten.WithExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            rewritten.ArgumentList.Arguments[0].Expression, current.Name))
                        .WithArgumentList(rewritten.ArgumentList.WithArguments(rewritten.ArgumentList.Arguments.RemoveAt(0)));
            }
            return rewritten;
        }
    }

    /// <summary>Spells Reader fixed advances as checked cursor arithmetic after all statement-pattern projections.</summary>
    private sealed class Advances : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _readers;
        private readonly bool _pointer;
        private readonly bool _span;

        /// <summary>Captures the Reader receivers and the target cursor representation.</summary>
        /// <param name="readers">The binary Reader receivers.</param>
        /// <param name="pointer">Whether the cursor is a native pointer.</param>
        /// <param name="span">Whether the cursor is stored in a one-element span.</param>
        internal Advances(HashSet<string> readers, bool pointer, bool span)
        {
            _readers = readers;
            _pointer = pointer;
            _span = span;
        }

        /// <summary>Retains the Reader's range check before lowering a fixed advance to cursor arithmetic.</summary>
        /// <param name="node">A generated statement.</param>
        /// <returns>The lowered advance, or the visited statement.</returns>
        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            var result = (ExpressionStatementSyntax)base.VisitExpressionStatement(node);
            if (result.Expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Advance" } access } call ||
                call.ArgumentList.Arguments.Count != 1 || call.ArgumentList.Arguments[0].RefKindKeyword.RawKind != 0 ||
                !_readers.Contains(access.Expression.ToString()) || _pointer)
                return result;
            var count = call.ArgumentList.Arguments[0].Expression;
            // Only side-effect-free widths are lowered, so the cursor is still read after the width is known.
            if (!IsPure(count))
                return result;
            string state = access.Expression.ToString();
            string offset = count is LiteralExpressionSyntax ? count.WithoutTrivia().ToString() : "(nint)(uint)(" + count.WithoutTrivia() + ")";
            const string marshal = "global::System.Runtime.InteropServices.MemoryMarshal";
            const string unsafeType = "global::System.Runtime.CompilerServices.Unsafe";
            string cursor = state + ".Cursor";
            string update = _span
                ? cursor + " = " + marshal + ".CreateSpan(ref " + unsafeType + ".Add(ref " + marshal + ".GetReference(" + cursor + "), " + offset + "), 1)"
                : cursor + " = ref " + unsafeType + ".Add(ref " + cursor + ", " + offset + ")";
            var advance = result.WithExpression(SyntaxFactory.ParseExpression(update));
            var check = SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, access.Expression,
                    SyntaxFactory.IdentifierName("EnsureReadable")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(count.WithoutTrivia())))));
            return SyntaxFactory.Block(check, advance.WithoutTrivia()).WithTriviaFrom(result);
        }

        /// <summary>Checks a range at the Reader's own cursor against its cached end instead of recomputing the position.</summary>
        /// <param name="node">A generated range check.</param>
        /// <returns>The cursor-relative check for <c>EnsureReadable(reader.Position, count)</c>, or the visited invocation.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var result = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            if (result.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "EnsureReadable" } access &&
                _readers.Contains(access.Expression.ToString()) && result.ArgumentList.Arguments.Count == 2 &&
                result.ArgumentList.Arguments[0].Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Position" } position &&
                position.Expression.ToString() == access.Expression.ToString())
                return result.WithArgumentList(result.ArgumentList.WithArguments(result.ArgumentList.Arguments.RemoveAt(0)));
            return result;
        }

        /// <summary>Accepts constant widths built from literals, locals and storage sizes.</summary>
        /// <param name="expression">The advance width.</param>
        /// <returns>Whether evaluating the width cannot move or observe the cursor.</returns>
        private static bool IsPure(ExpressionSyntax expression) => expression.DescendantNodesAndSelf().All(node => node switch
        {
            InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments.Count == 0 &&
                invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "SizeOf" } } sizeOf &&
                sizeOf.Expression.ToString().EndsWith("Unsafe", StringComparison.Ordinal),
            // The checked lowering evaluates a width twice; an arbitrary property getter must retain Advance's single evaluation.
            MemberAccessExpressionSyntax member => member.Ancestors().OfType<InvocationExpressionSyntax>().Any(invocation =>
                invocation.ArgumentList.Arguments.Count == 0 && invocation.Expression is MemberAccessExpressionSyntax
                    { Name: GenericNameSyntax { Identifier.ValueText: "SizeOf" } } sizeOf &&
                sizeOf.Expression.ToString().EndsWith("Unsafe", StringComparison.Ordinal)),
            AssignmentExpressionSyntax or PostfixUnaryExpressionSyntax or ArgumentSyntax => false,
            PrefixUnaryExpressionSyntax prefix => !prefix.IsKind(SyntaxKind.PreIncrementExpression) && !prefix.IsKind(SyntaxKind.PreDecrementExpression),
            _ => true
        });
    }

    /// <summary>Passes copied absolute offsets to legacy implementation kernels, without exposing state addresses.</summary>
    private sealed class Arguments : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _states;
        private int _next;

        /// <summary>Captures this operation's binary receiver identities.</summary>
        /// <param name="states">Binary states whose positions cannot escape by reference.</param>
        internal Arguments(HashSet<string> states)
        {
            _states = states; }

        /// <summary>Introduces copied offsets only at old absolute-position implementation boundaries.</summary>
        /// <param name="node">The current generated statement block.</param>
        /// <returns>A block with cursor-owning string and Union calls and nonescaping absolute offsets.</returns>
        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            var statements = new List<StatementSyntax>();
            foreach (var original in node.Statements)
            {
                var statement = (StatementSyntax)Visit(original);
                while (true)
                {
                    var reference = statement.DescendantNodes(descendIntoChildren: child => child is not BlockSyntax &&
                        child is not LocalFunctionStatementSyntax && child is not AnonymousFunctionExpressionSyntax)
                        .OfType<ArgumentSyntax>().FirstOrDefault(argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) &&
                            argument.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Position" } position &&
                            _states.Contains(position.Expression.ToString()));
                    if (reference == null)
                        break;
                    if (reference.Parent?.Parent is InvocationExpressionSyntax call && call.Expression is MemberAccessExpressionSyntax access)
                    {
                        string name = access.Name.Identifier.ValueText;
                        if (name is "ReadStringAndAdvance" or "ReadStringListAndAdvance" or "TryPeekUnionHeader" or "TryPeekWideUnionHeader")
                        {
                            string replacement = name == "ReadStringAndAdvance" ? "ReadStringRecord" :
                                name == "ReadStringListAndAdvance" ? "ReadStringListRecord" : name;
                            SimpleNameSyntax target = access.Name is GenericNameSyntax generic ? generic.WithIdentifier(SyntaxFactory.Identifier(replacement)) : SyntaxFactory.IdentifierName(replacement);
                            var updated = call.WithExpression(access.WithName(target)).WithArgumentList(call.ArgumentList.WithArguments(call.ArgumentList.Arguments.Remove(reference)));
                            statement = statement.ReplaceNode(call, updated);
                            continue;
                        }
                    }
                    string temporary = "__luminPosition" + _next++;
                    statements.Add(SyntaxFactory.ParseStatement("int " + temporary + " = " + reference.Expression + ";"));
                    statement = statement.ReplaceNode(reference, reference.WithExpression(SyntaxFactory.IdentifierName(temporary)));
                }
                statements.Add(statement);
            }
            return node.WithStatements(SyntaxFactory.List(statements));
        }
    }
}
