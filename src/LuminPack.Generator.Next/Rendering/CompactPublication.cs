using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using LuminPack.SourceGenerator.Next.Pipeline;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>A source contribution and the generator that owns its publication.</summary>
internal sealed class GeneratedSource : IEquatable<GeneratedSource>
{
    internal readonly string Hint, Text;
    internal readonly GenerationKind Kind;

    /// <summary>Captures one source contribution.</summary>
    /// <param name="hint">The filename within its output family.</param>
    /// <param name="kind">The binary, JSON or formatter output family.</param>
    /// <param name="text">The original generated text.</param>
    internal GeneratedSource(string hint, string text, GenerationKind kind)
    {
        Hint = hint;
        Text = text;
        Kind = kind;
    }

    /// <inheritdoc/>
    public bool Equals(GeneratedSource? other) => other != null && Hint == other.Hint && Kind == other.Kind && Text == other.Text;
    /// <inheritdoc/>
    public override bool Equals(object? other) => Equals(other as GeneratedSource);
    /// <inheritdoc/>
    public override int GetHashCode() => Hint.GetHashCode() ^ (int)Kind;
}

/// <summary>Publishes the source writers' explicit static operations without a second compiler pipeline.</summary>
internal static class CompactPublication
{
    private static readonly Regex Imports = new(@"(?m)^using (?:(\w+) = )?([^\r\n;]+);\s*", RegexOptions.Compiled);
    private static readonly Regex OwnerNames = new(@"\b(?:LuminRoots_|Binary_)\w+\b", RegexOptions.Compiled);
    private static readonly Regex DirectCalls = new(@"(?<![\w.:@])(?<target>(?:global::)?(?:LuminPack\.)?(?:LuminPackSerializer\.)?(?:LuminPackSerializer|LuminRoots_\w+|Binary_\w+)\.\w+|(?:Root|Serialize|Deserialize)\w*)\s*(?=<|\()", RegexOptions.Compiled);
    private static readonly Regex MethodGroups = new(@"(?<target>(?:(?:global::)?LuminPack\.)?(?:LuminPackSerializer|LuminRoots_\w+|Binary_\w+)\.\w+)\s*", RegexOptions.Compiled);

