using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Shares identical convenience bodies while retaining concrete state dispatch and API signatures.</summary>
internal sealed class SharedFacadeProjection
{
    private const string Payload = "__LuminFacadePayload";
    private const string Profile = "__LuminFacadeProfile";
    private const string Adapter = "__LuminFacadeAdapter";
    private const string Operations = "__LuminFacadeOperations";
    private const string Prefix = "class LuminPackSerializer {\n";
    private readonly ConditionalWeakTable<string, Contribution> _contributions = new();
    private readonly object _gate = new();
    private string _support;
    private string[] _supportKeys;
    private bool _supportScoped;

    /// <summary>Projects repeated full facade blocks without retaining consumer syntax or symbols.</summary>
    /// <param name="blocks">Concrete API fragments in canonical order.</param>
    /// <param name="eligible">Types retaining the complete convenience API.</param>
    /// <param name="metadata">The framework and language settings for these fragments.</param>
    /// <returns>Private common support, or an empty string when sharing cannot reduce duplication.</returns>
    internal string Apply(List<KeyValuePair<string, string>> blocks, HashSet<string> eligible, MetaInfo metadata)
    {
        if (eligible.Count < 2) return string.Empty;
        lock (_gate)
        {
            var contributions = new Dictionary<string, Contribution>(StringComparer.Ordinal);
            var groups = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
            foreach (var block in blocks)
            {
                if (!eligible.Contains(block.Key)) continue;
                if (!_contributions.TryGetValue(block.Value, out var contribution))
                {
                    contribution = Analyze(block.Key, block.Value, metadata);
                    _contributions.Add(block.Value, contribution);
                }
                contributions.Add(block.Key, contribution);
                foreach (var candidate in contribution.Candidates)
                {
                    if (!groups.TryGetValue(candidate.Key, out var group)) groups.Add(candidate.Key, group = new List<Candidate>());
                    group.Add(candidate);
                }
            }
            var shared = groups.Where(pair => pair.Value.Count > 1).OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
            if (shared.Length == 0) return string.Empty;
            var keys = shared.Select(pair => pair.Key).ToArray();
            var selected = new HashSet<string>(keys, StringComparer.Ordinal);
            for (int index = 0; index < blocks.Count; index++)
            {
                var block = blocks[index];
                if (contributions.TryGetValue(block.Key, out var contribution))
                    blocks[index] = new KeyValuePair<string, string>(block.Key, contribution.Render(block.Value, selected));
            }
            if (_support != null && _supportScoped == metadata.IsNet8 && _supportKeys.SequenceEqual(keys)) return _support;
            var support = new StringBuilder(BuildOperations(metadata.IsNet8));
            foreach (var group in shared) support.AppendLine(group.Value[0].Helper);
            _supportKeys = keys;
            _supportScoped = metadata.IsNet8;
            return _support = support.ToString();
        }
    }

