using System.Buffers;
using System.Buffers.Binary;
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
    public ref partial struct LuminPackReader
    {
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
            LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, (uint)byteCount);
        }

        /// <summary>Returns the current input byte and whether <paramref name="length"/> bytes remain from it.</summary>
        /// <param name = "length">The nonnegative byte count a generated comparison needs.</param>
        /// <param name = "available">Whether the bytes remain; nothing may be read through the result otherwise.</param>
        /// <returns>The current input byte.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte PeekInput(int length, out bool available)
        {
#if NET8_0_OR_GREATER
            available = (long)Unsafe.ByteOffset(ref Cursor, ref _end) >= length;
#else
            available = (long)Unsafe.ByteOffset(ref Current, ref MemoryMarshal.GetReference(_end)) >= length;
#endif
            return ref Current;
        }

        /// <summary>Copies a fixed storage batch and tests the Boolean bytes of its input through one eight-byte window.</summary>
        /// <param name = "destination">The first byte of the complete fixed storage batch.</param>
        /// <param name = "byteCount">The batch width, at least <paramref name="window"/> plus eight.</param>
        /// <param name = "window">The batch offset of the tested window.</param>
        /// <param name = "mask">The non-low bits of every Boolean byte in the window.</param>
        /// <returns>Whether every Boolean byte is zero or one.</returns>
        /// <remarks>The test reads the input rather than the copied fields, so it never waits for the copy's stores.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadCanonicalBatch(scoped ref byte destination, int byteCount, int window, ulong mask)
        {
            ref byte source = ref Current;
            Advance(byteCount);
            // 32-bit targets keep typed unaligned access within four bytes.
            ulong bits = Unsafe.SizeOf<IntPtr>() == 8
                ? Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, (nint)(uint)window))
                : Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, (nint)(uint)window)) |
                  (ulong)Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, (nint)(uint)window + 4)) << 32;
            LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, (uint)byteCount);
            return (bits & mask) == 0;
        }

        /// <summary>Copies a fixed storage batch and tests the Boolean bytes of its input through one four-byte window.</summary>
        /// <param name = "destination">The first byte of the complete fixed storage batch.</param>
        /// <param name = "byteCount">The batch width, at least <paramref name="window"/> plus four.</param>
        /// <param name = "window">The batch offset of the tested window.</param>
        /// <param name = "mask">The non-low bits of every Boolean byte in the window.</param>
        /// <returns>Whether every Boolean byte is zero or one.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadCanonicalBatch(scoped ref byte destination, int byteCount, int window, uint mask)
        {
            ref byte source = ref Current;
            Advance(byteCount);
            uint bits = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, (nint)(uint)window));
            LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, (uint)byteCount);
            return (bits & mask) == 0;
        }

        /// <summary>Reads a contiguous payload directly into its selected destination storage.</summary>
        /// <typeparam name = "T">The reference-free element storage type.</typeparam>
        /// <param name = "value">The complete destination span.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<T>(scoped Span<T> value)
            where T : unmanaged
        {
            int bytes = checked(value.Length * Unsafe.SizeOf<T>());
            ref byte input = ref Current;
            ValidateCurrentCollectionPayload(ref input, bytes);
            if (bytes != 0)
                LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(value)), ref input, (uint)bytes);
            AdvanceValidated(bytes);
        }

        /// <summary>Reads a collection count and rejects a payload that cannot fit before allocation.</summary>
        /// <param name = "minimumElementSize">The minimum wire width of one element.</param>
        /// <returns>The nonnegative count, or minus one for a null collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadLength(int minimumElementSize = 1)
        {
            ref byte input = ref Current;
            int room = ValidateCurrentCollectionPayload(ref input, sizeof(int));
            LuminPackReader.Read(ref input, out int count);
            if (count < -1)
                LuminPackExceptionHelper.ThrowInvalidCollectionLength(count);
            if (minimumElementSize != 0 && count >= 0)
            {
                // Keep the existing overflow limit and range diagnostic for a forged count.
                int width = CollectionPayloadWidth(count, minimumElementSize);
                if ((uint)width > (uint)room)
                    ThrowPayloadOutOfRange(_buffer.Length - room, width);
            }
            AdvanceValidated(sizeof(int));
            return count;
        }

        /// <summary>Checks a complete fixed-width payload before entering a bounded collection loop.</summary>
        /// <param name="count">The number of records after the header.</param>
        /// <param name="width">The proven positive wire width of one record.</param>
        /// <returns>Whether the reader advanced over the whole payload. A short window keeps the original cursor for scalar reads.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryAdvanceFixedPayload(int count, int width)
        {
            long bytes = (long)count * width;
            int position = Position;
            int length = _buffer.Length;
            if (count <= 0 || (uint)position > (uint)length || bytes > length - position)
                return false;

            Advance((int)bytes);
            return true;
        }

        /// <summary>Computes the minimum payload width using the existing overflow limit.</summary>
        /// <param name="count">The nonnegative collection count.</param>
        /// <param name="minimumElementSize">The minimum encoded width per element.</param>
        /// <returns>The minimum payload width capped at the largest possible reader window.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CollectionPayloadWidth(int count, int minimumElementSize)
        {
            if (minimumElementSize == 1)
                return count;

            long required = (long)count * minimumElementSize;
            return required > int.MaxValue ? int.MaxValue : (int)required;
        }

        /// <summary>Validates the current source window and returns the unread width after the requested bytes.</summary>
        /// <param name="input">The reader's current byte.</param>
        /// <param name="byteCount">The nonnegative collection header or payload width.</param>
        /// <returns>The readable bytes after this range.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ValidateCurrentCollectionPayload(ref byte input, int byteCount)
        {
#if NET8_0_OR_GREATER
            if (IntPtr.Size == 8)
            {
                long room = (long)Unsafe.ByteOffset(ref input, ref _end);
                if (!ContainsEndRange(room, byteCount, _buffer.Length))
                    ThrowPayloadOutOfRange((int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref input), byteCount);
                return (int)room - byteCount;
            }
#endif
            nint index = Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref input);
            int last = _buffer.Length - byteCount;
            if (last < 0 || unchecked((nuint)index) > (nuint)last)
                ThrowPayloadOutOfRange((int)index, byteCount);
            return last - (int)index;
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
                ValidateCollectionPayload(position + sizeof(int), CollectionPayloadWidth(length, minimumElementSize));
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

        /// <summary>Checks the complete record before reusing rectangular array storage.</summary>
        /// <param name="width">The rectangular header and payload width.</param>
        /// <returns>Whether the entire record fits in a supported input window.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanReadRectangularRecord(long width)
        {
            if (IntPtr.Size != 8 || (ulong)width > (uint)_buffer.Length)
                return false;
            nint position = Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref Current);
            return (nuint)position <= (nuint)(_buffer.Length - width);
        }

        /// <summary>Consumes a short, validated block of native elements for immediate immutable construction.</summary>
        /// <typeparam name="T">The raw unmanaged element type.</typeparam>
        /// <param name="count">The number of elements.</param>
        /// <param name="items">The complete borrowed input, valid during construction.</param>
        /// <returns>Whether the native input was consumed.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadSmallNativeSpan<T>(int count, out ReadOnlySpan<T> items) where T : unmanaged
        {
            if (IntPtr.Size != 8 || (uint)count > 8)
            {
                items = default;
                return false;
            }
            int bytes = checked(count * Unsafe.SizeOf<T>());
            int start = Position;
            Advance(bytes);
            items = MemoryMarshal.Cast<byte, T>(_buffer.Slice(start, bytes));
            return true;
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
            ref byte marker = ref Current;
            EnsureReadableByte(ref marker);
            tag = marker;
            AdvanceValidated(1);
            return tag is not LuminPackCode.NullObject;
        }

        /// <summary>Validates one byte and returns its window-relative offset.</summary>
        /// <param name="source">The current input byte.</param>
        /// <returns>The validated offset of the input byte.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly int EnsureReadableByte(scoped ref byte source)
        {
#if NET8_0_OR_GREATER
            if (IntPtr.Size == 8)
            {
                long room = (long)Unsafe.ByteOffset(ref source, ref _end);
                if (!ContainsEndRange(room, 1, _buffer.Length))
                    LuminPackExceptionHelper.ThrowSpanOutOfRange((long)(int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref source) + 1);
                return _buffer.Length - (int)room;
            }
#endif
            nint offset = Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref source);
            if (unchecked((nuint)offset) >= (uint)_buffer.Length)
                LuminPackExceptionHelper.ThrowSpanOutOfRange((long)unchecked((int)offset) + 1);
            return (int)offset;
        }

        /// <summary>Attempts to peek wide union header and reports whether it succeeded.</summary>
        /// <param name = "tag">Receives the tag produced by the operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekWideUnionHeader(out ushort tag)
        {
            ref byte marker = ref Current;
            int position = EnsureReadableByte(ref marker);
            byte firstTag = marker;
            if (firstTag < LuminPackCode.WideTag)
            {
                tag = firstTag;
                AdvanceValidated(1);
                return true;
            }
            else if (firstTag == LuminPackCode.WideTag)
            {
                if (_buffer.Length - position < 3)
                    LuminPackExceptionHelper.ThrowSpanOutOfRange((long)position + 3);
                tag = LuminPackReader.Read<ushort>(ref Unsafe.Add(ref marker, 1));
                AdvanceValidated(3);
                return true;
            }
            else
            {
                tag = 0;
                AdvanceValidated(1);
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
            int size = Unsafe.SizeOf<T>();
            if (size == 1)
                EnsureReadableByte(ref Current);
            else
                EnsureReadableScalar(size);
            Read(ref Current, out value);
            AdvanceValidated(size);
        }

        /// <summary>Validates a fixed scalar against both ends of the active reader window.</summary>
        /// <param name="size">The width of the scalar storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureReadableScalar(int size)
        {
#if NET8_0_OR_GREATER
            long remaining = (long)Unsafe.ByteOffset(ref Cursor, ref _end);
#else
            long remaining = (long)Unsafe.ByteOffset(ref Current, ref MemoryMarshal.GetReference(_end));
#endif
            // A cursor before the buffer has more than its full length remaining.
#if NET8_0_OR_GREATER
            if (IntPtr.Size == 8 ? !ContainsEndRange(remaining, size, _buffer.Length) : remaining < size || remaining > _buffer.Length)
#else
            if (remaining < size || remaining > _buffer.Length)
#endif
                LuminPackExceptionHelper.ThrowSpanOutOfRange((long)Position + size);
        }

#if NET8_0_OR_GREATER
        /// <summary>Reads a present nullable's native payload and publishes the complete value.</summary>
        /// <typeparam name="T">The native, unmanaged payload type.</typeparam>
        /// <param name="value">The nullable destination after its present marker was consumed.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadNativeNullablePayload<T>(scoped ref T? value)
            where T : unmanaged
        {
            if (IntPtr.Size != 8)
            {
                value = Read<T>();
                return;
            }
            ref T payload = ref Unsafe.AsRef(in Nullable.GetValueRefOrDefaultRef(in value));
            if (Unsafe.SizeOf<T>() == 12 && Unsafe.SizeOf<T?>() == 16 && IntPtr.Size == 8 &&
                BitConverter.IsLittleEndian && Vector128.IsHardwareAccelerated &&
                Unsafe.AreSame(ref Unsafe.Add(ref Unsafe.As<T?, byte>(ref value), 4), ref Unsafe.As<T, byte>(ref payload)))
            {
                EnsureReadableScalar(12);
                ref byte source = ref Cursor;
                // The native value starts four bytes after HasValue. Read exactly twelve input bytes.
                ulong low = 1u | ((ulong)Unsafe.ReadUnaligned<uint>(ref source) << 32);
                ulong high = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, 4));
                Unsafe.As<T?, Vector128<ulong>>(ref value) = Vector128.Create(low, high);
                AdvanceValidated(12);
                return;
            }

            Read(ref payload);
            Unsafe.As<T?, bool>(ref value) = true;
        }
