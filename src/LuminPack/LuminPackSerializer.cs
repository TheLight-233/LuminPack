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
    public static class LuminPackSerializer
    {
        [ThreadStatic]
        private static LuminPackWriterOptionalState? _threadStaticWriterOptionalState;
        [ThreadStatic]
        private static LuminPackReaderOptionalState? _threadStaticReaderOptionalState;
        [ThreadStatic]
        private static LuminPackEvaluatorOptionState? _threadStaticEvaluatorOptionalState;

        internal static bool NeedInitParserFactory = true;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Initialize(List<(Type TargetType, Type ParserType)> registryType)
        {
            NeedInitParserFactory = false;
            ParserFactory.Initialize(registryType);
        }
        
        #region Serialize

        /// <summary>
        /// 序列化主方法
        /// </summary>
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
        
        /// <summary>
        /// 序列化主方法
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize<T>(in T? value, LuminBufferWriter writerBuffer, LuminPackSerializerOption? option = null)
        {
            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();
            state.Init(option);
            
            try
            {
                var writer = new LuminPackWriter(writerBuffer, state);

                writer.WriteValue(value);
                
                writerBuffer._writtenCount = writer.CurrentIndex;
            }
            finally
            {
                state.Reset();
            }
        }
        
        /// <summary>
        /// 序列化主方法
        /// </summary>
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
                LuminBufferWriterPool.Return(writerBuffer);
            }
        }
        
        /// <summary>
        /// 序列化主方法
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeJson<T>(T? value, LuminBufferWriter writerBuffer, LuminPackSerializerOption? option = null)
        {
            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();
                
            state.Init(option);
            
            var writer = new LuminPackJsonWriter(writerBuffer, state);

            LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref value);
            
            writerBuffer._writtenCount = writer.CurrentIndex;
        }

        /// <summary>
        /// 异步序列化
        /// </summary>
        public static async ValueTask SerializeAsync<T>(
            Stream stream, T? value, 
            LuminPackSerializerOption? option = null, 
            CancellationToken cancellationToken = default)
        {
            var tempWriter = LuminBufferWriterPool.Rent();
            try
            {
                var buffer = Serialize(value, option);
                await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                LuminBufferWriterPool.Return(tempWriter);
            }
        }

        #endregion
        
        #region Deserialize
        
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? Deserialize<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(LuminBufferWriter bufferWriter, LuminPackSerializerOption? options = null)
        {
            T? value = default;
            Deserialize(bufferWriter.GetSpan(), ref value, options);
            return value;
        }
        
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? DeserializeJson<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(LuminBufferWriter bufferWriter, LuminPackSerializerOption? options = null)
        {
            T? value = default;
            DeserializeJson(bufferWriter.GetSpan(), ref value, options);
            return value;
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
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref value);
           
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
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref value);
           
                return reader.CurrentIndex;
            }
            finally
            {
                state.Reset();
            }
        }

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

        /// <summary>
        /// 计算对象大小
        /// </summary>
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
            
            Span<byte> tempSpan = size <= 512 ? stackalloc byte[size] : new byte[size];
            try
            {
                buffer.Slice(0, size).CopyTo(tempSpan);
                value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(tempSpan));
                return size;
            }
            finally
            {
                if (size > 512) ArrayPool<byte>.Shared.Return(tempSpan.ToArray());
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
