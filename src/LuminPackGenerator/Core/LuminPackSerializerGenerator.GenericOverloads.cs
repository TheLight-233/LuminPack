using System.Text;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{
    /// <summary>Completes concrete overload shapes with generic wrappers over shared operation cores.</summary>
    /// <param name="builder">The shared serializer contribution.</param>
    private static void AppendAdditionalGenericFallbacks(StringBuilder builder)
        => builder.AppendLine(AdditionalGenericFallbackTemplate.Replace("__ACCESS__", _currentAccess));

    private const string AdditionalGenericFallbackTemplate = """
                /// <summary>Writes a value using the exclusive buffer's binary operation context.</summary>
                /// <typeparam name="T">The value type.</typeparam>
                /// <param name="value">The value to serialize.</param>
                /// <param name="writerBuffer">The exclusive destination and reference-tracking context.</param>
                /// <remarks>Appends without resetting contents or reference state. Call ResetForReuse for an independent session or after failure.</remarks>
                __ACCESS__ static void Serialize<T>(in T value, LuminBufferWriter writerBuffer)
                {
                    var writer = new LuminPackWriter(writerBuffer, writerBuffer.WriterState);
                    try { writer.WriteValue(in value); }
                    finally { writerBuffer.CompleteWrite(writer.CurrentIndex); }
                }

                /// <summary>Writes JSON using the exclusive buffer's operation context.</summary>
                /// <typeparam name="T">The value type.</typeparam>
                /// <param name="value">The value to serialize.</param>
                /// <param name="writerBuffer">The exclusive destination and reference-tracking context.</param>
                /// <remarks>Appends without resetting contents or reference state. Call ResetForReuse for an independent session or after failure.</remarks>
                __ACCESS__ static void SerializeJson<T>(T value, LuminBufferWriter writerBuffer)
                {
                    var writer = new LuminPackJsonWriter(writerBuffer, writerBuffer.WriterState);
                    try { writer.WriteValue(in value); }
                    finally { writerBuffer.CompleteWrite(writer.CurrentIndex); }
                }

                /// <summary>Serializes a value and asynchronously writes and flushes its binary payload.</summary>
                /// <typeparam name="T">The value type.</typeparam>
                /// <param name="stream">The destination stream.</param>
                /// <param name="value">The value to serialize.</param>
                /// <param name="option">The operation configuration, or null for defaults.</param>
                /// <param name="cancellationToken">Cancels stream writes and flushing.</param>
                /// <returns>A task-like value completing after the stream is flushed.</returns>
                __ACCESS__ static async ValueTask SerializeAsync<T>(Stream stream, T value,
                    LuminPackSerializerOption? option = null, CancellationToken cancellationToken = default)
                {
                    var buffer = LuminBufferWriterPool.Rent();
                    try
                    {
                        var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();
                        state.Init(option);
                        try { SerializeCore(buffer, state, in value); }
                        finally { state.Reset(); }
                        await buffer.WriteToAndResetAsync(stream, cancellationToken).ConfigureAwait(false);
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally { LuminBufferWriterPool.Return(buffer); }
                }

                /// <summary>Reads a JSON document into existing value storage.</summary>
                /// <typeparam name="T">The target type.</typeparam>
                /// <param name="buffer">The JSON document.</param>
                /// <param name="value">Receives the deserialized value.</param>
                /// <param name="options">The operation configuration, or null for defaults.</param>
                /// <returns>Consumed bytes in the configured encoding after transcoding.</returns>
                __ACCESS__ static int DeserializeJson<T>(string buffer, ref T value, LuminPackSerializerOption? options = null)
                {
                    if (buffer is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(buffer));
                    return DeserializeJson<T>(buffer.AsSpan(), ref value, options);
                }

                /// <summary>Reads JSON bytes into existing value storage through the shared reader core.</summary>
                /// <typeparam name="T">The target type.</typeparam>
                /// <param name="buffer">The encoded JSON document.</param>
                /// <param name="value">Receives the deserialized value.</param>
                /// <param name="options">The operation configuration, or null for defaults.</param>
                /// <returns>The consumed byte count.</returns>
                __ACCESS__ static int DeserializeJson<T>(ReadOnlySpan<byte> buffer, ref T value, LuminPackSerializerOption? options = null)
                {
                    var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();
                    state.Init(options);
                    try { return DeserializeJsonCore(buffer, state, ref value); }
                    finally { state.Reset(); }
                }

                /// <summary>Transcodes JSON characters when required and reads into existing value storage.</summary>
                /// <typeparam name="T">The target type.</typeparam>
                /// <param name="buffer">The JSON characters.</param>
                /// <param name="value">Receives the deserialized value.</param>
                /// <param name="options">The operation configuration, or null for defaults.</param>
                /// <returns>Consumed bytes in the configured encoding after transcoding.</returns>
                __ACCESS__ static int DeserializeJson<T>(ReadOnlySpan<char> buffer, ref T value, LuminPackSerializerOption? options = null)
                {
                    if (options?.StringEncoding is LuminPackStringEncoding.UTF16)
                        return DeserializeJson<T>(MemoryMarshal.AsBytes(buffer), ref value, options);
                    int byteCount = Encoding.UTF8.GetByteCount(buffer);
                    byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
                    try
                    {
                        int written = Encoding.UTF8.GetBytes(buffer, rented);
                        return DeserializeJson<T>(rented.AsSpan(0, written), ref value, options);
                    }
                    finally { ArrayPool<byte>.Shared.Return(rented); }
                }

                /// <summary>Reads JSON using the exclusive buffer's reader context.</summary>
                /// <typeparam name="T">The target type.</typeparam>
                /// <param name="bufferWriter">The JSON bytes and reference-tracking context.</param>
                /// <param name="value">Receives the deserialized value.</param>
                /// <returns>The consumed byte count.</returns>
                /// <remarks>Uses compiled options and clears only temporary operation state.</remarks>
                __ACCESS__ static int DeserializeJson<T>(LuminBufferWriter bufferWriter, ref T value)
                {
                    var state = bufferWriter.ReaderState;
                    try { return DeserializeJsonCore(bufferWriter.GetSpan(), state, ref value); }
                    finally { state.ResetOperationState(); }
                }

                /// <summary>Writes <typeparamref name="T"/> using its selected formatter and caller-owned binary state.</summary>
                /// <typeparam name="T">The payload type.</typeparam>
                /// <param name="buffer">The destination storage.</param>
                /// <param name="state">The initialized operation context.</param>
                /// <param name="value">The value to serialize.</param>
                private static void SerializeCore<T>(LuminBufferWriter buffer, LuminPackWriterOptionalState state, in T value)
                {
                    var writer = new LuminPackWriter(buffer, state);
                    writer.WriteValue(in value);
                    buffer.CompleteWrite(writer.CurrentIndex);
                }

                /// <summary>Writes <typeparamref name="T"/> using its selected formatter and caller-owned JSON state.</summary>
                /// <typeparam name="T">The payload type.</typeparam>
                /// <param name="buffer">The destination storage.</param>
                /// <param name="state">The initialized reference-tracking context.</param>
                /// <param name="value">The value to serialize.</param>
                private static void SerializeJsonCore<T>(LuminBufferWriter buffer, LuminPackWriterOptionalState state, in T value)
                {
                    var writer = new LuminPackJsonWriter(buffer, state);
                    writer.WriteValue(in value);
                    buffer.CompleteWrite(writer.CurrentIndex);
                }

                /// <summary>Performs one generic binary read with caller-owned state.</summary>
                /// <typeparam name="T">The target type.</typeparam>
                /// <param name="buffer">The binary bytes.</param>
                /// <param name="state">The initialized reader context.</param>
                /// <param name="value">Receives the deserialized value.</param>
                /// <returns>The consumed byte count.</returns>
                private static int DeserializeCore<T>(ReadOnlySpan<byte> buffer, LuminPackReaderOptionalState state, ref T value)
                {
                    var reader = new LuminPackReader(ref buffer, state);
                    reader.ReadValue(ref value);
                    return reader.GetCurrentSpanIndex();
                }

                /// <summary>Performs one generic JSON read with caller-owned state.</summary>
                /// <typeparam name="T">The target type.</typeparam>
                /// <param name="buffer">The JSON bytes.</param>
                /// <param name="state">The initialized reader context.</param>
                /// <param name="value">Receives the deserialized value.</param>
                /// <returns>The consumed byte count.</returns>
                private static int DeserializeJsonCore<T>(ReadOnlySpan<byte> buffer, LuminPackReaderOptionalState state, ref T value)
                {
                    var reader = new LuminPackJsonReader(ref buffer, state);
                    if (!reader.Read()) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON input does not contain a value");
                    reader.ReadValue(ref value);
                    reader.EnsureEndOfDocument();
                    return reader.CurrentIndex;
                }
        """;
}
