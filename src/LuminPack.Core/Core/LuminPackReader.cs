using System.Buffers;
using LuminPack.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Option;
using LuminPack.Utility;
using LuminPack.Utility.ViewModel;
using static LuminPack.Code.LuminPackMarshal;

#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Numerics;
using System.Text.Unicode;

#endif
namespace LuminPack.Core
{
    /// <summary>Reads complete binary records with an advancing input cursor and reusable storage.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct LuminPackReader
    {
#if NET8_0_OR_GREATER
        /// <summary>The advancing data cursor; caller-owned managed storage must remain pinned on netstandard2.1.</summary>
        public ref byte Cursor;
#endif
#if !NET8_0_OR_GREATER
        /// <summary>The advancing data cursor; caller-owned managed storage must remain pinned on netstandard2.1.</summary>
        public byte* Cursor;
#endif
        private Span<byte> _buffer;
        /// <summary>The first byte after the readable window, cached with every window change.</summary>
#if NET8_0_OR_GREATER
        private ref byte _end;
#else
        private byte* _end;
#endif

        /// <summary>Gets the complete retained storage window used for dynamic bounds and framing patches.</summary>
        /// <remarks>Only the reader replaces its window, so the cached end always describes it.</remarks>
        public Span<byte> Buffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] readonly get => _buffer;
            internal set
            {
                _buffer = value;
#if NET8_0_OR_GREATER
                _end = ref Unsafe.Add(ref MemoryMarshal.GetReference(value), (nint)(uint)value.Length);
#else
                _end = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(value)) + value.Length;
#endif
            }
        }
        /// <summary>The explicit or materialized reference context, or null before an implicit scope requests state.</summary>
        /// <remarks>Use <see cref="OptionState"/> when reference tracking requires an initialized context.</remarks>
        public LuminPackReaderOptionalState? Options;

        #region Constructors

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name = "option">An explicit shared reference context, or null for a lazily owned independent context.</param>
        public LuminPackReader(LuminPackReaderOptionalState? option = null)
        {
            Options = option;
            Buffer = default;
            Position = 0;
        }

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name = "bufferReference">The buffer reference used by this operation.</param>
        /// <param name = "option">An explicit shared reference context, or null for a lazily owned independent context.</param>
        /// <remarks>On netstandard2.1, caller-owned managed span storage must remain pinned for the complete operation lifetime.</remarks>
        public LuminPackReader(ref Span<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            Options = option;
            Buffer = bufferReference;
            Position = 0;
        }

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name = "bufferReference">The buffer reference used by this operation.</param>
        /// <param name = "option">An explicit shared reference context, or null for a lazily owned independent context.</param>
        /// <remarks>On netstandard2.1, caller-owned managed span storage must remain pinned for the complete operation lifetime.</remarks>
        public LuminPackReader(ref ReadOnlySpan<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            Options = option;
            Buffer = LuminPackMarshal.CreateSpan(ref GetNonNullPinnableReference(bufferReference), bufferReference.Length);
            Position = 0;
        }

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name = "bufferWriter">The writer receiving the output data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LuminPackReader(LuminBufferWriter bufferWriter)
        {
            Options = bufferWriter.ReaderState;
            Buffer = bufferWriter.GetSpan();
            Position = 0;
        }

        #endregion

        #region Cursor and helpers

        /// <summary>Dereferences the architecture-specific advancing cursor inside shared implementation kernels.</summary>
        private ref byte Current {[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
#if NET8_0_OR_GREATER
            ref Cursor;
#else
            ref Unsafe.AsRef<byte>(Cursor);
#endif
        }

        /// <summary>Copies individual cursor fields into an isolated operation state without taking this state's address.</summary>
        /// <param name = "destination">The state used by an outlined compound operation.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void CopyTo(ref LuminPackReader destination)
        {
            destination.Options = Options;
#if NET8_0_OR_GREATER
            destination.Cursor = ref Cursor;
#else
            destination.Cursor = Cursor;
#endif
            destination.Buffer = Buffer;
        }

        /// <summary>Gets the initialized reference context, creating an independent implicit scope on first access.</summary>
        /// <remarks>Initialize this context before copying an uninitialized reader when the copies must share references.</remarks>
        public LuminPackReaderOptionalState OptionState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Options is null) Options = CreateReferenceState();
                return Options;
            }
        }

        /// <summary>Creates reference storage outside the hot cursor operation.</summary>
        /// <returns>An independent reference context owned by the requesting reader.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static LuminPackReaderOptionalState CreateReferenceState() => new LuminPackReaderOptionalState();

        /// <summary>Validates in every build that the requested bytes remain in the input buffer from the cursor.</summary>
        /// <param name = "count">The number of bytes required by the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureReadable(int count)
        {
            // Signed 64-bit distances reject a cursor past the end and every negative or oversized count on all targets.
#if NET8_0_OR_GREATER
            long room = (long)Unsafe.ByteOffset(ref Cursor, ref _end);
#else
            long room = (long)(_end - Cursor);
#endif
            if (room < (long)(uint)count || count < 0)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(Position + count));
        }

        /// <summary>Validates that a range of the readable window exists.</summary>
        /// <param name = "index">The position at which the range starts.</param>
        /// <param name = "count">The number of bytes in the range.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureReadable(int index, int count)
        {
            if (IsOutOfRange(index, count, Buffer.Length))
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(index + count));
        }

        /// <summary>Returns whether <paramref name="count"/> bytes from <paramref name="index"/> leave a buffer of <paramref name="length"/> bytes.</summary>
        /// <param name="index">The starting position, possibly negative or past the end.</param>
        /// <param name="count">The requested byte count, possibly negative.</param>
        /// <param name="length">The nonnegative buffer length.</param>
        /// <returns>Whether the range is invalid.</returns>
        /// <remarks>On 64-bit targets the two unsigned operands cannot overflow their 64-bit sum, so one comparison
        /// rejects exactly the ranges the two-comparison form rejects: a negative or past-the-end index, a negative
        /// count, and any range ending after the buffer.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsOutOfRange(int index, int count, int length) =>
            Unsafe.SizeOf<IntPtr>() == 8
                ? (ulong)(uint)index + (uint)count > (uint)length
                : (uint)index > (uint)length || (uint)count > (uint)(length - index);

        /// <summary>
        /// Advances the target pointer.
        /// </summary>
        /// <param name = "count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            EnsureReadable(count);
#if NET8_0_OR_GREATER
            Cursor = ref Unsafe.Add(ref Cursor, (nint)(uint)count);
#else
            Cursor += count;
#endif
        }

        /// <summary>
        /// Replaces the current buffer span.
        /// </summary>
        /// <param name = "span"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            int position = Position;
            Buffer = span;
            Position = position;
        }

        /// <summary>
        /// Releases the input span and resets its cursor without modifying caller memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            Buffer = null;
            Position = 0;
        }

        /// <summary>
        /// Returns the span currently held by the reader.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return Buffer;
        }

        /// <summary>Examines the current object marker without advancing.</summary>
        /// <returns>Whether the header represents a nonnull value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullObject()
        {
            // At the end of the input no marker byte may be loaded; the following checked read rejects the missing record.
            return (uint)Position < (uint)Buffer.Length && Current == LuminPackCode.NullObject;
        }

        /// <summary>Gets or replaces the absolute position for framing and random-access implementation kernels.</summary>
        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
            get => (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(Buffer), ref Cursor);
