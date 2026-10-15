using System.Globalization;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides number syntax probe functionality.</summary>
[LuminPackable]
public sealed partial class NumberSyntaxProbe
{
    /// <summary>Gets or sets the age.</summary>
    public int Age { get; set; }
    /// <summary>Gets or sets the values.</summary>
    public int[] Values { get; set; } = System.Array.Empty<int>();
}

// RFC 8259 number grammar, token boundaries and IEEE-754 correct rounding for the JSON reader.
// Both encodings run the same contract: numbers written by LuminPack round-trip bit for bit and
// malformed numbers are rejected instead of being truncated.
/// <summary>Provides json number syntax test functionality.</summary>
public class JsonNumberSyntaxTest
{
    private static readonly LuminPackSerializerOption s_strictUtf8 =
        LuminPackSerializerOption.Utf8 with { ValidateJsonSyntax = true };
    private static readonly LuminPackSerializerOption s_strictUtf16 =
        LuminPackSerializerOption.Utf16 with { ValidateJsonSyntax = true };

    /// <summary>Gets the integer literals.</summary>
    public static TheoryData<string> IntegerLiterals => new()
    {
        "0", "-0", "1", "-1", "9", "10", "99", "123456789",
        "2147483647", "-2147483648", "4294967295", "65535", "32767", "-32768", "255", "-128",
        "9223372036854775807", "-9223372036854775808", "18446744073709551615"
    };

    /// <summary>Gets the unsigned literals.</summary>
    public static TheoryData<string> UnsignedLiterals => new()
    {
        "0", "1", "9", "10", "99", "123456789",
        "2147483647", "4294967295", "65535", "255",
        "9223372036854775807", "18446744073709551615"
    };

    /// <summary>Gets the signed literals.</summary>
    public static TheoryData<string> SignedLiterals => new()
    {
        "0", "-0", "1", "-1", "9", "10", "99", "123456789",
        "2147483647", "-2147483648", "65535", "32767", "-32768", "255", "-128",
        "9223372036854775807", "-9223372036854775808"
    };

