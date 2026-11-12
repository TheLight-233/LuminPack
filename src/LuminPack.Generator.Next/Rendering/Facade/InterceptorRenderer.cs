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

    /// <summary>Whether the entry is a generic Union definition, whose closed uses always bind its generic roots.</summary>
    internal readonly bool GenericUnion;

    /// <summary>The root method stem, which differs from the entry name for a closed generic payload.</summary>
    internal readonly string RootName;

    /// <summary>The closed type arguments the generic roots take before the profile, each followed by ", ".</summary>
    internal readonly string RootArguments;

    /// <summary>Whether the payload's own roots publish its exact record measure.</summary>
    internal readonly bool Measured;

    /// <summary>Describes a published root.</summary>
    internal RootEntry(string readable, string declaredProfile, bool binary, bool json, string? owner = null, int? limit = null,
        int encoding = 1, string? jsonReadProfile = null, string? rootName = null, string rootArguments = "", bool genericUnion = false, bool measured = false)
    {
        Measured = measured;
        GenericUnion = genericUnion;
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

    /// <summary>Copies the entry under another entry name; the roots it calls keep their names.</summary>
    /// <param name="readable">The entry name.</param>
    /// <returns>The renamed entry.</returns>
    internal RootEntry WithReadable(string readable) => new(readable, DeclaredProfile, Binary, Json, Owner, Limit, Encoding, JsonReadProfile,
        RootName, RootArguments, GenericUnion, Measured);

    /// <inheritdoc/>
    public bool Equals(RootEntry? other) => other != null && Readable == other.Readable && DeclaredProfile == other.DeclaredProfile &&
        Binary == other.Binary && Json == other.Json && Owner == other.Owner && Limit == other.Limit && Encoding == other.Encoding &&
        JsonReadProfile == other.JsonReadProfile && RootName == other.RootName && RootArguments == other.RootArguments && Measured == other.Measured;

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
    /// <param name="il">Whether the calls bind through the installed IL rewriter rather than compiler interception.</param>
    /// <returns>The source, or an empty string when no call binds to a root.</returns>
    /// <param name="wrappers">The closed copies of source generic methods, whose body calls are among <paramref name="calls"/>.</param>
    internal static string Render(IReadOnlyList<SerializerCallModel> calls, RootCatalog roots, bool il = false, WrapperSet? wrappers = null)
    {
        wrappers ??= WrapperSet.Empty;
        var bound = new List<(SerializerCallModel Call, RootEntry Root)>();
        foreach (SerializerCallModel call in calls)
            if (roots.Find(call) is { } root && (call.Json ? root.Json : root.Binary))
                bound.Add((call, root));
        // Payloads whose readable names coincide, such as two generic definitions of the same name, keep one entry each: the
        // first payload in ordinal order keeps the name and the others are numbered, as the original numbered its intercepts.
        // Their roots resolve by the payload type, so only the entry names change. Spellings of the same type, such as tuple
        // element names, bind the same entry.
        var numbered = new Dictionary<string, RootEntry>(StringComparer.Ordinal);
        foreach (var name in bound.GroupBy(item => item.Root.Readable, StringComparer.Ordinal))
        {
            int number = 1;
            foreach (string payload in name.Select(item => Emitters.GeneratedNames.CanonicalKey(item.Call.Payload)).Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal))
            {
                RootEntry root = name.First(item => Emitters.GeneratedNames.CanonicalKey(item.Call.Payload) == payload).Root;
                numbered[payload] = number == 1 ? root : root.WithReadable(root.Readable + number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                number++;
            }
        }
        bound = bound.Select(item => (item.Call, numbered[Emitters.GeneratedNames.CanonicalKey(item.Call.Payload)])).ToList();
        if (bound.Count == 0 && wrappers.Wrappers.Length == 0)
            return string.Empty;
        var w = new StringBuilder(16384);
        w.Append(Header);
        // The installed IL rewriter maps each interceptor through a build-only attribute instead of a compiler location.
        w.Append(il ? StaticCall : InterceptsLocation).Append(FacadeOpen);
        // Concrete entries, one owner per payload, spelled as its first call in ordinal order spells it.
        foreach (var payload in bound.GroupBy(item => item.Root.Readable, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            RootEntry root = payload.First().Root;
            string type = payload.Select(item => item.Call.Payload).OrderBy(item => item, StringComparer.Ordinal).First();
            w.Append("        internal static partial class LuminEntry_").Append(root.Readable).Append('\n');
            w.Append("        {\n");
            // An entry keeps only the directions its calls use, as the payload keeps only the called operations.
            bool binaryWrites = false, binaryReads = false, jsonWrites = false, jsonReads = false;
            foreach (var item in payload)
            {
                ref bool used = ref item.Call.Json ? ref item.Call.Writes ? ref jsonWrites : ref jsonReads
                    : ref item.Call.Writes ? ref binaryWrites : ref binaryReads;
                used = true;
            }
            if (binaryWrites || binaryReads)
                BinaryEntries(w, type, root, payload.First().Call.Shape, root.Owner == null ? root.Measured : roots.Measured.Contains(root.RootName),
                    binaryWrites, binaryReads);
            if (jsonWrites || jsonReads)
                JsonEntries(w, type, root, jsonWrites, jsonReads);
            w.Append("        }\n\n");
        }
        // Interceptors, one method per (overload, payload, profile). A wrapper body calls the method of its call's key.
        int index = 0;
        var shims = new Dictionary<(SerializerApi, string, string?, bool), string>();
        foreach (var group in bound.GroupBy(item => (item.Call.Api, item.Call.Payload, item.Call.Profile, item.Call.OpenProfile))
            .OrderBy(group => group.Key.Payload, StringComparer.Ordinal).ThenBy(group => group.Key.Api)
            .ThenBy(group => group.Key.Profile ?? string.Empty, StringComparer.Ordinal).ThenBy(group => group.Key.OpenProfile))
        {
            RootEntry root = group.First().Root;
            // An open profile is forwarded through a generic interceptor of the facade method's arity.
            // A JSON read of an explicitly untrusted payload replaces only its declared profile with the strict one.
            string profile = group.Key.OpenProfile ? "TLuminProfile" : group.Key.Profile ??
                (IsJsonRead(group.Key.Api) ? root.JsonReadProfile ?? root.DeclaredProfile : root.DeclaredProfile);
            // A call of a wrapper body has no source location.
            foreach (string location in group.Select(item => item.Call.Location).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
                if (location.Length != 0)
                    w.Append("        ").Append(location).Append('\n');
            shims[group.Key] = "LuminIntercept_" + root.Readable + "_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Interceptor(w, group.Key.Api, group.Key.Payload, root.Readable, profile, index++, group.Key.OpenProfile);
            w.Append('\n');
        }
        Wrappers(w, wrappers, shims);
        w.Append("    }\n}\n");
        return w.ToString();
    }

    /// <summary>Writes each wrapper with the source calls it replaces, its body calls bound to their entry shims.</summary>
    /// <param name="w">The interception source.</param>
    /// <param name="wrappers">The wrappers.</param>
    /// <param name="shims">The interceptor method of each bound call key.</param>
    private static void Wrappers(StringBuilder w, WrapperSet wrappers, Dictionary<(SerializerApi, string, string?, bool), string> shims)
    {
        foreach (WrapperModel wrapper in wrappers.Wrappers)
        {
            string body = wrapper.Body;
            // Higher placeholders first, so __LuminCall_1 never rewrites the prefix of __LuminCall_10.
            for (int index = wrapper.Calls.Length - 1; index >= 0; index--)
            {
                SerializerCallModel call = wrapper.Calls[index];
                string target = shims.TryGetValue((call.Api, call.Payload, call.Profile, call.OpenProfile), out string? shim)
                    ? shim : wrapper.Fallbacks[index];
                body = body.Replace(WrapperModel.Placeholder(index), target);
            }
            foreach (string location in wrapper.Locations)
                w.Append("        ").Append(location).Append('\n');
            w.Append("        ").Append(wrapper.Declaration).Append('\n');
            foreach (string line in body.Split('\n'))
                w.Append("        ").Append(line.TrimEnd('\r')).Append('\n');
            w.Append('\n');
        }
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
            case SerializerApi.DeserializeSequenceRef:
                signature = "int " + name + "(global::System.Buffers.ReadOnlySequence<byte> buffer, ref " + type + " value)";
                call = entry + ".Deserialize<" + profile + ">(buffer, ref value)";
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

    private static void BinaryEntries(StringBuilder w, string type, RootEntry root, EntryShape shape, bool measured, bool writes, bool reads)
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

            public static int Deserialize<TLuminProfile>(global::System.Buffers.ReadOnlySequence<byte> buffer, ref %TYPE% value)
                where TLuminProfile : struct, ILuminPackOptions
            {
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
        text = Directions(Specialize(text.Replace("\r\n", "\n"), root, shape, measured, write), writes, reads);
        w.Append(Limits(text, type, root).Replace("%TYPE%", type).Replace("%WRITE%", write).Replace("%READ%", read));
    }

    /// <summary>Replaces the allocating serialize entry and the span read entry with the original specialized forms.</summary>
    /// <param name="text">The binary entry template.</param>
    /// <param name="root">The published root.</param>
    /// <param name="shape">The payload's entry form.</param>
    /// <param name="measured">Whether the root publishes its exact record measure.</param>
    /// <param name="write">The root write statement on <c>writer</c>.</param>
    /// <returns>The template with the selected forms.</returns>
    /// <remarks>Raw scalars copy their storage into an exact array and read it with one checked copy. Records of fixed or
    /// measurable width write directly into an exact array, with pooled output only when a measured width was not exact, and
    /// read through a plain reader that leases no reference-tracking state. Every other payload keeps pooled output.</remarks>
    private static string Specialize(string text, RootEntry root, EntryShape shape, bool measured, string write)
    {
        string? serialize = null, deserialize = null;
        const string Inline = "            [MethodImpl(MethodImplOptions.AggressiveInlining)]\n";
        const string Signature = "            public static byte[] Serialize<TLuminProfile>(in %TYPE% value)\n" +
            "                where TLuminProfile : struct, ILuminPackOptions\n";
        bool needsMeasure = shape.Kind is EntryKind.FixedRecord or EntryKind.DirectRecord;
        string measure = "LuminPackSerializer.MeasureRecord_" + root.RootName + "<" + root.RootArguments + "TLuminProfile>(ref directEvaluator, ref directValue);";
        switch (shape.Kind)
        {
            case EntryKind.RawScalar:
                serialize = Inline + Signature + "            {\n" +
                    "                var buffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(Unsafe.SizeOf<%TYPE%>());\n" +
                    "                LuminPackWriter.Write(ref MemoryMarshal.GetArrayDataReference(buffer), in value);\n" +
                    "                return buffer;\n            }\n";
                deserialize = Inline + "            public static int Deserialize<TLuminProfile>(ReadOnlySpan<byte> buffer, ref %TYPE% value)\n" +
                    "                where TLuminProfile : struct, ILuminPackOptions\n            {\n" +
                    "                LuminPackInputPolicy.CheckInput(buffer.Length, %LIMIT%);\n" +
                    "                if (buffer.Length < Unsafe.SizeOf<%TYPE%>())\n" +
                    "                    LuminPackExceptionHelper.ThrowSpanOutOfRange(Unsafe.SizeOf<%TYPE%>());\n" +
                    "                return LuminPackReader.ReadComplete(ref MemoryMarshal.GetReference(buffer), ref value);\n            }\n";
                break;
            case EntryKind.BigInteger:
                serialize = Inline + Signature + "            {\n" +
                    "                int byteCount = value.GetByteCount();\n" +
                    "                var buffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(checked(sizeof(int) + byteCount));\n" +
                    "                LuminPackWriter.Write(ref MemoryMarshal.GetArrayDataReference(buffer), byteCount);\n" +
                    "                value.TryWriteBytes(buffer.AsSpan(sizeof(int)), out _);\n" +
                    "                return buffer;\n            }\n";
                break;
            case EntryKind.FixedEnum or EntryKind.NullableDirect or EntryKind.FixedRecord or EntryKind.RawSequence or EntryKind.DirectRecord
                when !needsMeasure || measured:
            {
                bool exact = shape.Kind is EntryKind.FixedEnum or EntryKind.NullableDirect or EntryKind.FixedRecord;
                string length = shape.Kind switch
                {
                    EntryKind.FixedEnum => "                int directLength = Unsafe.SizeOf<%TYPE%>();\n",
                    EntryKind.NullableDirect => "                int directLength = value.HasValue ? 1 + Unsafe.SizeOf<" + shape.Element + ">() : 1;\n",
                    EntryKind.RawSequence => "                int directLength = checked(sizeof(int) + checked((int)value.Length) * Unsafe.SizeOf<" + shape.Element + ">());\n",
                    _ => "                var directEvaluator = new global::LuminPack.Core.LuminPackEvaluator();\n" +
                        "                var directValue = value;\n                " + measure + "\n" +
                        "                int directLength = directEvaluator.Value;\n",
                };
                var body = new StringBuilder();
                body.Append(Inline).Append(Signature).Append("            {\n").Append(length);
                // A fixed record is a few primitives wide; a plain array lets the JIT drop the large-array branch and null check.
                body.Append(exact ? "                var directBuffer = new byte[directLength];\n"
                    : "                var directBuffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(directLength);\n");
                body.Append("                var directSpan = directBuffer.AsSpan();\n");
                body.Append("                var writer = new LuminPackWriter(ref directSpan);\n");
                body.Append("                ").Append(write).Append('\n');
                if (exact)
                    body.Append("                return directBuffer;\n            }\n");
                else
                {
                    body.Append("                if (writer.Position == directLength) return directBuffer;\n");
                    body.Append("                var writerBuffer = LuminBufferWriterPool.Rent();\n                try\n                {\n");
                    body.Append("                    var fallbackWriter = new LuminPackWriter(writerBuffer);\n");
                    body.Append("                    ").Append(write.Replace("ref writer,", "ref fallbackWriter,")).Append('\n');
                    body.Append("                    var buffer = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<byte>(fallbackWriter.CurrentIndex);\n");
                    body.Append("                    fallbackWriter.GetSpan().CopyTo(buffer.AsSpan());\n                    return buffer;\n");
                    body.Append("                }\n                finally\n                {\n                    LuminBufferWriterPool.Return(writerBuffer);\n");
                    body.Append("                }\n            }\n");
                }
                serialize = body.ToString();
                break;
            }
        }
        if (deserialize == null && shape.StatelessRead && (!needsMeasure || measured))
            deserialize = "            public static int Deserialize<TLuminProfile>(ReadOnlySpan<byte> buffer, ref %TYPE% value)\n" +
                "                where TLuminProfile : struct, ILuminPackOptions\n            {\n" +
                "                LuminPackInputPolicy.CheckInput(buffer.Length, %LIMIT%);\n" +
                (shape.Kind == EntryKind.FixedEnum ? "                if (buffer.Length < Unsafe.SizeOf<%TYPE%>())\n" +
                    "                    LuminPackExceptionHelper.ThrowSpanOutOfRange(Unsafe.SizeOf<%TYPE%>());\n" : string.Empty) +
                "                var reader = new LuminPackReader(ref buffer);\n                %READ%\n                return reader.Position;\n            }\n";
        if (serialize != null)
            text = ReplaceMethod(text, "            public static byte[] Serialize<TLuminProfile>(in %TYPE% value)\n", serialize);
        if (deserialize != null)
            text = ReplaceMethod(text, "            public static int Deserialize<TLuminProfile>(ReadOnlySpan<byte> buffer, ref %TYPE% value)\n", deserialize);
        return text;
    }

    /// <summary>Replaces one template method, from its signature line to its closing brace.</summary>
    private static string ReplaceMethod(string text, string signature, string replacement)
    {
        int start = text.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0)
            return text;
        const string Close = "\n            }\n";
        int end = text.IndexOf(Close, start, StringComparison.Ordinal);
        return text.Substring(0, start) + replacement + text.Substring(end + Close.Length);
    }

    private static void JsonEntries(StringBuilder w, string type, RootEntry root, bool writes, bool reads)
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
        text = Directions(text.Replace("\r\n", "\n"), writes, reads);
        w.Append(Limits(text, type, root).Replace("%TYPE%", type).Replace("%WRITE%", write).Replace("%READ%", read));
    }

    /// <summary>Keeps the entry methods of the called directions.</summary>
    /// <param name="text">The entry template, whose members start at the member indentation after a blank line.</param>
    /// <param name="writes">Whether a call serializes.</param>
    /// <param name="reads">Whether a call deserializes.</param>
    /// <returns>The template without the methods of an uncalled direction.</returns>
    /// <remarks>A method belongs to the direction its name starts with, as the original operation filter classified facade
    /// members; an entry method only forwards to methods of its own direction.</remarks>
    private static string Directions(string text, bool writes, bool reads)
    {
        if (writes && reads)
            return text;
        var kept = new StringBuilder(text.Length);
        int start = 0;
        while (start < text.Length)
        {
            // A blank line inside a body is followed by deeper indentation; only a member line ends the current member.
            int end = start;
            do
            {
                end = text.IndexOf("\n\n", end, StringComparison.Ordinal);
                end = end < 0 ? text.Length : end + 2;
            }
            while (end < text.Length && !MemberStart(text, end));
            bool keep = true;
            int signature = text.IndexOf("public static ", start, end - start, StringComparison.Ordinal);
            int open = signature < 0 ? -1 : text.IndexOf('(', signature, end - signature);
            if (open > 0)
            {
                // The method name precedes its type argument list.
                int name = open;
                if (text[name - 1] == '>')
                    name = text.LastIndexOf('<', name - 1, name - signature);
                int first = name;
                while (first > signature && (char.IsLetterOrDigit(text[first - 1]) || text[first - 1] == '_'))
                    first--;
                bool deserializes = string.CompareOrdinal(text, first, "Deserialize", 0, 11) == 0;
                bool serializes = !deserializes && string.CompareOrdinal(text, first, "Serialize", 0, 9) == 0;
                keep = serializes ? writes : !deserializes || reads;
            }
            if (keep)
                kept.Append(text, start, end - start);
            start = end;
        }
        return kept.ToString();
    }

    /// <summary>Whether a line starts an entry member: an attribute or a declaration at the member indentation.</summary>
    /// <param name="text">The entry template.</param>
    /// <param name="line">The line start.</param>
    /// <returns>Whether the line starts a member.</returns>
    private static bool MemberStart(string text, int line)
    {
        const int Indent = 12;
        if (line + Indent >= text.Length)
            return false;
        for (int index = line; index < line + Indent; index++)
            if (text[index] != ' ')
                return false;
        char first = text[line + Indent];
        return first is '[' or 'p' or 'i';
    }
}

/// <summary>The value-equal set of published roots, keyed by payload spelling.</summary>
internal sealed class RootCatalog : IEquatable<RootCatalog>
{
    private readonly KeyValuePair<string, RootEntry>[] _entries;

    private readonly string[] _shared;

    /// <summary>The shared root keys by their spelling-independent key.</summary>
    private readonly Dictionary<string, string> _canonical;
    private readonly string? _extension;

    /// <summary>Captures the roots in ordinal key order.</summary>
    /// <param name="entries">The payload roots keyed by payload spelling.</param>
    /// <param name="shared">The readable names of the roots that shared owners publish.</param>
    /// <param name="extension">The assembly extension container of the shared owners.</param>
    internal RootCatalog(IEnumerable<KeyValuePair<string, RootEntry>> entries, IEnumerable<string> shared, string? extension,
        IEnumerable<string>? measured = null)
    {
        Measured = new HashSet<string>(measured ?? Array.Empty<string>(), StringComparer.Ordinal);
        _extension = extension;
        _shared = shared.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Shared = new HashSet<string>(_shared, StringComparer.Ordinal);
        _canonical = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in _shared)
            if (!_canonical.ContainsKey(Emitters.GeneratedNames.CanonicalKey(key)))
                _canonical.Add(Emitters.GeneratedNames.CanonicalKey(key), key);
        _entries = entries.GroupBy(pair => pair.Key, StringComparer.Ordinal).Select(group => group.First())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        Map = _entries.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    /// <summary>The roots keyed by payload spelling.</summary>
    internal IReadOnlyDictionary<string, RootEntry> Map { get; }

    /// <summary>The readable names of the roots that shared owners publish.</summary>
    internal HashSet<string> Shared { get; }

    /// <summary>The readable names of the shared roots that publish their exact record measure.</summary>
    internal HashSet<string> Measured { get; }

    /// <summary>Finds the root a call binds to: the payload's own roots, or the shared owner of a non-packable closed root.</summary>
    /// <param name="call">The closed call.</param>
    /// <returns>The root, or null when the call stays on the generic facade.</returns>
    internal RootEntry? Find(SerializerCallModel call)
    {
        if (Map.TryGetValue(call.Payload, out RootEntry? root))
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
        if (call.Definition != null && Map.TryGetValue(call.Definition, out RootEntry? union) && union.GenericUnion)
            return new RootEntry(readable, union.DeclaredProfile, union.Binary, union.Json, limit: union.Limit, encoding: union.Encoding,
                jsonReadProfile: union.JsonReadProfile, rootName: union.RootName, rootArguments: call.TypeArguments + ", ");
        if (call.Root != null && _extension != null && Shared.Contains(key))
            return new RootEntry(readable, RootEntry.DefaultProfile, binary: true, json: true, owner: "global::LuminPack.Binary_" + _extension + "_" + readable);
        // Without a closed owner, a closed generic payload calls its definition's generic roots from an entry named for the closed type.
        if (call.Definition != null && Map.TryGetValue(call.Definition, out RootEntry? definition) && definition.Owner == null)
            return new RootEntry(readable, definition.DeclaredProfile, definition.Binary, definition.Json, limit: definition.Limit,
                encoding: definition.Encoding, jsonReadProfile: definition.JsonReadProfile, rootName: definition.RootName,
                rootArguments: call.TypeArguments + ", ");
        return null;
    }

    /// <inheritdoc/>
    public bool Equals(RootCatalog? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other == null || other._entries.Length != _entries.Length || _extension != other._extension ||
            !_shared.SequenceEqual(other._shared, StringComparer.Ordinal) || !Measured.SetEquals(other.Measured))
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
