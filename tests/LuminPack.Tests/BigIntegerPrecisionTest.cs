using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks exact signed BigInteger encoding beyond floating-point and stack-buffer limits.</summary>
public class BigIntegerPrecisionTest
{
    /// <summary>Checks full two's-complement bytes and quoted decimal digits on both sides of sign and buffer boundaries.</summary>
    [Fact]
    public void LargeSignedValuesKeepEveryBit()
    {
        foreach (int bit in new[] { 0, 7, 8, 31, 32, 53, 63, 64, 127, 128, 2038, 2039, 2040, 2041, 4096, 8192 })
        {
            BigInteger power = BigInteger.One << bit;
            foreach (BigInteger value in new[] { power - 1, power, power + 1, -power - 1, -power, -power + 1 })
            {
                byte[] payload = value.ToByteArray();
                byte[] record = LuminPackSerializer.Serialize(value);
                Assert.Equal(payload.Length, BinaryPrimitives.ReadInt32LittleEndian(record));
                Assert.Equal(payload, record.AsSpan(4).ToArray());
                Assert.Equal(value, LuminPackSerializer.Deserialize<BigInteger>(record));

                string json = LuminPackSerializer.SerializeJson(value);
                Assert.Equal("\"" + value.ToString(CultureInfo.InvariantCulture) + "\"", json);
                Assert.Equal(value, LuminPackSerializer.DeserializeJson<BigInteger>(json));
            }
        }
    }

    /// <summary>Checks a custom culture's sign cannot alter the decimal string wire or its interpretation.</summary>
    [Fact]
    public void JsonDigitsAreIndependentOfTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        custom.NumberFormat.NegativeSign = "~";
        var value = -(BigInteger.One << 4096) - 17;
        string expected = "\"" + value.ToString(CultureInfo.InvariantCulture) + "\"";
        try
        {
            CultureInfo.CurrentCulture = custom;
            Assert.Equal(expected, LuminPackSerializer.SerializeJson(value));
            Assert.Equal(value, LuminPackSerializer.DeserializeJson<BigInteger>(expected));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>Checks a declared magnitude cannot make the BigInteger constructor read beyond the complete input.</summary>
    [Fact]
    public void TruncatedMagnitudeIsRejectedBeforeConstruction()
    {
        foreach (int declared in new[] { 1, 255, 256, 4096, int.MaxValue })
        {
            byte[] record = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(record, declared);
            var existing = (BigInteger.One << 128) + 123;
            var original = existing;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(record, ref existing));
            Assert.Equal(original, existing);
        }
    }
}
