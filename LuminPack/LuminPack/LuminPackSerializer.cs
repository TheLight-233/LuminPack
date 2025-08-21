#region Author

//By TheLight233

#endregion

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Data;
using LuminPack.Enum;
using LuminPack.Interface;
using LuminPack.Internal;
using LuminPack.Option;
using LuminPack.Parsers;
using LuminPack.Utility;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack
{
    public static partial class LuminPackSerializer
    {
        [ThreadStatic]
        private static LuminPackWriterOptionalState? _threadStaticWriterOptionalState;
        [ThreadStatic]
        private static LuminPackReaderOptionalState? _threadStaticReaderOptionalState;
        [ThreadStatic]
        private static LuminPackEvaluatorOptionState? _threadStaticEvaluatorOptionalState;

        #region Serialize

        /// <summary>
        /// 序列化主方法
        /// </summary>
        public static byte[] Serialize<T>(in T? value, LuminPackSerializerOption? option = null)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                return SerializeUnmanaged(value);
            }
            
            // if (LuminPackParseProvider.GetParserType<T>() is LuminPackParseProvider.ParserType.Data)
            // {
            //     return LoadData(value)!.Serialize();
            // }
            
            var typeKind = TypeHelpers.TryGetUnmanagedSzArrayElementSizeOrLuminPackableFixedSize<T>(out var elementSize);

            return typeKind switch
            {
                TypeKind.FixedSizeLuminPackable => SerializeFixedSize(value, elementSize),
                TypeKind.UnmanagedSzArray      => SerializeUnmanagedArray(value, elementSize),
                _                              => SerializeComplexType(value, option)
            };
        }

        /// <summary>
        /// 异步序列化
        /// </summary>
        public static async ValueTask SerializeAsync<T>(
            Stream stream, T? value, 
            LuminPackSerializerOption? option = null, 
            CancellationToken cancellationToken = default)
        {
            var tempWriter = ReusableLinkedArrayBufferWriterPool.Rent();
            try
            {
                var buffer = Serialize(value, option);
                await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ReusableLinkedArrayBufferWriterPool.Return(tempWriter);
            }
        }

        #endregion
        
        #region Deserialize
        
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
        
        public static int Deserialize<
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
            T>(
            ReadOnlySpan<byte> buffer, ref T? value, 
            LuminPackSerializerOption? options = null)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                return DeserializeUnmanaged(buffer, ref value);
            }
            
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
            var builder = ReusableReadOnlySequenceBuilderPool.Rent();
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
                        buffer = ArrayPool<byte>.Shared.Rent(MathEx.NewArrayCapacity(buffer.Length));
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
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// 获取解析器
        /// </summary>
        public static LuminData<T>? GetParser<T>()
        {
            EnsureParserRegistered<T>();
            var parser = LuminPackParseProvider.GetParser<T>();
            return LuminPackParseProvider.GetParserType<T>() is LuminPackParseProvider.ParserType.Data 
                ? null 
                : (LuminData<T>)parser;
        }
        
        /// <summary>
        /// 加载数据
        /// </summary>
        public static IDataParser<T>? LoadData<T>(in T data)
        {
            EnsureParserRegistered<T>();
            var parser = LuminPackParseProvider.GetParser<T>();
            
            (parser as LuminData<T>)!.LoadData(data);
            
            return (IDataParser<T>)parser;
        }
        
        public static IDataParser<T> LoadData<T>(byte[] data)
        {
            EnsureParserRegistered<T>();
            var parser = LuminPackParseProvider.GetParser<T>();
            
            (parser as LuminData<T>)!.LoadData(data);
            return (IDataParser<T>)parser;
        }

        /// <summary>
        /// 加载数据（异步）
        /// </summary>
        public static IDataParserAsync<T> LoadDataAsync<T>(in T data)
        {
            EnsureParserRegistered<T>();
            var parser = LuminPackParseProvider.GetParser<T>();
            
            (parser as LuminData<T>)!.LoadData(data);
            return (IDataParserAsync<T>)parser;
            
        }
        
        public static IDataParserAsync<T> LoadDataAsync<T>(in byte[] data)
        {
            EnsureParserRegistered<T>();
            var parser = LuminPackParseProvider.GetParser<T>();
            
            (parser as LuminData<T>)!.LoadData(data);
            return (IDataParserAsync<T>)parser;
        }

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
        
        #endregion
        
        #region Private Helper Methods
        
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
        private static byte[] SerializeUnmanaged<T>(in T? value)
        {
            byte[] array = AllocateUninitializedArray<byte>(Unsafe.SizeOf<T>());
            Unsafe.WriteUnaligned(ref GetArrayDataReference(array), value);
            return array;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] SerializeFixedSize<T>(in T? value, int elementSize)
        {
            var buffer = new byte[(value is null) ? 1 : elementSize];
            var bufferWriter = new FixedArrayBufferWriter(buffer);
            var writer = new LuminPackWriter(bufferWriter, LuminPackWriterOptionalState.NullOption);
            writer.WriteValue(value);
            
            var result = AllocateUninitializedArray<byte>(writer.CurrentIndex);
            var dest = result.AsSpan();
            
            writer.GetSpan().CopyTo(dest);
            
            return result;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] SerializeUnmanagedArray<T>(in T? value, int elementSize)
        {
            if (value is null) return LuminPackCode.NullCollectionData.ToArray();
            
            var srcArray = (Array)(object)value;
            if (srcArray.Length is 0) return "\0\0\0\0"u8.ToArray();
            
            int length = srcArray.Length;
            int dataSize = elementSize * length;
            byte[] destArray = AllocateUninitializedArray<byte>(dataSize + 4);
            
            ref byte head = ref GetArrayDataReference(destArray);
            Unsafe.WriteUnaligned(ref head, length);
            Unsafe.CopyBlockUnaligned(
                ref Unsafe.Add(ref head, 4), 
                ref GetArrayDataReference(srcArray), 
                (uint)dataSize);
            return destArray;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] SerializeComplexType<T>(in T? value, LuminPackSerializerOption? option)
        {
            var writerBuffer = ReusableLinkedArrayBufferWriterPool.Rent();
            
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
                ReusableLinkedArrayBufferWriterPool.Return(writerBuffer);
                state.Reset();
            }
            
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DeserializeUnmanaged<T>(ReadOnlySpan<byte> buffer, scoped ref T? value)
        {
            int size = Unsafe.SizeOf<T>();
            if (buffer.Length < size)
            {
                LuminPackExceptionHelper.ThrowInvalidRange(size, buffer.Length);
            }
            value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(buffer));
            return size;
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

