using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>Selects how covered types are emitted.</summary>
internal enum DirectEmissionMode
{
    /// <summary>Covered types are written directly from their model.</summary>
    On,
    /// <summary>Every type uses the original pipeline.</summary>
    Off,
    /// <summary>Covered types use the original pipeline, and each published file is compared with its direct rendering.</summary>
    Verify,
}

/// <summary>Entry points of model-direct emission.</summary>
/// <remarks>Covered types skip the emitter text, its syntax projections and the publication layout: their final binary and JSON
/// files are rendered from <see cref="DirectTypeModel"/> for the consumer's available aliases. <see cref="Mode"/> is an
/// internal switch for generator development; <see cref="DirectEmissionMode.Verify"/> keeps the original output and records
/// every file the direct renderer would write differently.</remarks>
internal static class DirectEmission
{
    /// <summary>The emission mode, changed only by development harnesses.</summary>
    internal static DirectEmissionMode Mode = DirectEmissionMode.On;

    /// <summary>Where <see cref="DirectEmissionMode.Verify"/> writes mismatching file pairs, or null.</summary>
    internal static string VerifyDirectory;

    /// <summary>Counts of verified and mismatching files in <see cref="DirectEmissionMode.Verify"/>.</summary>
    internal static int Verified, Mismatched;

    private static readonly ConditionalWeakTable<Compilation, StrongBox<bool>> Projects = new();

    /// <summary>Counts uncovered types by the check that excluded them, in <see cref="DirectEmissionMode.Verify"/>.</summary>
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> Rejections = new();

    /// <summary>Records why a type keeps the original pipeline.</summary>
    /// <param name="check">The name of the failed check.</param>
    /// <returns>Null, for the caller to return.</returns>
    internal static T Reject<T>(string check) where T : class
    {
        if (Mode == DirectEmissionMode.Verify)
            Rejections.AddOrUpdate(check, 1, (_, count) => count + 1);
        return null;
    }

    /// <summary>Checks the assembly-wide facts the direct emitters assume, once per compilation.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <returns>Whether no assembly attribute can change generated LuminPack code.</returns>
    internal static bool ProjectFacts(Compilation compilation)
        => Projects.GetValue(compilation, static item =>
        {
            foreach (var attribute in item.Assembly.GetAttributes())
            {
                // The generation mode is not read by the generator; registration mode and forced operations are.
                if (attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackGeneratorOptionsAttribute" &&
                    attribute.NamedArguments.Length == 0 && attribute.ConstructorArguments.All(argument =>
                        argument.Type?.ToDisplayString() == "LuminPack.Attribute.LuminPackGenerationMode" ||
                        argument.Type?.ToDisplayString() == "LuminPack.Attribute.LuminPackRegisterMode" &&
                        Convert.ToInt64(argument.Value, System.Globalization.CultureInfo.InvariantCulture) == 0))
                    continue;
                string name = attribute.AttributeClass?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (name == "LuminPack" || name.StartsWith("LuminPack.", StringComparison.Ordinal))
                    return new StrongBox<bool>(false);
            }
            return new StrongBox<bool>(true);
        }).Value;

    /// <summary>Renders one format of a covered type for the consumer's aliases.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="json">Whether the JSON file is rendered, otherwise the binary file.</param>
    /// <param name="aliases">The aliases available in the consuming compilation.</param>
    /// <returns>The complete published file.</returns>
    /// <param name="operations">The operations the project calls; the members of the others are left out.</param>
    internal static string Render(DirectTypeModel model, bool json, int aliases, LuminPack.Code.Core.GeneratedOperations operations)
    {
        var writer = new DirectWriter(aliases);
        if (json)
            DirectJsonEmitter.Emit(model, writer, operations);
        else if (model.IsStruct)
            DirectStructEmitter.Emit(model, writer, operations);
        else
            DirectBinaryEmitter.Emit(model, writer, operations);
        return GeneratedSourceLayout.LayOutLines(writer.Complete(json));
    }

