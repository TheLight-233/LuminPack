using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Numerics;
#endif

namespace LuminPack.Core
{
    /// <summary>
    /// JSON UTF-8 numeric fast paths selected by benchmark.
    /// Only contains paths that won the final decision benchmark for their target primitive.
    /// </summary>
    internal static class LuminPackJsonNumberFormatter
    {
        private sealed class Packed4Lut
        {
            internal readonly uint[] Packed;
            internal readonly byte[] Lengths;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WritePacked4(Span<byte> dest, Packed4Lut lut, int index)
        {
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(dest), lut.Packed[index]);
            return lut.Lengths[index];
        }

        // -----------------------------------------------------------------
        // byte / sbyte
        // -----------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteByte(Span<byte> dest, byte value)
            => WritePacked4(dest, s_lut256, value);

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteUShort(Span<byte> dest, ushort value)
        {
            if (value < 10_000)
                return WritePacked4(dest, s_lut10K, value);

            Utf8Formatter.TryFormat(value, dest, out int written);
            return written;
        }

        // Final benchmark winner for short: 1K LUT + 32-bit pair formatter fallback.
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
        // long
        // -----------------------------------------------------------------

        // Final benchmark winner for long: 10K packed LUT + quad formatter.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteLong(Span<byte> dest, long value)
        {
            if (value >= 0)
            {
                ulong u = (ulong)value;
                if (u < 10_000UL)
                    return WritePacked4(dest, s_lut10K, (int)u);

                return WriteUInt64Quad(dest, u);
            }

            ulong magnitude = 0UL - (ulong)value; // long.MinValue safe
            dest[0] = (byte)'-';

            if (magnitude < 10_000UL)
                return 1 + WritePacked4(dest[1..], s_lut10K, (int)magnitude);

            return 1 + WriteUInt64Quad(dest[1..], magnitude);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Write4Digits(Span<byte> dest, uint value)
        {
            uint q = value / 100u;
            uint r = value - q * 100u;
            int hp = (int)q << 1;
            int lp = (int)r << 1;
            dest[0] = s_digitPairs[hp];
            dest[1] = s_digitPairs[hp + 1];
            dest[2] = s_digitPairs[lp];
            dest[3] = s_digitPairs[lp + 1];
        }

        // -----------------------------------------------------------------
        // decimal
        // -----------------------------------------------------------------

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteDecimalUInt64Fixed(Span<byte> dest, ulong coefficient, int scale, bool negative)
        {
            Span<byte> digits = stackalloc byte[20];
            int count = WriteUInt64Quad(digits, coefficient);
            return WriteDecimalDigits(dest, digits[..count], scale, negative);
        }

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
