using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Separates stable state entry signatures from the single execution body of each selected contract.</summary>
internal sealed class RootOperationProjection : CSharpSyntaxRewriter
{
    /// <summary>Resolves closed extension shells through the actual specialized forwarders rather than guessing root arity.</summary>
    /// <param name="node">The complete contribution after profile specialization.</param>
    /// <returns>The contribution with its extensions targeting the final available execution root.</returns>
    public override SyntaxNode VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var root = (CompilationUnitSyntax)base.VisitCompilationUnit(node);
        var operations = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(type => type.Identifier.ValueText == "LuminPackSerializer")
            .SelectMany(type => type.Members.OfType<MethodDeclarationSyntax>())
            .Where(method => method.ParameterList.Parameters.Count == 2).ToArray();
        var shells = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method =>
            method.ParameterList.Parameters.Count == 2 && method.Parent is ClassDeclarationSyntax owner &&
            (owner.Identifier.ValueText.StartsWith("Binary_", StringComparison.Ordinal) ||
                owner.Identifier.ValueText == "LuminPackSerializer" && method.Body != null && IsForwarder(method.Body)));
        return root.ReplaceNodes(shells, (original, shell) =>
        {
            string state = Normalize(shell.ParameterList.Parameters[0].Type);
            string payload = Normalize(shell.ParameterList.Parameters[1].Type);
            var calls = shell.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call =>
                call.Expression is MemberAccessExpressionSyntax access && access.Expression.ToString() == "global::LuminPack.LuminPackSerializer" ||
                call.Expression is SimpleNameSyntax simple && (simple.Identifier.ValueText.StartsWith("Root", StringComparison.Ordinal) ||
                    simple.Identifier.ValueText is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson")).ToArray();
            return shell.ReplaceNodes(calls, (old, call) =>
            {
                var access = call.Expression as MemberAccessExpressionSyntax;
                SimpleNameSyntax selected = access?.Name ?? (SimpleNameSyntax)call.Expression;
                bool valuePayload = false;
                for (int depth = 0; depth < 8; depth++)
                {
                    var arguments = selected is GenericNameSyntax generic ? generic.TypeArgumentList.Arguments.ToArray() : Array.Empty<TypeSyntax>();
                    MethodDeclarationSyntax target = null;
                    Dictionary<string, TypeSyntax> substitutions = null;
                    foreach (var candidate in operations.Where(method => method.Identifier.ValueText == selected.Identifier.ValueText &&
                        (method.TypeParameterList?.Parameters.Count ?? 0) == arguments.Length && Normalize(method.ParameterList.Parameters[0].Type) == state))
                    {
                        var map = new Dictionary<string, TypeSyntax>(StringComparer.Ordinal);
                        for (int index = 0; index < arguments.Length; index++) map.Add(candidate.TypeParameterList.Parameters[index].Identifier.ValueText, arguments[index]);
                        if (Normalize(Substitute(candidate.ParameterList.Parameters[1].Type, map)) != payload) continue;
                        target = candidate; substitutions = map; break;
                    }
                    if (target == null) break;
                    valuePayload = !target.ParameterList.Parameters[1].Modifiers.Any(SyntaxKind.RefKeyword) &&
                        !target.ParameterList.Parameters[1].Modifiers.Any(SyntaxKind.InKeyword);
                    var forward = target.ExpressionBody?.Expression as InvocationExpressionSyntax;
                    if (forward == null && target.Body != null && IsForwarder(target.Body))
                        forward = target.Body.Statements[0] is ExpressionStatementSyntax statement ? (InvocationExpressionSyntax)statement.Expression
                            : (InvocationExpressionSyntax)((ReturnStatementSyntax)target.Body.Statements[0]).Expression;
                    if (forward == null) break;
                    var next = forward.Expression is MemberAccessExpressionSyntax member ? member.Name : forward.Expression as SimpleNameSyntax;
                    if (next == null || !(next.Identifier.ValueText.StartsWith("Root", StringComparison.Ordinal) ||
                        next.Identifier.ValueText is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson")) break;
                    selected = (SimpleNameSyntax)Substitute(next, substitutions);
                }
                var rewrittenCall = call.WithExpression(access == null ? selected : access.WithName(selected));
                if (valuePayload && rewrittenCall.ArgumentList.Arguments.Count == 2)
                {
                    var payloadArgument = rewrittenCall.ArgumentList.Arguments[1];
                    rewrittenCall = rewrittenCall.WithArgumentList(rewrittenCall.ArgumentList.WithArguments(
                        rewrittenCall.ArgumentList.Arguments.Replace(payloadArgument, payloadArgument.WithRefKindKeyword(default))));
                }
                return rewrittenCall;
            });
        });
    }

    /// <summary>Compares state and payload spellings independently of qualification and nullable annotations.</summary>
    /// <param name="type">The generated type spelling.</param>
    /// <returns>The canonical comparison key.</returns>
    private static string Normalize(SyntaxNode type) => type.ToString().Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty).Replace("?", string.Empty).Replace(" ", string.Empty);

    /// <summary>Substitutes declared generic parameters in a selected signature or forwarding target.</summary>
    /// <param name="node">The signature fragment.</param>
    /// <param name="arguments">The actual closed or enclosing generic arguments.</param>
    /// <returns>The fragment with exactly those declared type parameters substituted.</returns>
    private static SyntaxNode Substitute(SyntaxNode node, Dictionary<string, TypeSyntax> arguments) => node.ReplaceNodes(
        node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Where(name => arguments.ContainsKey(name.Identifier.ValueText)),
        (original, rewritten) => arguments[original.Identifier.ValueText].WithTriviaFrom(rewritten));

    /// <summary>Moves complete state bodies into roots and leaves aggressively inlineable forwarding entries.</summary>
    /// <param name="node">The generated serializer container.</param>
    /// <returns>The same callable operations and their private execution roots.</returns>
    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (node.Identifier.ValueText != "LuminPackSerializer") return base.VisitClassDeclaration(node);
        var members = new List<MemberDeclarationSyntax>();
        foreach (var member in node.Members)
        {
            if (member is MethodDeclarationSyntax forwarder && forwarder.Body != null && IsForwarder(forwarder.Body) &&
                forwarder.ParameterList.Parameters.Count == 2 &&
                forwarder.ParameterList.Parameters[0].Type.ToString().IndexOf("LuminPack", StringComparison.Ordinal) >= 0 &&
                forwarder.Identifier.ValueText is "Serialize" or "Deserialize" or "SerializeUnion" or "DeserializeUnion" or "SerializeJson" or "DeserializeJson" or "DeserializeFresh")
            {
                var inlineAttributes = forwarder.AttributeLists.Select(list => list.WithAttributes(SyntaxFactory.SeparatedList(list.Attributes.Where(attribute =>
                    !attribute.Name.ToString().EndsWith("MethodImpl", StringComparison.Ordinal))))).Where(list => list.Attributes.Count != 0);
                members.Add(forwarder.WithAttributeLists(SyntaxFactory.List(inlineAttributes)).AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::System.Runtime.CompilerServices.MethodImpl"),
                        SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                            SyntaxFactory.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining")))))))));
                continue;
            }
            if (member is not MethodDeclarationSyntax method || method.Body == null ||
                method.ParameterList.Parameters.Count != 2 || IsForwarder(method.Body))
            {
                members.Add(member);
                continue;
            }
            string state = method.ParameterList.Parameters[0].Type.ToString().Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty);
            if (state is not ("LuminPackWriter" or "LuminPackReader" or "LuminPackJsonWriter" or "LuminPackJsonReader"))
            {
                members.Add(member);
                continue;
            }
            string name = method.Identifier.ValueText switch
            {
                "Serialize" => "RootWrite",
                "Deserialize" => "RootRead",
                "SerializeUnion" => "RootWriteUnion",
                "DeserializeUnion" => "RootReadUnion",
                "SerializeJson" => "RootWriteJson",
                "DeserializeJson" => "RootReadJson",
                "DeserializeFresh" => "RootReadFresh",
                "SerializeWithCompress" => "RootWriteCompressed",
                "DeserializeWithCompress" => "RootReadCompressed",
                _ => null
            };
            if (name == null) { members.Add(member); continue; }
            SimpleNameSyntax target = method.TypeParameterList == null ? SyntaxFactory.IdentifierName(name)
                : SyntaxFactory.GenericName(SyntaxFactory.Identifier(name), SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(method.TypeParameterList.Parameters.Select(parameter => SyntaxFactory.IdentifierName(parameter.Identifier)))));
            var arguments = method.ParameterList.Parameters.Select(parameter => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter.Identifier))
                .WithRefKindKeyword(parameter.Modifiers.Any(SyntaxKind.RefKeyword) ? SyntaxFactory.Token(SyntaxKind.RefKeyword)
                    : parameter.Modifiers.Any(SyntaxKind.InKeyword) ? SyntaxFactory.Token(SyntaxKind.InKeyword) : default));
            var call = SyntaxFactory.InvocationExpression(target, SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));
            var attributes = method.AttributeLists.Select(list => list.WithAttributes(SyntaxFactory.SeparatedList(list.Attributes.Where(attribute =>
                !attribute.Name.ToString().EndsWith("MethodImpl", StringComparison.Ordinal)))))
                .Where(list => list.Attributes.Count != 0);
            var shell = method.WithBody(SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(call)))
                .WithAttributeLists(SyntaxFactory.List(attributes)).AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::System.Runtime.CompilerServices.MethodImpl"),
                        SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                            SyntaxFactory.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining"))))))));
            members.Add(shell);
            var implementation = method.WithIdentifier(SyntaxFactory.Identifier(name)).WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));
            if (method.Modifiers.Any(SyntaxKind.UnsafeKeyword))
                implementation = implementation.AddModifiers(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword));
            if (name.StartsWith("RootWrite", StringComparison.Ordinal) && method.HasAnnotations("LuminPack.ReferencePayload"))
            {
                var payloadParameter = implementation.ParameterList.Parameters[1];
                implementation = implementation.WithParameterList(implementation.ParameterList.WithParameters(
                    implementation.ParameterList.Parameters.Replace(payloadParameter, payloadParameter.WithModifiers(default))));
            }
            implementation = RootStateLocalization.Apply(implementation);
            members.Add(name == "RootRead" ? IsolatePairElements(IsolateCompoundStrings(implementation)) : implementation);
        }
        return node.WithMembers(SyntaxFactory.List(members));
    }

    /// <summary>Gives string-pair element reads an independent inlining budget without changing standalone roots.</summary>
    /// <param name="method">The complete root after its original state-ownership projection.</param>
    /// <returns>The same record traversal with a thin local call boundary, when applicable.</returns>
    private static MethodDeclarationSyntax IsolatePairElements(MethodDeclarationSyntax method)
    {
        string payload = method.ParameterList.Parameters[1].Type.ToString();
        if (!payload.StartsWith("global::System.Collections.Generic.List<", StringComparison.Ordinal) || method.TypeParameterList?.Parameters.Count != 1)
            return method;
        var list = method.ParameterList.Parameters[1].Type.DescendantNodesAndSelf().OfType<GenericNameSyntax>().First();
        var element = list.TypeArgumentList.Arguments[0];
        if (!element.ToString().StartsWith("global::System.Collections.Generic.KeyValuePair<", StringComparison.Ordinal)) return method;
        var pair = element.DescendantNodesAndSelf().OfType<GenericNameSyntax>().First();
        if (!pair.TypeArgumentList.Arguments.Any(argument => argument is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.StringKeyword) || argument.ToString() == "global::System.String")) return method;
        string helper = "__readPairElement";
        while (method.DescendantTokens().Any(token => token.ValueText == helper)) helper += "_";
        string profile = method.TypeParameterList.Parameters[0].Identifier.ValueText;
        var readers = new HashSet<string>(StringComparer.Ordinal) { method.ParameterList.Parameters[0].Identifier.ValueText };
        foreach (var variable in method.Body.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            if (variable.Parent is VariableDeclarationSyntax declaration && declaration.Type.ToString().EndsWith("LuminPackReader", StringComparison.Ordinal))
                readers.Add(variable.Identifier.ValueText);
        var calls = method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call =>
            call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver, Name: GenericNameSyntax name } && readers.Contains(receiver.Identifier.ValueText) &&
            name.Identifier.ValueText == "Read" && name.TypeArgumentList.Arguments.Count == 1 && name.TypeArgumentList.Arguments[0].ToString() == profile &&
            call.ArgumentList.Arguments.Count == 1 && call.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) &&
            !call.Ancestors().Any(node => node is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) &&
            call.Ancestors().Any(node => node is ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax)).ToArray();
        if (calls.Length == 0) return method;
        var body = method.Body.ReplaceNodes(calls, (original, call) => SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(helper),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] {
                SyntaxFactory.Argument(((MemberAccessExpressionSyntax)call.Expression).Expression).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword)),
                call.ArgumentList.Arguments[0] }))));
        var forward = (LocalFunctionStatementSyntax)SyntaxFactory.ParseStatement(
            "// Keep pair construction within an independent native inlining budget.\n" +
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] " +
            "static void " + helper + "(ref global::LuminPack.Core.LuminPackReader elementReader, ref " + element + " elementValue) " +
            "=> global::LuminPack.LuminPackSerializer.Deserialize<" + profile + ">(ref elementReader, ref elementValue);");
        return method.WithBody(body.AddStatements(forward));
    }

    /// <summary>Recognizes signatures that already delegate their complete operation to another core.</summary>
    /// <param name="body">The state operation body.</param>
    /// <returns>Whether the body consists only of a call or a returned call.</returns>
    private static bool IsForwarder(BlockSyntax body) => body.Statements.Count == 1 && body.Statements[0] is
        ExpressionStatementSyntax { Expression: InvocationExpressionSyntax } or ReturnStatementSyntax { Expression: InvocationExpressionSyntax };
    /// <summary>Isolates complete string reads for closed scalar/string compound records.</summary>
    /// <param name="method">The original complete read root.</param>
    /// <returns>The unchanged record with a thin local string call boundary.</returns>
    private static MethodDeclarationSyntax IsolateCompoundStrings(MethodDeclarationSyntax method)
    {
        string payload = method.ParameterList.Parameters[1].Type.ToString();
        if (method.TypeParameterList?.Parameters.Count != 1 ||
            !(payload.StartsWith("global::System.Collections.Generic.KeyValuePair<", StringComparison.Ordinal) ||
              payload.StartsWith("global::System.ValueTuple<", StringComparison.Ordinal) ||
              payload.StartsWith("global::System.Tuple<", StringComparison.Ordinal))) return method;
        var compound = method.ParameterList.Parameters[1].Type.DescendantNodesAndSelf().OfType<GenericNameSyntax>().First();
        bool Text(TypeSyntax type) => type.ToString() is "string" or "global::System.String";
        if (!compound.TypeArgumentList.Arguments.Any(Text) || compound.TypeArgumentList.Arguments.Any(type =>
            !Text(type) && !global::LuminPack.SourceGenerator.CodeEmitterRegistry.KnownValueTypes.Contains(type.ToString()))) return method;
        var strings = new HashSet<string>(method.Body.DescendantNodes().OfType<VariableDeclarationSyntax>()
            .Where(declaration => Text(declaration.Type)).SelectMany(declaration => declaration.Variables).Select(variable => variable.Identifier.ValueText), StringComparer.Ordinal);
        string state = method.ParameterList.Parameters[0].Identifier.ValueText;
        string profile = method.TypeParameterList.Parameters[0].Identifier.ValueText;
        var calls = method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call =>
            call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver, Name: GenericNameSyntax name } &&
            receiver.Identifier.ValueText == state && name.Identifier.ValueText == "Read" && name.TypeArgumentList.Arguments.Count == 1 &&
            name.TypeArgumentList.Arguments[0].ToString() == profile && call.ArgumentList.Arguments.Count == 1 &&
            call.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) &&
            call.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax value &&
            strings.Contains(value.Identifier.ValueText)).ToArray();
        if (calls.Length == 0) return method;
        string helper = "__readCompoundString";
        while (method.DescendantTokens().Any(token => token.ValueText == helper)) helper += "_";
        var statements = calls.Select(call => (ExpressionStatementSyntax)call.Parent).ToArray();
        var body = method.Body.ReplaceNodes(statements, (original, statement) =>
        {
            var call = (InvocationExpressionSyntax)statement.Expression;
            var value = call.ArgumentList.Arguments[0].Expression;
            var forwardCall = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(helper),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] {
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(state)).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword)),
                    SyntaxFactory.Argument(value) })));
            return statement.WithExpression(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, value, forwardCall));
        });
        var forward = (LocalFunctionStatementSyntax)SyntaxFactory.ParseStatement(
            "// Keep the complete original string operation within its own inlining budget.\n" +
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] " +
            "static string " + helper + "(ref global::LuminPack.Core.LuminPackReader stringReader, string text) " +
            "=> stringReader.Read<" + profile + ">(text)!;");
        return method.WithBody(body.AddStatements(forward));
    }


}