    /// <summary>Shares algorithms, eliminates pure adapters and groups closed formatter definitions.</summary>
    /// <param name="cache">The driver-owned publication cache.</param>
    /// <param name="sources">The generated source contributions.</param>
    /// <param name="symbols">The active conditional-compilation symbols.</param>
    /// <param name="token">Cancels publication.</param>
    /// <returns>The final deterministic generated files.</returns>
    // AggressiveOptimization is available in newer hosts but unnamed by netstandard2.0.
    [MethodImpl((MethodImplOptions)512)]
    internal static GeneratedSource[] Create(List<GeneratedSource> sources, string[] symbols, CancellationToken token, Cache cache)
    {
        var retained = new HashSet<string>(StringComparer.Ordinal);
        foreach (GeneratedSource source in sources)
            retained.Add(SourceKey(source));
        var obsolete = new List<string>();
        foreach (string key in cache.Documents.Keys)
            if (!retained.Contains(key))
                obsolete.Add(key);
        foreach (string key in obsolete)
        {
            cache.Documents.Remove(key);
            cache.Outputs.Remove(key);
        }
        var documents = new Document[sources.Count];
        for (int index = 0; index < sources.Count; index++)
        {
            GeneratedSource source = sources[index];
            string key = SourceKey(source);
            if (cache.Documents.TryGetValue(key, out Document? prior) && prior.Source.Text == source.Text)
                documents[index] = prior;
            else
                cache.Documents[key] = documents[index] = new Document(source);
        }
        var catalog = new Dictionary<OperationKey, Operation>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var rootSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (Document document in documents)
            foreach (GeneratedText.Method method in document.Methods)
                if (method.Owner.StartsWith("LuminRoots_", StringComparison.Ordinal))
                    rootSet.Add(method.Owner.Substring(11));
        var roots = new List<string>(rootSet);
        roots.Sort(static (left, right) => right.Length.CompareTo(left.Length));
        var suffixes = new string[roots.Count];
        for (int index = 0; index < roots.Count; index++)
            suffixes[index] = "_" + roots[index];
        foreach (Document document in documents)
            foreach (GeneratedText.Method method in document.Methods)
            {
                string owner = method.Owner;
                if (owner.StartsWith("LuminRoots_", StringComparison.Ordinal))
                {
                    if (!names.ContainsKey(owner))
                        names.Add(owner, owner.Substring(11) + "Formatter");
                }
                else if (owner.StartsWith("Binary_", StringComparison.Ordinal))
                {
                    if (!names.ContainsKey(owner))
                        for (int index = 0; index < suffixes.Length; index++)
                            if (owner.EndsWith(suffixes[index], StringComparison.Ordinal))
                            {
                                names.Add(owner, roots[index] + "State");
                                break;
                            }
                }
                else if (owner != "LuminPackSerializer")
                    continue;
                OperationKey key = Key(owner, method.Name, method.Generics.Length, method.Json);
                if (catalog.TryGetValue(key, out Operation? duplicate))
                {
                    duplicate.Forward = null;
                }
                else
                    catalog.Add(key, new Operation(document, method));
            }
        // Method groups retain exact ref/in signatures for existing typed delegates.
        foreach (Document document in documents)
            foreach (OperationKey reference in document.References)
                if (catalog.TryGetValue(reference, out Operation? operation))
                    operation.Forward = null;
        var groups = new SortedDictionary<string, (GenerationKind Kind, List<string> Texts)>(StringComparer.Ordinal);
        foreach (Document document in documents)
        {
            token.ThrowIfCancellationRequested();
            string cacheKey = SourceKey(document.Source);
            if (cache.Outputs.TryGetValue(cacheKey, out var previous) && previous.Raw == document.Source.Text && SameBindings(previous, catalog))
            {
                Add(previous.Hint, previous.Text, document.Source.Kind);
                continue;
            }
            var aliases = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var bindings = new Dictionary<OperationKey, Operation>();
            var edits = new List<(int Start, int End, string Text)>();
            var removed = new HashSet<GeneratedText.Method>();
            foreach (GeneratedText.Method method in document.Methods)
            {
                OperationKey key = Key(method.Owner, method.Name, method.Generics.Length, method.Json);
                bool removable = catalog.TryGetValue(key, out Operation? operation) && operation.Method == method && operation.Forward != null;
                if (operation != null)
                    bindings[key] = operation;
                if (removable || method.Name is "WritePolymorphismValue" or "ReadPolymorphismValue")
                {
                    edits.Add((method.Start, method.End, string.Empty));
                    removed.Add(method);
                    continue;
                }
                string renamed = MethodName(method.Owner, method.Name, method.Json);
                string signature = FormatSignature(method, renamed);
                if (signature != method.Signature)
                {
                    edits.Add((method.SignatureStart, method.SignatureStart + method.Signature.Length, signature));
                }
            }
            int currentMethod = 0;
            foreach (Match match in RetainedCalls(document, removed))
            {
                while (currentMethod < document.Methods.Count && document.Methods[currentMethod].End <= match.Index)
                    currentMethod++;
                GeneratedText.Method? context = currentMethod < document.Methods.Count && document.Methods[currentMethod].Start <= match.Index
                    ? document.Methods[currentMethod] : null;
                if (context != null && (removed.Contains(context) || match.Index < context.SignatureStart + context.Signature.Length))
                    continue;
                string target = match.Groups["target"].Value;
                string scope = context?.Owner ?? "LuminPackSerializer";
                bool json = context?.Json ?? document.Source.Kind == GenerationKind.Json;
                string owner = target.Contains('.') ? Owner(target.Substring(0, target.LastIndexOf('.'))) : scope;
                string name = target.Substring(target.LastIndexOf('.') + 1);
                int position = match.Index + match.Length;
                string generic = string.Empty;
                if (document.Text[position] == '<')
                {
                    int end = GeneratedText.Close(document.Text, position, '<', '>');
                    if (end < 0)
                        continue;
                    generic = document.Text.Substring(position + 1, end - position - 1);
                    position = end + 1;
                }
                string[] types = GeneratedText.Split(generic);
                if (!catalog.ContainsKey(Key(owner, name, types.Length, json)) && !target.Contains('.'))
                    owner = "LuminPackSerializer";
                if (!catalog.ContainsKey(Key(owner, name, types.Length, json)))
                    continue;
                while (position < document.Text.Length && char.IsWhiteSpace(document.Text[position]))
                    position++;
                if (position >= document.Text.Length || document.Text[position] != '(')
                    continue;
                int close = GeneratedText.Close(document.Text, position, '(', ')');
                if (close < 0 || Overlaps(edits, match.Index, close + 1))
                    continue;
                var call = new GeneratedText.Call(target, types, GeneratedText.Split(document.Text.Substring(position + 1, close - position - 1)));
                edits.Add((match.Index, close + 1, Resolve(call, owner, json, aliases, catalog, names, bindings, 0)));
            }
            foreach (Match match in OwnerNames.Matches(document.Text))
                if (names.TryGetValue(match.Value, out string? name) && !Overlaps(edits, match.Index, match.Index + match.Length))
                    edits.Add((match.Index, match.Index + match.Length, name));
            // A state owner containing no retained method or other member has no remaining declaration contract.
            foreach (var owner in document.Owners)
            {
                if (!owner.Key.StartsWith("Binary_", StringComparison.Ordinal))
                    continue;
                bool allRemoved = true;
                foreach (GeneratedText.Method method in owner.Value)
                    if (!removed.Contains(method))
                        allRemoved = false;
                if (!allRemoved)
                    continue;
                int declaration = document.Text.IndexOf("internal static partial class " + owner.Key, StringComparison.Ordinal);
                if (declaration < 0)
                    continue;
                int open = document.Text.IndexOf('{', declaration);
                int close = GeneratedText.Close(document.Text, open, '{', '}');
                if (close < 0)
                    continue;
                int cursor = open + 1;
                bool empty = true;
                foreach (GeneratedText.Method method in owner.Value)
                {
                    for (int index = cursor; index < method.Start; index++)
                        if (!char.IsWhiteSpace(document.Text[index]))
                            empty = false;
                    cursor = method.End;
                }
                for (int index = cursor; index < close; index++)
                    if (!char.IsWhiteSpace(document.Text[index]))
                        empty = false;
                if (!empty)
                    continue;
                edits.RemoveAll(edit => edit.Start >= declaration && edit.End <= close + 1);
                edits.Add((declaration, close + 1, string.Empty));
            }
            foreach (var owner in document.Owners)
            {
                bool first = true;
                foreach (GeneratedText.Method method in owner.Value)
                    if (!removed.Contains(method))
                    {
                        if (!first)
                            edits.Add((method.Start, method.Start, "\n"));
                        first = false;
                    }
            }
            int firstNamespace = document.Text.IndexOf("namespace ", StringComparison.Ordinal);
            if (firstNamespace >= 0 && aliases.Count != 0)
            {
                using var imports = new PooledTextBuffer(512);
                foreach (var alias in aliases)
                    imports.Append("using ").Append(alias.Key).Append(" = ").Append(alias.Value).Append(";\n");
                imports.Append('\n');
                edits.Add((firstNamespace, firstNamespace, imports.ToString()));
            }
            using var builder = new SourcePresentation.Builder(document.Text.Length);
            int copied = 0;
            edits.Sort(static (left, right) => left.Start != right.Start ? left.Start.CompareTo(right.Start) : left.End.CompareTo(right.End));
            foreach (var edit in edits)
            {
                if (edit.Start < copied)
                    continue;
                builder.Append(document.Text, copied, edit.Start - copied);
                builder.Append(edit.Text, 0, edit.Text.Length);
                copied = edit.End;
            }
            builder.Append(document.Text, copied, document.Text.Length - copied);
            string output = builder.Complete();
            string hint = document.Source.Hint.StartsWith("LuminPack.SharedFormatters/", StringComparison.Ordinal) ? FormatterHint(document) : document.Source.Hint;
            cache.Outputs[cacheKey] = new CachedOutput(document.Source.Text, hint, output, bindings);
            Add(hint, output, document.Source.Kind);
        }
        var result = new GeneratedSource[groups.Count];
        int resultIndex = 0;
        foreach (var group in groups)
            result[resultIndex++] = new GeneratedSource(group.Key.Substring(group.Key.IndexOf(':') + 1), Join(group.Value.Texts), group.Value.Kind);
        return result;
        void Add(string hint, string output, GenerationKind kind)
        {
            string groupKey = ((int)kind).ToString() + ":" + hint;
            if (!groups.TryGetValue(groupKey, out var group))
                groups.Add(groupKey, group = (kind, new List<string>()));
            group.Texts.Add(output);
        }
    }

