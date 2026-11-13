#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>The checked-copy context of one untrusted root.</summary>
/// <remarks>
/// A nested read that needs the root's domain checks or declared limits runs as a copy of its read body with the root's
/// policy frozen, as the original <c>InputRead_</c> helpers
/// did. Copies redirect their own nested reads the same way, so a recursive child reuses its one copy.
/// </remarks>
internal sealed class InputRedirect
{
    private readonly HashSet<string> _copyable;
    private readonly HashSet<string> _unchanged;
    private readonly IReadOnlyDictionary<string, string> _compressedChildren;
    private readonly HashSet<(string Name, InputReadKind Kind)> _requested = new();
    private Dictionary<string, PayloadRenderModel> _unionCases;

    /// <summary>Captures the root and the targets whose read bodies can be copied.</summary>
    /// <param name="root">The root's readable name, whose own entry already applies these limits.</param>
    /// <param name="extension">The consumer's extension container name.</param>
    /// <param name="limits">The root's frozen limits.</param>
    /// <param name="copyable">The readable names of owners and payloads with a copyable read body.</param>
    /// <param name="unchanged">Payloads that already check their input and need no copy for an unbounded root.</param>
    /// <param name="compressedChildren">Jagged compressed owners and their immediate child owner names.</param>
    internal InputRedirect(string root, string extension, InputLimits limits, HashSet<string> copyable,
        HashSet<string> unchanged, IReadOnlyDictionary<string, string> compressedChildren)
    {
        Root = root;
        Limits = limits;
        OwnerPrefix = "Binary_" + extension + "_";
        Helpers = "LuminInputReads_" + root;
        _copyable = copyable;
        _unchanged = unchanged;
        _compressedChildren = compressedChildren;
    }

    /// <summary>The root's readable name.</summary>
    internal readonly string Root;

    /// <summary>The root's frozen limits.</summary>
    internal readonly InputLimits Limits;

    /// <summary>The extension-class prefix of every generated owner and payload extension.</summary>
    internal readonly string OwnerPrefix;

    /// <summary>The class that holds the root's checked copies.</summary>
    internal readonly string Helpers;

    /// <summary>The readable names whose copies were requested, in request order.</summary>
    internal readonly List<(string Name, InputReadKind Kind)> Targets = new();

    /// <summary>Redirects one statement's nested owner read to the root's checked copy.</summary>
    /// <param name="line">The statement.</param>
    /// <returns>The redirected statement, or the original one.</returns>
    internal string Apply(string line)
    {
        int search = 0;
        while ((search = line.IndexOf(OwnerPrefix, search, StringComparison.Ordinal)) >= 0)
        {
            int nameStart = search + OwnerPrefix.Length, nameEnd = nameStart;
            while (nameEnd < line.Length && (char.IsLetterOrDigit(line[nameEnd]) || line[nameEnd] == '_'))
                nameEnd++;
            bool compressed = nameEnd < line.Length && line[nameEnd] == RenderMember.CompressedOwner;
            const string read = ".Read<";
            int readStart = compressed ? nameEnd + 1 : nameEnd;
            bool call = string.CompareOrdinal(line, readStart, read, 0, read.Length) == 0;
            // The owner may be spelled through its namespace or directly.
            int start = search;
            const string qualified = "global::LuminPack.";
            if (start >= qualified.Length && string.CompareOrdinal(line, start - qualified.Length, qualified, 0, qualified.Length) == 0)
                start -= qualified.Length;
            bool boundary = start == 0 || !(char.IsLetterOrDigit(line[start - 1]) || line[start - 1] is '_' or '.' or ':');
            if (!call || !boundary)
            {
                search = nameEnd;
                continue;
            }
            string target = line.Substring(nameStart, nameEnd - nameStart);
            if (compressed ? Limits.Collection == int.MaxValue || !_compressedChildren.ContainsKey(target) :
                target == Root || !_copyable.Contains(target) || _unchanged?.Contains(target) == true)
            {
                search = nameEnd;
                continue;
            }
            string replacement = Request(target, compressed ? InputReadKind.Compressed : InputReadKind.Ordinary) + "<";
            line = line.Substring(0, start) + replacement + line.Substring(readStart + read.Length);
            search = start + replacement.Length;
        }
        return line;
    }

