using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Rendering.Facade;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Completes caller-facing XML with text edits that leave executable tokens unchanged.</summary>
internal static partial class SerializerDocumentation
{
    private static readonly Dictionary<string, PayloadDocumentation> Empty = new(StringComparer.Ordinal);
    internal static readonly string BinaryFacade = SourcePresentation.Apply(Facade(SerializerFacadeSource.Binary, typeSummary: true));
    internal static readonly string JsonFacade = SourcePresentation.Apply(Facade(SerializerFacadeSource.Json, typeSummary: false));

    /// <summary>Documents the fixed facade once without retaining compiler state.</summary>
    /// <param name="source">The post-initialization facade.</param>
    /// <param name="typeSummary">Whether this contribution owns the facade's type summary.</param>
    /// <returns>The complete caller-facing documentation.</returns>
    internal static string Facade(string source, bool typeSummary) => Complete(source, Empty, typeSummary, out _);

    /// <summary>Documents one final generated file and captures its exact payload dependencies.</summary>
    /// <param name="source">The source after operation binding and container renaming.</param>
    /// <param name="catalog">The immutable payload documentation facts.</param>
    /// <param name="typeSummary">Whether to document the facade type in this contribution.</param>
    /// <param name="dependencies">Receives only the payload contracts used by this file.</param>
    /// <returns>The source with documentation restricted to caller-facing APIs.</returns>
    private static string Complete(string source, IReadOnlyDictionary<string, PayloadDocumentation> catalog, bool typeSummary,
        out KeyValuePair<string, PayloadDocumentation?>[] dependencies)
    {
        var edits = new List<(int Start, int End, string Text)>();
        for (int start = 0; start < source.Length;)
        {
            int end = source.IndexOf((char)10, start);
            end = end < 0 ? source.Length : end + 1;
            int content = start;
            while (content < end && source[content] is ' ' or (char)9)
                content++;
            if (content + 2 < end && source[content] == '/' && source[content + 1] == '/' && source[content + 2] == '/')
                edits.Add((start, end, string.Empty));
            start = end;
        }
        var used = new Dictionary<string, PayloadDocumentation?>(StringComparer.Ordinal);
        foreach (DocumentationMethods.Method method in DocumentationMethods.Read(source))
        {
            string? payload = PayloadType(method);
            PayloadDocumentation? contract = null;
            if (payload != null)
            {
                string key = Key(payload);
                bool found = catalog.TryGetValue(key, out contract);
                used[key] = contract;
                if (!found)
                {
                    key = PublicationNames.Mangle(payload);
                    catalog.TryGetValue(key, out contract);
                    used[key] = contract;
                }
            }
            using var xml = new PooledTextBuffer(2048);
            Method(xml, source, method, payload, contract);
            edits.Add((method.Start, method.Start, xml.ToString()));
        }
        if (typeSummary)
        {
            int type = source.IndexOf("public static partial class LuminPackSerializer", StringComparison.Ordinal);
            if (type >= 0)
            {
                int start = source.LastIndexOf((char)10, type) + 1;
                string indent = source.Substring(start, type - start);
                edits.Add((start, start, indent + "/// <summary>Provides binary and JSON serialization with compiled profiles and typed formatter registration.</summary>" + (char)10));
            }
        }
        dependencies = new KeyValuePair<string, PayloadDocumentation?>[used.Count];
        int dependency = 0;
        foreach (var item in used)
            dependencies[dependency++] = item;
        if (edits.Count == 0)
            return source;
        edits.Sort(static (left, right) => left.Start != right.Start ? left.Start.CompareTo(right.Start) : left.End.CompareTo(right.End));
        using var output = new PooledTextBuffer(source.Length + edits.Count * 128);
        int copied = 0;
        foreach (var edit in edits)
        {
            if (edit.Start > copied)
                output.Append(source, copied, edit.Start - copied);
            output.Append(edit.Text);
            copied = Math.Max(copied, edit.End);
        }
        output.Append(source, copied, source.Length - copied);
        return output.ToString();
    }