#else
            get => (int)(Cursor - (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)));
#endif
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
#if NET8_0_OR_GREATER
                Cursor = ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)value);
#else
                Cursor = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)) + value;
#endif
            }
        }

        #endregion

        #region Values

        /// <summary>Validates and copies into a proven fixed storage group, then advances the input cursor.</summary>
        /// <param name = "destination">The first byte of the proven adjacent fields.</param>
        /// <param name = "byteCount">The complete fixed storage width.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(scoped ref byte destination, int byteCount)
        {
            // The checked Advance validates the complete range before the cursor moves, so a failed read keeps its cursor;
            // the copy then reads from the captured position.
            ref byte source = ref Current;
            Advance(byteCount);
            global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, (uint)byteCount);
        }

        /// <summary>Reads a contiguous payload directly into its selected destination storage.</summary>
        /// <typeparam name = "T">The reference-free element storage type.</typeparam>
        /// <param name = "value">The complete destination span.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<T>(scoped Span<T> value)
            where T : unmanaged
        {
            int bytes = checked(value.Length * Unsafe.SizeOf<T>());
            ValidateCollectionPayload(Position, bytes);
            if (bytes != 0)
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(value)), ref Current, (uint)bytes);
            Advance(bytes);
        }

        /// <summary>Reads a collection count and rejects a payload that cannot fit before allocation.</summary>
        /// <param name = "minimumElementSize">The minimum wire width of one element.</param>
        /// <returns>The nonnegative count, or minus one for a null collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadLength(int minimumElementSize = 1)
        {
            int position = Position;
            ValidateCollectionPayload(position, sizeof(int));
            LuminPackReader.Read(ref Current, out int count);
            if (count < -1)
                LuminPackExceptionHelper.ThrowInvalidCollectionLength(count);
            if (minimumElementSize != 0 && count >= 0)
            {
                // A forged count can overflow the product, which must fail like any other payload larger than the input.
                long required = (long)count * minimumElementSize;
                ValidateCollectionPayload(position + sizeof(int), required > int.MaxValue ? int.MaxValue : (int)required);
            }
            Advance(sizeof(int));
            return count;
        }

        /// <summary>Reads, validates and advances past a collection length.</summary>
        /// <param name = "length">Receives the validated element count.</param>
        /// <returns>Whether the header represents a nonnull value.</returns>
        /// <remarks>Uses the ordinary one-byte minimum. Proven zero-width generated elements use the explicit-width overload.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCollectionHead(out int length)
            => TryReadCollectionHead(out length, 1);

        /// <summary>Reads a collection count using its proven minimum element width.</summary>
        /// <param name="length">Receives the validated count or null marker.</param>
        /// <param name="minimumElementSize">The minimum wire width, including zero for packed empty elements.</param>
        /// <returns>Whether the count is nonnull.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCollectionHead(out int length, int minimumElementSize)
        {
            length = ReadLength(minimumElementSize);
            return length != LuminPackCode.NullCollection;
        }

        /// <summary>Examines the current member marker without consuming a nonnull tuple's first child header.</summary>
        /// <returns>Whether the marker is nonnull.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekObjectHeader() => !PeekIsNullObject();

        /// <summary>Examines and validates a collection count without advancing.</summary>
        /// <param name = "length">Receives the count or the null marker.</param>
        /// <returns>Whether the count is nonnull.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekCollectionHeader(out int length)
            => PeekCollectionHeader(out length, 1);

        /// <summary>Validates a collection count and its minimum payload without advancing.</summary>
        /// <param name="length">Receives the count or null marker.</param>
        /// <param name="minimumElementSize">The proven minimum wire width of an element.</param>
        /// <returns>Whether the count is nonnull.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekCollectionHeader(out int length, int minimumElementSize)
        {
            int position = Position;
            ValidateCollectionPayload(position, sizeof(int));
            length = LuminPackReader.Read<int>(ref Current);
            if (length < LuminPackCode.NullCollection)
                LuminPackExceptionHelper.ThrowInvalidCollectionLength(length);
            if (length > 0 && minimumElementSize != 0)
            {
                long required = (long)length * minimumElementSize;
                ValidateCollectionPayload(position + sizeof(int), required > int.MaxValue ? int.MaxValue : (int)required);
            }
            return length != LuminPackCode.NullCollection;
        }

        /// <summary>Reads and advances past an object marker.</summary>
        /// <returns>Whether the header represents a nonnull value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadObjectHead()
        {
            return TryReadObjectHead(out _);
        }

        /// <summary>Reads and advances past an object marker.</summary>
        /// <param name = "code">Receives the member count or null marker.</param>
        /// <returns>Whether the header represents a nonnull value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadObjectHead(out byte code)
        {
            // The checked Advance validates the marker before it is loaded from the captured position.
            ref byte marker = ref Current;
            Advance(1);
            code = marker;
            return code != LuminPackCode.NullObject;
        }

        /// <summary>Reads and advances past an object member count.</summary>
        /// <param name = "memberCount">Receives the serialized member count.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadObjectHeader(out byte memberCount)
        {
            ref byte marker = ref Current;
            Advance(1);
            memberCount = marker;
        }

        /// <summary>Attempts to peek union header and reports whether it succeeded.</summary>
        /// <param name = "tag">Receives the tag produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekUnionHeader(out ushort tag)
        {
            EnsureReadable(Position, 1);
            tag = Current;
            Advance(1);
            return tag is not LuminPackCode.NullObject;
        }

        /// <summary>Attempts to peek wide union header and reports whether it succeeded.</summary>
        /// <param name = "tag">Receives the tag produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekWideUnionHeader(out ushort tag)
        {
            EnsureReadable(Position, 1);
#if NET8_0_OR_GREATER
            ref var firstTag = ref Current;
#else
            ref var firstTag = ref Unsafe.AsRef<byte>(Cursor);
#endif
            if (firstTag < LuminPackCode.WideTag)
            {
                tag = firstTag;
                Advance(1);
                return true;
            }
            else if (firstTag == LuminPackCode.WideTag)
            {
                EnsureReadable(Position, 3);
                tag = LuminPackReader.Read<ushort>(ref Unsafe.Add(ref firstTag, 1));
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

        /// <summary>Reads action from the current input.</summary>
        /// <param name = "type">The type or symbol to analyze.</param>
        /// <param name = "instance">The instance used by this operation.</param>
        /// <param name = "methodInfo">The method info used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Delegate ReadAction(Type type, object? instance, MethodInfo methodInfo)
        {
            return Delegate.CreateDelegate(type, instance, methodInfo);
        }

        /// <summary>Reads scalar storage for framing code that publishes a complete batch cursor separately.</summary>
        /// <typeparam name = "T">The reference-free scalar storage.</typeparam>
        /// <param name = "source">The already selected fixed input location.</param>
        /// <returns>The complete scalar storage value without advancing another state.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(ref byte source)
        {
            Read(ref source, out T value);
            return value;
        }

        /// <summary>Reads a complete scalar storage value and advances the binary cursor.</summary>
        /// <typeparam name = "T">The unmanaged storage type.</typeparam>
        /// <param name = "value">The destination scalar storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<T>(ref T value)
            where T : unmanaged
        {
            EnsureReadable(Position, Unsafe.SizeOf<T>());
            Advance(Read(ref Current, out value));
        }

        /// <summary>Reads and returns complete scalar storage through the advancing API.</summary>
        /// <typeparam name = "T">The unmanaged storage type.</typeparam>
        /// <returns>The complete scalar storage value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>()
            where T : unmanaged
        {
            T value = default;
            Read(ref value);
            return value;
        }

        /// <summary>Reads one validated contiguous record into reusable array storage.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "source">The remaining input from the record's start.</param>
        /// <param name = "value">The reusable array destination.</param>
        /// <param name = "count">The known count when the count header was already consumed.</param>
        /// <param name = "header">Whether the record includes its count.</param>
        /// <param name = "compressed">Whether the payload uses LuminZ compression.</param>
        /// <returns>The actual complete byte width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(scoped ReadOnlySpan<byte> source, ref T[]? value, int count, bool header, bool compressed)
        {
            int bytes = ReadBlock<T>(source, ref count, header, compressed, out int offset, out int payloadBytes);
            if (count < 0)
            {
                value = null;
                return bytes;
            }

            if (count == 0)
            {
                value = Array.Empty<T>();
                return bytes;
            }

            if (value is null || value.Length != count)
                value = LuminPackMarshal.AllocateUninitializedArray<T>(count);
            ReadPayload(source.Slice(offset, payloadBytes), value.AsSpan(), compressed);
            return bytes;
        }

        /// <summary>Reads one validated contiguous record into reusable span storage.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "source">The remaining input from the record's start.</param>
        /// <param name = "value">The reusable span destination.</param>
        /// <param name = "count">The known count when the count header was already consumed.</param>
        /// <param name = "header">Whether the record includes its count.</param>
        /// <param name = "compressed">Whether the payload uses LuminZ compression.</param>
        /// <returns>The actual complete byte width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(scoped ReadOnlySpan<byte> source, ref Span<T> value, int count, bool header, bool compressed)
        {
            int bytes = ReadBlock<T>(source, ref count, header, compressed, out int offset, out int payloadBytes);
            if (count <= 0)
            {
                value = default;
                return bytes;
            }

            if (value.Length != count)
                value = LuminPackMarshal.AllocateUninitializedArray<T>(count);
            ReadPayload(source.Slice(offset, payloadBytes), value, compressed);
            return bytes;
        }

        /// <summary>Reads a contiguous array record and reports its complete byte width.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "source">The remaining input from the record's start.</param>
        /// <param name = "value">The reusable array destination.</param>
        /// <param name = "count">The known element count.</param>
        /// <param name = "header">Whether the record includes its count.</param>
        /// <param name = "compressed">Whether the payload uses LuminZ compression.</param>
        /// <param name = "bytes">Receives the complete byte width.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Read<T>(scoped ReadOnlySpan<byte> source, ref T[]? value, int count, bool header, bool compressed, out int bytes) => bytes = Read(source, ref value, count, header, compressed);

        /// <summary>Reads a contiguous span record and reports its complete byte width.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "source">The remaining input from the record's start.</param>
        /// <param name = "value">The reusable span destination.</param>
        /// <param name = "count">The known element count.</param>
        /// <param name = "header">Whether the record includes its count.</param>
        /// <param name = "compressed">Whether the payload uses LuminZ compression.</param>
        /// <param name = "bytes">Receives the complete byte width.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Read<T>(scoped ReadOnlySpan<byte> source, ref Span<T> value, int count, bool header, bool compressed, out int bytes) => bytes = Read(source, ref value, count, header, compressed);

        /// <summary>Validates complete dynamic framing before any collection allocation or unsafe payload copy.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "source">The remaining input.</param>
        /// <param name = "count">The known or decoded element count.</param>
        /// <param name = "header">Whether a count header is present.</param>
        /// <param name = "compressed">Whether a compressed-size header is present for a nonempty payload.</param>
        /// <param name = "offset">Receives the payload's first byte.</param>
        /// <param name = "payloadBytes">Receives the encoded payload byte count.</param>
        /// <returns>The complete encoded record byte count.</returns>
        private static int ReadBlock<T>(ReadOnlySpan<byte> source, ref int count, bool header, bool compressed, out int offset, out int payloadBytes)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(T));
            offset = header ? sizeof(int) : 0;
            if (source.Length < offset)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(offset);
            if (header)
                Read(ref MemoryMarshal.GetReference(source), out count);
            if (count < -1)
                LuminPackExceptionHelper.ThrowInvalidCollectionLength(count);
            if (count <= 0)
            {
                payloadBytes = 0;
                return offset;
            }

            payloadBytes = checked(count * Unsafe.SizeOf<T>());
            int decodedBytes = payloadBytes;
            if (compressed)
            {
                if (source.Length - offset < sizeof(int))
                    LuminPackExceptionHelper.ThrowSpanOutOfRange(offset + sizeof(int));
                Read(ref Unsafe.Add(ref MemoryMarshal.GetReference(source), (nint)(uint)(offset)), out payloadBytes);
                offset += sizeof(int);
            }

            if ((uint)payloadBytes > (uint)(source.Length - offset))
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(offset + payloadBytes));
            if (compressed && LuminCompressor.GetDecompressedSize(source.Slice(offset, payloadBytes)) != decodedBytes)
                LuminPackExceptionHelper.ThrowInvalidDataException("Compressed collection output length does not match its element count.");
            return checked(offset + payloadBytes);
        }

        /// <summary>Shares the single contiguous payload copy between array and span wrappers.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "source">The already validated encoded payload.</param>
        /// <param name = "value">The complete destination storage.</param>
        /// <param name = "compressed">Whether the payload requires LuminZ decoding.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadPayload<T>(ReadOnlySpan<byte> source, Span<T> value, bool compressed)
        {
            int bytes = checked(value.Length * Unsafe.SizeOf<T>());
            ref byte destination = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(value));
            if (compressed)
                LuminCompressor.Decompress(source, LuminPackMarshal.CreateSpan(ref destination, bytes));
            else
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref destination, ref MemoryMarshal.GetReference(source), (uint)bytes);
        }

        /// <summary>Processes a fixed storage batch without introducing a Release bounds check.</summary>
        /// <param name = "reference">The first byte of the complete fixed storage batch.</param>
        /// <typeparam name = "T1">The reference-free storage type.</typeparam>
        /// <param name = "value1">The field participating in the memory operation.</param>
        /// <returns>The complete packed wire width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T1>(ref byte reference, out T1 value1)
        {
            if (Unsafe.SizeOf<IntPtr>() == 8)
            {
                value1 = Unsafe.ReadUnaligned<T1>(ref reference);
            }
            else
            {
                value1 = default!;
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref reference, (uint)Unsafe.SizeOf<T1>());
            }

            return Unsafe.SizeOf<T1>();
        }

        /// <summary>Reads one complete root scalar into its destination storage without introducing a Release bounds check.</summary>
        /// <typeparam name = "T">The reference-free storage type.</typeparam>
        /// <param name = "source">The first byte of the complete fixed record.</param>
        /// <param name = "value">The caller's destination storage.</param>
        /// <returns>The complete packed wire width.</returns>
        /// <remarks>
        /// A sixteen-byte value is moved with one 128-bit load and store on accelerated 64-bit targets. A destination
        /// local split into its fields would otherwise receive several narrow stores, and a later complete-width load
        /// of that storage cannot be forwarded from them. The width is the value's own storage, so no wider vector
        /// applies. Other widths, 32-bit targets, unaccelerated hardware and older targets use <see cref="Read{T1}(ref byte, out T1)"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadComplete<T>(ref byte source, ref T value)
        {
#if NETSTANDARD2_1
            if (Unsafe.SizeOf<IntPtr>() == 8 && Unsafe.SizeOf<T>() == 16 && global::LuminPack.Core.LuminPackBurst.TryCopy(ref Unsafe.As<T, byte>(ref value), ref source, 16))
                return 16;
#endif
#if NET8_0_OR_GREATER
            if (Unsafe.SizeOf<IntPtr>() == 8 && Unsafe.SizeOf<T>() == 16 && Vector128.IsHardwareAccelerated)
            {
                Vector128.LoadUnsafe(ref source).StoreUnsafe(ref Unsafe.As<T, byte>(ref value));
                return 16;
            }
#endif
            return Read(ref source, out value);
        }

        #endregion

        #region String

        /// <summary>Validates string length before the operation continues.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void ValidateStringLength<TOptions>(Span<byte> buffer, int index, int length)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (length < 0 && length != LuminPackCode.NullCollection)
                LuminPackExceptionHelper.ThrowInvalidStringLength(length);
            int headerSize = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? sizeof(int) * 2 : sizeof(int);
            if ((uint)index > (uint)buffer.Length || (uint)headerSize > (uint)(buffer.Length - index) || (length >= 0 && (uint)length > (uint)(buffer.Length - index - headerSize)))
                LuminPackExceptionHelper.ThrowInSufficientBuffer(length);
        }

        /// <summary>
        /// Calculates the binary string encoding length.
        /// </summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index"></param>
        /// <param name = "length"></param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void ReadStringLength<TOptions>(Span<byte> buffer, ref int index, out int length)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token))
            {
                var remaining = buffer.Slice(index);
                if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
                {
                    length = remaining.IndexOf((byte)0);
                }
                else
                {
                    var characters = MemoryMarshal.Cast<byte, ushort>(remaining.Slice(0, remaining.Length & ~1));
                    var characterLength = characters.IndexOf((ushort)0);
                    length = characterLength < 0 ? -1 : checked(characterLength * sizeof(ushort));
                }

                if (length < 0)
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidDataException("String token terminator was not found.");
                return;
            }

            EnsureReadable(buffer, index, sizeof(int));
