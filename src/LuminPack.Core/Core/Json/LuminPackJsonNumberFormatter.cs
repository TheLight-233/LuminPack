using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Numerics;
#endif

namespace LuminPack.Core
{
    /// <summary>
    /// JSON UTF-8 numeric fast paths
    /// </summary>
    internal static class LuminPackJsonNumberFormatter
    {
        /// <summary>Provides packed4 lut functionality.</summary>
        private sealed class Packed4Lut
        {
            internal readonly uint[] Packed;
            internal readonly byte[] Lengths;

            /// <summary>Initializes a new instance of <c>Packed4Lut</c>.</summary>
            /// <param name="count">The number of entries involved in the operation.</param>
            internal Packed4Lut(int count)
            {
                Packed = new uint[count];
                Lengths = new byte[count];
                Span<byte> tmp = stackalloc byte[16];

                for (int i = 0; i < count; i++)
                {
                    Utf8Formatter.TryFormat(i, tmp, out int n);
                    uint packed = 0;
                    for (int j = 0; j < n; j++)
                        packed |= (uint)tmp[j] << (j << 3);

                    Packed[i] = packed;
                    Lengths[i] = (byte)n;
                }
            }
        }

        // Dedicated working sets used by the winning candidates.
        private static readonly Packed4Lut s_lut129 = new(129);
        private static readonly Packed4Lut s_lut256 = new(256);
        private static readonly Packed4Lut s_lut1K = new(1024);
        private static readonly Packed4Lut s_lut10K = new(10_000);
        private static readonly byte[] s_digitPairs = BuildDigitPairs();

        /// <summary>Performs the build digit pairs step for the current operation.</summary>
        /// <returns>The result of the operation.</returns>
        private static byte[] BuildDigitPairs()
        {
            var pairs = new byte[200];
            for (int i = 0; i < 100; i++)
            {
                pairs[i << 1] = (byte)('0' + i / 10);
                pairs[(i << 1) + 1] = (byte)('0' + i % 10);
            }
            return pairs;
        }

        /// <summary>Writes packed4 to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="lut">The lut used by this operation.</param>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WritePacked4(Span<byte> dest, Packed4Lut lut, int index)
        {
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(dest), lut.Packed[index]);
            return lut.Lengths[index];
        }

        // -----------------------------------------------------------------
        // byte / sbyte
        // -----------------------------------------------------------------

        /// <summary>Writes byte to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteByte(Span<byte> dest, byte value)
            => WritePacked4(dest, s_lut256, value);

        /// <summary>Writes s byte to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteSByte(Span<byte> dest, sbyte value)
        {
            if (value >= 0)
                return WritePacked4(dest, s_lut129, value);

            dest[0] = (byte)'-';
            int magnitude = -value;
            return 1 + WritePacked4(dest[1..], s_lut129, magnitude);
        }

        // -----------------------------------------------------------------
        // short / ushort
        // -----------------------------------------------------------------

        // Final benchmark winner for ushort: 0..9999 LUT, DotNet fallback.
        /// <summary>Writes u short to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteUShort(Span<byte> dest, ushort value)
        {
            if (value < 10_000)
                return WritePacked4(dest, s_lut10K, value);

            Utf8Formatter.TryFormat(value, dest, out int written);
            return written;
        }

        // Final benchmark winner for short: 1K LUT + 32-bit pair formatter fallback.
        /// <summary>Writes short to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteShort(Span<byte> dest, short value)
        {
            bool negative = value < 0;
            uint magnitude = negative ? (uint)(-(int)value) : (uint)value;

            if (magnitude < 1024u)
            {
                int pos = 0;
                if (negative)
                    dest[pos++] = (byte)'-';

                pos += WritePacked4(dest[pos..], s_lut1K, (int)magnitude);
                return pos;
            }

            int offset = 0;
            if (negative)
                dest[offset++] = (byte)'-';

            return offset + WriteUInt32PairLinear(dest[offset..], magnitude);
        }

