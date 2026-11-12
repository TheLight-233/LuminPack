using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Reads caller API signatures without parsing or allocating syntax for execution bodies.</summary>
internal static class DocumentationMethods
{
    private static readonly Regex Classes = new(@"(?m)^[ ]*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly)[ ]+)*(?:class|struct|interface)[ ]+", RegexOptions.Compiled);
    private static readonly Regex Starts = new(@"(?m)^(?<indent>[ ]*)(?<access>public|internal|private|protected)[ ]+static[ ]+", RegexOptions.Compiled);

    /// <summary>Reads caller methods on the serializer facade and its concrete convenience-entry containers.</summary>
    /// <param name="source">The final generated source.</param>
    /// <returns>The caller-facing signatures in source order.</returns>
    internal static List<Method> Read(string source)
    {
        var classes = new List<(int Start, int End, string Name)>();
        foreach (Match match in Classes.Matches(source))
        {
            int nameStart = match.Index + match.Length;
            int nameEnd = nameStart;
            while (nameEnd < source.Length && (char.IsLetterOrDigit(source[nameEnd]) || source[nameEnd] is '_' or '@'))
                nameEnd++;
            int open = source.IndexOf('{', nameEnd);
            int end = open < 0 ? -1 : GeneratedText.Close(source, open, '{', '}');
            if (end >= 0)
                classes.Add((match.Index, end, source.Substring(nameStart, nameEnd - nameStart)));
        }
        var methods = new List<Method>();
        foreach (Match match in Starts.Matches(source))
        {
            string? owner = null;
            int ownerIndex = -1;
            for (int index = classes.Count - 1; index >= 0; index--)
                if (classes[index].Start < match.Index && match.Index < classes[index].End)
                {
                    owner = classes[index].Name;
                    ownerIndex = index;
                    break;
                }
            bool entry = false;
            if (owner?.StartsWith("LuminEntry_", StringComparison.Ordinal) == true)
                for (int index = ownerIndex - 1; index >= 0; index--)
                    if (classes[index].Start < classes[ownerIndex].Start && classes[ownerIndex].Start < classes[index].End)
                    {
                        entry = classes[index].Name == "LuminPackSerializer";
                        break;
                    }
            if (owner != "LuminPackSerializer" && !entry)
                continue;
            int position = match.Index + match.Length;
            SkipModifier(source, ref position, "async");
            SkipModifier(source, ref position, "unsafe");
            int returnStart = position;
            SkipModifier(source, ref position, "ref");
            SkipModifier(source, ref position, "readonly");
            while (position < source.Length && !char.IsWhiteSpace(source[position]))
            {
                if (source[position] == '<')
                {
                    int end = GeneratedText.Close(source, position, '<', '>');
                    if (end < 0)
                        break;
                    position = end;
                }
                position++;
            }
            string returns = source.Substring(returnStart, position - returnStart);
            SkipWhitespace(source, ref position);
            int nameStart = position;
            while (position < source.Length && (char.IsLetterOrDigit(source[position]) || source[position] is '_' or '@'))
                position++;
            string name = source.Substring(nameStart, position - nameStart);
            if (!(name.StartsWith("Serialize", StringComparison.Ordinal) || name.StartsWith("Deserialize", StringComparison.Ordinal) ||
                name is "Register" or "Compress" or "Decompress"))
                continue;
            string[] generics = Array.Empty<string>();
            SkipWhitespace(source, ref position);
            if (position < source.Length && source[position] == '<')
            {
                int close = GeneratedText.Close(source, position, '<', '>');
                if (close < 0)
                    continue;
                generics = GeneratedText.Split(source.Substring(position + 1, close - position - 1));
                position = close + 1;
            }
            SkipWhitespace(source, ref position);
            if (position >= source.Length || source[position] != '(')
                continue;
            int parametersEnd = GeneratedText.Close(source, position, '(', ')');
            if (parametersEnd < 0)
                continue;
            string[] parameters = GeneratedText.Split(source.Substring(position + 1, parametersEnd - position - 1));
            if (match.Groups["access"].Value != "public" && parameters.Length != 0 && IsState(parameters[0]))
                continue;
            int body = parametersEnd + 1;
            while (body < source.Length && source[body] != '{' && source[body] != '=')
                body++;
            if (body == source.Length)
                continue;
            int endBody = source[body] == '{' ? GeneratedText.Close(source, body, '{', '}') : source.IndexOf(';', body);
            if (endBody < 0)
                continue;
            int start = match.Index;
            while (start > 0)
            {
                int previousEnd = start - 1;
                while (previousEnd >= 0 && source[previousEnd] is (char)10 or (char)13)
                    previousEnd--;
                int previousStart = source.LastIndexOf((char)10, Math.Max(0, previousEnd)) + 1;
                string line = source.Substring(previousStart, Math.Max(0, previousEnd - previousStart + 1)).TrimStart();
                if (!line.StartsWith("[", StringComparison.Ordinal))
                    break;
                start = previousStart;
            }
            methods.Add(new Method(start, body, endBody, match.Groups["indent"].Value, name, returns, generics, parameters));
        }
        return methods;
    }

