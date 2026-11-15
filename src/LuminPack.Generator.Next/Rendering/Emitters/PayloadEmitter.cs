using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Entry points of the model-direct emission that the Next pipeline owns.</summary>
/// <remarks>The emitters below this boundary consume only <see cref="PayloadRenderModel"/> values; no Roslyn symbol, legacy
/// analysis object or old generator service participates. Union and reference-tracked payloads keep their placeholders until
/// their model facts are ported.</remarks>
internal static class PayloadEmitter
{
    /// <summary>Renders one format of a covered payload.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="json">Whether the JSON file is rendered, otherwise the binary file.</param>
    /// <param name="aliases">The aliases available in the consuming compilation.</param>
    /// <param name="operations">The operations the project calls.</param>
    /// <returns>The complete published file, or an empty string when this renderer does not cover the payload yet.</returns>
    internal static string Render(PayloadRenderModel model, bool json, int aliases, GeneratedOperations operations)
    {
        if (model.UnionRoot)
            return string.Empty;
        using var writer = new CodeWriter(aliases) { Scoped = model.Net8, Json = json };
        if (json)
        {
            if (model.Circle)
                CircleJsonEmitter.Emit(model, writer, operations);
            else
                JsonEmitter.Emit(model, writer, operations);
        }
        else if (model.Circle)
            CircleBinaryEmitter.Emit(model, writer, operations);
        else if (model.IsStruct)
            StructEmitter.Emit(model, writer, operations);
        else
            BinaryEmitter.Emit(model, writer, operations);
        return PublicationLayout.LayOutLines(writer.Complete(json));
    }

    /// <summary>Records why a type keeps the original pipeline.</summary>
    /// <param name="check">The name of the failed check.</param>
    /// <returns>Null, for the caller to return.</returns>
    /// <typeparam name="T">The value type processed by the operation.</typeparam>
    internal static T? Reject<T>(string check) where T : class => null;

    /// <summary>Spells a member's read or snapshot local: leading underscores are dropped and the first letter is lower-cased.</summary>
    /// <param name="name">The member name.</param>
    /// <returns>The local stem.</returns>
    internal static string LocalName(string name)
    {
        string trimmed = name.TrimStart('_');
        return trimmed.Length == 0 ? name : char.ToLowerInvariant(trimmed[0]) + trimmed.Substring(1);
    }
}

/// <summary>Builds one direct file line by line in the publication layout.</summary>
internal sealed class CodeWriter : IDisposable
{
    private readonly PooledTextBuffer _body = new(16384);
    private readonly GeneratedText.Capture _capture = new();
    private readonly int _available;
    private int _used;
    private int _indent;
    private bool _pendingBlank;

    /// <summary>Creates a writer for the consumer's available aliases.</summary>
    /// <param name="aliases">The alias mask.</param>
    internal CodeWriter(int aliases) => _available = aliases < 0 ? int.MaxValue : aliases;

    /// <summary>Discards lines and alias uses while set, for the members of operations the project does not call.</summary>
    internal bool Suppressed;

    private static readonly Regex MemberAccess = new(@"(?<=[\w\)\]!>])\.([A-Za-z_][A-Za-z0-9_]*)\b");

