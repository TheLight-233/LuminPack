using System;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>A public <c>LuminPackSerializer</c> overload that a closed call can bind to a concrete root.</summary>
internal enum SerializerApi
{
    /// <summary><c>ValueTask SerializeAsync&lt;T&gt;(Stream, T, CancellationToken)</c>.</summary>
    SerializeStream,

    /// <summary><c>ValueTask&lt;T&gt; DeserializeAsync&lt;T&gt;(Stream, CancellationToken)</c>.</summary>
    DeserializeStream,

    /// <summary><c>ValueTask&lt;T&gt; DeserializeAsync&lt;T&gt;(Stream, T, CancellationToken)</c>.</summary>
    DeserializeStreamValue,

    /// <summary><c>byte[] Serialize&lt;T&gt;(in T)</c>.</summary>
    SerializeArray,

    /// <summary><c>void Serialize&lt;T&gt;(in T, IBufferWriter&lt;byte&gt;)</c>.</summary>
    SerializeBufferWriter,

    /// <summary><c>void Serialize&lt;T&gt;(in T, LuminBufferWriter)</c>.</summary>
    SerializeBuffer,

    /// <summary><c>T Deserialize&lt;T&gt;(ReadOnlySpan&lt;byte&gt;)</c>.</summary>
    DeserializeSpan,

    /// <summary><c>int Deserialize&lt;T&gt;(ReadOnlySpan&lt;byte&gt;, ref T)</c>.</summary>
    DeserializeSpanRef,

    /// <summary><c>T Deserialize&lt;T&gt;(LuminBufferWriter)</c>.</summary>
    DeserializeBuffer,

    /// <summary><c>int Deserialize&lt;T&gt;(LuminBufferWriter, ref T)</c>.</summary>
    DeserializeBufferRef,

    /// <summary><c>int Deserialize&lt;T&gt;(ReadOnlySequence&lt;byte&gt;, ref T)</c>.</summary>
    DeserializeSequenceRef,

    /// <summary><c>void Serialize&lt;T&gt;(ref LuminPackWriter, scoped in T)</c>.</summary>
    SerializeWriter,

    /// <summary><c>void Deserialize&lt;T&gt;(ref LuminPackReader, scoped ref T)</c>.</summary>
    DeserializeReader,

    /// <summary><c>string SerializeJson&lt;T&gt;(T)</c>.</summary>
    SerializeJsonString,

    /// <summary><c>void SerializeJson&lt;T&gt;(T, IBufferWriter&lt;byte&gt;)</c>.</summary>
    SerializeJsonBufferWriter,

    /// <summary><c>void SerializeJson&lt;T&gt;(T, LuminBufferWriter)</c>.</summary>
    SerializeJsonBuffer,

    /// <summary><c>T DeserializeJson&lt;T&gt;(string)</c>.</summary>
    DeserializeJsonString,

    /// <summary><c>T DeserializeJson&lt;T&gt;(ReadOnlySpan&lt;byte&gt;)</c>.</summary>
    DeserializeJsonBytes,

    /// <summary><c>T DeserializeJson&lt;T&gt;(ReadOnlySpan&lt;char&gt;)</c>.</summary>
    DeserializeJsonChars,

    /// <summary><c>T DeserializeJson&lt;T&gt;(LuminBufferWriter)</c>.</summary>
    DeserializeJsonBuffer,

    /// <summary><c>int DeserializeJson&lt;T&gt;(string, ref T)</c>.</summary>
    DeserializeJsonStringRef,

    /// <summary><c>int DeserializeJson&lt;T&gt;(ReadOnlySpan&lt;byte&gt;, ref T)</c>.</summary>
    DeserializeJsonBytesRef,

    /// <summary><c>int DeserializeJson&lt;T&gt;(ReadOnlySpan&lt;char&gt;, ref T)</c>.</summary>
    DeserializeJsonCharsRef,

    /// <summary><c>int DeserializeJson&lt;T&gt;(LuminBufferWriter, ref T)</c>.</summary>
    DeserializeJsonBufferRef,

    /// <summary><c>void SerializeJson&lt;T&gt;(ref LuminPackJsonWriter, scoped in T)</c>.</summary>
    SerializeJsonWriter,

    /// <summary><c>void DeserializeJson&lt;T&gt;(ref LuminPackJsonReader, scoped ref T)</c>.</summary>
    DeserializeJsonReader,
}

/// <summary>One closed serializer call site: the bound overload, its closed payload and profile, and the interception location.</summary>
/// <remarks>The model is compiler-free and value-equal, so an edit that leaves a call's binding unchanged keeps its cached
/// interceptor. The location text changes with the file's content and is part of equality.</remarks>
/// <summary>The allocating-entry form the original concrete facade selected for a closed payload.</summary>
internal enum EntryKind
{
    /// <summary>Pooled output and thread-static reader state.</summary>
    Pooled,

    /// <summary>A known reference-free scalar copied as its complete storage.</summary>
    RawScalar,

    /// <summary>A record of fixed-width primitive members, measured and written into an exact array.</summary>
    FixedRecord,

    /// <summary>A record measured into an exact array, with pooled output when the measure was not exact.</summary>
    DirectRecord,

    /// <summary>An enum written at its native width.</summary>
    FixedEnum,

    /// <summary>A nullable known scalar: a marker byte and the value.</summary>
    NullableDirect,

    /// <summary>A sequence of known scalars, sized by its length.</summary>
    RawSequence,

