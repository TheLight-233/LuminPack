using System;
using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Publishes algorithms before convenience APIs and drops proven-empty generated containers.</summary>
internal static class PublicationOrder
{
    /// <summary>Applies existing edits while moving unguarded serializer APIs to the end of the file.</summary>
    /// <param name="source">The unmodified contribution.</param>
    /// <param name="methods">Its source-writer method boundaries.</param>
    /// <param name="removed">The methods eliminated by publication.</param>
    /// <param name="edits">The ordered operation and declaration edits.</param>
    /// <returns>The final source, reconstructed once in its publication order.</returns>
    internal static string Render(string source, List<GeneratedText.Method> methods, HashSet<GeneratedText.Method> removed,
        List<TextEdit> edits)
    {
        var excluded = new List<(int Start, int End)>();
        List<(int Start, int End)>? apis = null;
        bool conditional = source.IndexOf("#if", StringComparison.Ordinal) >= 0;
        foreach (var edit in edits)
            if (edit.Removes && edit.End > edit.Start)
                excluded.Add((edit.Start, edit.End));
        foreach (GeneratedText.Method method in methods)
            if (!conditional && !removed.Contains(method) && method.Owner == "LuminPackSerializer" && IsApi(method.Name))
            {
                (apis ??= new List<(int Start, int End)>()).Add((method.Start, method.End));
                excluded.Add((method.Start, method.End));
            }
        return Render(source, excluded, apis, edits);
    }

    /// <summary>Orders already projected formatter contributions without rebuilding method contracts.</summary>
    /// <param name="source">The assembled formatter file.</param>
    /// <returns>The final formatted source.</returns>
    internal static string RenderJoined(string source)
    {
        var apis = GeneratedText.ReadApiRanges(source);
        return Render(source, apis == null ? new() : new(apis), apis, NoEdits);
    }

    private static readonly List<TextEdit> NoEdits = new();

    /// <summary>Reconstructs the file from sorted source ranges and moves facade entries to its end.</summary>
    /// <param name="source">The unmodified contribution.</param>
    /// <param name="excluded">Source ranges removed from their original positions.</param>
    /// <param name="apis">Facade methods moved to the end.</param>
    /// <param name="edits">The ordered publication edits.</param>
    /// <returns>The final source.</returns>
    private static string Render(string source, List<(int Start, int End)> excluded, List<(int Start, int End)>? apis,
        List<TextEdit> edits)
    {
        Sort(excluded);
        RemoveEmpty(source, "internal static partial class LuminPackExtensions_", excluded);
        if (apis != null)
            RemoveEmpty(source, "public static partial class LuminPackSerializer", excluded);
        RemoveEmpty(source, "namespace LuminPack.Generated", excluded);
        RemoveEmpty(source, "namespace LuminPack", excluded);

        using var output = new SourcePresentation.Builder(source.Length);
        int copied = 0;
        int editIndex = 0;
        foreach (var range in excluded)
        {
            if (range.End <= copied)
                continue;
            if (range.Start > copied)
                Append(output, source, copied, range.Start, edits, ref editIndex);
            copied = range.End;
            while (editIndex < edits.Count && edits[editIndex].Start < copied)
                editIndex++;
        }
        Append(output, source, copied, source.Length, edits, ref editIndex);
        if (apis != null)
        {
            output.Append("\nnamespace LuminPack\n{\n    public static partial class LuminPackSerializer\n    {\n");
            editIndex = 0;
            foreach (var method in apis)
            {
                Append(output, source, method.Start, method.End, edits, ref editIndex);
                output.Append("\n\n");
            }
            output.Append("    }\n}\n");
        }
        return output.Complete();
    }

    /// <summary>Distinguishes convenience entries from cursor and member-writing algorithms.</summary>
    /// <param name="name">The original generated method name.</param>
    /// <returns>Whether the method belongs below the algorithms.</returns>
    internal static bool IsApi(string name) => name is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or
        "SerializeAsync" or "DeserializeAsync" || name.StartsWith("Serialize_", StringComparison.Ordinal) ||
        name.StartsWith("Deserialize_", StringComparison.Ordinal) || name.StartsWith("SerializeJson_", StringComparison.Ordinal) ||
        name.StartsWith("DeserializeJson_", StringComparison.Ordinal) || name.StartsWith("SerializeAsync_", StringComparison.Ordinal) ||
        name.StartsWith("DeserializeAsync_", StringComparison.Ordinal);

