using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Spells the generated container, helper and formatter-owner names from a payload identity.</summary>
/// <remarks>These names are part of the generated contract: the extension container, the JSON name table, the layout mirror
/// and the binary formatter owner must agree with the files the other generators publish.</remarks>
internal static class GeneratedNames
{
    /// <summary>Names the assembly-specific extension container.</summary>
    /// <param name="assemblyName">The consuming assembly name.</param>
    /// <returns>The extension container identity.</returns>
    internal static string Extension(string assemblyName) => "LuminPackExtensions_" + Sanitize(assemblyName ?? "Assembly");

    /// <summary>Sanitizes an assembly or type name into one identifier segment.</summary>
    /// <param name="name">The spelling to sanitize.</param>
    /// <returns>The identifier spelling.</returns>
    internal static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Unknown";
        var builder = new StringBuilder(name.Length);
        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            if (index == 0)
            {
                if (char.IsLetter(character) || character == '_')
                    builder.Append(character);
                else if (char.IsDigit(character))
                    builder.Append('_').Append(character);
                else
                    builder.Append('_');
            }
            else if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
            }
            else if (builder[builder.Length - 1] != '_')
            {
                builder.Append('_');
            }
        }
        return builder.ToString();
    }

    /// <summary>Names the JSON name-table helper class.</summary>
    /// <param name="name">The unsanitized helper identity, such as <c>Json_global_N_Model</c>.</param>
    /// <returns>The helper class name.</returns>
    internal static string JsonHelperName(string name)
    {
        string type = name.Substring("Json_".Length);
        if (type.StartsWith("global_", StringComparison.Ordinal))
            type = type.Substring("global_".Length);
        return "Json" + new string(type.Where(char.IsLetterOrDigit).ToArray());
    }

    /// <summary>Names the binary formatter owner of a payload.</summary>
    /// <param name="container">The assembly extension container.</param>
    /// <param name="payload">The payload spelling.</param>
    /// <returns>The formatter owner class name.</returns>
    internal static string Owner(string container, string payload) => "Binary_" + container + "_" + ReadableTypeName.Of(PayloadKey(payload));

    /// <summary>Reduces a payload spelling to its identity key.</summary>
    /// <param name="type">The payload spelling.</param>
    /// <returns>The key without global qualification, nullability or spacing.</returns>
    internal static string PayloadKey(string type) => type.Replace("global::", string.Empty).Replace("?", string.Empty).Replace(" ", string.Empty);

    /// <summary>Reduces a payload spelling to a key that does not depend on how the type was written.</summary>
    /// <param name="type">The payload spelling, as a call site or an owner requirement spells it.</param>
    /// <returns>The key, with tuple syntax spelled as <c>System.ValueTuple</c>, tuple element names dropped and native integers
    /// spelled as <c>System.IntPtr</c> and <c>System.UIntPtr</c>.</returns>
    /// <remarks>Call sites spell tuples and native integers with their C# keywords, while owner requirements spell their
    /// metadata names; both must find the same owner.</remarks>
    internal static string CanonicalKey(string type)
    {
        string text = type.Replace("global::", string.Empty).Replace("?", string.Empty);
        int index = 0;
        var key = new StringBuilder(text.Length + 16);
        Canonical(text, ref index, key, stop: '\0');
        return key.ToString();
    }

    /// <summary>Handles canonical for <c>GeneratedNames</c>.</summary>
    /// <param name="text">The text supplied to <c>Canonical</c>.</param>
    /// <param name="index">The zero-based item index.</param>
    /// <param name="key">The key identifying the entry.</param>
    /// <param name="stop">The stop supplied to <c>Canonical</c>.</param>
    private static void Canonical(string text, ref int index, StringBuilder key, char stop)
    {
        var token = new StringBuilder();
        void Flush()
        {
            string word = token.ToString();
            key.Append(word == "nint" ? "System.IntPtr" : word == "nuint" ? "System.UIntPtr" : word);
            token.Clear();
        }
        while (index < text.Length && text[index] != stop)
        {
            char current = text[index];
            if (current == '(')
            {
                Flush();
                index++;
                key.Append("System.ValueTuple<");
                bool first = true;
                while (index < text.Length && text[index] != ')')
                {
                    if (!first)
                        key.Append(',');
                    first = false;
                    // One tuple element: its type, then an optional element name after a top-level space.
                    var element = new StringBuilder();
                    Canonical(text, ref index, element, stop: ',', tupleElement: true);
                    key.Append(element);
                    if (index < text.Length && text[index] == ',')
                        index++;
                }
                index++;
                key.Append('>');
                continue;
            }
            if (current is '<' or '>' or ',' or '[' or ']')
            {
                Flush();
                key.Append(current);
                index++;
                if (current == '<')
                {
                    Canonical(text, ref index, key, stop: '>');
                    if (index < text.Length)
                    {
                        key.Append('>');
                        index++;
                    }
                }
                continue;
            }
            if (current != ' ')
                token.Append(current);
            else
                Flush();
            index++;
        }
        Flush();
    }

    /// <summary>Handles canonical for <c>GeneratedNames</c>.</summary>
    /// <param name="text">The text supplied to <c>Canonical</c>.</param>
    /// <param name="index">The zero-based item index.</param>
    /// <param name="key">The key identifying the entry.</param>
    /// <param name="stop">The stop supplied to <c>Canonical</c>.</param>
    /// <param name="tupleElement">Whether to enable tuple element for this operation.</param>
    private static void Canonical(string text, ref int index, StringBuilder key, char stop, bool tupleElement)
    {
        // A tuple element ends at a top-level comma or at the closing parenthesis; a trailing name follows the last space.
        int depth = 0, start = index;
        while (index < text.Length)
        {
            char current = text[index];
            if (current is '<' or '(' or '[')
                depth++;
            else if (current is '>' or ']' || current == ')' && depth > 0)
                depth--;
            else if (depth == 0 && (current == stop || current == ')'))
                break;
            index++;
        }
        string element = text.Substring(start, index - start).Trim();
        int name = LastTopLevelSpace(element);
        if (name > 0)
            element = element.Substring(0, name);
        int inner = 0;
        Canonical(element, ref inner, key, stop: '\0');
    }

    /// <summary>Handles last top level space for <c>GeneratedNames</c>.</summary>
    /// <param name="element">The collection element or its formatter contract.</param>
    /// <returns>The resulting <c>int</c>.</returns>
    private static int LastTopLevelSpace(string element)
    {
        int depth = 0, found = -1;
        for (int index = 0; index < element.Length; index++)
        {
            char current = element[index];
            if (current is '<' or '(' or '[')
                depth++;
            else if (current is '>' or ')' or ']')
                depth--;
            else if (current == ' ' && depth == 0)
                found = index;
        }
        // A space directly after a comma inside the type is not a name separator; only a space followed by an identifier at the
        // end qualifies.
        return found > 0 && found < element.Length - 1 && char.IsLetter(element[found + 1]) && element.IndexOf(',', found) < 0 &&
            element.IndexOf('>', found) < 0 ? found : -1;
    }
}

