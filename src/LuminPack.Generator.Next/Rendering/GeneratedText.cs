using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Reads the explicit declaration and call contracts emitted by the source writers.</summary>
/// <remarks>This scanner handles generated text only. It never binds code or reparses a consuming compilation.</remarks>
internal static class GeneratedText
{
    private static readonly Regex Classes = new(@"(?m)^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly)\s+)*(?:class|struct|interface)\s+(\w+)", RegexOptions.Compiled);
    private static readonly Regex Methods = new(@"(?m)^(?<indent>[ \t]*)(?:public|internal|private|protected)\s+static\s+(?:unsafe\s+)?(?:ref\s+)?[\w.:<>?,\[\]]+\s+(?<name>\w+)\s*(?=<|\()", RegexOptions.Compiled);
    internal static readonly Regex Calls = new(@"(?<![\w@])(?<target>(?:global::)?[A-Za-z_@][\w@]*(?:\.[A-Za-z_@][\w@]*)*)\s*(?=<|\()", RegexOptions.Compiled);
    internal static readonly Regex Words = new(@"(?<![\w@.:])[A-Za-z_@][\w@]*", RegexOptions.Compiled);

    /// <summary>Finds the closing delimiter while skipping generated string and character literals.</summary>
    /// <param name="close">The closing delimiter.</param>
    /// <param name="open">The opening delimiter.</param>
    /// <param name="start">The inclusive character offset.</param>
    /// <param name="text">The original generated text.</param>
    /// <returns>The closing delimiter offset, or minus one for an incomplete range.</returns>
    internal static int Close(string text, int start, char open, char close)
    {
        int depth = 0;
        char quote = '\0';
        for (int i = start; i < text.Length; i++)
        {
            char current = text[i];
            if (quote != '\0')
            {
                if (current == '\\')
                    i++;
                else if (current == quote)
                    quote = '\0';
                continue;
            }
            if (current is '"' or '\'')
            {
                quote = current;
                continue;
            }
            if (current == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i = text.IndexOf('\n', i);
                if (i < 0)
                    return -1;
                continue;
            }
            if (current == open)
                depth++;
            else if (current == close && --depth == 0)
                return i;
        }
        return -1;
    }

