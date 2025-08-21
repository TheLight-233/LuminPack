using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#if NET7_0_OR_GREATER
using System.Text.Unicode;
#endif

using LuminPack.Interface;
using LuminPack.Option;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Utility;
using static LuminPack.Code.LuminPackMarshal;


namespace LuminPack.Core
{
    [StructLayout(LayoutKind.Auto)]
    public ref struct LuminPackReader
    {
        
        private Span<byte> _bufferReference;
        private int _currentIndex;

        private readonly LuminPackReaderOptionalState _optionState;

        private readonly bool SerializeStringAsUtf8;
        private readonly bool SerializeStringRecordAsToken;

        public LuminPackReaderOptionalState OptionState => _optionState;
        public LuminPackSerializerOption Option => _optionState.Option;

        public LuminPackReader(LuminPackReaderOptionalState? option = null)
        {

            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            _bufferReference = default;
            _currentIndex = 0;
            
            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }

        public LuminPackReader(ref Span<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            _bufferReference = bufferReference;
            _currentIndex = 0;

            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }
        
        public LuminPackReader(ref ReadOnlySpan<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            _bufferReference = LuminPackMarshal.CreateSpan(ref GetNonNullPinnableReference(bufferReference), bufferReference.Length);
            _currentIndex = 0;

            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }
        
        public LuminPackReader(ref ReadOnlySequence<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            var span = bufferReference.FirstSpan;
            _bufferReference = LuminPackMarshal.CreateSpan(ref GetNonNullPinnableReference(span), span.Length);
            _currentIndex = 0;

            SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
        }

        /// <summary>
        /// 获取Span数组指针
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetSpanReference(int index)
        {
            return ref _bufferReference[index];
        }

        /// <summary>
        /// 移动指针
        /// </summary>
        /// <param name="count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            if (_bufferReference.Length < count)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(count);

            _currentIndex += count;
            
        }