    /// <summary>Identifies a contribution within its output family.</summary>
    /// <param name="source">The contribution.</param>
    /// <returns>The cache key.</returns>
    private static string SourceKey(GeneratedSource source) => ((int)source.Kind).ToString() + ":" + source.Hint;

    /// <summary>Checks only the operations on which an unchanged document depends.</summary>
    /// <param name="previous">The previous document output.</param>
    /// <param name="catalog">The current operations.</param>
    /// <returns>Whether every dependency retains its signature and adapter target.</returns>
    private static bool SameBindings(CachedOutput previous, Dictionary<OperationKey, Operation> catalog)
    {
        foreach (var binding in previous.Bindings)
            if (!catalog.TryGetValue(binding.Key, out Operation? current) || !current.SameBinding(binding.Value))
                return false;
        return true;
    }

    /// <summary>Checks source edits without allocating a predicate for every candidate call.</summary>
    /// <param name="edits">The existing edits.</param>
    /// <param name="start">The proposed start.</param>
    /// <param name="end">The proposed exclusive end.</param>
    /// <returns>Whether the proposed range intersects an existing edit.</returns>
    private static bool Overlaps(List<(int Start, int End, string Text)> edits, int start, int end)
    {
        foreach (var edit in edits)
            if (start < edit.End && end > edit.Start)
                return true;
        return false;
    }

