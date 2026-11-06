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
#if NET8_0_OR_GREATER
    public ref partial struct LuminPackWriter
#else
    public unsafe ref partial struct LuminPackWriter
#endif
    {
        /// <summary>Writes a value using the smallest tagged variable-width integer record.</summary>
        /// <param name = "x">The value to encode.</param>
        public void WriteVarInt(byte x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(LuminPackWriter.Write(ref Current, (sbyte)x));
            }
            else
            {
                ref byte destination = ref Current;
                int length = 0;
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Byte);
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), x);
                Advance(length);
            }
        }

        /// <summary>Writes a value using the smallest tagged variable-width integer record.</summary>
        /// <param name = "x">The value to encode.</param>
        public void WriteVarInt(sbyte x)
        {
            if (VarIntCodes.MinSingleValue <= x)
            {
                Advance(LuminPackWriter.Write(ref Current, x));
            }
            else
            {
                ref byte destination = ref Current;
                int length = 0;
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.SByte);
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), x);
                Advance(length);
            }
        }

        /// <summary>Writes a value using the smallest tagged variable-width integer record.</summary>
        /// <param name = "x">The value to encode.</param>
        public void WriteVarInt(ushort x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(LuminPackWriter.Write(ref Current, (sbyte)x));
            }
            else
            {
                ref byte destination = ref Current;
                int length = 0;
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.UInt16);
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (ushort)x);
                Advance(length);
            }
        }

        /// <summary>Writes a value using the smallest tagged variable-width integer record.</summary>
        /// <param name = "x">The value to encode.</param>
        public void WriteVarInt(short x)
        {
            if (0 <= x)
            {
                if (x <= VarIntCodes.MaxSingleValue) // same as sbyte.MaxValue
                {
                    Advance(LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int16);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (short)x);
                    Advance(length);
                }
            }
            else
            {
                if (VarIntCodes.MinSingleValue <= x)
                {
                    Advance(LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else if (sbyte.MinValue <= x)
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.SByte);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (sbyte)x);
                    Advance(length);
                }
                else
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int16);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (short)x);
                    Advance(length);
                }
            }
        }

        /// <summary>Writes a value using the smallest tagged variable-width integer record.</summary>
        /// <param name = "x">The value to encode.</param>
        public void WriteVarInt(uint x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(LuminPackWriter.Write(ref Current, (sbyte)x));
            }
            else if (x <= ushort.MaxValue)
            {
                ref byte destination = ref Current;
                int length = 0;
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.UInt16);
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (ushort)x);
                Advance(length);
            }
            else
            {
                ref byte destination = ref Current;
                int length = 0;
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.UInt32);
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (uint)x);
                Advance(length);
            }
        }

        /// <summary>Writes a value using the smallest tagged variable-width integer record.</summary>
        /// <param name = "x">The value to encode.</param>
        public void WriteVarInt(int x)
        {
            if (0 <= x)
            {
                if (x <= VarIntCodes.MaxSingleValue) // same as sbyte.MaxValue
                {
                    Advance(LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else if (x <= short.MaxValue)
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int16);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (short)x);
                    Advance(length);
                }
                else
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int32);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (int)x);
                    Advance(length);
                }
            }
            else
            {
                if (VarIntCodes.MinSingleValue <= x)
                {
                    Advance(LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else if (sbyte.MinValue <= x)
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.SByte);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (sbyte)x);
                    Advance(length);
                }
                else if (short.MinValue <= x)
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int16);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (short)x);
                    Advance(length);
                }
                else
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int32);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (int)x);
                    Advance(length);
                }
            }
        }

        /// <summary>Writes a value using the smallest tagged variable-width integer record.</summary>
        /// <param name = "x">The value to encode.</param>
        public void WriteVarInt(ulong x)
        {
            if (x <= VarIntCodes.MaxSingleValue)
            {
                Advance(LuminPackWriter.Write(ref Current, (sbyte)x));
            }
            else if (x <= ushort.MaxValue)
            {
                ref byte destination = ref Current;
                int length = 0;
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.UInt16);
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (ushort)x);
                Advance(length);
            }
            else if (x <= uint.MaxValue)
            {
                ref byte destination = ref Current;
                int length = 0;
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.UInt32);
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (uint)x);
                Advance(length);
            }
            else
            {
                ref byte destination = ref Current;
                int length = 0;
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.UInt64);
                length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (ulong)x);
                Advance(length);
            }
        }

        /// <summary>Writes a value using the smallest tagged variable-width integer record.</summary>
        /// <param name = "x">The value to encode.</param>
        public void WriteVarInt(long x)
        {
            if (0 <= x)
            {
                if (x <= VarIntCodes.MaxSingleValue) // same as sbyte.MaxValue
                {
                    Advance(LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else if (x <= short.MaxValue)
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int16);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (short)x);
                    Advance(length);
                }
                else if (x <= int.MaxValue)
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int32);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (int)x);
                    Advance(length);
                }
                else
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int64);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (long)x);
                    Advance(length);
                }
            }
            else
            {
                if (VarIntCodes.MinSingleValue <= x)
                {
                    Advance(LuminPackWriter.Write(ref Current, (sbyte)x));
                }
                else if (sbyte.MinValue <= x)
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.SByte);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (sbyte)x);
                    Advance(length);
                }
                else if (short.MinValue <= x)
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int16);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (short)x);
                    Advance(length);
                }
                else if (int.MinValue <= x)
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int32);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (int)x);
                    Advance(length);
                }
                else
                {
                    ref byte destination = ref Current;
                    int length = 0;
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), VarIntCodes.Int64);
                    length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (long)x);
                    Advance(length);
                }
            }
        }

        /// <summary>Writes the named version-tolerant header with the default binary string profile.</summary>
        /// <param name = "count">The number of table entries, at most 249.</param>
        public void WriteVersionHeader(byte count) => WriteVersionHeader<LuminPackDefaultOptions>(count);

        /// <summary>Writes the record marker, revision, binary string profile and member count.</summary>
        /// <typeparam name = "TOptions">The compiled binary profile.</typeparam>
        /// <param name = "count">The number of table entries, at most 249.</param>
        public void WriteVersionHeader<TOptions>(byte count)
            where TOptions : struct, ILuminPackOptions
        {
            byte profile = BinaryProfile<TOptions>();
            EnsureAdditionalCapacity(4);
            ref byte destination = ref Current;
            int length = 0;
            length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (byte)251);
            length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), (byte)1);
            length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), profile);
            length += LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)length), count);
            Advance(length);
        }

        /// <summary>Writes a generated fixed prefix with the default binary profile.</summary>
        /// <param name="prefix">The complete UTF-8 length-profile header, metadata and fixed length table.</param>
        public void WriteVersionPrefix(scoped ReadOnlySpan<byte> prefix) => WriteVersionPrefix<LuminPackDefaultOptions>(prefix);

        /// <summary>Copies a generated fixed prefix and stamps the actual compiled profile.</summary>
        /// <typeparam name="TOptions">The selected stateless binary profile.</typeparam>
        /// <param name="prefix">The complete generated header, metadata and fixed length table.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVersionPrefix<TOptions>(scoped ReadOnlySpan<byte> prefix) where TOptions : struct, ILuminPackOptions
        {
            if (prefix.Length < 4)
                LuminPackExceptionHelper.ThrowArgumentException("A version prefix must contain a complete header.", nameof(prefix));
            EnsureAdditionalCapacity(prefix.Length);
            // The header with the stamped profile is one store, and the rest is copied in disjoint word stores. A
            // vectorized block copy writes overlapping halves, and a record's next wide load over those stores (for
            // example the allocating facade's copy) stalls on store forwarding for about a hundred nanoseconds.
            uint header = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.Slice(Position, 4), (header & 0xFF00FFFFu) | (uint)BinaryProfile<TOptions>() << 16);
            CopyDisjoint(ref Unsafe.Add(ref Current, 4), ref Unsafe.Add(ref MemoryMarshal.GetReference(prefix), 4), prefix.Length - 4);
            Advance(prefix.Length);
        }

        /// <summary>Returns this operation's schema identity, assigning a new nonzero one on first use.</summary>
        /// <returns>The identity version schemas compare with their last written operation on this thread.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSchemaOperation() => SchemaOperation != 0 ? SchemaOperation : SchemaOperation = NextSchemaOperation();

        /// <summary>Draws a new nonzero operation identity on this thread.</summary>
        /// <returns>The identity.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int NextSchemaOperation()
        {
            int next = unchecked(++t_schemaOperations);
            return next != 0 ? next : ++t_schemaOperations;
        }

        /// <summary>Stores a short version header: the member count and the fingerprint of a table written earlier in this operation.</summary>
        /// <typeparam name="TOptions">The selected stateless binary profile.</typeparam>
        /// <param name="target">The first reserved byte.</param>
        /// <param name="count">The member count.</param>
        /// <param name="fingerprint">The table fingerprint.</param>
        /// <returns>The byte after the twelve-byte header.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte StoreVersionShort<TOptions>(ref byte target, byte count, ulong fingerprint) where TOptions : struct, ILuminPackOptions
        {
            Unsafe.WriteUnaligned(ref target, 251u | 3u << 8 | (uint)BinaryProfile<TOptions>() << 16 | (uint)count << 24);
            // 32-bit targets keep typed unaligned access within four bytes.
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref target, 4), (uint)fingerprint);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref target, 8), (uint)(fingerprint >> 32));
            return ref Unsafe.Add(ref target, 12);
        }

        /// <summary>Stores a cached version prefix into reserved room and stamps the compiled profile.</summary>
        /// <typeparam name="TOptions">The selected stateless binary profile.</typeparam>
        /// <param name="target">The first reserved byte.</param>
        /// <param name="prefix">The complete cached prefix.</param>
        /// <returns>The byte after the stored prefix.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte StoreVersionPrefix<TOptions>(ref byte target, scoped ReadOnlySpan<byte> prefix) where TOptions : struct, ILuminPackOptions
        {
            if (prefix.Length < 4)
                LuminPackExceptionHelper.ThrowArgumentException("A version prefix must contain a complete header.", nameof(prefix));
            uint header = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
            Unsafe.WriteUnaligned(ref target, (header & 0xFF00FFFFu) | (uint)BinaryProfile<TOptions>() << 16);
            CopyDisjoint(ref Unsafe.Add(ref target, 4), ref Unsafe.Add(ref MemoryMarshal.GetReference(prefix), 4), prefix.Length - 4);
            return ref Unsafe.Add(ref target, prefix.Length);
        }

        /// <summary>Reserves the bytes of a generated constant prefix and returns their first byte.</summary>
        /// <param name="length">The prefix length.</param>
        /// <returns>The first reserved byte; the caller stores exactly <paramref name="length"/> bytes there.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte ReservePrefix(int length)
        {
            EnsureAdditionalCapacity(length);
            ref byte target = ref Current;
            Advance(length);
            return ref target;
        }

        /// <summary>Reserves the four-byte payload length of a variable revision-2 version member.</summary>
        /// <returns>The absolute length position, stable across owner growth.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReserveVersionLength()
        {
            int position = Position;
            EnsureAdditionalCapacity(sizeof(int));
            Advance(sizeof(int));
            return position;
        }

        /// <summary>Patches a reserved member length after its payload has been written.</summary>
        /// <param name="position">The position returned by <see cref="ReserveVersionLength"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompleteVersionLength(int position)
            => BinaryPrimitives.WriteInt32LittleEndian(_buffer.Slice(position, sizeof(int)), Position - position - sizeof(int));

        /// <summary>Writes the payload length of a member whose width is known before it is written.</summary>
        /// <param name="length">The exact payload length.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVersionLength(int length)
        {
            EnsureAdditionalCapacity(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.Slice(Position, sizeof(int)), length);
            Advance(sizeof(int));
        }

        /// <summary>Copies a small cached block with disjoint stores of at most one machine word.</summary>
        /// <param name="target">The reserved destination.</param>
        /// <param name="source">The cached source.</param>
        /// <param name="length">The byte count.</param>
        /// <remarks>32-bit targets use four-byte values, the widest typed unaligned access they permit.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyDisjoint(ref byte target, ref byte source, int length)
        {
            int index = 0;
            if (Unsafe.SizeOf<IntPtr>() == 8)
                for (; index + sizeof(ulong) <= length; index += sizeof(ulong))
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref target, index), Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, index)));
            for (; index + sizeof(uint) <= length; index += sizeof(uint))
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref target, index), Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, index)));
            for (; index < length; index++)
                Unsafe.Add(ref target, index) = Unsafe.Add(ref source, index);
        }

        /// <summary>Copies the complete cached name and type table at its dynamic framing boundary.</summary>
        /// <param name = "metadata">The generated immutable metadata table.</param>
        /// <param name = "count">The number of payload lengths that follow the metadata.</param>
        public void WriteVersionMetadata(scoped ReadOnlySpan<byte> metadata, int count)
        {
            EnsureAdditionalCapacity(checked(metadata.Length + count * 5));
            CopyDisjoint(ref Current, ref MemoryMarshal.GetReference(metadata), metadata.Length);
            Advance(metadata.Length);
        }
    }
}