#endif

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

            bool destinationIsNew = value is null || value.Length != count;
            if (destinationIsNew)
                value = LuminPackMarshal.AllocateUninitializedArray<T>(count);
            ReadPayload(source.Slice(offset, payloadBytes), value.AsSpan(), compressed, destinationIsNew);
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

            bool destinationIsNew = value.Length != count;
            if (destinationIsNew)
                value = LuminPackMarshal.AllocateUninitializedArray<T>(count);
            ReadPayload(source.Slice(offset, payloadBytes), value, compressed, destinationIsNew);
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
            // LuminZ produces at most 255 bytes per encoded byte, so a larger claim is rejected before its storage is allocated.
            if (compressed && decodedBytes / 256 > payloadBytes)
                LuminPackExceptionHelper.ThrowInvalidDataException("Compressed collection output length exceeds what its payload can encode.");
            if (compressed && LuminCompressor.GetDecompressedSize(source.Slice(offset, payloadBytes)) != decodedBytes)
                LuminPackExceptionHelper.ThrowInvalidDataException("Compressed collection output length does not match its element count.");
            return checked(offset + payloadBytes);
        }

        /// <summary>Shares the single contiguous payload copy between array and span wrappers.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name="source">The already validated encoded payload.</param>
        /// <param name="value">The complete destination storage.</param>
        /// <param name="compressed">Whether the payload requires LuminZ decoding.</param>
        /// <param name="destinationIsNew">Whether the destination array was allocated by this read and is not yet in cache.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadPayload<T>(ReadOnlySpan<byte> source, Span<T> value, bool compressed, bool destinationIsNew)
        {
            int bytes = checked(value.Length * Unsafe.SizeOf<T>());
            ref byte destination = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(value));
            if (compressed)
                LuminCompressor.Decompress(source, LuminPackMarshal.CreateSpan(ref destination, bytes));
            else
                LuminPackMarshal.CopyBlockUnaligned(ref destination, ref MemoryMarshal.GetReference(source), (uint)bytes, destinationIsNew);
        }

        /// <summary>Processes a fixed storage batch without introducing a Release bounds check.</summary>
        /// <param name = "reference">The first byte of the complete fixed storage batch.</param>
        /// <typeparam name = "T1">The reference-free storage type.</typeparam>
        /// <param name = "value1">The field participating in the memory operation.</param>
        /// <returns>The complete packed wire width.</returns>
        /// <remarks>A value of at most four bytes uses typed unaligned access on both pointer widths, as the writer's
        /// scalar store does; wider values keep the block copy on 32-bit targets.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T1>(ref byte reference, out T1 value1)
        {
            if (Unsafe.SizeOf<IntPtr>() == 8 || Unsafe.SizeOf<T1>() <= 4)
            {
                value1 = Unsafe.ReadUnaligned<T1>(ref reference);
            }
            else
            {
                value1 = default!;
                LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref reference, (uint)Unsafe.SizeOf<T1>());
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
    }
}
