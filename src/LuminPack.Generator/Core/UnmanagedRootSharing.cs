using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Shares the roots of closed formatter payloads that differ only in unmanaged type arguments.</summary>
/// <remarks>
/// Closed constructions of one generic definition, such as <c>List&lt;int&gt;</c> and <c>List&lt;float&gt;</c>, often render
/// root bodies that are identical except for the spelling of an unmanaged type argument. Such a root is emitted once as a
/// generic method constrained to <c>unmanaged</c>, and each closed root becomes a forwarder with its original name,
/// signature and attributes, so callers, interceptors and registration are unchanged. Value-type instantiations are compiled
/// separately by the runtime, so every payload keeps its specialized machine code.
/// <para>A root is shared only when two proofs hold for every member of its group. Textually, substituting the member's type
/// arguments into the generic body reproduces the member's own body. Semantically, every name, call, operator and expression
/// in the generic body binds to the same definition as in the member, with types equal under that substitution, and the
/// generic body has no errors. Anything else keeps its closed root.</para>
/// </remarks>
internal static class UnmanagedRootSharing
{
    /// <summary>The name prefix of a shared root container.</summary>
    internal const string ContainerPrefix = "LuminShared_";
    private const string TypeParameterPrefix = "TUnmanaged";

    /// <summary>Retains verified groups across edits that leave their closed roots unchanged.</summary>
    internal sealed class Cache
    {
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        /// <summary>Stores one group's inputs and results.</summary>
        private sealed class Entry
        {
            internal string[] Texts;
            internal MetadataReference[] References;
            internal Result Result;
        }

        /// <summary>Returns a previous result computed from identical texts and references.</summary>
        /// <param name="key">The group identity.</param>
        /// <param name="texts">The members' complete contributions in order.</param>
        /// <param name="references">The compilation's metadata references.</param>
        /// <param name="build">Computes the result when no reusable one exists.</param>
        /// <returns>The group's sharing result.</returns>
        internal Result GetOrBuild(string key, string[] texts, MetadataReference[] references, Func<Result> build)
        {
            lock (_gate)
                if (_entries.TryGetValue(key, out var entry) && entry.Texts.Length == texts.Length &&
                    entry.Texts.Zip(texts, (left, right) => ReferenceEquals(left, right) || left == right).All(same => same) &&
                    entry.References.SequenceEqual(references))
                    return entry.Result;
            var result = build();
            lock (_gate)
                _entries[key] = new Entry { Texts = texts, References = references, Result = result };
            return result;
        }
    }

    /// <summary>The rewritten members and shared declarations of one group.</summary>
    internal sealed class Result
    {
        internal static readonly Result None = new(null, null);
        internal readonly string[] Members;
        internal readonly string Shared;

        /// <summary>Captures one group's output.</summary>
        /// <param name="members">The members' contributions with forwarders, or null when nothing is shared.</param>
        /// <param name="shared">The shared root contribution, or null.</param>
        internal Result(string[] members, string shared)
        {
            Members = members;
            Shared = shared;
        }
    }

    /// <summary>One closed payload's contribution.</summary>
    private sealed class Member
    {
        internal int File;
        internal ITypeSymbol Type;
        internal ITypeSymbol[] Arguments;
        internal CompilationUnitSyntax Root;
        internal ClassDeclarationSyntax Container;
    }