    /// <summary>Registers a checked read and returns its generated method name.</summary>
    internal string Request(string target, InputReadKind kind)
    {
        if (_requested.Add((target, kind)))
            Targets.Add((target, kind));
        return Helpers + (kind switch
        {
            InputReadKind.Compressed => ".ReadCompressed_",
            InputReadKind.UnionCase => ".ReadUnionCase_",
            _ => ".Read_",
        }) + target;
    }

    /// <summary>Requests the exact case model with its Union payload framing.</summary>
    internal string RequestUnionCase(PayloadRenderModel payload)
    {
        (_unionCases ??= new Dictionary<string, PayloadRenderModel>(StringComparer.Ordinal))[payload.Readable] = payload;
        return Request(payload.Readable, InputReadKind.UnionCase);
    }

    /// <summary>Returns the case model recorded when the Union switch requested the helper.</summary>
    internal PayloadRenderModel UnionCase(string target) => _unionCases[target];

    /// <summary>Applies the collection limit at the compressed child before its read can allocate.</summary>
    internal string ApplyCompressedChild(string line, string target, string policy)
    {
        const string operation = ".DeserializeWithCompress<";
        int call = line.IndexOf(operation, StringComparison.Ordinal);
        if (call < 0)
            return line;
        string child = _compressedChildren[target];
        int start = line.Length - line.TrimStart().Length;
        if (_compressedChildren.ContainsKey(child))
            return line.Substring(0, start) + Request(child, InputReadKind.Compressed) + line.Substring(call + operation.Length - 1);
        return line.Insert(start, policy + ".CheckCompressedCount(reader.GetSpan().Slice(reader.Position), " +
            Limits.Collection.ToString(CultureInfo.InvariantCulture) + "); ");
    }
}

/// <summary>The wire entry whose read body is copied under the caller's policy.</summary>
internal enum InputReadKind
{
    Ordinary,
    Compressed,
    UnionCase,
}

/// <summary>Projects frozen input limits onto read statements and renders a bounded root's checked copies.</summary>
internal static class InputReadRenderer
{
    private const string Policy = "global::LuminPack.Security.LuminPackInputPolicy";
    private static readonly string[] Readers = { "reader", "operationState", "state", "reader2" };
    private static readonly Regex StringBlocks = new(
        @"(?<![\w.])(reader|operationState|state|reader2)\.ReadStrings<([^>]+)>\(([^()]*)\)", RegexOptions.CultureInvariant);
    private static readonly Regex StringOwners = new(
        @"global::LuminPack\.Binary_\w+_String\.Read<([^>]+)>\(ref (reader|operationState|state|reader2), (ref [^;]+)\);", RegexOptions.CultureInvariant);
    private const string ForwardedBooleanRead = Policy + ".ReadBooleans(ref reader, ref ";

    /// <summary>Checks for a supported binary read body in a published owner.</summary>
    /// <param name="source">The original shared owner file.</param>
    /// <param name="target">The readable owner identity.</param>
    /// <returns>Whether the checked-copy extractor can read this owner's binary operation.</returns>
    internal static bool CanCopy(string source, string target)
    {
        int start = FindClass(source, "LuminRoots_" + target);
        if (start < 0)
            return false;
        int open = source.IndexOf('{', start);
        int close = Matching(source, open, '{', '}');
        return close > open &&
            (source.IndexOf(" void RootRead<TLuminProfile>(", open, close - open, StringComparison.Ordinal) >= 0 ||
             source.IndexOf(" void RootRead<TOptions>(", open, close - open, StringComparison.Ordinal) >= 0 ||
             source.IndexOf(" void RootRead(", open, close - open, StringComparison.Ordinal) >= 0);
    }