    /// <summary>Searches only source ranges whose declarations survive publication.</summary>
    /// <param name="document">The indexed source contribution.</param>
    /// <param name="removed">The declarations omitted from final publication.</param>
    /// <returns>The calls within retained source ranges.</returns>
    private static IEnumerable<Match> RetainedCalls(Document document, HashSet<GeneratedText.Method> removed)
    {
        int start = 0;
        foreach (GeneratedText.Method method in document.Methods)
        {
            if (!removed.Contains(method))
                continue;
            for (Match match = DirectCalls.Match(document.Text, start, method.Start - start); match.Success; match = match.NextMatch())
                yield return match;
            start = method.End;
        }
        for (Match match = DirectCalls.Match(document.Text, start, document.Text.Length - start); match.Success; match = match.NextMatch())
            yield return match;
    }

    /// <summary>Follows pure adapters without changing evaluation order or ref kinds.</summary>
    /// <param name="aliases">The aliases needed by the current file.</param>
    /// <param name="bindings">The operations on which this file depends.</param>
    /// <param name="call">The explicit generated invocation.</param>
    /// <param name="catalog">The current operation contracts.</param>
    /// <param name="depth">The declaration nesting or forwarding depth.</param>
    /// <param name="json">Whether the operation reads or writes JSON.</param>
    /// <param name="names">The final static-container names.</param>
    /// <param name="scope">The enclosing static container.</param>
    /// <returns>The invocation after eliminating safe forwarding operations.</returns>
    private static string Resolve(GeneratedText.Call call, string scope, bool json, SortedDictionary<string, string> aliases,
        Dictionary<OperationKey, Operation> catalog, Dictionary<string, string> names, Dictionary<OperationKey, Operation> bindings, int depth)
    {
        string target = call.Target;
        string owner = target.Contains('.') ? Owner(target.Substring(0, target.LastIndexOf('.'))) : scope;
        string name = target.Substring(target.LastIndexOf('.') + 1);
        if (!catalog.TryGetValue(Key(owner, name, call.Generics.Length, json), out Operation? operation) && !target.Contains('.'))
            catalog.TryGetValue(Key(owner = "LuminPackSerializer", name, call.Generics.Length, json), out operation);
        string generic = call.Generics.Length == 0 ? string.Empty : "<" + string.Join(", ", call.Generics) + ">";
        if (operation == null || depth > 32)
            return target + generic + "(" + string.Join(", ", call.Arguments) + ")";
        bindings[Key(operation.Method.Owner, name, call.Generics.Length, json)] = operation;
        GeneratedText.Method method = operation.Method;
        if (operation.Forward is { } forward)
        {
            var values = new Dictionary<string, string>(operation.Document.Imports, StringComparer.Ordinal);
            for (int i = 0; i < Math.Min(method.Parameters.Length, call.Arguments.Length); i++)
                values[GeneratedText.ParameterName(method.Parameters[i])] = Regex.Replace(call.Arguments[i], @"^(?:scoped\s+)?(?:ref|in|out)\s+", string.Empty);
            for (int i = 0; i < Math.Min(method.Generics.Length, call.Generics.Length); i++)
                values[method.Generics[i]] = call.Generics[i];
            string Substitute(string value) => GeneratedText.Words.Replace(value, match => values.TryGetValue(match.Value, out string? replacement) ? replacement : match.Value);
            var generics = new string[forward.Generics.Length];
            var arguments = new string[forward.Arguments.Length];
            for (int index = 0; index < generics.Length; index++)
                generics[index] = Substitute(forward.Generics[index]);
            for (int index = 0; index < arguments.Length; index++)
                arguments[index] = Substitute(forward.Arguments[index]);
            return Resolve(new GeneratedText.Call(Substitute(forward.Target), generics, arguments),
                method.Owner, method.Json, aliases, catalog, names, bindings, depth + 1);
        }
        if (names.TryGetValue(owner, out string? renamed))
        {
            target = renamed.Replace("Formatter", string.Empty) + (name.Contains("Read") ? "Reader" : "Writer");
            aliases[target] = owner.StartsWith("LuminRoots_", StringComparison.Ordinal) ? "global::LuminPack.LuminPackSerializer." + renamed : "global::LuminPack." + renamed;
        }
        else
            target = "global::LuminPack.LuminPackSerializer";
        return target + "." + MethodName(owner, name, method.Json) + generic + "(" + string.Join(", ", call.Arguments) + ")";
    }

