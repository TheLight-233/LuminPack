using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Core
{
    /// <summary>
    /// JSON number emitter for <see cref="XjbFloat32"/> / <see cref="XjbFloat64"/> digits.
    ///
    /// Format policy (explicit, kept byte compatible with the previous implementation):
    /// <list type="bullet">
    /// <item><description>fixed notation inside the historical fast-path exponent window while the
    /// decimal needs at most 9 (float) / 17 (double) fraction digits, because that is exactly what the
    /// old digit-by-digit loop emitted;</description></item>
    /// <item><description>otherwise the .NET <c>"G"</c> layout: fixed when the leading decimal exponent is in
    /// [-4, 8] for float and [-4, 16] for double, scientific with an upper-case <c>E</c>, an explicit sign
    /// and at least two exponent digits everywhere else.</description></item>
    /// </list>
    /// Integral values never grow a ".0" and negative zero keeps its sign, matching both the old writer
    /// and the previous fallback (<c>Utf8Formatter</c> with <c>"G"</c>).
    /// </summary>
    internal static class LuminPackJsonFloatFormatter
    {
        private const int FloatFractionCap = 9;
        private const int DoubleFractionCap = 17;
        private const uint FloatLegacyLowExponent = 95;    // 127 - 32
        private const ulong DoubleLegacyLowExponent = 991; // 1023 - 32

        private static ReadOnlySpan<byte> DigitPairs =>
            "00010203040506070809101112131415161718192021222324252627282930313233343536373839404142434445464748495051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899"u8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountDecimalDigits(ulong value)
        {
            if (value < 10_000_000_000UL)
            {
                if (value < 100_000UL)
                {
                    if (value < 100UL)
                        return value < 10UL ? 1 : 2;

                    if (value < 1_000UL) return 3;
                    if (value < 10_000UL) return 4;
                    return 5;
                }

                if (value < 10_000_000UL)
                    return value < 1_000_000UL ? 6 : 7;

                if (value < 100_000_000UL) return 8;
                if (value < 1_000_000_000UL) return 9;
                return 10;
            }

            if (value < 1_000_000_000_000_000UL)
            {
                if (value < 1_000_000_000_000UL)
                    return value < 100_000_000_000UL ? 11 : 12;

                if (value < 10_000_000_000_000UL) return 13;
                if (value < 100_000_000_000_000UL) return 14;
                return 15;
            }

            if (value < 100_000_000_000_000_000UL)
                return value < 10_000_000_000_000_000UL ? 16 : 17;

            if (value < 10_000_000_000_000_000_000UL)
                return value < 1_000_000_000_000_000_000UL ? 18 : 19;

            return 20;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteDigitsUtf8(
            Span<byte> dest, ulong value)
        {
            return WriteDigitsUtf8(
                dest, value, CountDecimalDigits(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteDigitsUtf8(
            Span<byte> dest, ulong value, int digits)
        {
            if (dest.Length < digits)
                throw new ArgumentException(
                    "Destination too small", nameof(dest));

            ref byte output = ref MemoryMarshal.GetReference(dest);

            ReadOnlySpan<byte> pairs = DigitPairs;
            ref byte lut = ref MemoryMarshal.GetReference(pairs);

            int pos = digits;

            while (value >= 100UL)
            {
                ulong q = value / 100UL;
                uint r = (uint)(value - q * 100UL);

                pos -= 2;

                ushort pair = Unsafe.ReadUnaligned<ushort>(
                    ref Unsafe.Add(ref lut, (int)r << 1));

                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref output, pos), pair);

                value = q;
            }

            if (value < 10UL)
            {
                output = (byte)('0' + value);
            }
            else
            {
                ushort pair = Unsafe.ReadUnaligned<ushort>(
                    ref Unsafe.Add(ref lut, (int)value << 1));

                Unsafe.WriteUnaligned(ref output, pair);
            }

            return digits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteDigitsUtf16(Span<char> dest, ulong value)
        {
            if (value < 10)
            {
                dest[0] = (char)('0' + value);
                return 1;
            }

            int digits = CountDecimalDigits(value);
            int pos = digits;
            ReadOnlySpan<byte> pairs = DigitPairs;

            while (value >= 100)
            {
                ulong q = value / 100;
                uint r = (uint)(value - q * 100);
                int p = (int)r << 1;
                dest[--pos] = (char)pairs[p + 1];
                dest[--pos] = (char)pairs[p];
                value = q;
            }

            if (value < 10)
            {
                dest[0] = (char)('0' + value);
            }
            else
            {
                int p = (int)value << 1;
                dest[0] = (char)pairs[p];
                dest[1] = (char)pairs[p + 1];
            }

            return digits;
        }

        /// <summary>
        /// Chooses fixed or scientific notation exactly like the previous JSON writer did:
        /// inside the historical window the old loop emitted fixed notation as long as the decimal
        /// needed at most 9/17 fraction digits; every other value went through Utf8Formatter, whose
        /// layout is the .NET "G" window (fixed for leading exponents in [-4, limit - 1]).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool UseFixedNotation(bool legacyRange, int exponent10, int digitCount, bool isFloat)
        {
            if (legacyRange && (exponent10 >= 0 || -exponent10 <= (isFloat ? FloatFractionCap : DoubleFractionCap)))
                return true;

            int leadingExponent = exponent10 + digitCount - 1;
            return leadingExponent >= -4 && leadingExponent < (isFloat ? 9 : 17);
        }

        /// <summary>
        /// Integral shortcut inside the historical exponent window. The old writer printed such values
        /// with a plain digit loop, and game data is dominated by them (positions, counts, ids), so the
        /// shortcut keeps both the previous bytes and the previous throughput.
        /// <para><paramref name="magnitudeLimit"/> keeps it inside the range where the value's spacing is
        /// below one, so no shorter decimal candidate (a tie point) can exist and the exact integer is
        /// already the shortest form: 2^24 for float, 2^53 for double. Above that limit the xjb core
        /// decides, which is why the emitted text always decodes to the core's canonical digits.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryWriteIntegralUtf8(Span<byte> dest, double magnitude, ulong absBits, ulong exponent, bool negative, ulong lowExponent, double magnitudeLimit, out int written)
        {
            written = 0;
            if (absBits == 0 || (ulong)(exponent - lowExponent) > 64UL || magnitude >= magnitudeLimit || magnitude != Math.Floor(magnitude))
                return false;

            int pos = 0;
            if (negative)
                dest[pos++] = (byte)'-';

            written = pos + WriteDigitsUtf8(dest.Slice(pos), (ulong)magnitude);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryWriteIntegralUtf16(Span<char> dest, double magnitude, ulong absBits, ulong exponent, bool negative, ulong lowExponent, double magnitudeLimit, out int written)
        {
            written = 0;
            if (absBits == 0 || (ulong)(exponent - lowExponent) > 64UL || magnitude >= magnitudeLimit || magnitude != Math.Floor(magnitude))
                return false;

            int pos = 0;
            if (negative)
                dest[pos++] = '-';

            written = pos + WriteDigitsUtf16(dest.Slice(pos), (ulong)magnitude);
            return true;
        }

        /// <summary>
        /// Guarded short-decimal dispatch for the values game data is made of (positions, config
        /// decimals, UI numbers): with at most three fraction digits the previous writer's verified
        /// scaling attempt costs a few nanoseconds, while the full shortest-decimal conversion costs
        /// roughly thirty. BenchmarkDotNet showed a 1.40x regression on GameLike doubles without this
        /// path, so it is kept as a measured dispatch (the task book allows exactly this); everything
        /// else falls through to the xjb core.
        /// <para>The verification compares the quotient with the value as a <c>double</c>, so a hit means
        /// the decimal is bit-exactly the value itself, and <c>maxDigits</c> keeps only digit counts where
        /// the decimal resolution is provably finer than the target type's spacing (6 for float, 15 for
        /// double). Both together rule out the old round-half-up tie artefact by construction: the unit
        /// tests assert that the emitted text always decodes to the core's canonical (digits, exponent)
        /// pair, which is what caught the float tie candidate 0x48201208 = 163912.125f during
        /// development.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryWriteShortDecimalUtf8(Span<byte> dest, double magnitude, ulong absBits, ulong exponent, bool negative, ulong lowExponent, int maxDigits, out int written)
        {
            written = 0;
            if (absBits == 0 || (ulong)(exponent - lowExponent) > 64UL || magnitude > 2_147_483_648.0)
                return false;

            // Cheap pre-filter: a value with more than three fraction digits is rejected with one
            // multiply; the attempts below still do the exact verification.
            double prefilter = magnitude * 1000.0;
            if (prefilter != Math.Floor(prefilter))
                return false;

            int decimals = 1;
            long scaled = (long)(magnitude * 10.0 + 0.5);
            if ((double)scaled / 10.0 != magnitude)
            {
                decimals = 2;
                scaled = (long)(magnitude * 100.0 + 0.5);
                if ((double)scaled / 100.0 != magnitude)
                {
                    decimals = 3;
                    scaled = (long)(magnitude * 1000.0 + 0.5);
                    if ((double)scaled / 1000.0 != magnitude)
                        return false;
                }
            }

            while (decimals > 0 && scaled % 10 == 0)
            {
                scaled /= 10;
                decimals--;
            }

            int length = CountDecimalDigits((ulong)scaled);

            // Provably shortest: a shorter candidate would have to sit within half a float/double ULP of
            // the value, while distinct decimals with fewer significant digits are further apart than
            // that for these digit counts (float: <= 6 digits vs a 2^-24 relative spacing; double:
            // <= 15 digits vs 2^-53). Longer forms fall through to the xjb core.
            if (length > maxDigits)
                return false;

            int pos = 0;
            if (negative)
                dest[pos++] = (byte)'-';

            written = pos + EmitFixedUtf8(dest.Slice(pos), (ulong)scaled, -decimals, length);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryWriteShortDecimalUtf16(Span<char> dest, double magnitude, ulong absBits, ulong exponent, bool negative, ulong lowExponent, int maxDigits, out int written)
        {
            written = 0;
            if (absBits == 0 || (ulong)(exponent - lowExponent) > 64UL || magnitude > 2_147_483_648.0)
                return false;

            double prefilter = magnitude * 1000.0;
            if (prefilter != Math.Floor(prefilter))
                return false;

            int decimals = 1;
            long scaled = (long)(magnitude * 10.0 + 0.5);
            if ((double)scaled / 10.0 != magnitude)
            {
                decimals = 2;
                scaled = (long)(magnitude * 100.0 + 0.5);
                if ((double)scaled / 100.0 != magnitude)
                {
                    decimals = 3;
                    scaled = (long)(magnitude * 1000.0 + 0.5);
                    if ((double)scaled / 1000.0 != magnitude)
                        return false;
                }
            }

            while (decimals > 0 && scaled % 10 == 0)
            {
                scaled /= 10;
                decimals--;
            }

            int length = CountDecimalDigits((ulong)scaled);
            if (length > maxDigits)
                return false;

            int pos = 0;
            if (negative)
                dest[pos++] = '-';

            written = pos + EmitFixedUtf16(dest.Slice(pos), (ulong)scaled, -decimals, length);
            return true;
        }

        internal static int WriteFloatUtf8(Span<byte> dest, float value)
        {
            uint bits = Unsafe.As<float, uint>(ref value);
            uint absBits = bits & 0x7FFF_FFFFu;
            uint exponent = (absBits >> 23) & 0xFFu;
            bool negative = (bits & 0x8000_0000u) != 0;
            double magnitude = negative ? -(double)value : value;

            if (TryWriteIntegralUtf8(dest, magnitude, absBits, exponent, negative, FloatLegacyLowExponent, 16_777_216.0, out int written))
                return written;

            if (TryWriteShortDecimalUtf8(dest, magnitude, absBits, exponent, negative, FloatLegacyLowExponent, 6, out written))
                return written;

            XjbFloat32.Convert(absBits, out ulong digits, out int exponent10);
            bool legacyRange = absBits == 0 || (uint)(exponent - FloatLegacyLowExponent) <= 64u;
            return EmitUtf8(dest, digits, exponent10, negative, legacyRange, true);
        }

        internal static int WriteDoubleUtf8(Span<byte> dest, double value)
        {
            ulong bits = Unsafe.As<double, ulong>(ref value);
            ulong absBits = bits & 0x7FFF_FFFF_FFFF_FFFFUL;
            ulong exponent = (absBits >> 52) & 0x7FFUL;
            bool negative = (bits & 0x8000_0000_0000_0000UL) != 0;
            double magnitude = negative ? -value : value;

            if (TryWriteIntegralUtf8(dest, magnitude, absBits, exponent, negative, DoubleLegacyLowExponent, 9_007_199_254_740_992.0, out int written))
                return written;

            if (TryWriteShortDecimalUtf8(dest, magnitude, absBits, exponent, negative, DoubleLegacyLowExponent, 15, out written))
                return written;

            XjbFloat64.Convert(absBits, out ulong digits, out int exponent10);
            bool legacyRange = absBits == 0 || (ulong)(exponent - DoubleLegacyLowExponent) <= 64UL;
            return EmitUtf8(dest, digits, exponent10, negative, legacyRange, false);
        }

        internal static int WriteFloatUtf16(Span<char> dest, float value)
        {
            uint bits = Unsafe.As<float, uint>(ref value);
            uint absBits = bits & 0x7FFF_FFFFu;
            uint exponent = (absBits >> 23) & 0xFFu;
            bool negative = (bits & 0x8000_0000u) != 0;
            double magnitude = negative ? -(double)value : value;

            if (TryWriteIntegralUtf16(dest, magnitude, absBits, exponent, negative, FloatLegacyLowExponent, 16_777_216.0, out int written))
                return written;

            if (TryWriteShortDecimalUtf16(dest, magnitude, absBits, exponent, negative, FloatLegacyLowExponent, 6, out written))
                return written;

            XjbFloat32.Convert(absBits, out ulong digits, out int exponent10);
            bool legacyRange = absBits == 0 || (uint)(exponent - FloatLegacyLowExponent) <= 64u;
            return EmitUtf16(dest, digits, exponent10, negative, legacyRange, true);
        }

        internal static int WriteDoubleUtf16(Span<char> dest, double value)
        {
            ulong bits = Unsafe.As<double, ulong>(ref value);
            ulong absBits = bits & 0x7FFF_FFFF_FFFF_FFFFUL;
            ulong exponent = (absBits >> 52) & 0x7FFUL;
            bool negative = (bits & 0x8000_0000_0000_0000UL) != 0;
            double magnitude = negative ? -value : value;

            if (TryWriteIntegralUtf16(dest, magnitude, absBits, exponent, negative, DoubleLegacyLowExponent, 9_007_199_254_740_992.0, out int written))
                return written;

            if (TryWriteShortDecimalUtf16(dest, magnitude, absBits, exponent, negative, DoubleLegacyLowExponent, 15, out written))
                return written;

            XjbFloat64.Convert(absBits, out ulong digits, out int exponent10);
            bool legacyRange = absBits == 0 || (ulong)(exponent - DoubleLegacyLowExponent) <= 64UL;
            return EmitUtf16(dest, digits, exponent10, negative, legacyRange, false);
        }

        private static int EmitUtf8(Span<byte> dest, ulong digits, int exponent10, bool negative, bool legacyRange, bool isFloat)
        {
            int pos = 0;
            if (digits == 0)
            {
                // Zero can only be reached through the legacy range (both ±0 bit patterns qualify).
                if (negative)
                    dest[pos++] = (byte)'-';
                dest[pos++] = (byte)'0';
                return pos;
            }

            int length = CountDecimalDigits(digits);
            if (negative)
                dest[pos++] = (byte)'-';

            return pos + (UseFixedNotation(legacyRange, exponent10, length, isFloat)
                ? EmitFixedUtf8(dest.Slice(pos), digits, exponent10, length)
                : EmitScientificUtf8(dest.Slice(pos), digits, exponent10, length));
        }

        private static int EmitUtf16(Span<char> dest, ulong digits, int exponent10, bool negative, bool legacyRange, bool isFloat)
        {
            int pos = 0;
            if (digits == 0)
            {
                if (negative)
                    dest[pos++] = '-';
                dest[pos++] = '0';
                return pos;
            }

            int length = CountDecimalDigits(digits);
            if (negative)
                dest[pos++] = '-';

            return pos + (UseFixedNotation(legacyRange, exponent10, length, isFloat)
                ? EmitFixedUtf16(dest.Slice(pos), digits, exponent10, length)
                : EmitScientificUtf16(dest.Slice(pos), digits, exponent10, length));
        }

        private static int EmitFixedUtf8(Span<byte> dest, ulong digits, int exponent10, int length)
        {
            if (exponent10 >= 0)
            {
                int written = WriteDigitsUtf8(dest, digits);
                for (int i = 0; i < exponent10; i++)
                    dest[written + i] = (byte)'0';
                return written + exponent10;
            }

            int fractionDigits = -exponent10;
            if (fractionDigits < length)
            {
                int written = WriteDigitsUtf8(dest, digits);
                int dot = length - fractionDigits;
                dest.Slice(dot, fractionDigits).CopyTo(dest.Slice(dot + 1));
                dest[dot] = (byte)'.';
                return written + 1;
            }

            int zeros = fractionDigits - length;
            int pos = 0;
            dest[pos++] = (byte)'0';
            dest[pos++] = (byte)'.';
            for (int i = 0; i < zeros; i++)
                dest[pos + i] = (byte)'0';
            pos += zeros;
            return pos + WriteDigitsUtf8(dest.Slice(pos), digits);
        }

        private static int EmitFixedUtf16(Span<char> dest, ulong digits, int exponent10, int length)
        {
            if (exponent10 >= 0)
            {
                int written = WriteDigitsUtf16(dest, digits);
                for (int i = 0; i < exponent10; i++)
                    dest[written + i] = '0';
                return written + exponent10;
            }

            int fractionDigits = -exponent10;
            if (fractionDigits < length)
            {
                int written = WriteDigitsUtf16(dest, digits);
                int dot = length - fractionDigits;
                dest.Slice(dot, fractionDigits).CopyTo(dest.Slice(dot + 1));
                dest[dot] = '.';
                return written + 1;
            }

            int zeros = fractionDigits - length;
            int pos = 0;
            dest[pos++] = '0';
            dest[pos++] = '.';
            for (int i = 0; i < zeros; i++)
                dest[pos + i] = '0';
            pos += zeros;
            return pos + WriteDigitsUtf16(dest.Slice(pos), digits);
        }

        private static int EmitScientificUtf8(Span<byte> dest, ulong digits, int exponent10, int length)
        {
            int leadingExponent = exponent10 + length - 1;

            // Digits are written two slots to the right so the leading digit can move in front of the point.
            int written = WriteDigitsUtf8(dest.Slice(2), digits);
            int pos;
            if (written == 1)
            {
                dest[0] = dest[2];
                pos = 1;
            }
            else
            {
                byte leading = dest[2];
                dest.Slice(3, written - 1).CopyTo(dest.Slice(2));
                dest[0] = leading;
                dest[1] = (byte)'.';
                pos = written + 1;
            }

            return pos + WriteExponentUtf8(dest.Slice(pos), leadingExponent);
        }

        private static int EmitScientificUtf16(Span<char> dest, ulong digits, int exponent10, int length)
        {
            int leadingExponent = exponent10 + length - 1;

            int written = WriteDigitsUtf16(dest.Slice(2), digits);
            int pos;
            if (written == 1)
            {
                dest[0] = dest[2];
                pos = 1;
            }
            else
            {
                char leading = dest[2];
                dest.Slice(3, written - 1).CopyTo(dest.Slice(2));
                dest[0] = leading;
                dest[1] = '.';
                pos = written + 1;
            }

            return pos + WriteExponentUtf16(dest.Slice(pos), leadingExponent);
        }

        private static int WriteExponentUtf8(Span<byte> dest, int leadingExponent)
        {
            int pos = 0;
            dest[pos++] = (byte)'E';
            dest[pos++] = leadingExponent < 0 ? (byte)'-' : (byte)'+';

            uint magnitude = (uint)(leadingExponent < 0 ? -leadingExponent : leadingExponent);
            if (magnitude >= 100)
                dest[pos++] = (byte)('0' + magnitude / 100);

            uint remainder = magnitude % 100;
            int pair = (int)remainder << 1;
            ReadOnlySpan<byte> pairs = DigitPairs;
            dest[pos++] = pairs[pair];
            dest[pos++] = pairs[pair + 1];
            return pos;
        }

        private static int WriteExponentUtf16(Span<char> dest, int leadingExponent)
        {
            int pos = 0;
            dest[pos++] = 'E';
            dest[pos++] = leadingExponent < 0 ? '-' : '+';

            uint magnitude = (uint)(leadingExponent < 0 ? -leadingExponent : leadingExponent);
            if (magnitude >= 100)
                dest[pos++] = (char)('0' + magnitude / 100);

            uint remainder = magnitude % 100;
            int pair = (int)remainder << 1;
            ReadOnlySpan<byte> pairs = DigitPairs;
            dest[pos++] = (char)pairs[pair];
            dest[pos++] = (char)pairs[pair + 1];
            return pos;
        }
    }
}