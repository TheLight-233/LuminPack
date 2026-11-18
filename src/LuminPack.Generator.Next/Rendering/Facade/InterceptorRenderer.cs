using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Facade;

/// <summary>A concrete root published by Next that ordinary facade calls may bind to.</summary>
internal sealed class RootEntry
{
    /// <summary>The root identifier used by <c>Serialize_</c> and <c>Deserialize_</c> members.</summary>
    internal readonly string Readable;

    /// <summary>The profile substituted for calls that use the declared profile.</summary>
    internal readonly string DeclaredProfile;

    /// <summary>Whether binary roots are published.</summary>
    internal readonly bool Binary;

    /// <summary>Whether JSON roots are published.</summary>
    internal readonly bool Json;

    /// <summary>The shared owner whose static <c>Write</c> and <c>Read</c> operations the entries call, or null for a payload's own
    /// <c>Serialize_</c> and <c>Deserialize_</c> roots.</summary>
    internal readonly string? Owner;

    /// <summary>The profile substituted for calls without a profile when the payload declares none.</summary>
    internal const string DefaultProfile = "global::LuminPack.Option.LuminPackUtf16LengthOptions";

    /// <summary>The payload's own frozen input cap; null for a trusted payload, whose entries check no input size.</summary>
    /// <remarks>Shared owners keep the assembly cap through the generic dispatch selector.</remarks>
    internal readonly int? Limit;

    /// <summary>The strict profile that replaces the declared profile in JSON reads of an explicitly untrusted payload, or null.</summary>
    internal readonly string? JsonReadProfile;

    /// <summary>Whether the entry is a generic Union definition, whose closed uses always bind its generic roots.</summary>
    internal readonly bool GenericUnion;

    /// <summary>The root method stem, which differs from the entry name for a closed generic payload.</summary>
    internal readonly string RootName;

    /// <summary>The closed type arguments the generic roots take before the profile, each followed by ", ".</summary>
    internal readonly string RootArguments;

    /// <summary>Whether the payload's own roots publish its exact record measure.</summary>
    internal readonly bool Measured;

    /// <summary>Describes a published root.</summary>
    /// <param name="readable">The payload's readable name.</param>
    /// <param name="declaredProfile">The effective declaration profile.</param>
    /// <param name="binary">Whether binary roots are published.</param>
    /// <param name="json">Whether JSON roots are published.</param>
    /// <param name="owner">The shared formatter state owner, if any.</param>
    /// <param name="limit">The payload's frozen input cap.</param>
    /// <param name="jsonReadProfile">The payload's strict JSON profile, if any.</param>
    /// <param name="rootName">The named root's method suffix.</param>
    /// <param name="rootArguments">The root's closed type arguments before the profile.</param>
    /// <param name="genericUnion">Whether the entry describes an open Union definition.</param>
    /// <param name="measured">Whether the root has an exact record measurement.</param>
    internal RootEntry(string readable, string declaredProfile, bool binary, bool json, string? owner = null, int? limit = null,
        string? jsonReadProfile = null, string? rootName = null, string rootArguments = "", bool genericUnion = false, bool measured = false)
    {
        Measured = measured;
        GenericUnion = genericUnion;
        RootName = rootName ?? readable;
        RootArguments = rootArguments;
        Limit = limit;
        JsonReadProfile = jsonReadProfile;
        Owner = owner;
        Readable = readable;
        DeclaredProfile = declaredProfile;
        Binary = binary;
        Json = json;
    }

}

/// <summary>Binds each observed facade signature directly to its published static root.</summary>
internal static class InterceptorRenderer
{
    /// <summary>Renders only the intercepted signatures and their required document boundaries.</summary>
    /// <param name="calls">The bound facade calls.</param>
    /// <param name="roots">The published roots keyed by payload spelling.</param>
    /// <param name="il">Whether the installed IL postprocessor binds the calls.</param>
    /// <returns>The interceptor source, or an empty string when no root binds.</returns>
    internal static string Render(IReadOnlyList<SerializerCallModel> calls, RootCatalog roots, bool il)
    {
        var writer = new StringBuilder();
        var documentBodies = new Dictionary<(SerializerApi Api, EntryShape Shape, bool Measured), string>();
        int index = 0;
        foreach (var group in calls.Where(call => call.Location.Length != 0).GroupBy(call => (call.Api, call.Payload, call.OpenProfile))
            .OrderBy(group => group.Key.Payload, StringComparer.Ordinal).ThenBy(group => group.Key.Api).ThenBy(group => group.Key.OpenProfile))
        {
            SerializerCallModel call = group.First();
            if (roots.Find(call) is not { } root || (call.Json ? !root.Json : !root.Binary))
                continue;
            if (index == 0)
                writer.Append(Header).Append(il ? StaticCall : InterceptsLocation).Append(FacadeOpen);
            foreach (string location in group.Select(item => item.Location).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
                writer.Append("        ").Append(location).Append('\n');
            string profile = call.OpenProfile ? "TLuminProfile" :
                call.Json && !call.Writes ? root.JsonReadProfile ?? root.DeclaredProfile : root.DeclaredProfile;
            Interceptor(writer, call, root, profile, index++, root.Owner == null ? root.Measured : roots.Measured.Contains(root.RootName), documentBodies);
        }
        return index == 0 ? string.Empty : writer.Append("    }\n}\n").ToString();
    }

    private const string Header = @"// <auto-generated/>
#nullable enable annotations
#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649, CS1591, CS9113
using global::System;
using global::System.Buffers;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using global::System.Text;
using global::LuminPack.Core;
using global::LuminPack.Code;
using global::LuminPack.Option;
using global::LuminPack.Utility;
using LuminPackSerializer = global::LuminPack.LuminPackSerializer;
using LuminPackInputPolicy = global::LuminPack.Security.LuminPackInputPolicy;
using LuminPackExceptionHelper = global::LuminPack.Code.LuminPackExceptionHelper;
using ILuminPackOptions = global::LuminPack.Option.ILuminPackOptions;

";

    /// <summary>The compiler interception attribute, declared file-local as the compiler requires.</summary>
    private const string InterceptsLocation = @"namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
    file sealed class InterceptsLocationAttribute : global::System.Attribute
    {
        public InterceptsLocationAttribute(int version, string data) { }
    }
}

";

    /// <summary>The build-only mapping attribute the installed IL rewriter consumes and removes.</summary>
    private const string StaticCall = @"namespace LuminPack.Generated
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class StaticCallAttribute : global::System.Attribute
    {
        public StaticCallAttribute(string key, int profileArgument) { }
    }
}

