using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks exact signed BigInteger encoding beyond floating-point and stack-buffer limits.</summary>
public class BigIntegerPrecisionTest
{
    /// <summary>Checks warmed writes allocate nothing and reads allocate only the BCL integer result storage.</summary>
    [Fact]
    public void HotPathsAvoidTemporaryManagedAllocations()
    {
        foreach (int bits in new[] { 0, 64, 256, 8192 })
        {
            BigInteger value = bits == 0 ? BigInteger.Zero : -(BigInteger.One << bits) + 1;
            string digits = value.ToString(CultureInfo.InvariantCulture);
            var output = new LuminBufferWriter(true);
            CheckBinaryAllocations(value, output);
            CheckAllocations<LuminPackUtf8LengthOptions>(value, digits, Encoding.UTF8, output);
            CheckAllocations<LuminPackUtf16LengthOptions>(value, digits, Encoding.Unicode, output);
        }
    }

    /// <summary>Exercises arbitrary limb carries and stack/pool transitions with deterministic magnitudes.</summary>
    [Fact]
    public void RandomizedMagnitudesKeepExactDecimalDigits()
    {
        var random = new Random(1337);
        foreach (int length in new[] { 1, 4, 5, 8, 9, 31, 32, 33, 100, 101, 102, 103, 255, 256, 257, 1023, 1024, 1025 })
        {
            var bytes = new byte[length];
            random.NextBytes(bytes);
            var magnitude = new BigInteger(bytes);
            foreach (var value in new[] { magnitude, -magnitude })
            {
                string expected = "\"" + value.ToString(CultureInfo.InvariantCulture) + "\"";
                Assert.Equal(expected, LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(value));
                Assert.Equal(expected, LuminPackSerializer.SerializeJson<LuminPackUtf16LengthOptions>(value));
                BigInteger target = default;
                LuminPackSerializer.DeserializeJson<LuminPackUtf8LengthOptions>(Encoding.UTF8.GetBytes(expected), ref target);
                Assert.Equal(value, target);
                LuminPackSerializer.DeserializeJson<LuminPackUtf16LengthOptions>(Encoding.Unicode.GetBytes(expected), ref target);
                Assert.Equal(value, target);
            }
        }
    }

    /// <summary>Checks binary writes avoid staging arrays and reads allocate only final magnitude storage.</summary>
    /// <param name="value">The exact integer.</param>
    /// <param name="output">The reusable output buffer.</param>
    private static void CheckBinaryAllocations(BigInteger value, LuminBufferWriter output)
    {
        byte[] input = LuminPackSerializer.Serialize(value);
        byte[] magnitude = BigInteger.Abs(value).ToByteArray(isUnsigned: true);
        BigInteger target = default;
        for (int i = 0; i < 64; i++)
        {
            output.ResetCore(); LuminPackSerializer.Serialize(value, output);
            LuminPackSerializer.Deserialize(input, ref target);
            target = NecessaryMagnitude(value, magnitude);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++)
        {
            output.ResetCore(); LuminPackSerializer.Serialize(value, output);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++) target = NecessaryMagnitude(value, magnitude);
        long necessary = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++) LuminPackSerializer.Deserialize(input, ref target);
        Assert.Equal(necessary, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(value, target);
    }

    /// <summary>Constructs final result storage without signed-constructor trimming buffers.</summary>
    /// <param name="value">The expected signed value.</param>
    /// <param name="magnitude">Its minimal unsigned bytes, prepared outside the measurement.</param>
    /// <returns>An integer with the same necessary storage size.</returns>
    private static BigInteger NecessaryMagnitude(BigInteger value, byte[] magnitude) =>
        value >= int.MinValue && value <= int.MaxValue ? new BigInteger((int)value) : new BigInteger(magnitude, isUnsigned: true);