#if NET8_0_OR_GREATER
            length = LuminPackReader.Read<int>(ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)index));
#else
            length = LuminPackReader.Read<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(buffer)), (nint)(uint)index));
#endif
            ValidateStringLength<TOptions>(buffer, index, length);
        }

        /// <summary>Returns the size of the string header used by the current recording mode.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int StringRecordLength<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            // UTF-16 token mode uses a two-byte 0x0000 terminator because individual zero bytes are common within code units.
            return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2) : (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 8 : 4;
        }

        /// <summary>
        /// Reads one string record and advances the supplied absolute offset.
        /// Intended for source-generated parsers so the JIT sees a single call and
        /// does not have to keep the offset live across four string helpers.
        /// </summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string? ReadStringAndAdvance<TOptions>(Span<byte> buffer, ref int index)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            bool isUtf8 = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8);
            if ((default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token))
            {
                return ReadTokenStringAndAdvance<TOptions>(buffer, ref index, isUtf8);
            }

            int recordLength = isUtf8 ? sizeof(int) * 2 : sizeof(int);
            int bufferLength = buffer.Length;
            if ((uint)index > (uint)bufferLength || (uint)recordLength > (uint)(bufferLength - index))
            {
                LuminPackExceptionHelper.ThrowInSufficientBuffer(recordLength);
            }

