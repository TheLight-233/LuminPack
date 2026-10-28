using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Gives outlined binary loops a nonescaping local cursor that the JIT can promote independently of their caller.</summary>
/// <remarks>
/// A copied state only pays for itself while the JIT can keep it in registers. Passing it by reference to a call
/// that is not inlined exposes the complete local, so a loop containing such a call keeps the caller's state and
/// only escape-free loops are localized, using the narrowest copy their accesses require.
/// </remarks>
internal sealed class RootStateLocalization : CSharpSyntaxRewriter
{
    /// <summary>Statement count up to which a localized root is inlined into its forwarding shell.</summary>
    private const int InlineStatementLimit = 48;

    /// <summary>How a state's references leave the JIT's view of the current method.</summary>
    private enum Escape
    {
        /// <summary>Every reference is a field access, an inlined state member or a by-value copy.</summary>
        None,
        /// <summary>A not-inlined call outside every loop; a field-wise copy isolates it once.</summary>
        Cold,
        /// <summary>A not-inlined call inside a loop, or a reference that cannot be isolated.</summary>
        Hot
    }

    private readonly string _source, _local, _complete, _type;
    private readonly Dictionary<string, LocalFunctionStatementSyntax> _helpers;
    private readonly HashSet<string> _used;
    private readonly bool _shared;
    private int _next;
    private bool _returns;

    /// <summary>Captures the owned state and collision-free local identities.</summary>
    /// <param name="source">The root's original state parameter.</param>
    /// <param name="local">The nonescaping execution state, or the source itself for a shared inlined helper.</param>
    /// <param name="complete">The successful completion label outside resource scopes.</param>
    /// <param name="type">The root's binary state type.</param>
    /// <param name="helpers">Local helpers accepting the same state, keyed by name.</param>
    /// <param name="used">Identifiers already present in the complete root.</param>
    /// <param name="shared">Whether this body is an inlined helper operating on its caller's localized state.</param>
    private RootStateLocalization(string source, string local, string complete, string type, Dictionary<string, LocalFunctionStatementSyntax> helpers,
        HashSet<string> used, bool shared)
    { _source = source; _local = local; _complete = complete; _type = type; _helpers = helpers; _used = used; _shared = shared; }

