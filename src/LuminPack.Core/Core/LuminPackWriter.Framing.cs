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

        /// <summary>Reserves the four-byte payload length of a length-prefixed member.</summary>
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

    }
}
