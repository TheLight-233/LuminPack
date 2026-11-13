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

/// <summary>Normalized root facts shared by all facade calls of one payload.</summary>
internal sealed class PayloadBinding : IEquatable<PayloadBinding>
{
    internal readonly string Type;
    internal readonly MemberModel? Root;
    internal readonly string? Definition;
    internal readonly string? TypeArguments;
    internal readonly EntryShape Shape;
    internal readonly OperationReach Reach;

    /// <summary>Captures the closed payload and the root declarations it needs.</summary>
    /// <param name="type">The closed payload spelling.</param>
    /// <param name="root">The shared formatter requirement, if any.</param>
    /// <param name="definition">The generic payload definition.</param>
    /// <param name="typeArguments">The closed arguments of the payload definition.</param>
    /// <param name="shape">The allocating binary entry form.</param>
    /// <param name="reach">The declarations that keep the bound operation.</param>
    internal PayloadBinding(string type, MemberModel? root, string? definition, string? typeArguments, EntryShape shape, OperationReach reach)
    {
        Type = type;
        Root = root;
        Definition = definition;
        TypeArguments = typeArguments;
        Shape = shape;
        Reach = reach;
    }

    /// <inheritdoc/>
    public bool Equals(PayloadBinding? other) => ReferenceEquals(this, other) || other != null && Type == other.Type &&
        Definition == other.Definition && TypeArguments == other.TypeArguments && Shape.Equals(other.Shape) &&
        Equals(Root, other.Root) && Reach.Equals(other.Reach);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PayloadBinding);

    /// <inheritdoc/>
    public override int GetHashCode() => Type.GetHashCode();
}

/// <summary>A facade overload, its payload binding and the location or IL signature it replaces.</summary>
internal sealed class SerializerCallModel : IEquatable<SerializerCallModel>
{
    internal readonly SerializerApi Api;
    internal readonly PayloadBinding Binding;
    internal readonly string Location;
    internal readonly bool OpenProfile;

    /// <summary>Captures one intercepted call.</summary>
    /// <param name="api">The facade overload.</param>
    /// <param name="binding">The shared payload facts.</param>
    /// <param name="location">The interception attribute, if the backend can bind this call.</param>
    /// <param name="openProfile">Whether the interceptor forwards the original profile type argument.</param>
    internal SerializerCallModel(SerializerApi api, PayloadBinding binding, string location, bool openProfile)
    {
        Api = api;
        Binding = binding;
        Location = location;
        OpenProfile = openProfile;
    }

    internal string Payload => Binding.Type;
    internal MemberModel? Root => Binding.Root;
    internal string? Definition => Binding.Definition;
    internal string? TypeArguments => Binding.TypeArguments;
    internal EntryShape Shape => Binding.Shape;

    /// <summary>Whether the call uses a JSON root.</summary>
    internal bool Json => Api >= SerializerApi.SerializeJsonString;

    /// <summary>Whether the call serializes.</summary>
    internal bool Writes => Api is SerializerApi.SerializeStream or SerializerApi.SerializeArray or SerializerApi.SerializeBufferWriter or
        SerializerApi.SerializeBuffer or SerializerApi.SerializeWriter or SerializerApi.SerializeJsonString or
        SerializerApi.SerializeJsonBufferWriter or SerializerApi.SerializeJsonBuffer or SerializerApi.SerializeJsonWriter;

    /// <inheritdoc/>
    public bool Equals(SerializerCallModel? other) => ReferenceEquals(this, other) || other != null && Api == other.Api &&
        OpenProfile == other.OpenProfile && Location == other.Location && Binding.Equals(other.Binding);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SerializerCallModel);

    /// <inheritdoc/>
    public override int GetHashCode() => ((int)Api * 397) ^ Binding.GetHashCode() ^ Location.GetHashCode();
}

/// <summary>One facade invocation: its closed root, or the operations every payload keeps when it has none.</summary>
internal readonly struct CallFact : IEquatable<CallFact>
{
    /// <summary>The closed call, or null when the invocation stays on the generic facade or is not a facade call.</summary>
    internal readonly SerializerCallModel? Call;

    /// <summary>The operations an unbound facade invocation keeps for every payload.</summary>
    internal readonly Rendering.Emitters.GeneratedOperations Whole;

    /// <summary>Captures the precise call or the conservative operation selection.</summary>
    /// <param name="call">The bound call, if available.</param>
    /// <param name="whole">The operations every payload retains.</param>
    internal CallFact(SerializerCallModel? call, Rendering.Emitters.GeneratedOperations whole)
    {
        Call = call;
        Whole = whole;
    }

    /// <inheritdoc/>
    public bool Equals(CallFact other) => Whole == other.Whole && Equals(Call, other.Call);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CallFact other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (Call?.GetHashCode() ?? 0) ^ (int)Whole;
}
