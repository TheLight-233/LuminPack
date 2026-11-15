using System;
using System.Text.RegularExpressions;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Spaces generated declarations without reparsing or normalizing their algorithm text.</summary>
internal static class SourcePresentation
{
    private static readonly Regex Declarations = new(@"(?m)(?<prior>[^\r\n])\r?\n(?<indent>[ \t]*)(?=(?:\[|///|(?:public|internal|private|protected) static))", RegexOptions.Compiled);
    private static readonly Regex Blanks = new(@"(?:\r?\n)(?:[ \t]*\r?\n){2,}", RegexOptions.Compiled);

    /// <summary>Separates methods and their documentation while keeping related attribute and documentation lines together.</summary>
    /// <param name="source">The completed source text.</param>
    /// <returns>The source with readable method separation.</returns>
    internal static string Apply(string source)
    {
        source = Declarations.Replace(source, match => match.Groups["prior"].Value is ";" or "}" ?
            match.Groups["prior"].Value + Environment.NewLine + Environment.NewLine + match.Groups["indent"].Value : match.Value);
        return Blanks.Replace(source, Environment.NewLine + Environment.NewLine).TrimEnd() + Environment.NewLine;
    }
    /// <summary>Reconstructs one file while collapsing only redundant blank lines.</summary>
    internal sealed class Builder : IDisposable
    {
        private readonly PooledTextBuffer _text;
        private int _lineStart;
        private bool _content;
        private bool _blank;

        /// <summary>Rents transient storage for one final source file.</summary>
        /// <param name="capacity">The expected character count.</param>
        internal Builder(int capacity) => _text = new PooledTextBuffer(capacity);

        /// <summary>Appends a complete source fragment.</summary>
        /// <param name="text">The source fragment.</param>
        internal void Append(string text) => Append(text, 0, text.Length);

        /// <summary>Appends a resolved invocation without building a combined string.</summary>
        /// <param name="call">The resolved target and its explicit arguments.</param>
        internal void Append(CallEdit call)
        {
            Append(call.Target);
            if (call.Generics.Length != 0)
            {
                Append("<");
                for (int index = 0; index < call.Generics.Length; index++)
                {
                    if (index != 0)
                        Append(", ");
                    Append(call.Generics[index]);
                }
                Append(">");
            }
            Append("(");
            for (int index = 0; index < call.Arguments.Length; index++)
            {
                if (index != 0)
                    Append(", ");
                Append(call.Arguments[index]);
            }
            Append(")");
        }

        /// <summary>Appends a source slice without creating an intermediate substring.</summary>
        /// <param name="length">The selected character count.</param>
        /// <param name="start">The inclusive character offset.</param>
        /// <param name="text">The original generated text.</param>
        internal void Append(string text, int start, int length)
        {
            int end = start + length;
            while (start < end)
            {
                int newline = text.IndexOf('\n', start, end - start);
                int stop = newline < 0 ? end : newline;
                int contentEnd = stop > start && text[stop - 1] == '\r' ? stop - 1 : stop;
                if (contentEnd > start)
                {
                    _text.Append(text, start, contentEnd - start);
                    if (!_content)
                        for (int index = start; index < contentEnd; index++)
                            if (text[index] is not (' ' or '\t'))
                            {
                                _content = true;
                                break;
                            }
                }
                if (newline >= 0)
                {
                    if (!_content)
                    {
                        _text.Length = _lineStart;
                        if (_blank)
                        {
                            start = newline + 1;
                            continue;
                        }
                    }
                    _text.Append('\n');
                    _blank = !_content;
                    _content = false;
                    _lineStart = _text.Length;
                }
                start = stop + 1;
            }
        }

        /// <summary>Returns a completed file with one final newline.</summary>
        /// <returns>The immutable, independently owned final source text.</returns>
        internal string Complete()
        {
            while (_text.Length > 1 && char.IsWhiteSpace(_text[_text.Length - 1]))
                _text.Length--;
            _text.Append('\n');
            return _text.ToString();
        }

        /// <summary>Returns transient character storage even when publication is cancelled.</summary>
        public void Dispose() => _text.Dispose();
    }

}