    /// <summary>Verifies invalid integer tokens are rejected.</summary>
    /// <param name="literal">The literal used by this operation.</param>
    [Theory]
    [InlineData("1.5"), InlineData("1e2"), InlineData("1E2"), InlineData("01"), InlineData("-01"),
    InlineData("00"), InlineData("1."), InlineData("1e"), InlineData("1e+"), InlineData("+1"),
    InlineData("--1"), InlineData("-.1"), InlineData("1x"), InlineData("NaN"), InlineData("Infinity"),
    InlineData(".5"), InlineData("1e--2"), InlineData("1.2.3"), InlineData("2147483648"),
    InlineData("-2147483649")]
    public void InvalidIntegerTokensAreRejected(string literal)
    {
        Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<int>(literal, s_strictUtf8));
        Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<int>(literal, s_strictUtf16));
    }

    /// <summary>Verifies malformed structures are rejected.</summary>
    /// <param name="json">The json used by this operation.</param>
    [Theory]
    [InlineData("[1 2]")]
    [InlineData("[1,]")]
    [InlineData("[,]")]
    [InlineData("]")]
    [InlineData("}")]
    [InlineData("{\"Age\" 1}")]
    [InlineData("{\"Age\":1,}")]
    [InlineData("{\"Age\":1 \"Values\":[]}")]
    [InlineData("{\"Age\":1]")]
    [InlineData("{\"Values\":[1}}")]
    [InlineData("{\"Age\":1}extra")]
    [InlineData("1 2")]
    [InlineData("[1")]
    [InlineData("{\"Age\":1")]
    public void MalformedStructuresAreRejected(string json)
    {
        Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<NumberSyntaxProbe>(json, s_strictUtf8));
        Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<NumberSyntaxProbe>(json, s_strictUtf16));
    }

    /// <summary>Verifies malformed literals are rejected.</summary>
    /// <param name="json">The json used by this operation.</param>
    [Theory]
    [InlineData("truex")]
    [InlineData("nullx")]
    [InlineData("false0")]
    [InlineData("tru")]
    [InlineData("nul")]
    public void MalformedLiteralsAreRejected(string json)
    {
        Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<bool>(json, s_strictUtf8));
        Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<bool>(json, s_strictUtf16));
    }

    /// <summary>Verifies valid numbers still parse.</summary>
    /// <param name="json">The json used by this operation.</param>
    /// <param name="expected">The expected used by this operation.</param>
    [Theory]
    [InlineData("[1,2,3]", new[] { 1, 2, 3 })]
    public void ValidNumbersStillParse(string json, int[] expected)
    {
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<int[]>(json, s_strictUtf8));
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<int[]>(json, s_strictUtf16));
    }

    /// <summary>Verifies valid double tokens parse.</summary>
    /// <param name="literal">The literal used by this operation.</param>
    /// <param name="expected">The expected used by this operation.</param>
    [Theory]
    [InlineData("0", 0)]
    [InlineData("-0", 0)]
    [InlineData("0.0", 0.0)]
    [InlineData("1e2", 100.0)]
    [InlineData("1E+2", 100.0)]
    [InlineData("1e-2", 0.01)]
    [InlineData("0.001", 0.001)]
    [InlineData("3.141592653589793", 3.141592653589793)]
    [InlineData("-2.5", -2.5)]
    public void ValidDoubleTokensParse(string literal, double expected)
    {
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<double>(literal, s_strictUtf8));
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<double>(literal, s_strictUtf16));
    }

    /// <summary>Verifies valid integer tokens parse.</summary>
    /// <param name="literal">The literal used by this operation.</param>
    /// <param name="expected">The expected used by this operation.</param>
    [Theory]
    [InlineData("0", 0)]
    [InlineData("-1", -1)]
    [InlineData("1", 1)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void ValidIntegerTokensParse(string literal, int expected)
    {
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<int>(literal, s_strictUtf8));
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<int>(literal, s_strictUtf16));
    }

    /// <summary>Verifies negative zero keeps its sign bit.</summary>
    [Fact]
    public void NegativeZeroKeepsItsSignBit()
    {
        foreach (var option in new[] { s_strictUtf8, s_strictUtf16 })
        {
            Assert.Equal(0x8000_0000_0000_0000UL, System.BitConverter.DoubleToUInt64Bits(LuminPackSerializer.DeserializeJson<double>("-0.0", option)));
            Assert.Equal(0x8000_0000_0000_0000UL, System.BitConverter.DoubleToUInt64Bits(LuminPackSerializer.DeserializeJson<double>("-0", option)));
            Assert.Equal(0x8000_0000u, System.BitConverter.SingleToUInt32Bits(LuminPackSerializer.DeserializeJson<float>("-0.0", option)));
            Assert.Equal(0UL, System.BitConverter.DoubleToUInt64Bits(LuminPackSerializer.DeserializeJson<double>("0.0", option)));
        }
    }

    /// <summary>Verifies double conversion matches the reference parser bit for bit.</summary>
    [Fact]
    public void DoubleConversionMatchesTheReferenceParserBitForBit()
    {
        foreach (string literal in DoubleLiterals())
        {
            double expected = double.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture);

            if (double.IsInfinity(expected))
            {
                // Overflow keeps LuminPack's documented policy: reject instead of producing Infinity.
                Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<double>(literal, s_strictUtf8));
                Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<double>(literal, s_strictUtf16));
                continue;
            }

            double utf8 = LuminPackSerializer.DeserializeJson<double>(literal, s_strictUtf8);
            double utf16 = LuminPackSerializer.DeserializeJson<double>(literal, s_strictUtf16);

            Assert.Equal(System.BitConverter.DoubleToUInt64Bits(expected), System.BitConverter.DoubleToUInt64Bits(utf8));
            Assert.Equal(System.BitConverter.DoubleToUInt64Bits(expected), System.BitConverter.DoubleToUInt64Bits(utf16));
        }
    }

    /// <summary>Verifies single conversion matches the reference parser bit for bit.</summary>
    [Fact]
    public void SingleConversionMatchesTheReferenceParserBitForBit()
    {
        foreach (string literal in SingleLiterals())
        {
            float expected = float.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture);

            if (float.IsInfinity(expected))
            {
                Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<float>(literal, s_strictUtf8));
                Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<float>(literal, s_strictUtf16));
                continue;
            }

            float utf8 = LuminPackSerializer.DeserializeJson<float>(literal, s_strictUtf8);
            float utf16 = LuminPackSerializer.DeserializeJson<float>(literal, s_strictUtf16);

            Assert.Equal(System.BitConverter.SingleToUInt32Bits(expected), System.BitConverter.SingleToUInt32Bits(utf8));
            Assert.Equal(System.BitConverter.SingleToUInt32Bits(expected), System.BitConverter.SingleToUInt32Bits(utf16));
        }
    }

    /// <summary>Verifies random double literals match the reference parser.</summary>
    [Fact]
    public void RandomDoubleLiteralsMatchTheReferenceParser()
    {
        var rng = new System.Random(20261010);

        for (int i = 0; i < 20_000; i++)
        {
            string literal = RandomLiteral(rng, 17);
            double expected = double.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture);
            double actual = LuminPackSerializer.DeserializeJson<double>(literal, s_strictUtf8);

            Assert.Equal(System.BitConverter.DoubleToUInt64Bits(expected), System.BitConverter.DoubleToUInt64Bits(actual));
        }
    }

    /// <summary>Verifies random single literals match the reference parser.</summary>
    [Fact]
    public void RandomSingleLiteralsMatchTheReferenceParser()
    {
        var rng = new System.Random(20261011);

        for (int i = 0; i < 20_000; i++)
        {
            string literal = RandomLiteral(rng, 9);
            float expected = float.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture);

            if (float.IsInfinity(expected))
            {
                Assert.ThrowsAny<System.Exception>(() => LuminPackSerializer.DeserializeJson<float>(literal, s_strictUtf8));
                continue;
            }

            float actual = LuminPackSerializer.DeserializeJson<float>(literal, s_strictUtf8);

            Assert.Equal(System.BitConverter.SingleToUInt32Bits(expected), System.BitConverter.SingleToUInt32Bits(actual));
        }
    }

    /// <summary>Verifies integer boundaries round trip.</summary>
    [Fact]
    public void IntegerBoundariesRoundTrip()
    {
        foreach (string literal in UnsignedLiterals)
        {
            Assert.Equal(literal, LuminPackSerializer.SerializeJson(LuminPackSerializer.DeserializeJson<ulong>(literal, s_strictUtf8)));
            Assert.Equal(literal, LuminPackSerializer.SerializeJson(LuminPackSerializer.DeserializeJson<ulong>(literal, s_strictUtf16)));
        }

        foreach (string literal in SignedLiterals)
        {
            Assert.Equal(
                long.Parse(literal, CultureInfo.InvariantCulture),
                LuminPackSerializer.DeserializeJson<long>(literal, s_strictUtf8));
            Assert.Equal(
                long.Parse(literal, CultureInfo.InvariantCulture),
                LuminPackSerializer.DeserializeJson<long>(literal, s_strictUtf16));
        }
    }

    /// <summary>Verifies random literal.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="maxDigits">The max digits used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string RandomLiteral(System.Random rng, int maxDigits)
    {
        int digits = 1 + rng.Next(maxDigits);
        var chars = new char[digits];
        chars[0] = (char)('1' + rng.Next(9));
        for (int i = 1; i < digits; i++)
            chars[i] = (char)('0' + rng.Next(10));

        string mantissa = new string(chars);
        int exponent = rng.Next(60) - 30;
        return mantissa + "e" + exponent.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Verifies double literals.</summary>
    /// <returns>The result of the operation.</returns>
    private static string[] DoubleLiterals() => new[]
    {
        "0", "-0", "1", "-1",
        "0.1", "-0.1", "0.3", "0.30000000000000004", "0.7", "1.83749023360500067",
        "2.2250738585072014e-308", "2.225073858507201e-308", "5e-324", "4.9e-324",
        "1.7976931348623157e308", "1.7976931348623158e308", "1.7976931348623159e308",
        "9007199254740991", "9007199254740992", "9007199254740993", "9007199254740994",
        "4503599627370496", "4503599627370497", "1e15", "1e16", "1e17", "1e22", "1e23",
        "123456789.123456789", "1234567890123456789", "-1234567890123456789",
        "1e-300", "1e300", "-1e300", "2.5e-10", "7.8e+15",
        "0.000001", "0.0000001", "1e-7", "1e-8", "1e-15", "1e-16",
        "3.141592653589793238462643383279", "2.718281828459045235360287471352",
        "0.333333333333333314829616256247", "1.0000000000000002", "0.9999999999999999",
        "1e-320", "1e-310", "1.23e-45", "4.9406564584124654e-324"
    };

    /// <summary>Verifies single literals.</summary>
    /// <returns>The result of the operation.</returns>
    private static string[] SingleLiterals() => new[]
    {
        "0", "-0", "1", "-1",
        "0.1", "-0.1", "0.3", "0.7", "16777215", "16777216", "16777217", "16777218",
        "8388608", "8388609", "3.4028235e38", "3.4028236e38", "1.17549435e-38", "1.1754943e-38",
        "1e-45", "1.4e-45", "7e-46", "1e-44", "3.1415927", "3.14159265", "2.7182818",
        "123456.78", "1234567.8", "12345678", "123456789", "0.016666668", "1e10", "1e11",
        "33554432", "16777219", "1.9999999", "2.0000001"
    };
}
