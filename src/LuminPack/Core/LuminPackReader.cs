using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Numerics;
using System.Text.Unicode;
#endif

using LuminPack.Option;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Utility;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack.Core
{
    /// <summary>Reads LuminPack binary values while tracking the current input position and operation state.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref partial struct LuminPackReader
    {

        private Span<byte> _bufferReference;
#if NET8_0_OR_GREATER
        internal ref byte _bufferStart;
#else
        internal byte* _bufferStart;
#endif
        private int _currentIndex;

        private readonly LuminPackReaderOptionalState _optionState;


        /// <summary>Gets the option state.</summary>
        public LuminPackReaderOptionalState OptionState => _optionState;

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name="option">The shared reference-tracking state, or null for the default operation state.</param>
        public LuminPackReader(LuminPackReaderOptionalState? option = null)
        {

            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            _bufferReference = default;
            _currentIndex = 0;


        }

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name="bufferReference">The buffer reference used by this operation.</param>
        /// <param name="option">The shared reference-tracking state, or null for the default operation state.</param>
        public LuminPackReader(ref Span<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            _bufferReference = bufferReference;
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;


        }

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name="bufferReference">The buffer reference used by this operation.</param>
        /// <param name="option">The shared reference-tracking state, or null for the default operation state.</param>
        public LuminPackReader(ref ReadOnlySpan<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            _bufferReference = LuminPackMarshal.CreateSpan(ref GetNonNullPinnableReference(bufferReference), bufferReference.Length);
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;


        }

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name="bufferReference">The buffer reference used by this operation.</param>
        /// <param name="option">The shared reference-tracking state, or null for the default operation state.</param>
        public LuminPackReader(ref ReadOnlySequence<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _optionState = option ?? LuminPackReaderOptionalState.NullOption;
            _bufferReference = bufferReference.IsSingleSegment
                ? LuminPackMarshal.CreateSpan(
                    ref GetNonNullPinnableReference(bufferReference.FirstSpan),
                    bufferReference.FirstSpan.Length)
                : bufferReference.ToArray();
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;


        }

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name="bufferWriter">The writer receiving the output data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LuminPackReader(LuminBufferWriter bufferWriter)
        {
            _optionState = bufferWriter.ReaderState;
            _bufferReference = bufferWriter.GetSpan();
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;


        }


        /// <summary>
        /// Returns a reference to the span's first element.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetSpanReference(int index) =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

        /// <summary>Validates that the requested bytes remain in the input buffer.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("DEBUG")]
        public void EnsureReadable(int index, int count)
        {
            if ((uint)index > (uint)_bufferReference.Length ||
                (uint)count > (uint)(_bufferReference.Length - index))
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(index + count));
        }

        /// <summary>Validates a generated fixed-width collection's complete dynamic payload in every build.</summary>
        /// <param name="index">The absolute beginning of the collection header or payload.</param>
        /// <param name="byteLength">The checked number of bytes required by the collection boundary.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCollectionPayload(int index, int byteLength) => ValidateCollectionPayload(index, byteLength);

        /// <summary>Validates a raw collection copy before accessing its complete payload in every build.</summary>
        /// <param name="index">The first payload byte after the collection header.</param>
        /// <param name="byteLength">The checked number of payload bytes to copy.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateCollectionPayload(int index, int byteLength)
        {
            if ((uint)index > (uint)_bufferReference.Length ||
                (uint)byteLength > (uint)(_bufferReference.Length - index))
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(index + byteLength));
        }

        /// <summary>
        /// Advances the target pointer.
        /// </summary>
        /// <param name="count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            EnsureReadable(_currentIndex, count);
            _currentIndex += count;

        }

        /// <summary>
        /// Replaces the current buffer span.
        /// </summary>
        /// <param name="span"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            _bufferReference = span;
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
        }

        /// <summary>
        /// Releases the input span and resets its cursor without modifying caller memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            _bufferReference = null;
#if NET8_0_OR_GREATER
            _bufferStart = ref Unsafe.NullRef<byte>();
#else
            _bufferStart = default;