        /// <summary>
        /// 设置Span
        /// </summary>
        /// <param name="span"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            _bufferReference = span;
        }

        /// <summary>
        /// 刷新Writer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            _bufferReference = null;
            _currentIndex = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILuminPackableParser<T> GetParser<T>()
        {
            return LuminPackParseProvider.GetParser<T>();
        }

        /// <summary>
        /// 获取当前数组指针
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetCurrentSpanReference()
        {
            return ref _bufferReference[_currentIndex];
        }

        /// <summary>
        /// 获取Reader持有的Span
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return _bufferReference;
        }
        
        /// <summary>
        /// 获取当前SpanOffset
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref int GetCurrentSpanOffset()
        {
            //int* ptr = (int*)Unsafe.AsPointer(ref _currentIndex);
            
            return ref _currentIndex;
        }
        
        /// <summary>
        /// 获取当前SpanOffset
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCurrentSpanIndex()
        {
            return _currentIndex;
        }
        
        /// <summary>
        /// 刷新当前SpanOffset
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushCurrentIndex(int offset)
        {
            _currentIndex = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsReferenceOrContainsReferences<T>()
        {
            return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> CreateSpan<T>(scoped ref T reference, int length)
        {
            return LuminPackMarshal.CreateSpan(ref reference, length);
        }

        /// <summary>
        /// 反序列化集合长度Head
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCollectionHead(ref int index, out int length)
        {
            length = Unsafe.ReadUnaligned<int>(ref GetSpanReference(index));

            return length is not LuminPackCode.NullCollection;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullObject(ref int index)
        {
            return GetSpanReference(index) == LuminPackCode.NullObject;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullCollection(ref int index)
        {
            return Unsafe.ReadUnaligned<int>(ref GetSpanReference(index)) == LuminPackCode.NullCollection;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullString(ref int index)
        {
            return SerializeStringRecordAsToken 
                ? GetSpanReference(index) == 0 
                : Unsafe.ReadUnaligned<int>(ref GetSpanReference(index)) == LuminPackCode.NullCollection;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadObjectHead(ref int index)
        {
            var code = GetSpanReference(index);

            return code is not LuminPackCode.NullObject;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadObjectHead(ref int index, out byte code)
        {
            code = GetSpanReference(index);

            return code is not LuminPackCode.NullObject;
        }

        /// <summary>
        /// 反序列化字符串长度(Length模式)
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadStringHead(ref int index, out int length)
        {
            if (SerializeStringRecordAsToken)
                LuminPackExceptionHelper.ThrowFailedParseStringWithLength();

            length = Unsafe.ReadUnaligned<int>(ref GetSpanReference(index));

            if (length > _bufferReference.Length)
                LuminPackExceptionHelper.ThrowInSufficientBuffer(length);

            return length is not LuminPackCode.NullCollection;
        }

        /// <summary>
        /// 获取二进制字符串编码长度
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadStringLength(ref int index, out int length)
        {
            if (SerializeStringRecordAsToken)
            {
                length = 0;
                while (_bufferReference[index + length] != 0)
                {
                    length++;
                }
            }
            else
            {
                TryReadStringHead(ref index, out length);
            }

        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int StringRecordLength()
        {
            return SerializeStringRecordAsToken ? 1 : SerializeStringAsUtf8 ? 8 : 4;
        }

        /// <summary>
        /// 反序列化字符串
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString() => (SerializeStringAsUtf8, SerializeStringRecordAsToken) switch
        {
            (true, true) => ReadUtf8StringWithToken(_currentIndex),
            (true, false) => ReadUtf8StringWithLength(_currentIndex),
            (false, true) => ReadUtf16StringWithToken(_currentIndex),
            (false, false) => ReadUtf16StringWithLength(_currentIndex),
        };
        
        /// <summary>
        /// 反序列化字符串(已知长度)
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(int length) => (SerializeStringAsUtf8, SerializeStringRecordAsToken) switch
        {
            (true, true) => ReadUtf8StringWithToken(_currentIndex, length),
            (true, false) => ReadUtf8StringWithLength(_currentIndex, length),
            (false, true) => ReadUtf16StringWithToken(_currentIndex, length),
            (false, false) => ReadUtf16StringWithLength(_currentIndex, length),
        };

        /// <summary>
        /// 反序列化字符串
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(ref int index) => (SerializeStringAsUtf8, SerializeStringRecordAsToken) switch
        {
            (true, true) => ReadUtf8StringWithToken(index),
            (true, false) => ReadUtf8StringWithLength(index),
            (false, true) => ReadUtf16StringWithToken(index),
            (false, false) => ReadUtf16StringWithLength(index),
        };

        /// <summary>
        /// 反序列化字符串(已知长度)
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(int index, int length) => (SerializeStringAsUtf8, SerializeStringRecordAsToken) switch
        {
            (true, true) => ReadUtf8StringWithToken(index, length),
            (true, false) => ReadUtf8StringWithLength(index, length),
            (false, true) => ReadUtf16StringWithToken(index, length),
            (false, false) => ReadUtf16StringWithLength(index, length),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithToken(int index)
        {
            ReadStringLength(ref index, out var length);

            return ReadUtf8StringWithLength(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithToken(int index, int length)
        {
            return ReadUtf8StringWithLength(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithToken(int index)
        {
            ReadStringLength(ref index, out var length);

            return ReadUtf16StringWithLength(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithToken(int index, int length)
        {
            return ReadUtf16StringWithLength(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithLength(int index)
        {
            ReadStringLength(ref index, out var length);

            return ReadUtf8StringWithLength(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithLength(int index, int length)
        {
            if (length is 0) return null;

            ref var spanRef = ref GetSpanReference(index + 4);
            
            var utf16Length = Unsafe.ReadUnaligned<int>(ref spanRef);
            
            if (utf16Length <= 0)
            {
                var src = LuminPackMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref spanRef, 4), length);
                return Encoding.UTF8.GetString(src);
            }
            else
            {

                // regular path, know decoded UTF16 length will gets faster decode result
                unsafe
                {
                    fixed (byte* p = &Unsafe.Add(ref spanRef, 4))
                    {
                        
                        return string.Create(utf16Length, ((IntPtr)p, length), static (dest, state) =>
                        {
                            var src = LuminPackMarshal.CreateSpan(ref Unsafe.AsRef<byte>((byte*)state.Item1), state.Item2);
                            var status = StringSerializer.Deserialize(src, dest, out var bytesRead, out var charsWritten,
                                replaceInvalidSequences: false);
                            if (status != OperationStatus.Done)
                            {
                                LuminPackExceptionHelper.ThrowFailedEncoding(status);
                            }
                        });
                    }
                }
            }

        }
    

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithLength(int index)
        {
            ReadStringLength(ref index, out var length);

            return ReadUtf16StringWithLength(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithLength(int index, int length)
        {
            if (length is 0) return null;

            ref var spanRef = ref GetSpanReference(index + 4);
            
            var utf16Length = length / 2;
            
            if (length % 2 is not 0)
            {
                var src = LuminPackMarshal.CreateReadOnlySpan(ref spanRef, length);
                return Encoding.Unicode.GetString(src);
            }
            else
            {
                unsafe
                {
                    fixed (byte* p = &spanRef)
                    {
                        return string.Create(utf16Length, ((IntPtr)p, length), static (dest, state) =>
                        {
                            var src = LuminPackMarshal.CreateSpan(ref Unsafe.AsRef<byte>((byte*)state.Item1), state.Item2);
                            var status = StringSerializer.Deserialize(src, dest, out var bytesRead, out var charsWritten,
                                replaceInvalidSequences: false);
                            if (status != OperationStatus.Done)
                            {
                                LuminPackExceptionHelper.ThrowFailedEncoding(status);
                            }
                        });
                    }
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValue<T>(scoped ref T? value)
        {
            GetParser<T>().Deserialize(ref this, ref value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? ReadValue<T>()
        {
            T? value = default;
            GetParser<T>().Deserialize(ref this, ref value);
            return value;
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadValueWithParser<TParser, T>(TParser parser, scoped ref T? value)
            where TParser : ILuminPackableParser<T>
        {
            parser.Deserialize(ref this, ref value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? ReadValueWithParser<TParser, T>(TParser parser)
            where TParser : ILuminPackableParser<T>
        {
            T? value = default;
            parser.Deserialize(ref this, ref value);
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadObjectHeader(ref int index, out byte memberCount)
        {
            memberCount = _bufferReference[index];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekUnionHeader(ref int index, out ushort tag)
        {
            var firstTag = GetSpanReference(index);
            if (firstTag < LuminPackCode.WideTag)
            {
                tag = firstTag;
                index += 1;
                return true;
            }
            else if (firstTag == LuminPackCode.WideTag)
            {
                tag = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref firstTag, 1));
                index += 3;
                return true;
            }
            else
            {
                tag = 0;
                index += 1;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T?[]? ReadArray<T>()
        {
            T?[]? value = default;
            ReadArray(ref value);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadArray<T>(scoped ref T[]? array)
        {
            var index = _currentIndex;

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedArray(ref index, ref array!, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (!TryReadCollectionHead(ref index, out var length))
            {
                array = null;
                
                Advance(4);
                
                return;
            }

            Advance(4);
            
            if (length is 0)
            {
                array = Array.Empty<T>();
                
                return;
            }
            
            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }

            var parser = GetParser<T>();
            
            var span = array.AsSpan();
            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref span[i]!);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadArray<T>(scoped ref int index, ref T[]? array)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedArray(ref index, ref array!, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (!TryReadCollectionHead(ref index, out var length))
            {
                array = null;
                
                Advance(4);
                
                return;
            }

            Advance(4);
            
            if (length is 0)
            {
                array = Array.Empty<T>();
                
                return;
            }
            
            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }

            var parser = GetParser<T>();
            
            var span = array.AsSpan();
            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref span[i]!);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadSpan<T>(scoped ref Span<T?> span)
        {
            var index = _currentIndex;

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedSpan(ref index, ref span, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (!TryReadCollectionHead(ref index, out var length))
            {
                span = null;
                
                Advance(4);
                
                return;
            }

            Advance(4);
            
            if (length is 0)
            {
                span = Array.Empty<T>();
                
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }

            var parser = GetParser<T>();
            

            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref span[i]);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadSpan<T>(scoped ref int index, scoped ref Span<T?> span)
        {

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedSpan(ref index, ref span, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (!TryReadCollectionHead(ref index, out var length))
            {
                span = null;
                
                Advance(4);
                
                return;
            }
            
            Advance(4);

            if (length is 0)
            {
                span = Array.Empty<T>();
                
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }

            var parser = GetParser<T>();
            

            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref span[i]);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadArray<T>(scoped ref int index, int length, ref T[]? array)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedArray(ref index, ref array!, length, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                
                return;
            }
            
            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }

            var parser = GetParser<T>();
            
            var span = array.AsSpan();
            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref span[i]!);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadSpan<T>(scoped ref int index, int length, scoped ref Span<T?> span)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                DangerousReadUnmanagedSpan(ref index, ref span, length, out var offset);
                
                Advance(offset);
                
                return;
            }

            if (length is 0)
            {
                span = Array.Empty<T>();
                
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }

            var parser = GetParser<T>();
            

            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref this, ref span[i]);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref T[]? array)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref _currentIndex, ref array!);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, int length)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!, length, out var offset);
            Advance(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, int length)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span, length, out var offset);
            Advance(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, int length, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!, length, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, int length, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span, length, out spanOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArray<T>(scoped ref T[]? array)
        {

            var index = _currentIndex;

            if (!TryReadCollectionHead(ref index, out var length))
            {
                array = null;
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                return;
            }

            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }

            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.CopyBlockUnaligned(ref dest, ref GetSpanReference(offset), (uint)(length * Unsafe.SizeOf<T>()));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpan<T>(scoped ref Span<T> span)
        {
            var index = _currentIndex;
            
            if (!TryReadCollectionHead(ref index, out var length))
            {
                span = default;
                return;
            }

            if (length is 0)
            {
                span = Span<T>.Empty;
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }

            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());

            Unsafe.CopyBlockUnaligned(ref dest, ref GetSpanReference(offset), (uint)(length * Unsafe.SizeOf<T>()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArray<T>(scoped ref int index, ref T[]? array)
        {

            if (!TryReadCollectionHead(ref index, out var length))
            {
                array = null;
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                return;
            }

            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }
            
            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);

            Unsafe.CopyBlockUnaligned(ref dest, ref GetSpanReference(offset), (uint)(length * Unsafe.SizeOf<T>()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span)
        {

            if (!TryReadCollectionHead(ref index, out var length))
            {
                span = default;
                return;
            }

            if (length is 0)
            {
                span = Span<T>.Empty;
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }

            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());

            Unsafe.CopyBlockUnaligned(ref dest, ref GetSpanReference(offset), (uint)(length * Unsafe.SizeOf<T>()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, out int spanOffset)
        {

            if (!TryReadCollectionHead(ref index, out var length))
            {
                array = null;
                spanOffset = 4;
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                spanOffset = 4;
                return;
            }

            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }
            
            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);

            var srcLength = length * Unsafe.SizeOf<T>();
            
            Unsafe.CopyBlockUnaligned(ref dest, ref GetSpanReference(offset), (uint)srcLength);

            spanOffset = srcLength + 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, out int spanOffset)
        {

            if (!TryReadCollectionHead(ref index, out var length))
            {
                span = default;
                spanOffset = 4;
                return;
            }

            if (length is 0)
            {
                span = Span<T>.Empty;
                spanOffset = 4;
                return;
            }

            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }
            
            var offset = index + 4;

            ref var dest = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());
            
            var srcLength = length * Unsafe.SizeOf<T>();

            Unsafe.CopyBlockUnaligned(ref dest, ref GetSpanReference(offset), (uint)srcLength);

            spanOffset = srcLength + 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, int length)
        {
            if (length is LuminPackCode.NullCollection)
            {
                array = null;
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                return;
            }

            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }
            
            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);

            var offset = length * Unsafe.SizeOf<T>();
            
            Unsafe.CopyBlockUnaligned(ref dest, ref GetSpanReference(index), (uint)offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, int length)
        {

            if (length is LuminPackCode.NullCollection)
            {
                span = default;
                return;
            }

            if (length is 0)
            {
                span = Span<T>.Empty;
                return;
            }
            
            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }
            
            var offset = length * Unsafe.SizeOf<T>();
            
            ref var dest = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());

            Unsafe.CopyBlockUnaligned(ref dest, ref GetSpanReference(index), (uint)offset);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, int length, out int spanOffset)
        {
            if (length is LuminPackCode.NullCollection)
            {
                array = null;
                spanOffset = 0;
                return;
            }

            if (length is 0)
            {
                array = Array.Empty<T>();
                spanOffset = 0;
                return;
            }

            if (array is null || array.Length != length)
            {
                array = AllocateUninitializedArray<T>(length);
            }
            
            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);
            
            var offset = length * Unsafe.SizeOf<T>();

            Unsafe.CopyBlockUnaligned(ref dest, ref GetSpanReference(index), (uint)offset);

            spanOffset = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, int length,
            out int spanOffset)
        {
            if (length is LuminPackCode.NullCollection)
            {
                span = default;
                spanOffset = 0;
                return;
            }

            if (length is 0)
            {
                span = Span<T>.Empty;
                spanOffset = 0;
                return;
            }

            if (span.Length != length)
            {
                span = AllocateUninitializedArray<T>(length);
            }
            
            ref var buffer = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());

            var offset = length * Unsafe.SizeOf<T>();
            
            Unsafe.CopyBlockUnaligned(ref buffer, ref GetSpanReference(index), (uint)offset);

            spanOffset = offset;
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1"></param>
        /// <typeparam name="T1"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1>(out T1 value1)
            where T1 : unmanaged
        {
            var index = _currentIndex;

            ref var spanRef = ref GetSpanReference(index);
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);

            return Unsafe.SizeOf<T1>();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T1 ReadUnmanaged<T1>()
            where T1 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>();
            ref var spanRef = ref GetSpanReference(_currentIndex);
            var value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            Advance(size);
            return value1;
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value1"></param>
        /// <typeparam name="T1"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1>(ref int index, out T1 value1)
            where T1 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
    
            return Unsafe.SizeOf<T1>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2>(ref int index, out T1 value1, out T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3>(ref int index, out T1 value1, out T2 value2, out T3 value3)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T13>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
            where T14 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T13>();
            value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T14>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
            where T14 : unmanaged
            where T15 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T13>();
            value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T14>();
            value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T15>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1>(ref int index, out T1 value1)
            where T1 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2>(ref int index, out T1 value1, out T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3>(ref int index, out T1 value1, out T2 value2, out T3 value3)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
            where T14 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T13>();
            value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
            where T8 : unmanaged
            where T9 : unmanaged
            where T10 : unmanaged
            where T11 : unmanaged
            where T12 : unmanaged
            where T13 : unmanaged
            where T14 : unmanaged
            where T15 : unmanaged
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T13>();
            value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T14>();
            value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, offset));
            
        }
        
        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1"></param>
        /// <typeparam name="T1"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1>(out T1 value1)
        {
            var index = _currentIndex;

            ref var spanRef = ref GetSpanReference(index);
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);

            return Unsafe.SizeOf<T1>();
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value1"></param>
        /// <typeparam name="T1"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1>(ref int index, out T1 value1)
        {
            ref var spanRef = ref GetSpanReference(index);
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
    
            return Unsafe.SizeOf<T1>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2>(ref int index, out T1 value1, out T2 value2)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3>(ref int index, out T1 value1, out T2 value2, out T3 value3)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T13>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T13>();
            value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T14>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T13>();
            value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T14>();
            value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T15>();
    
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1>(ref int index, out T1 value1)
        {
            ref var spanRef = ref GetSpanReference(index);
            
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2>(ref int index, out T1 value1, out T2 value2)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3>(ref int index, out T1 value1, out T2 value2, out T3 value3)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T13>();
            value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, offset));
    
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
        {
            ref var spanRef = ref GetSpanReference(index);
            int offset = 0;
            value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            offset += Unsafe.SizeOf<T1>();
            value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T2>();
            value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T3>();
            value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T4>();
            value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T5>();
            value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T6>();
            value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T7>();
            value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T8>();
            value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T9>();
            value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T10>();
            value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T11>();
            value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T12>();
            value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T13>();
            value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, offset));
            offset += Unsafe.SizeOf<T14>();
            value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, offset));
    
        }
        
        public byte ReadVarIntByte()
        {
            ReadUnmanaged(out sbyte typeCode);

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return checked((byte)ReadUnmanaged<sbyte>());
                case VarIntCodes.UInt16:
                    return checked((byte)ReadUnmanaged<byte>());
                case VarIntCodes.Int16:
                    return checked((byte)ReadUnmanaged<short>());
                case VarIntCodes.UInt32:
                    return checked((byte)ReadUnmanaged<uint>());
                case VarIntCodes.Int32:
                    return checked((byte)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return checked((byte)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((byte)ReadUnmanaged<long>());
                default:
                    return checked((byte)typeCode);
            }
        }

        public sbyte ReadVarIntSByte()
        {
            ReadUnmanaged(out sbyte typeCode);

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return checked((sbyte)ReadUnmanaged<byte>());
                case VarIntCodes.SByte:
                    return ReadUnmanaged<sbyte>();
                case VarIntCodes.UInt16:
                    return checked((sbyte)ReadUnmanaged<ushort>());
                case VarIntCodes.Int16:
                    return checked((sbyte)ReadUnmanaged<short>());
                case VarIntCodes.UInt32:
                    return checked((sbyte)ReadUnmanaged<uint>());
                case VarIntCodes.Int32:
                    return checked((sbyte)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return checked((sbyte)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((sbyte)ReadUnmanaged<long>());
                default:
                    return typeCode;
            }
        }

        public ushort ReadVarIntUInt16()
        {
            ReadUnmanaged(out sbyte typeCode);

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return checked((ushort)ReadUnmanaged<sbyte>());
                case VarIntCodes.UInt16:
                    return ReadUnmanaged<ushort>();
                case VarIntCodes.Int16:
                    return checked((ushort)ReadUnmanaged<short>());
                case VarIntCodes.UInt32:
                    return checked((ushort)ReadUnmanaged<uint>());
                case VarIntCodes.Int32:
                    return checked((ushort)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return checked((ushort)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((ushort)ReadUnmanaged<long>());
                default:
                    return checked((ushort)typeCode);
            }
        }

        public short ReadVarIntInt16()
        {
            ReadUnmanaged(out sbyte typeCode);

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return ReadUnmanaged<sbyte>();
                case VarIntCodes.UInt16:
                    return checked((short)ReadUnmanaged<ushort>());
                case VarIntCodes.Int16:
                    return ReadUnmanaged<short>();
                case VarIntCodes.UInt32:
                    return checked((short)ReadUnmanaged<uint>());
                case VarIntCodes.Int32:
                    return checked((short)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return checked((short)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((short)ReadUnmanaged<long>());
                default:
                    return typeCode;
            }
        }

        public uint ReadVarIntUInt32()
        {
            ReadUnmanaged(out sbyte typeCode);

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return checked((uint)ReadUnmanaged<sbyte>());
                case VarIntCodes.UInt16:
                    return ReadUnmanaged<ushort>();
                case VarIntCodes.Int16:
                    return checked((uint)ReadUnmanaged<short>());
                case VarIntCodes.UInt32:
                    return ReadUnmanaged<uint>();
                case VarIntCodes.Int32:
                    return checked((uint)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return checked((uint)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((uint)ReadUnmanaged<long>());
                default:
                    return checked((uint)typeCode);
            }
        }

        public int ReadVarIntInt32()
        {
            ReadUnmanaged(out sbyte typeCode);

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return ReadUnmanaged<sbyte>();
                case VarIntCodes.UInt16:
                    return ReadUnmanaged<ushort>();
                case VarIntCodes.Int16:
                    return ReadUnmanaged<short>();
                case VarIntCodes.UInt32:
                    return checked((int)ReadUnmanaged<uint>());
                case VarIntCodes.Int32:
                    return ReadUnmanaged<int>();
                case VarIntCodes.UInt64:
                    return checked((int)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return checked((int)ReadUnmanaged<long>());
                default:
                    return typeCode;
            }
        }

        public ulong ReadVarIntUInt64()
        {
            ReadUnmanaged(out sbyte typeCode);

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return checked((ulong)ReadUnmanaged<sbyte>());
                case VarIntCodes.UInt16:
                    return ReadUnmanaged<ushort>();
                case VarIntCodes.Int16:
                    return checked((ulong)ReadUnmanaged<short>());
                case VarIntCodes.UInt32:
                    return ReadUnmanaged<uint>();
                case VarIntCodes.Int32:
                    return checked((ulong)ReadUnmanaged<int>());
                case VarIntCodes.UInt64:
                    return ReadUnmanaged<ulong>();
                case VarIntCodes.Int64:
                    return checked((ulong)ReadUnmanaged<long>());
                default:
                    return checked((ulong)typeCode);
            }
        }

        public long ReadVarIntInt64()
        {
            ReadUnmanaged(out sbyte typeCode);

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return ReadUnmanaged<sbyte>();
                case VarIntCodes.UInt16:
                    return ReadUnmanaged<ushort>();
                case VarIntCodes.Int16:
                    return ReadUnmanaged<short>();
                case VarIntCodes.UInt32:
                    return ReadUnmanaged<uint>();
                case VarIntCodes.Int32:
                    return ReadUnmanaged<int>();
                case VarIntCodes.UInt64:
                    return checked((long)ReadUnmanaged<ulong>());
                case VarIntCodes.Int64:
                    return ReadUnmanaged<long>();
                default:
                    return typeCode;
            }
        }
    }
}