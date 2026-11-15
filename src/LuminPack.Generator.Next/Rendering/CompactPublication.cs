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

/// <summary>One publication edit, carrying literal replacement text or a resolved invocation.</summary>
internal readonly struct TextEdit
{
    internal readonly int Start, End;

    /// <summary>The literal replacement text, or null for a structured invocation.</summary>
    internal readonly string? Text;

    /// <summary>The resolved invocation written directly during the output pass.</summary>
    internal readonly CallEdit? Call;

    /// <summary>Captures a literal replacement or removal.</summary>
    /// <param name="start">The inclusive source start.</param>
    /// <param name="end">The exclusive source end.</param>
    /// <param name="text">The replacement text, empty for a removal.</param>
    internal TextEdit(int start, int end, string text)
    {
        Start = start;
        End = end;
        Text = text;
        Call = null;
    }

    /// <summary>Captures a structured invocation that the output pass spells directly.</summary>
    /// <param name="start">The inclusive source start.</param>
    /// <param name="end">The exclusive source end.</param>
    /// <param name="call">The resolved invocation.</param>
    internal TextEdit(int start, int end, CallEdit call)
    {
        Start = start;
        End = end;
        Text = null;
        Call = call;
    }

    /// <summary>Whether the edit only removes its source range.</summary>
    internal bool Removes => Text is { Length: 0 };

    /// <summary>Accepts the tuple form used by the declaration edits.</summary>
    /// <param name="edit">The start, end and replacement text.</param>
    public static implicit operator TextEdit((int Start, int End, string Text) edit) => new(edit.Start, edit.End, edit.Text);
}

/// <summary>A resolved invocation that avoids joining its own final string.</summary>
internal sealed class CallEdit
{
    internal readonly string Target;
    internal readonly string[] Generics, Arguments;

    /// <summary>Captures the resolved target and its type and value arguments.</summary>
    /// <param name="target">The qualified operation target.</param>
    /// <param name="generics">The explicit type arguments.</param>
    /// <param name="arguments">The argument expressions in call order.</param>
    internal CallEdit(string target, string[] generics, string[] arguments)
    {
        Target = target;
        Generics = generics;
        Arguments = arguments;
    }
}

/// <summary>Publishes the source writers' explicit static operations without a second compiler pipeline.</summary>
internal static partial class CompactPublication
{
    private static readonly Regex Imports = new(@"(?m)^using (?:(\w+) = )?([^\r\n;]+);\s*", RegexOptions.Compiled);
    // Formatter owners are field-free readonly structs, so shared generic owners can take them as strategy type arguments.
    private static readonly Regex OwnerNames = new(@"\b(?:LuminRoots_|Binary_)\w+\b", RegexOptions.Compiled);
    private static readonly Regex DirectCalls = new(@"(?<![\w.:@])(?<target>(?:global::)?(?:LuminPack\.)?(?:LuminPackSerializer\.)?(?:LuminPackSerializer|LuminRoots_\w+|Binary_\w+)\.\w+|(?:Root|Serialize|Deserialize)\w*)\s*(?=<|\()", RegexOptions.Compiled);
    private static readonly Regex MethodGroups = new(@"(?<target>(?:(?:global::)?LuminPack\.)?(?:LuminPackSerializer|LuminRoots_\w+|Binary_\w+)\.\w+)\s*", RegexOptions.Compiled);

