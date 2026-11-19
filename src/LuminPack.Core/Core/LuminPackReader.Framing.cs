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

        /// <summary>Reads the four-byte payload length of a length-prefixed member.</summary>
        /// <returns>The nonnegative length, validated against the remaining input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadVersionPayloadLength() => ReadVersionPayloadLength(out _);

        /// <summary>Reads a field length and proves its complete range against the active input window.</summary>
        /// <param name="end">Receives the enclosing-buffer position directly after the validated payload.</param>
        /// <returns>The nonnegative field length.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadVersionPayloadLength(out int end)
        {
            ref byte source = ref Current;
            int position = Position;
            EnsureReadable(position, sizeof(int));
            int length = Unsafe.ReadUnaligned<int>(ref source);
            if ((uint)length > (uint)(_buffer.Length - position - sizeof(int)))
                ThrowVersionPayloadLength(length);
            end = position + sizeof(int) + length;
            AdvanceValidated(sizeof(int));
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