/// <summary>Spells a payload type as a short identifier for generated members and containers.</summary>
/// <remarks>A type name is spelled without its namespace unless the compilation declares that simple name in more than one
/// namespace (or beside a common framework type); such names keep their namespace, as in <c>OtherPoint</c>, so their owners and
/// aliases stay distinct. The ambiguous names are a compilation fact that the analysis and publication stages install with
/// <see cref="Use"/>.</remarks>
internal static class ReadableTypeName
{
    [ThreadStatic]
    private static HashSet<string>? _ambiguous;

    /// <summary>Installs the compilation's ambiguous simple type names for the current thread until the scope is disposed.</summary>
    /// <param name="names">The ambiguous names, or an empty array.</param>
    /// <returns>The scope that restores the previous names.</returns>
    internal static Scope Use(string[] names)
    {
        HashSet<string>? previous = _ambiguous;
        _ambiguous = names.Length == 0 ? null : new HashSet<string>(names, StringComparer.Ordinal);
        return new Scope(previous);
    }

    /// <summary>Restores the ambiguous names that were installed before a <see cref="Use"/> call.</summary>
    internal readonly struct Scope : IDisposable
    {
        private readonly HashSet<string>? _previous;

        /// <summary>Initializes a <c>Scope</c> from the supplied data.</summary>
        /// <param name="previous">The previous supplied to <c>Scope</c>.</param>
        internal Scope(HashSet<string>? previous) => _previous = previous;

        /// <inheritdoc/>
        public void Dispose() => _ambiguous = _previous;
    }

    /// <summary>Spells a type.</summary>
    /// <param name="spelling">A C# type spelling, optionally qualified with <c>global::</c>.</param>
    /// <returns>The identifier.</returns>
    internal static string Of(string spelling)
    {
        string text = spelling.Replace("global::", string.Empty).Replace(" ", string.Empty);
        int index = 0;
        var name = new StringBuilder(text.Length);
        Type(text, ref index, name);
        return name.Length == 0 || !char.IsLetter(name[0]) ? "Type" + name : name.ToString();
    }