#endif
            _currentIndex = 0;
        }

        /// <summary>
        /// Obtains a reference to the current array data.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetCurrentSpanReference() =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

        /// <summary>
        /// Returns the span currently held by the reader.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return _bufferReference;
        }

        /// <summary>
        /// Returns a reference to the current buffer offset.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref int GetCurrentSpanOffset()
        {
            //int* ptr = (int*)Unsafe.AsPointer(ref _currentIndex);

            return ref _currentIndex;
        }

        /// <summary>
        /// Returns a reference to the current buffer offset.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCurrentSpanIndex()
        {
            return _currentIndex;
        }

        /// <summary>
        /// Publishes the current buffer offset.
        /// </summary>
        /// <returns></returns>
        /// <param name="offset">The position at which the operation starts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushCurrentIndex(int offset)
        {
            _currentIndex = offset;
        }

        /// <summary>Determines whether the value type requires managed reference handling.</summary>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsReferenceOrContainsReferences<T>()
        {
            return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        }

        /// <summary>Creates span from the supplied input.</summary>
        /// <param name="reference">The reference used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> CreateSpan<T>(scoped ref T reference, int length)
        {
            return LuminPackMarshal.CreateSpan(ref reference, length);
        }

        /// <summary>
        /// DeserializeThe collection length.Head
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCollectionHead(ref int index, out int length)
        {
            EnsureReadable(index, sizeof(int));
#if NET8_0_OR_GREATER
            length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index));
#else
            length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index));
#endif

            if (length < LuminPackCode.NullCollection)
                LuminPackExceptionHelper.ThrowInvalidCollectionLength(length);

            return length is not LuminPackCode.NullCollection;
        }

        /// <summary>Performs the peek is null object step for the current operation.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullObject(ref int index)
        {
#if NET8_0_OR_GREATER
            return Unsafe.Add(ref _bufferStart, (nint)(uint)index) == LuminPackCode.NullObject;
#else
            return Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) == LuminPackCode.NullObject;
#endif
        }

        /// <summary>Performs the peek is null collection step for the current operation.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullCollection(ref int index)
        {
#if NET8_0_OR_GREATER
            return Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index)) == LuminPackCode.NullCollection;
#else
            return Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index)) == LuminPackCode.NullCollection;