";

    private const string FacadeOpen = @"namespace LuminPack
{
    public static partial class LuminPackSerializer
    {
";

    /// <summary>Applies the root's input boundary: a frozen cap, the shared assembly selector, or no check for a trusted payload.</summary>
    /// <param name="text">The entry template.</param>
    /// <param name="type">The payload spelling.</param>
    /// <param name="root">The published root.</param>
    /// <returns>The template with its input checks resolved.</returns>
    private static string Limits(string text, string type, RootEntry root)
    {
        // A stream read stages at most the frozen cap; a trusted payload keeps the assembly selector there, as the original
        // concrete facade only replaced that argument under an input policy.
        text = text.Replace("%STREAMLIMIT%", root.Owner == null && root.Limit is { } streamLimit
            ? streamLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "LuminPackSerializer.FormatterDispatch.LuminPackInputLimit<" + type + ">()");
        if (root.Owner != null)
            return text.Replace("%LIMIT%", "LuminPackSerializer.FormatterDispatch.LuminPackInputLimit<" + type + ">()");
        if (root.Limit is not { } limit)
            return string.Join("\n", text.Split('\n').Where(line => line.IndexOf("%LIMIT%", StringComparison.Ordinal) < 0));
        return text.Replace("%LIMIT%", limit.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Writes an interceptor with the facade signature and its root invocation.</summary>
    /// <param name="w">The interceptor source.</param>
    /// <param name="bound">The bound facade signature and payload facts.</param>
    /// <param name="root">The published root.</param>
    /// <param name="profile">The concrete or forwarded profile.</param>
    /// <param name="index">The unique interceptor index.</param>
    /// <param name="measured">Whether the root has an exact record measurement.</param>
    /// <param name="documentBodies">The document templates already assembled for this publication.</param>
    private static void Interceptor(StringBuilder w, SerializerCallModel bound, RootEntry root, string profile, int index, bool measured,
        Dictionary<(SerializerApi Api, EntryShape Shape, bool Measured), string> documentBodies)
    {
        SerializerApi api = bound.Api;
        string type = bound.Payload;
        string name = (bound.Json ? "LuminInterceptJson_" : "LuminIntercept_") + root.Readable + "_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            (bound.OpenProfile ? "<T, TLuminProfile>" : string.Empty);
        string signature;
        switch (api)
        {
            case SerializerApi.SerializeStream:
                signature = "global::System.Threading.Tasks.ValueTask " + name + "(global::System.IO.Stream stream, " + type +
                    " value, global::System.Threading.CancellationToken cancellationToken)";
                break;
            case SerializerApi.DeserializeStream:
                signature = "global::System.Threading.Tasks.ValueTask<" + type + "> " + name +
                    "(global::System.IO.Stream stream, global::System.Threading.CancellationToken cancellationToken)";
                break;
            case SerializerApi.DeserializeStreamValue:
                // The value argument is a compatibility parameter: the result is deserialized independently.
                signature = "global::System.Threading.Tasks.ValueTask<" + type + "> " + name + "(global::System.IO.Stream stream, " + type +
                    " value, global::System.Threading.CancellationToken cancellationToken)";
                break;
            case SerializerApi.SerializeArray:
                signature = "byte[] " + name + "(in " + type + " value)";
                break;
            case SerializerApi.SerializeBufferWriter:
                signature = "void " + name + "(in " + type + " value, global::System.Buffers.IBufferWriter<byte> bufferWriter)";
                break;
            case SerializerApi.SerializeBuffer:
                signature = "void " + name + "(in " + type + " value, LuminBufferWriter writerBuffer)";
                break;
            case SerializerApi.DeserializeSpan:
                signature = type + " " + name + "(ReadOnlySpan<byte> buffer)";
                break;
            case SerializerApi.DeserializeSpanRef:
                signature = "int " + name + "(ReadOnlySpan<byte> buffer, ref " + type + " value)";
                break;
            case SerializerApi.DeserializeBuffer:
                signature = type + " " + name + "(LuminBufferWriter bufferWriter)";
                break;
            case SerializerApi.DeserializeBufferRef:
                signature = "int " + name + "(LuminBufferWriter bufferWriter, ref " + type + " value)";
                break;
            case SerializerApi.DeserializeSequenceRef:
                signature = "int " + name + "(global::System.Buffers.ReadOnlySequence<byte> buffer, ref " + type + " value)";
                break;
            case SerializerApi.DeserializeSequence:
                signature = type + " " + name + "(in global::System.Buffers.ReadOnlySequence<byte> buffer)";
                break;
            case SerializerApi.SerializeWriter:
                signature = "void " + name + "(ref LuminPackWriter state, scoped in " + type + " value)";
                break;
            case SerializerApi.DeserializeReader:
                signature = "void " + name + "(ref LuminPackReader state, scoped ref " + type + " value)";
                break;
            case SerializerApi.SerializeJsonWriter:
                signature = "void " + name + "(ref LuminPackJsonWriter state, scoped in " + type + " value)";
                break;
            case SerializerApi.DeserializeJsonReader:
                signature = "void " + name + "(ref LuminPackJsonReader state, scoped ref " + type + " value)";
                break;
            case SerializerApi.SerializeJsonString:
                signature = "string " + name + "(" + type + " value)";
                break;
            case SerializerApi.SerializeJsonBufferWriter:
                signature = "void " + name + "(" + type + " value, global::System.Buffers.IBufferWriter<byte> bufferWriter)";
                break;
            case SerializerApi.SerializeJsonBuffer:
                signature = "void " + name + "(" + type + " value, LuminBufferWriter writerBuffer)";
                break;
            case SerializerApi.DeserializeJsonString:
                signature = type + " " + name + "(string buffer)";
                break;
            case SerializerApi.DeserializeJsonBytes:
                signature = type + " " + name + "(ReadOnlySpan<byte> buffer)";
                break;
            case SerializerApi.DeserializeJsonChars:
                signature = type + " " + name + "(ReadOnlySpan<char> buffer)";
                break;
            case SerializerApi.DeserializeJsonBuffer:
                signature = type + " " + name + "(LuminBufferWriter bufferWriter)";
                break;
            case SerializerApi.DeserializeJsonStringRef:
                signature = "int " + name + "(string buffer, ref " + type + " value)";
                break;
            case SerializerApi.DeserializeJsonBytesRef:
                signature = "int " + name + "(ReadOnlySpan<byte> buffer, ref " + type + " value)";
                break;
            case SerializerApi.DeserializeJsonCharsRef:
                signature = "int " + name + "(ReadOnlySpan<char> buffer, ref " + type + " value)";
                break;
            case SerializerApi.DeserializeJsonBufferRef:
                signature = "int " + name + "(LuminBufferWriter bufferWriter, ref " + type + " value)";
                break;
            default:
                LuminPackExceptionHelper.ThrowArgumentOutOfRange(nameof(api));
                return;
        }
        bool stream = api is SerializerApi.SerializeStream or SerializerApi.DeserializeStream or SerializerApi.DeserializeStreamValue;
        if (!stream)
            w.Append("        [MethodImpl(MethodImplOptions.AggressiveInlining)]\n");
        w.Append("        internal static ");
        if (stream)
            w.Append("async ");
        w.Append(signature).Append('\n');
        if (bound.OpenProfile)
            w.Append("            where TLuminProfile : struct, ILuminPackOptions\n");

        string operation = bound.Writes ? "Serialize" : "Deserialize";
        if (bound.Json)
            operation += "Json";
        string target = root.Owner != null
            ? root.Owner + (bound.Writes ? ".Write" : ".Read") + "<" + profile + ">"
            : "LuminPackSerializer." + operation + "_" + root.RootName + "<" + root.RootArguments + profile + ">";
        if (api is SerializerApi.SerializeWriter or SerializerApi.DeserializeReader or SerializerApi.SerializeJsonWriter or SerializerApi.DeserializeJsonReader)
        {
            w.Append("            => ").Append(target).Append(bound.Writes ? "(ref state, in value);\n\n" : "(ref state, ref value);\n\n");
            return;
        }

        // A document read selects the first token itself and discards the reader after the end check.
        // The shared state extension's token selection and completion are for caller-owned readers.
        if (root.Owner != null && bound.Json && !bound.Writes)
            target = "LuminPackSerializer.DeserializeJson_" + root.RootName + "<" + profile + ">";

        bool binaryShape = api is SerializerApi.SerializeArray or SerializerApi.SerializeBuffer or SerializerApi.DeserializeSpan or SerializerApi.DeserializeSpanRef or
            SerializerApi.DeserializeStream or SerializerApi.DeserializeStreamValue;
        var bodyKey = (Api: api, Shape: binaryShape ? bound.Shape : EntryShape.Pooled, Measured: binaryShape && measured);
        // The compound's exact writer encodes strings under its fixed profile. Read state follows the formatter graph.
        if (api == SerializerApi.SerializeArray && bodyKey.Shape.Kind == EntryKind.CompoundDirect && profile != RootEntry.DefaultProfile)
            bodyKey = (api, EntryShape.Pooled, false);
        if (api == SerializerApi.SerializeBuffer && profile != RootEntry.DefaultProfile &&
            bodyKey.Shape.Kind is EntryKind.RootString or EntryKind.RootUri or EntryKind.RootCulture or EntryKind.LazyString)
            bodyKey = (api, EntryShape.Pooled, false);
        if (!documentBodies.TryGetValue(bodyKey, out string? body))
        {
            body = DocumentBody(bodyKey.Api, bodyKey.Shape, bodyKey.Measured);
            documentBodies.Add(bodyKey, body);
        }
        string measure = "LuminPackSerializer.MeasureRecord_" + root.RootName + "<" + root.RootArguments + profile + ">";
        AppendBody(w, Limits(body, type, root), type, profile, measure, target, root.RootName);
        w.Append('\n');
    }

    /// <summary>Writes the selected document body without materializing each substitution as a new body string.</summary>
    /// <param name="writer">The interceptor output.</param>
    /// <param name="body">The document template after applying input limits.</param>
    /// <param name="type">The closed payload spelling.</param>
    /// <param name="profile">The declared or forwarded profile.</param>
    /// <param name="measure">The exact record measurement call target.</param>
    /// <param name="target">The existing static read or write target.</param>
    /// <param name="rootName">The nested root's published suffix.</param>
    private static void AppendBody(StringBuilder writer, string body, string type, string profile, string measure, string target,
        string rootName)
    {
        int start = 0;
        for (int marker = body.IndexOf('%'); marker >= 0; marker = body.IndexOf('%', start))
        {
            int end = body.IndexOf('%', marker + 1);
            if (end < 0)
                break;
            writer.Append(body, start, marker - start);
            switch (body.AsSpan(marker + 1, end - marker - 1))
            {
                case "TYPE":
                    writer.Append(type);
                    break;
                case "PROFILE":
                    writer.Append(profile);
                    break;
                case "MEASURE":
                    writer.Append(measure);
                    break;
                case "WRITE":
                    writer.Append(target).Append("(ref writer, in value);");
                    break;
                case "WRITEDIRECT":
                    writer.Append("LuminPackSerializer.WriteExact_").Append(rootName).Append("(ref writer, in directValue);");
                    break;
                case "FALLBACKWRITE":
                    writer.Append(target).Append("(ref fallbackWriter, in value);");
                    break;
                case "READ":
                    writer.Append(target).Append("(ref reader, ref value);");
                    break;
                default:
                    writer.Append(body, marker, end - marker + 1);
                    break;
            }
            start = end + 1;
        }
        writer.Append(body, start, body.Length - start);
    }

    /// <summary>Selects the established document boundary and combines it with the required return shape.</summary>
    /// <param name="api">The intercepted facade overload.</param>
    /// <param name="shape">The payload's specialized binary entry form.</param>
    /// <param name="measured">Whether the root publishes its record measure.</param>
    /// <returns>The interceptor body template.</returns>
    private static string DocumentBody(SerializerApi api, EntryShape shape, bool measured)
    {
        string binaryRead = BinarySpanRead;
        if (api is SerializerApi.DeserializeSpan or SerializerApi.DeserializeSpanRef or SerializerApi.DeserializeStream or SerializerApi.DeserializeStreamValue)
        {
            if (shape.Kind == EntryKind.RawScalar)
                binaryRead = RawRead;
            else if (shape.StatelessRead)
                binaryRead = shape.Kind == EntryKind.FixedEnum ? StatelessRead.Replace("%ENUMCHECK%", EnumCheck) : StatelessRead.Replace("%ENUMCHECK%", string.Empty);
        }
        if (shape.OutlineNonNullRead && api is SerializerApi.DeserializeSpan or SerializerApi.DeserializeSpanRef)
        {
            binaryRead = NullObjectSpanRead + "\n" +
                "                [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]\n" +
                LocalRead("ReadNonNull", "ReadOnlySpan<byte>", binaryRead);
        }
        binaryRead = BinarySpanInputCheck + binaryRead;
        string statements = api switch
        {
            SerializerApi.SerializeArray => AllocatingWrite(shape, measured),
            SerializerApi.SerializeBufferWriter => BinaryBufferWriter,
            SerializerApi.SerializeBuffer => RetainedWrite(shape),
            SerializerApi.SerializeStream => StreamWrite,
            SerializerApi.DeserializeSpan => ReturnValue(binaryRead, "buffer"),
            SerializerApi.DeserializeSpanRef => binaryRead,
            SerializerApi.DeserializeSequenceRef => SequenceRead,
            SerializerApi.DeserializeSequence => ReturnValue(SequenceRead, "buffer", "global::System.Buffers.ReadOnlySequence<byte>"),
            SerializerApi.DeserializeBuffer => ReturnValue(BinaryBufferRead, "bufferWriter"),
            SerializerApi.DeserializeBufferRef => BinaryBufferRead,
            SerializerApi.DeserializeStream or SerializerApi.DeserializeStreamValue => StreamRead + ReturnValue(binaryRead, "data.Span"),
            SerializerApi.SerializeJsonString => JsonStringWrite,
            SerializerApi.SerializeJsonBufferWriter => JsonBufferWriter,
            SerializerApi.SerializeJsonBuffer => JsonBufferWrite,
            SerializerApi.DeserializeJsonBytes => ReturnValue(JsonSpanRead, "buffer"),
            SerializerApi.DeserializeJsonChars => ReturnValue(JsonCharacters(), "buffer", "ReadOnlySpan<char>"),
            SerializerApi.DeserializeJsonString => StringCheck + ReturnValue(JsonCharacters(), "buffer.AsSpan()", "ReadOnlySpan<char>"),
            SerializerApi.DeserializeJsonBuffer => ReturnValue(JsonBufferRead, "bufferWriter"),
            SerializerApi.DeserializeJsonBytesRef => JsonSpanRead,
            SerializerApi.DeserializeJsonCharsRef => JsonCharacters(),
            SerializerApi.DeserializeJsonStringRef => StringCheck + "                return ReadCharacters(buffer.AsSpan(), ref value);\n\n" +
                LocalRead("ReadCharacters", "ReadOnlySpan<char>", JsonCharacters()),
            SerializerApi.DeserializeJsonBufferRef => JsonBufferRead,
            _ => LuminPackExceptionHelper.ThrowArgumentOutOfRange<string>(nameof(api)),
        };
        using var body = new PooledTextBuffer(statements.Length + 24);
        body.Append("        {\n");
        for (int start = 0; start < statements.Length;)
        {
            int end = statements.IndexOf('\n', start) + 1;
            int indent = end - start > 4 && statements[start] == ' ' ? 4 : 0;
            body.Append(statements, start + indent, end - start - indent);
            start = end;
        }
        body.Append("        }\n");
        return body.ToString();
    }

    /// <summary>Creates a local read function with the original input boundary and ref semantics.</summary>
    /// <param name="name">The local function name.</param>
    /// <param name="input">The local function's input type.</param>
    /// <param name="body">The statements of the read operation.</param>
    /// <returns>The local function source.</returns>
    private static string LocalRead(string name, string input, string body) =>
        "                static int " + name + "(" + input + " buffer, ref %TYPE% value)\n                {\n" +
        Indent(body) + "                }\n";

    /// <summary>Wraps the existing ref read in the return-value form of the facade.</summary>
    /// <param name="body">The established ref read boundary.</param>
    /// <param name="buffer">The input expression.</param>
    /// <param name="input">The local function's input type when inference is unavailable.</param>
    /// <returns>The statements of the return-value read.</returns>
    private static string ReturnValue(string body, string buffer, string? input = null) =>
        "                %TYPE% result = default!;\n                ReadValue(" + buffer + ", ref result);\n                return result;\n\n" +
        LocalRead("ReadValue", input ?? (buffer == "bufferWriter" ? "LuminBufferWriter" : "ReadOnlySpan<byte>"),
            buffer == "bufferWriter" ? body.Replace("bufferWriter", "buffer") : body);

    /// <summary>Converts UTF-16 characters when necessary and passes the resulting bytes to the JSON document reader.</summary>
    /// <returns>The character read boundary and its local byte reader.</returns>
    private static string JsonCharacters() => JsonCharacterRead + "\n" +
        LocalRead("ReadUtf8", "ReadOnlySpan<char>", JsonTranscodedRead) + "\n" +
        LocalRead("ReadBytes", "ReadOnlySpan<byte>", JsonSpanRead);

    /// <summary>Indents a template when it moves into a local function or try block.</summary>
    /// <param name="body">The original template statements.</param>
    /// <returns>The statements with one extra level of indentation.</returns>
    private static string Indent(string body) => "    " + body.TrimEnd('\n').Replace("\n", "\n    ") + "\n";

    /// <summary>Selects the established allocating binary write for one closed payload shape.</summary>
    /// <param name="shape">The normalized entry form.</param>
    /// <param name="measured">Whether the record measure is published.</param>
    /// <returns>The write statements.</returns>
    private static string AllocatingWrite(EntryShape shape, bool measured)
    {
        if (shape.Kind == EntryKind.RawScalar)
            return RawWrite;
        if (shape.Kind == EntryKind.RawList)
            return RawListWrite.Replace("%ELEMENT%", shape.Element!);
        if (FixedRoot(shape) is { } fixedRoot)
        {
            if (shape.Kind is not (EntryKind.RawArray or EntryKind.BigInteger or EntryKind.BitArray))
                return fixedRoot.Prepare + "                var buffer = new byte[length];\n" +
                    "                ref byte destination = ref MemoryMarshal.GetArrayDataReference(buffer);\n" +
                    fixedRoot.Store + "                return buffer;\n";
            return fixedRoot.Prepare +
                "                var buffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(length);\n" +
                "                ref byte destination = ref MemoryMarshal.GetArrayDataReference(buffer);\n" +
                fixedRoot.Store + "                return buffer;\n";
        }
        if (shape.Kind == EntryKind.FixedObjectArray)
            return FixedObjectArrayWrite.Replace("%ELEMENTOWNER%", FixedObjectOwner(shape.Element!)) + "\n" +
                "                [MethodImpl(MethodImplOptions.NoInlining)]\n" +
                "                static byte[] WriteOversized(in %TYPE% value)\n                {\n" + Indent(PooledWrite) + "                }\n";
        if (shape.Kind is EntryKind.TimeZoneSnapshot or EntryKind.TimeZoneCatalog)
        {
            const string cache = "global::LuminPack.Code.LuminPackTimeZoneInfoCache";
            bool catalog = shape.Kind == EntryKind.TimeZoneCatalog;
            return TimeZoneSnapshotWrite.Replace("%TIMEZONE_TEXT%", catalog ? cache + ".GetBinaryText(value, out byte marker)" :
                    "value is null ? null : " + cache + ".GetText(value)")
                .Replace("%TIMEZONE_MARKER%", catalog ? "marker, " : string.Empty) + PooledWrite;
        }
        if (shape.Kind == EntryKind.PackedPriorityQueue)
            return PackedPriorityQueueWrite + PooledWrite;
        if (shape.Kind is EntryKind.ExactList or EntryKind.ExactCapturedList or
            EntryKind.ExactHashSet or EntryKind.ExactSmallHashSet)
            return ExactInterfaceWrite(shape);
        string? size = shape.Kind switch
        {
            EntryKind.FixedEnum => "                int directLength = Unsafe.SizeOf<%TYPE%>();\n",
            EntryKind.NullableDirect => "                int directLength = value.HasValue ? 1 + " +
                (shape.Width?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unsafe.SizeOf<" + shape.Element + ">()") + " : 1;\n",
            EntryKind.FixedWidthRecord => "                int directLength = " + shape.Width!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";\n",
            EntryKind.RawSequence => "                int directLength = checked(sizeof(int) + checked((int)value.Length) * Unsafe.SizeOf<" + shape.Element + ">());\n",
            EntryKind.FixedRecord or EntryKind.DirectRecord or EntryKind.RootString or EntryKind.RootUri or EntryKind.RootCulture
                when measured => MeasureRecord,
            EntryKind.CompoundDirect when measured => MeasureRecord,
            _ => null,
        };
        if (size == null)
            return shape.NullObject
                ? "                if (value is null) return new byte[] { LuminPackCode.NullObject };\n" +
                    "                return WriteNonNull(in value);\n\n" +
                    "                [MethodImpl(MethodImplOptions.NoInlining)]\n" +
                    "                static byte[] WriteNonNull(in %TYPE% value)\n                {\n" + Indent(PooledWrite) + "                }\n"
                : PooledWrite;
        bool exact = shape.Kind is EntryKind.FixedEnum or EntryKind.NullableDirect or EntryKind.FixedRecord or EntryKind.FixedWidthRecord;
        if (shape.Kind == EntryKind.CompoundDirect)
            return size + "                var directBuffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(directLength);\n" +
                DirectWrite.Replace("%WRITE%", "%WRITEDIRECT%") + "                return directBuffer;\n";
        return size + (exact ? "                var directBuffer = new byte[directLength];\n" :
            "                var directBuffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(directLength);\n") +
            DirectWrite + (exact ? "                return directBuffer;\n" : MeasuredFallback);
    }

    /// <summary>Returns the generated payload root that owns the proven fixed stores.</summary>
    /// <param name="element">The published payload suffix.</param>
    /// <returns>The qualified fixed object owner.</returns>
    private static string FixedObjectOwner(string element) => "LuminPackSerializer.LuminRoots_" + element;

    /// <summary>Describes the bound and address-only stores of a fixed wire record.</summary>
    /// <param name="shape">The analyzed root contract.</param>
    /// <returns>The generated bound and stores, or null for a variable/stateful root.</returns>
    private static (string Prepare, string Store)? FixedRoot(EntryShape shape)
    {
        switch (shape.Kind)
        {
            case EntryKind.RawScalar:
            case EntryKind.FixedEnum:
                return ("                int length = Unsafe.SizeOf<%TYPE%>();\n",
                    "                LuminPackWriter.Write(ref destination, in value);\n");
            case EntryKind.NullableDirect when shape.Width == null && shape.RetainedDirect:
                return ("                bool present = value.HasValue;\n" +
                    "                int length = present ? 1 + Unsafe.SizeOf<" + shape.Element + ">() : 1;\n",
                    "                if (present) LuminPackWriter.Write(ref Unsafe.Add(ref destination, 1), in global::System.Nullable.GetValueRefOrDefaultRef(in value));\n" +
                    "                destination = present ? (byte)0 : LuminPackCode.NullObject;\n");
            case EntryKind.FixedObject:
            case EntryKind.FixedVersion:
                string owner = shape.Kind == EntryKind.FixedObject ? FixedObjectOwner(shape.Element!) : "LuminPackSerializer";
                string width = shape.Kind == EntryKind.FixedObject ? owner + ".FixedRootWidth()" : "17";
                string write = shape.Kind == EntryKind.FixedObject ? "WriteFixedRoot" : "WriteFixedRoot_Version";
                return ("                var record = value;\n                int length = record is null ? 1 : " + width + ";\n",
                    "                if (record is null) destination = LuminPackCode.NullObject;\n" +
                    "                else " + owner + "." + write + "(ref destination, record);\n");
            case EntryKind.BitArray:
                return ("                var bits = value;\n" +
                    "                int bitCount = bits?.Length ?? -1;\n" +
                    "                int length = sizeof(int) + ((bitCount < 0 ? 0 : (int)(((uint)bitCount + 31) >> 5)) * sizeof(int));\n",
                    "                global::LuminPack.Code.LuminPackCollectionAccess.WriteReservedBitArray(ref destination, bits, bitCount);\n");
            case EntryKind.BigInteger:
                return ("                int byteCount = value.GetByteCount();\n" +
                    "                int length = checked(sizeof(int) + byteCount);\n",
                    "                LuminPackWriter.Write(ref destination, byteCount);\n" +
                    "                value.TryWriteBytes(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref destination, sizeof(int)), byteCount), out _);\n");
            case EntryKind.RawArray:
                return ("                var items = value;\n" +
                    "                int length = LuminPackWriter.SpanRecordBound<" + shape.Element + ">(items is null, items?.Length ?? 0);\n",
                    "                LuminPackWriter.WriteReservedSpan<" + shape.Element + ">(ref destination, items is null, items);\n");
            default:
                return null;
        }
    }

    /// <summary>Uses the existing retained window for state-free fixed records and bounded text roots.</summary>
    /// <param name="shape">The analyzed root contract.</param>
    /// <returns>The complete retained writer entry with its outlined original path.</returns>
    private static string RetainedWrite(EntryShape shape)
    {
        if (!shape.RetainedDirect)
            return BinaryBufferWrite;
        string hot;
        if (shape.Kind is EntryKind.RootString or EntryKind.RootUri or EntryKind.RootCulture or EntryKind.LazyString)
        {
            hot = shape.Kind switch
            {
                EntryKind.RootString => RetainedTextRoot,
                EntryKind.RootUri => RetainedTextObject.Replace("%TEXT%", "record.OriginalString").Replace("%EXACT%", string.Empty),
                EntryKind.LazyString => GuardedTextObject("record.Value", "!record.IsValueCreated"),
                _ => GuardedTextObject("record.Name", "record.GetType() != typeof(global::System.Globalization.CultureInfo)"),
            };
        }
        else if (FixedRoot(shape) is { } fixedRoot)
        {
            hot = "                if (writerBuffer is not null)\n                {\n" + Indent(fixedRoot.Prepare) +
                "                    ref byte destination = ref writerBuffer.TryBeginFixedRoot(length);\n" +
                "                    if (!Unsafe.IsNullRef(ref destination))\n                    {\n" + Indent(Indent(fixedRoot.Store)) +
                "                        writerBuffer.CompleteWrite(length);\n                        return;\n                    }\n                }\n";
        }
        else if (shape.Kind == EntryKind.FixedWidthRecord && shape.Element != null)
            hot = RetainedFixedStorage.Replace("%OWNER%", FixedObjectOwner(shape.Element)).Replace("%WIDTH%",
                shape.Width!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        else if (shape.Kind == EntryKind.BoundedNullableObject)
            hot = RetainedBoundedNullableObject.Replace("%OWNER%", FixedObjectOwner(shape.Element!));
        else if (shape.NullObject)
            hot = RetainedNullRoot;
        else
            return BinaryBufferWrite;
        return hot + "                WriteRootSlow(in value, writerBuffer);\n\n" +
            "                [MethodImpl(MethodImplOptions.NoInlining)]\n" +
            "                static void WriteRootSlow(in %TYPE% value, LuminBufferWriter writerBuffer)\n                {\n" +
            Indent(BinaryBufferWrite) + "                }\n";
    }

    private const string RetainedFixedStorage = """
                if (writerBuffer is not null)
                {
                    ref byte destination = ref writerBuffer.TryBeginFixedRoot(%WIDTH%);
                    if (!Unsafe.IsNullRef(ref destination))
                    {
                        %OWNER%.WriteFixedStorage(ref destination, in value);
                        writerBuffer.CompleteWrite(%WIDTH%);
                        return;
                    }
                }

    """;

    private const string RetainedBoundedNullableObject = """
                if (writerBuffer is not null)
                {
                    var record = value;
                    ref byte destination = ref writerBuffer.TryBeginFixedRoot(record is null ? 1 : %OWNER%.NullableRootBound());
                    if (!Unsafe.IsNullRef(ref destination))
                    {
                        int written;
                        if (record is null)
                        {
                            destination = LuminPackCode.NullObject;
                            written = 1;
                        }
                        else
                            written = %OWNER%.WriteBoundedNullableRoot(ref destination, record);
                        writerBuffer.CompleteWrite(written);
                        return;
                    }
                }

    """;

    private const string FixedObjectArrayWrite = """
                        var items = value;
                        if (items is null) return new byte[] { 255, 255, 255, 255 };
                        int width = %ELEMENTOWNER%.FixedRootWidth();
                        long bound = sizeof(int) + (long)items.Length * width;
                        if (bound > Array.MaxLength) return WriteOversized(in value);
                        var buffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>((int)bound);
                        ref byte start = ref MemoryMarshal.GetArrayDataReference(buffer);
                        LuminPackWriter.Write(ref start, items.Length);
                        ref byte destination = ref Unsafe.Add(ref start, sizeof(int));
                        foreach (var item in items)
                        {
                            if (item is null)
                            {
                                destination = LuminPackCode.NullObject;
                                destination = ref Unsafe.Add(ref destination, 1);
                            }
                            else
                            {
                                %ELEMENTOWNER%.WriteFixedRoot(ref destination, item);
                                destination = ref Unsafe.Add(ref destination, width);
                            }
                        }
                        if (Unsafe.ByteOffset(ref start, ref destination) != (nint)bound)
                            Array.Resize(ref buffer, (int)Unsafe.ByteOffset(ref start, ref destination));
                        return buffer;
        """ + "\n";

    private const string RetainedNullRoot = """
                        if (value is null && writerBuffer is not null)
                        {
                            ref byte destination = ref writerBuffer.TryBeginFixedRoot(1);
                            if (!Unsafe.IsNullRef(ref destination))
                            {
                                destination = LuminPackCode.NullObject;
                                writerBuffer.CompleteWrite(1);
                                return;
                            }
                        }
        """ + "\n";

    private const string RetainedTextRoot = """
                        if (writerBuffer is not null)
                        {
                            var text = value;
                            ref byte destination = ref writerBuffer.TryBeginFixedRoot(LuminPackWriter.Utf16RecordBound(text));
                            if (!Unsafe.IsNullRef(ref destination))
                            {
                                int written = LuminPackWriter.WriteReservedUtf16Record(ref destination, text);
                                writerBuffer.CompleteWrite(written);
                                return;
                            }
                        }
        """ + "\n";

    /// <summary>Preserves the original writer when the projected text can execute user code.</summary>
    /// <param name="text">The stable framework string after the guard.</param>
    /// <param name="guard">The condition that requires the original writer.</param>
    /// <returns>The retained object write.</returns>
    private static string GuardedTextObject(string text, string guard) =>
        RetainedTextObject.Replace("%TEXT%", text).Replace("%EXACT%",
            "                        if (" + guard + ")\n" +
            "                        {\n                            WriteRootSlow(in value, writerBuffer);\n                            return;\n                        }\n");

    private const string RetainedTextObject = """
                        if (writerBuffer is not null)
                        {
                            var record = value;
                            if (record is null)
                            {
                                ref byte marker = ref writerBuffer.TryBeginFixedRoot(1);
                                if (!Unsafe.IsNullRef(ref marker))
                                {
                                    marker = LuminPackCode.NullObject;
                                    writerBuffer.CompleteWrite(1);
                                    return;
                                }
                            }
                            else
                            {
        %EXACT%                        string text = %TEXT%;
                                ref byte destination = ref writerBuffer.TryBeginFixedRoot(1 + LuminPackWriter.Utf16RecordBound(text));
                                if (!Unsafe.IsNullRef(ref destination))
                                {
                                    destination = 0;
                                    int written = LuminPackWriter.WriteReservedUtf16Record(ref Unsafe.Add(ref destination, 1), text);
                                    writerBuffer.CompleteWrite(1 + written);
                                    return;
                                }
                            }
                        }
        """ + "\n";

    /// <summary>Writes the proven exact interface implementation and keeps the existing pooled entry for other values.</summary>
    /// <param name="shape">The collection or payload shape to use.</param>
    /// <returns>The resulting text.</returns>
    private static string ExactInterfaceWrite(EntryShape shape)
    {
        bool captureList = shape.Kind == EntryKind.ExactCapturedList;
        bool list = shape.Kind == EntryKind.ExactList || captureList;
        string implementation = "global::System.Collections.Generic." + (list ? "List" : "HashSet") + "<" + shape.Element + ">";
        const string marshal = "global::LuminPack.Code.LuminPackMarshal";
        const string listSpan = "var items = " + marshal + ".GetListSpan(exact);\n";
        string capture = captureList ? "                " + listSpan : string.Empty;
        string write = list
            ? (captureList ? string.Empty : "                    " + listSpan) +
              "                    writer.Write<" + shape.Element + ">(items, items.Length);\n"
            : "                    " + marshal + "." + (shape.Kind == EntryKind.ExactSmallHashSet ? "WriteSmallHashSetRaw" : "WriteHashSetRaw") +
              "(ref writer, exact);\n";
        return """
                        var selectedValue = value;
                        if (selectedValue is not null && selectedValue.GetType() == typeof(%IMPLEMENTATION%))
                        {
                            var exact = Unsafe.As<%IMPLEMENTATION%>(selectedValue);
            %CAPTURE%                int directLength = checked(sizeof(int) + %COUNT% * Unsafe.SizeOf<%ELEMENT%>());
                            var directBuffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(directLength);
                            var directSpan = directBuffer.AsSpan();
                            var writer = new LuminPackWriter(ref directSpan);
            """.Replace("%IMPLEMENTATION%", implementation).Replace("%ELEMENT%", shape.Element!)
                .Replace("%CAPTURE%", capture).Replace("%COUNT%", captureList ? "items.Length" : "exact.Count") + "\n" + write +
            """
                            if (writer.Position == directLength)
                                return directBuffer;
                        }
                        return SerializeInterfaceSlow(in selectedValue);

                        [MethodImpl(MethodImplOptions.NoInlining)]
                        static byte[] SerializeInterfaceSlow(in %TYPE% value)
                        {
            """ + "\n" + Indent(PooledWrite) + "                }\n";
    }

    private const string TimeZoneSnapshotWrite = """
                        if (default(%PROFILE%).StringEncoding == LuminPackStringEncoding.UTF16 &&
                            default(%PROFILE%).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Length)
                        {
                            string? snapshot = %TIMEZONE_TEXT%;
                            int length = global::LuminPack.Code.LuminPackTimeZoneInfoCache.Utf16SnapshotRecordLength(snapshot);
                            var buffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(length);
                            var span = buffer.AsSpan();
                            var writer = new LuminPackWriter(ref span);
                            global::LuminPack.Code.LuminPackTimeZoneInfoCache.WriteTimeZoneText<%PROFILE%>(ref writer, snapshot, %TIMEZONE_MARKER%capacityReserved: true);
                            return buffer;
                        }
        """ + "\n";

    private const string PackedPriorityQueueWrite = """
                        if (global::LuminPack.Code.LuminPackCollectionAccess.TryAllocatePriorityQueueRecord(value) is { } direct)
                            return direct;
        """ + "\n";

    private const string PooledWrite = """
                        var writerBuffer = LuminBufferWriterPool.Rent();
                        try
                        {
                            var writer = new LuminPackWriter(writerBuffer);
                            %WRITE%
                            var buffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(writer.CurrentIndex);
                            writer.GetSpan().CopyTo(buffer.AsSpan());
                            return buffer;
                        }
                        finally
                        {
                            LuminBufferWriterPool.Return(writerBuffer);
                        }
        """ + "\n";

    private const string BinaryBufferWriter = """
                        if (bufferWriter is null)
                            LuminPackExceptionHelper.ThrowArgumentNullException(nameof(bufferWriter));
                        var writerBuffer = LuminBufferWriterPool.Rent();
                        try
                        {
                            var writer = new LuminPackWriter(writerBuffer);
                            %WRITE%
                            writerBuffer.CompleteWrite(writer.CurrentIndex);
                            var source = writerBuffer.GetSpan();
                            var destination = bufferWriter.GetSpan(source.Length);
                            source.CopyTo(destination);
                            bufferWriter.Advance(source.Length);
                        }
                        finally
                        {
                            LuminBufferWriterPool.Return(writerBuffer);
                        }
        """ + "\n";

    private const string BinaryBufferWrite = """
                        var writer = new LuminPackWriter(writerBuffer);
                        %WRITE%
                        writerBuffer.CompleteWrite(writer.CurrentIndex);
        """ + "\n";

    private const string BinarySpanInputCheck = """
                        LuminPackInputPolicy.CheckInput(buffer.Length, %LIMIT%);
        """ + "\n";

    private const string BinarySpanRead = """
                        var state = LuminPackReaderOptionalState.Lease(ref _threadStaticReaderOptionalState);
                        var reader = new LuminPackReader(ref buffer, state);
                        try
                        {
                            %READ%
                            return reader.Position;
                        }
                        finally
                        {
                            state.Release();
                        }
        """ + "\n";

    private const string NullObjectSpanRead = """
                        if (!buffer.IsEmpty && buffer[0] == global::LuminPack.Code.LuminPackCode.NullObject)
                            return 1;
                        return ReadNonNull(buffer, ref value);
        """ + "\n";

    private const string SequenceRead = """
                        LuminPackInputPolicy.CheckInput(buffer.Length, %LIMIT%);
                        if (!buffer.IsSingleSegment)
                        {
                            var staging = LuminBufferWriterPool.Rent();
                            try
                            {
                                staging.LoadSequence(in buffer);
                                var reader = new LuminPackReader(staging);
                                %READ%
                                return reader.Position;
                            }
                            finally
                            {
                                LuminBufferWriterPool.Return(staging);
                            }
                        }

                        {
                            var state = LuminPackReaderOptionalState.Lease(ref _threadStaticReaderOptionalState);
                            var single = buffer.FirstSpan;
                            var reader = new LuminPackReader(ref single, state);
                            try
                            {
                                %READ%
                                return reader.Position;
                            }
                            finally
                            {
                                state.Release();
                            }
                        }
        """ + "\n";

    private const string BinaryBufferRead = """
                        LuminPackInputPolicy.CheckInput(bufferWriter.GetSpan().Length, %LIMIT%);
                        try
                        {
                            var reader = new LuminPackReader(bufferWriter);
                            %READ%
                            return reader.Position;
                        }
                        finally
                        {
                            bufferWriter.ReaderState.ResetOperationState();
                        }
        """ + "\n";

    private const string StreamWrite = """
                        var writerBuffer = LuminBufferWriterPool.Rent();
                        try
                        {
                            WriteValue(in value, writerBuffer);
                            await writerBuffer.WriteToAndResetAsync(stream, cancellationToken).ConfigureAwait(false);
                            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            LuminBufferWriterPool.Return(writerBuffer);
                        }

                        static void WriteValue(in %TYPE% value, LuminBufferWriter writerBuffer)
                        {
                            var writer = new LuminPackWriter(writerBuffer);
                            %WRITE%
                            writerBuffer.CompleteWrite(writer.CurrentIndex);
                        }
        """ + "\n";

    private const string StreamRead = """
                        using var data = await global::LuminPack.Utility.LuminPackStreamInput.ReadAsync(stream, %STREAMLIMIT%, cancellationToken).ConfigureAwait(false);
        """ + "\n";

    private const string JsonStringWrite = """
                        var writerBuffer = LuminBufferWriterPool.Rent();
                        try
                        {
                            var writer = new LuminPackJsonWriter(writerBuffer);
                            %WRITE%
                            return default(%PROFILE%).StringEncoding is LuminPackStringEncoding.UTF8
                                ? Encoding.UTF8.GetString(writer.GetSpan())
                                : Encoding.Unicode.GetString(writer.GetSpan());
                        }
                        finally
                        {
                            LuminBufferWriterPool.Return(writerBuffer);
                        }
        """ + "\n";

    private const string JsonBufferWriter = """
                        if (bufferWriter is null)
                            LuminPackExceptionHelper.ThrowArgumentNullException(nameof(bufferWriter));
                        var state = LuminPackWriterOptionalState.Lease(ref _threadStaticWriterOptionalState);
                        LuminPackJsonWriter writer = default;
                        try
                        {
                            writer = new LuminPackJsonWriter(bufferWriter, state, 256);
                            %WRITE%
                            writer.FlushToBufferWriter();
                        }
                        finally
                        {
                            writer.Dispose();
                            state.Release();
                        }
        """ + "\n";

    private const string JsonBufferWrite = """
                        var writer = new LuminPackJsonWriter(writerBuffer);
                        %WRITE%
                        writerBuffer.CompleteWrite(writer.CurrentIndex);
        """ + "\n";

    private const string JsonSpanRead = """
                        LuminPackInputPolicy.CheckInput(buffer.Length, %LIMIT%);
                        var state = LuminPackReaderOptionalState.Lease(ref _threadStaticReaderOptionalState);
                        try
                        {
                            var reader = new LuminPackJsonReader(ref buffer, state);
                            if (!reader.Read<%PROFILE%>())
                                LuminPackExceptionHelper.ThrowFormatException("JSON input does not contain a value");
                            %READ%
                            reader.EnsureEndOfDocument<%PROFILE%>();
                            return reader.CurrentIndex;
                        }
                        finally
                        {
                            state.Release();
                        }
        """ + "\n";

    private const string JsonCharacterRead = """
                        var encoding = default(%PROFILE%).StringEncoding;
                        LuminPackInputPolicy.CheckCharacters(buffer, (int)encoding, %LIMIT%);
                        if (encoding is LuminPackStringEncoding.UTF16)
                            return ReadBytes(MemoryMarshal.AsBytes(buffer), ref value);
                        return ReadUtf8(buffer, ref value);
        """ + "\n";

    private const string JsonTranscodedRead = """
                        if (buffer.Length <= 1024)
                        {
                            Span<byte> span = stackalloc byte[buffer.Length * 3];
                            int length = Encoding.UTF8.GetBytes(buffer, span);
                            return ReadBytes(span.Slice(0, length), ref value);
                        }
                        int byteCount = Encoding.UTF8.GetByteCount(buffer);
                        byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
                        try
                        {
                            int written = Encoding.UTF8.GetBytes(buffer, rented);
                            return ReadBytes(rented.AsSpan(0, written), ref value);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(rented);
                        }
        """ + "\n";

    private const string StringCheck = """
                        if (buffer is null)
                            LuminPackExceptionHelper.ThrowArgumentNullException(nameof(buffer));
        """ + "\n";

    private const string JsonBufferRead = """
                        LuminPackInputPolicy.CheckInput(bufferWriter.GetSpan().Length, %LIMIT%);
                        try
                        {
                            var reader = new LuminPackJsonReader(bufferWriter);
                            if (!reader.Read<%PROFILE%>())
                                LuminPackExceptionHelper.ThrowFormatException("JSON input does not contain a value");
                            %READ%
                            reader.EnsureEndOfDocument<%PROFILE%>();
                            return reader.CurrentIndex;
                        }
                        finally
                        {
                            bufferWriter.ReaderState.ResetOperationState();
                        }
        """ + "\n";

    private const string RawRead = """
                        if (buffer.Length < Unsafe.SizeOf<%TYPE%>())
                            LuminPackExceptionHelper.ThrowSpanOutOfRange(Unsafe.SizeOf<%TYPE%>());
                        return LuminPackReader.ReadComplete(ref MemoryMarshal.GetReference(buffer), ref value);
        """ + "\n";

    private const string StatelessRead = """
        %ENUMCHECK%                var reader = new LuminPackReader(ref buffer);
                        %READ%
                        return reader.Position;
        """ + "\n";

    private const string EnumCheck = """
                        if (buffer.Length < Unsafe.SizeOf<%TYPE%>())
                            LuminPackExceptionHelper.ThrowSpanOutOfRange(Unsafe.SizeOf<%TYPE%>());
        """ + "\n";

    private const string RawWrite = """
                        var buffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(Unsafe.SizeOf<%TYPE%>());
                        LuminPackWriter.Write(ref MemoryMarshal.GetArrayDataReference(buffer), in value);
                        return buffer;
        """ + "\n";

    private const string RawListWrite = """
                        var list = value;
                        var items = list is null ? default(global::System.ReadOnlySpan<%ELEMENT%>) :
                            global::LuminPack.Code.LuminPackMarshal.GetListSpan(list);
                        int length = LuminPackWriter.SpanRecordBound<%ELEMENT%>(list is null, items.Length);
                        var buffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(length);
                        LuminPackWriter.WriteReservedSpan<%ELEMENT%>(ref MemoryMarshal.GetArrayDataReference(buffer), list is null, items);
                        return buffer;
        """ + "\n";

    private const string MeasureRecord = """
                        var directEvaluator = new global::LuminPack.Core.LuminPackEvaluator();
                        var directValue = value;
                        %MEASURE%(ref directEvaluator, ref directValue);
                        int directLength = directEvaluator.Value;
        """ + "\n";

    private const string DirectWrite = """
                        var directSpan = directBuffer.AsSpan();
                        var writer = new LuminPackWriter(ref directSpan);
                        %WRITE%
        """ + "\n";

    private const string MeasuredFallback = """
                        if (writer.Position == directLength)
                            return directBuffer;
                        var writerBuffer = LuminBufferWriterPool.Rent();
                        try
                        {
                            var fallbackWriter = new LuminPackWriter(writerBuffer);
                            %FALLBACKWRITE%
                            var buffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(fallbackWriter.CurrentIndex);
                            fallbackWriter.GetSpan().CopyTo(buffer.AsSpan());
                            return buffer;
                        }
                        finally
                        {
                            LuminBufferWriterPool.Return(writerBuffer);
                        }
        """ + "\n";

}

/// <summary>The published payload and shared formatter roots used by interceptors.</summary>
internal sealed class RootCatalog
{
    private readonly Dictionary<string, RootEntry> _entries;

    /// <summary>The shared root keys by their spelling-independent key.</summary>
    private readonly Dictionary<string, string> _canonical;
    private readonly string? _extension;
    private readonly string _assemblyProfile;
    private readonly string _assemblyJsonReadProfile;

    /// <summary>Indexes the published roots by payload identity.</summary>
    /// <param name="entries">The payload roots keyed by payload spelling.</param>
    /// <param name="shared">The readable names of the roots that shared owners publish.</param>
    /// <param name="extension">The assembly extension container of the shared owners.</param>
    /// <param name="assemblyProfile">The consuming assembly's compiled default profile.</param>
    /// <param name="assemblyJsonReadProfile">The default profile for JSON reads, including its grammar policy.</param>
    /// <param name="measured">The shared roots with exact record measurements.</param>
    internal RootCatalog(IEnumerable<KeyValuePair<string, RootEntry>> entries, IEnumerable<string> shared, string? extension,
        string assemblyProfile, string assemblyJsonReadProfile, IEnumerable<string>? measured = null)
    {
        Measured = new HashSet<string>(measured ?? Array.Empty<string>(), StringComparer.Ordinal);
        _extension = extension;
        _assemblyProfile = assemblyProfile;
        _assemblyJsonReadProfile = assemblyJsonReadProfile;
        var ordered = shared.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Shared = new HashSet<string>(ordered, StringComparer.Ordinal);
        _canonical = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in ordered)
            if (!_canonical.ContainsKey(Emitters.GeneratedNames.CanonicalKey(key)))
                _canonical.Add(Emitters.GeneratedNames.CanonicalKey(key), key);
        _entries = new Dictionary<string, RootEntry>(StringComparer.Ordinal);
        foreach (var entry in entries)
            if (!_entries.ContainsKey(entry.Key))
                _entries.Add(entry.Key, entry.Value);
    }

    /// <summary>The readable names of the roots that shared owners publish.</summary>
    private HashSet<string> Shared { get; }

    /// <summary>The readable names of the shared roots that publish their exact record measure.</summary>
    internal HashSet<string> Measured { get; }

    /// <summary>Finds the root a call binds to: the payload's own roots, or the shared owner of a non-packable closed root.</summary>
    /// <param name="call">The closed call.</param>
    /// <returns>The root, or null when the call stays on the generic facade.</returns>
    internal RootEntry? Find(SerializerCallModel call)
    {
        if (_entries.TryGetValue(call.Payload, out RootEntry? root))
            return root;
        // A closed payload binds its closed shared owner, which renders the closed members concretely, as the original binds a
        // closed root. Shared roots are published by payload key: two payloads whose readable names coincide must not bind each
        // other's owner.
        string key = Emitters.GeneratedNames.PayloadKey(call.Payload);
        // A call spells tuples and native integers with C# syntax; the owner requirement spells their metadata names.
        if (!Shared.Contains(key) && _canonical.TryGetValue(Emitters.GeneratedNames.CanonicalKey(call.Payload), out string? published))
            key = published;
        string readable = Emitters.ReadableTypeName.Of(key);
        // A generic Union has no closed owner; its closed uses dispatch through the generic root, as in the original.
        if (call.Definition != null && _entries.TryGetValue(call.Definition, out RootEntry? union) && union.GenericUnion)
            return new RootEntry(readable, union.DeclaredProfile, union.Binary, union.Json, limit: union.Limit,
                jsonReadProfile: union.JsonReadProfile, rootName: union.RootName, rootArguments: call.TypeArguments + ", ");
        if (call.Root != null && _extension != null && Shared.Contains(key))
            return new RootEntry(readable, _assemblyProfile, binary: true, json: true,
                owner: "global::LuminPack.Binary_" + _extension + "_" + readable, jsonReadProfile: _assemblyJsonReadProfile);
        // Without a closed owner, a closed generic payload calls its definition's generic roots from an entry named for the closed type.
        if (call.Definition != null && _entries.TryGetValue(call.Definition, out RootEntry? definition) && definition.Owner == null)
            return new RootEntry(readable, definition.DeclaredProfile, definition.Binary, definition.Json, limit: definition.Limit,
                jsonReadProfile: definition.JsonReadProfile, rootName: definition.RootName,
                rootArguments: call.TypeArguments + ", ");
        return null;
    }

}
