#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
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
    internal static List<string> InputChecks(PayloadRenderModel model, CodeWriter w, List<string> body, bool read)
    {
        InputLimits limits = read ? model.Limits : null;
        // Under a bounded root, a nested read whose own entry applies other checks runs as the root's checked copy.
        InputRedirect redirect = read ? model.Redirect : null;
        bool project = limits != null && limits.Bounded;
        if (!project && redirect == null)
        {
            // Unbounded bodies are owned by their emitter. Remove only the collection placeholders in place.
            int retained = 0;
            for (int index = 0; index < body.Count; index++)
            {
                string line = body[index];
                if (line.Length == 0 || line[0] != CollectionCheck)
                {
                    if (retained != index)
                        body[retained] = line;
                    retained++;
                }
            }
            body.RemoveRange(retained, body.Count - retained);
            return body;
        }
        var result = new List<string>(body.Count + 4);
        for (int index = 0; index < body.Count; index++)
        {
            string line = body[index];
            if (line.Length != 0 && line[0] == CollectionCheck)
            {
                // A count beyond the declared limit fails after the kernel has bounded it by the input.
                if (limits != null && limits.Collection != int.MaxValue)
                    result.Add(w.A("LuminPackInputPolicy") + ".CheckLength(" + line.Substring(1) + ", " + Literal(limits.Collection) + ");");
                continue;
            }
            if (project)
                line = InputReadRenderer.Project(line, limits, w.A("LuminPackInputPolicy"));
            // A raw primitive collection keeps its runtime kernel; the placeholder after it checks the count.
            bool rawCollection = index + 1 < body.Count && body[index + 1].Length != 0 && body[index + 1][0] == CollectionCheck;
            if (redirect != null && !rawCollection)
                line = redirect.Apply(line);
            result.Add(line);
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

    /// <summary>Formats a frozen limit.</summary>
    /// <param name="value">The limit.</param>
    /// <returns>The invariant literal.</returns>
    private static string Literal(int value) => value.ToString(CultureInfo.InvariantCulture);
}