    /// <summary>Removes only containers whose projected body contains no remaining member or directive.</summary>
    /// <param name="source">The original source.</param>
    /// <param name="declaration">The known generated declaration prefix.</param>
    /// <param name="excluded">The source ranges omitted from the algorithm section.</param>
    private static void RemoveEmpty(string source, string declaration, List<(int Start, int End)> excluded)
    {
        int position = 0;
        while ((position = source.IndexOf(declaration, position, StringComparison.Ordinal)) >= 0)
        {
            int start = position;
            position += declaration.Length;
            if (declaration.StartsWith("namespace ", StringComparison.Ordinal) && position < source.Length &&
                !char.IsWhiteSpace(source[position]) && source[position] != '{')
                continue;
            int line = source.LastIndexOf('\n', start) + 1;
            if (!Whitespace(source, line, start))
                continue;
            bool alreadyRemoved = false;
            foreach (var range in excluded)
                if (range.Start <= start && start < range.End)
                {
                    alreadyRemoved = true;
                    break;
                }
            if (alreadyRemoved)
                continue;
            if (!declaration.StartsWith("namespace ", StringComparison.Ordinal))
            {
                int prior = line - 1;
                while (prior >= 0 && char.IsWhiteSpace(source[prior]))
                    prior--;
                if (prior >= 0 && source[prior] == ']')
                    continue;
            }
            int open = source.IndexOf('{', position);
            if (open < 0)
                continue;
            int copied = open + 1;
            foreach (var range in excluded)
            {
                if (range.End <= copied)
                    continue;
                while (copied < range.Start && char.IsWhiteSpace(source[copied]))
                    copied++;
                if (copied < range.Start)
                    break;
                copied = Math.Max(copied, range.End);
            }
            while (copied < source.Length && char.IsWhiteSpace(source[copied]))
                copied++;
            // Every excluded range is a complete balanced declaration. Its first remaining brace is the container's end.
            if (copied < source.Length && source[copied] == '}')
            {
                excluded.Add((start, copied + 1));
                Sort(excluded);
                position = copied + 1;
            }
        }
    }

    /// <summary>Checks the exact characters left between removed declarations.</summary>
    /// <param name="source">The original source.</param>
    /// <param name="start">The inclusive range start.</param>
    /// <param name="end">The exclusive range end.</param>
    /// <returns>Whether the range contains whitespace only.</returns>
    private static bool Whitespace(string source, int start, int end)
    {
        for (int index = start; index < end; index++)
            if (!char.IsWhiteSpace(source[index]))
                return false;
        return true;
    }

    /// <summary>Orders excluded ranges so outer containers can subsume their removed members.</summary>
    /// <param name="ranges">The excluded source ranges.</param>
    private static void Sort(List<(int Start, int End)> ranges) => ranges.Sort(static (left, right) =>
        left.Start != right.Start ? left.Start.CompareTo(right.Start) : right.End.CompareTo(left.End));

    /// <summary>Copies a selected source range with its existing redirects and signature edits.</summary>
    /// <param name="output">The final source builder.</param>
    /// <param name="source">The original source.</param>
    /// <param name="start">The inclusive range start.</param>
    /// <param name="end">The exclusive range end.</param>
    /// <param name="edits">The ordered publication edits.</param>
    /// <param name="editIndex">The next edit to consume.</param>
    private static void Append(SourcePresentation.Builder output, string source, int start, int end,
        List<TextEdit> edits, ref int editIndex)
    {
        int copied = start;
        while (editIndex < edits.Count)
        {
            var edit = edits[editIndex];
            if (edit.Start > end || edit.Start == end && edit.End != edit.Start)
                break;
            editIndex++;
            if (edit.Start < copied || edit.End > end)
                continue;
            output.Append(source, copied, edit.Start - copied);
            if (edit.Text != null)
                output.Append(edit.Text);
            else
                output.Append(edit.Call!);
            copied = edit.End;
        }
        output.Append(source, copied, end - copied);
    }
}
