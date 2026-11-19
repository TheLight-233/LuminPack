using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Option;

namespace LuminPack.Core
{
    public ref partial struct LuminPackReader
    {
        /// <summary>Decodes the revision-1 name and length tables before any field is published.</summary>
        /// <typeparam name="TOptions">The compiled binary string profile.</typeparam>
        /// <param name="fields">The exact legacy names and type contracts of the receiving model.</param>
        /// <returns>The incoming slot map and exact field lengths.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public LuminPackDescriptorMap ReadLegacyVersionRecord<TOptions>(LuminPackVersionField[] fields)
            where TOptions : struct, ILuminPackStringCodec
        {
            int start = Position;
            EnsureReadable(start, 4);
            var header = _buffer.Slice(start, 4);
            int count = header[3];
            if (header[0] != LuminPackVersionRecord.Marker || header[1] != 1 || count > LuminPackVersionRecord.MaximumFields)
                ThrowVersionHeader();
            if (header[2] != default(TOptions).BinaryProfile)
                LuminPackExceptionHelper.ThrowFormatException("The version-tolerant binary string profile does not match the input.");
            AdvanceValidated(4);

            var slots = new byte[count];
            var lengths = new int[count];
            var present = new VersionPresence();
            for (int i = 0; i < count; i++)
            {
                var name = ReadVersionMetadata();
                var contract = ReadVersionMetadata();
                int slot = LuminPackVersionField.Find(fields, name, i);
                if (slot >= 0)
                {
                    if (!contract.SequenceEqual(fields[slot].Contract))
                        LuminPackExceptionHelper.ThrowFormatException("The version-tolerant member type or binary profile has changed.");
                    if (!present.Add(slot))
                        LuminPackExceptionHelper.ThrowFormatException("Duplicate version-tolerant member identity.");
                }
                slots[i] = (byte)slot;
            }
            ReadVersionLengths(lengths);
            return new LuminPackDescriptorMap(slots, lengths, present);
        }

        /// <summary>Reads a complete payload-length table and validates its combined input boundary.</summary>
        /// <param name = "lengths">Receives lengths in wire order.</param>
        private void ReadVersionLengths(scoped Span<int> lengths)
        {
            // Every length has at least one byte. Extended records revalidate the remaining table.
            ValidateCurrentCollectionPayload(ref Current, lengths.Length);
            long total = 0;
            for (int i = 0; i < lengths.Length; i++)
            {
                byte first = Current;
                int length;
                if (first <= 127)
                {
                    AdvanceValidated(1);
                    length = first;
                }
                else
                {
                    length = ReadVersionExtendedLength();
                    ValidateCurrentCollectionPayload(ref Current, lengths.Length - i - 1);
                }

                lengths[i] = length;
                total += length;
            }

            if (total > int.MaxValue)
                LuminPackExceptionHelper.ThrowFormatException("Version-tolerant payload is too large.");
            ValidateCollectionPayload(Position, (int)total);
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
                ref byte prefix = ref Current;
                ValidateCurrentCollectionPayload(ref prefix, 1);
                byte next = prefix;
                AdvanceValidated(1);
                if (shift == 28 && next > 7)
                    LuminPackExceptionHelper.ThrowFormatException("Invalid version-tolerant metadata length.");
                length |= (uint)(next & 127) << shift;
                if (next < 128)
                    break;
            }

            ref byte metadata = ref Current;
            ValidateCurrentCollectionPayload(ref metadata, (int)length);
            var result = MemoryMarshal.CreateReadOnlySpan(ref metadata, (int)length);
            AdvanceValidated((int)length);
            return result;
        }

    }
}
