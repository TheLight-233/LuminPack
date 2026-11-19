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
    public ref partial struct LuminPackWriter
    {
        /// <summary>Copies a proven fixed storage group and advances once without adding a Release bounds check.</summary>
        /// <param name = "source">The first byte of the proven adjacent fields.</param>
        /// <param name = "byteCount">The complete fixed storage width.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(scoped ref byte source, int byteCount)
        {
            LuminPackMarshal.CopyBlockUnaligned(ref Current, ref source, (uint)byteCount);
            Advance(byteCount);
        }

        /// <summary>Writes and advances past a null object header.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullObjectHeader()
        {
            Write(LuminPackCode.NullObject);
        }

        /// <summary>Reserves and writes a nullable value whose standalone payload is native storage.</summary>
        /// <typeparam name="T">The unmanaged payload type.</typeparam>
        /// <param name="value">The nullable payload.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullable<T>(T? value) where T : unmanaged
        {
            ref byte cursor = ref ReserveRecord(ref Current, value.HasValue ? 1 + Unsafe.SizeOf<T>() : 1);
            CommitRecords(ref WriteReservedNullable(ref cursor, value));
        }

        /// <summary>Writes the nullable marker and any native payload into a reserved record.</summary>
        /// <typeparam name="T">The unmanaged payload type.</typeparam>
        /// <param name="cursor">The current byte, followed by room for the complete value.</param>
        /// <param name="value">The nullable payload.</param>
        /// <returns>The cursor after the written record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte WriteReservedNullable<T>(ref byte cursor, T? value) where T : unmanaged
        {
            ref byte payload = ref Unsafe.Add(ref cursor, 1);
            if (!value.HasValue)
            {
                cursor = LuminPackCode.NullObject;
                return ref payload;
            }
            cursor = 0;
            T item = value.GetValueOrDefault();
            return ref WriteReserved(ref payload, in item);
        }

        /// <summary>Writes and advances past a collection length.</summary>
        /// <param name = "length">The collection element count.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCollectionHeader(int length)
        {
            Write(length);
        }

        /// <summary>Writes and advances past a null collection header.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullCollectionHeader()
        {
            Write(LuminPackCode.NullCollection);
        }

        /// <summary>Writes and advances past an object member count.</summary>
        /// <param name = "memberCount">The serialized member count.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectHeader(byte memberCount)
        {
            Write(memberCount);
        }

        /// <summary>Writes an ordinary object's member count and first scalar field with one cursor update.</summary>
        /// <param name="memberCount">The serialized member count.</param>
        /// <param name="field">The directly accessible first field of a non-null object.</param>
        /// <typeparam name="T">The scalar storage type.</typeparam>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectHeader<T>(byte memberCount, in T field) where T : unmanaged
        {
#if DEBUG
            WriteObjectHeader(memberCount);
            Write(in field);
#else
            ref byte header = ref Current;
            header = memberCount;
            Write(ref Unsafe.Add(ref header, 1), in field);
            AdvanceProven(1 + Unsafe.SizeOf<T>());
#endif
        }

        /// <summary>Writes and advances past a narrow Union tag.</summary>
        /// <param name = "tag">The declared narrow tag.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnionHeader(ushort tag)
        {
            Write((byte)tag);
        }

        /// <summary>Writes and advances past a narrow or wide Union tag.</summary>
        /// <param name = "tag">The declared Union tag.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteWideUnionHeader(ushort tag)
        {
            if (tag < LuminPackCode.WideTag)
            {
                WriteUnionHeader(tag);
                return;
            }

            ref byte current = ref Current;
            current = LuminPackCode.WideTag;
            LuminPackWriter.Write(ref Unsafe.Add(ref current, 1), tag);
            Advance(3);
        }

        /// <summary>Writes and advances past a null Union header.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullUnionHeader()
        {
            WriteNullObjectHeader();
        }

        /// <summary>Stores a variable-length unsigned integer into reserved room of at least five bytes.</summary>
        /// <param name = "cursor">The caller's current position in reserved room.</param>
        /// <param name = "value">The value.</param>
        /// <returns>The position after the encoded value.</returns>
        /// <remarks>The encoding equals <see cref="WriteVarInt(uint)"/>; each store stays within four bytes for 32-bit targets.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte StoreVarInt(ref byte cursor, uint value)
        {
            if (value <= VarIntCodes.MaxSingleValue)
            {
                cursor = (byte)value;
                return ref Unsafe.Add(ref cursor, 1);
            }
            return ref StoreExtendedVarInt(ref cursor, value);
        }

        /// <summary>Stores a variable-length unsigned integer wider than one byte.</summary>
        /// <param name = "cursor">The caller's current position in reserved room.</param>
        /// <param name = "value">The value, above the single-byte range.</param>
        /// <returns>The position after the encoded value.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref byte StoreExtendedVarInt(ref byte cursor, uint value)
        {
            if (value <= ushort.MaxValue)
            {
                cursor = unchecked((byte)VarIntCodes.UInt16);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref cursor, 1), (ushort)value);
                return ref Unsafe.Add(ref cursor, 3);
            }
            cursor = unchecked((byte)VarIntCodes.UInt32);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref cursor, 1), value);
            return ref Unsafe.Add(ref cursor, 5);
        }

        /// <summary>Writes a reference marker and its variable-length object identifier.</summary>
        /// <param name = "referenceId">The identifier already registered in the operation state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectReferenceId(uint referenceId)
        {
            int width = referenceId <= VarIntCodes.MaxSingleValue ? 2 : referenceId <= ushort.MaxValue ? 4 : 6;
            EnsureAdditionalCapacity(width);
            ref byte destination = ref Current;
            destination = LuminPackCode.ReferenceId;
            StoreVarInt(ref Unsafe.Add(ref destination, 1), referenceId);
            AdvanceProven(width);
        }

        /// <summary>Writes contiguous storage with a caller-selected logical count, including bit-oriented containers.</summary>
        /// <typeparam name = "T">The reference-free element storage type.</typeparam>
        /// <param name = "value">The complete contiguous payload.</param>
        /// <param name = "count">The logical count recorded in the header.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(scoped ReadOnlySpan<T> value, int count)
            where T : unmanaged
        {
            int bytes = checked(value.Length * Unsafe.SizeOf<T>());
            int length = checked(sizeof(int) + bytes);
            ref byte source = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(value));
            // The original reservation, borrowed-source lifetime and completed-position growth ordering are retained;
            // growth goes through the static helpers, so the writer's address stays unexposed.
            bool fits = FitsRecord(length);
            if (!fits)
                EnsureAdditionalCapacity(length);
            WritePayload(ref Current, ref source, count, bytes);
            if (fits)
                Advance(length);
            else
                AdvanceSafe(length);
        }

        /// <summary>Copies an already reserved contiguous record without repeating capacity or completion checks.</summary>
        /// <param name="current">The complete reserved destination record.</param>
        /// <param name="source">The first byte of the complete source storage.</param>
        /// <param name="count">The logical count written in the header.</param>
        /// <param name="bytes">The checked payload width.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WritePayload(ref byte current, ref byte source, int count, int bytes)
        {
            LuminPackWriter.Write<int>(ref current, count);
            if (bytes != 0)
                LuminPackMarshal.CopyCollectionPayload(ref Unsafe.Add(ref current, sizeof(int)),
                    ref source, (uint)bytes);
        }

        /// <summary>Proves the complete record and following fixed-value headroom with one unsigned capacity comparison.</summary>
        /// <param name="length">The nonnegative complete record width.</param>
        /// <returns>Whether both existing dynamic boundary checks are already satisfied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FitsRecord(int length)
        {
            // The cached limit is min(storage, threshold - 1) from the base: one signed comparison proves both.
            return RecordDistance() >= (nint)(uint)length;
        }

        /// <summary>Writes a complete scalar storage value and advances the binary cursor.</summary>
        /// <typeparam name = "T">The unmanaged storage type.</typeparam>
        /// <param name = "value">The complete scalar storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(in T value)
            where T : unmanaged
        {
            LuminPackWriter.Write(ref Current, value);
            Advance(Unsafe.SizeOf<T>());
        }

        /// <summary>Forwards writable source storage to the single contiguous </summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "destination">The complete reserved block.</param>
        /// <param name = "value">The source storage.</param>
        /// <param name = "count">The selected count.</param>
        /// <param name = "header">Whether the count is included.</param>
        /// <param name = "compressed">Whether the payload is compressed.</param>
        /// <returns>The actual complete byte width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(scoped Span<byte> destination, scoped Span<T> value, int count, bool header, bool compressed) => Write<T>(destination, (ReadOnlySpan<T>)value, count, header, compressed);

        /// <summary>Forwards writable source storage and reports its actual complete width.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "destination">The complete reserved block.</param>
        /// <param name = "value">The source storage.</param>
        /// <param name = "count">The selected count.</param>
        /// <param name = "header">Whether the count is included.</param>
        /// <param name = "compressed">Whether the payload is compressed.</param>
        /// <param name = "bytes">Receives the actual complete byte width.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(scoped Span<byte> destination, scoped Span<T> value, int count, bool header, bool compressed, out int bytes) => bytes = Write<T>(destination, (ReadOnlySpan<T>)value, count, header, compressed);

        /// <summary>Writes one contiguous record into its complete reservation.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "destination">The complete reserved output block.</param>
        /// <param name = "value">The contiguous source storage.</param>
        /// <param name = "count">The selected element count, or minus one for null.</param>
        /// <param name = "header">Whether the record includes its count.</param>
        /// <param name = "compressed">Whether the payload uses LuminZ compression.</param>
        /// <returns>The actual complete byte width.</returns>
        /// <remarks>The source storage must not overlap the destination reservation. The compressed path writes the
        /// LuminZ frame directly into the reserved payload and backfills the frame length after compression.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(scoped Span<byte> destination, scoped ReadOnlySpan<T> value, int count, bool header, bool compressed)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(T));
            int prefix = header ? sizeof(int) : 0;
            if (header)
                Write(ref MemoryMarshal.GetReference(destination), count);
            if (count <= 0)
                return prefix;
            int bytes = checked(count * Unsafe.SizeOf<T>());
            ref byte source = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(value));
            if (!compressed)
            {
                LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(destination), (nint)(uint)(prefix)), ref source, (uint)bytes);
                return prefix + bytes;
            }

            return WriteCompressedStorage(destination, ref source, bytes, prefix);
        }

        /// <summary>Compresses the raw payload directly into its reserved record and reports the actual width.</summary>
        /// <param name="destination">The complete reserved output record, including its optional count.</param>
        /// <param name="source">The first byte of the complete raw payload; it must not overlap the destination record.</param>
        /// <param name="bytes">The checked raw payload width.</param>
        /// <param name="prefix">The width of the already written count header.</param>
        /// <returns>The complete compressed record width.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteCompressedStorage(scoped Span<byte> destination, scoped ref byte source, int bytes, int prefix)
        {
            int length = LuminCompressor.Compress(LuminPackMarshal.CreateReadOnlySpan(ref source, bytes),
                destination.Slice(prefix + sizeof(int)));
            LuminPackWriter.Write(ref Unsafe.Add(ref MemoryMarshal.GetReference(destination), (nint)(uint)(prefix)), length);
            return checked(prefix + sizeof(int) + length);
        }

        /// <summary>Writes a reserved contiguous record and reports its actual byte width.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "destination">The complete reserved output block.</param>
        /// <param name = "value">The contiguous source storage.</param>
        /// <param name = "count">The selected element count.</param>
        /// <param name = "header">Whether the record includes its count.</param>
        /// <param name = "compressed">Whether the payload uses LuminZ compression.</param>
        /// <param name = "bytes">Receives the actual complete byte width.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(scoped Span<byte> destination, scoped ReadOnlySpan<T> value, int count, bool header, bool compressed, out int bytes) => bytes = Write(destination, value, count, header, compressed);

        /// <summary>Processes a fixed storage batch without introducing a Release bounds check.</summary>
        /// <param name = "reference">The first byte of the complete fixed storage batch.</param>
        /// <typeparam name = "T">The reference-free storage type.</typeparam>
        /// <param name = "value1">The field participating in the memory operation.</param>
        /// <returns>The complete packed wire width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(ref byte reference, in T value1)
        {
            int width = Unsafe.SizeOf<T>();
            if (Unsafe.SizeOf<IntPtr>() == 8 || width <= 4)
                Unsafe.WriteUnaligned(ref reference, value1);
            else
                WriteWideStorage32(ref reference, in value1);

            return width;
        }

        /// <summary>Writes a wide scalar through packed storage on 32-bit targets.</summary>
        /// <typeparam name="T">The reference-free storage type.</typeparam>
        /// <param name="reference">The already reserved destination.</param>
        /// <param name="value">The complete source value.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteWideStorage32<T>(ref byte reference, in T value)
        {
            var view = UnmanagedViewModel.Create(value);
            LuminPackMarshal.CopyBlockUnaligned(ref reference, ref Unsafe.As<UnmanagedViewModel<T>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T>>());
        }
    }
}