    /// <summary>Escapes a member whose name is a reserved C# keyword where the line accesses it.</summary>
    /// <param name="text">The line.</param>
    /// <returns>The line with <c>@</c>-escaped keyword members.</returns>
    /// <remarks>A reserved keyword never follows a member-access dot in valid source, so the escape cannot change other code;
    /// JSON property names are string literals and keep the declared spelling.</remarks>
    internal static string EscapeKeywordMembers(string text)
    {
        List<int>? insertions = null;
        for (int index = text.IndexOf('.'); index >= 0; index = text.IndexOf('.', index + 1))
        {
            int start = index + 1;
            if (start >= text.Length || text[start] < 'a' || text[start] > 'z')
                continue;
            int end = start + 1;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                end++;
            if (Keyword(text, start, end - start))
                (insertions ??= new List<int>()).Add(start);
        }
        int equals = text.IndexOf('=');
        if (equals > 0)
        {
            int start = 0;
            int end = equals;
            while (start < end && char.IsWhiteSpace(text[start]))
                start++;
            while (end > start && char.IsWhiteSpace(text[end - 1]))
                end--;
            if (!(end - start == 4 && string.CompareOrdinal(text, start, "this", 0, 4) == 0) && Keyword(text, start, end - start))
                (insertions ??= new List<int>()).Add(start);
            else if (text.IndexOf(" static ", 0, equals, StringComparison.Ordinal) >= 0 && text.IndexOf('(', 0, equals) < 0)
            {
                Match match = StaticDeclaration.Match(text);
                if (match.Success && Keyword(match.Groups[2].Value))
                    (insertions ??= new List<int>()).Add(match.Groups[2].Index);
            }
        }
        if (equals < 0 && text.IndexOf(" static ", StringComparison.Ordinal) >= 0 && text.IndexOf('{') >= 0)
        {
            Match match = StaticDeclaration.Match(text);
            if (match.Success && Keyword(match.Groups[2].Value))
                (insertions ??= new List<int>()).Add(match.Groups[2].Index);
        }
        if (insertions == null)
            return text;
        insertions.Sort();
        var result = new StringBuilder(text.Length + insertions.Count);
        int copied = 0;
        foreach (int position in insertions)
        {
            result.Append(text, copied, position - copied).Append('@');
            copied = position;
        }
        return result.Append(text, copied, text.Length - copied).ToString();
    }

    private static readonly Regex StaticDeclaration = new(@"^(\s*(?:internal|private|public) static [^=(]*\s)([A-Za-z_][A-Za-z0-9_]*)(\s*(?:=|\{))");

    private static readonly Regex InitializerAssignment = new(@"^(\s*)([A-Za-z_][A-Za-z0-9_]*)(\s*=[^=>])");

