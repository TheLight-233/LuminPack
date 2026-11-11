using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Facade;

/// <summary>A concrete root published by Next that ordinary facade calls may bind to.</summary>
internal sealed class RootEntry : IEquatable<RootEntry>
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

    /// <summary>The payload's JSON encoding, used to bound character input: zero for UTF-8, one for UTF-16.</summary>
    internal readonly int Encoding;

    /// <summary>The strict profile that replaces the declared profile in JSON reads of an explicitly untrusted payload, or null.</summary>
    internal readonly string? JsonReadProfile;

    /// <summary>The root method stem, which differs from the entry name for a closed generic payload.</summary>
    internal readonly string RootName;

    /// <summary>The closed type arguments the generic roots take before the profile, each followed by ", ".</summary>
    internal readonly string RootArguments;

    /// <summary>Describes a published root.</summary>
    internal RootEntry(string readable, string declaredProfile, bool binary, bool json, string? owner = null, int? limit = null,
        int encoding = 1, string? jsonReadProfile = null, string? rootName = null, string rootArguments = "")
    {
        RootName = rootName ?? readable;
        RootArguments = rootArguments;
        Limit = limit;
        Encoding = encoding;
        JsonReadProfile = jsonReadProfile;
        Owner = owner;
        Readable = readable;
        DeclaredProfile = declaredProfile;
        Binary = binary;
        Json = json;
    }

    /// <inheritdoc/>
    public bool Equals(RootEntry? other) => other != null && Readable == other.Readable && DeclaredProfile == other.DeclaredProfile &&
        Binary == other.Binary && Json == other.Json && Owner == other.Owner && Limit == other.Limit && Encoding == other.Encoding &&
        JsonReadProfile == other.JsonReadProfile && RootName == other.RootName && RootArguments == other.RootArguments;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as RootEntry);

    /// <inheritdoc/>
    public override int GetHashCode() => Readable.GetHashCode();
}

/// <summary>Renders concrete serializer entries and the interceptors that bind ordinary facade calls to them.</summary>
/// <remarks>Each closed payload with calls receives one <c>LuminEntry_</c> owner whose bodies follow the established concrete entry
/// shapes: pooled buffers, caller-owned buffer writers, thread-static optional state and the default Reader validation. Each distinct
/// (overload, payload, profile) group receives one interceptor carrying every location that binds to it. Calls without a published
/// root are left on the generic facade, which keeps its registration-only fallback.</remarks>
internal static class InterceptorRenderer
{
    private const string Utf16 = "global::LuminPack.Option.LuminPackUtf16LengthOptions";