    /// <summary>Writes a complete overload contract before its original attributes.</summary>
    /// <param name="xml">The transient documentation buffer.</param>
    /// <param name="source">The unchanged source containing the method body.</param>
    /// <param name="method">The caller's signature.</param>
    /// <param name="payload">The payload type spelling, when available.</param>
    /// <param name="contract">The exact payload declaration facts, when available.</param>
    private static void Method(PooledTextBuffer xml, string source, DocumentationMethods.Method method, string? payload, PayloadDocumentation? contract)
    {
        bool read = method.Name.StartsWith("Deserialize", StringComparison.Ordinal);
        bool json = method.Name.Contains("Json");
        bool genericPayload = false;
        foreach (string generic in method.Generics)
            genericPayload |= payload == generic;
        string operation = method.Name == "Register" ? "Registers typed formatter delegates." :
            method.Name == "Compress" ? "Compresses the supplied binary payload." :
            method.Name == "Decompress" ? "Decompresses the supplied binary payload." :
            (read ? "Reads a payload" : "Writes a payload") + (json ? " using JSON." : " using the binary format.");
        Line(xml, method.Indent, "<summary>" + operation + "</summary>");
        foreach (string generic in method.Generics)
        {
            string description = generic is "TOptions" or "TLuminProfile" ? "The stateless compiled serialization profile." :
                generic == "TUnion" ? "The Union root type." : generic == "TMember" ? "The registered concrete Union case." :
                "The payload type or a type argument of its declared contract.";
            Element(xml, method.Indent, "typeparam", generic, description);
        }
        bool reused = false;
        bool buffer = false;
        bool arbitrary = false;
        bool stream = false;
        foreach (string parameter in method.Parameters)
        {
            string name = GeneratedText.ParameterName(parameter).TrimStart('@');
            string type = DocumentationMethods.ParameterType(parameter);
            bool byRef = parameter.StartsWith("ref ", StringComparison.Ordinal) || parameter.StartsWith("scoped ref ", StringComparison.Ordinal);
            reused |= name == "value" && byRef;
            buffer |= type.EndsWith("LuminBufferWriter", StringComparison.Ordinal);
            arbitrary |= type.Contains("IBufferWriter<");
            stream |= type.EndsWith("Stream", StringComparison.Ordinal);
            string description = name switch
            {
                "value" or "input" => read && byRef ? "The destination storage; existing values follow the formatter's reuse rules." : "The value processed by this operation.",
                "state" or "reader" or "writer" => "The caller-owned operation state.",
                "buffer" or "source" => "The encoded input, which must remain stable throughout the read.",
                "writerBuffer" or "bufferWriter" => "The caller-owned input or output buffer.",
                "destination" => "The caller-owned destination storage.",
                "stream" => "The caller-owned stream, which remains open.",
                "cancellationToken" => "The cancellation token for the asynchronous operation.",
                "tag" => "The Union wire tag, excluding generated cases.",
                "writeValue" => "The typed binary writer delegate.",
                "readValue" => "The typed binary reader delegate.",
                "writeValueJson" => "The typed JSON writer delegate.",
                "readValueJson" => "The typed JSON reader delegate.",
                _ => "The argument supplied to this generated operation.",
            };
            Element(xml, method.Indent, "param", name, description);
        }
        if (method.Returns != "void")
            Line(xml, method.Indent, "<returns>" + (method.Returns == "int" ?
                read ? "The number of consumed bytes in the selected input representation." : "The number of produced bytes." :
                method.Returns.Contains("ValueTask") ? "The asynchronous operation and its decoded value when one is returned." :
                read ? "The decoded payload." : method.Returns == "string" ? "The complete JSON document." : "A new array containing the complete encoded payload.") + "</returns>");
        Line(xml, method.Indent, "<remarks>");
        if (contract != null)
            Contract(xml, method.Indent, contract);
        else if (payload != null && !genericPayload)
            Line(xml, method.Indent, "<para>Uses the formatter for this exact closed type; CLR layout alone does not define a built-in formatter's representation.</para>");
        else if (method.Name.StartsWith("Serialize", StringComparison.Ordinal) || read)
            Line(xml, method.Indent, "<para>Statically bound calls use generated concrete entries. Calls that cannot bind statically require explicit or Custom registration for the requested operation; missing registrations throw LuminPackException. Default fallback document boundaries use assembly options; explicit profiles select their compiled document policy.</para>");
        if (buffer)
            Line(xml, method.Indent, read ?
                "<para>The caller exclusively owns the reusable buffer and its reference state for this operation.</para>" :
                "<para>Appends at the caller-owned buffer's cursor without resetting payload or reference state. Call ResetForReuse before an independent session or after failure; partial output and reference state can remain after an exception.</para>");
        if (arbitrary)
            Line(xml, method.Indent, "<para>Advances the supplied IBufferWriter by the produced byte count. The destination remains caller-owned.</para>");
        if (reused)
            Line(xml, method.Indent, "<para>Populates the supplied value by reference. Reuse is not transactional; completed updates can remain after a failed read.</para>");
        if (stream)
            Line(xml, method.Indent, "<para>Uses the caller-owned stream asynchronously and observes cancellationToken without disposing the stream.</para>");
        if (method.Parameters.Length != 0 && DocumentationMethods.IsState(method.Parameters[0]))
            Line(xml, method.Indent, "<para>Uses the supplied state directly, preserving its cursor and reference identities. The caller owns initialization and completion.</para>");
        string? navigation = contract == null ? null : "global::LuminPack.LuminPackSerializer." +
            ReadableTypeName.Of(GeneratedNames.PayloadKey(contract.Model.Identity.GlobalName)) + "Formatter";
        if (navigation == null)
        foreach (Match call in GeneratedText.Calls.Matches(source, method.Body))
        {
            if (call.Index >= method.End)
                break;
            string target = call.Groups["target"].Value;
            if (!target.Contains("Formatter."))
                continue;
            navigation = target.Substring(0, target.LastIndexOf('.'));
            break;
        }
        if (navigation != null)
        {
            xml.Append(method.Indent).Append("/// <para>Generated implementation: <see cref='");
            Escape(xml, navigation);
            xml.Append("'/>.</para>").Append((char)10);
        }
        Line(xml, method.Indent, "</remarks>");
        if (contract != null && contract.TypeId.Length != 0)
        {
            xml.Append(method.Indent).Append("/// <seealso cref='");
            Escape(xml, contract.TypeId);
            xml.Append("'/>").Append((char)10);
        }
    }

