using System.Buffers.Text;
using System.Numerics;
using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest;

// Phase B verification for the xjb port: integer helpers, upstream tables, an independent
// exact-arithmetic shortest-decimal verifier, emitter layout and buffer safety.
//
// Note on oracles: Utf8Formatter/ToString("G") is NOT used as the shortest-decimal oracle
// because this runtime emits non round-tripping text for some values (for example
// 0x0410000000000000 renders as "4.104536801298376E-289", which parses back to
// 0x040FFFFFFFFFFFFF). Shortest-ness is therefore verified with exact rational interval
// arithmetic, and round-tripping with the correctly rounded .NET parser.
/// <summary>Provides json xjb formatter test functionality.</summary>
public class JsonXjbFormatterTest
{
    // ---------------------------------------------------------------- integer helpers

    /// <summary>Verifies mul64x64 matches big integer.</summary>
    [Fact]
    public void Mul64x64MatchesBigInteger()
    {
        ulong[] values =
        {
            0, 1, 2, 0xFFFF_FFFF, 0x1_0000_0000, 0x7FFF_FFFF_FFFF_FFFF, 0x8000_0000_0000_0000,
            ulong.MaxValue, 0x0123_4567_89AB_CDEF, 0xFEDC_BA98_7654_3210
        };

        var rng = new Random(20261010);
        for (int i = 0; i < 20_000; i++)
        {
            ulong a = i < values.Length ? values[i] : ((ulong)rng.NextInt64() << 32) | (uint)rng.Next();
            ulong b = ((ulong)rng.NextInt64() << 32) | (uint)rng.Next();

            XjbUIntMath.Mul64x64(a, b, out ulong hi, out ulong lo);
            XjbUIntMath.Mul64x64Portable(a, b, out ulong portableHi, out ulong portableLo);

            BigInteger expected = (BigInteger)a * b;
            BigInteger twoPow64 = BigInteger.One << 64;
            Assert.Equal((ulong)(expected / twoPow64), hi);
            Assert.Equal((ulong)(expected % twoPow64), lo);
            Assert.Equal(hi, XjbUIntMath.MulHi64(a, b));
            Assert.Equal(hi, portableHi);
            Assert.Equal(lo, portableLo);
        }
    }

    // ---------------------------------------------------------------- upstream tables

    /// <summary>Verifies tables match the locked upstream commit.</summary>
    [Fact]
    public void TablesMatchTheLockedUpstreamCommit()
    {
        // FNV-1a 64 hashes of the tables extracted from
        // https://github.com/xjb714/xjb @ 80cc89574a8f8457ffbf951afa2fd27c2459bd4a
        Assert.Equal(0xd587676ea34ea910UL, Fnv1a64(XjbFloat32Tables.Pow10Reverse, 8));
        Assert.Equal(0x2b1628fc094586e6UL, Fnv1a64(XjbFloat32Tables.H37, 1));
        Assert.Equal(0x011f1b8d67176a6aUL, Fnv1a64(XjbFloat64Tables.Pow10, 8));

        // The upstream h37 table equals the x64 formula h + 37 for every explicit entry
        // (index 255 is the omitted trailing zero of the C array and is unreachable because
        // exponent 255 is non-finite).
        for (int exp = 0; exp < 255; exp++)
        {
            int q = exp == 0 ? -149 : exp - 150;
            int k = (q * 1233) >> 12;
            int h = q + ((k * -1701 - 1701) >> 9);
            Assert.Equal((byte)(h + 37), XjbFloat32Tables.H37[exp]);
        }

        // The upstream double h7 table equals the x64 formula h + 7 for every exponent, so the
        // scalar core reproduces the aarch64 specialization bit for bit (hash of the official
        // table computed from the upstream source, see tools/xjb-tables/extract_tables.py).
        Assert.Equal(0x8fbe1453ffa24dd9UL, Fnv1a64(H7Formula(), 1));
    }

    // ---------------------------------------------------------------- int32/uint32 number fast path