namespace LuminPack.Data
{
    [Preserve]
    public abstract class LuminData<T> : LuminPackParser<T>, IDataParser<T>, IDataParserAsync<T>
    {
        protected T? _dataRef;
        protected byte[]? _data;
        protected static readonly Type Type = typeof(T);

        [Preserve]
        public IDataParser<T> GetDataParser() => this;

        [Preserve]
        public IDataParserAsync<T> GetDataParserAsync() => this;

        [Preserve]
        public IDataParser<T> LoadData(T? data)
        {
            _dataRef = data;
            return this;
        }

        [Preserve]
        public IDataParser<T> LoadData(byte[]? data)
        {
            _data = data;
            return this;
        }
        
        [Preserve]
        public IDataParserAsync<T> LoadDataAsync(T? data)
        {
            _dataRef = data;
            return this;
        }

        [Preserve]
        public IDataParserAsync<T> LoadDataAsync(byte[]? data)
        {
            _data = data;
            return this;
        }

        [Preserve]
        public abstract void RegisterParser();
        
        [Preserve]
        public abstract byte[] Serialize();
        
        [Preserve]
        public abstract T? Deserialize();
        
        [Preserve]
        public abstract T? Deserialize(T? data);
        
        [Preserve]
        public abstract Task<byte[]> SerializeAsync();
        
        [Preserve]
        public abstract Task<T?> DeserializeAsync();
        
        [Preserve]
        public abstract Task<T?> DeserializeAsync(T? data);

        [Preserve]
        public virtual int Sizeof()
        {
            var totalLength = 0;
            var evaluator = new LuminPackEvaluator(ref totalLength);
            CalculateOffset(ref evaluator, ref _dataRef);
            return evaluator.Value;
        }
    }
    
}