    /// <summary>Shares algorithms, eliminates pure adapters and groups closed formatter definitions.</summary>
    /// <param name="cache">The driver-owned publication cache.</param>
    /// <param name="sources">The generated source contributions.</param>
    /// <param name="token">Cancels publication.</param>
    /// <returns>The final deterministic generated files.</returns>
    // AggressiveOptimization is available in newer hosts but unnamed by netstandard2.0.
    [MethodImpl((MethodImplOptions)512)]
    internal static GeneratedSource[] Create(List<GeneratedSource> sources, CancellationToken token, Cache cache)
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
        var stateNames = new Dictionary<string, string>(StringComparer.Ordinal);
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
                    if (!stateNames.TryGetValue(owner, out string? state))
                    {
                        for (int separator = owner.IndexOf('_', 7); separator >= 0; separator = owner.IndexOf('_', separator + 1))
                        {
                            string suffix = owner.Substring(separator + 1);
                            if (rootSet.Contains(suffix))
                            {
                                state = suffix + "State";
                                break;
                            }
                        }
                        stateNames.Add(owner, state ?? string.Empty);
                        if (state != null)
                            names.Add(owner, state);
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
                {
                    var operation = new Operation(document, method);
                    // Bound closed owners call shared generic operations by name, so they always keep their declarations.
                    if (document.Source.Hint.StartsWith(Formatters.GenericFormatterReuse.HintPrefix, StringComparison.Ordinal))
                        operation.Forward = null;
                    catalog.Add(key, operation);
                }
            }
        // Method groups retain exact ref/in signatures for existing typed delegates.
        foreach (Document document in documents)
        {
            foreach (OperationKey reference in document.References)
                if (catalog.TryGetValue(reference, out Operation? operation))
                    operation.Forward = null;
            // A call that infers the type arguments of a generic forwarder cannot inline it, so the forwarder stays.
            foreach (OperationKey call in document.Inferred)
                if (!catalog.ContainsKey(call))
                    for (int arity = 1; arity <= 4; arity++)
                        if (catalog.TryGetValue(call.WithArity(arity), out Operation? generic))
                            generic.Forward = null;
        }
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
            var edits = new List<TextEdit>();
            var removed = new HashSet<GeneratedText.Method>();
            foreach (GeneratedText.Method method in document.Methods)
            {
                OperationKey key = Key(method.Owner, method.Name, method.Generics.Length, method.Json);
                // A state extension is callable from consumer code, so its declaration stays even when generated callers bypass it.
                bool removable = catalog.TryGetValue(key, out Operation? operation) && operation.Method == method && operation.Forward != null &&
                    !Extension(method);
                if (operation != null)
                    bindings[key] = operation;
                if (removable || method.Name is "WritePolymorphismValue" or "ReadPolymorphismValue")
                {
                    edits.Add((method.Start, method.End, string.Empty));
                    removed.Add(method);
                    continue;
                }
                string renamed = MethodName(method.Owner, method.Name, method.Json, Extension(method));
                string signature = FormatSignature(method, renamed);
                if (signature != method.Signature)
                {
                    edits.Add((method.SignatureStart, method.SignatureStart + method.Signature.Length, signature));
                }
            }
            int currentMethod = 0;
            int declarationEdit = 0;
            int declarationCount = edits.Count;
            int lastCallEnd = 0;
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
                if (!catalog.ContainsKey(Key(owner, name, types.Length, json)) && !(types.Length == 0 && Inferred(catalog, owner, name, json)))
                    continue;
                while (position < document.Text.Length && char.IsWhiteSpace(document.Text[position]))
                    position++;
                if (position >= document.Text.Length || document.Text[position] != '(')
                    continue;
                int close = GeneratedText.Close(document.Text, position, '(', ')');
                if (close < 0 || match.Index < lastCallEnd || Intersects(edits, declarationCount, ref declarationEdit, match.Index, close + 1))
                    continue;
                var call = new GeneratedText.Call(target, types, GeneratedText.Split(document.Text.Substring(position + 1, close - position - 1)));
                edits.Add(new TextEdit(match.Index, close + 1, Resolve(call, owner, json, aliases, catalog, names, bindings, 0)));
                lastCallEnd = close + 1;
            }
            AddOwnerDeclarationEdits(document, names, edits);
            SortEdits(edits);
            int operationEdit = 0;
            int operationCount = edits.Count;
            foreach (Match match in OwnerNames.Matches(document.Text))
                if (names.TryGetValue(match.Value, out string? name) &&
                    !Intersects(edits, operationCount, ref operationEdit, match.Index, match.Index + match.Length))
                    edits.Add((match.Index, match.Index + match.Length, name));
            // A state owner containing no retained method or other member has no remaining declaration contract.
            foreach (var owner in document.Owners)
            {
                if (!owner.Key.StartsWith("Binary_", StringComparison.Ordinal) && !owner.Key.StartsWith("LuminRoots_", StringComparison.Ordinal))
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
            SortEdits(edits);
            string output = PublicationOrder.Render(document.Text, document.Methods, removed, edits);
            string hint = document.Source.Hint.StartsWith("LuminPack.SharedFormatters/", StringComparison.Ordinal) ? FormatterHint(document) : document.Source.Hint;
            cache.Outputs[cacheKey] = new CachedOutput(document.Source.Text, hint, output, bindings);
            Add(hint, output, document.Source.Kind);
        }
        var result = new GeneratedSource[groups.Count];
        var joinedGroups = new Dictionary<string, JoinedGroup>(StringComparer.Ordinal);
        int resultIndex = 0;
        foreach (var group in groups)
        {
            List<string> texts = group.Value.Texts;
            string output;
            if (texts.Count == 1)
            {
                output = texts[0];
            }
            else
            {
                if (!cache.JoinedGroups.TryGetValue(group.Key, out JoinedGroup? joined) || !joined.Matches(texts))
                    joined = new JoinedGroup(texts.ToArray(), Join(texts));
                joinedGroups.Add(group.Key, joined);
                output = joined.Output;
            }
            result[resultIndex++] = new GeneratedSource(group.Key.Substring(group.Key.IndexOf(':') + 1), output, group.Value.Kind);
        }
        cache.JoinedGroups = joinedGroups;
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

    /// <summary>Tests increasing source ranges against a fixed, sorted prefix of nonoverlapping edits.</summary>
    /// <param name="edits">The edit list, whose sorted prefix excludes edits appended by the current pass.</param>
    /// <param name="count">The length of the sorted prefix.</param>
    /// <param name="index">The first edit that may intersect this range.</param>
    /// <param name="start">The proposed start.</param>
    /// <param name="end">The proposed exclusive end.</param>
    /// <returns>Whether the proposed range intersects an existing edit.</returns>
    private static bool Intersects(List<TextEdit> edits, int count, ref int index, int start, int end)
    {
        while (index < count && edits[index].End <= start)
            index++;
        return index < count && edits[index].Start < end;
    }

    /// <summary>Sorts edits by source position, with insertions before longer edits at the same offset.</summary>
    /// <param name="edits">The edits to order.</param>
    private static void SortEdits(List<TextEdit> edits) =>
        edits.Sort(static (left, right) => left.Start != right.Start ? left.Start.CompareTo(right.Start) : left.End.CompareTo(right.End));

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
    private static CallEdit Resolve(GeneratedText.Call call, string scope, bool json, SortedDictionary<string, string> aliases,
        Dictionary<OperationKey, Operation> catalog, Dictionary<string, string> names, Dictionary<OperationKey, Operation> bindings, int depth)
    {
        string target = call.Target;
        string owner = target.Contains('.') ? Owner(target.Substring(0, target.LastIndexOf('.'))) : scope;
        string name = target.Substring(target.LastIndexOf('.') + 1);
        if (!catalog.TryGetValue(Key(owner, name, call.Generics.Length, json), out Operation? operation) && !target.Contains('.'))
            catalog.TryGetValue(Key(owner = "LuminPackSerializer", name, call.Generics.Length, json), out operation);
        // A call that leaves its type arguments to inference still names a generic operation; it is renamed, never inlined.
        int arity = call.Generics.Length;
        for (int candidate = 1; operation == null && call.Generics.Length == 0 && candidate <= 4; candidate++)
            if (catalog.TryGetValue(Key(owner, name, candidate, json), out operation))
                arity = candidate;
        if (operation == null || depth > 32)
            return new CallEdit(target, call.Generics, call.Arguments);
        bindings[Key(operation.Method.Owner, name, arity, json)] = operation;
        GeneratedText.Method method = operation.Method;
        if (arity == call.Generics.Length && operation.Forward is { } forward)
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
            // A closed owner bound to a shared generic owner is called through a file alias of the generic instantiation.
            int typeArguments = forward.Target.IndexOf('<');
            if (typeArguments >= 0 && names.TryGetValue(method.Owner, out string? closedOwner))
            {
                int member = forward.Target.LastIndexOf('.');
                string operationName = forward.Target.Substring(member + 1);
                string alias = closedOwner.Replace("Formatter", string.Empty) + (operationName.Contains("Read") ? "Reader" : "Writer");
                aliases[alias] = forward.Target.Substring(0, member);
                return new CallEdit(alias + "." + operationName, generics, arguments);
            }
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
        return new CallEdit(target + "." + MethodName(owner, name, method.Json, Extension(method)), call.Generics, call.Arguments);
    }

    /// <summary>Whether a call without type arguments names a generic operation whose arguments are inferred.</summary>
    /// <param name="catalog">The operation catalog.</param>
    /// <param name="owner">The declaring static container.</param>
    /// <param name="name">The unqualified operation name.</param>
    /// <param name="json">Whether the operation reads or writes JSON.</param>
    /// <returns>Whether a generic operation of that name exists.</returns>
    private static bool Inferred(Dictionary<OperationKey, Operation> catalog, string owner, string name, bool json)
    {
        for (int arity = 1; arity <= 4; arity++)
            if (catalog.ContainsKey(Key(owner, name, arity, json)))
                return true;
        return false;
    }

    /// <summary>Whether a method is a state extension, which consumer code calls by its original name.</summary>
    /// <param name="method">The source-writer method contract.</param>
    /// <returns>Whether the first parameter is the extended state.</returns>
    private static bool Extension(GeneratedText.Method method) =>
        method.Parameters.Length != 0 && method.Parameters[0].StartsWith("this ", StringComparison.Ordinal);

    /// <summary>Spells an operation without legacy root or Union forwarding roles.</summary>
    /// <param name="json">Whether the operation reads or writes JSON.</param>
    /// <param name="name">The unqualified operation or parameter name.</param>
    /// <param name="owner">The declaring static container.</param>
    /// <param name="extension">Whether the operation is a state extension, which keeps its name.</param>
    /// <returns>The final operation name.</returns>
    private static string MethodName(string owner, string name, bool json, bool extension) =>
        owner.StartsWith("LuminRoots_", StringComparison.Ordinal) && name.StartsWith("Root", StringComparison.Ordinal) ? name.Substring(4).Replace("Union", "Payload").Replace("Member", "Members") :
        owner.StartsWith("Binary_", StringComparison.Ordinal) && json && !extension && name is "Write" or "Read" ? name + "Json" :
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

        /// <summary>The same operation at another generic arity.</summary>
        /// <param name="arity">The generic parameter count.</param>
        /// <returns>The operation key.</returns>
        internal OperationKey WithArity(int arity) => new(_owner, _name, arity, _json);

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

    /// <summary>Adds the formatter owner declaration rewrite as a source edit.</summary>
    /// <param name="document">The indexed contribution.</param>
    /// <param name="names">The final owner names applied by the same publication pass.</param>
    /// <param name="edits">The publication edits receiving the rewrite.</param>
    /// <remarks>Replaces <c>(?m)^(?&lt;indent&gt;[ \t]*)internal static partial class (?&lt;name&gt;\w+Formatter)\b</c> with
    /// <c>${indent}internal readonly partial struct ${name}</c> without a second pass over the final file. The suffix test
    /// uses the name after the owner rename, which is what the final file declares.</remarks>
    private static void AddOwnerDeclarationEdits(Document document, Dictionary<string, string> names,
        List<TextEdit> edits)
    {
        const string marker = "internal static partial class ";
        const string keywords = "internal static partial class";
        const string replacement = "internal readonly partial struct";
        string text = document.Text;
        int index = 0;
        while ((index = text.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
        {
            int lineStart = text.LastIndexOf('\n', index) + 1;
            bool indented = true;
            for (int position = lineStart; position < index; position++)
                if (text[position] is not (' ' or '\t'))
                {
                    indented = false;
                    break;
                }
            if (indented)
            {
                int nameStart = index + marker.Length;
                int nameEnd = nameStart;
                while (nameEnd < text.Length && IsWordChar(text[nameEnd]))
                    nameEnd++;
                string raw = text.Substring(nameStart, nameEnd - nameStart);
                string name = names.TryGetValue(raw, out string? renamed) ? renamed : raw;
                // \w+Formatter requires at least one character before the suffix.
                if (name.Length >= 10 && name.EndsWith("Formatter", StringComparison.Ordinal))
                    edits.Add((index, index + keywords.Length, replacement));
            }
            index += marker.Length;
        }
    }

    /// <summary>Reads the word character class used by the declaration scan.</summary>
    /// <param name="value">The candidate character.</param>
    /// <returns>Whether the character is a letter, digit or underscore.</returns>
    private static bool IsWordChar(char value) => char.IsLetterOrDigit(value) || value == '_';

    /// <summary>Preserves the old generator's open-definition file identities.</summary>
    /// <param name="document">The indexed source contribution.</param>
    /// <returns>The filename shared by the formatter definition.</returns>
    private static string FormatterHint(Document document)
    {
        string prefix = Formatters.GenericFormatterReuse.HintPrefix;
        if (document.Source.Hint.StartsWith(prefix, StringComparison.Ordinal))
            return document.Source.Hint.Substring(prefix.Length, document.Source.Hint.IndexOf('/', prefix.Length) - prefix.Length);
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
        return GeneratedFileNames.Type(type);
    }

    /// <summary>Joins formatter contributions while retaining one set of imports.</summary>
    /// <param name="files">The related formatter contributions.</param>
    /// <returns>The combined file with one import section.</returns>
    private static string Join(List<string> files)
    {
        if (files.Count == 1)
            return files[0];
        var imports = new SortedSet<string>(StringComparer.Ordinal);
        var bodies = new List<(string Text, int Start, int Length)>(files.Count);
        int capacity = 128;
        foreach (string file in files)
        {
            foreach (Match match in Imports.Matches(file))
                imports.Add(match.Value.Trim());
            int start = file.IndexOf("namespace ", StringComparison.Ordinal);
            if (start < 0)
                continue;
            int end = file.Length;
            while (end > start && char.IsWhiteSpace(file[end - 1]))
                end--;
            bodies.Add((file, start, end - start));
            capacity += end - start + 2 * Environment.NewLine.Length;
        }
        using var joined = new PooledTextBuffer(capacity);
        joined.Append("// <auto-generated/>\n#nullable enable annotations\n#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649, CS1591\n");
        foreach (string import in imports)
            joined.Append(import).Append(Environment.NewLine);
        if (imports.Count == 0)
            joined.Append(Environment.NewLine);
        joined.Append(Environment.NewLine);
        for (int i = 0; i < bodies.Count; i++)
        {
            if (i != 0)
                joined.Append(Environment.NewLine).Append(Environment.NewLine);
            var body = bodies[i];
            joined.Append(body.Text, body.Start, body.Length);
        }
        string source = joined.Append(Environment.NewLine).ToString();
        return PublicationOrder.RenderJoined(source);
    }

    /// <summary>Retains one grouped file for its ordered, already projected contributions.</summary>
    internal sealed class JoinedGroup
    {
        private readonly string[] _contributions;
        internal readonly string Output;

        /// <summary>Captures the inputs and resulting source of a multi-contribution file.</summary>
        /// <param name="contributions">Projected texts in publication order.</param>
        /// <param name="output">The grouped source text.</param>
        internal JoinedGroup(string[] contributions, string output)
        {
            _contributions = contributions;
            Output = output;
        }

        /// <summary>Checks whether the same projected contributions can reuse this grouped file.</summary>
        /// <param name="contributions">The current projected texts.</param>
        /// <returns>Whether every text is reused in the same position.</returns>
        internal bool Matches(List<string> contributions)
        {
            if (contributions.Count != _contributions.Length)
                return false;
            for (int index = 0; index < contributions.Count; index++)
                if (!ReferenceEquals(contributions[index], _contributions[index]))
                    return false;
            return true;
        }
    }

    /// <summary>Retains text-only declaration and output snapshots between incremental edits.</summary>
    internal sealed class Cache
    {
        internal readonly Dictionary<string, Document> Documents;
        internal readonly Dictionary<string, CachedOutput> Outputs;
        internal Dictionary<string, JoinedGroup> JoinedGroups;

        /// <summary>Copies the preceding document and output indexes for one driver publication.</summary>
        /// <param name="prior">The preceding publication or operation contract.</param>
        internal Cache(Cache? prior)
        {
            Documents = prior == null ? new(StringComparer.Ordinal) : new(prior.Documents, StringComparer.Ordinal);
            Outputs = prior == null ? new(StringComparer.Ordinal) : new(prior.Outputs, StringComparer.Ordinal);
            JoinedGroups = prior?.JoinedGroups ?? new(StringComparer.Ordinal);
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

        /// <summary>Invocations without type arguments, keyed at arity zero.</summary>
        internal readonly List<OperationKey> Inferred = new();

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
                bool invoked = position < Text.Length && Text[position] == '(';
                // An invocation that leaves its type arguments to inference cannot inline a generic forwarder, which then stays.
                if (invoked && arity != 0)
                    continue;
                string target = match.Groups["target"].Value;
                int dot = target.LastIndexOf('.');
                string owner = Owner(target.Substring(0, dot));
                string name = target.Substring(dot + 1);
                if (invoked)
                {
                    Inferred.Add(Key(owner, name, 0, false));
                    Inferred.Add(Key(owner, name, 0, true));
                    continue;
                }
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
            // Interceptors are bound by their attribute or called by wrapper bodies by name, so they are never inlined away.
            Forward = method.Name.StartsWith("LuminIntercept", StringComparison.Ordinal) ||
                document.Text.Substring(method.Start, method.SignatureStart - method.Start).Contains("InterceptsLocation") ? null : method.Forward;
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