    /// <summary>Checks the counts and strings one statement reads against the frozen limits.</summary>
    /// <param name="line">The statement.</param>
    /// <param name="limits">The root's limits.</param>
    /// <param name="policy">The spelling of the input policy class.</param>
    /// <returns>The checked statement.</returns>
    /// <remarks>An unbounded limit is not checked, so those reads keep their trusted call sites exactly.</remarks>
    internal static string Project(string line, InputLimits limits, string policy)
    {
        if (limits.Collection != int.MaxValue)
            foreach (string reader in Readers)
            {
                // A compressed payload's element count is not bounded by its input bytes; its header is checked before the
                // kernel allocates, as the original projection checks the compressed read's input.
                string compressed = reader + ".ReadValueWithCompress(";
                int site = line.IndexOf(compressed, StringComparison.Ordinal);
                // Record bodies still spell the compressed read as its marked owner call, which the source writer resolves later.
                if (site < 0 && line.IndexOf(RenderMember.CompressedOwner + ".Read<", StringComparison.Ordinal) is int owner && owner >= 0 &&
                    line.IndexOf(">(ref " + reader + ",", owner, StringComparison.Ordinal) >= 0)
                    site = line.Length - line.TrimStart().Length;
                if (site >= 0 && (site == 0 || !(char.IsLetterOrDigit(line[site - 1]) || line[site - 1] is '_' or '.')))
                    line = line.Substring(0, site) + policy + ".CheckCompressedCount(" + reader + ".GetSpan().Slice(" + reader + ".Position), " +
                        Literal(limits.Collection) + "); " + line.Substring(site);
                line = Kernel(line, reader, "ReadLength", policy, limits.Collection, defaultMinimum: true);
                line = Kernel(line, reader, "TryReadCollectionHead", policy, limits.Collection, defaultMinimum: false);
                line = Kernel(line, reader, "PeekCollectionHeader", policy, limits.Collection, defaultMinimum: false);
            }
        if (limits.String != int.MaxValue)
        {
            string limit = Literal(limits.String);
            if (line.IndexOf(".ReadStrings<", StringComparison.Ordinal) >= 0)
                line = StringBlocks.Replace(line, match => policy + ".ReadStrings<" + match.Groups[2].Value + ">(ref " +
                    match.Groups[1].Value + ", " + match.Groups[3].Value + ", " + limit + ")");
            if (line.IndexOf("_String.Read<", StringComparison.Ordinal) >= 0)
                line = StringOwners.Replace(line, match => policy + ".ReadStringRecord<" + match.Groups[1].Value + ">(ref " +
                    match.Groups[2].Value + ", " + match.Groups[3].Value + ", " + limit + ");");
            foreach (string reader in Readers)
                line = Strings(line, reader, policy, limit);
        }
        return line;
    }

    /// <summary>Redirects a reader count kernel to its checked adapter.</summary>
    /// <param name="line">The statement.</param>
    /// <param name="reader">The reader local.</param>
    /// <param name="name">The kernel name.</param>
    /// <param name="policy">The policy class spelling.</param>
    /// <param name="limit">The collection limit.</param>
    /// <param name="defaultMinimum">Whether the kernel's only argument is the minimum element size.</param>
    /// <returns>The checked statement.</returns>
    private static string Kernel(string line, string reader, string name, string policy, int limit, bool defaultMinimum)
    {
        string call = reader + "." + name + "(";
        int search = 0;
        while ((search = line.IndexOf(call, search, StringComparison.Ordinal)) >= 0)
        {
            if (search > 0 && (char.IsLetterOrDigit(line[search - 1]) || line[search - 1] is '_' or '.'))
            {
                search += call.Length;
                continue;
            }
            int open = search + call.Length - 1;
            int close = Matching(line, open, '(', ')');
            if (close < 0)
                break;
            string arguments = line.Substring(open + 1, close - open - 1).Trim();
            int count = arguments.Length == 0 ? 0 : SplitArguments(arguments).Count;
            string checkedArguments = defaultMinimum
                ? (arguments.Length == 0 ? "1" : arguments)
                : arguments + (count == 1 ? ", 1" : string.Empty);
            string replacement = policy + "." + name + "(ref " + reader + ", " + checkedArguments + ", " + Literal(limit) + ")";
            line = line.Substring(0, search) + replacement + line.Substring(close + 1);
            search += replacement.Length;
        }
        return line;
    }