#endif
        }

        /// <summary>Performs the peek is null string step for the current operation.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullString(ref int index)
        { return PeekIsNullString<global::LuminPack.Option.LuminPackDefaultOptions>(ref index); }

        /// <summary>Attempts to read object head and reports whether it succeeded.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadObjectHead(ref int index)
        {
#if NET8_0_OR_GREATER
            var code = Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            var code = Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            return code is not LuminPackCode.NullObject;
        }

        /// <summary>Attempts to read object head and reports whether it succeeded.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="code">Receives the code produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadObjectHead(ref int index, out byte code)
        {
#if NET8_0_OR_GREATER
            code = Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            code = Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            return code is not LuminPackCode.NullObject;
        }

        /// <summary>
        /// Reads a string length header in length-recording mode.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadStringHead(ref int index, out int length)
        { return TryReadStringHead<global::LuminPack.Option.LuminPackDefaultOptions>(ref index, out length); }

        /// <summary>Validates string length before the operation continues.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateStringLength(int index, int length)
        { ValidateStringLength<global::LuminPack.Option.LuminPackDefaultOptions>(index, length); }

        /// <summary>
        /// Calculates the binary string encoding length.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadStringLength(ref int index, out int length)
        { ReadStringLength<global::LuminPack.Option.LuminPackDefaultOptions>(ref index, out length); }

        /// <summary>Returns the size of the string header used by the current recording mode.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int StringRecordLength()
        { return StringRecordLength<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>
        /// Reads one string record and advances the supplied absolute offset.
        /// Intended for source-generated parsers so the JIT sees a single call and
        /// does not have to keep the offset live across four string helpers.
        /// </summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadStringAndAdvance(ref int index)
        { return ReadStringAndAdvance<global::LuminPack.Option.LuminPackDefaultOptions>(ref index); }

        /// <summary>Reads token string and advance from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="isUtf8">The is utf8 used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadTokenStringAndAdvance(ref int index, bool isUtf8)
        { return ReadTokenStringAndAdvance<global::LuminPack.Option.LuminPackDefaultOptions>(ref index, isUtf8); }

        /// <summary>
        /// Generator-only fast path for a list that is known to be newly allocated. It publishes
        /// the final count without running the reusable-list capacity and clearing logic.
        /// </summary>
        /// <param name="count">The number of entries involved in the operation.</param>
        /// <param name="value">Receives the value produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T?> CreateFreshListSpan<T>(int count, out List<T?> value)
        {
            value = new List<T?>(count);
            return LuminPackMarshal.DangerousGetFreshListArray(value, count).AsSpan(0, count);
        }

        /// <summary>
        /// Array-returning variant for generated reference-element loops. Keeping the backing
        /// array as one managed reference shortens the live range compared with Span&lt;T&gt;.
        /// </summary>
        /// <param name="count">The number of entries involved in the operation.</param>
        /// <param name="value">Receives the value produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T?[] CreateFreshListArray<T>(int count, out List<T?> value)
        {
            value = new List<T?>(count);
            return LuminPackMarshal.DangerousGetFreshListArray(value, count);
        }

        /// <summary>
        /// Reads a complete length-prefixed list of strings and advances an absolute offset.
        /// This is a generated-parser fast path that keeps collection bookkeeping out of
        /// very large parser methods after their JIT inline budget has been exhausted.
        /// </summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<string>? ReadStringListAndAdvance(ref int index)
        { return ReadStringListAndAdvance<global::LuminPack.Option.LuminPackDefaultOptions>(ref index); }

        /// <summary>
        /// Reads a serialized string.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString()
        { return ReadString<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>
        /// Reads a string with a known length.
        /// </summary>
        /// <returns></returns>
        /// <param name="length">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(int length)
        { return ReadString<global::LuminPack.Option.LuminPackDefaultOptions>(length); }

        /// <summary>
        /// Reads a serialized string.
        /// </summary>
        /// <returns></returns>
        /// <param name="index">The position at which the operation starts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(ref int index)
        { return ReadString<global::LuminPack.Option.LuminPackDefaultOptions>(ref index); }

        /// <summary>
        /// Reads a string with a known length.
        /// </summary>
        /// <returns></returns>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString(int index, int length)
        { return ReadString<global::LuminPack.Option.LuminPackDefaultOptions>(index, length); }

        /// <summary>Reads utf8 string with token from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithToken(int index)
        { return ReadUtf8StringWithToken<global::LuminPack.Option.LuminPackDefaultOptions>(index); }

        /// <summary>Reads utf8 string with token from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithToken(int index, int length)
        { return ReadUtf8StringWithToken<global::LuminPack.Option.LuminPackDefaultOptions>(index, length); }

        /// <summary>Reads utf16 string with token from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithToken(int index)
        { return ReadUtf16StringWithToken<global::LuminPack.Option.LuminPackDefaultOptions>(index); }

        /// <summary>Reads utf16 string with token from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithToken(int index, int length)
        { return ReadUtf16StringWithToken<global::LuminPack.Option.LuminPackDefaultOptions>(index, length); }

        /// <summary>Reads utf8 string with length from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithLength(int index)
        { return ReadUtf8StringWithLength<global::LuminPack.Option.LuminPackDefaultOptions>(index); }

        /// <summary>Reads utf8 string with length from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf8StringWithLength(int index, int length)
        { return ReadUtf8StringWithLength<global::LuminPack.Option.LuminPackDefaultOptions>(index, length); }


        /// <summary>Reads utf16 string with length from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithLength(int index)
        { return ReadUtf16StringWithLength<global::LuminPack.Option.LuminPackDefaultOptions>(index); }

        /// <summary>Reads utf16 string with length from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadUtf16StringWithLength(int index, int length)
        { return ReadUtf16StringWithLength<global::LuminPack.Option.LuminPackDefaultOptions>(index, length); }

        /// <summary>Reads object header from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="memberCount">Receives the member count produced by the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadObjectHeader(ref int index, out byte memberCount)
        {
            memberCount = _bufferReference[index];
        }

        /// <summary>Attempts to peek union header and reports whether it succeeded.</summary>
        /// <param name="tag">Receives the tag produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekUnionHeader(out ushort tag)
        {
            EnsureReadable(_currentIndex, 1);
#if NET8_0_OR_GREATER
            tag = Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            tag = Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            Advance(1);
            return tag is not LuminPackCode.NullObject;
        }

        /// <summary>Attempts to peek union header and reports whether it succeeded.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="tag">Receives the tag produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekUnionHeader(ref int index, out ushort tag)
        {
            EnsureReadable(index, 1);
#if NET8_0_OR_GREATER
            tag = Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            tag = Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            index += 1;
            return tag is not LuminPackCode.NullObject;
        }

        /// <summary>Attempts to peek wide union header and reports whether it succeeded.</summary>
        /// <param name="tag">Receives the tag produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekWideUnionHeader(out ushort tag)
        {
            EnsureReadable(_currentIndex, 1);
#if NET8_0_OR_GREATER
            ref var firstTag = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var firstTag = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            if (firstTag < LuminPackCode.WideTag)
            {
                tag = firstTag;
                Advance(1);
                return true;
            }
            else if (firstTag == LuminPackCode.WideTag)
            {
                EnsureReadable(_currentIndex, 3);
                tag = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref firstTag, 1));
                Advance(3);
                return true;
            }
            else
            {
                tag = 0;
                Advance(1);
                return false;
            }
        }

        /// <summary>Attempts to peek wide union header and reports whether it succeeded.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="tag">Receives the tag produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekWideUnionHeader(ref int index, out ushort tag)
        {
            EnsureReadable(index, 1);
#if NET8_0_OR_GREATER
            ref var firstTag = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var firstTag = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            if (firstTag < LuminPackCode.WideTag)
            {
                tag = firstTag;
                index += 1;
                return true;
            }
            else if (firstTag == LuminPackCode.WideTag)
            {
                EnsureReadable(index, 3);
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

        /// <summary>Reads array from the current input.</summary>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T?[]? ReadArray<T>()
        {
            T?[]? value = default;
            ReadArray(ref value);
            return value;
        }

        /// <summary>Reads array from the current input.</summary>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>Reads array from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>Reads span from the current input.</summary>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>Reads span from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>Reads array from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>Reads span from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
        }

        /// <summary>Reads unmanaged array from the current input.</summary>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref T[]? array)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref _currentIndex, ref array!);
        }

        /// <summary>Reads unmanaged array from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!);
        }

        /// <summary>Reads unmanaged span from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span);
        }

        /// <summary>Reads unmanaged array from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!, out spanOffset);
        }

        /// <summary>Reads unmanaged span from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span, out spanOffset);
        }

        /// <summary>Reads unmanaged array from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, int length)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!, length, out var offset);
            Advance(offset);
        }

        /// <summary>Reads unmanaged span from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, int length)
            where T : struct
        {
            DangerousReadUnmanagedSpan(ref index, ref span, length, out var offset);
            Advance(offset);
        }

        /// <summary>Reads unmanaged array from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedArray<T>(scoped ref int index, ref T[]? array, int length, out int spanOffset)
            where T : struct
        {
            DangerousReadUnmanagedArray(ref index, ref array!, length, out spanOffset);
        }

        /// <summary>
        /// Generator-only path for an unmanaged array that was allocated from the already
        /// validated collection length. The reusable-array null/length/allocation checks are
        /// intentionally omitted because the generated caller owns the fresh destination.
        /// </summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadFreshUnmanagedArray<T>(scoped ref int index, T[] array, int length, out int spanOffset)
            where T : struct
        {
            var byteLength = checked(length * Unsafe.SizeOf<T>());
            ValidateCollectionPayload(index, byteLength);
            ref var dest = ref LuminPackMarshal.GetArrayDataReference(array);

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), (uint)byteLength);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), (uint)byteLength);
#endif

            spanOffset = byteLength;
        }

        /// <summary>Reads unmanaged span from the current input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedSpan<T>(scoped ref int index, scoped ref Span<T> span, int length, out int spanOffset)
            where T : struct
        {
            ValidateCollectionPayload(index, checked(length * Unsafe.SizeOf<T>()));
            DangerousReadUnmanagedSpan(ref index, ref span, length, out spanOffset);
        }

        /// <summary>Reads unmanaged array using the caller-validated buffer layout.</summary>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#endif
        }

        /// <summary>Reads unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#endif
        }

        /// <summary>Reads unmanaged array using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#endif
        }

        /// <summary>Reads unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)(length * Unsafe.SizeOf<T>()));