    /// <summary>Splits generated parameter or argument lists without splitting nested generic or invocation syntax.</summary>
    /// <param name="text">The original generated text.</param>
    /// <returns>The top-level items without surrounding whitespace.</returns>
    internal static string[] Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();
        var parts = new List<string>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            if (current is '(' or '[' or '<' or '{')
            {
                int end = Close(text, i, current, current == '(' ? ')' : current == '[' ? ']' : current == '<' ? '>' : '}');
                if (end >= 0)
                    i = end;
            }
            else if (current == ',')
            {
                parts.Add(Trimmed(text, start, i - start));
                start = i + 1;
            }
        }
        parts.Add(Trimmed(text, start, text.Length - start));
        return parts.ToArray();
    }

    /// <summary>Copies a trimmed source range once, retaining the original string when possible.</summary>
    /// <param name="text">The original text.</param>
    /// <param name="start">The range start.</param>
    /// <param name="length">The range length.</param>
    /// <param name="leading">Whether leading whitespace is removed.</param>
    /// <returns>The selected text without trailing whitespace.</returns>
    internal static string Trimmed(string text, int start, int length, bool leading = true)
    {
        int end = start + length;
        if (leading)
            while (start < end && char.IsWhiteSpace(text[start]))
                start++;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;
        return start == 0 && end == text.Length ? text : text.Substring(start, end - start);
    }

    /// <summary>Counts indentation without copying the non-whitespace suffix.</summary>
    /// <param name="text">The declaration text.</param>
    /// <returns>The leading whitespace length.</returns>
    private static int LeadingWhitespace(string text)
    {
        int index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;
        return index;
    }

    /// <summary>Reads the trailing declared parameter identifier.</summary>
    /// <param name="parameter">The generated parameter declaration.</param>
    /// <returns>The trailing parameter identifier.</returns>
    internal static string ParameterName(string parameter)
    {
        int end = parameter.IndexOf('=');
        if (end < 0)
            end = parameter.Length;
        while (end > 0 && char.IsWhiteSpace(parameter[end - 1]))
            end--;
        int start = end;
        while (start > 0 && (char.IsLetterOrDigit(parameter[start - 1]) || parameter[start - 1] is '_' or '@'))
            start--;
        return parameter.Substring(start, end - start);
    }

    /// <summary>Reads the source writer's class and static method boundaries.</summary>
    /// <param name="text">The original generated text.</param>
    /// <returns>The explicit static method contracts.</returns>
    internal static List<Method> ReadMethods(string text)
    {
        if (Captured.TryGetValue(text, out var captured))
            return captured;
        var classes = new List<(int Start, int End, string Name)>();
        foreach (Match match in Classes.Matches(text))
        {
            int open = text.IndexOf('{', match.Index + match.Length);
            int end = open < 0 ? -1 : Close(text, open, '{', '}');
            if (end >= 0)
                classes.Add((match.Index, end, match.Groups[1].Value));
        }
        var methods = new List<Method>();
        foreach (Match match in Methods.Matches(text))
        {
            var owner = classes.LastOrDefault(item => item.Start < match.Index && item.End > match.Index);
            if (owner.Name == null)
                continue;
            int position = match.Index + match.Length;
            string generic = string.Empty;
            if (text[position] == '<')
            {
                int close = Close(text, position, '<', '>');
                if (close < 0)
                    continue;
                generic = text.Substring(position + 1, close - position - 1);
                position = close + 1;
            }
            int parameters = text.IndexOf('(', position);
            int parametersEnd = parameters < 0 ? -1 : Close(text, parameters, '(', ')');
            if (parametersEnd < 0)
                continue;
            int body = parametersEnd + 1;
            while (body < text.Length && text[body] != '{' && !(text[body] == '=' && body + 1 < text.Length && text[body + 1] == '>'))
                body++;
            if (body == text.Length)
                continue;
            bool expression = text[body] == '=';
            int end = expression ? text.IndexOf(';', body) : Close(text, body, '{', '}');
            if (end < 0)
                continue;
            int start = match.Index;
            while (start > 0)
            {
                int priorEnd = start - 1;
                if (text[priorEnd] == '\n')
                    priorEnd--;
                if (priorEnd >= 0 && text[priorEnd] == '\r')
                    priorEnd--;
                int priorStart = text.LastIndexOf('\n', Math.Max(0, priorEnd)) + 1;
                string prior = text.Substring(priorStart, Math.Max(0, priorEnd - priorStart + 1)).Trim();
                if (!prior.StartsWith("[", StringComparison.Ordinal) && !prior.StartsWith("///", StringComparison.Ordinal))
                    break;
                start = priorStart;
            }
            string signature = text.Substring(match.Index, body - match.Index).TrimEnd();
            string content = expression ? text.Substring(body + 2, end - body - 2).Trim() : text.Substring(body + 1, end - body - 1);
            methods.Add(new Method(start, end + 1, match.Index, owner.Name, match.Groups["name"].Value, generic,
                text.Substring(parameters + 1, parametersEnd - parameters - 1), signature, content, expression, match.Groups["indent"].Value));
        }
        return methods;
    }

    private static readonly ConditionalWeakTable<string, List<Method>> Captured = new();

    /// <summary>Retains the explicit declaration offsets of a transformed writer contribution.</summary>
    /// <param name="methods">The explicit method contracts.</param>
    /// <param name="source">The completed source text.</param>
    internal static void Register(string source, List<Method> methods) => Captured.Add(source, methods);

    /// <summary>Captures declaration offsets while the source writer already knows every brace boundary.</summary>
    internal sealed class Capture
    {
        private readonly Stack<(int Depth, string Name)> _owners = new();
        private readonly List<Entry> _entries = new();
        private string? _pendingOwner;
        private Entry? _method;
        private int _attributeStart = -1;

        /// <summary>Records a writer line before it reaches the output buffer.</summary>
        /// <param name="depth">The current declaration nesting depth.</param>
        /// <param name="offset">The current output character count.</param>
        /// <param name="text">The original generated text.</param>
        internal void Line(string text, int offset, int depth)
        {
            int start = offset + depth * 4;
            if (text == "{")
            {
                if (_pendingOwner != null)
                {
                    _owners.Push((depth, _pendingOwner));
                    _pendingOwner = null;
                    _attributeStart = -1;
                }
                else if (_method != null && _method.BodyStart < 0)
                {
                    _method.SignatureEnd = start;
                    _method.BodyStart = start + 1;
                    _method.Depth = depth;
                }
                return;
            }
            if (text.StartsWith("}", StringComparison.Ordinal))
            {
                if (_method != null && _method.BodyStart >= 0 && _method.Depth == depth)
                {
                    _method.BodyEnd = start;
                    _method.End = start + text.Length;
                    _entries.Add(_method);
                    _method = null;
                }
                if (_owners.Count > 0 && _owners.Peek().Depth == depth)
                    _owners.Pop();
                _attributeStart = -1;
                return;
            }
            if (_method?.BodyStart >= 0)
                return;
            int declaration = text.IndexOf(" class ", StringComparison.Ordinal);
            if (declaration < 0)
                declaration = text.IndexOf(" struct ", StringComparison.Ordinal);
            if (declaration < 0)
                declaration = text.IndexOf(" interface ", StringComparison.Ordinal);
            // A constraint clause such as "where T : struct where U : ..." names a kind, not a declaration.
            if (declaration >= 0 && text.IndexOf('(') < 0 && !text.StartsWith("where ", StringComparison.Ordinal))
            {
                string tail = text.Substring(text.IndexOf(' ', declaration + 1) + 1);
                int end = 0;
                while (end < tail.Length && (char.IsLetterOrDigit(tail[end]) || tail[end] == '_'))
                    end++;
                _pendingOwner = tail.Substring(0, end);
                return;
            }
            if (_method == null && (text.StartsWith("[", StringComparison.Ordinal) || text.StartsWith("///", StringComparison.Ordinal)))
            {
                if (_attributeStart < 0)
                    _attributeStart = offset;
                return;
            }
            if (_method == null && _owners.Count > 0 && text.Contains(" static ") && text.IndexOf('(') >= 0 &&
                (text.IndexOf('=') < 0 || text.IndexOf('=') > text.IndexOf('(')))
            {
                _method = new Entry { Start = _attributeStart < 0 ? offset : _attributeStart, SignatureStart = offset, Owner = _owners.Peek().Name };
                _attributeStart = -1;
            }
            if (_method == null)
            {
                _attributeStart = -1;
                return;
            }
            int arrow = text.IndexOf("=>", StringComparison.Ordinal);
            if (arrow >= 0 && text.TrimEnd().EndsWith(";", StringComparison.Ordinal))
            {
                _method.SignatureEnd = start + arrow;
                _method.BodyStart = start + arrow + 2;
                _method.BodyEnd = start + text.LastIndexOf(';');
                _method.End = start + text.Length;
                _method.Expression = true;
                _entries.Add(_method);
                _method = null;
            }
        }

        /// <summary>Publishes declaration metadata for the exact completed string instance.</summary>
        /// <param name="header">The file-header character count.</param>
        /// <param name="source">The completed source text.</param>
        internal void Complete(string source, int header)
        {
            var methods = new List<Method>(_entries.Count);
            foreach (Entry entry in _entries)
            {
                string signature = Trimmed(source, header + entry.SignatureStart, entry.SignatureEnd - entry.SignatureStart, leading: false);
                int parametersOpen = signature.IndexOf('(');
                if (parametersOpen < 0)
                    continue;
                int nameEnd = parametersOpen;
                while (nameEnd > 0 && char.IsWhiteSpace(signature[nameEnd - 1]))
                    nameEnd--;
                string generic = string.Empty;
                if (nameEnd > 0 && signature[nameEnd - 1] == '>')
                {
                    int depth = 1, open = nameEnd - 2;
                    while (open >= 0 && depth != 0)
                    {
                        if (signature[open] == '>')
                            depth++;
                        else if (signature[open] == '<')
                            depth--;
                        if (depth != 0)
                            open--;
                    }
                    if (open < 0)
                        continue;
                    generic = signature.Substring(open + 1, nameEnd - open - 2);
                    nameEnd = open;
                    while (nameEnd > 0 && char.IsWhiteSpace(signature[nameEnd - 1]))
                        nameEnd--;
                }
                int nameStart = nameEnd;
                while (nameStart > 0 && (char.IsLetterOrDigit(signature[nameStart - 1]) || signature[nameStart - 1] is '_' or '@'))
                    nameStart--;
                if (nameEnd == nameStart)
                    continue;
                string name = signature.Substring(nameStart, nameEnd - nameStart);
                int position = parametersOpen;
                int parameters = signature.IndexOf('(', position);
                int parametersEnd = Close(signature, parameters, '(', ')');
                if (parametersEnd < 0)
                    continue;
                int bodyLength = entry.BodyEnd - entry.BodyStart;
                int firstSemicolon = source.IndexOf(';', header + entry.BodyStart, bodyLength);
                bool singleStatement = firstSemicolon < 0 || source.IndexOf(';', firstSemicolon + 1, header + entry.BodyEnd - firstSemicolon - 1) < 0;
                string body = entry.Expression || singleStatement || name is "RootWrite" or "RootRead" or "RootWriteUnion" or "RootReadUnion"
                    ? source.Substring(header + entry.BodyStart, bodyLength) : string.Empty;
                if (entry.Expression)
                    body = body.Trim();
                methods.Add(new Method(header + entry.Start, header + entry.End, header + entry.SignatureStart, entry.Owner,
                    name, generic, signature.Substring(parameters + 1, parametersEnd - parameters - 1),
                    signature, body, entry.Expression, signature.Substring(0, LeadingWhitespace(signature))));
            }
            Captured.Add(source, methods);
        }

        private sealed class Entry
        {
            internal int Start, End, SignatureStart, SignatureEnd, BodyStart = -1, BodyEnd, Depth;
            internal string Owner = string.Empty;
            internal bool Expression;
        }
    }

    /// <summary>Reads a complete generated invocation expression.</summary>
    /// <param name="text">The original generated text.</param>
    /// <returns>The invocation contract, or null for a different expression.</returns>
    internal static Call? ReadCall(string text)
    {
        string source = text.Trim().TrimEnd(';').Trim();
        int position = 0;
        while (true)
        {
            while (position < source.Length && (char.IsLetterOrDigit(source[position]) || source[position] is '_' or '@' or '.' or ':'))
                position++;
            // A closed generic owner, such as Owner<int>.Write, continues the target after its type arguments.
            if (position == 0 || position >= source.Length || source[position] != '<')
                break;
            int closeType = Close(source, position, '<', '>');
            if (closeType < 0 || closeType + 1 >= source.Length || source[closeType + 1] != '.')
                break;
            position = closeType + 2;
        }
        if (position == 0)
            return null;
        string target = source.Substring(0, position);
        while (position < source.Length && char.IsWhiteSpace(source[position]))
            position++;
        string generic = string.Empty;
        if (position < source.Length && source[position] == '<')
        {
            int close = Close(source, position, '<', '>');
            if (close < 0)
                return null;
            generic = source.Substring(position + 1, close - position - 1);
            position = close + 1;
        }
        while (position < source.Length && char.IsWhiteSpace(source[position]))
            position++;
        if (position == source.Length || source[position] != '(')
            return null;
        int end = Close(source, position, '(', ')');
        if (end != source.Length - 1)
            return null;
        return new Call(target, Split(generic), Split(source.Substring(position + 1, end - position - 1)));
    }

    /// <summary>A source-writer method boundary and its explicit operation contract.</summary>
    internal sealed class Method
    {
        internal readonly int Start, End, SignatureStart;
        internal readonly string Owner, Name, Signature, Body, Indent;
        internal readonly string[] Generics, Parameters;
        internal readonly bool Expression, Json;
        internal Call? Forward;

        /// <summary>Captures a generated method without syntax trees or symbols.</summary>
        /// <param name="body">The operation body or forwarding expression.</param>
        /// <param name="end">The exclusive character offset.</param>
        /// <param name="expression">Whether the body is an expression.</param>
        /// <param name="generic">The generic parameter text.</param>
        /// <param name="indent">The declaration indentation.</param>
        /// <param name="name">The unqualified method name.</param>
        /// <param name="owner">The declaring static container.</param>
        /// <param name="parameters">The method parameter text.</param>
        /// <param name="signature">The unchanged method signature.</param>
        /// <param name="signatureStart">The start of the method signature.</param>
        /// <param name="start">The inclusive character offset.</param>
        internal Method(int start, int end, int signatureStart, string owner, string name, string generic, string parameters,
            string signature, string body, bool expression, string indent)
        {
            Start = start;
            End = end;
            SignatureStart = signatureStart;
            Owner = owner;
            Name = name;
            Signature = signature;
            Body = body;
            Expression = expression;
            Indent = indent;
            Generics = Split(generic);
            Parameters = Split(parameters);
            // Only the operation prefix names the format; a payload name after the separator may itself contain "Json".
            int separator = name.IndexOf('_');
            Json = name.IndexOf("Json", 0, separator < 0 ? name.Length : separator, StringComparison.Ordinal) >= 0 ||
                (Parameters.Length != 0 && Parameters[0].Contains("LuminPackJson"));
            if (signature.Contains(" void ") && (expression || body.IndexOf(';') == body.LastIndexOf(';')))
                Forward = ReadCall(body);
        }
    }

    /// <summary>The explicit static target and arguments of an invocation.</summary>
    internal sealed class Call
    {
        internal readonly string Target;
        internal readonly string[] Generics, Arguments;

        /// <summary>Captures an invocation selected by the emitter.</summary>
        /// <param name="arguments">The explicit call arguments.</param>
        /// <param name="generics">The explicit generic arguments.</param>
        /// <param name="target">The qualified static call target.</param>
        internal Call(string target, string[] generics, string[] arguments)
        {
            Target = target;
            Generics = generics;
            Arguments = arguments;
        }
    }
}