    /// <summary>Renders the interception file.</summary>
    /// <param name="calls">The discovered closed calls.</param>
    /// <param name="roots">The published roots keyed by payload spelling.</param>
    /// <returns>The source, or an empty string when no call binds to a root.</returns>
    internal static string Render(IReadOnlyList<SerializerCallModel> calls, RootCatalog roots)
    {
        var bound = new List<(SerializerCallModel Call, RootEntry Root)>();
        foreach (SerializerCallModel call in calls)
            if (roots.Find(call) is { } root && (call.Json ? root.Json : root.Binary))
                bound.Add((call, root));
        // Each root name binds one payload spelling; another spelling that reads the same is left on the generic facade.
        var claimed = new Dictionary<string, string>(StringComparer.Ordinal);
        bound = bound.Where(item => claimed.TryGetValue(item.Root.Readable, out string? payload)
            ? payload == item.Call.Payload : (claimed[item.Root.Readable] = item.Call.Payload) != null).ToList();
        if (bound.Count == 0)
            return string.Empty;
        var w = new StringBuilder(16384);
        w.Append(Header);
        // Concrete entries, one owner per payload.
        foreach (var payload in bound.GroupBy(item => item.Call.Payload, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            RootEntry root = payload.First().Root;
            string type = payload.Key;
            w.Append("        internal static partial class LuminEntry_").Append(root.Readable).Append('\n');
            w.Append("        {\n");
            if (payload.Any(item => !item.Call.Json))
                BinaryEntries(w, type, root);
            if (payload.Any(item => item.Call.Json))
                JsonEntries(w, type, root);
            w.Append("        }\n\n");
        }
        // Interceptors, one method per (overload, payload, profile).
        int index = 0;
        foreach (var group in bound.GroupBy(item => (item.Call.Api, item.Call.Payload, item.Call.Profile, item.Call.OpenProfile))
            .OrderBy(group => group.Key.Payload, StringComparer.Ordinal).ThenBy(group => group.Key.Api)
            .ThenBy(group => group.Key.Profile ?? string.Empty, StringComparer.Ordinal).ThenBy(group => group.Key.OpenProfile))
        {
            RootEntry root = group.First().Root;
            // An open profile is forwarded through a generic interceptor of the facade method's arity.
            // A JSON read of an explicitly untrusted payload replaces only its declared profile with the strict one.
            string profile = group.Key.OpenProfile ? "TLuminProfile" : group.Key.Profile ??
                (IsJsonRead(group.Key.Api) ? root.JsonReadProfile ?? root.DeclaredProfile : root.DeclaredProfile);
            foreach (string location in group.Select(item => item.Call.Location).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
                w.Append("        ").Append(location).Append('\n');
            Interceptor(w, group.Key.Api, group.Key.Payload, root.Readable, profile, index++, group.Key.OpenProfile);
            w.Append('\n');
        }
        w.Append("    }\n}\n");
        return w.ToString();
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

namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
    file sealed class InterceptsLocationAttribute : global::System.Attribute
    {
        public InterceptsLocationAttribute(int version, string data) { }
    }
}

namespace LuminPack
{
    public static partial class LuminPackSerializer
    {
";

    /// <summary>Whether an API reads a JSON document.</summary>
    /// <param name="api">The intercepted overload.</param>
    /// <returns>Whether the overload deserializes JSON.</returns>
    private static bool IsJsonRead(SerializerApi api) => api is SerializerApi.DeserializeJsonReader or SerializerApi.DeserializeJsonString or
        SerializerApi.DeserializeJsonBytes or SerializerApi.DeserializeJsonChars or SerializerApi.DeserializeJsonBuffer or
        SerializerApi.DeserializeJsonStringRef or SerializerApi.DeserializeJsonBytesRef or SerializerApi.DeserializeJsonCharsRef or
        SerializerApi.DeserializeJsonBufferRef;

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
            return text.Replace("%LIMIT%", "LuminPackSerializer.FormatterDispatch.LuminPackInputLimit<" + type + ">()")
                .Replace("%ENCODING%", "(int)LuminPackSerializer.FormatterDispatch.LuminPackJsonEncoding<" + type + ">()");
        if (root.Limit is not { } limit)
            return string.Join("\n", text.Split('\n').Where(line => line.IndexOf("%LIMIT%", StringComparison.Ordinal) < 0));
        return text.Replace("%LIMIT%", limit.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("%ENCODING%", root.Encoding.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void Interceptor(StringBuilder w, SerializerApi api, string type, string readable, string profile, int index,
        bool openProfile)
    {
        string entry = "LuminEntry_" + readable;
        string name = "LuminIntercept_" + readable + "_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            (openProfile ? "<T, TLuminProfile>" : string.Empty);
        string signature, call;
        switch (api)
        {
            case SerializerApi.SerializeStream:
                signature = "global::System.Threading.Tasks.ValueTask " + name + "(global::System.IO.Stream stream, " + type +
                    " value, global::System.Threading.CancellationToken cancellationToken)";
                call = entry + ".SerializeAsync<" + profile + ">(stream, value, cancellationToken)";
                break;
            case SerializerApi.DeserializeStream:
                signature = "global::System.Threading.Tasks.ValueTask<" + type + "> " + name +
                    "(global::System.IO.Stream stream, global::System.Threading.CancellationToken cancellationToken)";
                call = entry + ".DeserializeAsync<" + profile + ">(stream, cancellationToken)";
                break;
            case SerializerApi.DeserializeStreamValue:
                // The value argument is a compatibility parameter: the result is deserialized independently.
                signature = "global::System.Threading.Tasks.ValueTask<" + type + "> " + name + "(global::System.IO.Stream stream, " + type +
                    " value, global::System.Threading.CancellationToken cancellationToken)";
                call = entry + ".DeserializeAsync<" + profile + ">(stream, cancellationToken)";
                break;
            case SerializerApi.SerializeArray:
                signature = "byte[] " + name + "(in " + type + " value)";
                call = entry + ".Serialize<" + profile + ">(in value)";
                break;
            case SerializerApi.SerializeBufferWriter:
                signature = "void " + name + "(in " + type + " value, global::System.Buffers.IBufferWriter<byte> bufferWriter)";
                call = entry + ".Serialize<" + profile + ">(in value, bufferWriter)";
                break;
            case SerializerApi.SerializeBuffer:
                signature = "void " + name + "(in " + type + " value, LuminBufferWriter writerBuffer)";
                call = entry + ".Serialize<" + profile + ">(in value, writerBuffer)";
                break;
            case SerializerApi.DeserializeSpan:
                signature = type + " " + name + "(ReadOnlySpan<byte> buffer)";
                call = entry + ".Deserialize<" + profile + ">(buffer)";
                break;
            case SerializerApi.DeserializeSpanRef:
                signature = "int " + name + "(ReadOnlySpan<byte> buffer, ref " + type + " value)";
                call = entry + ".Deserialize<" + profile + ">(buffer, ref value)";
                break;
            case SerializerApi.DeserializeBuffer:
                signature = type + " " + name + "(LuminBufferWriter bufferWriter)";
                call = entry + ".Deserialize<" + profile + ">(bufferWriter)";
                break;
            case SerializerApi.DeserializeBufferRef:
                signature = "int " + name + "(LuminBufferWriter bufferWriter, ref " + type + " value)";
                call = entry + ".Deserialize<" + profile + ">(bufferWriter, ref value)";
                break;
            case SerializerApi.SerializeWriter:
                signature = "void " + name + "(ref LuminPackWriter state, scoped in " + type + " value)";
                call = entry + ".Serialize<" + profile + ">(ref state, in value)";
                break;
            case SerializerApi.DeserializeReader:
                signature = "void " + name + "(ref LuminPackReader state, scoped ref " + type + " value)";
                call = entry + ".Deserialize<" + profile + ">(ref state, ref value)";
                break;
            case SerializerApi.SerializeJsonWriter:
                signature = "void " + name + "(ref LuminPackJsonWriter state, scoped in " + type + " value)";
                call = entry + ".SerializeJson<" + profile + ">(ref state, in value)";
                break;
            case SerializerApi.DeserializeJsonReader:
                signature = "void " + name + "(ref LuminPackJsonReader state, scoped ref " + type + " value)";
                call = entry + ".DeserializeJson<" + profile + ">(ref state, ref value)";
                break;
            case SerializerApi.SerializeJsonString:
                signature = "string " + name + "(" + type + " value)";
                call = entry + ".SerializeJson<" + profile + ">(value)";
                break;
            case SerializerApi.SerializeJsonBufferWriter:
                signature = "void " + name + "(" + type + " value, global::System.Buffers.IBufferWriter<byte> bufferWriter)";
                call = entry + ".SerializeJson<" + profile + ">(value, bufferWriter)";
                break;
            case SerializerApi.SerializeJsonBuffer:
                signature = "void " + name + "(" + type + " value, LuminBufferWriter writerBuffer)";
                call = entry + ".SerializeJson<" + profile + ">(value, writerBuffer)";
                break;
            case SerializerApi.DeserializeJsonString:
                signature = type + " " + name + "(string buffer)";
                call = entry + ".DeserializeJson<" + profile + ">(buffer)";
                break;
            case SerializerApi.DeserializeJsonBytes:
                signature = type + " " + name + "(ReadOnlySpan<byte> buffer)";
                call = entry + ".DeserializeJson<" + profile + ">(buffer)";
                break;
            case SerializerApi.DeserializeJsonChars:
                signature = type + " " + name + "(ReadOnlySpan<char> buffer)";
                call = entry + ".DeserializeJson<" + profile + ">(buffer)";
                break;
            case SerializerApi.DeserializeJsonBuffer:
                signature = type + " " + name + "(LuminBufferWriter bufferWriter)";
                call = entry + ".DeserializeJson<" + profile + ">(bufferWriter)";
                break;
            case SerializerApi.DeserializeJsonStringRef:
                signature = "int " + name + "(string buffer, ref " + type + " value)";
                call = entry + ".DeserializeJson<" + profile + ">(buffer, ref value)";
                break;
            case SerializerApi.DeserializeJsonBytesRef:
                signature = "int " + name + "(ReadOnlySpan<byte> buffer, ref " + type + " value)";
                call = entry + ".DeserializeJson<" + profile + ">(buffer, ref value)";
                break;
            case SerializerApi.DeserializeJsonCharsRef:
                signature = "int " + name + "(ReadOnlySpan<char> buffer, ref " + type + " value)";
                call = entry + ".DeserializeJson<" + profile + ">(buffer, ref value)";
                break;
            case SerializerApi.DeserializeJsonBufferRef:
                signature = "int " + name + "(LuminBufferWriter bufferWriter, ref " + type + " value)";
                call = entry + ".DeserializeJson<" + profile + ">(bufferWriter, ref value)";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(api));
        }
        w.Append("        [MethodImpl(MethodImplOptions.AggressiveInlining)]\n");
        w.Append("        internal static ").Append(signature).Append('\n');
        if (openProfile)
            w.Append("            where TLuminProfile : struct, ILuminPackOptions\n");
        w.Append("            => ").Append(call).Append(";\n");
    }

    private static void BinaryEntries(StringBuilder w, string type, RootEntry root)
    {
        string write = root.Owner != null ? root.Owner + ".Write<TLuminProfile>(ref writer, in value);" :
            "LuminPackSerializer.Serialize_" + root.RootName + "<" + root.RootArguments + "TLuminProfile>(ref writer, in value);";
        string read = root.Owner != null ? root.Owner + ".Read<TLuminProfile>(ref reader, ref value);" :
            "LuminPackSerializer.Deserialize_" + root.RootName + "<" + root.RootArguments + "TLuminProfile>(ref reader, ref value);";
        string text = @"
            public static byte[] Serialize<TLuminProfile>(in %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
            {
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
            }

            public static void Serialize<TLuminProfile>(in %TYPE% value, global::System.Buffers.IBufferWriter<byte> bufferWriter)
                where TLuminProfile : struct, ILuminPackOptions
            {
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
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Serialize<TLuminProfile>(in %TYPE% value, LuminBufferWriter writerBuffer)
                where TLuminProfile : struct, ILuminPackOptions
            {
                var writer = new LuminPackWriter(writerBuffer);
                %WRITE%
                writerBuffer.CompleteWrite(writer.CurrentIndex);
            }

            public static int Deserialize<TLuminProfile>(ReadOnlySpan<byte> buffer, ref %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
            {
                LuminPackInputPolicy.CheckInput(buffer.Length, %LIMIT%);
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
            }

            public static %TYPE% Deserialize<TLuminProfile>(ReadOnlySpan<byte> buffer)
                where TLuminProfile : struct, ILuminPackOptions
            {
                %TYPE% value = default!;
                Deserialize<TLuminProfile>(buffer, ref value);
                return value;
            }

            public static int Deserialize<TLuminProfile>(LuminBufferWriter bufferWriter, ref %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
            {
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
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Serialize<TLuminProfile>(ref LuminPackWriter writer, scoped in %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
                => %WRITE%

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Deserialize<TLuminProfile>(ref LuminPackReader reader, scoped ref %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
                => %READ%

            public static %TYPE% Deserialize<TLuminProfile>(LuminBufferWriter bufferWriter)
                where TLuminProfile : struct, ILuminPackOptions
            {
                %TYPE% value = default!;
                Deserialize<TLuminProfile>(bufferWriter, ref value);
                return value;
            }

            public static async global::System.Threading.Tasks.ValueTask SerializeAsync<TLuminProfile>(global::System.IO.Stream stream, %TYPE% value, global::System.Threading.CancellationToken cancellationToken)
                where TLuminProfile : struct, ILuminPackOptions
            {
                var writerBuffer = LuminBufferWriterPool.Rent();
                try
                {
                    Serialize<TLuminProfile>(in value, writerBuffer);
                    await writerBuffer.WriteToAndResetAsync(stream, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    LuminBufferWriterPool.Return(writerBuffer);
                }
            }

            public static async global::System.Threading.Tasks.ValueTask<%TYPE%> DeserializeAsync<TLuminProfile>(global::System.IO.Stream stream, global::System.Threading.CancellationToken cancellationToken)
                where TLuminProfile : struct, ILuminPackOptions
            {
                using var data = await global::LuminPack.Utility.LuminPackStreamInput.ReadAsync(stream, %STREAMLIMIT%, cancellationToken).ConfigureAwait(false);
                return Deserialize<TLuminProfile>(data.Span);
            }
";
        w.Append(Limits(text.Replace("\r\n", "\n"), type, root).Replace("%TYPE%", type).Replace("%WRITE%", write).Replace("%READ%", read));
    }

    private static void JsonEntries(StringBuilder w, string type, RootEntry root)
    {
        string write = root.Owner != null ? root.Owner + ".Write<TLuminProfile>(ref writer, in value);" :
            "LuminPackSerializer.SerializeJson_" + root.RootName + "<" + root.RootArguments + "TLuminProfile>(ref writer, in value);";
        string read = root.Owner != null ? root.Owner + ".Read<TLuminProfile>(ref reader, ref value);" :
            "LuminPackSerializer.DeserializeJson_" + root.RootName + "<" + root.RootArguments + "TLuminProfile>(ref reader, ref value);";
        string text = @"
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static string SerializeJson<TLuminProfile>(%TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
            {
                var writerBuffer = LuminBufferWriterPool.Rent();
                try
                {
                    var writer = new LuminPackJsonWriter(writerBuffer);
                    %WRITE%
                    return default(TLuminProfile).StringEncoding is LuminPackStringEncoding.UTF8
                        ? Encoding.UTF8.GetString(writer.GetSpan())
                        : Encoding.Unicode.GetString(writer.GetSpan());
                }
                finally
                {
                    LuminBufferWriterPool.Return(writerBuffer);
                }
            }

            public static void SerializeJson<TLuminProfile>(%TYPE% value, global::System.Buffers.IBufferWriter<byte> bufferWriter)
                where TLuminProfile : struct, ILuminPackOptions
            {
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
            }

            public static void SerializeJson<TLuminProfile>(%TYPE% value, LuminBufferWriter writerBuffer)
                where TLuminProfile : struct, ILuminPackOptions
            {
                var writer = new LuminPackJsonWriter(writerBuffer);
                %WRITE%
                writerBuffer.CompleteWrite(writer.CurrentIndex);
            }

            public static int DeserializeJson<TLuminProfile>(ReadOnlySpan<byte> buffer, ref %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
            {
                LuminPackInputPolicy.CheckInput(buffer.Length, %LIMIT%);
                var state = LuminPackReaderOptionalState.Lease(ref _threadStaticReaderOptionalState);
                try
                {
                    var reader = new LuminPackJsonReader(ref buffer, state);
                    if (!reader.Read<TLuminProfile>())
                        LuminPackExceptionHelper.ThrowFormatException(""JSON input does not contain a value"");
                    %READ%
                    reader.EnsureEndOfDocument<TLuminProfile>();
                    return reader.CurrentIndex;
                }
                finally
                {
                    state.Release();
                }
            }

            public static int DeserializeJson<TLuminProfile>(ReadOnlySpan<char> buffer, ref %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
            {
                LuminPackInputPolicy.CheckCharacters(buffer, %ENCODING%, %LIMIT%);
                if (default(TLuminProfile).StringEncoding is LuminPackStringEncoding.UTF16)
                    return DeserializeJson<TLuminProfile>(MemoryMarshal.AsBytes(buffer), ref value);
                if (buffer.Length <= 1024)
                {
                    Span<byte> span = stackalloc byte[buffer.Length * 3];
                    int length = Encoding.UTF8.GetBytes(buffer, span);
                    return DeserializeJson<TLuminProfile>(span.Slice(0, length), ref value);
                }
                int byteCount = Encoding.UTF8.GetByteCount(buffer);
                byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
                try
                {
                    int written = Encoding.UTF8.GetBytes(buffer, rented);
                    return DeserializeJson<TLuminProfile>(rented.AsSpan(0, written), ref value);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }

            public static int DeserializeJson<TLuminProfile>(string buffer, ref %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
            {
                if (buffer is null)
                    LuminPackExceptionHelper.ThrowArgumentNullException(nameof(buffer));
                return DeserializeJson<TLuminProfile>(buffer.AsSpan(), ref value);
            }

            public static int DeserializeJson<TLuminProfile>(LuminBufferWriter bufferWriter, ref %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
            {
                LuminPackInputPolicy.CheckInput(bufferWriter.GetSpan().Length, %LIMIT%);
                try
                {
                    var reader = new LuminPackJsonReader(bufferWriter);
                    if (!reader.Read<TLuminProfile>())
                        LuminPackExceptionHelper.ThrowFormatException(""JSON input does not contain a value"");
                    %READ%
                    reader.EnsureEndOfDocument<TLuminProfile>();
                    return reader.CurrentIndex;
                }
                finally
                {
                    bufferWriter.ReaderState.ResetOperationState();
                }
            }

            public static %TYPE% DeserializeJson<TLuminProfile>(string buffer)
                where TLuminProfile : struct, ILuminPackOptions
            {
                %TYPE% value = default!;
                DeserializeJson<TLuminProfile>(buffer, ref value);
                return value;
            }

            public static %TYPE% DeserializeJson<TLuminProfile>(ReadOnlySpan<byte> buffer)
                where TLuminProfile : struct, ILuminPackOptions
            {
                %TYPE% value = default!;
                DeserializeJson<TLuminProfile>(buffer, ref value);
                return value;
            }

            public static %TYPE% DeserializeJson<TLuminProfile>(ReadOnlySpan<char> buffer)
                where TLuminProfile : struct, ILuminPackOptions
            {
                %TYPE% value = default!;
                DeserializeJson<TLuminProfile>(buffer, ref value);
                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void SerializeJson<TLuminProfile>(ref LuminPackJsonWriter writer, scoped in %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
                => %WRITE%

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void DeserializeJson<TLuminProfile>(ref LuminPackJsonReader reader, scoped ref %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
                => %READ%

            public static %TYPE% DeserializeJson<TLuminProfile>(LuminBufferWriter bufferWriter)
                where TLuminProfile : struct, ILuminPackOptions
            {
                %TYPE% value = default!;
                DeserializeJson<TLuminProfile>(bufferWriter, ref value);
                return value;
            }
";
        w.Append(Limits(text.Replace("\r\n", "\n"), type, root).Replace("%TYPE%", type).Replace("%WRITE%", write).Replace("%READ%", read));
    }
}

/// <summary>The value-equal set of published roots, keyed by payload spelling.</summary>
internal sealed class RootCatalog : IEquatable<RootCatalog>
{
    private readonly KeyValuePair<string, RootEntry>[] _entries;

    private readonly string[] _shared;
    private readonly string? _extension;

    /// <summary>Captures the roots in ordinal key order.</summary>
    /// <param name="entries">The payload roots keyed by payload spelling.</param>
    /// <param name="shared">The readable names of the roots that shared owners publish.</param>
    /// <param name="extension">The assembly extension container of the shared owners.</param>
    internal RootCatalog(IEnumerable<KeyValuePair<string, RootEntry>> entries, IEnumerable<string> shared, string? extension)
    {
        _extension = extension;
        _shared = shared.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Shared = new HashSet<string>(_shared, StringComparer.Ordinal);
        _entries = entries.GroupBy(pair => pair.Key, StringComparer.Ordinal).Select(group => group.First())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        Map = _entries.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    /// <summary>The roots keyed by payload spelling.</summary>
    internal IReadOnlyDictionary<string, RootEntry> Map { get; }

    /// <summary>The readable names of the roots that shared owners publish.</summary>
    internal HashSet<string> Shared { get; }

    /// <summary>Finds the root a call binds to: the payload's own roots, or the shared owner of a non-packable closed root.</summary>
    /// <param name="call">The closed call.</param>
    /// <returns>The root, or null when the call stays on the generic facade.</returns>
    internal RootEntry? Find(SerializerCallModel call)
    {
        if (Map.TryGetValue(call.Payload, out RootEntry? root))
            return root;
        // A closed generic payload calls its definition's generic roots from an entry named for the closed type.
        if (call.Definition != null && Map.TryGetValue(call.Definition, out RootEntry? definition) && definition.Owner == null)
            return new RootEntry(Emitters.ReadableTypeName.Of(Emitters.GeneratedNames.PayloadKey(call.Payload)), definition.DeclaredProfile,
                definition.Binary, definition.Json, limit: definition.Limit, encoding: definition.Encoding,
                jsonReadProfile: definition.JsonReadProfile, rootName: definition.RootName, rootArguments: call.TypeArguments + ", ");
        if (call.Root == null)
            return null;
        // Shared roots are published by payload key: two payloads whose readable names coincide must not bind each other's owner.
        string key = Emitters.GeneratedNames.PayloadKey(call.Payload);
        string readable = Emitters.ReadableTypeName.Of(key);
        return _extension != null && Shared.Contains(key) ? new RootEntry(readable, RootEntry.DefaultProfile, binary: true,
            json: true, owner: "global::LuminPack.Binary_" + _extension + "_" + readable) : null;
    }

    /// <inheritdoc/>
    public bool Equals(RootCatalog? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other == null || other._entries.Length != _entries.Length || _extension != other._extension ||
            !_shared.SequenceEqual(other._shared, StringComparer.Ordinal))
            return false;
        for (int index = 0; index < _entries.Length; index++)
            if (_entries[index].Key != other._entries[index].Key || !_entries[index].Value.Equals(other._entries[index].Value))
                return false;
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as RootCatalog);

    /// <inheritdoc/>
    public override int GetHashCode() => _entries.Length;
}
