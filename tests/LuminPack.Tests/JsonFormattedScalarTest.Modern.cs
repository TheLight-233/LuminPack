#if NET8_0_OR_GREATER
using System.Globalization;
using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;
using StrictUtf8 = LuminPack.Option.LuminPackOptions<LuminPack.Option.LuminPackUtf8LengthOptions, LuminPack.Option.LuminPackGeneralFormat, LuminPack.Option.LuminPackDefaultDepth, LuminPack.Option.LuminPackStrictJson>;
using StrictUtf16 = LuminPack.Option.LuminPackOptions<LuminPack.Option.LuminPackUtf16LengthOptions, LuminPack.Option.LuminPackGeneralFormat, LuminPack.Option.LuminPackDefaultDepth, LuminPack.Option.LuminPackStrictJson>;

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

    /// <summary>Checks the calendar limits, escaped date tokens and failed reads through both document encodings.</summary>
    [Fact]
    public void DateOnlyReadsKeepInvariantCalendarAndFailedTargets()
    {
        foreach (var (token, expected) in new[]
        {
            ("\"0001-01-01\"", DateOnly.MinValue),
            ("\"1900-02-28\"", new DateOnly(1900, 2, 28)),
            ("\"\\u0032000-02-29\"", new DateOnly(2000, 2, 29)),
            ("\"9999-12-31\"", DateOnly.MaxValue)
        })
        {
            Assert.Equal(expected, LuminPackSerializer.DeserializeJson<DateOnly, StrictUtf8>(Encoding.UTF8.GetBytes(token)));
            Assert.Equal(expected, LuminPackSerializer.DeserializeJson<DateOnly, StrictUtf16>(Encoding.Unicode.GetBytes(token)));
        }

        foreach (string token in new[] { "\"0000-01-01\"", "\"1900-02-29\"", "\"2000-02-30\"", "\"2026-04-31\"", "\"2026-13-01\"", "\"2026/03/02\"" })
        {
            var original = new DateOnly(2000, 2, 29);
            var target = original;
            Assert.Throws<FormatException>(() => LuminPackSerializer.DeserializeJson<DateOnly, StrictUtf8>(Encoding.UTF8.GetBytes(token), ref target));
            Assert.Equal(original, target);
            Assert.Throws<FormatException>(() => LuminPackSerializer.DeserializeJson<DateOnly, StrictUtf16>(Encoding.Unicode.GetBytes(token), ref target));
            Assert.Equal(original, target);
        }
    }

    /// <summary>Checks timestamp kind, explicit offset and failure through the invariant UTF-16 roots.</summary>
    [Fact]
    public void CanonicalTimestampReadsKeepKindOffsetAndFailedTargets()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var utc = new DateTime(2024, 2, 29, 23, 59, 58, DateTimeKind.Utc).AddTicks(1234567);
            var utcInput = Encoding.Unicode.GetBytes("\"2024-02-29T23:59:58.1234567Z\"");
            var readUtc = LuminPackSerializer.DeserializeJson<DateTime, LuminPackUtf16LengthOptions>(utcInput);
            Assert.Equal(utc.ToBinary(), readUtc.ToBinary());

            var offset = new DateTimeOffset(utc.Ticks, TimeSpan.FromMinutes(-510));
            var offsetInput = Encoding.Unicode.GetBytes("\"2024-02-29T23:59:58.1234567-08:30\"");
            var readOffset = LuminPackSerializer.DeserializeJson<DateTimeOffset, LuminPackUtf16LengthOptions>(offsetInput);
            Assert.Equal(offset.Ticks, readOffset.Ticks);
            Assert.Equal(offset.Offset, readOffset.Offset);

            foreach (string invalid in new[] { "\"2024-02-29T23:59:58.1234567+14:01\"", "\"0001-01-01T00:00:00.0000000+00:01\"" })
            {
                var target = offset;
                var malformed = Encoding.Unicode.GetBytes(invalid);
                Assert.Throws<FormatException>(() =>
                    LuminPackSerializer.DeserializeJson<DateTimeOffset, LuminPackUtf16LengthOptions>(malformed, ref target));
                Assert.Equal(offset.Ticks, target.Ticks);
                Assert.Equal(offset.Offset, target.Offset);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Checks that strict JSON scalar reads avoid allocating a string for each quoted token.</summary>
    [Fact]
    public void StrictQuotedScalarReadsDoNotAllocateTokenStrings()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Check(new TimeOnly(12, 34, 56).Add(TimeSpan.FromTicks(1234567)), "12:34:56.1234567");
            Check(new TimeSpan(2, 3, 4, 5).Add(TimeSpan.FromTicks(6)).Negate(), "-2.03:04:05.0000006");
            Check(new Guid("35a8c7d1-bd3a-48b6-b043-432096f520ff"), "35a8c7d1-bd3a-48b6-b043-432096f520ff");
            Check(Int128.MinValue, Int128.MinValue.ToString(CultureInfo.InvariantCulture));
            Check(UInt128.MaxValue, UInt128.MaxValue.ToString(CultureInfo.InvariantCulture));
        }
        finally { CultureInfo.CurrentCulture = previous; }

        static void Check<T>(T expected, string text) where T : struct
        {
            string token = "\"" + text + "\"";
            Verify<StrictUtf8>(Encoding.UTF8.GetBytes(token));
            Verify<LuminPackFixedOptions<StrictUtf16>>(Encoding.Unicode.GetBytes(token));

            void Verify<TOptions>(byte[] input) where TOptions : struct, ILuminPackOptions
            {
                var state = new LuminPackReaderOptionalState();
                for (int i = 0; i < 64; i++) Read(input, state);
                T result = default;
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 256; i++) result = Read(input, state);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(expected, result);
                Assert.Equal(0, allocated);

                static T Read(ReadOnlySpan<byte> input, LuminPackReaderOptionalState state)
                {
                    var reader = new LuminPackJsonReader(ref input, state);
                    if (!reader.Read<TOptions>()) throw new InvalidOperationException("Missing scalar token.");
                    return reader.ReadQuotedScalar<T, TOptions>();
                }
            }
        }
    }

    /// <summary>Writes modern concrete scalar roots into retained storage.</summary>
    /// <param name="buffer">The reusable output buffer.</param>
    private static void WriteModernScalars(LuminBufferWriter buffer)
    {
        LuminPackSerializer.SerializeJson<DateOnly, LuminPackUtf8LengthOptions>(DateOnly.MaxValue, buffer);
        LuminPackSerializer.SerializeJson<TimeOnly, LuminPackUtf8LengthOptions>(TimeOnly.MaxValue, buffer);
        LuminPackSerializer.SerializeJson<Int128, LuminPackUtf8LengthOptions>(Int128.MinValue, buffer);
        LuminPackSerializer.SerializeJson<UInt128, LuminPackUtf8LengthOptions>(UInt128.MaxValue, buffer);
        buffer.ResetCore();
    }
}
#endif