    /// <summary>Removes passing modifiers and the parameter name from an emitted declaration.</summary>
    /// <param name="parameter">The complete generated parameter.</param>
    /// <returns>The declared type spelling.</returns>
    internal static string ParameterType(string parameter)
    {
        int position = 0;
        foreach (string modifier in ParameterModifiers)
            SkipModifier(parameter, ref position, modifier);
        string name = GeneratedText.ParameterName(parameter);
        int end = parameter.LastIndexOf(name, StringComparison.Ordinal);
        return parameter.Substring(position, end - position).Trim();
    }

    private static readonly string[] ParameterModifiers = { "this", "scoped", "ref", "readonly", "in", "out" };

    /// <summary>Recognizes the original state-passing implementation boundary.</summary>
    /// <param name="parameter">The first emitted parameter.</param>
    /// <returns>Whether it is a binary, JSON or evaluator state.</returns>
    internal static bool IsState(string parameter)
    {
        string type = ParameterType(parameter);
        return type.EndsWith("LuminPackWriter", StringComparison.Ordinal) || type.EndsWith("LuminPackReader", StringComparison.Ordinal) ||
            type.EndsWith("LuminPackJsonWriter", StringComparison.Ordinal) || type.EndsWith("LuminPackJsonReader", StringComparison.Ordinal) ||
            type.EndsWith("LuminPackEvaluator", StringComparison.Ordinal);
    }

    /// <summary>Advances past a complete generated modifier token.</summary>
    /// <param name="source">The signature.</param>
    /// <param name="position">The next token offset.</param>
    /// <param name="modifier">The expected modifier.</param>
    private static void SkipModifier(string source, ref int position, string modifier)
    {
        if (position + modifier.Length < source.Length && string.CompareOrdinal(source, position, modifier, 0, modifier.Length) == 0 &&
            char.IsWhiteSpace(source[position + modifier.Length]))
        {
            position += modifier.Length;
            SkipWhitespace(source, ref position);
        }
    }

    /// <summary>Advances over generated signature whitespace.</summary>
    /// <param name="source">The signature.</param>
    /// <param name="position">The next token offset.</param>
    private static void SkipWhitespace(string source, ref int position)
    {
        while (position < source.Length && char.IsWhiteSpace(source[position]))
            position++;
    }

    /// <summary>The signature and body range needed to document one caller-facing method.</summary>
    internal sealed class Method
    {
        internal readonly int Start, Body, End;
        internal readonly string Indent, Name, Returns;
        internal readonly string[] Generics, Parameters;

        /// <summary>Stores source ranges and normalized signature components.</summary>
        /// <param name="start">The first attribute or declaration line.</param>
        /// <param name="body">The block or expression body start.</param>
        /// <param name="end">The closing brace or semicolon.</param>
        /// <param name="indent">The original declaration indentation.</param>
        /// <param name="name">The method name.</param>
        /// <param name="returns">The return type.</param>
        /// <param name="generics">The generic parameter names.</param>
        /// <param name="parameters">The parameter declarations.</param>
        internal Method(int start, int body, int end, string indent, string name, string returns, string[] generics, string[] parameters)
        {
            Start = start;
            Body = body;
            End = end;
            Indent = indent;
            Name = name;
            Returns = returns;
            Generics = generics;
            Parameters = parameters;
        }
    }
}