        /// <summary>Writes u int32 pair linear to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteUInt32PairLinear(Span<byte> dest, uint value)
        {
            if (value < 10u)
            {
                dest[0] = (byte)('0' + value);
                return 1;
            }

            int digits =
                value < 100u ? 2 :
                value < 1_000u ? 3 :
                value < 10_000u ? 4 :
                value < 100_000u ? 5 :
                value < 1_000_000u ? 6 :
                value < 10_000_000u ? 7 :
                value < 100_000_000u ? 8 :
                value < 1_000_000_000u ? 9 : 10;

            // The complete ten-byte destination also covers the head table's four-byte store.
            // Short spans retain the pair loop and its original checked-write failure prefix.
            if (value >= 10000u && dest.Length >= 10 && BitConverter.IsLittleEndian)
            {
                uint high = value / 10000u, low = value - high * 10000u;
                int head;
                if (high < 10000u)
                {
                    head = WritePacked4(dest, s_lut10K, (int)high);
                }
                else
                {
                    uint top = high / 10000u;
                    head = WritePacked4(dest, s_lut10K, (int)top);
                    Write4Digits(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(dest), (nint)(uint)head), 4), high - top * 10000u);
                    head += 4;
                }
                Write4Digits(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(dest), (nint)(uint)head), 4), low);
                return head + 4;
            }

            int pos = digits;
            while (value >= 100u)
            {
                uint q = value / 100u;
                uint r = value - q * 100u;
                int pair = (int)r << 1;
                dest[--pos] = s_digitPairs[pair + 1];
                dest[--pos] = s_digitPairs[pair];
                value = q;
            }

            if (value < 10u)
            {
                dest[--pos] = (byte)('0' + value);
            }
            else
            {
                int pair = (int)value << 1;
                dest[--pos] = s_digitPairs[pair + 1];
                dest[--pos] = s_digitPairs[pair];
            }

            return digits;
        }

        // -----------------------------------------------------------------
        // int / uint
        // -----------------------------------------------------------------

        // 10K packed LUT for small values (packed store intentionally writes 4 bytes),
        // digit-pair linear formatter for the rest.
        /// <summary>Writes u int32 to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteUInt32(Span<byte> dest, uint value)
        {
            if (value < 10_000u)
                return WritePacked4(dest, s_lut10K, (int)value);

            return WriteUInt32PairLinear(dest, value);
        }

        /// <summary>Writes int32 to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteInt32(Span<byte> dest, int value)
        {
            if (value >= 0)
            {
                uint u = (uint)value;
                if (u < 10_000u)
                    return WritePacked4(dest, s_lut10K, (int)u);

                return WriteUInt32PairLinear(dest, u);
            }

            uint magnitude = 0u - (uint)value; // int.MinValue safe
            dest[0] = (byte)'-';

            if (magnitude < 10_000u)
                return 1 + WritePacked4(dest[1..], s_lut10K, (int)magnitude);

            return 1 + WriteUInt32PairLinear(dest[1..], magnitude);
        }

        // -----------------------------------------------------------------
        // long / ulong
        // -----------------------------------------------------------------

        // Final benchmark winner for long: 10K packed LUT + quad formatter.
        /// <summary>Writes long to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteLong(Span<byte> dest, long value)
        {
            if (value >= 0)
                return WriteULong(dest, (ulong)value);

            ulong magnitude = 0UL - (ulong)value; // long.MinValue safe
            dest[0] = (byte)'-';

            if (magnitude < 10_000UL)
                return 1 + WritePacked4(dest[1..], s_lut10K, (int)magnitude);

            return 1 + WriteUInt64Quad(dest[1..], magnitude);
        }

        // Same routing as long: 10K packed LUT for small values (packed store
        // intentionally writes 4 bytes), quad formatter above.
        /// <summary>Writes u long to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteULong(Span<byte> dest, ulong value)
        {
            if (value < 10_000UL)
                return WritePacked4(dest, s_lut10K, (int)value);

            return WriteUInt64Quad(dest, value);
        }

        private static readonly ulong[] s_pow10U64 =
        {
            1UL,
            10UL,
            100UL,
            1_000UL,
            10_000UL,
            100_000UL,
            1_000_000UL,
            10_000_000UL,
            100_000_000UL,
            1_000_000_000UL,
            10_000_000_000UL,
            100_000_000_000UL,
            1_000_000_000_000UL,
            10_000_000_000_000UL,
            100_000_000_000_000UL,
            1_000_000_000_000_000UL,
            10_000_000_000_000_000UL,
            100_000_000_000_000_000UL,
            1_000_000_000_000_000_000UL,
            10_000_000_000_000_000_000UL
        };

        /// <summary>Performs the count digits step for the current operation.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountDigits(ulong value)
        {
#if NET5_0_OR_GREATER
            if (value == 0)
                return 1;

            int log2 = BitOperations.Log2(value);
            int estimate = ((log2 * 1233) >> 12) + 1;
            if (estimate < 20 && value >= s_pow10U64[estimate])
                estimate++;
            return estimate;
#else
            if (value < 10_000_000_000UL)
            {
                if (value < 100_000UL)
                {
                    if (value < 1_000UL)
                    {
                        if (value < 100UL) return value < 10UL ? 1 : 2;
                        return 3;
                    }
                    return value < 10_000UL ? 4 : 5;
                }

                if (value < 10_000_000UL)
                    return value < 1_000_000UL ? 6 : 7;
                return value < 1_000_000_000UL ? (value < 100_000_000UL ? 8 : 9) : 10;
            }

            if (value < 1_000_000_000_000_000UL)
            {
                if (value < 1_000_000_000_000UL)
                    return value < 100_000_000_000UL ? 11 : 12;
                return value < 100_000_000_000_000UL ?
                    (value < 10_000_000_000_000UL ? 13 : 14) : 15;
            }

            if (value < 100_000_000_000_000_000UL)
                return value < 10_000_000_000_000_000UL ? 16 : 17;
            if (value < 10_000_000_000_000_000_000UL)
                return value < 1_000_000_000_000_000_000UL ? 18 : 19;
            return 20;
#endif
        }

        /// <summary>Writes u int64 quad to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteUInt64Quad(Span<byte> dest, ulong value)
        {
            if (value < 10UL)
            {
                dest[0] = (byte)('0' + value);
                return 1;
            }

            int digits = CountDigits(value);
            int pos = digits;

            while (value >= 10_000UL)
            {
                ulong q = value / 10_000UL;
                uint r = (uint)(value - q * 10_000UL);
                pos -= 4;
                Write4Digits(dest[pos..], r);
                value = q;
            }

            uint head = (uint)value;
            if (head >= 1000u)
            {
                Write4Digits(dest, head);
            }
            else if (head >= 100u)
            {
                uint q = head / 100u;
                uint r = head - q * 100u;
                if (q >= 10u)
                {
                    int qp = (int)q << 1;
                    dest[0] = s_digitPairs[qp];
                    dest[1] = s_digitPairs[qp + 1];
                    int rp = (int)r << 1;
                    dest[2] = s_digitPairs[rp];
                    dest[3] = s_digitPairs[rp + 1];
                }
                else
                {
                    dest[0] = (byte)('0' + q);
                    int rp = (int)r << 1;
                    dest[1] = s_digitPairs[rp];
                    dest[2] = s_digitPairs[rp + 1];
                }
            }
            else if (head >= 10u)
            {
                int hp = (int)head << 1;
                dest[0] = s_digitPairs[hp];
                dest[1] = s_digitPairs[hp + 1];
            }
            else
            {
                dest[0] = (byte)('0' + head);
            }

            return digits;
        }

        /// <summary>Writes 4 digits to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Write4Digits(Span<byte> dest, uint value)
        {
            // Every caller writes an entire four-digit chunk, including leading zeroes.
            if (dest.Length < sizeof(uint))
            {
                uint q = value / 100u;
                uint r = value - q * 100u;
                int hp = (int)q << 1;
                int lp = (int)r << 1;
                dest[0] = s_digitPairs[hp];
                dest[1] = s_digitPairs[hp + 1];
                dest[2] = s_digitPairs[lp];
                dest[3] = s_digitPairs[lp + 1];
                return;
            }
            uint packed = QuadDigits.Values[value];
            if (!BitConverter.IsLittleEndian)
                packed = global::System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(packed);
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(dest), packed);
        }

        /// <summary>Gets a complete zero-padded ASCII quad in explicit little-endian numeric order.</summary>
        /// <param name="value">The validated digit group below 10000.</param>
        /// <returns>The four encoded ASCII digits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint GetFourDigits(uint value) => QuadDigits.Values[value];

        /// <summary>Initializes the four-digit working set only when a quad-format operation needs it.</summary>
        private static class QuadDigits
        {
            internal static readonly uint[] Values = Build();

            /// <summary>Builds zero-padded ASCII quads in explicit little-endian byte order.</summary>
            /// <returns>The immutable 40000-byte digit payload.</returns>
            private static uint[] Build()
            {
                var values = new uint[10000];
                for (uint value = 0; value < values.Length; value++)
                {
                    uint high = value / 100, low = value - high * 100;
                    values[value] = (uint)s_digitPairs[high * 2] | ((uint)s_digitPairs[high * 2 + 1] << 8) |
                        ((uint)s_digitPairs[low * 2] << 16) | ((uint)s_digitPairs[low * 2 + 1] << 24);
                }
                return values;
            }
        }

        // -----------------------------------------------------------------
        // decimal
        // -----------------------------------------------------------------

        /// <summary>Writes decimal to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteDecimal(Span<byte> dest, decimal value)
        {
#if NET5_0_OR_GREATER
            Span<int> bits = stackalloc int[4];
            decimal.GetBits(value, bits);

            uint lo = (uint)bits[0];
            uint mid = (uint)bits[1];
            uint hi = (uint)bits[2];
            uint flags = (uint)bits[3];
            int scale = (int)((flags >> 16) & 0x7Fu);
            bool negative = (flags & 0x8000_0000u) != 0 && (hi | mid | lo) != 0;

            // Winning combined path: 64-bit coefficient with small scale first,
            // otherwise full 96-bit custom conversion. No DotNet fallback.
            if (hi == 0 && scale <= 4)
            {
                ulong coefficient = ((ulong)mid << 32) | lo;
                return WriteDecimalUInt64Fixed(dest, coefficient, scale, negative);
            }

            Span<byte> digits = stackalloc byte[29];
            int digitCount = WriteUInt96Digits(digits, hi, mid, lo);
            return WriteDecimalDigits(dest, digits[..digitCount], scale, negative);
#else
            // Unity / older target compatibility: keep allocation-free official path.
            Utf8Formatter.TryFormat(value, dest, out int written);
            return written;
#endif
        }