    /// <summary>Handles type for <c>ReadableTypeName</c>.</summary>
    /// <param name="text">The text supplied to <c>Type</c>.</param>
    /// <param name="index">The zero-based item index.</param>
    /// <param name="name">The name to look up or emit.</param>
    private static void Type(string text, ref int index, StringBuilder name)
    {
        var part = new StringBuilder();
        bool closed = false;
        if (index < text.Length && text[index] == '(')
        {
            index++;
            closed = true;
            part.Append("ValueTupleOf");
            Arguments(text, ref index, ')', part);
        }
        else
        {
            var segment = new StringBuilder();
            var qualifier = new StringBuilder();
            bool escaped = false;
            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] is '_' or '.' or ':' or '@'))
            {
                if (text[index] == '.')
                {
                    qualifier.Append(segment);
                    segment.Clear();
                    escaped = false;
                }
                else if (text[index] == ':')
                {
                    qualifier.Clear();
                    segment.Clear();
                    escaped = false;
                }
                else if (text[index] == '@')
                {
                    escaped = true;
                }
                else
                {
                    segment.Append(text[index]);
                }
                index++;
            }
            string simple = segment.ToString();
            // A simple name the compilation declares in several namespaces keeps its namespace and containing types.
            if (_ambiguous != null && qualifier.Length != 0 && _ambiguous.Contains(simple))
                part.Append(qualifier).Append(simple);
            // Verbatim identifiers such as @int name declarations rather than the built-in scalar.
            else if (escaped)
                part.Append("Verbatim").Append(simple);
            else
                part.Append(Keyword(simple));
            if (index < text.Length && text[index] == '<')
            {
                index++;
                closed = true;
                part.Append("Of");
                Arguments(text, ref index, '>', part);
                // A type nested in a generic type keeps that type and its arguments as a prefix, so Outer<int>.Inner,
                // Outer<string>.Inner and the definition's Outer<T>.Inner name different owners.
                if (index < text.Length && text[index] == '.')
                {
                    index++;
                    Type(text, ref index, part);
                }
            }
        }
        while (index < text.Length)
        {
            if (text[index] == '?')
            {
                part.Insert(0, "Nullable");
                index++;
            }
            else if (text[index] == '[')
            {
                int rank = 1;
                for (index++; index < text.Length && text[index] != ']'; index++)
                    if (text[index] == ',')
                        rank++;
                index++;
                // An array of a closed generic type is separated from its last argument, so List<int>[] and List<int[]> differ.
                part.Append(closed ? "_Array" : "Array");
                closed = false;
                if (rank > 1)
                    part.Append(rank);
            }
            else if (text[index] == '*')
            {
                part.Append("Pointer");
                index++;
            }
            else
            {
                break;
            }
        }
        name.Append(part);
    }

    /// <summary>Handles arguments for <c>ReadableTypeName</c>.</summary>
    /// <param name="text">The text supplied to <c>Arguments</c>.</param>
    /// <param name="index">The zero-based item index.</param>
    /// <param name="close">The close supplied to <c>Arguments</c>.</param>
    /// <param name="name">The name to look up or emit.</param>
    private static void Arguments(string text, ref int index, char close, StringBuilder name)
    {
        bool first = true;
        while (index < text.Length && text[index] != close)
        {
            if (text[index] == ',')
            {
                index++;
                continue;
            }
            if (!first)
                name.Append("And");
            first = false;
            int start = index;
            Type(text, ref index, name);
            while (index < text.Length && text[index] != ',' && text[index] != close)
                index++;
            if (index == start)
                index++;
        }
        index++;
    }

    /// <summary>Handles keyword for <c>ReadableTypeName</c>.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <returns>The resulting text.</returns>
    private static string Keyword(string name) => name switch
    {
        "int" => "Int",
        "uint" => "UInt",
        "long" => "Long",
        "ulong" => "ULong",
        "short" => "Short",
        "ushort" => "UShort",
        "byte" => "Byte",
        "sbyte" => "SByte",
        "bool" => "Bool",
        "char" => "Char",
        "float" => "Float",
        "double" => "Double",
        "decimal" => "Decimal",
        "string" => "String",
        "object" => "Object",
        "nint" => "NInt",
        "nuint" => "NUInt",
        _ => name.Replace("_", string.Empty),
    };
}
