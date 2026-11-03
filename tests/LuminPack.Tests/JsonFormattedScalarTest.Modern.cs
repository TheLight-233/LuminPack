#if NET8_0_OR_GREATER
using System.Globalization;
using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

public partial class JsonFormattedScalarTest
{
    /// <summary>Checks bounded scalar formatting against its original text in both encodings and cultures.</summary>
    [Fact]
    public void FormattedScalarsKeepFormatsAndOutputBounds()
    {
        var original = CultureInfo.CurrentCulture;
        var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        custom.NumberFormat.NegativeSign = "\"negative\"";
        try
        {
            foreach (var culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("ar-SA"), custom })
            {
                CultureInfo.CurrentCulture = culture;
                foreach (var value in DateTimes)
                    CheckModernValue(value, value.ToString("O"),
                        static (ref LuminPackJsonWriter writer, DateTime item) => writer.Write<LuminPackUtf8LengthOptions>(item),
                        static (ref LuminPackJsonWriter writer, DateTime item) => writer.Write<LuminPackUtf16LengthOptions>(item));
                foreach (var value in DateTimeOffsets)
                    CheckModernValue(value, value.ToString("O"),
                        static (ref LuminPackJsonWriter writer, DateTimeOffset item) => writer.Write<LuminPackUtf8LengthOptions>(item),
                        static (ref LuminPackJsonWriter writer, DateTimeOffset item) => writer.Write<LuminPackUtf16LengthOptions>(item));
                foreach (var value in Guids)
                    CheckModernValue(value, value.ToString(),
                        static (ref LuminPackJsonWriter writer, Guid item) => writer.Write<LuminPackUtf8LengthOptions>(item),
                        static (ref LuminPackJsonWriter writer, Guid item) => writer.Write<LuminPackUtf16LengthOptions>(item));
                foreach (var value in TimeSpans)
                    CheckModernValue(value, value.ToString("c"),
                        static (ref LuminPackJsonWriter writer, TimeSpan item) => writer.Write<LuminPackUtf8LengthOptions>(item),
                        static (ref LuminPackJsonWriter writer, TimeSpan item) => writer.Write<LuminPackUtf16LengthOptions>(item));
                foreach (var value in new[] { DateOnly.MinValue, DateOnly.MaxValue, new DateOnly(2024, 2, 29) })
                    CheckModernValue(value, value.ToString("O"),
                        static (ref LuminPackJsonWriter writer, DateOnly item) => writer.Write<LuminPackUtf8LengthOptions>(item),
                        static (ref LuminPackJsonWriter writer, DateOnly item) => writer.Write<LuminPackUtf16LengthOptions>(item));
                foreach (var value in new[] { TimeOnly.MinValue, TimeOnly.MaxValue, new TimeOnly(12, 34, 56).Add(TimeSpan.FromTicks(1234567)) })
                    CheckModernValue(value, value.ToString("O"),
                        static (ref LuminPackJsonWriter writer, TimeOnly item) => writer.Write<LuminPackUtf8LengthOptions>(item),
                        static (ref LuminPackJsonWriter writer, TimeOnly item) => writer.Write<LuminPackUtf16LengthOptions>(item));
                foreach (var value in new[] { Int128.MinValue, Int128.MaxValue, Int128.Zero, -Int128.One, Int128.One, (Int128)long.MinValue - 1, (Int128)ulong.MaxValue + 1 })
                    CheckModernValue(value, value.ToString(CultureInfo.InvariantCulture),
                        static (ref LuminPackJsonWriter writer, Int128 item) => writer.Write<LuminPackUtf8LengthOptions>(item),
                        static (ref LuminPackJsonWriter writer, Int128 item) => writer.Write<LuminPackUtf16LengthOptions>(item));
                foreach (var value in new[] { UInt128.MinValue, UInt128.MaxValue, UInt128.One, (UInt128)ulong.MaxValue + 1 })
                    CheckModernValue(value, value.ToString(CultureInfo.InvariantCulture),
                        static (ref LuminPackJsonWriter writer, UInt128 item) => writer.Write<LuminPackUtf8LengthOptions>(item),
                        static (ref LuminPackJsonWriter writer, UInt128 item) => writer.Write<LuminPackUtf16LengthOptions>(item));
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    /// <summary>Checks exact output, short-buffer rejection and complete destination canaries.</summary>
    /// <param name="value">The scalar to format.</param>
    /// <param name="text">The original unquoted text.</param>
    /// <param name="utf8">The UTF-8 write operation.</param>
    /// <param name="utf16">The UTF-16 write operation.</param>
    /// <typeparam name="T">The scalar type.</typeparam>
    private static void CheckModernValue<T>(T value, string text, WriteScalar<T> utf8, WriteScalar<T> utf16)
    {
        foreach (bool narrow in new[] { true, false })
        {
            byte[] expected = (narrow ? Encoding.UTF8 : Encoding.Unicode).GetBytes("\"" + text + "\"");
            foreach (int capacity in new[] { 0, expected.Length - 1, expected.Length })
            {
                byte[] bytes = Enumerable.Repeat((byte)0xCC, capacity + 16).ToArray();
                Span<byte> output = bytes.AsSpan(8, capacity);
                var writer = new LuminPackJsonWriter(ref output, new LuminPackWriterOptionalState());
                bool failed = false;
                try { (narrow ? utf8 : utf16)(ref writer, value); }
                catch (Exception) { failed = true; }
                Assert.Equal(capacity < expected.Length, failed);
                if (!failed) Assert.True(output.SequenceEqual(expected));
                Assert.All(bytes.Take(8).Concat(bytes.Skip(8 + capacity)), item => Assert.Equal((byte)0xCC, item));
            }
        }
    }

    /// <summary>Checks the generated concrete scalar roots after their runtime formatting capability has been selected.</summary>
    [Fact]
    public void ModernScalarSerializationHasNoIntermediateAllocations()
    {
        using var buffer = new LuminBufferWriter(useFirstBuffer: true);
        for (int i = 0; i < 2048; i++) WriteModernScalars(buffer);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4096; i++) WriteModernScalars(buffer);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    /// <summary>Writes modern concrete scalar roots into retained storage.</summary>
    /// <param name="buffer">The reusable output buffer.</param>
    private static void WriteModernScalars(LuminBufferWriter buffer)
    {
        LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(DateOnly.MaxValue, buffer);
        LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(TimeOnly.MaxValue, buffer);
        LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(Int128.MinValue, buffer);
        LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(UInt128.MaxValue, buffer);
        buffer.ResetCore();
    }
}
#endif