    /// <summary>Spells an operation without legacy root or Union forwarding roles.</summary>
    /// <param name="json">Whether the operation reads or writes JSON.</param>
    /// <param name="name">The unqualified operation or parameter name.</param>
    /// <param name="owner">The declaring static container.</param>
    /// <returns>The final operation name.</returns>
    private static string MethodName(string owner, string name, bool json) =>
        owner.StartsWith("LuminRoots_", StringComparison.Ordinal) && name.StartsWith("Root", StringComparison.Ordinal) ? name.Substring(4).Replace("Union", "Payload").Replace("Member", "Members") :
        owner.StartsWith("Binary_", StringComparison.Ordinal) && json && name is "Write" or "Read" ? name + "Json" :
        name.StartsWith("SerializeUnionAt_", StringComparison.Ordinal) ? "WriteAt_" + name.Substring(17) : name;

    /// <summary>Wraps long signatures without reparsing their already captured parameter contracts.</summary>
    /// <param name="method">The source-writer method contract.</param>
    /// <param name="name">The unqualified operation or parameter name.</param>
    /// <returns>The renamed and wrapped signature.</returns>
    private static string FormatSignature(GeneratedText.Method method, string name)
    {
        string signature = method.Signature;
        if (name != method.Name)
        {
            int start = signature.IndexOf(method.Name, StringComparison.Ordinal);
            signature = signature.Substring(0, start) + name + signature.Substring(start + method.Name.Length);
        }
        int firstLine = signature.IndexOf('\n');
        if ((firstLine < 0 ? signature.Length : firstLine) > 180 && method.Parameters.Length > 1)
        {
            int open = signature.IndexOf('(');
            int close = GeneratedText.Close(signature, open, '(', ')');
            string indent = method.Indent + "    ";
            signature = signature.Substring(0, open + 1) + "\n" + indent + string.Join(",\n" + indent, method.Parameters) +
                ")" + signature.Substring(close + 1);
        }
        return signature;
    }

    /// <summary>Creates an operation key from explicit static emission facts.</summary>
    /// <param name="arity">The generic parameter count.</param>
    /// <param name="json">Whether the operation reads or writes JSON.</param>
    /// <param name="name">The unqualified operation or parameter name.</param>
    /// <param name="owner">The declaring static container.</param>
    /// <returns>The allocation-free operation identity.</returns>
    private static OperationKey Key(string owner, string name, int arity, bool json) => new(owner, name, arity, json);