    /// <summary>Extracts value-only sharing candidates from one frozen, fully projected fragment.</summary>
    /// <param name="type">The exact concrete payload name.</param>
    /// <param name="source">The original documented convenience fragment.</param>
    /// <param name="metadata">The parse settings and scoped-reference capability.</param>
    /// <returns>Offsets and completed source strings without a retained syntax tree.</returns>
    private static Contribution Analyze(string type, string source, MetaInfo metadata)
    {
        var root = CSharpSyntaxTree.ParseText(Prefix + source + "}", metadata.ParseOptions).GetRoot();
        var candidates = new List<Candidate>();
        var stateCalls = new Dictionary<string, bool>(StringComparer.Ordinal);
        string adapterName = Identity("__LuminFacadeFor", type);
        foreach (var method in root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().Members.OfType<MethodDeclarationSyntax>())
        {
            if (method.Body == null || method.TypeParameterList?.Parameters.Count != 1 ||
                method.ParameterList.Parameters.All(parameter => parameter.Identifier.ValueText != "value" || parameter.Type?.ToString() != type) ||
                !IsConvenience(method.Identifier.ValueText) ||
                method.ParameterList.Parameters[0].Identifier.ValueText is "writer" or "reader" or "evaluator") continue;
            // These exact roots and their private fallback need the concrete interface overload, not an erased payload.
            if (method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call => call.Expression is SimpleNameSyntax name &&
                    name.Identifier.ValueText is "RootWriteExactInterface" or "SerializeInterfaceSlow")) continue;
            string profile = method.TypeParameterList.Parameters[0].Identifier.ValueText;
            var normalizer = new NormalizeBody(type, profile, stateCalls);
            var body = (BlockSyntax)normalizer.Visit(method.Body);
            var parameters = method.ParameterList.ReplaceNodes(method.ParameterList.Parameters.Where(parameter => parameter.Identifier.ValueText == "value"),
                (parameter, _) => parameter.WithType(SyntaxFactory.IdentifierName(Payload).WithTriviaFrom(parameter.Type)));
            // Only the asynchronous read returns the payload. byte[], int and string are API results even for those payload types.
            var returnType = method.Identifier.ValueText == "DeserializeAsync"
                ? SyntaxFactory.ParseTypeName("ValueTask<" + Payload + ">") : method.ReturnType;
            string key = method.Identifier.ValueText + "|" + returnType.WithoutTrivia().NormalizeWhitespace() + "|" +
                parameters.WithoutTrivia().NormalizeWhitespace() + "|" + body.WithoutTrivia().NormalizeWhitespace() + "|" +
                string.Concat(method.AttributeLists.Select(attribute => attribute.WithoutTrivia().NormalizeWhitespace().ToFullString()));
            string helperName = Identity("__LuminFacade", key);
            var helper = method.WithIdentifier(SyntaxFactory.Identifier(helperName))
                .WithoutLeadingTrivia().WithoutTrailingTrivia()
                .WithModifiers(SyntaxFactory.TokenList(method.Modifiers.Where(token => !token.IsKind(SyntaxKind.PublicKeyword) && !token.IsKind(SyntaxKind.InternalKeyword))).Insert(0, SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                .WithTypeParameterList(SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(new[] {
                    SyntaxFactory.TypeParameter(Payload), SyntaxFactory.TypeParameter(Profile), SyntaxFactory.TypeParameter(Adapter) })))
                .WithParameterList(parameters).WithReturnType(returnType)
                .WithConstraintClauses(SyntaxFactory.List(new[] {
                    SyntaxFactory.TypeParameterConstraintClause(Profile).AddConstraints(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint),
                        SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName("global::LuminPack.Option.ILuminPackOptions"))),
                    SyntaxFactory.TypeParameterConstraintClause(Adapter).AddConstraints(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint),
                        SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName(Operations + "<" + Payload + ">"))) }))
                .WithBody(body);
            string arguments = string.Join(", ", method.ParameterList.Parameters.Select(parameter =>
                string.Join(" ", parameter.Modifiers.Where(token => token.IsKind(SyntaxKind.RefKeyword) || token.IsKind(SyntaxKind.InKeyword) || token.IsKind(SyntaxKind.OutKeyword))
                    .Select(token => token.Text).Append(parameter.Identifier.Text))));
            string call = helperName + "<" + type + ", " + profile + ", " + adapterName + "<" + profile + ">>(" + arguments + ");";
            string invocation = (method.ReturnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword) ? "" : "return ") + call;
            var wrapper = method.WithModifiers(SyntaxFactory.TokenList(method.Modifiers.Where(token => !token.IsKind(SyntaxKind.AsyncKeyword))))
                .WithBody((BlockSyntax)SyntaxFactory.ParseStatement("{" + Environment.NewLine + invocation + Environment.NewLine + "}"));
            candidates.Add(new Candidate(key, method.Span.Start - Prefix.Length, method.Span.Length,
                wrapper.WithoutLeadingTrivia().WithoutTrailingTrivia().NormalizeWhitespace().ToFullString(), DocumentHelper(helper)));
        }
        // Opaque formatter shapes can already use the generic fallback. Preserve that exact choice in their adapter.
        // Only a direct record's allocating write evaluates its exact width; the other four operations are always present.
        return new[] { "Serialize", "Deserialize", "SerializeJson", "DeserializeJson" }.All(stateCalls.ContainsKey)
            ? new Contribution(candidates.ToArray(), BuildAdapter(type, adapterName, metadata.IsNet8, stateCalls))
            : new Contribution(Array.Empty<Candidate>(), string.Empty);
    }

    /// <summary>Identifies facade operations whose complete bodies can be compared structurally.</summary>
    /// <param name="name">The generated operation name.</param>
    /// <returns>Whether the name belongs to a convenience operation.</returns>
    private static bool IsConvenience(string name)
        => name is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or "Sizeof" or "SerializeAsync" or "DeserializeAsync";

    /// <summary>Names private generated support deterministically without lossy type-name sanitization.</summary>
    /// <param name="prefix">The private member family.</param>
    /// <param name="key">The complete normalized identity.</param>
    /// <returns>A valid member name with a 128-bit identity suffix.</returns>
    private static string Identity(string prefix, string key)
        => prefix + "_" + PartitionedSourceCache.CreateHintName("id", key).Substring(3, 32);

    /// <summary>Formats the private shared operation without generating caller-facing XML.</summary>
    /// <param name="method">The normalized generic helper.</param>
    /// <returns>The formatted helper declaration.</returns>
    private static string DocumentHelper(MethodDeclarationSyntax method)
    {
        return method.NormalizeWhitespace().ToFullString();
    }

    /// <summary>Describes a parameter retained from the original facade template.</summary>
    /// <param name="name">The template parameter name.</param>
    /// <returns>A description of the parameter's operation role.</returns>
    private static string ParameterDescription(string name) => name switch
    {
        "value" => "The payload to write, evaluate or populate.",
        "writerBuffer" => "The exclusively owned output buffer and operation state.",
        "readerBuffer" => "The input buffer and its existing operation state.",
        "stream" => "The stream retained by the caller.",
        "cancellationToken" => "The cancellation token observed by the stream operation.",
        _ => "The original input for this convenience operation."
    };

    /// <summary>Builds the constrained, allocation-free adapter contract.</summary>
    /// <param name="scoped">Whether the target state APIs use scoped references.</param>
    /// <returns>The complete private interface declaration.</returns>
    private static string BuildOperations(bool scoped)
    {
        var text = new StringBuilder().AppendLine("private interface " + Operations + "<T>\n{");
        AppendAdapterMethods(text, "T", null, scoped, null);
        return text.AppendLine("}").ToString();
    }

    /// <summary>Builds a stateless adapter whose methods call only existing concrete state overloads.</summary>
    /// <param name="type">The concrete payload type.</param>
    /// <param name="name">The stable adapter identifier.</param>
    /// <param name="scoped">Whether the target state APIs use scoped references.</param>
    /// <param name="stateCalls">Whether each original entry used a payload-generic state fallback.</param>
    /// <returns>The complete private adapter declaration.</returns>
    private static string BuildAdapter(string type, string name, bool scoped, IReadOnlyDictionary<string, bool> stateCalls)
    {
        var text = new StringBuilder().AppendLine("private readonly struct " + name + "<" + Profile + "> : " + Operations + "<" + type + ">")
            .AppendLine("where " + Profile + " : struct, global::LuminPack.Option.ILuminPackOptions\n{");
        AppendAdapterMethods(text, type, Profile, scoped, stateCalls);
        return text.AppendLine("}").ToString();
    }

    /// <summary>Emits matching interface and adapter signatures with the target's established ref-safety policy.</summary>
    /// <param name="text">The support declaration builder.</param>
    /// <param name="type">The exact or generic payload type.</param>
    /// <param name="profile">The adapter profile, or null for interface declarations.</param>
    /// <param name="scoped">Whether scoped parameter annotations are supported.</param>
    /// <param name="stateCalls">The original concrete or fallback selection for each operation; an absent width is unsupported.</param>
    private static void AppendAdapterMethods(StringBuilder text, string type, string profile, bool scoped, IReadOnlyDictionary<string, bool> stateCalls)
    {
        foreach (string operation in new[] { "Serialize", "Deserialize", "SerializeJson", "DeserializeJson", "Sizeof" })
        {
            string adapter = operation == "Sizeof" ? "Size" : operation.Replace("Deserialize", "Read").Replace("Serialize", "Write");
            string state = operation == "Sizeof" ? "LuminPackEvaluator" : "LuminPack" + (operation.EndsWith("Json", StringComparison.Ordinal) ? "Json" : "") +
                (operation.StartsWith("Deserialize", StringComparison.Ordinal) ? "Reader" : "Writer");
            string modifier = operation is "Serialize" or "SerializeJson" ? "in" : "ref";
            string scope = scoped ? "scoped " : "";
            if (profile != null) text.AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            text.Append(profile == null ? "" : "public ").Append("void ").Append(adapter).Append('(').Append(scope)
                .Append("ref global::LuminPack.Core.").Append(state).Append(" state, ").Append(scope).Append(modifier).Append(' ').Append(type).Append(" value)");
            bool fallback = false;
            if (profile != null && !stateCalls.TryGetValue(operation, out fallback))
                text.Append(" => ").Append(global::LuminPack.Code.Core.LuminPackExtensionGenerator.UnsupportedWidth.TrimEnd(';'));
            else if (profile != null) text.Append(" => global::LuminPack.LuminPackSerializer.").Append(operation).Append('<')
                .Append(fallback ? type + ", " : "").Append(profile).Append(">(ref state, ").Append(modifier).Append(" value)");
            text.AppendLine(";");
        }
    }

    /// <summary>Normalizes only proven payload slots and exact concrete state calls in generated templates.</summary>
    private sealed class NormalizeBody : CSharpSyntaxRewriter
    {
        private readonly string _type;
        private readonly string _profile;
        private readonly IDictionary<string, bool> _stateCalls;

        /// <summary>Captures the concrete template substitutions.</summary>
        /// <param name="type">The exact payload type.</param>
        /// <param name="profile">The original profile parameter.</param>
        /// <param name="stateCalls">Collects the exact original dispatch choice for the concrete adapter.</param>
        internal NormalizeBody(string type, string profile, IDictionary<string, bool> stateCalls)
        { _type = type; _profile = profile; _stateCalls = stateCalls; }

        /// <summary>Renames the template's generated profile parameter.</summary>
        /// <param name="node">An identifier in a generated body.</param>
        /// <returns>The normalized profile or original identifier.</returns>
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            => node.Identifier.ValueText == _profile ? SyntaxFactory.IdentifierName(Profile).WithTriviaFrom(node) : base.VisitIdentifierName(node);

        /// <summary>Preserves older async templates' payload parameter conventions.</summary>
        /// <param name="node">A generated local-function parameter.</param>
        /// <returns>The parameter with only its proven payload slot normalized.</returns>
        public override SyntaxNode VisitParameter(ParameterSyntax node)
            => node.Identifier.ValueText == "value" && node.Type?.ToString() == _type
                ? node.WithType(SyntaxFactory.IdentifierName(Payload).WithTriviaFrom(node.Type)) : base.VisitParameter(node);

        /// <summary>Normalizes the older asynchronous read's concrete result local.</summary>
        /// <param name="node">A generated local declaration.</param>
        /// <returns>The generic payload declaration or original local.</returns>
        public override SyntaxNode VisitVariableDeclaration(VariableDeclarationSyntax node)
            => node.Type.ToString() == _type && node.Variables.Count == 1 && node.Variables[0].Identifier.ValueText == "value"
                ? base.VisitVariableDeclaration(node.WithType(SyntaxFactory.IdentifierName(Payload).WithTriviaFrom(node.Type))) : base.VisitVariableDeclaration(node);

        /// <summary>Normalizes the older asynchronous read's concrete local-function return type.</summary>
        /// <param name="node">The generated local helper.</param>
        /// <returns>The helper with its payload result normalized.</returns>
        public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
            => base.VisitLocalFunctionStatement(node.ReturnType.ToString() == _type
                ? node.WithReturnType(SyntaxFactory.IdentifierName(Payload).WithTriviaFrom(node.ReturnType)) : node);

        /// <summary>Replaces only an existing typed state entry with the constrained stateless adapter.</summary>
        /// <param name="node">An invocation in a generated convenience body.</param>
        /// <returns>The same arguments bound to the adapter, or the unchanged invocation.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var result = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            if (node.Expression is not MemberAccessExpressionSyntax access || access.Expression.ToString() != "global::LuminPack.LuminPackSerializer" ||
                access.Name is not GenericNameSyntax generic || generic.TypeArgumentList.Arguments.Count is not (1 or 2) ||
                generic.TypeArgumentList.Arguments.Last().ToString() != _profile ||
                generic.TypeArgumentList.Arguments.Count == 2 && generic.TypeArgumentList.Arguments[0].ToString() != _type ||
                node.ArgumentList.Arguments.Count != 2 || !node.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) ||
                node.ArgumentList.Arguments[1].Expression is not IdentifierNameSyntax value) return result;
            string operation = access.Name.Identifier.ValueText;
            string state = operation == "Sizeof" ? "evaluator" : operation is "Serialize" or "SerializeJson" ? "writer" : "reader";
            string receiver = node.ArgumentList.Arguments[0].Expression.ToString();
            bool original = receiver == state && value.Identifier.ValueText == "value";
            bool directRecord = operation == "Sizeof" && receiver == "directEvaluator" && value.Identifier.ValueText == "directValue" ||
                operation == "Serialize" && receiver == "fallbackWriter" && value.Identifier.ValueText == "value";
            if (!(original || directRecord) || operation is not ("Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or "Sizeof")) return result;
            _stateCalls[operation] = generic.TypeArgumentList.Arguments.Count == 2;
            string adapter = operation == "Sizeof" ? "Size" : operation.Replace("Deserialize", "Read").Replace("Serialize", "Write");
            return result.WithExpression(SyntaxFactory.ParseExpression("default(" + Adapter + ")." + adapter).WithTriviaFrom(result.Expression));
        }
    }

    /// <summary>Stores a normalized helper and the exact source span of its concrete wrapper.</summary>
    private sealed class Candidate
    {
        internal readonly string Key;
        internal readonly int Start;
        internal readonly int Length;
        internal readonly string Wrapper;
        internal readonly string Helper;

        /// <summary>Captures completed text without retaining Roslyn nodes or consumer symbols.</summary>
        /// <param name="key">The exact normalized operation identity.</param>
        /// <param name="start">The method offset in the frozen contribution.</param>
        /// <param name="length">The complete original method span.</param>
        /// <param name="wrapper">The thin concrete wrapper with its original signature.</param>
        /// <param name="helper">The private generic implementation.</param>
        internal Candidate(string key, int start, int length, string wrapper, string helper)
        { Key = key; Start = start; Length = length; Wrapper = wrapper; Helper = helper; }
    }

    /// <summary>Caches only normalized text and offsets for a single frozen facade contribution.</summary>
    private sealed class Contribution
    {
        internal readonly Candidate[] Candidates;
        private readonly string _adapter;
        private string _selection;
        private string _rendered;

        /// <summary>Retains completed sharing candidates and the concrete adapter source.</summary>
        /// <param name="candidates">Value-only method replacements.</param>
        /// <param name="adapter">The stateless concrete state adapter.</param>
        internal Contribution(Candidate[] candidates, string adapter) { Candidates = candidates; _adapter = adapter; }

        /// <summary>Rewrites only methods whose normalized bodies actually repeat in this compilation.</summary>
        /// <param name="source">The original frozen concrete contribution.</param>
        /// <param name="selected">The current set of repeated normalized operations.</param>
        /// <returns>The original or independently cached shared contribution.</returns>
        internal string Render(string source, HashSet<string> selected)
        {
            var methods = Candidates.Where(candidate => selected.Contains(candidate.Key)).ToArray();
            if (methods.Length == 0) return source;
            string selection = string.Join(",", methods.Select(method => method.Start.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (_rendered != null && _selection == selection) return _rendered;
            var result = new StringBuilder(source);
            foreach (var method in methods.OrderByDescending(candidate => candidate.Start)) result.Remove(method.Start, method.Length).Insert(method.Start, method.Wrapper);
            _selection = selection;
            return _rendered = result.AppendLine().AppendLine(_adapter).ToString();
        }
    }
}
