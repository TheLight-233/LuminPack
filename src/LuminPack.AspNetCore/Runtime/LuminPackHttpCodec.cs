using Errors = LuminPack.Code.LuminPackExceptionHelper;
using LuminPack.Platforms;

namespace LuminPack.AspNetCore;

/// <summary>Closes application-generated serialization operations for a declared HTTP body type.</summary>
/// <typeparam name="T">The declared request/response DTO.</typeparam>
/// <remarks>Supply concrete generated calls. No reflection-based formatter, runtime type id or alternate serializer is introduced.</remarks>
public sealed class LuminPackHttpCodec<T>
{
    private readonly Func<T, byte[]> _serialize;
    private readonly Func<ReadOnlyMemory<byte>, T> _deserialize;
    private readonly Func<Exception, bool> _isMalformedInput;

    /// <summary>Creates a closed HTTP codec using application-selected generated operations.</summary>
    /// <param name="serialize">The complete record serializer.</param><param name="deserialize">The bounded complete record reader.</param>
    /// <param name="isMalformedInput">An optional classifier; unclassified application exceptions propagate.</param>
    public LuminPackHttpCodec(Func<T, byte[]> serialize, Func<ReadOnlyMemory<byte>, T> deserialize, Func<Exception, bool>? isMalformedInput = null)
    {
        _serialize = serialize ?? Errors.ThrowArgumentNullException<Func<T, byte[]>>(nameof(serialize));
        _deserialize = deserialize ?? Errors.ThrowArgumentNullException<Func<ReadOnlyMemory<byte>, T>>(nameof(deserialize));
        _isMalformedInput = isMalformedInput ?? IsCorePayloadError;
    }

    /// <summary>Adapts an explicit platform value codec to an HTTP body.</summary>
    /// <param name="codec">The complete platform codec.</param>
    public LuminPackHttpCodec(LuminPackCodec<T> codec)
    {
        if (codec is null) Errors.ThrowArgumentNullException(nameof(codec));
        _serialize = value => codec.Serialize(value);
        _deserialize = bytes => codec.Deserialize(bytes.Span);
        _isMalformedInput = IsCorePayloadError;
    }

    /// <summary>Serializes the declared DTO using the configured concrete operation.</summary>
    /// <param name="value">The DTO.</param><returns>The complete binary record.</returns>
    public byte[] Serialize(T value) => _serialize(value) ?? Errors.ThrowInvalidOperationException<byte[]>("The HTTP serializer returned null output.");

    /// <summary>Deserializes the bounded HTTP body using the selected concrete operation.</summary>
    /// <param name="bytes">The complete body.</param><returns>The DTO.</returns>
    public T Deserialize(ReadOnlyMemory<byte> bytes) => _deserialize(bytes);

    /// <summary>Classifies input failures without treating every application exception as malformed data.</summary>
    /// <param name="error">The failure.</param><returns>Whether the application selected this as a payload failure.</returns>
    public bool IsMalformedInput(Exception error) => _isMalformedInput(error);

    /// <summary>Recognizes explicit format errors and Core's private bounds/record exception.</summary>
    /// <param name="error">The failure.</param><returns>Whether it is a known input error.</returns>
    private static bool IsCorePayloadError(Exception error) => error is FormatException or InvalidDataException or EndOfStreamException ||
        (error.GetType().Assembly == typeof(Errors).Assembly && error.GetType().FullName == "LuminPack.Code.LuminPackExceptionHelper+LuminPackException");
}