    /// <summary>Redirects string record and reused string reads to the checked adapters.</summary>
    /// <param name="line">The statement.</param>
    /// <param name="reader">The reader local.</param>
    /// <param name="policy">The policy class spelling.</param>
    /// <param name="limit">The string limit.</param>
    /// <returns>The checked statement.</returns>
    /// <remarks>A string record reads through <c>reader.Read&lt;TProfile&gt;(ref storage)</c> and a reused string through
    /// <c>reader.Read&lt;TProfile&gt;(current)</c>; scalar reads carry no argument and stay unchanged.</remarks>
    private static string Strings(string line, string reader, string policy, string limit)
    {
        string call = reader + ".Read<";
        int search = 0;
        while ((search = line.IndexOf(call, search, StringComparison.Ordinal)) >= 0)
        {
            if (search > 0 && (char.IsLetterOrDigit(line[search - 1]) || line[search - 1] is '_' or '.'))
            {
                search += call.Length;
                continue;
            }
            int open = search + call.Length - 1;
            int close = Matching(line, open, '<', '>');
            if (close < 0 || close + 1 >= line.Length || line[close + 1] != '(')
            {
                search += call.Length;
                continue;
            }
            int end = Matching(line, close + 1, '(', ')');
            string profile = line.Substring(open + 1, close - open - 1);
            string argument = end < 0 ? string.Empty : line.Substring(close + 2, end - close - 2).Trim();
            if (end < 0 || argument.Length == 0 || SplitArguments(argument).Count != 1)
            {
                search += call.Length;
                continue;
            }
            string replacement = argument.StartsWith("ref ", StringComparison.Ordinal)
                ? policy + ".ReadStringRecord<" + profile + ">(ref " + reader + ", " + argument + ", " + limit + ")"
                : policy + ".ReadString<" + profile + ">(ref " + reader + ", " + argument + ", " + limit + ")";
            line = line.Substring(0, search) + replacement + line.Substring(end + 1);
            search += replacement.Length;
        }
        return line;
    }

