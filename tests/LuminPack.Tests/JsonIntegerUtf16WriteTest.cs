using System.Globalization;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;
namespace LuminPackUnitTest;

/// <summary>Checks integer digit boundaries and fused UTF-16 output against scalar operations.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed unsafe class JsonIntegerUtf16WriteTest
{
    /// <summary>Checks the complete quad path and shorter checked spans around every decimal boundary.</summary>
    [Fact]
    public void Utf8IntegerDigitBoundariesMatchInvariantFormatting()
    {
        var values = new List<uint> { 0, 1, 9, 10, uint.MaxValue, int.MaxValue, 0x80000000 };
        for (ulong power = 10; power <= uint.MaxValue; power *= 10)
        {
            values.Add((uint)power - 1);
            values.Add((uint)power);
            values.Add((uint)power + 1);
        }
        var random = new Random(71603);
        for (int i = 0; i < 2048; i++)
            values.Add((uint)random.NextInt64(0, (long)uint.MaxValue + 1));

        byte[] buffer = new byte[32];
        foreach (uint value in values)
        foreach (bool signed in new[] { false, true })
        {
            string expected = signed
                ? unchecked((int)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
            // Small packed values may write four digits inside the reserved window.
            for (int length = Math.Max(signed ? 5 : 4, expected.Length); length <= 13; length++)
            {
                Array.Fill(buffer, (byte)0xCC);
                Span<byte> destination = buffer.AsSpan(3, length);
                int written = signed
                    ? LuminPackJsonNumberFormatter.WriteInt32(destination, unchecked((int)value))
                    : LuminPackJsonNumberFormatter.WriteUInt32(destination, value);
                Assert.Equal(expected.Length, written);
                Assert.Equal(expected, System.Text.Encoding.ASCII.GetString(destination[..written]));
                for (int i = 0; i < 3; i++)
                    Assert.Equal(0xCC, buffer[i]);
                for (int i = length + 3; i < buffer.Length; i++)
                    Assert.Equal(0xCC, buffer[i]);
            }
        }
    }

    /// <summary>Checks output, partial failure, flags and depth with ordinary and extended negative signs.</summary>
    [Fact]
    public void BoundedOperationsPreserveScalarFormattingAndFailureState()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (string sign in new[] { "-", "\u2212", "negative", new string('n', 24) })
            {
                var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                culture.NumberFormat.NegativeSign = sign;
                CultureInfo.CurrentCulture = culture;
                for (int operation = 0; operation < 9; operation++)
                for (int length = 0; length < 96; length++)
                foreach (bool first in new[] { false, true })
                {
                    string expected = Execute(operation, length, first, false), actual = Execute(operation, length, first, true);
                    Assert.True(expected == actual, operation + "/" + length + "/" + first + "/" + sign + ": " + expected + " != " + actual);
                }
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    /// <summary>Records the observable writer boundary state for direct or scalar output.</summary>
    /// <param name="operation">The integer writing operation.</param><param name="length">Complete output window.</param>
    /// <param name="first">Initial separator state.</param><param name="fused">Whether to call the optimized operation.</param>
    /// <returns>The error, output and published writer state.</returns>
    private static string Execute(int operation, int length, bool first, bool fused)
    {
        byte[] bytes = new byte[length];
        fixed (byte* pin = bytes)
        {
            Span<byte> span = bytes;
            var writer = new LuminPackJsonWriter(ref span, null!);
            writer.SetFirstElement(first);
            writer.Depth = 5;
            string error = "OK";
            try
            {
                foreach (long number in new[] { 0L, -1, int.MinValue, int.MaxValue, long.MinValue, long.MaxValue })
                {
                    int key = (int)number;
                    if (fused)
                    {
                        switch (operation)
                        {
                            case 0: writer.WriteElement<LuminPackUtf16LengthOptions>(key, !first); break;
                            case 1: writer.WriteElement<LuminPackUtf16LengthOptions>(number, !first); break;
                            case 2: writer.WriteEnumerationElement<LuminPackUtf16LengthOptions>(key, !first); break;
                            case 3: writer.WriteEnumerationElement<LuminPackUtf16LengthOptions>(number, !first); break;
                            case 4: writer.WriteIntegerEntry<LuminPackUtf16LengthOptions>(key, key); break;
                            case 5: writer.WriteIntegerEntry<LuminPackUtf16LengthOptions>(key, number); break;
                            case 6: writer.WriteEntryKey<LuminPackUtf16LengthOptions>(key); break;
                            case 7: writer.WriteEntryValue<LuminPackUtf16LengthOptions>(key); break;
                            case 8: writer.WriteEntryValue<LuminPackUtf16LengthOptions>(number); break;
                        }
                    }
                    else if (operation < 4)
                    {
                        if (!first) writer.WriteByteRaw<LuminPackUtf16LengthOptions>((byte)',');
                        writer.SetFirstElement(true);
                        if ((operation & 1) == 0) writer.Write<LuminPackUtf16LengthOptions>(key);
                        else writer.Write<LuminPackUtf16LengthOptions>(number);
                    }
                    else if (operation < 6)
                    {
                        writer.WriteArrayStart<LuminPackUtf16LengthOptions>();
                        writer.Write<LuminPackUtf16LengthOptions>(key);
                        if (operation == 4) writer.Write<LuminPackUtf16LengthOptions>(key);
                        else writer.Write<LuminPackUtf16LengthOptions>(number);
                        writer.WriteArrayEnd<LuminPackUtf16LengthOptions>();
                    }
                    else if (operation == 6)
                    {
                        if (!writer.IsFirstElement) writer.WriteByteRaw<LuminPackUtf16LengthOptions>((byte)',');
                        writer.SetFirstElement(false);
                        writer.WriteByteRaw<LuminPackUtf16LengthOptions>((byte)'[');
                        writer.SetFirstElement(true);
                        writer.Write<LuminPackUtf16LengthOptions>(key);
                        writer.WriteByteRaw<LuminPackUtf16LengthOptions>((byte)',');
                        writer.SetFirstElement(true);
                    }
                    else
                    {
                        if (operation == 7) writer.Write<LuminPackUtf16LengthOptions>(key);
                        else writer.Write<LuminPackUtf16LengthOptions>(number);
                        writer.WriteByteRaw<LuminPackUtf16LengthOptions>((byte)']');
                        writer.SetFirstElement(false);
                    }
                }
            }
            catch (Exception failure) { error = failure.GetType().FullName!; }
            return error + "|" + writer.Position + "|" + writer.Depth + "|" + writer.IsFirstElement + "|" +
                Convert.ToBase64String(bytes.AsSpan(0, writer.Position));
        }
    }
}
