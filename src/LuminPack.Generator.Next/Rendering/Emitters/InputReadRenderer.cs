#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>The checked-copy context of one bounded untrusted root.</summary>
/// <remarks>
/// Under a root with declared collection, string or depth limits, a nested read whose own entry applies other checks is not
/// called: it runs as a copy of that entry's read body with the root's limits frozen, as the original <c>InputRead_</c> helpers
/// did. Copies redirect their own nested reads the same way, so a recursive child reuses its one copy.
/// </remarks>
internal sealed class InputRedirect
{
    private readonly HashSet<string> _copyable;
    private readonly HashSet<string> _requested = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Captures the root and the targets whose read bodies can be copied.</summary>
    /// <param name="root">The root's readable name, whose own entry already applies these limits.</param>
    /// <param name="extension">The consumer's extension container name.</param>
    /// <param name="limits">The root's frozen limits.</param>
    /// <param name="copyable">The readable names of owners and payloads with a copyable read body.</param>
    internal InputRedirect(string root, string extension, InputLimits limits, HashSet<string> copyable)
    {
        Root = root;
        Limits = limits;
        OwnerPrefix = "Binary_" + extension + "_";
        Helpers = "LuminInputReads_" + root;
        _copyable = copyable;
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
    internal readonly List<string> Targets = new List<string>();

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
            const string read = ".Read<";
            bool call = string.CompareOrdinal(line, nameEnd, read, 0, read.Length) == 0;
            // The owner may be spelled through its namespace or directly.
            int start = search;
            const string qualified = "global::LuminPack.";
            if (start >= qualified.Length && string.CompareOrdinal(line, start - qualified.Length, qualified, 0, qualified.Length) == 0)
                start -= qualified.Length;
            bool boundary = start == 0 || !(char.IsLetterOrDigit(line[start - 1]) || line[start - 1] is '_' or '.' or ':');
            string target = line.Substring(nameStart, nameEnd - nameStart);
            if (!call || !boundary || target == Root || !_copyable.Contains(target))
            {
                search = nameEnd;
                continue;
            }
            if (_requested.Add(target))
                Targets.Add(target);
            string replacement = Helpers + ".Read_" + target + "<";
            line = line.Substring(0, start) + replacement + line.Substring(nameEnd + read.Length);
            search = start + replacement.Length;
        }
        return line;
    }
}