#if NET8_0_OR_GREATER
            int length = LuminPackReader.Read<int>(ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)index));
#else
            int length = LuminPackReader.Read<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(buffer)), (nint)(uint)index));
#endif
            if (length < 0)
            {
                if (length != LuminPackCode.NullCollection)
                    LuminPackExceptionHelper.ThrowInvalidStringLength(length);
                index += recordLength;
                return null;
            }

            // A UTF-16 header is the raw byte count or the compact Latin-1 form; both carry their payload width.
            int payload = isUtf8 ? length : StringSerializer.Utf16RecordPayload(length);
            if ((uint)payload > (uint)(bufferLength - index - recordLength))
            {
                LuminPackExceptionHelper.ThrowInSufficientBuffer(payload);
            }

            var value = payload == 0 ? string.Empty : isUtf8 ? ReadUtf8StringWithLength<TOptions>(buffer, index, length) : ReadUtf16StringWithLength<TOptions>(buffer, index, length);
            index += payload + recordLength;
            return value;
        }

        /// <summary>Reads token string and advance from the current input.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "isUtf8">The is utf8 used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string? ReadTokenStringAndAdvance<TOptions>(Span<byte> buffer, ref int index, bool isUtf8)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            ReadStringLength<TOptions>(buffer, ref index, out var length);
            // Token mode has no null marker: null and empty are both encoded as an empty
            // token and must therefore deserialize to string.Empty.
            var value = isUtf8 ? ReadUtf8StringWithToken<TOptions>(buffer, index, length) : ReadUtf16StringWithToken<TOptions>(buffer, index, length);
            index += length + (isUtf8 ? 1 : 2);
            return value;
        }

        /// <summary>Reads utf8 string with token from the current input.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static string? ReadUtf8StringWithToken<TOptions>(Span<byte> buffer, int index, int length)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(buffer)), (nint)(uint)index);
#endif
            return Encoding.UTF8.GetString(CreateSpan(ref spanRef, length));
        }

        /// <summary>Reads utf16 string with token from the current input.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static string? ReadUtf16StringWithToken<TOptions>(Span<byte> buffer, int index, int length)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(buffer)), (nint)(uint)index);
#endif
            return Encoding.Unicode.GetString(CreateSpan(ref spanRef, length));
        }

        /// <summary>Reads utf8 string with length from the current input.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static string? ReadUtf8StringWithLength<TOptions>(Span<byte> buffer, int index, int length)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (length < 0)
                return null;
            if (length == 0)
                return string.Empty;
            ref byte count = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)(index + sizeof(int)));
            return StringSerializer.DeserializeUtf8(ref Unsafe.Add(ref count, sizeof(int)), length, LuminPackReader.Read<int>(ref count));
        }

        /// <summary>Reads utf16 string with length from the current input.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static string? ReadUtf16StringWithLength<TOptions>(Span<byte> buffer, int index, int length)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (length < 0)
                return null;
            int payload = StringSerializer.Utf16RecordPayload(length);
            if (payload == 0)
                return string.Empty;
            int index1 = index + 4;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)index1);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(buffer)), (nint)(uint)index1);