    /// <summary>Compares the original published file with the direct rendering and records a mismatch.</summary>
    /// <param name="name">The hint name.</param>
    /// <param name="original">The original published text.</param>
    /// <param name="direct">The direct rendering.</param>
    internal static void Compare(string name, string original, string direct)
    {
        if (string.Equals(original, direct, StringComparison.Ordinal))
        {
            System.Threading.Interlocked.Increment(ref Verified);
            return;
        }
        System.Threading.Interlocked.Increment(ref Mismatched);
        if (VerifyDirectory == null)
            return;
        try
        {
            Directory.CreateDirectory(VerifyDirectory);
            name += original.Contains("RootReadJson") ? ".json" : ".bin";
            File.WriteAllText(Path.Combine(VerifyDirectory, name + ".old.cs"), original);
            File.WriteAllText(Path.Combine(VerifyDirectory, name + ".new.cs"), direct);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>Builds one direct file line by line in the layout the publication formatter produces.</summary>
internal sealed class DirectWriter
{
    private readonly StringBuilder _body = new StringBuilder(16384);
    private readonly int _available;
    private int _used;
    private int _indent;
    private bool _pendingBlank;

    /// <summary>Creates a writer for the consumer's available aliases.</summary>
    internal DirectWriter(int aliases) => _available = aliases;

    /// <summary>Discards lines and alias uses while set, for the members of operations the project does not call.</summary>
    internal bool Suppressed;

    /// <summary>Spells a helper type through its alias when the consumer leaves the alias available.</summary>
    /// <param name="alias">The alias name.</param>
    /// <returns>The alias or the fully qualified target.</returns>
    internal string A(string alias)
    {
        int index = GeneratedSourceLayout.AliasIndex(alias);
        if ((_available & (1 << index)) == 0)
            return GeneratedSourceLayout.AliasTarget(index);
        if (!Suppressed)
            _used |= 1 << index;
        return alias;
    }

    /// <summary>Spells an attribute through its alias, as <c>@Preserve</c>, or its qualified name without the suffix.</summary>
    internal string Attribute(string alias)
    {
        string name = A(alias);
        return name == alias ? "@" + alias : name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - 9) : name;
    }

    /// <summary>The <c>[Preserve]</c> attribute line.</summary>
    internal string Preserve => "[" + Attribute("Preserve") + "]";

    /// <summary>The <c>[MethodImpl(option)]</c> attribute line.</summary>
    internal string MethodImpl(string option) => "[" + Attribute("MethodImpl") + "(" + A("MethodImplOptions") + "." + option + ")]";

    /// <summary>Appends one line at the current indentation.</summary>
    internal void Line(string text)
    {
        if (Suppressed)
            return;
        if (_pendingBlank)
        {
            _body.Append(Environment.NewLine);
            _pendingBlank = false;
        }
        _body.Append(' ', _indent * 4).Append(text).Append(Environment.NewLine);
    }

    /// <summary>Requests a blank line before the next line; a following closing brace drops it.</summary>
    internal void Blank()
    {
        if (!Suppressed)
            _pendingBlank = true;
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
    internal void Close(string suffix = "")
    {
        if (!Suppressed)
            _pendingBlank = false;
        _indent--;
        Line("}" + suffix);
    }

    /// <summary>Runs an action one level deeper without braces.</summary>
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
        var header = new StringBuilder(_body.Length + 2048);
        string newLine = Environment.NewLine;
        header.Append("#nullable enable annotations").Append(newLine);
        header.Append("#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649, CS1591").Append(newLine);
        foreach (string space in new[] { "System", "System.Collections.Generic", "System.Runtime.CompilerServices", "System.Runtime.InteropServices",
            "System.Threading.Tasks", "LuminPack", "LuminPack.Code", "LuminPack.Core", "LuminPack.Utility", "LuminPack.Attribute", "LuminPack.Generated" })
            header.Append("using global::").Append(space).Append(';').Append(newLine);
        for (int index = 0; index < GeneratedSourceLayout.AliasCount; index++)
            if ((_used & (1 << index)) != 0)
                header.Append("using ").Append(GeneratedSourceLayout.AliasName(index)).Append(" = ").Append(GeneratedSourceLayout.AliasTarget(index))
                    .Append(';').Append(newLine);
        header.Append(newLine);
        // The published file ends with its last closing brace.
        int end = _body.Length;
        while (end > 0 && (_body[end - 1] == '\n' || _body[end - 1] == '\r'))
            end--;
        return header.Append(_body.ToString(0, end)).ToString();
    }
}

/// <summary>Counts call sites in emitted statements the way the inlining policy counts invocation syntax.</summary>
internal static class DirectCalls
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
                    // Skip a generic argument list.
                    int depth = 0;
                    for (end--; end >= 0; end--)
                    {
                        if (text[end] == '>') depth++;
                        else if (text[end] == '<' && --depth == 0) break;
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
                // A constructor call is an object creation, not an invocation.
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
