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
    public ref partial struct LuminPackReader
#else
    public unsafe ref partial struct LuminPackReader
#endif
    {
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadVarIntUInt32()
        {
            sbyte typeCode = this.Read<sbyte>();
            if (typeCode >= 0)
                return (uint)typeCode;
            return ReadExtendedVarIntUInt32(typeCode);
        }

        /// <summary>Decodes an extended tagged integer outside the common one-byte path.</summary>
        /// <param name="typeCode">The already consumed original type code.</param>
        /// <returns>The original checked integer value.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private uint ReadExtendedVarIntUInt32(sbyte typeCode)
        {
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadVarIntInt32()
        {
            sbyte typeCode = this.Read<sbyte>();
            if (typeCode >= 0)
                return (int)typeCode;
            return ReadExtendedVarIntInt32(typeCode);
        }

        /// <summary>Decodes an extended tagged integer outside the common one-byte path.</summary>
        /// <param name="typeCode">The already consumed original type code.</param>
        /// <returns>The original checked integer value.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadExtendedVarIntInt32(sbyte typeCode)
        {
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

        /// <summary>Matches a complete revision-2 prefix: header with the compiled profile, names, types and widths.</summary>
        /// <typeparam name = "TOptions">The compiled receiving profile.</typeparam>
        /// <param name = "prefix">The generated prefix with a zero profile byte.</param>
        /// <returns>Whether the complete matching prefix was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadVersionPrefix<TOptions>(scoped ReadOnlySpan<byte> prefix)
            where TOptions : struct, ILuminPackOptions
        {
            if ((uint)Position > (uint)_buffer.Length || prefix.Length > RemainingWindowBytes)
                return false;
            uint expected = BinaryPrimitives.ReadUInt32LittleEndian(prefix) | (uint)LuminPackWriter.BinaryProfile<TOptions>() << 16;
            if (LuminPackReader.Read<uint>(ref Current) != expected ||
                !System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref Current, 4), prefix.Length - 4).SequenceEqual(prefix.Slice(4)))
                return false;
            Advance(prefix.Length);
            return true;
        }

        /// <summary>Reads the four-byte payload length of a variable revision-2 member.</summary>
        /// <returns>The nonnegative length, validated against the remaining input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadVersionPayloadLength()
        {
            ref byte source = ref Current;
            Advance(sizeof(int));
            int length = Unsafe.ReadUnaligned<int>(ref source);
            // One unsigned comparison rejects a negative length and a payload past the input.
#if NET8_0_OR_GREATER
            if ((ulong)(uint)length > (ulong)Unsafe.ByteOffset(ref Cursor, ref _end))
#else
            if ((ulong)(uint)length > (ulong)unchecked((nint)_end - (nint)Cursor))
#endif
                ThrowVersionPayloadLength(length);
            return length;
        }

        /// <summary>Rejects a version-tolerant payload length that is negative or exceeds the input.</summary>
        /// <param name="length">The rejected length.</param>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        private static void ThrowVersionPayloadLength(int length)
            => LuminPackExceptionHelper.ThrowFormatException(length < 0 ? "Negative version-tolerant payload length." : "Version-tolerant payload exceeds the input.");

        /// <summary>Reads a Boolean array record, copying its storage in one pass that also validates every element.</summary>
        /// <param name="value">The reusable destination; an array of another length is replaced.</param>
        /// <returns>Whether every element stored zero or one. The destination holds the copied bytes either way.</returns>
        /// <remarks>The record is the ordinary contiguous array record: a four-byte count, minus one for null, then one byte per element.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadCanonicalBooleans(ref bool[]? value)
        {
            int count = Read<int>();
            if (count <= 0)
            {
                if (count == 0)
                    value = Array.Empty<bool>();
                else if (count == LuminPackCode.NullCollection)
                    value = null;
                else
                    LuminPackExceptionHelper.ThrowInvalidCollectionLength(count);
                return true;
            }
            EnsureReadable(count);
            if (value is null || value.Length != count)
                value = LuminPackMarshal.AllocateUninitializedArray<bool>(count);
            bool canonical = global::LuminPack.Security.LuminPackInputPolicy.CopyBooleans(ref Current,
                ref Unsafe.As<bool, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), count);
            Advance(count);
            return canonical;
        }

        /// <summary>Checks the recorded width of a raw reference-tracked slot that differs from its storage width.</summary>
        /// <param name="recorded">The slot table byte.</param>
        /// <param name="width">The raw storage width.</param>
        /// <remarks>A width of 255 bytes or more is recorded as 255 with the width in front of the value; any other entry
        /// disagrees with the slot's type.</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ValidateReferenceWidth(int recorded, int width)
        {
            if (recorded != 255 || width < 255 || ReadVersionPayloadLength() != width)
                LuminPackExceptionHelper.ThrowFormatException("A reference-tracked slot width disagrees with its member type.");
        }

        /// <summary>Reads the length in front of a variable reference-tracked slot.</summary>
        /// <param name="recorded">The slot table byte, which must be 255.</param>
        /// <returns>The validated payload length.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadReferencePayloadLength(int recorded)
        {
            if (recorded != 255)
                LuminPackExceptionHelper.ThrowFormatException("A reference-tracked slot width disagrees with its member type.");
            return ReadVersionPayloadLength();
        }

        /// <summary>Skips the payload of one reference-tracked slot that the receiving model does not know.</summary>
        /// <param name="recorded">The slot table byte: a raw width, zero for an empty slot, or 255 for a length-prefixed payload.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SkipReferenceSlot(int recorded) => Advance(recorded == 255 ? ReadVersionPayloadLength() : recorded);

        /// <summary>Skips the payloads of reference-tracked slots that the receiving model does not know.</summary>
        /// <param name="tablePosition">The absolute position of the first unknown slot byte in the record table.</param>
        /// <param name="slots">The number of unknown trailing slots.</param>
        /// <remarks>The table is read again from the input, so the generated read keeps no table storage.</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SkipReferenceSlots(int tablePosition, int slots)
        {
            for (int i = 0; i < slots; i++)
                SkipReferenceSlot(_buffer[tablePosition + i]);
        }

        /// <summary>Reads a named header of either revision and rejects unrelated record formats.</summary>
        /// <typeparam name = "TOptions">The compiled receiving profile.</typeparam>
        /// <param name = "revision">Receives the record revision: 1 stores all lengths before the payloads, 2 stores a width table.</param>
        /// <returns>The member count, or minus one for a null object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadVersionHeader<TOptions>(out int revision)
            where TOptions : struct, ILuminPackOptions
        {
            ValidateCollectionPayload(Position, 1);
            revision = 0;
            if (Current == 255)
            {
                Advance(1);
                return -1;
            }

            ValidateCollectionPayload(Position, 2);
            revision = Unsafe.Add(ref Current, 1);
            return ReadNonNullVersionHeader<TOptions>(true);
        }

        /// <summary>Maps a dynamic member table of either revision before reading any payload.</summary>
        /// <param name = "fields">The generated receiving schema.</param>
        /// <param name = "revision">The record revision returned by the header.</param>
        /// <param name = "slots">Receives local member indices, or minus one for unknown fields.</param>
        /// <param name = "widths">Receives revision-1 payload lengths, or revision-2 fixed widths and minus one for a length-prefixed member.</param>
        /// <param name = "present">Receives the set of local fields supplied by the input.</param>
        public void ReadVersionTable(LuminPackVersionField[] fields, int revision, scoped Span<int> slots, scoped Span<int> widths, scoped Span<byte> present)
        {
            if (revision == 1)
            {
                ReadVersionTable(fields, slots, widths, present);
                return;
            }
            if (revision == 3)
            {
                // A short record names a table that a full record of this operation already carried.
                EnsureReadable(sizeof(ulong));
                ulong fingerprint = Unsafe.ReadUnaligned<ulong>(ref Current);
                Advance(sizeof(ulong));
                Span<byte> stored = default;
                if (_options is null || !_options.TryGetVersionTable(fingerprint, out stored))
                    LuminPackExceptionHelper.ThrowFormatException("A short version-tolerant record names a member table this operation has not read.");
                var tableReader = new LuminPackReader(ref stored, _options);
                tableReader.ReadVersionWidths(fields, slots, widths, present);
                return;
            }
            int start = Position;
            ReadVersionWidths(fields, slots, widths, present);
            // Later short records of this operation map through the same table.
            OptionState.AddVersionTable(LuminPackVersionField.Fingerprint(_buffer.Slice(start, Position - start)), _buffer.Slice(start, Position - start));
        }

        /// <summary>Maps the names, type contracts and widths of a revision-2 member table at the cursor.</summary>
        /// <param name = "fields">The generated receiving schema.</param>
        /// <param name = "slots">Receives local member indices, or minus one for unknown fields.</param>
        /// <param name = "widths">Receives fixed widths, or minus one for a length-prefixed member.</param>
        /// <param name = "present">Receives the set of local fields supplied by the input.</param>
        private void ReadVersionWidths(LuminPackVersionField[] fields, scoped Span<int> slots, scoped Span<int> widths, scoped Span<byte> present)
        {
            ReadVersionNames(fields, slots, present);
            ValidateCollectionPayload(Position, widths.Length);
            for (int i = 0; i < widths.Length; i++)
            {
                byte width = Read<byte>();
                widths[i] = width == 255 ? -1 : width;
            }
        }

        /// <summary>Matches a short record of the receiving schema: its header and the receiver's own table fingerprint.</summary>
        /// <typeparam name = "TOptions">The compiled receiving profile.</typeparam>
        /// <param name = "count">The receiving member count.</param>
        /// <param name = "fingerprint">The receiving table fingerprint.</param>
        /// <returns>Whether the short record matched and was consumed.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadVersionShort<TOptions>(int count, ulong fingerprint) where TOptions : struct, ILuminPackOptions
        {
            ref byte input = ref PeekInput(12, out bool available);
            if (!available || Unsafe.ReadUnaligned<uint>(ref input) != (251u | 3u << 8 | (uint)LuminPackWriter.BinaryProfile<TOptions>() << 16 | (uint)count << 24) ||
                Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref input, 4)) != (uint)fingerprint || Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref input, 8)) != (uint)(fingerprint >> 32))
                return false;
            Advance(12);
            return true;
        }

        /// <summary>Maps a stack table of either revision without a by-reference receiver.</summary>
        /// <param name="reader">The input state whose cursor is copied for this metadata-only operation.</param>
        /// <param name="fields">The receiving schema.</param>
        /// <param name="revision">The record revision.</param>
        /// <param name="slots">The incoming member slots.</param>
        /// <param name="widths">The incoming lengths or widths.</param>
        /// <param name="present">A writable range with one byte per receiving field.</param>
        /// <returns>The validated metadata end to publish to the original reader.</returns>
        public static int ReadVersionTable(LuminPackReader reader, LuminPackVersionField[] fields, int revision, scoped Span<int> slots, scoped Span<int> widths, scoped Span<byte> present)
        {
            reader.ReadVersionTable(fields, revision, slots, widths, present);
            return reader.Position;
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

            return ReadNonNullVersionHeader<TOptions>(false);
        }

        /// <summary>Decodes an evolved non-null header outside the common matching-schema native body.</summary>
        /// <typeparam name = "TOptions">The compiled receiving profile.</typeparam>
        /// <param name = "widthTables">Whether the caller also accepts revision 2.</param>
        /// <returns>The validated member count.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadNonNullVersionHeader<TOptions>(bool widthTables)
            where TOptions : struct, ILuminPackOptions
        {
            byte marker = Read<byte>();
            if (marker != 251)
                LuminPackExceptionHelper.ThrowFormatException("Expected a named version-tolerant record.");
            ValidateCollectionPayload(Position, 3);
            byte revision = Read<byte>();
            byte profile = Read<byte>();
            int count = Read<byte>();
            if (revision != 1 && !(widthTables && revision is 2 or 3) || count > 249)
                LuminPackExceptionHelper.ThrowFormatException("Unsupported version-tolerant record header.");
            if (profile != LuminPackWriter.BinaryProfile<TOptions>())
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
            ReadVersionNames(fields, slots, present);
            ReadVersionLengths(lengths);
        }

        /// <summary>Maps the names and types of a dynamic member table.</summary>
        /// <param name = "fields">The generated receiving schema.</param>
        /// <param name = "slots">Receives local member indices, or minus one for unknown fields.</param>
        /// <param name = "present">Receives the set of local fields supplied by the input.</param>
        private void ReadVersionNames(LuminPackVersionField[] fields, scoped Span<int> slots, scoped Span<byte> present)
        {
            present.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                int slot;
                if (i < fields.Length && fields[i].Prefix.Length <= RemainingWindowBytes && _buffer.Slice(Position, fields[i].Prefix.Length).SequenceEqual(fields[i].Prefix))
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
            if ((uint)Position > (uint)_buffer.Length || metadata.Length > RemainingWindowBytes || !System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref Current, metadata.Length).SequenceEqual(metadata))
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
            int available = _buffer.Length - Position;
            if ((uint)Position > (uint)_buffer.Length || available < 4 || metadata.Length > available - 4)
                return false;
            LuminPackReader.Read(ref Current, out uint header);
            uint profile = LuminPackWriter.BinaryProfile<TOptions>();
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
                byte first = Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)(Position));
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
            byte first = Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)(Position));
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
            var result = _buffer.Slice(Position, (int)length);
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
            int previousLength = _buffer.Length;
            Buffer = _buffer.Slice(0, Position + length);
            return previousLength;
        }

        /// <summary>Restores the exact enclosing input boundary after a generated field operation.</summary>
        /// <param name = "previousLength">The length returned by the matching EnterVersionField call.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitVersionField(int previousLength)
        {
            Buffer = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)(0)), previousLength);
        }

        /// <summary>Checks a known field's completed read against its table length.</summary>
        /// <param name = "actual">The completed absolute cursor.</param>
        /// <param name = "expected">The expected absolute end.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateVersionFieldEnd(int actual, int expected)
        {
            if (actual != expected)
                ThrowVersionFieldEnd();
        }

        /// <summary>Rejects a member whose read ended elsewhere than its recorded length.</summary>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        private static void ThrowVersionFieldEnd() => LuminPackExceptionHelper.ThrowFormatException("Version-tolerant field length does not match its payload.");
    }
}
