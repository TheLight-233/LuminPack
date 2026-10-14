using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

public class JsonSimpleDecimalTest
{
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, false)]
    [InlineData(LuminPackStringEncoding.UTF8, true)]
    [InlineData(LuminPackStringEncoding.UTF16, false)]
    [InlineData(LuminPackStringEncoding.UTF16, true)]
    public void DecimalFastPathAndFallbackAgreeBitForBitWithSystemTextJson(LuminPackStringEncoding encoding, bool strict)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding, ValidateJsonSyntax = strict };
        var literals = new List<string>
        {
            "0", "-0", "-0.0", "0.000000000000000000000000", "-0.000000000000000000000000",
            "16777215.0", "16777216.0", "16777217.0", "1.6777215", "1.6777216", "1.6777217",
            "9007199254740991", "9007199254740992", "9007199254740993",
            "900719925474099.1", "900719925474099.2", "900719925474099.3",
            "0.0000000001", "0.00000000001", "0.0000000000000000000001", "0.00000000000000000000001",
            "9999999999999999999", "18446744073709551615", "100000000000000000000",
            "1.17549435E-38", "1.401298464324817E-45", "2.2250738585072014E-308", "5E-324",
            "1.000000059604644775390625", "1.000000059604644775390626", "-1.000000059604644775390626"
        };
        var rng = new Random(20261011);
        for (int i = 0; i < 2000; i++)
        {
            long mantissa = rng.NextInt64(1, 9_000_000_000_000_000);
            int places = rng.Next(0, 23);
            string digits = mantissa.ToString(CultureInfo.InvariantCulture).PadLeft(places + 1, '0');
            string literal = places == 0 ? digits : digits.Insert(digits.Length - places, ".");
            literals.Add(i % 2 == 0 ? literal : "-" + literal);
        }

        foreach (string literal in literals)
        {
            using var doc = JsonDocument.Parse(literal);
            double expectedDouble = doc.RootElement.GetDouble();
            float expectedFloat = doc.RootElement.GetSingle();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expectedDouble),
                BitConverter.DoubleToInt64Bits(LuminPackSerializer.DeserializeJson<double>(literal, option)));
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedFloat),
                BitConverter.SingleToInt32Bits(LuminPackSerializer.DeserializeJson<float>(literal, option)));
        }
    }
}