/// <summary>Projects frozen input limits onto read statements and renders a bounded root's checked copies.</summary>
internal static class InputReadRenderer
{
    private const string Policy = "global::LuminPack.Security.LuminPackInputPolicy";
    private static readonly string[] Readers = { "reader", "operationState", "state", "reader2" };

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
                line = Kernel(line, reader, "ReadLength", policy, limits.Collection, defaultMinimum: true);
                line = Kernel(line, reader, "TryReadCollectionHead", policy, limits.Collection, defaultMinimum: false);
                line = Kernel(line, reader, "PeekCollectionHeader", policy, limits.Collection, defaultMinimum: false);
            }
        if (limits.String != int.MaxValue)
            foreach (string reader in Readers)
                line = Strings(line, reader, policy, limits.String);
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
    private static string Strings(string line, string reader, string policy, int limit)
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
                ? policy + ".ReadStringRecord<" + profile + ">(ref " + reader + ", " + argument + ", " + Literal(limit) + ")"
                : policy + ".ReadString<" + profile + ">(ref " + reader + ", " + argument + ", " + Literal(limit) + ")";
            line = line.Substring(0, search) + replacement + line.Substring(end + 1);
            search += replacement.Length;
        }
        return line;
    }

    /// <summary>Renders the checked copies that a bounded root's body requested, and the copies they request in turn.</summary>
    /// <param name="redirect">The root's checked-copy context, after its own body was rendered.</param>
    /// <param name="payloads">The retained payload render models by readable name, with their recursion facts.</param>
    /// <param name="sources">The shared owner files.</param>
    /// <param name="aliases">The alias mask the consumer leaves available.</param>
    /// <param name="operations">The operations the project calls.</param>
    /// <returns>The helper file, or an empty string when nothing was redirected.</returns>
    internal static string RenderCopies(InputRedirect redirect, IReadOnlyDictionary<string, PayloadRenderModel> payloads,
        IReadOnlyList<string> sources, int aliases, GeneratedOperations operations)
    {
        var methods = new List<string>();
        var usings = new SortedSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < redirect.Targets.Count; index++)
        {
            string target = redirect.Targets[index];
            string source;
            if (payloads.TryGetValue(target, out PayloadRenderModel payload))
                source = PayloadEmitter.Render(payload.WithInput(redirect.Limits, payload.Recursive, redirect), json: false, aliases, operations);
            else
                source = sources.FirstOrDefault(text => FindClass(text, "LuminRoots_" + target) >= 0);
            if (source == null)
                throw new InvalidOperationException("The checked copy of '" + target + "' has no source.");
            foreach (string line in source.Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.StartsWith("using ", StringComparison.Ordinal) && trimmed.EndsWith(";", StringComparison.Ordinal))
                    usings.Add(trimmed);
            }
            methods.Add(Copy(source, target, redirect, payload != null));
        }
        if (methods.Count == 0)
            return string.Empty;
        var file = new StringBuilder(4096);
        file.Append("// <auto-generated/>\n#nullable enable annotations\n#pragma warning disable CS0162, CS0164, CS0168, CS0219, CS0436, CS0649, CS1591\n");
        foreach (string line in usings)
            file.Append(line).Append('\n');
        file.Append("\nnamespace LuminPack\n{\n    public static partial class LuminPackSerializer\n    {\n");
        file.Append("        /// <summary>Checked copies of the nested reads under one bounded untrusted root, with its limits frozen.</summary>\n");
        file.Append("        internal static class ").Append(redirect.Helpers).Append("\n        {\n");
        file.Append(string.Join("\n", methods));
        file.Append("        }\n    }\n}\n");
        return file.ToString();
    }

    /// <summary>Copies one target's read body under the root's limits, or forwards to its own entry when the body cannot move.</summary>
    /// <param name="source">The target's file.</param>
    /// <param name="target">The target's readable name.</param>
    /// <param name="redirect">The root's checked-copy context.</param>
    /// <param name="projected">Whether the payload emitter already applied the limits to the body.</param>
    /// <returns>The helper method.</returns>
    private static string Copy(string source, string target, InputRedirect redirect, bool projected)
    {
        int classStart = FindClass(source, "LuminRoots_" + target);
        int classOpen = source.IndexOf('{', classStart);
        int classClose = Matching(source, classOpen, '{', '}');
        string container = source.Substring(classOpen, classClose - classOpen + 1);
        // A record of fixed members publishes a root read without a profile parameter.
        string signature = "RootRead<TLuminProfile>(";
        int method = container.IndexOf(signature, StringComparison.Ordinal);
        if (method < 0)
        {
            signature = "RootRead(";
            method = container.IndexOf(" " + signature, StringComparison.Ordinal) + 1;
        }
        if (method <= 0)
            throw new InvalidOperationException("The checked copy of '" + target + "' has no root read.");
        int parametersOpen = method + signature.Length - 1;
        int parametersClose = Matching(container, parametersOpen, '(', ')');
        string parameters = container.Substring(parametersOpen, parametersClose - parametersOpen + 1);
        int bodyOpen = container.IndexOf('{', parametersClose);
        int arrow = container.IndexOf("=>", parametersClose, StringComparison.Ordinal);
        string body;
        if (arrow >= 0 && arrow < bodyOpen)
        {
            int semicolon = container.IndexOf(';', arrow);
            body = container.Substring(arrow + 2, semicolon - arrow - 1).Trim();
        }
        else
            body = container.Substring(bodyOpen + 1, Matching(container, bodyOpen, '{', '}') - bodyOpen - 1);
        string valueType = ValueType(parameters);
        var helper = new StringBuilder(1024);
        helper.Append("            [global::LuminPack.Attribute.Preserve]\n");
        helper.Append("            internal static void Read_").Append(target).Append("<TLuminProfile>(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref ")
            .Append(valueType).Append(" value)\n");
        helper.Append("                where TLuminProfile : struct, global::LuminPack.Option.ILuminPackOptions\n");
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
            helper.Append("                global::LuminPack.").Append(redirect.OwnerPrefix).Append(target)
                .Append(".Read<TLuminProfile>(ref reader, ref value);\n");
        else
            foreach (string raw in body.Split('\n'))
            {
                string line = Rename(raw.TrimEnd('\r'), renames);
                if (!projected)
                    line = redirect.Apply(Project(line, redirect.Limits, Policy));
                helper.Append(line).Append('\n');
            }
        helper.Append("            }\n");
        if (!forward)
            foreach (string companion in companions)
            {
                helper.Append('\n');
                foreach (string raw in companion.Split('\n'))
                    helper.Append(redirect.Apply(Project(Rename(raw.TrimEnd('\r'), renames), redirect.Limits, Policy))).Append('\n');
            }
        return helper.ToString();
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