    /// <summary>Whether a name is a reserved C# keyword.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <returns>Whether the input satisfies the keyword check.</returns>
    private static bool Keyword(string name) => Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsReservedKeyword(
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name));

    private static readonly string[][] Keywords = CreateKeywords();

    /// <summary>Groups reserved keywords by first character for allocation-free source-slice comparisons.</summary>
    /// <returns>The lowercase keyword buckets.</returns>
    private static string[][] CreateKeywords()
    {
        var buckets = new List<string>[26];
        foreach (string keyword in ("abstract as base bool break byte case catch char checked class const continue decimal default delegate " +
            "do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal " +
            "is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed " +
            "short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using " +
            "virtual void volatile while").Split(' '))
        {
            int index = keyword[0] - 'a';
            (buckets[index] ??= new List<string>()).Add(keyword);
        }
        var result = new string[26][];
        for (int index = 0; index < result.Length; index++)
            result[index] = buckets[index]?.ToArray() ?? Array.Empty<string>();
        return result;
    }

    /// <summary>Tests a token without allocating a substring for each member access or assignment.</summary>
    /// <param name="text">The generated line.</param>
    /// <param name="start">The token start.</param>
    /// <param name="length">The token length.</param>
    /// <returns>Whether the slice is a reserved C# keyword.</returns>
    private static bool Keyword(string text, int start, int length)
    {
        if (length != 0 && text[start] == '_')
            return Keyword(text.Substring(start, length));
        if (length == 0 || length > 10 || text[start] < 'a' || text[start] > 'z')
            return false;
        foreach (string keyword in Keywords[text[start] - 'a'])
            if (length == keyword.Length && string.CompareOrdinal(text, start, keyword, 0, length) == 0)
                return true;
        return false;
    }

    /// <summary>Whether the file holds JSON operations, whose compressed members keep their ordinary owner.</summary>
    internal bool Json;

    /// <summary>Resolves a compressed member's owner call for this file.</summary>
    /// <param name="text">The line.</param>
    /// <returns>The binary compressed extension call, or the ordinary JSON owner call.</returns>
    private string CompressedCall(string text)
    {
        int mark = text.IndexOf(RenderMember.CompressedOwner);
        if (Json)
            return text.Remove(mark, 1);
        int start = text.LastIndexOf(' ', mark) + 1;
        string indent = text.Substring(0, start);
        string rest = text.Substring(mark + 1);
        bool write = rest.StartsWith(".Write<", StringComparison.Ordinal);
        int open = rest.IndexOf('(');
        string arguments = rest.Substring(open + 1, rest.LastIndexOf(')') - open - 1);
        int comma = arguments.IndexOf(',');
        string state = arguments.Substring(0, comma).Trim();
        if (state.StartsWith("ref ", StringComparison.Ordinal))
            state = state.Substring(4);
        string target = arguments.Substring(comma + 1).Trim();
        if (target.StartsWith("in ", StringComparison.Ordinal))
            target = target.Substring(3);
        return indent + (write ? state + ".WriteValueWithCompress(" + target + ");" : state + ".ReadValueWithCompress(" + target + ");");
    }

    /// <summary>Whether state and payload parameters keep their <c>scoped</c> modifier.</summary>
    internal bool Scoped = true;

    /// <summary>The current indentation depth of the lines the writer appends.</summary>
    internal int Indent => _indent;

    private static readonly Regex ScopedModifier = new(@"\bscoped (?=(ref|in) )");

    /// <summary>Spells a helper type through its alias when the consumer leaves the alias available.</summary>
    /// <param name="alias">The alias name.</param>
    /// <returns>The alias or the fully qualified target.</returns>
    internal string A(string alias)
    {
        if (alias.StartsWith("global::", StringComparison.Ordinal))
            return alias;
        int index = PublicationLayout.AliasIndex(alias);
        if ((_available & (1 << index)) == 0)
            return PublicationLayout.AliasTarget(index);
        if (!Suppressed)
            _used |= 1 << index;
        return alias;
    }

    /// <summary>Spells an attribute through its alias, as <c>@Preserve</c>, or its qualified name without the suffix.</summary>
    /// <param name="alias">The alias name.</param>
    /// <returns>The attribute spelling.</returns>
    internal string Attribute(string alias)
    {
        string name = A(alias);
        return name == alias ? "@" + alias : name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - 9) : name;
    }

    /// <summary>The <c>[Preserve]</c> attribute line.</summary>
    internal string Preserve => "[" + Attribute("Preserve") + "]";

    /// <summary>The <c>[MethodImpl(option)]</c> attribute line.</summary>
    /// <param name="option">The method implementation option.</param>
    /// <returns>The attribute line.</returns>
    internal string MethodImpl(string option) => "[" + Attribute("MethodImpl") + "(" + A("MethodImplOptions") + "." + option + ")]";

    /// <summary>Appends one line at the current indentation.</summary>
    /// <param name="text">The line.</param>
    internal void Line(string text)
    {
        // A collection-limit placeholder that no root projection resolved adds no statement.
        if (Suppressed || text.Length != 0 && text[0] == '\u0001')
            return;
        if (text.IndexOf(RenderMember.CompressedOwner) >= 0)
            text = CompressedCall(text);
        text = EscapeKeywordMembers(text);
        if (_pendingBlank)
        {
            _body.Append(Environment.NewLine);
            _pendingBlank = false;
        }
        if (!Scoped && text.Contains("scoped "))
            text = ScopedModifier.Replace(text, string.Empty);
        _capture.Line(text, _body.Length, _indent);
        _body.Append(' ', _indent * 4).Append(text).Append(Environment.NewLine);
    }

    /// <summary>Appends one line without the current indentation.</summary>
    /// <param name="text">The complete line.</param>
    internal void Raw(string text)
    {
        if (Suppressed)
            return;
        if (_pendingBlank)
        {
            _body.Append(Environment.NewLine);
            _pendingBlank = false;
        }
        _body.Append(text).Append(Environment.NewLine);
    }

    /// <summary>Requests a blank line before the next line; a following closing brace drops it.</summary>
    internal void Blank()
    {
        if (!Suppressed)
            _pendingBlank = true;
    }

    /// <summary>Appends a blank line at once, which a following opening brace keeps.</summary>
    internal void HardBlank()
    {
        if (Suppressed)
            return;
        _pendingBlank = false;
        _body.Append(Environment.NewLine);
    }

    /// <summary>Opens a brace block.</summary>
    internal void Open()
    {
        if (!Suppressed)
            _pendingBlank = false;
        Line("{");
        _indent++;
    }

    /// <summary>Closes a brace block, dropping a requested blank line.</summary>
    /// <param name="suffix">Text appended after the closing brace.</param>
    internal void Close(string suffix = "")
    {
        if (!Suppressed)
            _pendingBlank = false;
        _indent--;
        Line("}" + suffix);
    }

    /// <summary>Runs an action one level deeper without braces.</summary>
    /// <param name="body">The body.</param>
    internal void Indented(Action body)
    {
        _indent++;
        body();
        _indent--;
    }

    /// <summary>Prefixes the fixed file header and the used aliases.</summary>
    /// <param name="json">Whether the JSON file's header is written.</param>
    /// <returns>The file text before line wrapping.</returns>
    internal string Complete(bool json)
    {
        using var header = new PooledTextBuffer(2048);
        string newLine = Environment.NewLine;
        header.Append("#nullable enable annotations").Append(newLine);
        header.Append("#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649, CS1591").Append(newLine);
        foreach (string space in new[] { "System", "System.Collections.Generic", "System.Runtime.CompilerServices", "System.Runtime.InteropServices",
            "System.Threading.Tasks", "LuminPack", "LuminPack.Code", "LuminPack.Core", "LuminPack.Utility", "LuminPack.Attribute", "LuminPack.Generated" })
            header.Append("using global::").Append(space).Append(';').Append(newLine);
        for (int index = 0; index < PublicationLayout.AliasCount; index++)
            if ((_used & (1 << index)) != 0)
                header.Append("using ").Append(PublicationLayout.AliasName(index)).Append(" = ").Append(PublicationLayout.AliasTarget(index))
                    .Append(';').Append(newLine);
        header.Append(newLine);
        int end = _body.Length;
        while (end > 0 && (_body[end - 1] == '\n' || _body[end - 1] == '\r'))
            end--;
        int headerLength = header.Length;
        _body.Length = end;
        _body.Prepend(header);
        string source = _body.ToString();
        _capture.Complete(source, headerLength);
        return source;
    }

    /// <summary>The lines written so far, without the file header.</summary>
    internal string Text => _body.ToString();

    /// <summary>Returns the output lease after successful emission, failure or cancellation.</summary>
    public void Dispose() => _body.Dispose();
}