    /// <summary>Localizes binary cursor state around outlined operations; JSON retains pooled token scratch in its original state.</summary>
    /// <param name="method">The complete root before localization.</param>
    /// <returns>The same root with field-wise entry and successful completion copies where they keep the state in registers.</returns>
    internal static MethodDeclarationSyntax Apply(MethodDeclarationSyntax method)
    {
        var state = method.ParameterList.Parameters[0];
        string type = state.Type.ToString();
        string payload = method.ParameterList.Parameters[1].Type.ToString();
        bool collection = payload.StartsWith("global::System.", System.StringComparison.Ordinal) || payload.Contains("[");
        if (type.Contains("Json") || !collection || method.Body == null || !method.Body.DescendantNodes().Any(IsLoop)) return method;
        string Unique(string name)
        {
            while (method.Body.DescendantTokens().Any(token => token.ValueText == name)) name += "_";
            return name;
        }
        string source = state.Identifier.ValueText;
        var helpers = Helpers(method.Body, type);
        var used = new HashSet<string>(method.Body.DescendantTokens().Select(token => token.ValueText));
        BlockSyntax body;
        if (Classify(method.Body, source, false, helpers, new HashSet<string>()) == Escape.Hot)
            body = LocalizeLoops(method.Body, source, type, helpers, used);
        else
        {
            string local = Unique("operationState"), complete = Unique("operationComplete_" + method.Identifier.ValueText);
            var rewrite = new RootStateLocalization(source, local, complete, type, helpers, used, false);
            var statements = new List<StatementSyntax>
            {
                SyntaxFactory.ParseStatement(type + " " + local + " = default;"),
                SyntaxFactory.ParseStatement(source + ".CopyTo(ref " + local + ");"),
                (BlockSyntax)rewrite.Visit(method.Body)
            };
            if (rewrite._returns) statements.Add(SyntaxFactory.LabeledStatement(complete, SyntaxFactory.EmptyStatement()));
            statements.Add(SyntaxFactory.ParseStatement(local + ".CopyTo(ref " + source + ");"));
            body = SyntaxFactory.Block(statements);
        }
        var attributes = method.AttributeLists.Select(list => list.WithAttributes(SyntaxFactory.SeparatedList(list.Attributes.Where(attribute =>
            !attribute.Name.ToString().EndsWith("MethodImpl", System.StringComparison.Ordinal))))).Where(list => list.Attributes.Count != 0);
        return method.WithBody(body).WithAttributeLists(SyntaxFactory.List(attributes))
            .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::System.Runtime.CompilerServices.MethodImpl"),
                SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                    SyntaxFactory.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions." +
                        (InlinedSize(body) <= InlineStatementLimit && !Enumerates(body) ? "AggressiveInlining" : "NoInlining")))))))));
    }

    /// <summary>Recognizes a root that traverses through an enumerator.</summary>
    /// <param name="body">The complete localized root body.</param>
    /// <returns>Whether the root or one of its inlined helpers contains a <c>foreach</c> loop.</returns>
    /// <remarks>A forced inline places the root in its small forwarding shell, whose inlining budget is then exhausted
    /// before the enumerator's MoveNext and Current, which become calls for every element. Compiled on its own, the
    /// root inlines them; the single call per collection is outside the element loop.</remarks>
    private static bool Enumerates(BlockSyntax body)
        => body.DescendantNodes(node => node is not LocalFunctionStatementSyntax helper || IsInlined(helper)).OfType<CommonForEachStatementSyntax>().Any();

    /// <summary>Collects local helpers whose first parameter is the root's state type.</summary>
    /// <param name="body">The complete root body.</param>
    /// <param name="type">The root's binary state type.</param>
    /// <returns>The helpers keyed by name.</returns>
    private static Dictionary<string, LocalFunctionStatementSyntax> Helpers(BlockSyntax body, string type)
    {
        string Normalize(string name) => name.Replace("global::", "").Replace("LuminPack.Core.", "");
        var helpers = new Dictionary<string, LocalFunctionStatementSyntax>();
        foreach (var helper in body.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            if (helper.ParameterList.Parameters.Count != 0 && Normalize(helper.ParameterList.Parameters[0].Type.ToString()) == Normalize(type))
                helpers[helper.Identifier.ValueText] = helper;
        return helpers;
    }

    /// <summary>Recognizes helpers the JIT inlines into their caller, so their state parameter aliases the caller's local.</summary>
    /// <param name="helper">A local helper.</param>
    /// <returns>Whether the helper is forced inline and contains no exception-handling region.</returns>
    private static bool IsInlined(LocalFunctionStatementSyntax helper)
        => helper.Body != null && helper.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.ToString().Contains("AggressiveInlining")) &&
           !helper.Body.DescendantNodes(node => node is not LocalFunctionStatementSyntax).OfType<TryStatementSyntax>().Any();

    /// <summary>Recognizes loop statements.</summary>
    /// <param name="node">A syntax node.</param>
    /// <returns>Whether the node repeats its body.</returns>
    private static bool IsLoop(SyntaxNode node)
        => node is ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax or WhileStatementSyntax or DoStatementSyntax;

    /// <summary>Enumerates nodes of one method scope, excluding separately compiled local functions and lambdas.</summary>
    /// <param name="scope">The method body or statement being analyzed.</param>
    /// <returns>The scope's own descendants.</returns>
    private static IEnumerable<SyntaxNode> Own(SyntaxNode scope)
        => scope.DescendantNodes(node => node == scope || node is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax));

    /// <summary>Recognizes a reference to the state variable itself rather than a member or argument name.</summary>
    /// <param name="node">An identifier.</param>
    /// <param name="name">The state variable.</param>
    /// <returns>Whether the identifier denotes the state.</returns>
    private static bool IsStateReference(IdentifierNameSyntax node, string name)
        => node.Identifier.ValueText == name && !(node.Parent is MemberAccessExpressionSyntax access && access.Name == node) &&
           node.Parent is not (QualifiedNameSyntax or AliasQualifiedNameSyntax or NameColonSyntax or NameEqualsSyntax);

    /// <summary>Determines whether a node is repeated relative to its analyzed scope.</summary>
    /// <param name="node">A node inside the scope.</param>
    /// <param name="scope">The analyzed scope.</param>
    /// <returns>Whether a loop between them repeats the node.</returns>
    private static bool InLoop(SyntaxNode node, SyntaxNode scope)
    {
        for (var current = node.Parent; current != null && current != scope; current = current.Parent)
            if (IsLoop(current)) return true;
        return false;
    }

    /// <summary>Recognizes a call statement whose state argument can be isolated by field-wise copies around it.</summary>
    /// <param name="call">The escaping invocation.</param>
    /// <returns>Whether the call is a complete expression statement or the right side of one assignment statement.</returns>
    private static bool Isolatable(InvocationExpressionSyntax call)
        => call.Parent is ExpressionStatementSyntax ||
           call.Parent is AssignmentExpressionSyntax assignment && assignment.Right == call && assignment.Parent is ExpressionStatementSyntax;

    /// <summary>Finds the most severe escape of a state variable, following it into inlined helpers.</summary>
    /// <param name="scope">The body or loop being analyzed.</param>
    /// <param name="name">The state variable in that scope.</param>
    /// <param name="repeated">Whether the scope itself runs inside a caller's loop.</param>
    /// <param name="helpers">Local helpers accepting the state.</param>
    /// <param name="visiting">Inlined helpers on the current analysis path.</param>
    /// <returns>The escape classification.</returns>
    private static Escape Classify(SyntaxNode scope, string name, bool repeated, Dictionary<string, LocalFunctionStatementSyntax> helpers, HashSet<string> visiting)
    {
        var result = Escape.None;
        foreach (var node in Own(scope).OfType<IdentifierNameSyntax>())
        {
            if (!IsStateReference(node, name)) continue;
            bool loop = repeated || InLoop(node, scope);
            if (node.Parent is ArgumentSyntax argument)
            {
                if (argument.RefKindKeyword.IsKind(SyntaxKind.None)) continue;
                if (argument.Parent?.Parent is not InvocationExpressionSyntax call) return Escape.Hot;
                if (call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CopyTo" }) continue;
                string callee = call.Expression is SimpleNameSyntax simple ? simple.Identifier.ValueText : null;
                int position = ((ArgumentListSyntax)argument.Parent).Arguments.IndexOf(argument);
                if (callee != null && helpers.TryGetValue(callee, out var helper) && IsInlined(helper) &&
                    position < helper.ParameterList.Parameters.Count && visiting.Add(callee))
                {
                    var nested = Classify(helper.Body, helper.ParameterList.Parameters[position].Identifier.ValueText, loop, helpers, visiting);
                    visiting.Remove(callee);
                    if (nested > result) result = nested;
                    continue;
                }
                var escape = loop || !Isolatable(call) ? Escape.Hot : Escape.Cold;
                if (escape > result) result = escape;
                if (result == Escape.Hot) return result;
                continue;
            }
            if (node.Parent is RefExpressionSyntax || node.Parent is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.AddressOfExpression })
                return Escape.Hot;
        }
        return result;
    }

    /// <summary>Counts the statements a forced inline would copy into the caller.</summary>
    /// <param name="body">The final root body.</param>
    /// <returns>Statements outside separately compiled helpers.</returns>
    private static int InlinedSize(BlockSyntax body)
        => body.DescendantNodes(node => node is not LocalFunctionStatementSyntax helper || IsInlined(helper)).OfType<StatementSyntax>().Count();

    /// <summary>Keeps the caller's state for an escaping root and localizes only its escape-free loops.</summary>
    /// <param name="body">The root or helper body.</param>
    /// <param name="source">The state parameter.</param>
    /// <param name="type">The binary state type.</param>
    /// <param name="helpers">Local helpers accepting the state.</param>
    /// <param name="used">Identifiers already present in the complete root.</param>
    /// <returns>The body with independently localized helpers and loops.</returns>
    private static BlockSyntax LocalizeLoops(BlockSyntax body, string source, string type, Dictionary<string, LocalFunctionStatementSyntax> helpers, HashSet<string> used)
    {
        var loops = Own(body).Where(IsLoop).Where(loop => !loop.Ancestors().TakeWhile(node => node != body).Any(IsLoop))
            .Where(loop => Own(loop).OfType<IdentifierNameSyntax>().Any(node => IsStateReference(node, source)) &&
                Classify(loop, source, true, helpers, new HashSet<string>()) == Escape.None && !LeavesLoop(loop) &&
                !loop.DescendantNodes().Any(node => node is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) &&
                !Own(loop).OfType<InvocationExpressionSyntax>().Any(call => call.Expression is SimpleNameSyntax name && helpers.ContainsKey(name.Identifier.ValueText)))
            .ToList();
        var helperRewriter = new RootStateLocalization(source, source, null, type, helpers, used, true);
        return body.ReplaceNodes(loops.Cast<SyntaxNode>().Concat(Own(body).OfType<LocalFunctionStatementSyntax>()), (original, _) =>
        {
            if (original is LocalFunctionStatementSyntax function) return helperRewriter.VisitLocalFunctionStatement(function);
            var loop = (StatementSyntax)original;
            string Unique(string name)
            {
                while (!used.Add(name)) name += "_";
                return name;
            }
            var references = Own(loop).OfType<IdentifierNameSyntax>().Where(node => IsStateReference(node, source)).ToList();
            bool cursorOnly = references.All(node => node.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Cursor" } access &&
                access.Expression == node && access.Parent is not InvocationExpressionSyntax) &&
                references.Any(node => node.Parent.Parent is AssignmentExpressionSyntax { Right: RefExpressionSyntax } assignment && assignment.Left == node.Parent);
            if (cursorOnly)
            {
                // Only the cursor changes, so a ref local carries it and publishes once after the loop completes.
                string cursor = Unique("loopCursor");
                var hoisted = loop.ReplaceNodes(references.Select(node => node.Parent), (_, _) => SyntaxFactory.IdentifierName(cursor));
                return SyntaxFactory.Block(
                    SyntaxFactory.ParseStatement("ref byte " + cursor + " = ref " + source + ".Cursor;"),
                    hoisted,
                    SyntaxFactory.ParseStatement(source + ".Cursor = ref " + cursor + ";"));
            }
            string local = Unique("loopState");
            var copied = loop.ReplaceNodes(references, (node, _) => SyntaxFactory.IdentifierName(local).WithTriviaFrom(node));
            return SyntaxFactory.Block(
                SyntaxFactory.ParseStatement(type + " " + local + " = default;"),
                SyntaxFactory.ParseStatement(source + ".CopyTo(ref " + local + ");"),
                copied,
                SyntaxFactory.ParseStatement(local + ".CopyTo(ref " + source + ");"));
        });
    }

    /// <summary>Detects control flow that would skip a loop's trailing state publication.</summary>
    /// <param name="loop">The candidate loop.</param>
    /// <returns>Whether a return, yield or goto leaves the loop.</returns>
    private static bool LeavesLoop(SyntaxNode loop)
    {
        var labels = new HashSet<string>(Own(loop).OfType<LabeledStatementSyntax>().Select(label => label.Identifier.ValueText));
        return Own(loop).Any(node => node is ReturnStatementSyntax or YieldStatementSyntax ||
            node is GotoStatementSyntax jump && (jump.Expression is not IdentifierNameSyntax target || !labels.Contains(target.Identifier.ValueText)));
    }

    /// <summary>Rebinds state references without renaming user member names or independently declared helper states.</summary>
    /// <param name="node">A generated state reference.</param>
    /// <returns>The local execution reference or the original name.</returns>
    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        => !_shared && IsStateReference(node, _source) ? SyntaxFactory.IdentifierName(_local).WithTriviaFrom(node) : node;

    /// <summary>Commits only after all original resource scopes have completed successfully.</summary>
    /// <param name="node">An early successful return from the root.</param>
    /// <returns>A jump to successful completion outside the original body scope.</returns>
    public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
    {
        if (_shared) return base.VisitReturnStatement(node);
        _returns = true;
        return SyntaxFactory.GotoStatement(SyntaxKind.GotoStatement, SyntaxFactory.IdentifierName(_complete)).WithTriviaFrom(node);
    }

    /// <summary>Keeps the localized state from escaping through a call that is not inlined.</summary>
    /// <param name="node">A call or ordinary generated statement.</param>
    /// <returns>An isolated call or the ordinary statement.</returns>
    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        var result = (ExpressionStatementSyntax)base.VisitExpressionStatement(node);
        var call = result.Expression as InvocationExpressionSyntax ?? (result.Expression as AssignmentExpressionSyntax)?.Right as InvocationExpressionSyntax;
        if (call == null || call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CopyTo" } ||
            call.Expression is SimpleNameSyntax name && _helpers.TryGetValue(name.Identifier.ValueText, out var helper) && IsInlined(helper)) return result;
        var first = call.ArgumentList.Arguments.FirstOrDefault(argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None) &&
            argument.Expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == _local);
        if (first == null) return result;
        string isolated;
        do { isolated = _local + "Fallback" + _next++; } while (!_used.Add(isolated));
        var replaced = call.WithArgumentList(call.ArgumentList.WithArguments(call.ArgumentList.Arguments.Replace(first,
            first.WithExpression(SyntaxFactory.IdentifierName(isolated)))));
        return SyntaxFactory.Block(SyntaxFactory.ParseStatement(_type + " " + isolated + " = default;"),
            SyntaxFactory.ParseStatement(_local + ".CopyTo(ref " + isolated + ");"),
            result.ReplaceNode(call, replaced), SyntaxFactory.ParseStatement(isolated + ".CopyTo(ref " + _local + ");"));
    }

    /// <summary>Shares the caller's localized state with an inlined helper and localizes an outlined helper independently.</summary>
    /// <param name="node">The helper.</param>
    /// <returns>The rewritten helper.</returns>
    public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        if (node.Body == null || node.ParameterList.Parameters.Count < 2 || !node.ParameterList.Parameters[0].Type.ToString().Contains("LuminPack")) return node;
        if (!_shared && IsInlined(node) && _helpers.ContainsKey(node.Identifier.ValueText))
        {
            // After inlining, the helper's state parameter is the caller's promoted local; copying it again would only add moves.
            var shared = new RootStateLocalization(node.ParameterList.Parameters[0].Identifier.ValueText, node.ParameterList.Parameters[0].Identifier.ValueText,
                null, _type, _helpers, _used, true);
            return node.WithBody((BlockSyntax)shared.VisitBlock(node.Body));
        }
        if (node.ReturnType.ToString() != "void") return node;
        var method = SyntaxFactory.MethodDeclaration(node.ReturnType, node.Identifier).WithParameterList(node.ParameterList)
            .WithTypeParameterList(node.TypeParameterList).WithConstraintClauses(node.ConstraintClauses).WithAttributeLists(node.AttributeLists).WithBody(node.Body);
        var localized = Apply(method);
        return localized == method ? node : node.WithBody(localized.Body).WithAttributeLists(node.AttributeLists);
    }

    /// <summary>Retains returns belonging to an independently declared lambda.</summary>
    /// <param name="node">The independent lambda.</param>
    /// <returns>The unchanged lambda.</returns>
    public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => node;

    /// <summary>Retains returns belonging to an independently declared lambda.</summary>
    /// <param name="node">The independent lambda.</param>
    /// <returns>The unchanged lambda.</returns>
    public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => node;
}