    /// <summary>Reads the payload from a value parameter or a returning read overload.</summary>
    /// <param name="method">The caller-facing signature.</param>
    /// <returns>The payload spelling, or null for registration and utility APIs.</returns>
    private static string? PayloadType(DocumentationMethods.Method method)
    {
        if (!(method.Name.StartsWith("Serialize", StringComparison.Ordinal) || method.Name.StartsWith("Deserialize", StringComparison.Ordinal)))
            return null;
        foreach (string parameter in method.Parameters)
            if (GeneratedText.ParameterName(parameter) is "value" or "input")
                return DocumentationMethods.ParameterType(parameter);
        string result = method.Returns;
        int generic = result.IndexOf('<');
        return generic >= 0 && result.Contains("ValueTask") ? result.Substring(generic + 1, result.Length - generic - 2) : result;
    }

    /// <summary>Normalizes a generated type spelling for exact documentation lookup.</summary>
    /// <param name="type">The emitted type spelling.</param>
    /// <returns>The value-model lookup key.</returns>
    internal static string Key(string type) => GeneratedNames.PayloadKey(type);

    /// <summary>Writes one documentation line with the declaration's original indentation.</summary>
    /// <param name="xml">The transient output.</param>
    /// <param name="indent">The declaration indentation.</param>
    /// <param name="text">The complete XML fragment.</param>
    private static void Line(PooledTextBuffer xml, string indent, string text) => xml.Append(indent).Append("/// ").Append(text).Append((char)10);

    /// <summary>Writes a named parameter element with escaped identifiers.</summary>
    /// <param name="xml">The transient output.</param>
    /// <param name="indent">The declaration indentation.</param>
    /// <param name="element">The param or typeparam element.</param>
    /// <param name="name">The declared identifier.</param>
    /// <param name="description">The usage description.</param>
    private static void Element(PooledTextBuffer xml, string indent, string element, string name, string description)
    {
        xml.Append(indent).Append("/// <").Append(element).Append(" name='");
        Escape(xml, name);
        xml.Append("'>").Append(description).Append("</").Append(element).Append('>').Append((char)10);
    }

    /// <summary>Escapes XML text and attributes directly into the final buffer.</summary>
    /// <param name="xml">The transient output.</param>
    /// <param name="text">The compiler-free content.</param>
    private static void Escape(PooledTextBuffer xml, string text)
    {
        foreach (char character in text)
            switch (character)
            {
                case '&':
                    xml.Append("&amp;");
                    break;
                case '<':
                    xml.Append("&lt;");
                    break;
                case '>':
                    xml.Append("&gt;");
                    break;
                case (char)39:
                    xml.Append("&apos;");
                    break;
                case (char)34:
                    xml.Append("&quot;");
                    break;
                default:
                    xml.Append(character);
                    break;
            }
    }
}
