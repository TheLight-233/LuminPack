using System;
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
}

/// <summary>Spells a payload type as a short identifier for generated members and containers.</summary>
internal static class ReadableTypeName
{
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
            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] is '_' or '.' or ':'))
            {
                if (text[index] is '.' or ':')
                    segment.Clear();
                else
                    segment.Append(text[index]);
                index++;
            }
            part.Append(Keyword(segment.ToString()));
            if (index < text.Length && text[index] == '<')
            {
                index++;
                closed = true;
                part.Append("Of");
                Arguments(text, ref index, '>', part);
                if (index < text.Length && text[index] == '.')
                {
                    index++;
                    part.Clear();
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