    /// <summary>Renders the checked copies that a bounded root's body requested, and the copies they request in turn.</summary>
    /// <param name="redirect">The root's checked-copy context, after its own body was rendered.</param>
    /// <param name="payloads">The retained payload render models by readable name, with their recursion facts.</param>
    /// <param name="set">The shared owner facts, used to check inline elements through their original family renderer.</param>
    /// <param name="sources">The shared owner files.</param>
    /// <param name="aliases">The alias mask the consumer leaves available.</param>
    /// <param name="scoped">Whether the consumer supports scoped reader parameters.</param>
    /// <param name="payloadCopies">The directly emitted payload reads.</param>
    /// <returns>The helper file, or an empty string when nothing was redirected.</returns>
    internal static string RenderCopies(InputRedirect redirect, IReadOnlyDictionary<string, PayloadRenderModel> payloads,
        IReadOnlyList<string> sources, int aliases, bool scoped, SharedFormatterSet set, out string payloadCopies)
    {
        payloadCopies = string.Empty;
        if (redirect.Targets.Count == 0)
            return string.Empty;
        var methods = new List<string>();
        var usings = new SortedSet<string>(StringComparer.Ordinal);
        using var payloadWriter = new CheckedPayloadFile(aliases, scoped, redirect.Helpers);
        for (int index = 0; index < redirect.Targets.Count; index++)
        {
            var (target, kind) = redirect.Targets[index];
            bool compressed = kind == InputReadKind.Compressed;
            PayloadRenderModel payload = null;
            if (kind == InputReadKind.UnionCase)
                payload = redirect.UnionCase(target);
            else if (!compressed)
                payloads.TryGetValue(target, out payload);
            if (payload != null)
            {
                payloadWriter.Emit(payload.WithInput(redirect.Limits, payload.Recursive, redirect), kind);
                continue;
            }
            string source = (compressed ? null : InputFormatterRenderer.Render(set, target, redirect)) ??
                sources.FirstOrDefault(text => FindClass(text, "LuminRoots_" + target) >= 0);
            if (source != null && GenericFormatterReuse.ClosedSource(source) is { } closed)
                source = closed;
            if (source == null)
                throw new InvalidOperationException("The checked copy of '" + target + "' has no source.");
            foreach (string line in source.Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.StartsWith("using ", StringComparison.Ordinal) && trimmed.EndsWith(";", StringComparison.Ordinal))
                    usings.Add(trimmed);
            }
            methods.Add(Copy(source, target, redirect, compressed));
        }
        payloadCopies = payloadWriter.Complete();
        if (methods.Count == 0)
            return string.Empty;
        var file = new StringBuilder(4096);
        file.Append("// <auto-generated/>\n#nullable enable annotations\n#pragma warning disable CS0162, CS0164, CS0168, CS0219, CS0436, CS0649, CS1591\n");
        foreach (string line in usings)
            file.Append(line).Append('\n');
        file.Append("\nnamespace LuminPack\n{\n    public static partial class LuminPackSerializer\n    {\n");
        file.Append("        /// <summary>Checked copies of the nested reads under one bounded untrusted root, with its limits frozen.</summary>\n");
        file.Append("        internal static partial class ").Append(redirect.Helpers).Append("\n        {\n");
        file.Append(string.Join("\n", methods));
        file.Append("        }\n    }\n}\n");
        return file.ToString();
    }

    /// <summary>Opens a source writer only when a requested copy has a payload model.</summary>
    private sealed class CheckedPayloadFile : IDisposable
    {
        private readonly int _aliases;
        private readonly bool _scoped;
        private readonly string _helpers;
        private CodeWriter _writer;

        /// <summary>Captures the output settings for one root's checked payloads.</summary>
        /// <param name="aliases">The alias mask.</param>
        /// <param name="scoped">Whether scoped parameters are available.</param>
        /// <param name="helpers">The shared helper container.</param>
        internal CheckedPayloadFile(int aliases, bool scoped, string helpers)
        {
            _aliases = aliases;
            _scoped = scoped;
            _helpers = helpers;
            _writer = null;
        }

        /// <summary>Appends the checked read of a modeled payload.</summary>
        /// <param name="model">The payload with the root's input policy.</param>
        internal void Emit(PayloadRenderModel model, InputReadKind kind)
        {
            if (_writer == null)
            {
                _writer = new CodeWriter(_aliases) { Scoped = _scoped };
                _writer.Line("namespace LuminPack");
                _writer.Open();
                _writer.Line("public static partial class LuminPackSerializer");
                _writer.Open();
                _writer.Line("internal static partial class " + _helpers);
                _writer.Open();
            }
            if (model.UnionRoot)
                UnionEmitter.EmitCheckedMemberRead(model, _writer);
            else
                BinaryEmitter.EmitCheckedPayloadRead(model, _writer, unionCase: kind == InputReadKind.UnionCase,
                    methodPrefix: kind == InputReadKind.UnionCase ? "ReadUnionCase_" : "Read_");
        }

        /// <summary>Returns the complete file, or empty text when there are no modeled reads.</summary>
        /// <returns>The generated payload reads.</returns>
        internal string Complete()
        {
            if (_writer == null)
                return string.Empty;
            _writer.Close();
            _writer.Close();
            _writer.Close();
            return _writer.Complete(false);
        }

        /// <inheritdoc/>
        public void Dispose() => _writer?.Dispose();
    }

    /// <summary>Copies one target's read body under the root's limits, or forwards to its own entry when the body cannot move.</summary>
    /// <param name="source">The target's file.</param>
    /// <param name="target">The target's readable name.</param>
    /// <param name="redirect">The root's checked-copy context.</param>
    /// <param name="compressed">Whether the target's compressed operation is being copied.</param>
    /// <returns>The helper method.</returns>
    private static string Copy(string source, string target, InputRedirect redirect, bool compressed)
    {
        int classStart = FindClass(source, "LuminRoots_" + target);
        int classOpen = source.IndexOf('{', classStart);
        int classClose = Matching(source, classOpen, '{', '}');
        string container = source.Substring(classOpen, classClose - classOpen + 1);
        // A record of fixed members publishes a root read without a profile parameter.
        string profile = compressed ? "TOptions" : "TLuminProfile";
        string signature = compressed ? "RootReadCompressed<TOptions>(" : "RootRead<TLuminProfile>(";
        int method = FindMethod(signature);
        if (method < 0 && !compressed)
        {
            signature = "RootRead<TOptions>(";
            method = FindMethod(signature);
            if (method >= 0)
                profile = "TOptions";
            else
            {
                signature = "RootRead(";
                method = FindMethod(signature);
            }
        }
        if (method < 0)
            throw new InvalidOperationException("The checked copy of '" + target + "' has no root read.");
        int parametersOpen = method + signature.Length - 1;
        int parametersClose = Matching(container, parametersOpen, '(', ')');
        string parameters = container.Substring(parametersOpen, parametersClose - parametersOpen + 1);
        int bodyOpen = container.IndexOf('{', parametersClose);
        int arrow = container.IndexOf("=>", parametersClose, StringComparison.Ordinal);
        string body;
        // An expression-bodied read can be the last member of its class, with no block brace after it.
        if (arrow >= 0 && (bodyOpen < 0 || arrow < bodyOpen))
        {
            int semicolon = container.IndexOf(';', arrow);
            body = container.Substring(arrow + 2, semicolon - arrow - 1).Trim() + ";";
        }
        else
            body = container.Substring(bodyOpen + 1, Matching(container, bodyOpen, '{', '}') - bodyOpen - 1);
        // A shared record read freezes ordinary framing before projecting this root's input limits onto its statements.
        if (body.StartsWith("Read<", StringComparison.Ordinal))
        {
            var shared = GeneratedText.ReadMethods(source).FirstOrDefault(candidate => candidate.Owner == "LuminRoots_" + target &&
                candidate.Name == "Read" && candidate.Generics.LastOrDefault() == "TLuminFraming");
            if (shared != null)
            {
                int sharedOpen = source.IndexOf('{', shared.SignatureStart + shared.Signature.Length);
                body = source.Substring(sharedOpen + 1, Matching(source, sharedOpen, '{', '}') - sharedOpen - 1)
                    .Replace("TLuminFraming", "global::LuminPack.Code.LuminPackObjectFraming");
            }
        }
        string valueType = ValueType(parameters);
        var helper = new StringBuilder(1024);
        helper.Append("            [global::LuminPack.Attribute.Preserve]\n");
        helper.Append(compressed ? "            internal static void ReadCompressed_" : "            internal static void Read_")
            .Append(target).Append('<').Append(profile).Append(">(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref ")
            .Append(valueType).Append(" value)\n");
        helper.Append("                where ").Append(profile).Append(" : struct, global::LuminPack.Option.ILuminPackOptions\n");
        helper.Append("            {\n");
        // Internal members of the target's class stay reachable through the class; private methods move with the body under a
        // helper-specific name, as the original expansion copies an operation's local operations. Private state or types keep
        // the body in its class, which then forwards to the target's own entry.
        List<Declaration> declarations = Declarations(container);
        var companions = new List<string>();
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        bool forward = false;
        var pending = new Queue<string>();
        pending.Enqueue(body);
        while (pending.Count != 0 && !forward)
        {
            string text = pending.Dequeue();
            foreach (Declaration declaration in declarations)
            {
                if (renames.ContainsKey(declaration.Name) || !Uses(text, declaration.Name))
                    continue;
                if (!declaration.Private)
                {
                    renames[declaration.Name] = "LuminRoots_" + target + "." + declaration.Name;
                    continue;
                }
                if (declaration.Text == null)
                {
                    forward = true;
                    break;
                }
                renames[declaration.Name] = "Read_" + target + "_" + declaration.Name;
                companions.Add(declaration.Text);
                pending.Enqueue(declaration.Text);
            }
        }
        if (forward)
        {
            if (compressed)
                throw new InvalidOperationException("The compressed read of '" + target + "' requires a closed root body.");
            helper.Append("                global::LuminPack.").Append(redirect.OwnerPrefix).Append(target)
                .Append(".Read<").Append(profile).Append(">(ref reader, ref value);\n");
        }
        else
            foreach (string raw in body.Split('\n'))
            {
                string line = Rename(raw.TrimEnd('\r'), renames);
                if (compressed)
                    line = redirect.ApplyCompressedChild(line, target, Policy);
                line = Project(redirect.Apply(line), redirect.Limits, Policy);
                helper.Append(line).Append('\n');
                // Forwarded Boolean arrays retain the fused validating copy used by the ordinary owner.
                if (redirect.Limits.Collection != int.MaxValue &&
                    line.IndexOf(ForwardedBooleanRead, StringComparison.Ordinal) is int booleanSite && booleanSite >= 0 &&
                    line.EndsWith(");", StringComparison.Ordinal))
                {
                    int valueStart = booleanSite + ForwardedBooleanRead.Length;
                    helper.Append(line, 0, booleanSite).Append(Policy).Append(".CheckLength(")
                        .Append(line, valueStart, line.Length - valueStart - 2).Append(", ")
                        .Append(Literal(redirect.Limits.Collection)).Append(");\n");
                }
            }
        helper.Append("            }\n");
        if (!forward)
            foreach (string companion in companions)
            {
                helper.Append('\n');
                foreach (string raw in companion.Split('\n'))
                    helper.Append(Project(redirect.Apply(Rename(raw.TrimEnd('\r'), renames)), redirect.Limits, Policy)).Append('\n');
            }
        return helper.ToString();

        int FindMethod(string name)
        {
            int start = container.IndexOf(" void " + name, StringComparison.Ordinal);
            return start < 0 ? -1 : start + " void ".Length;
        }
    }

    /// <summary>Reads the value parameter's type from a root read's parameter list.</summary>
    /// <param name="parameters">The parenthesized parameter list.</param>
    /// <returns>The value type spelling.</returns>
    private static string ValueType(string parameters)
    {
        List<string> items = SplitArguments(parameters.Substring(1, parameters.Length - 2));
        string value = items[items.Count - 1].Trim();
        foreach (string prefix in new[] { "scoped ", "ref " })
            if (value.StartsWith(prefix, StringComparison.Ordinal))
                value = value.Substring(prefix.Length).TrimStart();
        if (value.StartsWith("ref ", StringComparison.Ordinal))
            value = value.Substring(4).TrimStart();
        int name = value.LastIndexOf(' ');
        return value.Substring(0, name).Trim();
    }

    /// <summary>One member declared directly in a root container.</summary>
    private sealed class Declaration
    {
        internal Declaration(string name, bool isPrivate, string text)
        {
            Name = name;
            Private = isPrivate;
            Text = text;
        }

        /// <summary>The member name.</summary>
        internal readonly string Name;

        /// <summary>Whether the member is private to its class.</summary>
        internal readonly bool Private;

        /// <summary>The complete declaration of a private method, or null for a type, field or other member.</summary>
        internal readonly string Text;
    }

    /// <summary>Lists the members declared directly in a root container.</summary>
    /// <param name="container">The class block, from its opening brace.</param>
    /// <returns>The member declarations.</returns>
    private static List<Declaration> Declarations(string container)
    {
        var result = new List<Declaration>();
        int depth = 0;
        for (int index = 0; index < container.Length; index++)
        {
            char c = container[index];
            if (c is '"' or '\'')
            {
                for (index++; index < container.Length && container[index] != c; index++)
                    if (container[index] == '\\')
                        index++;
                continue;
            }
            if (c == '{')
                depth++;
            else if (c == '}')
                depth--;
            if (c != '\n' || depth != 1)
                continue;
            int next = index + 1;
            int end = container.IndexOf('\n', next);
            string text = (end < 0 ? container.Substring(next) : container.Substring(next, end - next)).Trim();
            bool isPrivate = text.StartsWith("private ", StringComparison.Ordinal);
            if (!isPrivate && !text.StartsWith("internal ", StringComparison.Ordinal) && !text.StartsWith("public ", StringComparison.Ordinal))
                continue;
            int typeKeyword = text.IndexOf(" class ", StringComparison.Ordinal);
            if (typeKeyword < 0)
                typeKeyword = text.IndexOf(" struct ", StringComparison.Ordinal);
            if (typeKeyword >= 0)
            {
                string rest = text.Substring(text.IndexOf(' ', typeKeyword + 1) + 1);
                result.Add(new Declaration(new string(rest.TakeWhile(item => char.IsLetterOrDigit(item) || item == '_').ToArray()), true, null));
                continue;
            }
            int open = text.IndexOf('(');
            int assignment = text.IndexOf('=');
            if (open <= 0 || assignment >= 0 && assignment < open)
            {
                // A field or property: its name precedes the initializer, the terminator or the accessor block.
                string head = text.Split(new[] { '=', ';', '{' }, 2)[0].Trim();
                result.Add(new Declaration(head.Substring(head.LastIndexOf(' ') + 1), true, null));
                continue;
            }
            string signature = text.Substring(0, open);
            int generic = signature.IndexOf('<');
            if (generic >= 0)
                signature = signature.Substring(0, generic);
            string name = signature.Substring(signature.LastIndexOf(' ') + 1);
            string declaration = null;
            if (isPrivate)
            {
                // The declaration starts at its attributes and ends at its body's closing brace or expression terminator.
                int start = next;
                while (start > 1)
                {
                    int previous = container.LastIndexOf('\n', start - 2);
                    string attribute = container.Substring(previous + 1, start - previous - 2).Trim();
                    if (!attribute.StartsWith("[", StringComparison.Ordinal))
                        break;
                    start = previous + 1;
                }
                int parameters = container.IndexOf('(', next);
                int close = Matching(container, parameters, '(', ')');
                int brace = close < 0 ? -1 : container.IndexOf('{', close);
                int arrow = close < 0 ? -1 : container.IndexOf("=>", close, StringComparison.Ordinal);
                int finish = arrow >= 0 && (brace < 0 || arrow < brace) ? container.IndexOf(';', arrow) :
                    brace < 0 ? -1 : Matching(container, brace, '{', '}');
                if (finish > 0)
                    declaration = container.Substring(start, finish - start + 1);
            }
            result.Add(new Declaration(name, isPrivate, declaration));
        }
        return result;
    }

    /// <summary>Reports whether text names an unqualified identifier.</summary>
    /// <param name="text">The text.</param>
    /// <param name="name">The identifier.</param>
    /// <returns>Whether the identifier occurs on its own.</returns>
    private static bool Uses(string text, string name) => Find(text, name, 0) >= 0;

    /// <summary>Finds an unqualified identifier occurrence.</summary>
    /// <param name="text">The text.</param>
    /// <param name="name">The identifier.</param>
    /// <param name="start">The search start.</param>
    /// <returns>The position, or -1.</returns>
    private static int Find(string text, string name, int start)
    {
        int search = start;
        while ((search = text.IndexOf(name, search, StringComparison.Ordinal)) >= 0)
        {
            int after = search + name.Length;
            bool startBoundary = search == 0 || !(char.IsLetterOrDigit(text[search - 1]) || text[search - 1] is '_' or '.');
            bool endBoundary = after >= text.Length || !(char.IsLetterOrDigit(text[after]) || text[after] == '_');
            if (startBoundary && endBoundary)
                return search;
            search = after;
        }
        return -1;
    }

    /// <summary>Renames unqualified member references of one line.</summary>
    /// <param name="line">The line.</param>
    /// <param name="renames">The new spelling of each member.</param>
    /// <returns>The renamed line.</returns>
    private static string Rename(string line, Dictionary<string, string> renames)
    {
        foreach (var rename in renames)
        {
            int search = 0;
            while ((search = Find(line, rename.Key, search)) >= 0)
            {
                line = line.Substring(0, search) + rename.Value + line.Substring(search + rename.Key.Length);
                search += rename.Value.Length;
            }
        }
        return line;
    }

    /// <summary>Finds a class declaration by name.</summary>
    /// <param name="source">The file.</param>
    /// <param name="name">The class name.</param>
    /// <returns>The declaration position, or -1.</returns>
    private static int FindClass(string source, string name)
    {
        string declaration = " class " + name;
        int search = 0;
        while ((search = source.IndexOf(declaration, search, StringComparison.Ordinal)) >= 0)
        {
            int after = search + declaration.Length;
            if (after >= source.Length || !(char.IsLetterOrDigit(source[after]) || source[after] == '_'))
                return search;
            search = after;
        }
        return -1;
    }

    /// <summary>Splits an argument or parameter list at its top-level commas.</summary>
    /// <param name="text">The list without its parentheses.</param>
    /// <returns>The items.</returns>
    private static List<string> SplitArguments(string text)
    {
        var items = new List<string>();
        int depth = 0, start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            char c = text[index];
            if (c is '(' or '<' or '[' or '{')
                depth++;
            else if (c is ')' or '>' or ']' or '}')
                depth--;
            else if (c == ',' && depth == 0)
            {
                items.Add(text.Substring(start, index - start));
                start = index + 1;
            }
        }
        items.Add(text.Substring(start));
        return items;
    }

    /// <summary>Finds the bracket that closes the one at a position, skipping string and character literals.</summary>
    /// <param name="text">The text.</param>
    /// <param name="open">The opening bracket position.</param>
    /// <param name="opening">The opening bracket.</param>
    /// <param name="closing">The closing bracket.</param>
    /// <returns>The closing position, or -1.</returns>
    internal static int Matching(string text, int open, char opening, char closing)
    {
        int depth = 0;
        for (int index = open; index < text.Length; index++)
        {
            char c = text[index];
            if (c is '"' or '\'')
            {
                for (index++; index < text.Length && text[index] != c; index++)
                    if (text[index] == '\\')
                        index++;
                continue;
            }
            if (c == opening)
                depth++;
            else if (c == closing && --depth == 0)
                return index;
        }
        return -1;
    }

    /// <summary>Formats a frozen limit.</summary>
    /// <param name="value">The limit.</param>
    /// <returns>The invariant literal.</returns>
    private static string Literal(int value) => value.ToString(CultureInfo.InvariantCulture);
}
