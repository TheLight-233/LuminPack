using System.Text;

namespace LuminPack.Code.Core;

/// <summary>Spells a payload type as a short identifier for the generated members and containers named after it.</summary>
/// <remarks>The identifier joins the simple names of the type and its arguments, such as <c>ArmorItem</c>,
/// <c>ListOfInt</c>, <c>DictionaryOfIntAndString</c>, <c>IntArray</c> and <c>NullableInt</c>. Two payloads can share one
/// identifier, for example types of the same name in different namespaces. Every member named this way takes its payload as a
/// parameter and every container is partial, so such payloads become overloads in one container rather than conflicting
/// declarations.</remarks>
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

    /// <summary>Spells one type and its suffixes.</summary>
    private static void Type(string text, ref int index, StringBuilder name)
    {
        var part = new StringBuilder();
        if (index < text.Length && text[index] == '(')
        {
            index++;
            part.Append("ValueTupleOf");
            Arguments(text, ref index, ')', part);
        }
        else
        {
            // Keep the last segment of a qualified name, with its generic arguments.
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
                part.Append("Of");
                Arguments(text, ref index, '>', part);
                // A nested type of a generic type continues after the arguments.
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
                part.Append("Array");
                if (rank > 1)
                    part.Append(rank);
            }
            else if (text[index] == '*')
            {
                part.Append("Pointer");
                index++;
            }
            else
                break;
        }
        name.Append(part);
    }

    /// <summary>Spells a comma-separated argument list up to its closing character.</summary>
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
            // A tuple element name follows its type; it is not part of the identity.
            while (index < text.Length && text[index] != ',' && text[index] != close)
                index++;
            if (index == start)
                index++;
        }
        index++;
    }

    /// <summary>Capitalizes a C# keyword type.</summary>
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