    /// <summary>Indexes an operation without allocating a composite string for every lookup.</summary>
    internal readonly struct OperationKey : IEquatable<OperationKey>
    {
        private readonly string _owner;
        private readonly string _name;
        private readonly int _arity;
        private readonly bool _json;

        /// <summary>Captures the explicit static operation identity.</summary>
        /// <param name="owner">The declaring container.</param>
        /// <param name="name">The method name.</param>
        /// <param name="arity">The generic parameter count.</param>
        /// <param name="json">Whether the operation reads or writes JSON.</param>
        internal OperationKey(string owner, string name, int arity, bool json)
        {
            _owner = owner;
            _name = name;
            _arity = arity;
            _json = json;
        }

        /// <inheritdoc/>
        public bool Equals(OperationKey other) => _arity == other._arity && _json == other._json &&
            string.Equals(_owner, other._owner, StringComparison.Ordinal) && string.Equals(_name, other._name, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is OperationKey other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked((_owner.GetHashCode() * 397 ^ _name.GetHashCode()) * 397 ^ _arity * 2 ^ (_json ? 1 : 0));
    }

    /// <summary>Reads the final identifier of a qualified static owner.</summary>
    /// <param name="target">The qualified static target.</param>
    /// <returns>The unqualified static-container name.</returns>
    private static string Owner(string target) => target.Substring(Math.Max(target.LastIndexOf('.'), target.LastIndexOf(':')) + 1);

    /// <summary>Preserves the old generator's open-definition file identities.</summary>
    /// <param name="document">The indexed source contribution.</param>
    /// <returns>The filename shared by the formatter definition.</returns>
    private static string FormatterHint(Document document)
    {
        GeneratedText.Method? method = null;
        foreach (GeneratedText.Method candidate in document.Methods)
            if (candidate.Name.StartsWith("Serialize_", StringComparison.Ordinal) && candidate.Parameters.Length > 1)
            {
                method = candidate;
                break;
            }
        if (method == null)
            return "LuminPackGen.FormatterSupport.g.cs";
        string parameter = method.Parameters[1];
        string type = Regex.Replace(parameter.Substring(0, parameter.LastIndexOf(GeneratedText.ParameterName(parameter), StringComparison.Ordinal)).Trim(), @"^(?:(?:scoped|in|ref)\s+)+", string.Empty).TrimEnd('?');
        if (type.EndsWith("]", StringComparison.Ordinal))
            return "LuminPackGen.Array.g.cs";
        if (type is "bool" or "byte" or "sbyte" or "char" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "float" or "double" or "decimal" or "string" or "object")
            return "LuminPackGen.System.g.cs";
        return "LuminPackGen." + PublicationNames.Mangle(type) + ".g.cs";
    }

    /// <summary>Joins formatter contributions while retaining one set of imports.</summary>
    /// <param name="files">The related formatter contributions.</param>
    /// <returns>The combined file with one import section.</returns>
    private static string Join(List<string> files)
    {
        if (files.Count == 1)
            return files[0];
        var imports = new SortedSet<string>(StringComparer.Ordinal);
        var bodies = new List<string>();
        foreach (string file in files)
        {
            foreach (Match match in Imports.Matches(file))
                imports.Add(match.Value.Trim());
            int start = file.IndexOf("namespace ", StringComparison.Ordinal);
            if (start >= 0)
                bodies.Add(file.Substring(start).Trim());
        }
        return "// <auto-generated/>\n#nullable enable annotations\n#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649, CS1591\n" +
            string.Join(Environment.NewLine, imports) + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, bodies) + Environment.NewLine;
    }

    /// <summary>Retains text-only declaration and output snapshots between incremental edits.</summary>
    internal sealed class Cache
    {
        internal readonly Dictionary<string, Document> Documents;
        internal readonly Dictionary<string, CachedOutput> Outputs;

        /// <summary>Copies the preceding document and output indexes for one driver publication.</summary>
        /// <param name="prior">The preceding publication or operation contract.</param>
        internal Cache(Cache? prior)
        {
            Documents = prior == null ? new(StringComparer.Ordinal) : new(prior.Documents, StringComparer.Ordinal);
            Outputs = prior == null ? new(StringComparer.Ordinal) : new(prior.Outputs, StringComparer.Ordinal);
        }
    }

    /// <summary>The text and exact operation bindings used to publish one contribution.</summary>
    internal sealed class CachedOutput
    {
        internal readonly string Raw, Hint, Text;
        internal readonly Dictionary<OperationKey, Operation> Bindings;

        /// <summary>Retains one exact source contribution and its operation dependencies.</summary>
        /// <param name="raw">The unmodified source contribution.</param>
        /// <param name="hint">The filename within its output family.</param>
        /// <param name="text">The original generated text.</param>
        /// <param name="bindings">The operations on which this file depends.</param>
        internal CachedOutput(string raw, string hint, string text, Dictionary<OperationKey, Operation> bindings)
        {
            Raw = raw;
            Hint = hint;
            Text = text;
            Bindings = bindings;
        }
    }