    /// <summary>Shares eligible roots across the formatter contributions of one invocation batch.</summary>
    /// <param name="files">The formatter contributions in publication order; rewritten members replace their entries.</param>
    /// <param name="types">The payload type of each formatter contribution, by index in <paramref name="files"/>.</param>
    /// <param name="compilation">The consumer compilation used to prove binding equivalence.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <param name="cache">Results of previous runs.</param>
    internal static void Apply(List<FrozenGeneratedSource> files, IReadOnlyDictionary<int, ITypeSymbol> types, Compilation compilation,
        MetaInfo metadata, Cache cache)
    {
        var groups = new Dictionary<string, List<(int File, ITypeSymbol Type, ITypeSymbol[] Arguments)>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var pair in types.OrderBy(pair => pair.Key))
        {
            if (!TryGetArguments(pair.Value, out var arguments, out string shape))
                continue;
            if (!groups.TryGetValue(shape, out var group))
            {
                groups.Add(shape, group = new List<(int, ITypeSymbol, ITypeSymbol[])>());
                order.Add(shape);
            }
            group.Add((pair.Key, pair.Value, arguments));
        }
        var references = compilation.References.ToArray();
        var inserts = new List<(int Before, FrozenGeneratedSource File)>();
        foreach (string shape in order)
        {
            var group = groups[shape];
            if (group.Count < 2)
                continue;
            var texts = group.Select(member => files[member.File].Text).ToArray();
            var result = cache.GetOrBuild(shape, texts, references, () => Build(group, texts, compilation, metadata));
            if (result.Members == null)
                continue;
            for (int index = 0; index < group.Count; index++)
            {
                var original = files[group[index].File];
                files[group[index].File] = new FrozenGeneratedSource(original.Name, result.Members[index], original.Owner, original.Payload);
            }
            var first = files[group[0].File];
            inserts.Add((group[0].File, new FrozenGeneratedSource(first.Name, result.Shared, first.Owner)));
        }
        foreach (var insert in inserts.OrderByDescending(insert => insert.Before))
            files.Insert(insert.Before, insert.File);
    }

    /// <summary>Identifies a closed construction with unmanaged arguments and its group.</summary>
    /// <param name="type">A formatter payload.</param>
    /// <param name="arguments">The construction's type arguments, or the element of an array.</param>
    /// <param name="shape">The group identity: the definition with every eligible argument erased.</param>
    /// <returns>Whether the payload has at least one eligible argument.</returns>
    private static bool TryGetArguments(ITypeSymbol type, out ITypeSymbol[] arguments, out string shape)
    {
        arguments = null;
        shape = null;
        string definition;
        if (type is IArrayTypeSymbol array)
        {
            arguments = new[] { array.ElementType };
            definition = "array" + array.Rank;
        }
        else if (type is INamedTypeSymbol { IsGenericType: true, ContainingType: null } named && named.TypeArguments.Length != 0)
        {
            arguments = named.TypeArguments.ToArray();
            definition = FormatterTypeName.Get(named.OriginalDefinition);
        }
        else
            return false;
        bool eligible = false;
        var parts = new List<string> { definition };
        foreach (var argument in arguments)
        {
            bool shared = IsEligible(argument);
            eligible |= shared;
            parts.Add(shared ? "*" : FormatterTypeName.Get(argument));
        }
        shape = string.Join("|", parts);
        return eligible;
    }

    /// <summary>Accepts unmanaged arguments that satisfy an <c>unmanaged</c> constraint.</summary>
    /// <param name="type">A type argument.</param>
    /// <returns>Whether the argument may become a shared type parameter.</returns>
    private static bool IsEligible(ITypeSymbol type)
        => type.IsUnmanagedType && type.TypeKind is TypeKind.Struct or TypeKind.Enum &&
            type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T && type.DeclaredAccessibility != Accessibility.Private;

    /// <summary>Proves and renders the shared roots of one group.</summary>
    /// <param name="group">The group's payloads.</param>
    /// <param name="texts">Their contributions.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <returns>The rewritten contributions, or <see cref="Result.None"/>.</returns>
    private static Result Build(List<(int File, ITypeSymbol Type, ITypeSymbol[] Arguments)> group, string[] texts, Compilation compilation, MetaInfo metadata)
    {
        var members = new List<Member>();
        for (int index = 0; index < group.Count; index++)
        {
            var root = CSharpSyntaxTree.ParseText(texts[index], metadata.ParseOptions).GetCompilationUnitRoot();
            string container = RootContainerProjection.ContainerPrefix + ReadableTypeName.Of(FormatterTypeName.Get(group[index].Type));
            var declaration = root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or ClassDeclarationSyntax)
                .OfType<ClassDeclarationSyntax>().FirstOrDefault(type => type.Identifier.ValueText == container &&
                    type.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" });
            if (declaration == null)
                return Result.None;
            members.Add(new Member { File = index, Type = group[index].Type, Arguments = group[index].Arguments, Root = root, Container = declaration });
        }

        string sharedName = SharedName(members[0]);
        var containers = new SortedDictionary<int, List<MethodDeclarationSyntax>>();
        var forwarders = new Dictionary<(int Member, string Method), MethodDeclarationSyntax>();
        // Members whose roots align share one generic root; a member that fails a proof keeps its closed root.
        var jobs = new List<Proof>();
        foreach (string method in members[0].Container.Members.OfType<MethodDeclarationSyntax>().Select(item => item.Identifier.ValueText).Distinct())
        {
            var clusters = Cluster(members.Where(member => Find(member, method) != null).ToList(), method);
            for (int cluster = 0; cluster < clusters.Count; cluster++)
                if (Proof.Create(method, cluster, clusters[cluster]) is { } job)
                    jobs.Add(job);
        }
        // Each round binds every pending proof of the group in one compilation. Failing members leave their cluster at once,
        // and the rest are proved again with their own generic root in the next round.
        for (int round = 0; round < MaximumProofRounds && jobs.Count != 0; round++)
        {
            var options = metadata.ParseOptions.WithDocumentationMode(DocumentationMode.None);
            for (int index = 0; index < jobs.Count; index++)
                jobs[index].Prepare(index, options);
            var binding = compilation.AddSyntaxTrees(jobs.SelectMany(job => job.Trees));
            var retries = new List<Proof>();
            foreach (var job in jobs)
            {
                var failed = job.Evaluate(binding);
                if (failed == null)
                    continue;
                if (failed.Count == 0)
                {
                    if (!containers.TryGetValue(job.Cluster, out var templates))
                        containers.Add(job.Cluster, templates = new List<MethodDeclarationSyntax>());
                    templates.Add(job.Template);
                    foreach (var holder in job.Holders)
                        forwarders[(holder.File, job.Method)] = Forwarder(Find(holder, job.Method), holder, job.Used, ContainerName(sharedName, job.Cluster));
                    continue;
                }
                if (Proof.Create(job.Method, job.Cluster, job.Holders.Where((_, position) => !failed.Contains(position)).ToList()) is { } retry)
                    retries.Add(retry);
            }
            jobs = retries;
        }
        if (containers.Count == 0)
            return Result.None;

        var rewritten = new string[members.Count];
        foreach (var member in members)
        {
            var container = member.Container.WithMembers(SyntaxFactory.List(member.Container.Members.Select(item =>
                item is MethodDeclarationSyntax root && forwarders.TryGetValue((member.File, root.Identifier.ValueText), out var forwarder)
                    ? forwarder : item)));
            rewritten[member.File] = member.Root.ReplaceNode(member.Container, container).ToFullString();
        }
        var serializer = SyntaxFactory.ClassDeclaration("LuminPackSerializer")
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(containers.Select(pair => SyntaxFactory.ClassDeclaration(ContainerName(sharedName, pair.Key))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(pair.Value)))));
        var shared = members[0].Root.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
            SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("LuminPack")).AddMembers(serializer)))
            .NormalizeWhitespace("    ", Environment.NewLine);
        return new Result(rewritten, shared.ToFullString());
    }

    /// <summary>Names the container of one cluster.</summary>
    /// <param name="shared">The group's container name.</param>
    /// <param name="cluster">The cluster position among the clusters of a root.</param>
    /// <returns>The group name for the first cluster, and a numbered name for the others.</returns>
    private static string ContainerName(string shared, int cluster) => cluster == 0 ? shared : shared + "_" + (cluster + 1);

    /// <summary>Groups the holders of a root whose bodies align on argument spellings.</summary>
    /// <param name="holders">The members declaring the root, in group order.</param>
    /// <param name="method">The root name.</param>
    /// <returns>Clusters in order of their first member.</returns>
    private static List<List<Member>> Cluster(List<Member> holders, string method)
    {
        var clusters = new List<List<Member>>();
        foreach (var holder in holders)
        {
            var target = clusters.FirstOrDefault(cluster =>
            {
                var reference = cluster[0];
                var left = reference.Arguments.Select(Spell).ToArray();
                var right = holder.Arguments.Select(Spell).ToArray();
                return Align(Find(reference, method), Find(holder, method), new List<(TypeSyntax, TypeSyntax)>(), (x, y) =>
                    Enumerable.Range(0, left.Length).Any(argument => IsEligible(reference.Arguments[argument]) && left[argument] == x && right[argument] == y));
            });
            if (target != null)
                target.Add(holder);
            else
                clusters.Add(new List<Member> { holder });
        }
        return clusters;
    }

    /// <summary>Finds a root method of a member's container.</summary>
    /// <param name="member">The payload contribution.</param>
    /// <param name="name">The root name.</param>
    /// <returns>The single block-bodied root, or null.</returns>
    private static MethodDeclarationSyntax Find(Member member, string name)
    {
        var methods = member.Container.Members.OfType<MethodDeclarationSyntax>().Where(method => method.Identifier.ValueText == name).ToArray();
        return methods.Length == 1 && methods[0].Body != null && methods[0].TypeParameterList == null &&
            !methods[0].Modifiers.Any(SyntaxKind.UnsafeKeyword) && methods[0].ReturnType is not RefTypeSyntax ? methods[0] : null;
    }

    /// <summary>Names the shared container after the first payload with each shared argument replaced.</summary>
    /// <param name="first">The group's first payload.</param>
    /// <returns>The container name.</returns>
    private static string SharedName(Member first)
    {
        string spelling = first.Type is IArrayTypeSymbol array
            ? TypeParameterPrefix + "0" + "[" + new string(',', array.Rank - 1) + "]"
            : FormatterTypeName.Get(((INamedTypeSymbol)first.Type).OriginalDefinition.Construct(first.Arguments.Select((argument, index) =>
                IsEligible(argument) ? (ITypeSymbol)((INamedTypeSymbol)first.Type).OriginalDefinition.TypeParameters[index] : argument).ToArray()));
        if (first.Type is INamedTypeSymbol named)
            for (int index = 0; index < named.TypeArguments.Length; index++)
                spelling = ReplaceIdentifier(spelling, named.OriginalDefinition.TypeParameters[index].Name, TypeParameterPrefix + index);
        return ContainerPrefix + ReadableTypeName.Of(spelling);
    }

    /// <summary>Replaces a whole identifier in a type spelling.</summary>
    private static string ReplaceIdentifier(string text, string name, string replacement)
        => System.Text.RegularExpressions.Regex.Replace(text, @"(?<![\w.:])" + System.Text.RegularExpressions.Regex.Escape(name) + @"(?!\w)", replacement);

    /// <summary>Derives the generic body from the first holder by aligning it with every other holder.</summary>
    /// <param name="holders">The members declaring the root.</param>
    /// <param name="method">The root name.</param>
    /// <param name="used">Receives the argument positions that became type parameters.</param>
    /// <returns>The generic root, or null when the bodies differ in anything but argument spellings.</returns>
    private static MethodDeclarationSyntax CreateTemplate(List<Member> holders, string method, out SortedSet<int> used)
    {
        used = new SortedSet<int>();
        var reference = holders[0];
        var root = Find(reference, method);
        var spellings = holders.Select(holder => holder.Arguments.Select(Spell).ToArray()).ToArray();
        // Each differing type node maps to the argument positions consistent with every holder seen so far.
        var positions = new Dictionary<TypeSyntax, HashSet<int>>();
        for (int index = 1; index < holders.Count; index++)
        {
            var differences = new List<(TypeSyntax Reference, TypeSyntax Other)>();
            var other = spellings[index];
            bool IsArgumentPair(string left, string right) => Enumerable.Range(0, reference.Arguments.Length).Any(argument =>
                IsEligible(reference.Arguments[argument]) && spellings[0][argument] == left && other[argument] == right);
            if (!Align(root, Find(holders[index], method), differences, IsArgumentPair))
                return null;
            foreach (var (left, right) in differences)
            {
                string leftSpelling = Normalize(left.ToString()), rightSpelling = Normalize(right.ToString());
                var candidates = new HashSet<int>(Enumerable.Range(0, reference.Arguments.Length).Where(argument =>
                    IsEligible(reference.Arguments[argument]) && spellings[0][argument] == leftSpelling && spellings[index][argument] == rightSpelling));
                if (positions.TryGetValue(left, out var previous))
                    previous.IntersectWith(candidates);
                else
                    positions.Add(left, candidates);
                if (positions[left].Count == 0)
                    return null;
            }
        }
        if (positions.Count == 0)
            return null;
        var map = positions.ToDictionary(pair => pair.Key, pair => pair.Value.Min());
        foreach (int argument in map.Values)
            used.Add(argument);
        var template = root.ReplaceNodes(map.Keys, (original, _) => SyntaxFactory.IdentifierName(TypeParameterPrefix + map[original]).WithTriviaFrom(original));
        if (root.DescendantTokens().Any(token => token.ValueText.StartsWith(TypeParameterPrefix, StringComparison.Ordinal)))
            return null;
        var parameters = used.ToArray();
        return template
            .WithTypeParameterList(SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(parameters.Select(argument =>
                SyntaxFactory.TypeParameter(TypeParameterPrefix + argument)))))
            .WithConstraintClauses(SyntaxFactory.List(parameters.Select(argument => SyntaxFactory.TypeParameterConstraintClause(
                SyntaxFactory.IdentifierName(TypeParameterPrefix + argument)).WithConstraints(SyntaxFactory.SingletonSeparatedList<TypeParameterConstraintSyntax>(
                    SyntaxFactory.TypeConstraint(SyntaxFactory.IdentifierName("unmanaged")))))));
    }

    /// <summary>Spells a type argument as the emitters do, without spacing or qualification differences.</summary>
    private static string Spell(ITypeSymbol type) => Normalize(FormatterTypeName.Get(type));

    /// <summary>Removes spacing and global qualification from a type spelling.</summary>
    private static string Normalize(string spelling) => spelling.Replace("global::", string.Empty).Replace(" ", string.Empty);

    /// <summary>Walks two roots in parallel and collects the outermost type nodes whose spellings differ.</summary>
    /// <param name="left">The reference node.</param>
    /// <param name="right">The other node.</param>
    /// <param name="differences">Receives the differing type nodes.</param>
    /// <param name="isArgumentPair">Accepts the normalized spellings of one argument position in both payloads.</param>
    /// <returns>Whether the nodes differ only in type nodes.</returns>
    private static bool Align(SyntaxNode left, SyntaxNode right, List<(TypeSyntax, TypeSyntax)> differences, Func<string, string, bool> isArgumentPair)
    {
        if (left.IsEquivalentTo(right, topLevel: false))
            return true;
        if (left is TypeSyntax leftType && right is TypeSyntax rightType && IsTypePosition(leftType) &&
            isArgumentPair(Normalize(left.ToString()), Normalize(right.ToString())))
        {
            differences.Add((leftType, rightType));
            return true;
        }
        if (left.RawKind != right.RawKind)
            return false;
        var leftChildren = left.ChildNodesAndTokens();
        var rightChildren = right.ChildNodesAndTokens();
        if (leftChildren.Count != rightChildren.Count)
            return false;
        for (int index = 0; index < leftChildren.Count; index++)
        {
            var leftChild = leftChildren[index];
            var rightChild = rightChildren[index];
            if (leftChild.IsToken != rightChild.IsToken)
                return false;
            if (leftChild.IsToken)
            {
                if (leftChild.RawKind != rightChild.RawKind || leftChild.AsToken().ValueText != rightChild.AsToken().ValueText)
                    return false;
                continue;
            }
            if (!Align(leftChild.AsNode(), rightChild.AsNode(), differences, isArgumentPair))
                return false;
        }
        return true;
    }

    /// <summary>Accepts a type node that is a complete type reference rather than a name part of a qualified type.</summary>
    /// <param name="type">A type node.</param>
    /// <returns>Whether the node is not the right part of a qualified or alias-qualified name.</returns>
    private static bool IsTypePosition(TypeSyntax type)
        => type.Parent is not (QualifiedNameSyntax or AliasQualifiedNameSyntax);

    /// <summary>The number of proof rounds after which remaining failures keep their closed roots.</summary>
    private const int MaximumProofRounds = 3;

    /// <summary>One cluster's generic root and the evidence that each holder may forward to it.</summary>
    private sealed class Proof
    {
        internal string Method;
        internal int Cluster;
        internal List<Member> Holders;
        internal MethodDeclarationSyntax Template;
        internal SortedSet<int> Used;
        internal List<SyntaxTree> Trees = new();
        private HashSet<int> _failed;
        private SyntaxTree _template;
        private SyntaxTree[] _holders;

        /// <summary>Derives the generic root of a cluster.</summary>
        /// <param name="method">The root name.</param>
        /// <param name="cluster">The cluster position.</param>
        /// <param name="holders">The members to prove.</param>
        /// <returns>The pending proof, or null when fewer than two members remain or the bodies do not align.</returns>
        internal static Proof Create(string method, int cluster, List<Member> holders)
        {
            if (holders.Count < 2)
                return null;
            var template = CreateTemplate(holders, method, out var used);
            return template == null ? null : new Proof { Method = method, Cluster = cluster, Holders = holders, Template = template, Used = used };
        }

        /// <summary>Checks the substitution of every holder and creates the trees bound in the round.</summary>
        /// <param name="index">The proof's position in the round, which keeps its declarations distinct.</param>
        /// <param name="options">The parse options of the proof trees.</param>
        internal void Prepare(int index, CSharpParseOptions options)
        {
            _failed = new HashSet<int>();
            var generic = Template.WithTypeParameterList(null).WithConstraintClauses(default);
            for (int position = 0; position < Holders.Count; position++)
            {
                var holder = Holders[position];
                var substituted = generic.ReplaceNodes(generic.DescendantNodes().OfType<IdentifierNameSyntax>()
                        .Where(name => name.Identifier.ValueText.StartsWith(TypeParameterPrefix, StringComparison.Ordinal)),
                    (original, _) => SyntaxFactory.ParseTypeName(FormatterTypeName.Get(holder.Arguments[int.Parse(original.Identifier.ValueText.Substring(TypeParameterPrefix.Length))])));
                if (!substituted.IsEquivalentTo(Find(holder, Method), topLevel: false))
                    _failed.Add(position);
            }

            var usings = Holders[0].Root.Usings;
            SyntaxTree Wrap(MethodDeclarationSyntax root, string container) => GeneratedBinding.Tree(SyntaxFactory.CompilationUnit()
                .WithUsings(usings).AddMembers(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("LuminPack")).AddMembers(
                    SyntaxFactory.ClassDeclaration("LuminPackSerializer").AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)).AddMembers(
                        SyntaxFactory.ClassDeclaration(container).AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                            SyntaxFactory.Token(SyntaxKind.StaticKeyword)).AddMembers(root)))), options);
            _template = Wrap(Template, "LuminSharedProof" + index);
            // Holders that already failed textually are not bound.
            _holders = Holders.Select((holder, position) => _failed.Contains(position) ? null :
                Wrap(Find(holder, Method), "LuminSharedProof" + index + "_" + position)).ToArray();
            Trees = _holders.Where(tree => tree != null).Prepend(_template).ToList();
        }

        /// <summary>Compares the generic root with every holder in the round's compilation.</summary>
        /// <param name="binding">The compilation containing every tree of the round.</param>
        /// <returns>The positions of the holders that cannot forward, empty when all can, or null when the generic root is invalid.</returns>
        internal HashSet<int> Evaluate(Compilation binding)
        {
            var templateModel = binding.GetSemanticModel(_template);
            if (templateModel.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                return null;
            var templateMethod = _template.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
            var typeParameters = templateModel.GetDeclaredSymbol(templateMethod)?.TypeParameters;
            if (typeParameters == null)
                return null;
            NodeFacts[] facts = null;
            for (int index = 0; index < Holders.Count; index++)
            {
                if (_holders[index] == null)
                    continue;
                var model = binding.GetSemanticModel(_holders[index]);
                var holderMethod = _holders[index].GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
                // The payload parameter carries the holder's own arguments in the proof compilation.
                var arguments = model.GetDeclaredSymbol(holderMethod)?.Parameters.ElementAtOrDefault(1)?.Type switch
                {
                    IArrayTypeSymbol array => new[] { array.ElementType },
                    INamedTypeSymbol named => named.TypeArguments.ToArray(),
                    _ => null,
                };
                // A holder's text equals the error-free generic root apart from its arguments, so its own
                // diagnostics add no evidence; the node-by-node comparison below binds it once.
                if (arguments == null || arguments.Length != Holders[index].Arguments.Length)
                {
                    _failed.Add(index);
                    continue;
                }
                var substitution = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var parameter in typeParameters.Value)
                    substitution[parameter] = arguments[int.Parse(parameter.Name.Substring(TypeParameterPrefix.Length))];
                facts ??= Facts(templateMethod.Body, templateModel);
                if (!Equivalent(facts, holderMethod.Body, model, substitution))
                    _failed.Add(index);
            }
            return _failed;
        }
    }

    /// <summary>The bound facts of one generic-body node, computed once per proof.</summary>
    private sealed class NodeFacts
    {
        internal SyntaxNode Node;
        internal ITypeParameterSymbol Parameter;
        internal bool Compared;
        internal SymbolInfo Symbol;
        internal bool Typed;
        internal TypeInfo Type;
        internal Optional<object> Constant;
    }

    /// <summary>Binds the generic body once for comparison with every holder.</summary>
    /// <param name="template">The generic body.</param>
    /// <param name="model">The generic body's model.</param>
    /// <returns>The facts of every node in document order.</returns>
    private static NodeFacts[] Facts(SyntaxNode template, SemanticModel model)
    {
        var nodes = template.DescendantNodesAndSelf().ToArray();
        var facts = new NodeFacts[nodes.Length];
        for (int index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index];
            var fact = facts[index] = new NodeFacts { Node = node };
            if (node is IdentifierNameSyntax name && name.Identifier.ValueText.StartsWith(TypeParameterPrefix, StringComparison.Ordinal) &&
                model.GetSymbolInfo(name).Symbol is ITypeParameterSymbol parameter)
            {
                fact.Parameter = parameter;
                continue;
            }
            if (node is not ExpressionSyntax || node is TypeSyntax && node.Parent is not (ExpressionSyntax or ArgumentSyntax))
                continue;
            fact.Compared = true;
            fact.Symbol = model.GetSymbolInfo(node);
            // A null or default literal has no type of its own; its target is compared where it is used.
            if (node.IsKind(SyntaxKind.NullLiteralExpression) || node.IsKind(SyntaxKind.DefaultLiteralExpression))
                continue;
            fact.Typed = true;
            fact.Type = model.GetTypeInfo(node);
            fact.Constant = model.GetConstantValue(node);
        }
        return facts;
    }

    /// <summary>Compares the bound meaning of a holder's body with the generic body's facts.</summary>
    /// <param name="template">The generic body's facts in document order.</param>
    /// <param name="holder">A holder's closed body.</param>
    /// <param name="model">The holder's model.</param>
    /// <param name="substitution">The holder's argument for each type parameter.</param>
    /// <returns>Whether every expression binds equivalently.</returns>
    private static bool Equivalent(NodeFacts[] template, SyntaxNode holder, SemanticModel model,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> substitution)
    {
        var right = holder.DescendantNodesAndSelf().ToArray();
        // Type parameter names replace whole type nodes, so the holder has extra nodes inside those positions.
        int position = 0;
        foreach (var fact in template)
        {
            if (position >= right.Length)
                return false;
            var other = right[position];
            if (fact.Parameter != null)
            {
                if (other is not TypeSyntax || !Same(model.GetTypeInfo(other).Type, fact.Parameter, substitution))
                    return false;
                position += other.DescendantNodesAndSelf().Count();
                continue;
            }
            if (fact.Node.RawKind != other.RawKind)
                return false;
            position++;
            if (!fact.Compared)
                continue;
            var symbol = model.GetSymbolInfo(other);
            if (!Same(symbol.Symbol, fact.Symbol.Symbol, substitution) || fact.Symbol.CandidateSymbols.Length != symbol.CandidateSymbols.Length)
                return false;
            if (!fact.Typed)
                continue;
            var type = model.GetTypeInfo(other);
            if (!Same(type.Type, fact.Type.Type, substitution) || !Same(type.ConvertedType, fact.Type.ConvertedType, substitution))
                return false;
            var constant = model.GetConstantValue(other);
            if (fact.Constant.HasValue != constant.HasValue || fact.Constant.HasValue && !Equals(fact.Constant.Value, constant.Value))
                return false;
        }
        return position == right.Length;
    }

    /// <summary>Compares a closed symbol with a generic one under the type parameter substitution.</summary>
    /// <param name="closed">The holder's symbol.</param>
    /// <param name="generic">The generic body's symbol.</param>
    /// <param name="substitution">The holder's argument for each type parameter.</param>
    /// <returns>Whether both denote the same entity for this holder.</returns>
    private static bool Same(ISymbol closed, ISymbol generic, Dictionary<ITypeParameterSymbol, ITypeSymbol> substitution)
    {
        if (closed == null || generic == null)
            return closed == null && generic == null;
        if (generic is ITypeParameterSymbol parameter && substitution.TryGetValue(parameter, out var argument))
            return SymbolEqualityComparer.Default.Equals(closed, argument);
        if (closed.Kind != generic.Kind)
            return false;
        switch (generic)
        {
            case ILocalSymbol or IParameterSymbol or ILabelSymbol or IRangeVariableSymbol:
                return closed.Name == generic.Name && Same(TypeOf(closed), TypeOf(generic), substitution);
            case IMethodSymbol { MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction } local:
                return closed is IMethodSymbol other && other.MethodKind == local.MethodKind && closed.Name == generic.Name &&
                    other.Parameters.Length == local.Parameters.Length &&
                    other.Parameters.Zip(local.Parameters, (x, y) => Same(x.Type, y.Type, substitution) && x.RefKind == y.RefKind).All(same => same) &&
                    Same(other.ReturnType, local.ReturnType, substitution);
            case IArrayTypeSymbol array:
                return closed is IArrayTypeSymbol closedArray && closedArray.Rank == array.Rank && Same(closedArray.ElementType, array.ElementType, substitution);
            case IPointerTypeSymbol pointer:
                return closed is IPointerTypeSymbol closedPointer && Same(closedPointer.PointedAtType, pointer.PointedAtType, substitution);
            case INamedTypeSymbol named:
                return closed is INamedTypeSymbol closedNamed && SymbolEqualityComparer.Default.Equals(closedNamed.OriginalDefinition, named.OriginalDefinition) &&
                    closedNamed.TypeArguments.Length == named.TypeArguments.Length &&
                    closedNamed.TypeArguments.Zip(named.TypeArguments, (x, y) => Same(x, y, substitution)).All(same => same) &&
                    (named.ContainingType == null || Same(closedNamed.ContainingType, named.ContainingType, substitution));
            case IMethodSymbol method:
                var closedMethod = (IMethodSymbol)closed;
                if (method.MethodKind == MethodKind.BuiltinOperator || closedMethod.MethodKind == MethodKind.BuiltinOperator)
                    return closedMethod.MethodKind == method.MethodKind && closed.Name == generic.Name &&
                        Same(closedMethod.ReturnType, method.ReturnType, substitution) &&
                        closedMethod.Parameters.Zip(method.Parameters, (x, y) => Same(x.Type, y.Type, substitution)).All(same => same);
                return SymbolEqualityComparer.Default.Equals(closedMethod.OriginalDefinition, method.OriginalDefinition) &&
                    closedMethod.TypeArguments.Length == method.TypeArguments.Length &&
                    closedMethod.TypeArguments.Zip(method.TypeArguments, (x, y) => Same(x, y, substitution)).All(same => same) &&
                    Same(closedMethod.ContainingType, method.ContainingType, substitution);
            case IFieldSymbol or IPropertySymbol or IEventSymbol:
                return SymbolEqualityComparer.Default.Equals(closed.OriginalDefinition, generic.OriginalDefinition) &&
                    Same(closed.ContainingType, generic.ContainingType, substitution);
            default:
                return SymbolEqualityComparer.Default.Equals(closed, generic);
        }
    }

    /// <summary>Gets the type of a local, parameter or range variable.</summary>
    private static ITypeSymbol TypeOf(ISymbol symbol) => symbol switch
    {
        ILocalSymbol local => local.Type,
        IParameterSymbol parameter => parameter.Type,
        _ => null,
    };

    /// <summary>Replaces a closed root with a call to the shared root.</summary>
    /// <param name="root">The closed root.</param>
    /// <param name="holder">Its payload.</param>
    /// <param name="used">The argument positions that are type parameters.</param>
    /// <param name="container">The shared container name.</param>
    /// <returns>A forwarder with the root's name, signature and attributes, marked for aggressive inlining.</returns>
    private static MethodDeclarationSyntax Forwarder(MethodDeclarationSyntax root, Member holder, ISet<int> used, string container)
    {
        var arguments = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(root.ParameterList.Parameters.Select(parameter =>
        {
            var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter.Identifier.ValueText));
            if (parameter.Modifiers.Any(SyntaxKind.RefKeyword))
                return argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword));
            if (parameter.Modifiers.Any(SyntaxKind.OutKeyword))
                return argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.OutKeyword));
            if (parameter.Modifiers.Any(SyntaxKind.InKeyword))
                return argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.InKeyword));
            return argument;
        })));
        var target = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(container),
            SyntaxFactory.GenericName(root.Identifier.ValueText).WithTypeArgumentList(SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(
                used.OrderBy(argument => argument).Select(argument => SyntaxFactory.ParseTypeName(FormatterTypeName.Get(holder.Arguments[argument])))))));
        var attributes = root.AttributeLists.Select(list => list.WithAttributes(SyntaxFactory.SeparatedList(list.Attributes.Select(attribute =>
            attribute.Name.ToString().EndsWith("MethodImpl", StringComparison.Ordinal) || attribute.Name.ToString().EndsWith("MethodImplAttribute", StringComparison.Ordinal)
                ? attribute.WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                    SyntaxFactory.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining")))))
                : attribute))));
        return root.WithAttributeLists(SyntaxFactory.List(attributes)).WithBody(null)
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.InvocationExpression(target, arguments)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .NormalizeWhitespace("    ", Environment.NewLine).WithTriviaFrom(root);
    }
}