#endif
            if ((length & 1) != 0)
                return StringSerializer.CreateLatin1String(ref spanRef, payload);
            return StringSerializer.DeserializeUtf16(MemoryMarshal.CreateReadOnlySpan(ref spanRef, length));
        }

        /// <summary>Reads a compiled-profile string record without allocating an unchanged string.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "index">The absolute input cursor, advanced only after a successful read.</param>
        /// <param name = "current">The string available for reuse.</param>
        /// <returns>The existing string when equal, or the normally decoded value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string? ReadStringAndAdvance<TOptions>(Span<byte> buffer, ref int index, string? current)
            where TOptions : struct, ILuminPackOptions
        {
            if (current is null)
                return ReadStringAndAdvance<TOptions>(buffer, ref index);
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
                // The shared record reader validates the complete record, compares either payload form once and
                // publishes the cursor only after success.
                int start = index;
                if ((uint)start <= (uint)buffer.Length)
                {
                    string? text = StringSerializer.ReadUtf16Record(ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)start), buffer.Length - start, current, out int consumed);
                    index = start + consumed;
                    return text;
                }
            }
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
                int start = index;
                int available = buffer.Length;
                if ((uint)start <= (uint)available && available - start >= sizeof(int) * 2)
                {
                    ref byte __fixed0Reference = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)(start));
                    int __fixed0Length = 0;
                    __fixed0Length += global::LuminPack.Core.LuminPackReader.Read(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed0Reference, (nint)(uint)(__fixed0Length)), out int bytes);
                    __fixed0Length += global::LuminPack.Core.LuminPackReader.Read(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed0Reference, (nint)(uint)(__fixed0Length)), out int characters);
                    if (bytes > 0 && bytes <= available - start - sizeof(int) * 2)
                    {
                        if (characters > 0 && characters != current.Length)
                            return ReadStringAndAdvance<TOptions>(buffer, ref index);
                        ref byte payloadStart = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)start + sizeof(int) * 2);
                        bool matches;
                        if (bytes == current.Length)
                        {
                            // Short ASCII records dominate reuse traffic. The framework helper's fixed cost
                            // (span construction plus a cross-assembly vector setup) outweighs its work at these
                            // lengths, so compare short records in-assembly and keep the helper for longer ones.
                            matches = bytes <= 16 ? StringSerializer.MatchesAsciiRecord(ref payloadStart, current) : Ascii.Equals(MemoryMarshal.CreateReadOnlySpan(ref payloadStart, bytes), current.AsSpan());
                        }
                        else
                            matches = StringSerializer.MatchesUtf8(MemoryMarshal.CreateReadOnlySpan(ref payloadStart, bytes), current);
                        if (matches)
                        {
                            // Publish only a validated match; failed reads retain their original cursor.
                            index = start + sizeof(int) * 2 + bytes;
                            return current;
                        }

                        // The validated payload was compared once; a mismatch needs the original decoder only.
                        return ReadStringAndAdvance<TOptions>(buffer, ref index);
                    }
                }
            }
#endif
            return ReadExistingStringAndAdvance<TOptions>(buffer, ref index, current);
        }

        /// <summary>Isolates fallback comparison work from the original fresh-string reader.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "index">The absolute record cursor.</param>
        /// <param name = "current">The non-null candidate string.</param>
        /// <returns>The reused or normally decoded string.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string? ReadExistingStringAndAdvance<TOptions>(Span<byte> buffer, ref int index, string current)
            where TOptions : struct, ILuminPackOptions
        {
            if (MatchesStringRecord<TOptions>(buffer, index, current, out int end))
            {
                index = end;
                return current;
            }

            return ReadStringAndAdvance<TOptions>(buffer, ref index);
        }

        /// <summary>Compares validated string payload bytes without publishing a cursor or creating text.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "index">The original record cursor.</param>
        /// <param name = "current">The candidate immutable string.</param>
        /// <param name = "end">Receives the complete record end after a match.</param>
        /// <returns>Whether the ordinary reader would produce the candidate text.</returns>
        private static bool MatchesStringRecord<TOptions>(Span<byte> buffer, int index, string current, out int end)
            where TOptions : struct, ILuminPackOptions
        {
            end = index;
            bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            bool token = default(TOptions).StringRecording == LuminPackStringRecording.Token;
            int header = token ? 0 : utf8 ? 8 : 4;
            int available = buffer.Length;
            if ((uint)index > (uint)available || (uint)header > (uint)(available - index))
                return false;
            ReadStringLength<TOptions>(buffer, ref index, out int length);
            // Null, empty and malformed records retain the original reader's normalization and failures.
            if (length <= 0)
                return false;
            if ((uint)length > (uint)(available - index - header))
                return false;
            ReadOnlySpan<byte> payload = buffer.Slice(index + header, length);
            bool matches;
            if (utf8)
            {
                if (!token)
                {
                    int characterIndex = index + 4;
                    global::LuminPack.Core.LuminPackReader.Read(ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)(characterIndex)), out int characters);
                    if (characters > 0 && characters != current.Length)
                        return false;
                }

                matches = StringSerializer.MatchesUtf8(payload, current);
            }
            else
            {
                matches = payload.SequenceEqual(MemoryMarshal.AsBytes(current.AsSpan()));
                // Token decoding uses Encoding.Unicode, which replaces malformed surrogate sequences.
                if (matches && token)
                {
                    for (int i = 0; i < current.Length; i++)
                    {
                        if (!char.IsSurrogate(current[i]))
                            continue;
                        if (!char.IsHighSurrogate(current[i]) || ++i == current.Length || !char.IsLowSurrogate(current[i]))
                            return false;
                    }
                }
            }

            if (matches)
                end = index + header + length + (token ? utf8 ? 1 : 2 : 0);
            return matches;
        }

        /// <summary>Validates that the requested bytes remain in the input window in every build.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "count">The number of bytes required.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureReadable(Span<byte> buffer, int index, int count)
        {
            if (IsOutOfRange(index, count, buffer.Length))
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(index + count));
        }

        /// <summary>Reads a string with the default compiled profile, retaining equal immutable storage.</summary>
        /// <param name = "value">The current string and destination for the parsed string.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref string? value) => Read<LuminPackDefaultOptions>(ref value);

        /// <summary>Reads a complete string record and owns its successful cursor advancement.</summary>
        /// <typeparam name = "TOptions">The stateless binary profile.</typeparam>
        /// <param name = "value">The current string and destination for the parsed string.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref string? value)
            where TOptions : struct, ILuminPackOptions
        {
            value = Read<TOptions>(value);
        }

        /// <summary>Reads a complete default-profile record without exposing an integer cursor.</summary>
        /// <param name = "current">The immutable string available for reuse.</param>
        /// <returns>The parsed string, retaining equal storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? Read(string? current = null) => Read<LuminPackDefaultOptions>(current);

        /// <summary>Reads a complete compiled-profile record without exposing an integer cursor.</summary>
        /// <typeparam name = "TOptions">The stateless binary profile.</typeparam>
        /// <param name = "current">The immutable string available for reuse.</param>
        /// <returns>The parsed string, retaining equal storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? Read<TOptions>(string? current)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
#if NET8_0_OR_GREATER
                // Complete nonempty records validate before comparison or allocation. Equal candidates retain
                // their immutable storage; null, empty and malformed records keep the shared reader's failures.
                if (Buffer.Length - Position >= sizeof(int))
                {
                    int header = LuminPackReader.Read<int>(ref Current);
                    int payload = StringSerializer.Utf16RecordPayload(header);
                    if (payload > 0)
                    {
                        // The checked Advance validates the complete record before the cursor moves; the payload is
                        // then decoded from its captured position.
                        ref byte text = ref Unsafe.Add(ref Current, sizeof(int));
                        Advance(sizeof(int) + payload);
                        // Compact Latin-1 records carry most text and stay inline; raw UTF-16 payloads use the
                        // outlined decoder.
                        return (header & 1) != 0
                            ? StringSerializer.ReadLatin1Payload(ref text, payload, current)
                            : StringSerializer.ReadUtf16Payload(ref text, payload, current);
                    }
                }
#endif
                string? value = StringSerializer.ReadUtf16Record(ref Current, Buffer.Length - Position, current, out int bytes);
                Advance(bytes);
                return value;
            }

