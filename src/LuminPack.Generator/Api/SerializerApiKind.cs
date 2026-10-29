using System;

namespace LuminPack.Code.Core;

/// <summary>Identifies independently selectable concrete serializer overloads.</summary>
/// <remarks>Each bit is the position of its overload in the generated concrete overload order.</remarks>
[Flags]
internal enum SerializerApiKind
{
    /// <summary>No concrete overload is required.</summary>
    None = 0,
    /// <summary>Serializes a value into a new binary byte array.</summary>
    SerializeArray = 1 << 0,
    /// <summary>Serializes a value through a reusable binary writer.</summary>
    SerializeBuffer = 1 << 1,
    /// <summary>Serializes a value into a JSON string.</summary>
    SerializeJsonString = 1 << 2,
    /// <summary>Serializes JSON through a reusable writer.</summary>
    SerializeJsonBuffer = 1 << 3,
    /// <summary>Serializes a value asynchronously to a stream.</summary>
    SerializeAsync = 1 << 4,
    /// <summary>Deserializes a binary span into an existing value.</summary>
    DeserializeSpanRef = 1 << 5,
    /// <summary>Deserializes through a reusable binary reader.</summary>
    DeserializeBufferRef = 1 << 6,
    /// <summary>Deserializes a binary sequence into an existing value.</summary>
    DeserializeSequenceRef = 1 << 7,
    /// <summary>Deserializes a JSON string into an existing value.</summary>
    DeserializeJsonStringRef = 1 << 8,
    /// <summary>Deserializes a UTF-8 JSON span into an existing value.</summary>
    DeserializeJsonSpanByteRef = 1 << 9,
    /// <summary>Deserializes a UTF-16 JSON span into an existing value.</summary>
    DeserializeJsonSpanCharRef = 1 << 10,
    /// <summary>Deserializes JSON through a reusable reader.</summary>
    DeserializeJsonBufferRef = 1 << 11,
    /// <summary>Deserializes asynchronously with the compatibility value parameter.</summary>
    DeserializeAsyncValue = 1 << 12,
    /// <summary>Serializes binary data to an arbitrary byte buffer writer.</summary>
    SerializeBufferWriter = 1 << 13,
    /// <summary>Serializes JSON to an arbitrary byte buffer writer.</summary>
    SerializeJsonBufferWriter = 1 << 14,
    /// <summary>Preserves the complete legacy concrete API surface.</summary>
    All = (1 << 15) - 1,
}
