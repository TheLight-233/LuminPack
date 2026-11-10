using System;
using System.Text;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>The single source writer: every renderer appends through it, so indentation and blank lines have one owner.</summary>
internal sealed class SourceWriter
{
    private readonly StringBuilder _body = new StringBuilder(4096);
    private int _indent;
    private bool _pendingBlank;

    /// <summary>Appends one line at the current indentation.</summary>
    /// <param name="text">The line text.</param>
    internal void Line(string text)
    {
        if (_pendingBlank)
        {
            _body.Append(Environment.NewLine);
            _pendingBlank = false;
        }
        _body.Append(' ', _indent * 4).Append(text).Append(Environment.NewLine);
    }

    /// <summary>Requests a blank line before the next line.</summary>
    internal void Blank() => _pendingBlank = true;

    /// <summary>Appends a line without indentation, for a construct that keeps its own column.</summary>
    /// <param name="text">The complete line.</param>
    internal void Raw(string text)
    {
        _pendingBlank = false;
        _body.Append(text).Append(Environment.NewLine);
    }

    /// <summary>Opens a brace block.</summary>
    internal void Open()
    {
        _pendingBlank = false;
        Line("{");
        _indent++;
    }

    /// <summary>Closes a brace block.</summary>
    /// <param name="suffix">Text appended after the closing brace, such as a semicolon.</param>
    internal void Close(string suffix = "")
    {
        _pendingBlank = false;
        _indent--;
        Line("}" + suffix);
    }

    /// <summary>Completes the file with the shared header, the used aliases and the queued body.</summary>
    /// <param name="context">The render context that owns the alias table.</param>
    /// <returns>The unformatted file text.</returns>
    internal string Complete(RenderContext context)
    {
        var header = new StringBuilder(_body.Length + 512);
        header.Append("#nullable enable annotations").Append(Environment.NewLine);
        header.Append("#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649, CS1591").Append(Environment.NewLine);
        header.Append("namespace LuminPack").Append(Environment.NewLine);
        context.Aliases.AppendUsings(header);
        header.Append(Environment.NewLine);
        header.Append(_body);
        return header.ToString().TrimEnd();
    }
}