    /// <summary>A count-prefixed two's-complement big integer.</summary>
    BigInteger,

    /// <summary>An interface sequence of known scalars, read without reference-tracking state.</summary>
    StatelessRead,
}

/// <summary>The entry form of a closed payload and its element spelling where the form needs one.</summary>
internal readonly struct EntryShape : IEquatable<EntryShape>
{
    internal static readonly EntryShape Pooled = new(EntryKind.Pooled);

    internal readonly EntryKind Kind;

    internal readonly string? Element;

    internal EntryShape(EntryKind kind, string? element = null)
    {
        Kind = kind;
        Element = element;
    }

    /// <summary>Whether the binary reads construct a plain reader without leasing reference-tracking state.</summary>
    internal bool StatelessRead => Kind is EntryKind.FixedRecord or EntryKind.DirectRecord or EntryKind.FixedEnum or EntryKind.NullableDirect or
        EntryKind.BigInteger or EntryKind.StatelessRead;

    public bool Equals(EntryShape other) => Kind == other.Kind && Element == other.Element;

    public override bool Equals(object? obj) => obj is EntryShape other && Equals(other);

    public override int GetHashCode() => (int)Kind;
}

internal sealed class SerializerCallModel : IEquatable<SerializerCallModel>
{
    /// <summary>The allocating-entry form of the payload.</summary>
    internal readonly EntryShape Shape;

    /// <summary>The bound public overload.</summary>
    internal readonly SerializerApi Api;

    /// <summary>The closed payload spelling, such as <c>global::N.T</c>.</summary>
    internal readonly string Payload;

    /// <summary>The closed profile spelling, or null when the call uses the declared profile or an open profile.</summary>
    internal readonly string? Profile;

    /// <summary>Whether the call passes a type parameter as its profile, which the interceptor keeps generic.</summary>
    internal readonly bool OpenProfile;

    /// <summary>The complete interception attribute text for this call.</summary>
    internal readonly string Location;

    /// <summary>Whether the payload is a value type.</summary>
    internal readonly bool ValueType;

    /// <summary>The member shape of a non-packable root, which publishes the shared owner the call binds to.</summary>
    internal readonly MemberModel? Root;

    /// <summary>Captures a call site.</summary>
    /// <summary>The generic definition of a closed generic payload, spelled as its model identity, or null.</summary>
    internal readonly string? Definition;

    /// <summary>The closed type arguments of a generic payload, comma-separated, or null.</summary>
    internal readonly string? TypeArguments;

    /// <summary>The payload identities the call reaches, which keep the call's operation.</summary>
    internal readonly OperationReach Reach;

    internal SerializerCallModel(SerializerApi api, string payload, string? profile, string location, bool valueType, MemberModel? root,
        bool openProfile = false, string? definition = null, string? typeArguments = null, EntryShape shape = default, OperationReach? reach = null)
    {
        Reach = reach ?? OperationReach.Empty;
        Shape = shape;
        Definition = definition;
        TypeArguments = typeArguments;
        OpenProfile = openProfile;
        Root = root;
        Api = api;
        Payload = payload;
        Profile = profile;
        Location = location;
        ValueType = valueType;
    }

    /// <summary>Whether the call uses a JSON root.</summary>
    internal bool Json => Api >= SerializerApi.SerializeJsonString;

    /// <summary>Whether the call serializes rather than deserializes.</summary>
    internal bool Writes => Api is SerializerApi.SerializeStream or SerializerApi.SerializeArray or SerializerApi.SerializeBufferWriter or
        SerializerApi.SerializeBuffer or SerializerApi.SerializeWriter or SerializerApi.SerializeJsonString or
        SerializerApi.SerializeJsonBufferWriter or SerializerApi.SerializeJsonBuffer or SerializerApi.SerializeJsonWriter;

    /// <inheritdoc/>
    public bool Equals(SerializerCallModel? other) => ReferenceEquals(this, other) || other != null && Api == other.Api &&
        ValueType == other.ValueType && OpenProfile == other.OpenProfile && Payload == other.Payload && Profile == other.Profile &&
        Location == other.Location && Definition == other.Definition && TypeArguments == other.TypeArguments && Shape.Equals(other.Shape) &&
        Equals(Root, other.Root) && Reach.Equals(other.Reach);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SerializerCallModel);

    /// <inheritdoc/>
    public override int GetHashCode() => ((int)Api * 397) ^ Payload.GetHashCode() ^ Location.GetHashCode();
}

/// <summary>One facade invocation: its closed root, or the operations every payload keeps when it has none.</summary>
internal readonly struct CallFact : IEquatable<CallFact>
{
    /// <summary>The closed call, or null when the invocation stays on the generic facade or is not a facade call.</summary>
    internal readonly SerializerCallModel? Call;

    /// <summary>The operations an unbound facade invocation keeps for every payload.</summary>
    internal readonly Rendering.Emitters.GeneratedOperations Whole;

    /// <summary>Whether the invocation is a facade call that stays on the generic facade, such as one over an open payload,
    /// which generic expansion and wrappers may close from its callers.</summary>
    internal readonly bool Open;

    internal CallFact(SerializerCallModel? call, Rendering.Emitters.GeneratedOperations whole, bool open = false)
    {
        Call = call;
        Whole = whole;
        Open = open;
    }

    /// <inheritdoc/>
    public bool Equals(CallFact other) => Whole == other.Whole && Open == other.Open && Equals(Call, other.Call);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CallFact other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (Call?.GetHashCode() ?? 0) ^ (int)Whole;
}