#endif
        }

        /// <summary>Reads unmanaged array using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)srcLength);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)srcLength);
#endif

            spanOffset = srcLength + 4;
        }

        /// <summary>Reads unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)offset), (uint)srcLength);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)offset), (uint)srcLength);
#endif

            spanOffset = srcLength + 4;
        }

        /// <summary>Reads unmanaged array using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), (uint)offset);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), (uint)offset);
#endif
        }

        /// <summary>Reads unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), (uint)offset);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), (uint)offset);
#endif

        }

        /// <summary>Reads unmanaged array using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="array">The array used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), (uint)offset);
#else
            Unsafe.CopyBlockUnaligned(ref dest, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), (uint)offset);
#endif

            spanOffset = offset;
        }

        /// <summary>Reads unmanaged span using the caller-validated buffer layout.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="span">The buffer used by this operation.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <param name="spanOffset">Receives the span offset produced by the operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(ref buffer, ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), (uint)offset);
#else
            Unsafe.CopyBlockUnaligned(ref buffer, ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), (uint)offset);
#endif

            spanOffset = offset;
        }

        /// <summary>Reads var int byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public byte ReadVarIntByte()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return ReadUnmanaged<byte>();
                case VarIntCodes.SByte:
                    return checked((byte)ReadUnmanaged<sbyte>());
                case VarIntCodes.UInt16:
                    return checked((byte)ReadUnmanaged<ushort>());
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

        /// <summary>Reads var int s byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public sbyte ReadVarIntSByte()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

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

        /// <summary>Reads var int u int16 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public ushort ReadVarIntUInt16()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

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

        /// <summary>Reads var int int16 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public short ReadVarIntInt16()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

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

        /// <summary>Reads var int u int32 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public uint ReadVarIntUInt32()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

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

        /// <summary>Reads var int int32 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public int ReadVarIntInt32()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

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

        /// <summary>Reads var int u int64 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public ulong ReadVarIntUInt64()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

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

        /// <summary>Reads var int int64 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public long ReadVarIntInt64()
        {
            Advance(ReadUnmanaged(out sbyte typeCode));

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

        #region Delegate

        /// <summary>Reads action from the current input.</summary>
        /// <param name="type">The type or symbol to analyze.</param>
        /// <param name="instance">The instance used by this operation.</param>
        /// <param name="methodInfo">The method info used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Delegate ReadAction(Type type, object? instance, MethodInfo methodInfo)
        {
            return Delegate.CreateDelegate(type, instance, methodInfo);
        }

        #endregion

#if NET8_0_OR_GREATER
        /// <summary>Provides utf8 string state functionality.</summary>
        private readonly unsafe struct Utf8StringState
        {
            internal readonly byte* Data;
            internal readonly int Length;

            /// <summary>Initializes a new instance of <c>Utf8StringState</c>.</summary>
            /// <param name="data">The metadata describing the serializable type.</param>
            /// <param name="length">The number of bytes or elements to process.</param>
            internal Utf8StringState(byte* data, int length)
            {
                Data = data;
                Length = length;
            }
        }

        /// <summary>Provides ascii string state functionality.</summary>
        private readonly unsafe struct AsciiStringState
        {
            internal readonly byte* Data;
            internal readonly int Length;

            /// <summary>Initializes a new instance of <c>AsciiStringState</c>.</summary>
            /// <param name="data">The metadata describing the serializable type.</param>
            /// <param name="length">The number of bytes or elements to process.</param>
            internal AsciiStringState(byte* data, int length)
            {
                Data = data;
                Length = length;
            }
        }

        /// <summary>Performs the decode ascii string step for the current operation.</summary>
        /// <param name="destination">The destination receiving the result.</param>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DecodeAsciiString(Span<char> destination, AsciiStringState state)
        {
            ref byte src = ref Unsafe.AsRef<byte>(state.Data);
            ref byte dst = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(destination));
            uint i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; i + 64 <= state.Length; i += 64)
                {
                    var data = Vector512.LoadUnsafe(ref Unsafe.Add(ref src, i));
                    Vector512<ushort> lo = Vector512.WidenLower(data);
                    Vector512<ushort> hi = Vector512.WidenUpper(data);
                    lo.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)(i << 1))));
                    hi.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)((i + 32) << 1))));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i + 32 <= state.Length; i += 32)
                {
                    var data = Vector256.LoadUnsafe(ref Unsafe.Add(ref src, i));
                    Vector256<ushort> lo = Vector256.WidenLower(data);
                    Vector256<ushort> hi = Vector256.WidenUpper(data);
                    lo.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)(i << 1))));
                    hi.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)((i + 16) << 1))));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i + 16 <= state.Length; i += 16)
                {
                    var data = Vector128.LoadUnsafe(ref Unsafe.Add(ref src, i));
                    Vector128<ushort> lo = Vector128.WidenLower(data);
                    Vector128<ushort> hi = Vector128.WidenUpper(data);
                    lo.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)(i << 1))));
                    hi.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)((i + 8) << 1))));
                }
            }

            for (; i < state.Length; i++)
            {
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, (nint)(i << 1)), Unsafe.Add(ref src, (nint)i));
            }
        }

        /// <summary>Reads one to three ASCII bytes without pinning input or preparing a decoder callback.</summary>
        /// <param name="source">The validated payload's first byte.</param>
        /// <param name="length">The payload length, from one through three bytes.</param>
        /// <returns>A fresh decoded string, or null when the validating UTF-8 path is required.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string? ReadTinyAsciiString(ref byte source, int length)
        {
            uint first = source;
            if (first >= 0x80) return null;
            if (length == 1) return new string((char)first, 1);
            uint last = Unsafe.Add(ref source, length - 1);
            uint middle = length == 3 ? Unsafe.Add(ref source, 1) : 0u;
            if ((last | middle) >= 0x80) return null;
            string result = new string('\0', length);
            ref char output = ref Unsafe.AsRef(in result.GetPinnableReference());
            output = (char)first;
            Unsafe.Add(ref output, length - 1) = (char)last;
            if (length == 3) Unsafe.Add(ref output, 1) = (char)middle;
            return result;
        }

        /// <summary>Reads short ascii string from the current input.</summary>
        /// <param name="source">The input data to process.</param>
        /// <param name="length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string? ReadShortAsciiString(ref byte source, int length)
        {
            if (length < 8)
            {
                var shortBytes = Vector128.Create(Unsafe.ReadUnaligned<uint>(ref source),
                    Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, length - 4)), 0u, 0u).AsByte();
                if (shortBytes.ExtractMostSignificantBits() != 0) return null;
                string shortResult = new string('\0', length);
                ref ushort shortOutput = ref Unsafe.As<char, ushort>(ref Unsafe.AsRef(in shortResult.GetPinnableReference()));
                var chars = Vector128.WidenLower(shortBytes);
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref shortOutput), chars.GetLower());
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref shortOutput, length - 4)), chars.GetUpper());
                return shortResult;
            }

            // Scalar lane construction avoids stack staging on 64-bit. Retain
            // the existing vector-load path on 32-bit runtimes.
            var bytes = Unsafe.SizeOf<IntPtr>() == 8
                ? Vector128.Create(Unsafe.ReadUnaligned<ulong>(ref source),
                    Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, length - 8))).AsByte()
                : Vector128.Create(Vector64.LoadUnsafe(ref source),
                    Vector64.LoadUnsafe(ref source, (nuint)(length - 8)));
            // A matching byte/char count is only a hint; invalid UTF-8 must still
            // go through the validating decoder. No string is published yet.
            if (bytes.ExtractMostSignificantBits() != 0) return null;

            // This fresh, non-interned string is filled before it escapes. Managed
            // references remain GC-tracked, so no input pin or callback is needed.
            string result = new string('\0', length);
            ref ushort output = ref Unsafe.As<char, ushort>(ref Unsafe.AsRef(in result.GetPinnableReference()));
            Vector128.WidenLower(bytes).StoreUnsafe(ref output);
            Vector128.WidenUpper(bytes).StoreUnsafe(ref output, (nuint)(length - 8));
            return result;
        }

        /// <summary>Performs the decode utf8 string step for the current operation.</summary>
        /// <param name="destination">The destination receiving the result.</param>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DecodeUtf8String(Span<char> destination, Utf8StringState state)
        {
            var source = new ReadOnlySpan<byte>(state.Data, state.Length);
            var status = Utf8.ToUtf16(source, destination, out _, out var charsWritten,
                replaceInvalidSequences: false);
            if (status != OperationStatus.Done || charsWritten != destination.Length)
            {
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }
        }
#endif

    }
}
