#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Freezes an untrusted root's declared limits into its read bodies at the actual read sites.</summary>
/// <remarks>
/// This is the original safe-read projection over a finished read body. Counts, strings and nesting are checked where they are
/// read, against the root's limits as constants; an unbounded limit adds no statement, so those reads keep their ordinary call
/// sites. Nested objects expanded into the root body are checked with the root's limits.
/// </remarks>
internal static partial class BinaryEmitter
{
    /// <summary>Starts the placeholder a raw primitive collection read leaves for the root's collection limit.</summary>
    private const char CollectionCheck = '\u0001';

    /// <summary>Leaves the collection-limit placeholder after a raw primitive array or list read.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The collection member.</param>
    /// <param name="target">The member storage expression.</param>
    /// <remarks>The runtime kernel already bounds such a count by the input; only a declared limit adds the check, which the
    /// root's projection resolves.</remarks>
    private static void AddCollectionCheck(List<string> body, RenderMember member, string target)
    {
        if (member.Kind is RenderMemberKind.Array or RenderMemberKind.List && CheckedRawElement(member.Scalar))
            body.Add(CollectionCheck + target);
    }

    /// <summary>Identifies the reviewed primitives whose raw array and list kernels need only count and domain checks.</summary>
    /// <param name="scalar">The element spelling.</param>
    /// <returns>Whether the element is one of those primitives.</returns>
    private static bool CheckedRawElement(string scalar) => scalar is "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or
        "long" or "ulong" or "float" or "double" or "char" or "global::System.Boolean" or "global::System.Byte" or "global::System.SByte" or
        "global::System.Int16" or "global::System.UInt16" or "global::System.Int32" or "global::System.UInt32" or "global::System.Int64" or
        "global::System.UInt64" or "global::System.Single" or "global::System.Double" or "global::System.Char" or "global::System.Guid" or
        "global::System.TimeSpan";

    /// <summary>Applies the root's frozen limits to one finished operation body.</summary>
    /// <param name="model">The root.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="body">The finished body.</param>
    /// <param name="read">Whether the operation reads input.</param>
    /// <returns>The checked body.</returns>
    private static List<string> InputChecks(PayloadRenderModel model, CodeWriter w, List<string> body, bool read)
    {
        InputLimits limits = read ? model.Limits : null;
        var result = new List<string>(body.Count + 4);
        foreach (string line in body)
        {
            if (line.Length != 0 && line[0] == CollectionCheck)
            {
                // A count beyond the declared limit fails after the kernel has bounded it by the input.
                if (limits != null && limits.Collection != int.MaxValue)
                    result.Add(w.A("LuminPackInputPolicy") + ".CheckLength(" + line.Substring(1) + ", " + Literal(limits.Collection) + ");");
                continue;
            }
            result.Add(limits != null && limits.String != int.MaxValue ? StringLimit(line, w, limits.String) : line);
        }
        if (limits == null || !model.Recursive || limits.Depth == int.MaxValue)
            return result;
        // A null record is not a nested value: depth is entered after the object template's null test, and every later
        // successful return leaves it.
        string exit = w.A("LuminPackInputPolicy") + ".ExitDepth(ref reader);";
        // Only the root's own leading null test counts; an inline child's marker test lies further in the body, and a Union
        // payload read has no null test at all.
        int start = 0;
        if (result.Count != 0 && (result[0].Contains(".ReadNullMarker(") || result[0].Contains("reader.Read<byte>() == 255")))
        {
            start = 1;
            if (result.Count > 1 && result[1] == "\treturn;")
                start = 2;
            else if (result.Count > 1 && result[1] == "{")
            {
                // A block after the null test ends at its matching brace.
                for (int depth = 0, index = 1; index < result.Count; index++)
                {
                    if (result[index] == "{")
                        depth++;
                    else if (result[index] is "}" or "};" && --depth == 0)
                    {
                        start = index + 1;
                        break;
                    }
                }
            }
        }
        var checkedBody = new List<string>(result.Count + 6);
        for (int index = 0; index < start; index++)
            checkedBody.Add(result[index]);
        checkedBody.Add(w.A("LuminPackInputPolicy") + ".EnterDepth(ref reader, " + Literal(limits.Depth) + ");");
        for (int index = start; index < result.Count; index++)
        {
            string line = result[index];
            if (line == "return;")
            {
                checkedBody.Add(exit);
                checkedBody.Add(line);
            }
            else if (line == "\treturn;")
            {
                checkedBody.Add("{");
                checkedBody.Add(exit);
                checkedBody.Add("return;");
                checkedBody.Add("}");
            }
            else
                checkedBody.Add(line);
        }
        checkedBody.Add(exit);
        return checkedBody;
    }

    /// <summary>Redirects the string reads of one line to the checked adapters with the frozen string limit.</summary>
    /// <param name="line">The body line.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="limit">The declared string limit.</param>
    /// <returns>The checked line.</returns>
    /// <remarks>A string record reads through <c>reader.Read&lt;TProfile&gt;(ref storage)</c>, and a reused string through
    /// <c>reader.Read&lt;TProfile&gt;(current)</c>; scalar reads carry no argument and stay unchanged.</remarks>
    private static string StringLimit(string line, CodeWriter w, int limit)
    {
        const string call = "reader.Read<";
        int search = 0;
        StringBuilder rewritten = null;
        int copied = 0;
        while ((search = line.IndexOf(call, search, StringComparison.Ordinal)) >= 0)
        {
            // The receiver must be the reader itself, not a member that ends in the same name.
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
            string argument = end < 0 ? string.Empty : line.Substring(close + 2, end - close - 2);
            if (end < 0 || argument.Length == 0 || argument.IndexOf(',') >= 0)
            {
                search += call.Length;
                continue;
            }
            string policy = w.A("LuminPackInputPolicy");
            string replacement = argument.StartsWith("ref ", StringComparison.Ordinal)
                ? policy + ".ReadStringRecord<" + profile + ">(ref reader, " + argument + ", " + Literal(limit) + ")"
                : policy + ".ReadString<" + profile + ">(ref reader, " + argument + ", " + Literal(limit) + ")";
            rewritten ??= new StringBuilder(line.Length + 64);
            rewritten.Append(line, copied, search - copied).Append(replacement);
            copied = end + 1;
            search = end + 1;
        }
        if (rewritten == null)
            return line;
        return rewritten.Append(line, copied, line.Length - copied).ToString();
    }

    /// <summary>Finds the bracket that closes the one at a position.</summary>
    /// <param name="text">The text.</param>
    /// <param name="open">The opening bracket position.</param>
    /// <param name="opening">The opening bracket.</param>
    /// <param name="closing">The closing bracket.</param>
    /// <returns>The closing position, or -1.</returns>
    private static int Matching(string text, int open, char opening, char closing)
    {
        int depth = 0;
        for (int index = open; index < text.Length; index++)
        {
            if (text[index] == opening)
                depth++;
            else if (text[index] == closing && --depth == 0)
                return index;
        }
        return -1;
    }

    /// <summary>Formats a frozen limit.</summary>
    /// <param name="value">The limit.</param>
    /// <returns>The invariant literal.</returns>
    private static string Literal(int value) => value.ToString(CultureInfo.InvariantCulture);
}