    /// <summary>Writes int32 and write u int32 match utf8 formatter to the current output.</summary>
    [Fact]
    public void WriteInt32AndWriteUInt32MatchUtf8Formatter()
    {
        Span<byte> expected = stackalloc byte[16];
        Span<byte> dest = stackalloc byte[16];

        for (uint value = 0; value <= 10_000u; value++)
        {
            Utf8Formatter.TryFormat(value, expected, out int en);
            int an = LuminPackJsonNumberFormatter.WriteUInt32(dest, value);
            Assert.True(an == en && dest[..an].SequenceEqual(expected[..en]), $"uint32 mismatch: {value}");
        }

        for (int value = -10_000; value <= 10_000; value++)
        {
            Utf8Formatter.TryFormat(value, expected, out int en);
            int an = LuminPackJsonNumberFormatter.WriteInt32(dest, value);
            Assert.True(an == en && dest[..an].SequenceEqual(expected[..en]), $"int32 mismatch: {value}");
        }

        foreach (int value in new[] { int.MinValue, int.MinValue + 1, -1_000_000_000, -100_000, -10_001, 10_001, 100_000, 1_000_000_000, int.MaxValue - 1, int.MaxValue })
        {
            Utf8Formatter.TryFormat(value, expected, out int en);
            int an = LuminPackJsonNumberFormatter.WriteInt32(dest, value);
            Assert.True(an == en && dest[..an].SequenceEqual(expected[..en]), $"int32 mismatch: {value}");
        }

        foreach (uint value in new[] { 10_001u, 100_000u, 1_000_000_000u, uint.MaxValue - 1u, uint.MaxValue })
        {
            Utf8Formatter.TryFormat(value, expected, out int en);
            int an = LuminPackJsonNumberFormatter.WriteUInt32(dest, value);
            Assert.True(an == en && dest[..an].SequenceEqual(expected[..en]), $"uint32 mismatch: {value}");
        }

        ulong state = 0x5DEECE66D5DEECE6UL;
        for (int i = 0; i < 500_000; i++)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;

            uint u = (uint)z;
            Utf8Formatter.TryFormat(u, expected, out int en);
            int an = LuminPackJsonNumberFormatter.WriteUInt32(dest, u);
            Assert.True(an == en && dest[..an].SequenceEqual(expected[..en]), $"uint32 mismatch: {u}");

            int s = unchecked((int)z);
            Utf8Formatter.TryFormat(s, expected, out en);
            an = LuminPackJsonNumberFormatter.WriteInt32(dest, s);
            Assert.True(an == en && dest[..an].SequenceEqual(expected[..en]), $"int32 mismatch: {s}");
        }
    }

    /// <summary>Writes u long matches utf8 formatter to the current output.</summary>
    [Fact]
    public void WriteULongMatchesUtf8Formatter()
    {
        Span<byte> expected = stackalloc byte[24];
        Span<byte> dest = stackalloc byte[24];

        for (ulong value = 0; value <= 10_000UL; value++)
        {
            Utf8Formatter.TryFormat(value, expected, out int en);
            int an = LuminPackJsonNumberFormatter.WriteULong(dest, value);
            Assert.True(an == en && dest[..an].SequenceEqual(expected[..en]), $"ulong mismatch: {value}");
        }

        foreach (ulong value in new[] { 10_001UL, 100_000UL, 1_000_000_000UL, 9_999_999_999UL, 10_000_000_000UL, 1_000_000_000_000_000_000UL, ulong.MaxValue - 1UL, ulong.MaxValue })
        {
            Utf8Formatter.TryFormat(value, expected, out int en);
            int an = LuminPackJsonNumberFormatter.WriteULong(dest, value);
            Assert.True(an == en && dest[..an].SequenceEqual(expected[..en]), $"ulong mismatch: {value}");
        }

        ulong state = 0x243F6A8885A308D3UL;
        for (int i = 0; i < 500_000; i++)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;

            Utf8Formatter.TryFormat(z, expected, out int en);
            int an = LuminPackJsonNumberFormatter.WriteULong(dest, z);
            Assert.True(an == en && dest[..an].SequenceEqual(expected[..en]), $"ulong mismatch: {z}");
        }
    }

    /// <summary>Verifies h7 formula.</summary>
    /// <returns>The result of the operation.</returns>
    private static byte[] H7Formula()
    {
        var h7 = new byte[2048];
        for (int exp = 0; exp < 2048; exp++)
        {
            int q = exp == 0 ? -1074 : exp - 1075;
            int k = ((exp - 1075) * 78913) >> 18;
            int h = q + ((-1 - k) * 217707 >> 16);
            h7[exp] = (byte)(h + 7);
        }

        return h7;
    }

    /// <summary>Verifies fnv1a64.</summary>
    /// <param name="values">The values used by this operation.</param>
    /// <param name="byteWidth">The byte width used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static ulong Fnv1a64(byte[] values, int byteWidth)
    {
        ulong h = 0xCBF29CE484222325UL;
        foreach (byte v in values)
        {
            h ^= v;
            h *= 0x100000001B3UL;
        }

        return h;
    }

    /// <summary>Verifies fnv1a64.</summary>
    /// <param name="values">The values used by this operation.</param>
    /// <param name="byteWidth">The byte width used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static ulong Fnv1a64(ulong[] values, int byteWidth)
    {
        ulong h = 0xCBF29CE484222325UL;
        foreach (ulong v in values)
        {
            for (int i = 0; i < byteWidth; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 0x100000001B3UL;
            }
        }

        return h;
    }

    // ---------------------------------------------------------------- cores

    /// <summary>Verifies float core produces the shortest round tripping decimal.</summary>
    [Fact]
    public void FloatCoreProducesTheShortestRoundTrippingDecimal()
    {
        int count = 0;
        foreach (float value in FloatCorpus())
        {
            uint bits = BitConverter.SingleToUInt32Bits(value);
            uint absBits = bits & 0x7FFF_FFFFu;
            if ((absBits & 0x7F80_0000u) == 0x7F80_0000u)
                continue;

            XjbFloat32.Convert(absBits, out ulong digits, out int exponent10);
            VerifyDecimal(absBits, digits, exponent10, isFloat: true,
                (bits & 0x8000_0000u) != 0 ? "-0" : "0", EmitUtf8(value), EmitUtf16(value));
            count++;
        }

        Assert.True(count > 50_000, $"float corpus too small: {count}");
    }

    /// <summary>Verifies double core produces the shortest round tripping decimal.</summary>
    [Fact]
    public void DoubleCoreProducesTheShortestRoundTrippingDecimal()
    {
        int count = 0;
        foreach (double value in DoubleCorpus())
        {
            ulong bits = BitConverter.DoubleToUInt64Bits(value);
            ulong absBits = bits & 0x7FFF_FFFF_FFFF_FFFFUL;
            if ((absBits & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL)
                continue;

            XjbFloat64.Convert(absBits, out ulong digits, out int exponent10);
            VerifyDecimal(absBits, digits, exponent10, isFloat: false,
                (bits & 0x8000_0000_0000_0000UL) != 0 ? "-0" : "0", EmitUtf8(value), EmitUtf16(value));
            count++;
        }

        Assert.True(count > 50_000, $"double corpus too small: {count}");
    }

    // ---------------------------------------------------------------- opt-in long runs

    /// <summary>
    /// Exhaustive binary32 sweep. Opt-in because it walks all 2^31 positive finite bit patterns
    /// (the negative half only differs by the sign): set LUMINPACK_XJB_LONG=1 and run
    ///   dotnet test --filter FullyQualifiedName~ExhaustiveFloatBitPatternSweep
    /// Every value must round-trip bit for bit; the exact shortest-decimal interval check runs on
    /// every 4096th value.
    /// </summary>
    [Fact]
    public void ExhaustiveFloatBitPatternSweep()
    {
        if (Environment.GetEnvironmentVariable("LUMINPACK_XJB_LONG") != "1")
            return;

        Span<byte> buffer = stackalloc byte[32];
        long checkedValues = 0;
        for (uint bits = 0; bits <= 0x7F7F_FFFFu; bits++)
        {
            float value = BitConverter.UInt32BitsToSingle(bits);
            int written = LuminPackJsonFloatFormatter.WriteFloatUtf8(buffer, value);
            if (!System.Buffers.Text.Utf8Parser.TryParse(buffer[..written], out float parsed, out int consumed) ||
                consumed != written ||
                BitConverter.SingleToUInt32Bits(parsed) != bits)
            {
                Assert.Fail($"float round trip failed for 0x{bits:X8}: '{System.Text.Encoding.ASCII.GetString(buffer[..written])}'");
            }

            if ((bits & 0xFFFu) == 0)
            {
                uint absBits = bits & 0x7FFF_FFFFu;
                XjbFloat32.Convert(absBits, out ulong digits, out int exponent10);
                VerifyDecimal(absBits, digits, exponent10, isFloat: true, "0", EmitUtf8(value), EmitUtf16(value));
            }

            checkedValues++;
        }

        Assert.True(checkedValues == 0x7F80_0000L, $"swept {checkedValues} of {0x7F80_0000L} positive finite patterns");
    }

    /// <summary>
    /// Large deterministic random binary64 sample with the full verifier (opt-in, see above):
    /// 5,000,000 bit patterns round-trip and every 64th value gets the exact shortest check.
    /// </summary>
    [Fact]
    public void LargeRandomDoubleSample()
    {
        if (Environment.GetEnvironmentVariable("LUMINPACK_XJB_LONG") != "1")
            return;

        Span<byte> buffer = stackalloc byte[32];
        var rng = new Random(0x1CEB_00DA);
        long verified = 0;
        for (long i = 0; i < 5_000_000; i++)
        {
            ulong bits = ((ulong)rng.NextInt64() << 32) | (uint)rng.NextInt64();
            ulong absBits = bits & 0x7FFF_FFFF_FFFF_FFFFUL;
            if ((absBits & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL)
                continue;

            double value = BitConverter.UInt64BitsToDouble(bits);
            int written = LuminPackJsonFloatFormatter.WriteDoubleUtf8(buffer, value);
            if (!System.Buffers.Text.Utf8Parser.TryParse(buffer[..written], out double parsed, out int consumed) ||
                consumed != written ||
                BitConverter.DoubleToUInt64Bits(parsed) != bits)
            {
                Assert.Fail($"double round trip failed for 0x{bits:X16}: '{System.Text.Encoding.ASCII.GetString(buffer[..written])}'");
            }

            if ((i & 63) == 0)
            {
                XjbFloat64.Convert(absBits, out ulong digits, out int exponent10);
                VerifyDecimal(absBits, digits, exponent10, isFloat: false, "0", EmitUtf8(value), EmitUtf16(value));
                verified++;
            }
        }

        Assert.True(verified > 50_000, $"verified only {verified} values");
    }

    /// <summary>Verifies verify decimal.</summary>
    /// <param name="absBits">The abs bits used by this operation.</param>
    /// <param name="digits">The digits used by this operation.</param>
    /// <param name="exponent10">The exponent10 used by this operation.</param>
    /// <param name="isFloat">The is float used by this operation.</param>
    /// <param name="zeroText">The zero text used by this operation.</param>
    /// <param name="utf8Text">The utf8 text used by this operation.</param>
    /// <param name="utf16Text">The utf16 text used by this operation.</param>
    private static void VerifyDecimal(ulong absBits, ulong digits, int exponent10, bool isFloat, string zeroText, string utf8Text, string utf16Text)
    {
        Assert.Equal(utf8Text, utf16Text);

        int mantissaBits = isFloat ? 23 : 52;
        int exponent = (int)((absBits >> mantissaBits) & (isFloat ? 0xFFUL : 0x7FFUL));
        ulong fraction = absBits & ((1UL << mantissaBits) - 1);

        if (exponent == 0 && fraction == 0)
        {
            Assert.Equal(0UL, digits);
            Assert.Equal(zeroText, utf8Text);
            return;
        }

        Assert.True(digits > 0, "a non-zero finite value must produce non-zero digits");
        Assert.NotEqual(0UL, digits % 10); // canonical form: no insignificant trailing zero

        // The emitted text must parse back to the exact same bit pattern (correctly rounded parser).
        if (isFloat)
        {
            float parsed = float.Parse(utf8Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal((uint)absBits, BitConverter.SingleToUInt32Bits(parsed) & 0x7FFF_FFFFu);
        }
        else
        {
            double parsed = double.Parse(utf8Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(absBits, BitConverter.DoubleToUInt64Bits(parsed) & 0x7FFF_FFFF_FFFF_FFFFUL);
        }

        AssertShortestDecimal(absBits, mantissaBits, exponent, fraction, digits, exponent10);

        // The emitted text must carry exactly the core's canonical decimal: this also cross-checks the
        // guarded short-decimal dispatch against the xjb core for the whole corpus.
        (ulong textDigits, int textExponent) = ParseDecimalText(utf8Text);
        Assert.True(textDigits == digits && textExponent == exponent10,
            $"text '{utf8Text}' decodes to ({textDigits},{textExponent}) but the core produced ({digits},{exponent10}) for 0x{absBits:X}");

        // Format policy: the legacy window keeps fixed notation while the decimal needs at most
        // 9/17 fraction digits; every other value uses the .NET "G" window.
        int eIndex = utf8Text.IndexOf('E');
        int leadingExponent = exponent10 + DigitCount(digits) - 1;
        int limit = isFloat ? 9 : 17;
        bool legacy = LegacyFixedRange(isFloat, exponent, absBits);
        bool expectFixed = (legacy && (exponent10 >= 0 || -exponent10 <= limit))
            || (leadingExponent >= -4 && leadingExponent < limit);

        if (expectFixed)
            Assert.True(eIndex < 0, $"expected fixed notation for {utf8Text}");
        else
            Assert.True(eIndex > 0, $"expected scientific notation for {utf8Text}");

        if (eIndex >= 0)
        {
            Assert.True(utf8Text[eIndex + 1] == '-' || utf8Text[eIndex + 1] == '+', $"missing exponent sign in {utf8Text}");
            Assert.True(utf8Text.Length - eIndex - 1 >= 3, $"exponent needs at least two digits in {utf8Text}");
            int writtenExponent = int.Parse(utf8Text[(eIndex + 1)..], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(leadingExponent, writtenExponent);
        }
    }

    /// <summary>Verifies legacy fixed range.</summary>
    /// <param name="isFloat">The is float used by this operation.</param>
    /// <param name="exponent">The exponent used by this operation.</param>
    /// <param name="absBits">The abs bits used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool LegacyFixedRange(bool isFloat, int exponent, ulong absBits) =>
        isFloat
            ? absBits == 0 || (uint)(exponent - 95) <= 64u
            : absBits == 0 || (uint)(exponent - 991) <= 64u;

    /// <summary>
    /// Exact verification that <paramref name="digits"/> x 10^<paramref name="exponent10"/> is inside the
    /// rounding interval of the value and that no decimal with fewer significant digits is.
    /// </summary>
    /// <param name="absBits">The abs bits used by this operation.</param>
    /// <param name="mantissaBits">The mantissa bits used by this operation.</param>
    /// <param name="exponent">The exponent used by this operation.</param>
    /// <param name="fraction">The fraction used by this operation.</param>
    /// <param name="digits">The digits used by this operation.</param>
    /// <param name="exponent10">The exponent10 used by this operation.</param>
    private static void AssertShortestDecimal(ulong absBits, int mantissaBits, int exponent, ulong fraction, ulong digits, int exponent10)
    {
        BigInteger mantissa = fraction;
        int exponent2;
        if (exponent == 0)
        {
            exponent2 = 1 - (mantissaBits == 23 ? 127 : 1023) - mantissaBits;
        }
        else
        {
            mantissa |= BigInteger.One << mantissaBits;
            exponent2 = exponent - (mantissaBits == 23 ? 127 : 1023) - mantissaBits;
        }

        // Interval of decimals that round back to this value. Values with fraction == 0 and a non-zero
        // exponent sit on a binade boundary, so the spacing below is half the spacing above (the
        // asymmetric "irregular" interval); everything else uses the symmetric +-ulp/2 bounds.
        // The bounds are inclusive when the significand is even (round half to even).
        bool powerOfTwo = exponent > 0 && fraction == 0;
        Rat lo = powerOfTwo ? Rat.Dyadic(4 * mantissa - 1, exponent2 - 2) : Rat.Dyadic(2 * mantissa - 1, exponent2 - 1);
        Rat hi = Rat.Dyadic(2 * mantissa + 1, exponent2 - 1);
        bool inclusive = mantissa.IsEven;

        Rat candidate = Rat.Decimal(digits, exponent10);
        Assert.True(candidate.CompareTo(lo) >= (inclusive ? 0 : 1), $"candidate {digits}e{exponent10} below the rounding interval of 0x{absBits:X}");
        Assert.True(candidate.CompareTo(hi) <= (inclusive ? 0 : -1), $"candidate {digits}e{exponent10} above the rounding interval of 0x{absBits:X}");

        int length = DigitCount(digits);
        for (int shorter = 1; shorter < length; shorter++)
        {
            int baseExponent = exponent10 + (length - shorter);
            for (int delta = -2; delta <= 2; delta++)
            {
                int k = baseExponent + delta;
                Rat loScaled = lo.DividePow10(k);
                Rat hiScaled = hi.DividePow10(k);
                BigInteger min = Rat.Ceiling(loScaled);
                BigInteger max = Rat.Floor(hiScaled);

                if (!inclusive)
                {
                    if (loScaled.IsInteger)
                        min = BigInteger.Max(min, loScaled.IntegerValue + 1);
                    if (hiScaled.IsInteger)
                        max = BigInteger.Min(max, hiScaled.IntegerValue - 1);
                }

                BigInteger from = BigInteger.Pow(10, shorter - 1);
                BigInteger to = from * 10 - 1;
                BigInteger a = BigInteger.Max(min, from);
                BigInteger b = BigInteger.Min(max, to);
                Assert.True(a > b,
                    $"a shorter decimal exists for 0x{absBits:X}: {digits}e{exponent10} has {length} digits but D={a} x 10^{k} fits the interval");
            }
        }
    }

    /// <summary>Verifies digit count.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static int DigitCount(ulong value)
    {
        int count = 1;
        while (value >= 10)
        {
            value /= 10;
            count++;
        }

        return count;
    }

    /// <summary>Splits a decimal text into canonical digits and the matching power of ten.</summary>
    /// <param name="text">The text used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static (ulong Digits, int Exponent10) ParseDecimalText(string text)
    {
        if (text.StartsWith('-'))
            text = text[1..];

        int exponent = 0;
        int e = text.IndexOf('E');
        if (e >= 0)
        {
            exponent = int.Parse(text[(e + 1)..], System.Globalization.CultureInfo.InvariantCulture);
            text = text[..e];
        }

        var mantissa = new System.Text.StringBuilder(text.Length);
        int fractionDigits = 0;
        bool seenDot = false;
        foreach (char c in text)
        {
            if (c == '.')
            {
                seenDot = true;
                continue;
            }

            mantissa.Append(c);
            if (seenDot)
                fractionDigits++;
        }

        string digits = mantissa.ToString().TrimStart('0');
        int trailing = 0;
        while (digits.Length - trailing > 1 && digits[digits.Length - 1 - trailing] == '0')
            trailing++;

        if (trailing > 0)
            digits = digits[..^trailing];

        if (digits.Length == 0)
            return (0, 0);

        return (ulong.Parse(digits, System.Globalization.CultureInfo.InvariantCulture), exponent - fractionDigits + trailing);
    }

    /// <summary>Exact rational number used by the shortest-decimal verifier.</summary>
    private readonly struct Rat
    {
        private readonly BigInteger _num;
        private readonly BigInteger _den;

        /// <summary>Initializes a new instance of <c>Rat</c>.</summary>
        /// <param name="num">The num used by this operation.</param>
        /// <param name="den">The den used by this operation.</param>
        private Rat(BigInteger num, BigInteger den)
        {
            if (den.Sign < 0)
            {
                num = -num;
                den = -den;
            }

            _num = num;
            _den = den;
        }

        /// <summary>Gets the is integer.</summary>
        internal bool IsInteger => _num % _den == 0;

        /// <summary>Gets the integer value.</summary>
        internal BigInteger IntegerValue => _num / _den;

        /// <summary>Verifies dyadic.</summary>
        /// <param name="mantissa">The mantissa used by this operation.</param>
        /// <param name="exponent2">The exponent2 used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        internal static Rat Dyadic(BigInteger mantissa, int exponent2) =>
            exponent2 >= 0 ? new Rat(mantissa << exponent2, BigInteger.One) : new Rat(mantissa, BigInteger.One << -exponent2);

        /// <summary>Verifies decimal.</summary>
        /// <param name="digits">The digits used by this operation.</param>
        /// <param name="exponent10">The exponent10 used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        internal static Rat Decimal(ulong digits, int exponent10) =>
            exponent10 >= 0 ? new Rat(new BigInteger(digits) * BigInteger.Pow(10, exponent10), BigInteger.One)
                            : new Rat(new BigInteger(digits), BigInteger.Pow(10, -exponent10));

        /// <summary>Verifies compare to.</summary>
        /// <param name="other">The other used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        internal int CompareTo(Rat other) => (_num * other._den).CompareTo(other._num * _den);

        /// <summary>Verifies divide pow10.</summary>
        /// <param name="exponent10">The exponent10 used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        internal Rat DividePow10(int exponent10) =>
            exponent10 >= 0 ? new Rat(_num, _den * BigInteger.Pow(10, exponent10))
                            : new Rat(_num * BigInteger.Pow(10, -exponent10), _den);

        /// <summary>Verifies floor.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        internal static BigInteger Floor(Rat value)
        {
            BigInteger quotient = BigInteger.DivRem(value._num, value._den, out BigInteger remainder);
            if (remainder.Sign < 0)
                quotient -= 1;
            return quotient;
        }

        /// <summary>Verifies ceiling.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        internal static BigInteger Ceiling(Rat value)
        {
            BigInteger quotient = BigInteger.DivRem(value._num, value._den, out BigInteger remainder);
            if (remainder.Sign > 0)
                quotient += 1;
            return quotient;
        }
    }

    /// <summary>Verifies emit utf8.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string EmitUtf8(double value)
    {
        Span<byte> buffer = stackalloc byte[48];
        int written = LuminPackJsonFloatFormatter.WriteDoubleUtf8(buffer, value);
        return System.Text.Encoding.ASCII.GetString(buffer[..written]);
    }

    /// <summary>Verifies emit utf8.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string EmitUtf8(float value)
    {
        Span<byte> buffer = stackalloc byte[48];
        int written = LuminPackJsonFloatFormatter.WriteFloatUtf8(buffer, value);
        return System.Text.Encoding.ASCII.GetString(buffer[..written]);
    }

    /// <summary>Verifies emit utf16.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string EmitUtf16(double value)
    {
        Span<char> buffer = stackalloc char[48];
        int written = LuminPackJsonFloatFormatter.WriteDoubleUtf16(buffer, value);
        return new string(buffer[..written]);
    }

    /// <summary>Verifies emit utf16.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string EmitUtf16(float value)
    {
        Span<char> buffer = stackalloc char[48];
        int written = LuminPackJsonFloatFormatter.WriteFloatUtf16(buffer, value);
        return new string(buffer[..written]);
    }

    // ---------------------------------------------------------------- corpora

    /// <summary>Verifies float corpus.</summary>
    /// <returns>The result of the operation.</returns>
    private static float[] FloatCorpus()
    {
        var values = new System.Collections.Generic.List<float>();
        var rng = new Random(0x5F10A7E5);

        for (uint bits = 0; bits <= 0x7F80_0000u; bits += 0x0004_0001u)
            values.Add(BitConverter.UInt32BitsToSingle(bits));

        // every exponent segment with a few significands, including the irregular boundary
        for (int exponent = 0; exponent <= 254; exponent++)
        {
            foreach (uint significand in new[] { 0u, 1u, 0x40_0000u, 0x7F_FFFFu })
                values.Add(BitConverter.UInt32BitsToSingle(((uint)exponent << 23) | significand));
        }

        float[] structured =
        {
            0f, -0f, 1f, -1f, 0.5f, 0.1f, 0.3f, 1e-45f, float.Epsilon, float.MaxValue, float.MinValue,
            3.4028235e38f, 1.17549435e-38f, 16777215f, 16777216f, 16777218f, 123456.75f,
            1e7f, 1e8f, 1e9f, 1e10f, 1e-4f, 1e-5f, 1e-9f, 1e-10f, 1e-20f, 1e20f, 3.1415927f, 1f / 3f,
            0.000123f, 123456789f, 999999999f, 1e15f, 1e30f, 2.3283064e-10f
        };

        values.AddRange(structured);

        foreach (float v in structured)
        {
            uint b = BitConverter.SingleToUInt32Bits(v) & 0x7FFF_FFFFu;
            if ((b & 0x7F80_0000u) == 0x7F80_0000u)
                continue;
            values.Add(BitConverter.UInt32BitsToSingle(b + 1));
            values.Add(BitConverter.UInt32BitsToSingle(b - 1));
            values.Add(BitConverter.UInt32BitsToSingle(b + 2));
            values.Add(BitConverter.UInt32BitsToSingle(b - 2));
        }

        for (int i = 0; i < 60_000; i++)
        {
            uint bits = (uint)rng.NextInt64();
            if ((bits & 0x7F80_0000u) == 0x7F80_0000u)
                continue;
            values.Add(BitConverter.UInt32BitsToSingle(bits));
        }

        return values.ToArray();
    }

    /// <summary>Verifies double corpus.</summary>
    /// <returns>The result of the operation.</returns>
    private static double[] DoubleCorpus()
    {
        var values = new System.Collections.Generic.List<double>();
        var rng = new Random(unchecked((int)0xD0AB1E55));

        for (int exponent = 0; exponent <= 2046; exponent++)
        {
            foreach (ulong significand in new[] { 0UL, 1UL, 1UL << 51, (1UL << 52) - 1 })
                values.Add(BitConverter.UInt64BitsToDouble(((ulong)exponent << 52) | significand));
        }

        double[] structured =
        {
            0d, -0d, 1d, -1d, 0.5d, 0.1d, 0.3d, 0.30000000000000004d, 1.83749023360500067d,
            double.Epsilon, double.MaxValue, double.MinValue, 2.2250738585072014e-308d,
            9007199254740991d, 9007199254740992d, 9007199254740994d, 1e15d, 1e16d, 1e17d, 1e22d,
            1e23d, 1e100d, 1e300d, 1e-100d, 1e-300d, 5e-324d, 4.9406564584124654e-324d,
            123456789.125d, 3.141592653589793d, 2.718281828459045d, 0.0001d, 0.00001d, 1e-9d,
            1.2345678901234567e15d, 1234567890123456789d, 0.9999999999999999d, 1.0000000000000002d,
            4.1045368012983762e-289d, 2.2250738585072011e-308d
        };

        values.AddRange(structured);

        foreach (double v in structured)
        {
            ulong b = BitConverter.DoubleToUInt64Bits(v) & 0x7FFF_FFFF_FFFF_FFFFUL;
            if ((b & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL)
                continue;
            values.Add(BitConverter.UInt64BitsToDouble(b + 1));
            values.Add(BitConverter.UInt64BitsToDouble(b - 1));
            values.Add(BitConverter.UInt64BitsToDouble(b + 2));
            values.Add(BitConverter.UInt64BitsToDouble(b - 2));
        }

        for (int i = 0; i < 60_000; i++)
        {
            ulong bits = ((ulong)rng.NextInt64() << 32) | (uint)rng.NextInt64();
            if ((bits & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL)
                continue;
            values.Add(BitConverter.UInt64BitsToDouble(bits));
        }

        return values.ToArray();
    }

    // ---------------------------------------------------------------- emitter buffers

    /// <summary>Verifies emitter writes exactly the reported length inside an exact buffer.</summary>
    [Fact]
    public void EmitterWritesExactlyTheReportedLengthInsideAnExactBuffer()
    {
        var values = new double[]
        {
            0d, -0d, 1d, -1.5d, 0.1d, 1e-5d, 1e-9d, 1e16d, 1e17d, 1e300d, double.MaxValue,
            double.Epsilon, 5e-324d, 123456789.125d, 9007199254740993d, 0.00001d
        };

        foreach (double value in values)
        {
            string text = EmitUtf8(value);
            var exact = new byte[text.Length + 8];
            for (int i = 0; i < exact.Length; i++)
                exact[i] = 0xAA;

            int written = LuminPackJsonFloatFormatter.WriteDoubleUtf8(exact, value);

            Assert.Equal(text.Length, written);
            Assert.Equal(text, System.Text.Encoding.ASCII.GetString(exact[..written]));
            for (int i = written; i < exact.Length; i++)
                Assert.Equal(0xAA, exact[i]);

            Span<char> exactChars = new char[text.Length + 8];
            exactChars.Fill('\uFFFF');
            int writtenChars = LuminPackJsonFloatFormatter.WriteDoubleUtf16(exactChars, value);
            Assert.Equal(text.Length, writtenChars);
            Assert.Equal(text, new string(exactChars[..writtenChars]));
            for (int i = writtenChars; i < exactChars.Length; i++)
                Assert.Equal('\uFFFF', exactChars[i]);
        }

        foreach (float value in new[] { 0f, -0f, 1f, 1e9f, 1e10f, 1e-5f, 1e-9f, float.MaxValue, 3.4028235e38f, 0.00001f })
        {
            string text = EmitUtf8(value);
            Span<byte> exact = new byte[text.Length + 8];
            exact.Fill(0xAA);

            int written = LuminPackJsonFloatFormatter.WriteFloatUtf8(exact, value);
            Assert.Equal(text.Length, written);
            Assert.Equal(text, System.Text.Encoding.ASCII.GetString(exact[..written]));
            for (int i = written; i < exact.Length; i++)
                Assert.Equal(0xAA, exact[i]);
        }
    }

    /// <summary>Writes r rejects non finite values and keeps negative zero to the current output.</summary>
    [Fact]
    public void WriterRejectsNonFiniteValuesAndKeepsNegativeZero()
    {
        Span<byte> buffer = stackalloc byte[64];
        var writer = new LuminPackJsonWriter(ref buffer, new LuminPack.Option.LuminPackWriterOptionalState());

        Assert.True(ThrowsDouble(ref writer, double.NaN));
        Assert.True(ThrowsDouble(ref writer, double.PositiveInfinity));
        Assert.True(ThrowsDouble(ref writer, double.NegativeInfinity));
        Assert.True(ThrowsFloat(ref writer, float.NegativeInfinity));

        var negative = new LuminPackJsonWriter(ref buffer, new LuminPack.Option.LuminPackWriterOptionalState());
        negative.WriteDouble(-0.0);
        Assert.Equal("-0", System.Text.Encoding.ASCII.GetString(negative.GetSpan()));

        var positive = new LuminPackJsonWriter(ref buffer, new LuminPack.Option.LuminPackWriterOptionalState());
        positive.WriteFloat(0f);
        Assert.Equal("0", System.Text.Encoding.ASCII.GetString(positive.GetSpan()));
    }

    /// <summary>Raises the exception for s double.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>This method never returns a value.</returns>
    private static bool ThrowsDouble(ref LuminPackJsonWriter writer, double value)
    {
        try
        {
            writer.WriteDouble(value);
            return false;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Raises the exception for s float.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>This method never returns a value.</returns>
    private static bool ThrowsFloat(ref LuminPackJsonWriter writer, float value)
    {
        try
        {
            writer.WriteFloat(value);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
