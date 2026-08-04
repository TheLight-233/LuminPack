#region Author

//By TheLight233

#endregion

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Interface;
using LuminPack.Option;
using LuminPack.Parsers;
using LuminPack.Utility;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack
{
    /// <summary>Provides convenience and reusable-buffer APIs for LuminPack binary and JSON serialization.</summary>
    /// <remarks>Convenience overloads accept per-call options. Overloads accepting <see cref="LuminBufferWriter"/> use
    /// the option and operation state owned by that writer for a ThreadStatic-free high-performance path.</remarks>
    public static class LuminPackSerializer
    {
        [ThreadStatic]
        private static LuminPackWriterOptionalState? _threadStaticWriterOptionalState;
        [ThreadStatic]
        private static LuminPackReaderOptionalState? _threadStaticReaderOptionalState;
        [ThreadStatic]
        private static LuminPackEvaluatorOptionState? _threadStaticEvaluatorOptionalState;

        internal static bool NeedInitParserFactory = true;
        
        /// <summary>Initializes the parser factory with explicit target-to-parser registrations.</summary>
        /// <param name="registryType">The target types and parser types to register.</param>
        /// <remarks>This application-level initialization should complete before concurrent serialization begins.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Initialize(List<(Type TargetType, Type ParserType)> registryType)
        {
            NeedInitParserFactory = false;
            ParserFactory.Initialize(registryType);
        }
        
        #region Serialize

        /// <summary>Serializes <paramref name="value"/> to a newly allocated LuminPack binary payload.</summary>
        /// <typeparam name="T">The value type to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="option">The configuration for this call, or <see langword="null"/> for LuminPack defaults.</param>
        /// <returns>A new byte array containing the complete serialized payload.</returns>
        /// <remarks>
        /// This convenience overload allocates the returned array and may use thread-static operation state.
        /// Use <see cref="Serialize{T}(in T, LuminBufferWriter)"/> to reuse a buffer and avoid the serializer's
        /// thread-static optional-state path.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Serialize<T>(in T? value, LuminPackSerializerOption? option = null)
        {
            var writerBuffer = LuminBufferWriterPool.Rent();
            
            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();
            state.Init(option);
            
            try
            {
                var writer = new LuminPackWriter(writerBuffer, state);

                writer.WriteValue(value);
                
                var buffer = AllocateUninitializedArray<byte>(writer.CurrentIndex);
                writer.GetSpan().CopyTo(buffer.AsSpan());
                return buffer;
            }
            finally
            {
                LuminBufferWriterPool.Return(writerBuffer);
                state.Reset();
            }
        }
        
        /// <summary>Serializes <paramref name="value"/> into a reusable <see cref="LuminBufferWriter"/>.</summary>
        /// <typeparam name="T">The value type to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writerBuffer">The exclusive buffer and operation context that receives the payload.</param>
        /// <remarks>
        /// <para>This high-performance overload obtains its configuration from <see cref="LuminBufferWriter.Option"/>.</para>
        /// <para>Configure <paramref name="writerBuffer"/> before this call. Its option remains unchanged for the current
        /// Rent-to-Return lifetime and is restored to defaults by <see cref="LuminBufferWriterPool.Return"/>.</para>
        /// <para>The method uses the writer state owned by <paramref name="writerBuffer"/> and does not access the
        /// serializer's thread-static optional state. The caller must exclusively own the buffer until it is returned.</para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize<T>(in T? value, LuminBufferWriter writerBuffer)
        {
            var state = writerBuffer.WriterState;
            try
            {
                var writer = new LuminPackWriter(writerBuffer);

                writer.WriteValue(value);
                
                writerBuffer.CompleteWrite(writer.CurrentIndex);
            }
            catch
            {
                writerBuffer.ResetCore();
                throw;
            }
            finally
            {
                state.ResetOperationState();
            }
        }
        
        /// <summary>Serializes <paramref name="value"/> to a newly allocated JSON string.</summary>
        /// <typeparam name="T">The value type to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="option">The configuration for this call, or <see langword="null"/> for LuminPack defaults.</param>
        /// <returns>A new string containing the complete JSON document.</returns>
        /// <remarks>This convenience overload allocates the returned string and may use thread-static operation state.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SerializeJson<T>(T? value, LuminPackSerializerOption? option = null)
        {
            var writerBuffer = LuminBufferWriterPool.Rent();
            
            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();
                
            state.Init(option);
            
            try
            {
                var writer = new LuminPackJsonWriter(writerBuffer, state);

                LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref value);
                
                return writer.Option.StringEncoding is LuminPackStringEncoding.UTF8 
                    ? Encoding.UTF8.GetString(writer.GetSpan())
                    : Encoding.Unicode.GetString(writer.GetSpan());
            }
            finally
            {
                state.Reset();
                LuminBufferWriterPool.Return(writerBuffer);
            }
        }
        
        /// <summary>Serializes <paramref name="value"/> as JSON into a reusable <see cref="LuminBufferWriter"/>.</summary>
        /// <typeparam name="T">The value type to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writerBuffer">The exclusive buffer and operation context that receives the JSON bytes.</param>
        /// <remarks>
        /// <para>The JSON encoding and other settings come from <see cref="LuminBufferWriter.Option"/>; this overload
        /// intentionally has no separate option parameter.</para>
        /// <para>The option remains configured throughout the current Rent-to-Return lifetime and is restored to defaults
        /// by <see cref="LuminBufferWriterPool.Return"/>. Writer and reader temporary state are cleared as part of Return.</para>
        /// <para>This high-performance path does not access serializer thread-static optional state. Do not use the same
        /// buffer concurrently or reenter this method with that buffer.</para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeJson<T>(T? value, LuminBufferWriter writerBuffer)
        {
            var state = writerBuffer.WriterState;
            try
            {
                var writer = new LuminPackJsonWriter(writerBuffer);

                LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref value);
                
                writerBuffer.CompleteWrite(writer.CurrentIndex);
            }
            catch
            {
                writerBuffer.ResetCore();
                throw;
            }
            finally
            {
                state.ResetOperationState();
            }
        }

        /// <summary>Asynchronously serializes <paramref name="value"/> and writes the binary payload to a stream.</summary>
        /// <typeparam name="T">The value type to serialize.</typeparam>
        /// <param name="stream">The destination stream.</param>
        /// <param name="value">The value to serialize.</param>
        /// <param name="option">The configuration for this operation, or <see langword="null"/> for defaults.</param>
        /// <param name="cancellationToken">A token that can cancel stream writes and flushing.</param>
        /// <returns>A task-like value that completes after the stream has been flushed.</returns>
        /// <remarks>This convenience API internally rents and returns a buffer; the caller does not manage that buffer.</remarks>
        public static async ValueTask SerializeAsync<T>(
            Stream stream, T? value, 
            LuminPackSerializerOption? option = null, 
            CancellationToken cancellationToken = default)
        {
            var tempWriter = LuminBufferWriterPool.Rent();
            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();
            state.Init(option);
            try
            {
                var writer = new LuminPackWriter(tempWriter, state);
                writer.WriteValue(value);
                tempWriter.CompleteWrite(writer.CurrentIndex);
                await tempWriter.WriteToAndResetAsync(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                state.Reset();
                LuminBufferWriterPool.Return(tempWriter);
            }
        }

        #endregion
        
        #region Deserialize

        /// <summary>Deserializes a <typeparamref name="T"/> from a binary span.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="buffer">The span containing a complete or leading LuminPack payload.</param>
        /// <param name="options">The configuration for this call, or <see langword="null"/> for defaults.</param>
        /// <returns>The deserialized value.</returns>
        /// <remarks>This convenience overload may use thread-static reader operation state.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? Deserialize<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(ReadOnlySpan<byte> buffer, LuminPackSerializerOption? options = null)
        {
            T? value = default;
            Deserialize(buffer, ref value, options);
            return value;
        }
        
        /// <summary>Deserializes a <typeparamref name="T"/> from a reusable <see cref="LuminBufferWriter"/>.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="bufferWriter">The buffer containing binary data and the reader operation context.</param>
        /// <returns>The deserialized value.</returns>
        /// <remarks>
        /// <para>Configuration comes exclusively from <see cref="LuminBufferWriter.Option"/>. The option remains valid
        /// for the current Rent-to-Return lifetime and is restored to defaults by <see cref="LuminBufferWriterPool.Return"/>.</para>
        /// <para>This high-performance overload uses the reader state owned by <paramref name="bufferWriter"/> and does not
        /// access serializer thread-static optional state. The buffer must not be used concurrently or reentrantly.</para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? Deserialize<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(LuminBufferWriter bufferWriter)
        {
            T? value = default;
            Deserialize(bufferWriter, ref value);
            return value;
        }

        /// <summary>Deserializes into <paramref name="value"/> from a reusable <see cref="LuminBufferWriter"/>.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="bufferWriter">The buffer containing binary data and the reader operation context.</param>
        /// <param name="value">Receives the deserialized value.</param>
        /// <returns>The number of bytes consumed from the buffer.</returns>
        /// <remarks>
        /// Configuration comes from <see cref="LuminBufferWriter.Option"/> and persists until the buffer is returned.
        /// <see cref="LuminBufferWriterPool.Return"/> restores defaults and clears both operation states. This method does
        /// not use thread-static optional state and requires exclusive ownership of <paramref name="bufferWriter"/>.
        /// </remarks>
        public static int Deserialize<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(LuminBufferWriter bufferWriter, ref T? value)
        {
            var state = bufferWriter.ReaderState;
            try
            {
                var reader = new LuminPackReader(bufferWriter);
                reader.ReadValue(ref value);
                return reader.GetCurrentSpanIndex();
            }
            finally
            {
                state.ResetOperationState();
            }
        }
        
        /// <summary>Deserializes a binary span into <paramref name="value"/>.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="buffer">The span containing a complete or leading LuminPack payload.</param>
        /// <param name="value">Receives the deserialized value.</param>
        /// <param name="options">The configuration for this call, or <see langword="null"/> for defaults.</param>
        /// <returns>The number of bytes consumed.</returns>
        /// <remarks>This convenience overload may use thread-static reader operation state.</remarks>
        public static int Deserialize<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(
            ReadOnlySpan<byte> buffer, ref T? value, 
            LuminPackSerializerOption? options = null)
        {
            
            //if (LuminPackParseProvider.GetParserType<T>() is LuminPackParseProvider.ParserType.Data) goto Read;
            
            //Read:
            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();
            state.Init(options);
            
            var reader = new LuminPackReader(ref buffer, state);
            try
            {
                reader.ReadValue(ref value);
                return reader.GetCurrentSpanIndex();
            }
            finally
            {
                state.Reset();
            }
        }
        
        /// <summary>Deserializes a <typeparamref name="T"/> from a possibly multi-segment sequence.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="buffer">The sequence containing a LuminPack payload.</param>
        /// <param name="options">The configuration for this call, or <see langword="null"/> for defaults.</param>
        /// <returns>The deserialized value.</returns>
        /// <remarks>This convenience overload may use thread-static reader operation state.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? Deserialize<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(in ReadOnlySequence<byte> buffer, LuminPackSerializerOption? options = null)
        {
            T? value = default;
            Deserialize(buffer, ref value, options);
            return value;
        }
        
        /// <summary>Deserializes a possibly multi-segment sequence into <paramref name="value"/>.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="buffer">The sequence containing a LuminPack payload.</param>
        /// <param name="value">Receives the deserialized value.</param>
        /// <param name="options">The configuration for this call, or <see langword="null"/> for defaults.</param>
        /// <returns>The number of bytes consumed.</returns>
        /// <remarks>This convenience overload may use thread-static reader operation state.</remarks>
        public static int Deserialize<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(
            ReadOnlySequence<byte> buffer, ref T? value, 
            LuminPackSerializerOption? options = default)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                return DeserializeUnmanagedSequence(buffer, ref value);
            }
            
            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();
            state.Init(options);

            var reader = new LuminPackReader(ref buffer, state);
            try
            {
                reader.ReadValue(ref value);
                return reader.GetCurrentSpanIndex();
            }
            finally
            {
                state.Reset();
            }
        }
        
        /// <summary>Deserializes a <typeparamref name="T"/> from a JSON string.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="buffer">The JSON document.</param>
        /// <param name="options">The configuration for this call, or <see langword="null"/> for defaults.</param>
        /// <returns>The deserialized value.</returns>
        /// <remarks>This convenience overload performs any required text transcoding and may use pooled temporary storage.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public static T? DeserializeJson<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(string buffer, LuminPackSerializerOption? options = null)
        {
            T? value = default;
    
            if (options == null || options.StringEncoding is LuminPackStringEncoding.UTF8)
            {
                if (buffer.Length > 1024)
                {
                    var byteCount = Encoding.UTF8.GetByteCount(buffer);
                    byte[] byteBuffer = ArrayPool<byte>.Shared.Rent(byteCount);
                    try
                    {
                        Encoding.UTF8.GetBytes(buffer, 0, buffer.Length, byteBuffer, 0);
                        DeserializeJson(byteBuffer.AsSpan(0, byteCount), ref value, options);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(byteBuffer);
                    }
                }
                else
                {
                    Span<byte> span = stackalloc byte[buffer.Length * 3];
                    var written = Encoding.UTF8.GetBytes(buffer, span);
                    DeserializeJson(span[..written], ref value, options);
                }
            }
            else
            {
                DeserializeJson(buffer.AsSpan(), ref value, options);
            }
    
            return value;
        }

        /// <summary>Deserializes a <typeparamref name="T"/> from a UTF-16 JSON character span.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="buffer">The JSON character span.</param>
        /// <param name="options">The configuration for this call, or <see langword="null"/> for defaults.</param>
        /// <returns>The deserialized value.</returns>
        /// <remarks>This convenience overload may use thread-static reader operation state.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? DeserializeJson<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(ReadOnlySpan<char> buffer, LuminPackSerializerOption? options = null)
        {
            T? value = default;
            DeserializeJson(buffer, ref value, options);
            return value;
        }
        
        /// <summary>Deserializes a <typeparamref name="T"/> from JSON bytes in a reusable <see cref="LuminBufferWriter"/>.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="bufferWriter">The buffer containing JSON bytes and the reader operation context.</param>
        /// <returns>The deserialized value.</returns>
        /// <remarks>
        /// <para>JSON encoding and all other settings come from <see cref="LuminBufferWriter.Option"/>. The option remains
        /// configured for the current Rent-to-Return lifetime and is reset by <see cref="LuminBufferWriterPool.Return"/>.</para>
        /// <para>This high-performance overload uses the buffer-owned reader state and never accesses serializer
        /// thread-static optional state. The buffer must be exclusively owned and cannot be used for same-instance reentry.</para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? DeserializeJson<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(LuminBufferWriter bufferWriter)
        {
            T? value = default;
            DeserializeJson(bufferWriter, ref value);
            return value;
        }

        private static int DeserializeJson<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(LuminBufferWriter bufferWriter, ref T? value)
        {
            var state = bufferWriter.ReaderState;
            try
            {
                var reader = new LuminPackJsonReader(bufferWriter);
                if (!reader.Read())
                    throw new FormatException("JSON input does not contain a value");
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref value);
                reader.EnsureEndOfDocument();
                return reader.CurrentIndex;
            }
            finally
            {
                state.ResetOperationState();
            }
        }

        private static int DeserializeJson<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(
            ReadOnlySpan<char> buffer, ref T? value, LuminPackSerializerOption? options = null)
        {
            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();
    
            state.Init(options);
    
            try
            {
                var span = MemoryMarshal.Cast<char, byte>(buffer);
                var reader = new LuminPackJsonReader(ref span, state);
                if (!reader.Read())
                    throw new FormatException("JSON input does not contain a value");
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref value);
                reader.EnsureEndOfDocument();
           
                return reader.CurrentIndex;
            }
            finally
            {
                state.Reset();
            }
        }
        
        private static int DeserializeJson<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(
            ReadOnlySpan<byte> buffer, ref T? value, LuminPackSerializerOption? options = null)
        {
            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();
    
            state.Init(options);
    
            try
            {
                var reader = new LuminPackJsonReader(ref buffer, state);
                if (!reader.Read())
                    throw new FormatException("JSON input does not contain a value");
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref value);
                reader.EnsureEndOfDocument();
           
                return reader.CurrentIndex;
            }
            finally
            {
                state.Reset();
            }
        }

        /// <summary>Asynchronously reads a stream to completion and deserializes one <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="stream">The source stream.</param>
        /// <param name="options">The configuration for this operation, or <see langword="null"/> for defaults.</param>
        /// <param name="cancellationToken">A token that can cancel stream reads.</param>
        /// <returns>A task-like value containing the deserialized value.</returns>
        /// <remarks>This convenience API manages its own pooled read buffers; no BufferWriter must be returned by the caller.</remarks>
        public static async ValueTask<T?> DeserializeAsync<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(
            Stream stream, LuminPackSerializerOption? options = null, 
            CancellationToken cancellationToken = default)
        {
            
            if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> streamBuffer))
            {
                cancellationToken.ThrowIfCancellationRequested();
                T? value = default;
                var bytesRead = Deserialize(streamBuffer.AsSpan(checked((int)ms.Position)), ref value, options);
                ms.Seek(bytesRead, SeekOrigin.Current);
                return value;
            }

            // 处理一般流
            var builder = ReadOnlySequenceBuilderPool.Rent();
            try
            {
                const int initialBufferSize = 65536;
                var buffer = ArrayPool<byte>.Shared.Rent(initialBufferSize);
                var offset = 0;
                
                while (true)
                {
                    if (offset == buffer.Length)
                    {
                        builder.Add(buffer, returnToPool: true);
                        buffer = ArrayPool<byte>.Shared.Rent(Math.Min(buffer.Length << 2, 0x7FFFFFC7));
                        offset = 0;
                    }

                    int read;
                    try
                    {
                        read = await stream.ReadAsync(
                            buffer.AsMemory(offset, buffer.Length - offset), 
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                        throw;
                    }

                    if (read == 0)
                    {
                        builder.Add(buffer.AsMemory(0, offset), returnToPool: true);
                        break;
                    }

                    offset += read;
                }
                
                if (builder.TryGetSingleMemory(out var memory))
                {
                    return Deserialize<T>(memory.Span, options);
                }
                else
                {
                    return Deserialize<T>(builder.Build(), options);
                }
            }
            finally
            {
                builder.Reset();
            }
        }
        
        /// <summary>Asynchronously reads a stream and deserializes into an existing or default <paramref name="value"/>.</summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="stream">The source stream.</param>
        /// <param name="value">The initial value supplied to parsers that can reuse instances.</param>
        /// <param name="options">The configuration for this operation, or <see langword="null"/> for defaults.</param>
        /// <param name="cancellationToken">A token that can cancel stream reads.</param>
        /// <returns>A task-like value containing the deserialized value.</returns>
        /// <remarks>This convenience API manages its own pooled read buffers; no BufferWriter must be returned by the caller.</remarks>
        public static async ValueTask<T?> DeserializeAsync<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(
            Stream stream, T? value, LuminPackSerializerOption? options = null, 
            CancellationToken cancellationToken = default)
        {
            
            if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> streamBuffer))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytesRead = Deserialize(streamBuffer.AsSpan(checked((int)ms.Position)), ref value, options);
                ms.Seek(bytesRead, SeekOrigin.Current);
                return value;
            }

            // 处理一般流
            var builder = ReadOnlySequenceBuilderPool.Rent();
            try
            {
                const int initialBufferSize = 65536;
                var buffer = ArrayPool<byte>.Shared.Rent(initialBufferSize);
                var offset = 0;
                
                while (true)
                {
                    if (offset == buffer.Length)
                    {
                        builder.Add(buffer, returnToPool: true);
                        buffer = ArrayPool<byte>.Shared.Rent(Math.Min(buffer.Length << 2, 0x7FFFFFC7));
                        offset = 0;
                    }

                    int read;
                    try
                    {
                        read = await stream.ReadAsync(
                            buffer.AsMemory(offset, buffer.Length - offset), 
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                        throw;
                    }

                    if (read == 0)
                    {
                        builder.Add(buffer.AsMemory(0, offset), returnToPool: true);
                        break;
                    }

                    offset += read;
                }
                
                if (builder.TryGetSingleMemory(out var memory))
                {
                    Deserialize(memory.Span, ref value, options);
                    return value;
                }
                else
                {
                    Deserialize(builder.Build(), ref value, options);
                    return value;
                }
            }
            finally
            {
                builder.Reset();
            }
        }
        
        #endregion
        
        #region Helper Methods

        /// <summary>Calculates the binary payload size for <paramref name="data"/> without producing the payload.</summary>
        /// <typeparam name="T">The value type to evaluate.</typeparam>
        /// <param name="data">The value whose serialized size is calculated.</param>
        /// <param name="option">The configuration for this calculation, or <see langword="null"/> for defaults.</param>
        /// <returns>The number of bytes required by binary serialization.</returns>
        /// <remarks>This convenience API may use thread-static evaluator state and does not modify a BufferWriter.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sizeof<T>(T? data, LuminPackSerializerOption? option = null)
        {
            var state = _threadStaticEvaluatorOptionalState ??= new LuminPackEvaluatorOptionState();
            state.Init(option);
            try
            {
                var eval = LuminPackParseProvider.GetParserEvaluator<T>();
            
                var totalLength = 0;
            
                var evaluator = new LuminPackEvaluator(ref totalLength, state);
            
                eval.CalculateOffset(ref evaluator, ref data);
            
                return totalLength;
            }
            finally
            {
                state.Reset();
            }
            
        }
        
        /// <summary>
        /// 压缩数据
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="destination">目标位置</param>
        /// <returns>压缩后长度</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compress(LuminBufferWriter source, LuminBufferWriter destination)
        {
            return LuminCompressor.Compress(source, destination);
        }
        
        /// <summary>
        /// 压缩数据
        /// </summary>
        /// <param name="source">数据源</param>
        /// <returns>压缩后长度</returns>
        [Obsolete("建议使用 Compress(ReadOnlySpan<byte> source, Span<byte> destination) 方法以避免不必要的内存分配")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Compress(ReadOnlySpan<byte> source)
        {
            return LuminCompressor.Compress(source);
        }
        
        /// <summary>
        /// 压缩数据
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="destination">目标位置</param>
        /// <returns>压缩后长度</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            return LuminCompressor.Compress(source, destination);
        }
        
        /// <summary>
        /// 解压缩数据
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="destination">目标位置</param>
        /// <returns>解压后长度</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            return LuminCompressor.Decompress(source, destination);
        }
        
        /// <summary>
        /// 解压缩数据
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="destination">目标位置</param>
        /// <returns>解压后长度</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Decompress(LuminBufferWriter source, LuminBufferWriter destination)
        {
            return LuminCompressor.Decompress(source, destination);
        }
        
        /// <summary>
        /// 解压缩数据
        /// </summary>
        /// <param name="source">数据源</param>
        /// <returns>解压后长度</returns>
        [Obsolete("建议使用 Compress(ReadOnlySpan<byte> source, Span<byte> destination) 方法以避免不必要的内存分配")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Decompress(ReadOnlySpan<byte> source)
        {
            return LuminCompressor.Decompress(source);
        }

        
        /// <summary>
        /// 获取指定类型的解析器名称
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <returns>预期生成的解析器名称</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetParserName(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            
            return GenerateExpectedParserName(type);
        }

        /// <summary>
        /// 获取指定类型的解析器名称
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetParserName<T>()
        {
            return GetParserName(typeof(T));
        }
        
        #endregion
        
        #region Private Helper Methods
        
        private static string GenerateExpectedParserName(Type originalType)
        {
            const string parserNamespace = "LuminPack.Generated";
            bool isGeneric = originalType.IsGenericType;

            // 移除可能的 global:: 前缀
            static string RemoveGlobalPrefix(string typeName)
            {
                const string globalPrefix = "global::";
                return typeName.StartsWith(globalPrefix, StringComparison.Ordinal) 
                    ? typeName.Substring(globalPrefix.Length) 
                    : typeName;
            }
    
            string originalFullName = RemoveGlobalPrefix(originalType.FullName ?? originalType.Name);
    
            string normalizedName = originalFullName.Replace('.', '_').Replace('+', '_');

            string fullTypeName;
            if (isGeneric)
            {
                string baseName = normalizedName.Split('`')[0];
                int argCount = originalType.GetGenericArguments().Length;
                fullTypeName = $"{parserNamespace}.{baseName}Parser`{argCount}";
            }
            else
            {
                fullTypeName = $"{parserNamespace}.{normalizedName}Parser";
            }

            return fullTypeName;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureParserRegistered<T>()
        {
            if (!LuminPackParseProvider.IsRegistered<T>() && 
                !LuminPackParseProvider.TryRegisterParser<T>())
            {
                LuminPackExceptionHelper.ThrowNoParserRegistered(typeof(T));
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DeserializeUnmanagedSequence<T>(ReadOnlySequence<byte> buffer, ref T? value)
        {
            int size = Unsafe.SizeOf<T>();
            if (buffer.Length < size)
            {
                LuminPackExceptionHelper.ThrowInvalidRange(size, (int)buffer.Length);
            }
            
            if (buffer.IsSingleSegment)
            {
                value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(buffer.FirstSpan));
                return size;
            }
            
            if (size <= 512)
            {
                Span<byte> tempSpan = stackalloc byte[size];
                buffer.Slice(0, size).CopyTo(tempSpan);
                value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(tempSpan));
                return size;
            }

            var rentedBuffer = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                var tempSpan = rentedBuffer.AsSpan(0, size);
                buffer.Slice(0, size).CopyTo(tempSpan);
                value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(tempSpan));
                return size;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
        
        #endregion
    }
}

namespace LuminPack.Core
{
    public abstract class LuminPack<T> : IDisposable
    {
        protected T? DataRef { get; private set; }
        protected object? DataContainer { get; set; }
        protected readonly CancellationTokenSource _cts;
        
        internal LuminPack()
        {
            _cts = new CancellationTokenSource();
        }

        internal LuminPack(T dataRef)
        {
            _cts = new CancellationTokenSource();
            DataRef = dataRef;
        }

        [Preserve]
        public virtual T? GetDataRef() => DataRef;

        [Preserve]
        public virtual object? GetData()
        {
            if (typeof(T).IsValueType) 
                throw new ArgumentException("T must not be a value type!");
            return DataContainer;
        }

        [Preserve]
        public virtual TProperty GetData<TProperty>() where TProperty : class
            => (TProperty)DataContainer!;

        [Preserve]
        public virtual void CancelTask() => _cts.Cancel();
        
        public void Dispose()
        {
            DataRef = default;
            DisposeResources();
            GC.SuppressFinalize(this);
        }
        
        protected virtual void DisposeResources() { }
    }
}