#if NET5_0_OR_GREATER
        /// <summary>Writes decimal u int64 fixed to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="coefficient">The coefficient used by this operation.</param>
        /// <param name="scale">The scale used by this operation.</param>
        /// <param name="negative">The negative used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteDecimalUInt64Fixed(Span<byte> dest, ulong coefficient, int scale, bool negative)
        {
            Span<byte> digits = stackalloc byte[20];
            int count = WriteUInt64Quad(digits, coefficient);
            return WriteDecimalDigits(dest, digits[..count], scale, negative);
        }

        /// <summary>Writes decimal digits to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="digits">The digits used by this operation.</param>
        /// <param name="scale">The scale used by this operation.</param>
        /// <param name="negative">The negative used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteDecimalDigits(Span<byte> dest, ReadOnlySpan<byte> digits, int scale, bool negative)
        {
            int pos = 0;
            if (negative)
                dest[pos++] = (byte)'-';

            if (scale == 0)
            {
                digits.CopyTo(dest[pos..]);
                return pos + digits.Length;
            }

            if (digits.Length > scale)
            {
                int whole = digits.Length - scale;
                digits[..whole].CopyTo(dest[pos..]);
                pos += whole;
                dest[pos++] = (byte)'.';
                digits[whole..].CopyTo(dest[pos..]);
                return pos + scale;
            }

            dest[pos++] = (byte)'0';
            dest[pos++] = (byte)'.';
            int zeros = scale - digits.Length;
            dest.Slice(pos, zeros).Fill((byte)'0');
            pos += zeros;
            digits.CopyTo(dest[pos..]);
            return pos + digits.Length;
        }

        /// <summary>Writes u int96 digits to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="hi">The hi used by this operation.</param>
        /// <param name="mid">The mid used by this operation.</param>
        /// <param name="lo">The lo used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        private static int WriteUInt96Digits(Span<byte> dest, uint hi, uint mid, uint lo)
        {
            if ((hi | mid) == 0)
                return WriteUInt64Quad(dest, lo);

            if (hi == 0)
                return WriteUInt64Quad(dest, ((ulong)mid << 32) | lo);

            Span<uint> chunks = stackalloc uint[4];
            int count = 0;
            while ((hi | mid | lo) != 0)
                chunks[count++] = Div96By1Billion(ref hi, ref mid, ref lo);

            int pos = WriteUInt64Quad(dest, chunks[count - 1]);
            for (int i = count - 2; i >= 0; i--)
            {
                WriteNineDigits(dest[pos..], chunks[i]);
                pos += 9;
            }
            return pos;
        }

        /// <summary>Performs the div96 by1 billion step for the current operation.</summary>
        /// <param name="hi">The hi used by this operation.</param>
        /// <param name="mid">The mid used by this operation.</param>
        /// <param name="lo">The lo used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Div96By1Billion(ref uint hi, ref uint mid, ref uint lo)
        {
            const ulong Divisor = 1_000_000_000UL;

            ulong n = hi;
            uint qHi = (uint)(n / Divisor);
            ulong rem = n - (ulong)qHi * Divisor;

            n = (rem << 32) | mid;
            uint qMid = (uint)(n / Divisor);
            rem = n - (ulong)qMid * Divisor;

            n = (rem << 32) | lo;
            uint qLo = (uint)(n / Divisor);
            rem = n - (ulong)qLo * Divisor;

            hi = qHi;
            mid = qMid;
            lo = qLo;
            return (uint)rem;
        }

        /// <summary>Writes nine digits to the current output.</summary>
        /// <param name="dest">The destination receiving the result.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteNineDigits(Span<byte> dest, uint value)
        {
            for (int pos = 9; pos > 1; pos -= 2)
            {
                uint q = value / 100;
                uint r = value - q * 100;
                int p = (int)r << 1;
                dest[pos - 2] = s_digitPairs[p];
                dest[pos - 1] = s_digitPairs[p + 1];
                value = q;
            }
            dest[0] = (byte)('0' + value);
        }
#endif
    }
}
