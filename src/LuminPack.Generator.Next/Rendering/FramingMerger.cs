using System;
using System.Collections.Generic;
using System.Buffers;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Shares record bodies directly in the source writer's statement representation.</summary>
internal static class FramingMerger
{
    private const string Frame = "typeof(TLuminFraming) == typeof(global::LuminPack.Code.LuminPackObjectFraming)";

    /// <summary>Shares the emitter's prepared operation lists before any source or declaration is rendered.</summary>
    /// <param name="ordinary">The prepared ordinary-object statements.</param>
    /// <param name="payload">The prepared Union-payload statements.</param>
    /// <returns>The shared statements with framing-specific branches.</returns>
    internal static List<string> Share(IReadOnlyList<string> ordinary, IReadOnlyList<string> payload)
    {
        var lines = new List<string>();
        Render(Merge(Parse(ordinary), Parse(payload)), lines);
        return lines;
    }

    /// <summary>Reads the writer's existing line and brace representation into statement groups.</summary>
    /// <param name="source">The original source contribution.</param>
    /// <returns>The grouped statement representation.</returns>
    private static List<Node> Parse(IReadOnlyList<string> source)
    {
        var lines = new List<string>(source.Count);
        foreach (string raw in source)
        {
            if (raw.IndexOf('\n') >= 0)
            {
                foreach (string part in raw.Split('\n'))
                {
                    string trimmed = part.Trim();
                    if (trimmed.Length != 0)
                        lines.Add(trimmed);
                }
            }
            else
            {
                string trimmed = raw.Trim();
                if (trimmed.Length != 0)
                    lines.Add(trimmed);
            }
        }
        int position = 0;
        List<Node> Block()
        {
            var nodes = new List<Node>();
            while (position < lines.Count && lines[position] != "}" && lines[position] != "};")
                nodes.Add(Statement());
            if (position < lines.Count)
                position++;
            return nodes;
        }
        Node Statement()
        {
            string header = lines[position++];
            while (lines[position - 1].StartsWith("[", StringComparison.Ordinal) && position < lines.Count)
                header += "\n" + lines[position++];
            var node = new Node(header);
            if (header == "{")
            {
                node.Header = string.Empty;
                node.Children = Block();
                node.Suffix = position > 0 && lines[position - 1] == "};" ? ";" : string.Empty;
                node.Braces = true;
            }
            // A complete statement never owns the scope block that follows it.
            else if (position < lines.Count && lines[position] == "{" && !header.EndsWith(";", StringComparison.Ordinal))
            {
                position++;
                node.Children = Block();
                node.Suffix = position > 0 && lines[position - 1] == "};" ? ";" : string.Empty;
                node.Braces = true;
            }
            else if ((header.StartsWith("if (", StringComparison.Ordinal) || header.StartsWith("for (", StringComparison.Ordinal) ||
                header.StartsWith("foreach (", StringComparison.Ordinal)) && header.EndsWith(")", StringComparison.Ordinal) && position < lines.Count)
            {
                node.Children = new List<Node> { Statement() };
                node.Braces = true;
            }
            if (header.StartsWith("if (", StringComparison.Ordinal) && position < lines.Count && lines[position] == "else")
            {
                position++;
                node.Else = Statement();
            }
            return node;
        }
        return Block();
    }

    /// <summary>Aligns common statements and guards only the differing ranges.</summary>
    /// <param name="left">The preceding facts or ordinary statement groups.</param>
    /// <param name="right">The current facts or payload statement groups.</param>
    /// <returns>The aligned common and framing-specific statements.</returns>
    private static List<Node> Merge(List<Node> left, List<Node> right)
    {
        int stride = right.Count + 1;
        int count = checked((left.Count + 1) * stride);
        const int maximumPooledLength = 65536;
        int[] lengths = count <= maximumPooledLength ? ArrayPool<int>.Shared.Rent(count) : new int[count];
        try
        {
            Array.Clear(lengths, 0, count);
            for (int i = left.Count - 1; i >= 0; i--)
                for (int j = right.Count - 1; j >= 0; j--)
                    lengths[i * stride + j] = Matches(left[i], right[j]) ? lengths[(i + 1) * stride + j + 1] + 1 : Math.Max(lengths[(i + 1) * stride + j], lengths[i * stride + j + 1]);
            var result = new List<Node>();
            var onlyLeft = new List<Node>();
            var onlyRight = new List<Node>();
            int a = 0, b = 0;
            while (a < left.Count || b < right.Count)
            {
                if (a < left.Count && b < right.Count && Matches(left[a], right[b]))
                {
                    Flush();
                    Node l = left[a++], r = right[b++];
                    if (l.Children != null && r.Children != null)
                        l.Children = Merge(l.Children, r.Children);
                    else if (l.Header != r.Header)
                    {
                        l.Header = l.DeclarationPrefix + "(" + Frame + " ? " + l.Initializer + " : " + r.Initializer + ");";
                    }
                    result.Add(l);
                }
                else if (a < left.Count && (b == right.Count || lengths[(a + 1) * stride + b] >= lengths[a * stride + b + 1]))
                    onlyLeft.Add(left[a++]);
                else
                    onlyRight.Add(right[b++]);
            }
            Flush();
            return result;
            void Flush()
            {
                if (onlyLeft.Count == 0 && onlyRight.Count == 0)
                    return;
                // A goto cannot enter a block, so a framing-specific label stays in the shared block and the statements around
                // it keep their own branches. The other framing passes the label without jumping to it.
                int leftLabel = LabelIndex(onlyLeft), rightLabel = LabelIndex(onlyRight);
                if (leftLabel >= 0 || rightLabel >= 0)
                {
                    bool left = leftLabel >= 0 && (rightLabel < 0 || onlyRight.Count == 0 || leftLabel <= rightLabel);
                    List<Node> labelled = left ? onlyLeft : onlyRight, other = left ? onlyRight : onlyLeft;
                    int label = left ? leftLabel : rightLabel;
                    var before = labelled.GetRange(0, label);
                    int after = label + 1 < labelled.Count && labelled[label + 1].Header == ";" ? label + 2 : label + 1;
                    var rest = labelled.GetRange(after, labelled.Count - after);
                    Node mark = labelled[label];
                    var tail = new List<Node>(other);
                    labelled.Clear();
                    labelled.AddRange(before);
                    other.Clear();
                    Flush();
                    result.Add(mark);
                    result.Add(new Node(";"));
                    (left ? onlyLeft : onlyRight).AddRange(rest);
                    (left ? onlyRight : onlyLeft).AddRange(tail);
                    Flush();
                    return;
                }
                var branch = new Node("if (" + (onlyLeft.Count == 0 ? "!(" + Frame + ")" : Frame) + ")")
                {
                    Children = new List<Node>(onlyLeft.Count == 0 ? onlyRight : onlyLeft),
                    Braces = true,
                };
                if (onlyLeft.Count != 0 && onlyRight.Count != 0)
                    branch.Else = new Node(string.Empty) { Children = new List<Node>(onlyRight), Braces = true };
                result.Add(branch);
                onlyLeft.Clear();
                onlyRight.Clear();
            }
        }
        finally
        {
            if (lengths.Length <= maximumPooledLength)
                ArrayPool<int>.Shared.Return(lengths);
        }
    }

