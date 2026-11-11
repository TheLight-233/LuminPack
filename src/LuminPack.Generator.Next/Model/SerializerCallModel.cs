using System;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>A public <c>LuminPackSerializer</c> overload that a closed call can bind to a concrete root.</summary>
internal enum SerializerApi
{
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
internal sealed class SerializerCallModel : IEquatable<SerializerCallModel>
{
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
    internal SerializerCallModel(SerializerApi api, string payload, string? profile, string location, bool valueType, MemberModel? root,
        bool openProfile = false)
    {
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

    /// <inheritdoc/>
    public bool Equals(SerializerCallModel? other) => ReferenceEquals(this, other) || other != null && Api == other.Api &&
        ValueType == other.ValueType && OpenProfile == other.OpenProfile && Payload == other.Payload && Profile == other.Profile &&
        Location == other.Location &&
        Equals(Root, other.Root);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SerializerCallModel);

    /// <inheritdoc/>
    public override int GetHashCode() => ((int)Api * 397) ^ Payload.GetHashCode() ^ Location.GetHashCode();
}