    /// <summary>Checks every small signed width, redundant sign extension, carry chains and stack/pool boundaries.</summary>
    [Fact]
    public void BinarySignedMagnitudesPreserveCanonicalAndExtendedPayloads()
    {
        var random = new Random(6104);
        foreach (int length in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 13, 16, 17, 31, 32, 33, 63, 64, 65, 127, 128, 129, 255, 256, 257, 511, 512, 513, 1023, 1024, 1025, 4097 })
        {
            byte[] bytes = new byte[length];
            for (int sample = 0; sample < 8; sample++)
            {
                random.NextBytes(bytes);
                CheckBinaryPayload(bytes);
                if (length != 0) { bytes[length - 1] = 255; CheckBinaryPayload(bytes); }
            }
        }
        var output = new LuminBufferWriter(true);
        foreach (int bit in new[] { 0, 31, 32, 63, 64, 95, 96, 127, 128, 255, 256, 2047, 2048, 8192, 32768 })
        {
            BigInteger power = BigInteger.One << bit;
            foreach (BigInteger value in new[] { -power - 1, -power, -power + 1, power - 1, power })
            {
                CheckBinaryAllocations(value, output);
                byte[] canonical = value.ToByteArray();
                foreach (int padding in new[] { 0, 1, 4, 17 })
                {
                    byte[] extended = new byte[canonical.Length + padding];
                    if (value.Sign < 0) extended.AsSpan().Fill(255);
                    canonical.CopyTo(extended, 0);
                    CheckBinaryPayload(extended);
                }
            }
        }
    }

    /// <summary>Compares generated binary reads with the public signed byte constructor.</summary>
    /// <param name="payload">An arbitrary signed little-endian payload, including nonminimal forms.</param>
    private static void CheckBinaryPayload(byte[] payload)
    {
        byte[] record = new byte[payload.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(record, payload.Length);
        payload.CopyTo(record, 4);
        BigInteger target = 17;
        LuminPackSerializer.Deserialize(record, ref target);
        Assert.Equal(new BigInteger(payload), target);
    }

    /// <summary>Measures retained-buffer writes and compares reads with necessary magnitude construction after pool warmup.</summary>
    /// <param name="value">The exact integer.</param>
    /// <param name="digits">The invariant BCL representation.</param>
    /// <param name="encoding">The document encoding.</param>
    /// <param name="output">The reusable output buffer.</param>
    /// <typeparam name="TOptions">The compiled document profile.</typeparam>
    private static void CheckAllocations<TOptions>(BigInteger value, string digits, Encoding encoding, LuminBufferWriter output)
        where TOptions : struct, ILuminPackOptions
    {
        byte[] input = encoding.GetBytes("\"" + digits + "\"");
        byte[] magnitude = BigInteger.Abs(value).ToByteArray(isUnsigned: true);
        var state = new LuminPackReaderOptionalState();
        BigInteger target = default;
        for (int i = 0; i < 64; i++)
        {
            output.ResetCore();
            LuminPackSerializer.SerializeJson<TOptions>(value, output);
            LuminPackSerializer.DeserializeJson<TOptions>(input, ref target);
            target = ReadDirect<TOptions>(input, state);
            target = new BigInteger(magnitude, isUnsigned: true);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++)
        {
            output.ResetCore();
            LuminPackSerializer.SerializeJson<TOptions>(value, output);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++) target = new BigInteger(magnitude, isUnsigned: true);
        long necessary = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++) LuminPackSerializer.DeserializeJson<TOptions>(input, ref target);
        Assert.Equal(necessary, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(value, target);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++) target = ReadDirect<TOptions>(input, state);
        Assert.Equal(necessary, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(value, target);
    }

    /// <summary>Exercises complete token selection in the low-level API with input pinned for portable readers.</summary>
    /// <param name="input">The complete encoded token.</param>
    /// <param name="state">The caller-owned state.</param>
    /// <typeparam name="TOptions">The compiled document profile.</typeparam>
    /// <returns>The parsed integer.</returns>
    private static unsafe BigInteger ReadDirect<TOptions>(byte[] input, LuminPackReaderOptionalState state)
        where TOptions : struct, ILuminPackOptions
    {
        fixed (byte* pointer = input)
        {
            ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(pointer, input.Length);
            var reader = new LuminPackJsonReader(ref data, state);
            return reader.ReadBigInteger<TOptions>();
        }
    }

    /// <summary>Checks both JSON encodings at signed limb, decimal and temporary-buffer boundaries.</summary>
    [Fact]
    public void JsonEncodingsPreserveDigitsAndParseFailures()
    {
        CheckJson<LuminPackUtf8LengthOptions>(Encoding.UTF8);
        CheckJson<LuminPackUtf16LengthOptions>(Encoding.Unicode);
    }

    /// <summary>Compares generated JSON behavior with the original invariant BCL parser.</summary>
    /// <param name="encoding">The document encoding.</param>
    /// <typeparam name="TOptions">The compiled document profile.</typeparam>
    private static void CheckJson<TOptions>(Encoding encoding) where TOptions : struct, ILuminPackOptions
    {
        foreach (int bit in new[] { 0, 31, 32, 63, 64, 127, 128, 807, 808, 809, 847, 848, 849, 4096, 8192 })
        {
            BigInteger power = BigInteger.One << bit;
            foreach (BigInteger value in new[] { power - 1, power, power + 1, -power - 1, -power, -power + 1 })
            {
                string json = LuminPackSerializer.SerializeJson<TOptions>(value);
                Assert.Equal("\"" + value.ToString(CultureInfo.InvariantCulture) + "\"", json);
                BigInteger target = default;
                LuminPackSerializer.DeserializeJson<TOptions>(encoding.GetBytes(json), ref target);
                Assert.Equal(value, target);
            }
        }
        foreach (string? text in new[] { null, "", " ", "0", "-0", "+0", "  +17 ", "17\0", "17\0\0", "1\0x", "1.0", "1e2", "1,000", "NaN", "\u0661", "\u00a017", "\u22121", "17\u0085", "+\t17", " \t+00017\r\n\0\0", "17\0 ", "\0", "  -2147483648  ", "+9223372036854775808", "-18446744073709551616", new string('9', 575), new string('9', 576), new string('9', 577), new string('9', 3000), new string('9', 20000), new string('9', 20001) })
        {
            BigInteger expected = default;
            Exception? failure = Record.Exception(() => expected = BigInteger.Parse(text!, CultureInfo.InvariantCulture));
            string json = LuminPackSerializer.SerializeJson<TOptions>(text!);
            var target = (BigInteger.One << 256) + 17;
            var previous = target;
            Exception? actual = Record.Exception(() => LuminPackSerializer.DeserializeJson<TOptions>(encoding.GetBytes(json), ref target));
            Assert.Equal(failure?.GetType(), actual?.GetType());
            Assert.Equal((failure as ArgumentException)?.ParamName, (actual as ArgumentException)?.ParamName);
            Assert.Equal(failure is null ? expected : previous, target);
        }
        foreach (string json in new[] { "123", "[]", "\"17", "\"1\\u12\"" })
        {
            var target = (BigInteger.One << 256) + 17;
            var previous = target;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<TOptions>(encoding.GetBytes(json), ref target));
            Assert.Equal(previous, target);
        }
    }

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
                Assert.Equal(value, global::LuminPackUnitTest.ConcreteReads.Read<BigInteger>(record, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref BigInteger concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)));

                string json = LuminPackSerializer.SerializeJson(value);
                Assert.Equal("\"" + value.ToString(CultureInfo.InvariantCulture) + "\"", json);
                Assert.Equal(value, global::LuminPackUnitTest.ConcreteReads.ReadJson<BigInteger>(json, static (string concreteReadInput, ref BigInteger concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
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
            Assert.Equal(value, global::LuminPackUnitTest.ConcreteReads.ReadJson<BigInteger>(expected, static (string concreteReadInput, ref BigInteger concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
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