#if NET8_0_OR_GREATER
            // Fresh short ASCII length records widen inline, saving the shared decoder's call per string; every other
            // record, including non-ASCII payloads with an equal count, keeps the validating decoder.
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && default(TOptions).StringRecording == LuminPackStringRecording.Length &&
                current is null && Vector128.IsHardwareAccelerated)
            {
                int available = Buffer.Length - Position;
                if (available >= sizeof(int) * 2)
                {
                    long header = LuminPackReader.Read<long>(ref Current);
                    int payload = (int)header;
                    if (payload == (int)(header >> 32) && (uint)(payload - 4) <= 12 && payload <= available - sizeof(int) * 2)
                    {
                        string? ascii = StringSerializer.ReadShortAsciiString(ref Unsafe.Add(ref Current, sizeof(int) * 2), payload);
                        if (ascii is not null)
                        {
                            Advance(sizeof(int) * 2 + payload);
                            return ascii;
                        }
                    }
                }
            }
#endif
            int start = Position;
            int index = start;
            // The decoders are static, so a fresh or retried read never takes this state's address.
            string? result = ReadStringAndAdvance<TOptions>(Buffer, ref index, current);
            Advance(index - start);
            return result;
        }

        #endregion

        #region Collections

        /// <summary>Validates a generated fixed-width collection's complete dynamic payload in every build.</summary>
        /// <param name = "index">The absolute beginning of the collection header or payload.</param>
        /// <param name = "byteLength">The checked number of bytes required by the collection boundary.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCollectionPayload(int index, int byteLength) => ValidateCollectionPayload(index, byteLength);

        /// <summary>Validates a raw collection copy before accessing its complete payload in every build.</summary>
        /// <param name = "index">The first payload byte after the collection header.</param>
        /// <param name = "byteLength">The checked number of payload bytes to copy.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateCollectionPayload(int index, int byteLength)
        {
            if (IsOutOfRange(index, byteLength, Buffer.Length))
                ThrowPayloadOutOfRange(index, byteLength);
        }

        /// <summary>Reports a payload that does not fit the input, without overflowing for a forged length.</summary>
        /// <param name = "index">The first payload byte.</param>
        /// <param name = "byteLength">The requested payload length.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowPayloadOutOfRange(int index, int byteLength)
        {
            long end = (long)index + byteLength;
            LuminPackExceptionHelper.ThrowSpanOutOfRange(end > int.MaxValue ? int.MaxValue : end < 0 ? 0 : (int)end);
        }

        /// <summary>Examines the current collection marker without advancing.</summary>
        /// <returns>Whether the header represents a nonnull value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullCollection()
        {
            EnsureReadable(Position, sizeof(int));
            return global::LuminPack.Core.LuminPackReader.Read<int>(ref Current) == LuminPackCode.NullCollection;
        }

        /// <summary>
        /// Array-returning variant for generated reference-element loops. Keeping the backing
        /// array as one managed reference shortens the live range compared with Span&lt;T&gt;.
        /// </summary>
        /// <param name = "count">The number of entries involved in the operation.</param>
        /// <param name = "value">Receives the value produced by the operation.</param>
        /// <typeparam name = "T">The type represented by <c>T</c>.</typeparam>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? [] CreateFreshListArray<T>(int count, out List<T?> value)
        {
            value = new List<T?>(count);
            return LuminPackMarshal.DangerousGetFreshListArray(value, count);
        }

        /// <summary>Bounds a presizing hint for a collection whose elements occupy at least one input byte each.</summary>
        /// <param name = "count">The nonnegative count read from the collection header.</param>
        /// <returns>The count, limited by the remaining input so a forged header cannot force a large allocation.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCapacityHint(int count)
        {
            return Math.Min(count, Buffer.Length - Position);
        }

        #endregion

        #region Framing

        /// <summary>Reads var int byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public byte ReadVarIntByte()
        {
            sbyte typeCode = this.Read<sbyte>();
            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return this.Read<byte>();
                case VarIntCodes.SByte:
                    return checked((byte)this.Read<sbyte>());
                case VarIntCodes.UInt16:
                    return checked((byte)this.Read<ushort>());
                case VarIntCodes.Int16:
                    return checked((byte)this.Read<short>());
                case VarIntCodes.UInt32:
                    return checked((byte)this.Read<uint>());
                case VarIntCodes.Int32:
                    return checked((byte)this.Read<int>());
                case VarIntCodes.UInt64:
                    return checked((byte)this.Read<ulong>());
                case VarIntCodes.Int64:
                    return checked((byte)this.Read<long>());
                default:
                    return checked((byte)typeCode);
            }
        }

        /// <summary>Reads var int s byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public sbyte ReadVarIntSByte()
        {
            sbyte typeCode = this.Read<sbyte>();
            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return checked((sbyte)this.Read<byte>());
                case VarIntCodes.SByte:
                    return this.Read<sbyte>();
                case VarIntCodes.UInt16:
                    return checked((sbyte)this.Read<ushort>());
                case VarIntCodes.Int16:
                    return checked((sbyte)this.Read<short>());
                case VarIntCodes.UInt32:
                    return checked((sbyte)this.Read<uint>());
                case VarIntCodes.Int32:
                    return checked((sbyte)this.Read<int>());
                case VarIntCodes.UInt64:
                    return checked((sbyte)this.Read<ulong>());
                case VarIntCodes.Int64:
                    return checked((sbyte)this.Read<long>());
                default:
                    return typeCode;
            }
        }

        /// <summary>Reads var int u int16 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public ushort ReadVarIntUInt16()
        {
            sbyte typeCode = this.Read<sbyte>();
            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return this.Read<byte>();
                case VarIntCodes.SByte:
                    return checked((ushort)this.Read<sbyte>());
                case VarIntCodes.UInt16:
                    return this.Read<ushort>();
                case VarIntCodes.Int16:
                    return checked((ushort)this.Read<short>());
                case VarIntCodes.UInt32:
                    return checked((ushort)this.Read<uint>());
                case VarIntCodes.Int32:
                    return checked((ushort)this.Read<int>());
                case VarIntCodes.UInt64:
                    return checked((ushort)this.Read<ulong>());
                case VarIntCodes.Int64:
                    return checked((ushort)this.Read<long>());
                default:
                    return checked((ushort)typeCode);
            }
        }

        /// <summary>Reads var int int16 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public short ReadVarIntInt16()
        {
            sbyte typeCode = this.Read<sbyte>();
            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return this.Read<byte>();
                case VarIntCodes.SByte:
                    return this.Read<sbyte>();
                case VarIntCodes.UInt16:
                    return checked((short)this.Read<ushort>());
                case VarIntCodes.Int16:
                    return this.Read<short>();
                case VarIntCodes.UInt32:
                    return checked((short)this.Read<uint>());
                case VarIntCodes.Int32:
                    return checked((short)this.Read<int>());
                case VarIntCodes.UInt64:
                    return checked((short)this.Read<ulong>());
                case VarIntCodes.Int64:
                    return checked((short)this.Read<long>());
                default:
                    return typeCode;
            }
        }

        /// <summary>Reads var int u int32 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public uint ReadVarIntUInt32()
        {
            sbyte typeCode = this.Read<sbyte>();
            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return this.Read<byte>();
                case VarIntCodes.SByte:
                    return checked((uint)this.Read<sbyte>());
                case VarIntCodes.UInt16:
                    return this.Read<ushort>();
                case VarIntCodes.Int16:
                    return checked((uint)this.Read<short>());
                case VarIntCodes.UInt32:
                    return this.Read<uint>();
                case VarIntCodes.Int32:
                    return checked((uint)this.Read<int>());
                case VarIntCodes.UInt64:
                    return checked((uint)this.Read<ulong>());
                case VarIntCodes.Int64:
                    return checked((uint)this.Read<long>());
                default:
                    return checked((uint)typeCode);
            }
        }

        /// <summary>Reads var int int32 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public int ReadVarIntInt32()
        {
            sbyte typeCode = this.Read<sbyte>();
            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return this.Read<byte>();
                case VarIntCodes.SByte:
                    return this.Read<sbyte>();
                case VarIntCodes.UInt16:
                    return this.Read<ushort>();
                case VarIntCodes.Int16:
                    return this.Read<short>();
                case VarIntCodes.UInt32:
                    return checked((int)this.Read<uint>());
                case VarIntCodes.Int32:
                    return this.Read<int>();
                case VarIntCodes.UInt64:
                    return checked((int)this.Read<ulong>());
                case VarIntCodes.Int64:
                    return checked((int)this.Read<long>());
                default:
                    return typeCode;
            }
        }

        /// <summary>Reads var int u int64 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public ulong ReadVarIntUInt64()
        {
            sbyte typeCode = this.Read<sbyte>();
            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return this.Read<byte>();
                case VarIntCodes.SByte:
                    return checked((ulong)this.Read<sbyte>());
                case VarIntCodes.UInt16:
                    return this.Read<ushort>();
                case VarIntCodes.Int16:
                    return checked((ulong)this.Read<short>());
                case VarIntCodes.UInt32:
                    return this.Read<uint>();
                case VarIntCodes.Int32:
                    return checked((ulong)this.Read<int>());
                case VarIntCodes.UInt64:
                    return this.Read<ulong>();
                case VarIntCodes.Int64:
                    return checked((ulong)this.Read<long>());
                default:
                    return checked((ulong)typeCode);
            }
        }

        /// <summary>Reads var int int64 from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        public long ReadVarIntInt64()
        {
            sbyte typeCode = this.Read<sbyte>();
            switch (typeCode)
            {
                case VarIntCodes.Byte:
                    return this.Read<byte>();
                case VarIntCodes.SByte:
                    return this.Read<sbyte>();
                case VarIntCodes.UInt16:
                    return this.Read<ushort>();
                case VarIntCodes.Int16:
                    return this.Read<short>();
                case VarIntCodes.UInt32:
                    return this.Read<uint>();
                case VarIntCodes.Int32:
                    return this.Read<int>();
                case VarIntCodes.UInt64:
                    return checked((long)this.Read<ulong>());
                case VarIntCodes.Int64:
                    return this.Read<long>();
                default:
                    return typeCode;
            }
        }

        /// <summary>Reads a named version-tolerant header and rejects unrelated record formats.</summary>
        /// <returns>The member count, or minus one for a null object.</returns>
        public int ReadVersionHeader() => ReadVersionHeader<LuminPackDefaultOptions>();

        /// <summary>Reads a named header and rejects incompatible format revisions or binary string profiles.</summary>
        /// <typeparam name = "TOptions">The compiled receiving profile.</typeparam>
        /// <returns>The member count, or minus one for a null object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadVersionHeader<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            ValidateCollectionPayload(Position, 1);
            if (Current == 255)
            {
                Advance(1);
                return -1;
            }

            return ReadNonNullVersionHeader<TOptions>();
        }

        /// <summary>Decodes an evolved non-null header outside the common matching-schema native body.</summary>
        /// <typeparam name = "TOptions">The compiled receiving profile.</typeparam>
        /// <returns>The validated member count.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadNonNullVersionHeader<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            byte marker = Read<byte>();
            if (marker != 251)
                LuminPackExceptionHelper.ThrowFormatException("Expected a named version-tolerant record.");
            ValidateCollectionPayload(Position, 3);
            byte revision = Read<byte>();
            byte profile = Read<byte>();
            int count = Read<byte>();
            if (revision != 1 || count > 249)
                LuminPackExceptionHelper.ThrowFormatException("Unsupported version-tolerant record header.");
            if (profile != (((int)default(TOptions).StringEncoding << 1) | (int)default(TOptions).StringRecording))
                LuminPackExceptionHelper.ThrowFormatException("The version-tolerant binary string profile does not match the input.");
            return count;
        }

        /// <summary>Maps and validates a complete dynamic member table before reading any payload.</summary>
        /// <param name = "fields">The generated receiving schema.</param>
        /// <param name = "slots">Receives local member indices, or minus one for unknown fields.</param>
        /// <param name = "lengths">Receives payload lengths in table order.</param>
        /// <param name = "present">Receives the set of local fields supplied by the input.</param>
        public void ReadVersionTable(LuminPackVersionField[] fields, scoped Span<int> slots, scoped Span<int> lengths, scoped Span<byte> present)
        {
            present.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                int slot;
                if (i < fields.Length && fields[i].Prefix.Length <= Buffer.Length - Position && Buffer.Slice(Position, fields[i].Prefix.Length).SequenceEqual(fields[i].Prefix))
                {
                    Advance(fields[i].Prefix.Length);
                    slot = i;
                }
                else
                {
                    var name = ReadVersionMetadata();
                    var contract = ReadVersionMetadata();
                    slot = LuminPackVersionField.Find(fields, name, i);
                    if (slot >= 0 && !contract.SequenceEqual(fields[slot].Contract))
                        LuminPackExceptionHelper.ThrowFormatException("The version-tolerant member type or binary profile has changed.");
                }

                slots[i] = slot;
                if (slot >= 0)
                {
                    if (present[slot] != 0)
                        LuminPackExceptionHelper.ThrowFormatException("Duplicate version-tolerant member identity.");
                    present[slot] = 1;
                }
            }

            ReadVersionLengths(lengths);
        }

        /// <summary>Maps a stack table without a by-reference receiver for compilers that do not understand scoped Span metadata.</summary>
        /// <param name="reader">The input state whose cursor is copied for this metadata-only operation.</param>
        /// <param name="fields">The receiving schema.</param>
        /// <param name="slots">The incoming member slots.</param>
        /// <param name="lengths">The incoming payload lengths.</param>
        /// <param name="present">A writable range with one byte per receiving field.</param>
        /// <returns>The validated metadata end to publish to the original reader.</returns>
        public static int ReadVersionTable(LuminPackReader reader, LuminPackVersionField[] fields, scoped Span<int> slots, scoped Span<int> lengths, scoped Span<byte> present)
        {
            reader.ReadVersionTable(fields, slots, lengths, present);
            return reader.Position;
        }

        /// <summary>Matches a complete cached schema without decoding or mapping individual member names.</summary>
        /// <param name = "metadata">The receiving model's complete immutable metadata.</param>
        /// <returns>Whether the complete table matched and was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadVersionMetadata(scoped ReadOnlySpan<byte> metadata)
        {
            if ((uint)Position > (uint)Buffer.Length || metadata.Length > Buffer.Length - Position || !System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref Current, metadata.Length).SequenceEqual(metadata))
                return false;
            Advance(metadata.Length);
            return true;
        }

        /// <summary>Matches a complete header and metadata block with the default binary profile.</summary>
        /// <param name = "metadata">The generated complete name and type table.</param>
        /// <param name = "count">The exact receiving member count.</param>
        /// <returns>Whether the complete matching prefix was consumed.</returns>
        public bool TryReadVersionSchema(scoped ReadOnlySpan<byte> metadata, byte count) => TryReadVersionSchema<LuminPackDefaultOptions>(metadata, count);

        /// <summary>Matches a complete schema prefix before entering the known-order read path.</summary>
        /// <typeparam name = "TOptions">The compiled binary profile.</typeparam>
        /// <param name = "metadata">The generated complete name and type table.</param>
        /// <param name = "count">The exact receiving member count.</param>
        /// <returns>Whether the matching header and table were consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadVersionSchema<TOptions>(scoped ReadOnlySpan<byte> metadata, byte count)
            where TOptions : struct, ILuminPackOptions
        {
            int available = Buffer.Length - Position;
            if ((uint)Position > (uint)Buffer.Length || available < 4 || metadata.Length > available - 4)
                return false;
            global::LuminPack.Core.LuminPackReader.Read(ref Current, out uint header);
            uint profile = ((uint)default(TOptions).StringEncoding << 1) | (uint)default(TOptions).StringRecording;
            uint expected = 251u | (1u << 8) | (profile << 16) | ((uint)count << 24);
            if (header != expected || !System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref Current, 4), metadata.Length).SequenceEqual(metadata))
                return false;
            Advance(4 + metadata.Length);
            return true;
        }

        /// <summary>Matches a cached complete default-profile prefix or the selected alternate profile.</summary>
        /// <param name = "metadata">The exact name and type table.</param>
        /// <param name = "count">The receiving member count.</param>
        /// <param name = "defaultPrefix">The cached default header and metadata block.</param>
        /// <returns>Whether the complete matching prefix was consumed.</returns>
        public bool TryReadVersionSchema(scoped ReadOnlySpan<byte> metadata, byte count, scoped ReadOnlySpan<byte> defaultPrefix) => TryReadVersionSchema<LuminPackDefaultOptions>(metadata, count, defaultPrefix);

        /// <summary>Fuses the common header and metadata comparison into one constant-length operation.</summary>
        /// <typeparam name = "TOptions">The compiled receiving profile.</typeparam>
        /// <param name = "metadata">The exact name and type table.</param>
        /// <param name = "count">The receiving member count.</param>
        /// <param name = "defaultPrefix">The cached UTF-8 length-profile header and metadata.</param>
        /// <returns>Whether the matching header and metadata were consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadVersionSchema<TOptions>(scoped ReadOnlySpan<byte> metadata, byte count, scoped ReadOnlySpan<byte> defaultPrefix)
            where TOptions : struct, ILuminPackOptions => default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && default(TOptions).StringRecording == LuminPackStringRecording.Length ? TryReadVersionMetadata(defaultPrefix) : TryReadVersionSchema<TOptions>(metadata, count);

        /// <summary>Reads a complete payload-length table and validates its combined input boundary.</summary>
        /// <param name = "lengths">Receives lengths in wire order.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadVersionLengths(scoped Span<int> lengths)
        {
            // Every length has at least one byte. Extended records revalidate the remaining table.
            ValidateCollectionPayload(Position, lengths.Length);
            long total = 0;
            for (int i = 0; i < lengths.Length; i++)
            {
                byte first = Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position));
                int length;
                if (first <= 127)
                {
                    Advance(1);
                    length = first;
                }
                else
                {
                    length = ReadVersionExtendedLength();
                    ValidateCollectionPayload(Position, lengths.Length - i - 1);
                }

                lengths[i] = length;
                total += length;
            }

            if (total > int.MaxValue)
                LuminPackExceptionHelper.ThrowFormatException("Version-tolerant payload is too large.");
            ValidateCollectionPayload(Position, (int)total);
        }

        /// <summary>Validates a generated table's minimum width or its complete payload boundary.</summary>
        /// <param name = "length">The aggregate byte count, calculated without narrowing arithmetic.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ValidateVersionPayloadSize(long length)
        {
            if ((ulong)length > int.MaxValue)
                LuminPackExceptionHelper.ThrowFormatException("Version-tolerant payload is too large.");
            ValidateCollectionPayload(Position, (int)length);
        }

        /// <summary>Reads a known-schema length after the complete table's minimum width has been validated.</summary>
        /// <param name = "remainingEntries">The minimum byte count still required by subsequent table entries.</param>
        /// <returns>The nonnegative payload length.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadVersionLength(int remainingEntries)
        {
            byte first = Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position));
            if (first <= 127)
            {
                Advance(1);
                return first;
            }

            int length = ReadVersionExtendedLength();
            ValidateCollectionPayload(Position, remainingEntries);
            return length;
        }

        /// <summary>Validates and reads the uncommon extended payload length.</summary>
        /// <returns>The nonnegative payload length.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadVersionExtendedLength()
        {
            sbyte code = (sbyte)Current;
            int width = code switch
            {
                VarIntCodes.Byte or VarIntCodes.SByte => 2,
                VarIntCodes.UInt16 or VarIntCodes.Int16 => 3,
                VarIntCodes.UInt32 or VarIntCodes.Int32 => 5,
                VarIntCodes.UInt64 or VarIntCodes.Int64 => 9,
                _ => 1
            };
            ValidateCollectionPayload(Position, width);
            int length = ReadVarIntInt32();
            if (length < 0)
                LuminPackExceptionHelper.ThrowFormatException("Negative version-tolerant payload length.");
            return length;
        }

        /// <summary>Reads one bounded metadata byte string without allocating a managed string.</summary>
        /// <returns>The input metadata bytes.</returns>
        private ReadOnlySpan<byte> ReadVersionMetadata()
        {
            uint length = 0;
            for (int shift = 0;; shift += 7)
            {
                ValidateCollectionPayload(Position, 1);
                byte next = Read<byte>();
                if (shift == 28 && next > 7)
                    LuminPackExceptionHelper.ThrowFormatException("Invalid version-tolerant metadata length.");
                length |= (uint)(next & 127) << shift;
                if (next < 128)
                    break;
            }

            ValidateCollectionPayload(Position, (int)length);
            var result = Buffer.Slice(Position, (int)length);
            Advance((int)length);
            return result;
        }

        /// <summary>Restricts dynamic input to one field without changing the reader or operation-state identity.</summary>
        /// <param name = "length">The validated field payload length.</param>
        /// <returns>The previous input length, which must be restored in a finally block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EnterVersionField(int length)
        {
            ValidateCollectionPayload(Position, length);
            int previousLength = Buffer.Length;
            Buffer = Buffer.Slice(0, Position + length);
            return previousLength;
        }

        /// <summary>Restores the exact enclosing input boundary after a generated field operation.</summary>
        /// <param name = "previousLength">The length returned by the matching EnterVersionField call.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitVersionField(int previousLength)
        {
            Buffer = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(0)), previousLength);
        }

        /// <summary>Checks a known field's completed read against its table length.</summary>
        /// <param name = "actual">The completed absolute cursor.</param>
        /// <param name = "expected">The expected absolute end.</param>
        public static void ValidateVersionFieldEnd(int actual, int expected)
        {
            if (actual != expected)
                LuminPackExceptionHelper.ThrowFormatException("Version-tolerant field length does not match its payload.");
        }

        #endregion
}
}