/// <summary>Counts call sites in emitted statements the way the inlining policy counts invocation syntax.</summary>
internal static class InvocationCounter
{
    private static readonly HashSet<string> NotCalls = new(StringComparer.Ordinal)
    {
        "if", "while", "for", "foreach", "switch", "return", "typeof", "default", "checked", "unchecked", "sizeof", "nameof", "using", "lock",
        "catch", "when", "new", "in", "out", "ref", "is", "not", "and", "or", "else", "case", "throw", "await", "where", "base", "this",
    };

    /// <summary>Counts invocations: an identifier or generic name directly followed by an argument list.</summary>
    /// <param name="lines">Statements of one body.</param>
    /// <returns>The number of invocation expressions.</returns>
    internal static int Count(IEnumerable<string> lines)
    {
        int count = 0;
        foreach (string line in lines)
        {
            string text = line.TrimStart();
            if (text.StartsWith("//", StringComparison.Ordinal))
                continue;
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] != '(' || index == 0)
                    continue;
                int end = index;
                if (text[end - 1] == '>')
                {
                    int depth = 0;
                    for (end--; end >= 0; end--)
                    {
                        if (text[end] == '>')
                            depth++;
                        else if (text[end] == '<' && --depth == 0)
                            break;
                    }
                    if (end <= 0)
                        continue;
                }
                int start = end;
                while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
                    start--;
                if (start == end || char.IsDigit(text[start]))
                    continue;
                if (NotCalls.Contains(text.Substring(start, end - start)))
                    continue;
                int qualified = start;
                while (qualified > 0 && (char.IsLetterOrDigit(text[qualified - 1]) || text[qualified - 1] is '_' or '.' or ':'))
                    qualified--;
                string before = text.Substring(0, qualified).TrimEnd();
                if (before.EndsWith("new", StringComparison.Ordinal) && (before.Length == 3 || !char.IsLetterOrDigit(before[before.Length - 4])))
                    continue;
                count++;
            }
        }
        return count;
    }
}