    /// <summary>Finds the first goto label among framing-specific statements.</summary>
    /// <param name="nodes">The statements of one framing.</param>
    /// <returns>The label's index, or -1.</returns>
    private static int LabelIndex(List<Node> nodes)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            string header = nodes[index].Header;
            if (nodes[index].Children == null && header.Length > 1 && header[header.Length - 1] == ':' && header.IndexOf(' ') < 0 &&
                header != "default:")
                return index;
        }
        return -1;
    }

    /// <summary>Matches equal statements, equal control-flow headers and corresponding local declarations.</summary>
    /// <param name="left">The preceding facts or ordinary statement groups.</param>
    /// <param name="right">The current facts or payload statement groups.</param>
    /// <returns>Whether the two statement groups can share their operation.</returns>
    private static bool Matches(Node left, Node right)
    {
        if (left.Header == right.Header && left.Suffix == right.Suffix && left.Braces == right.Braces && (left.Children == null) == (right.Children == null) &&
            (left.Else == null && right.Else == null || left.Else?.Identity == right.Else?.Identity))
            return true;
        if (left.Children != null || right.Children != null)
            return false;
        return left.DeclarationPrefix != null && left.DeclarationPrefix == right.DeclarationPrefix;
    }

    /// <summary>Writes the aligned statement groups with their original branch structure.</summary>
    /// <param name="lines">The output statement lines.</param>
    /// <param name="nodes">The aligned statement groups.</param>
    private static void Render(List<Node> nodes, List<string> lines)
    {
        foreach (Node node in nodes)
        {
            if (node.Header.Length != 0)
            {
                if (node.Header.IndexOf('\n') < 0)
                    lines.Add(node.Header);
                else
                    lines.AddRange(node.Header.Split('\n'));
            }
            if (node.Children != null)
            {
                if (node.Braces)
                    lines.Add("{");
                Render(node.Children, lines);
                if (node.Braces)
                    lines.Add("}" + node.Suffix);
            }
            if (node.Else != null)
            {
                lines.Add("else");
                Render(new List<Node> { node.Else }, lines);
            }
        }
    }

    /// <summary>One existing statement and its optional nested blocks.</summary>
    private sealed class Node
    {
        internal string Header;
        internal string Suffix = string.Empty;
        internal readonly string? DeclarationPrefix;
        internal readonly string? Initializer;
        internal List<Node>? Children;
        internal Node? Else;
        internal bool Braces;
        private string? _identity;
        internal string Identity
        {
            get
            {
                if (_identity != null)
                    return _identity;
                using var text = new PooledTextBuffer(128);
                text.Append(Header).Append('{');
                if (Children != null)
                    for (int index = 0; index < Children.Count; index++)
                    {
                        if (index != 0)
                            text.Append('\n');
                        text.Append(Children[index].Identity);
                    }
                text.Append('}');
                if (Else != null)
                    text.Append(Else.Identity);
                return _identity = text.ToString();
            }
        }

        /// <summary>Captures the statement's existing header.</summary>
        /// <param name="header">The original statement header.</param>
        internal Node(string header)
        {
            Header = header;
            int equals = header.IndexOf(" = ", StringComparison.Ordinal);
            if (equals > 0 && header.EndsWith(";", StringComparison.Ordinal))
            {
                if (header.IndexOf(' ', 0, equals) > 0 && header.IndexOf('(', 0, equals) < 0)
                {
                    DeclarationPrefix = header.Substring(0, equals + 3);
                    Initializer = header.Substring(equals + 3, header.Length - equals - 4);
                }
            }
        }
    }
}
