using System;
using System.Collections.Generic;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Normalizes and parses the closed generic framework types that have a standalone formatter.</summary>
/// <remarks>The identity is the namespace-qualified definition with its comma-separated arguments and no spacing, such as
/// <c>System.Nullable&lt;int&gt;</c>. Nested arguments retain their own closed formatter identities.</remarks>
internal static class GenericIdentity
{
    private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal", "char",
    };

    /// <summary>The nullable value definition.</summary>
    internal const string Nullable = "System.Nullable";

    /// <summary>The key and value pair definition.</summary>
    internal const string KeyValuePair = "System.Collections.Generic.KeyValuePair";

    /// <summary>The lazy value definition.</summary>
    internal const string Lazy = "System.Lazy";

    /// <summary>The reference tuple definition.</summary>
    internal const string Tuple = "System.Tuple";

    /// <summary>The value tuple definition.</summary>
    internal const string ValueTuple = "System.ValueTuple";

    /// <summary>The grouping definition.</summary>
    internal const string Grouping = "System.Linq.IGrouping";

    /// <summary>The lookup definition.</summary>
    internal const string Lookup = "System.Linq.ILookup";

    /// <summary>The writable memory definition.</summary>
    internal const string Memory = "System.Memory";

    /// <summary>The read-only memory definition.</summary>
    internal const string ReadOnlyMemory = "System.ReadOnlyMemory";

    /// <summary>The array segment definition.</summary>
    internal const string ArraySegment = "System.ArraySegment";

    /// <summary>The read-only sequence definition.</summary>
    internal const string ReadOnlySequence = "System.Buffers.ReadOnlySequence";

    /// <summary>Tests whether an argument is a raw scalar keyword.</summary>
    /// <param name="argument">The argument spelling.</param>
    /// <returns>Whether the argument is a raw scalar keyword.</returns>
    internal static bool IsRaw(string argument) => Keywords.Contains(argument);

    /// <summary>Normalizes a displayed type to a supported generic identity.</summary>
    /// <param name="display">The displayed type, with or without global qualification and spacing.</param>
    /// <returns>The identity, or null when the type is not a supported closed generic.</returns>
    internal static string? Of(string display)
    {
        string text = display.Replace("global::", string.Empty).Replace(" ", string.Empty);
        string definition;
        string[] arguments;
        if (text.Length > 2 && text[0] == '(' && text[text.Length - 1] == ')')
        {
            definition = ValueTuple;
            arguments = text.Substring(1, text.Length - 2).Split(',');
        }
        else if (text.Length > 1 && text[text.Length - 1] == '?' && text.IndexOf('<') < 0)
        {
            definition = Nullable;
            arguments = new[] { text.Substring(0, text.Length - 1) };
        }
        else
        {
            int open = text.IndexOf('<');
            if (open <= 0 || text[text.Length - 1] != '>')
                return null;
            definition = text.Substring(0, open);
            arguments = SplitArguments(text.Substring(open + 1, text.Length - open - 2));
        }

        bool supported = definition switch
        {
            Nullable => arguments.Length == 1 && arguments[0] != "string",
            Lazy or Memory or ReadOnlyMemory or ArraySegment or ReadOnlySequence => arguments.Length == 1,
            Grouping or Lookup => arguments.Length == 2,
            KeyValuePair => arguments.Length == 2,
            Tuple => arguments.Length is >= 1 and <= 8,
            ValueTuple => arguments.Length is >= 1 and <= 8,
            _ => false,
        };
        return supported ? definition + "<" + string.Join(",", arguments) + ">" : null;
    }

    /// <summary>Recognizes the canonical spelling of a supported nullable scalar.</summary>
    /// <param name="type">The generated element spelling.</param>
    /// <returns>The nullable owner identity, or null.</returns>
    internal static string? NullableScalar(string? type)
    {
        if (type == null || !type.StartsWith("global::" + Nullable + "<", StringComparison.Ordinal))
            return null;
        return Of(type);
    }

    /// <summary>Splits an identity into its definition and arguments.</summary>
    /// <param name="identity">The normalized identity.</param>
    /// <param name="definition">Receives the definition.</param>
    /// <param name="arguments">Receives the arguments.</param>
    /// <returns>Whether the identity is a closed generic.</returns>
    internal static bool TryParse(string identity, out string definition, out string[] arguments)
    {
        int open = identity.IndexOf('<');
        if (open <= 0 || identity[identity.Length - 1] != '>')
        {
            definition = string.Empty;
            arguments = Array.Empty<string>();
            return false;
        }
        definition = identity.Substring(0, open);
        arguments = SplitArguments(identity.Substring(open + 1, identity.Length - open - 2));
        return true;
    }

    /// <summary>Splits only at the current generic level, retaining nested generic and array commas.</summary>
    /// <param name="text">The text supplied to <c>SplitArguments</c>.</param>
    /// <returns>The resulting <c>string[]</c>.</returns>
    private static string[] SplitArguments(string text)
    {
        var arguments = new List<string>();
        int start = 0, depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char item = text[i];
            if (item is '<' or '[' or '(')
                depth++;
            if (item is '>' or ']' or ')')
                depth--;
            if (item == ',' && depth == 0)
            {
                arguments.Add(text.Substring(start, i - start));
                start = i + 1;
            }
        }
        arguments.Add(text.Substring(start));
        return arguments.ToArray();
    }
}