    /// <summary>A source contribution with lightweight declaration boundaries.</summary>
    internal sealed class Document
    {
        internal readonly GeneratedSource Source;
        internal readonly string Text;
        internal readonly List<GeneratedText.Method> Methods;
        internal readonly Dictionary<string, string> Imports = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, List<GeneratedText.Method>> Owners = new(StringComparer.Ordinal);
        internal readonly List<OperationKey> References = new();

        /// <summary>Shares bodies before indexing the resulting explicit operations.</summary>
        /// <param name="source">The original source contribution.</param>
        internal Document(GeneratedSource source)
        {
            Source = source;
            Text = source.Text;
            Methods = GeneratedText.ReadMethods(Text);
            foreach (GeneratedText.Method method in Methods)
            {
                if (!Owners.TryGetValue(method.Owner, out var members))
                    Owners.Add(method.Owner, members = new List<GeneratedText.Method>());
                members.Add(method);
            }
            foreach (Match match in MethodGroups.Matches(Text))
            {
                int position = match.Index + match.Length;
                int arity = 0;
                if (position < Text.Length && Text[position] == '<')
                {
                    int end = GeneratedText.Close(Text, position, '<', '>');
                    if (end < 0)
                        continue;
                    arity = GeneratedText.Split(Text.Substring(position + 1, end - position - 1)).Length;
                    position = end + 1;
                }
                while (position < Text.Length && char.IsWhiteSpace(Text[position]))
                    position++;
                if (position < Text.Length && Text[position] == '(')
                    continue;
                string target = match.Groups["target"].Value;
                int dot = target.LastIndexOf('.');
                string owner = Owner(target.Substring(0, dot));
                string name = target.Substring(dot + 1);
                References.Add(Key(owner, name, arity, false));
                References.Add(Key(owner, name, arity, true));
            }
            foreach (Match match in CompactPublication.Imports.Matches(Text))
                if (match.Groups[1].Success)
                    Imports[match.Groups[1].Value] = match.Groups[2].Value;
        }
    }

    /// <summary>An explicitly named generated operation and its declaration imports.</summary>
    internal sealed class Operation
    {
        internal readonly Document Document;
        internal readonly GeneratedText.Method Method;
        internal GeneratedText.Call? Forward;

        /// <summary>Captures one operation's source contract.</summary>
        /// <param name="document">The indexed source contribution.</param>
        /// <param name="method">The source-writer method contract.</param>
        internal Operation(Document document, GeneratedText.Method method)
        {
            Document = document;
            Method = method;
            Forward = document.Text.Substring(method.Start, method.SignatureStart - method.Start).Contains("InterceptsLocation") ? null : method.Forward;
            if (Forward != null)
            {
                string values = Forward.Target + "(" + string.Join(",", Forward.Arguments) + ")";
                foreach (string parameter in method.Parameters)
                {
                    string name = GeneratedText.ParameterName(parameter);
                    if (Occurrences(values, name) != 1)
                    {
                        Forward = null;
                        break;
                    }
                }
            }
        }

        /// <summary>Compares only the signature and adapter target that can affect another generated file.</summary>
        /// <param name="prior">The preceding publication or operation contract.</param>
        /// <returns>Whether the referenced operation contract is unchanged.</returns>
        internal bool SameBinding(Operation prior) => Method.Signature == prior.Method.Signature &&
            (ReferenceEquals(Forward, prior.Forward) || Forward != null && prior.Forward != null &&
                Forward.Target == prior.Forward.Target && Forward.Generics.SequenceEqual(prior.Forward.Generics) &&
                Forward.Arguments.SequenceEqual(prior.Forward.Arguments));

        /// <summary>Counts parameter references without treating member names as parameters.</summary>
        /// <param name="name">The unqualified operation or parameter name.</param>
        /// <param name="text">The original generated text.</param>
        /// <returns>The number of standalone parameter references.</returns>
        private static int Occurrences(string text, string name)
        {
            if (name.Length == 0)
                return 0;
            int count = 0, position = 0;
            while ((position = text.IndexOf(name, position, StringComparison.Ordinal)) >= 0)
            {
                int end = position + name.Length;
                if ((position == 0 || !(char.IsLetterOrDigit(text[position - 1]) || text[position - 1] is '_' or '.' or ':')) &&
                    (end == text.Length || !(char.IsLetterOrDigit(text[end]) || text[end] == '_')))
                    count++;
                position = end;
            }
            return count;
        }
    }
}
