using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

// These scalars used to be written as value.ToString(...), allocating an
// intermediate string per value. They now format into a stack buffer, so the
// emitted JSON must stay byte-identical while the write path allocates nothing.
public class JsonFormattedScalarTest
{
    private static readonly DateTime[] DateTimes =
    {
        DateTime.MinValue,
        DateTime.MaxValue,
        new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
        new DateTime(9999, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc),
        new DateTime(2023, 11, 15, 6, 13, 20, DateTimeKind.Utc),
        new DateTime(2023, 11, 15, 6, 13, 20, 123, DateTimeKind.Local),
        new DateTime(2023, 11, 15, 6, 13, 20, DateTimeKind.Unspecified).AddTicks(4567),
        new DateTime(1969, 7, 20, 20, 17, 40, DateTimeKind.Utc),
    };

    private static readonly DateTimeOffset[] DateTimeOffsets =
    {
        DateTimeOffset.MinValue,
        DateTimeOffset.MaxValue,
        DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).ToOffset(TimeSpan.FromHours(8)),
        DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).ToOffset(TimeSpan.FromHours(14)),
        DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).ToOffset(TimeSpan.FromHours(-12)),
        DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).ToOffset(TimeSpan.FromMinutes(330)),
        DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).ToOffset(TimeSpan.Zero),
        DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).ToOffset(TimeSpan.FromHours(-8)).AddTicks(1234567),
    };

    private static readonly Guid[] Guids =
    {
        Guid.Empty,
        Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
        Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
        Guid.Parse("00000000-0000-0000-0000-000000000001"),
    };

    private static readonly TimeSpan[] TimeSpans =
    {
        TimeSpan.MinValue,
        TimeSpan.MaxValue,
        TimeSpan.Zero,
        TimeSpan.FromTicks(-1),
        TimeSpan.FromTicks(1),
        TimeSpan.FromDays(1.5),
        TimeSpan.FromTicks(-1234567890123456),
        TimeSpan.FromMilliseconds(1500.25),
    };

    [Fact]
    public void WriterEmitsTheSameTextAsToStringForEveryValue()
    {
        foreach (var value in DateTimes)
            AssertWriterText(value, "\"" + value.ToString("O") + "\"");

        foreach (var value in DateTimeOffsets)
            AssertWriterText(value, "\"" + value.ToString("O") + "\"");

        foreach (var value in Guids)
            AssertWriterText(value, "\"" + value.ToString() + "\"");

        foreach (var value in TimeSpans)
            AssertWriterText(value, "\"" + value.ToString("c") + "\"");
    }

    [Fact]
    public void SerializerMatchesToStringInBothEncodings()
    {
        foreach (var value in DateTimes)
            AssertSerializerMatches(value, "\"" + value.ToString("O") + "\"");

        foreach (var value in DateTimeOffsets)
            AssertSerializerMatches(value, "\"" + value.ToString("O") + "\"");

        foreach (var value in Guids)
            AssertSerializerMatches(value, "\"" + value.ToString() + "\"");

        foreach (var value in TimeSpans)
            AssertSerializerMatches(value, "\"" + value.ToString("c") + "\"");
    }

    [Fact]
    public void NestedAndCollectionValuesStillRoundTrip()
    {
        var model = new FormattedScalarModel
        {
            CreatedAt = new DateTime(2023, 11, 15, 6, 13, 20, DateTimeKind.Utc),
            SavedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).ToOffset(TimeSpan.FromHours(8)),
            Id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            Elapsed = TimeSpan.FromMilliseconds(1500.25),
            OptionalAt = new DateTime(2020, 2, 29, 12, 0, 0, DateTimeKind.Utc),
        };

        string json = LuminPackSerializer.SerializeJson(model);
        Assert.Contains("\"CreatedAt\":\"2023-11-15T06:13:20.0000000Z\"", json);

        var roundTrip = LuminPackSerializer.DeserializeJson<FormattedScalarModel>(json);
        Assert.Equal(model.CreatedAt, roundTrip.CreatedAt);
        Assert.Equal(model.SavedAt, roundTrip.SavedAt);
        Assert.Equal(model.Id, roundTrip.Id);
        Assert.Equal(model.Elapsed, roundTrip.Elapsed);
        Assert.Equal(model.OptionalAt, roundTrip.OptionalAt);

        var list = new List<DateTimeOffset>(DateTimeOffsets);
        var listJson = LuminPackSerializer.SerializeJson(list);
        Assert.Equal(list, LuminPackSerializer.DeserializeJson<List<DateTimeOffset>>(listJson));

        var array = (DateTime[])DateTimes.Clone();
        var arrayJson = LuminPackSerializer.SerializeJson(array);
        Assert.Equal(array, LuminPackSerializer.DeserializeJson<DateTime[]>(arrayJson));
    }

    [Fact]
    public void NullableValuesStillEmitNullAndValues()
    {
        var none = new FormattedScalarModel
        {
            CreatedAt = DateTime.UnixEpoch,
            SavedAt = DateTimeOffset.UnixEpoch,
            Id = Guid.Empty,
            Elapsed = TimeSpan.Zero,
            OptionalAt = null,
        };

        string nullJson = LuminPackSerializer.SerializeJson(none);
        Assert.Contains("\"OptionalAt\":null", nullJson);
        Assert.Null(LuminPackSerializer.DeserializeJson<FormattedScalarModel>(nullJson).OptionalAt);

        none.OptionalAt = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        string valueJson = LuminPackSerializer.SerializeJson(none);
        Assert.Equal(none.OptionalAt, LuminPackSerializer.DeserializeJson<FormattedScalarModel>(valueJson).OptionalAt);
    }

    [Fact]
    public void WritingScalarsIntoAReusableBufferAllocatesNothing()
    {
        using var buffer = LuminBufferWriterPool.Rent();
        DateTime moment = new(2023, 11, 15, 6, 13, 20, DateTimeKind.Utc);
        DateTimeOffset offset = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).ToOffset(TimeSpan.FromHours(8));
        Guid id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        TimeSpan elapsed = TimeSpan.FromMilliseconds(1500.25);

        // Warm up the code paths and the reusable buffer before measuring.
        for (int i = 0; i < 64; i++)
            WriteAll(buffer, moment, offset, id, elapsed);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++)
            WriteAll(buffer, moment, offset, id, elapsed);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static void WriteAll(LuminBufferWriter buffer, DateTime moment, DateTimeOffset offset, Guid id, TimeSpan elapsed)
    {
        LuminPackSerializer.SerializeJson(moment, buffer);
        LuminPackSerializer.SerializeJson(offset, buffer);
        LuminPackSerializer.SerializeJson(id, buffer);
        LuminPackSerializer.SerializeJson(elapsed, buffer);
    }

    private static void AssertWriterText(DateTime value, string expected) => AssertWriter(value, expected, WriteDateTime);

    private static void AssertWriterText(DateTimeOffset value, string expected) => AssertWriter(value, expected, WriteDateTimeOffset);

    private static void AssertWriterText(Guid value, string expected) => AssertWriter(value, expected, WriteGuid);

    private static void AssertWriterText(TimeSpan value, string expected) => AssertWriter(value, expected, WriteTimeSpan);

    private static void WriteDateTime(ref LuminPackJsonWriter writer, DateTime value) => writer.WriteDateTime(value);

    private static void WriteDateTimeOffset(ref LuminPackJsonWriter writer, DateTimeOffset value) => writer.WriteDateTimeOffset(value);

    private static void WriteGuid(ref LuminPackJsonWriter writer, Guid value) => writer.WriteGuid(value);

    private static void WriteTimeSpan(ref LuminPackJsonWriter writer, TimeSpan value) => writer.WriteTimeSpan(value);

    private static void AssertWriter<T>(T value, string expected, WriteScalar<T> write)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] guarded = Enumerable.Repeat((byte)0xCC, expectedBytes.Length + 16).ToArray();
        Span<byte> destination = guarded.AsSpan(0, expectedBytes.Length);

        var writer = new LuminPackJsonWriter(ref destination, new LuminPackWriterOptionalState());
        write(ref writer, value);

        Assert.Equal(expected, Encoding.UTF8.GetString(writer.GetSpan()));
        Assert.All(guarded.Skip(expectedBytes.Length), octet => Assert.Equal((byte)0xCC, octet));
    }

    private delegate void WriteScalar<T>(ref LuminPackJsonWriter writer, T value);

    private static void AssertSerializerMatches<T>(T value, string expected)
    {
        Assert.Equal(expected, LuminPackSerializer.SerializeJson(value));

        string utf16 = LuminPackSerializer.SerializeJson(value, new LuminPackSerializerOption
        {
            StringEncoding = LuminPackStringEncoding.UTF16
        });
        Assert.Equal(expected, utf16);
    }
}

[LuminPack.Attribute.LuminPackable]
public sealed partial class FormattedScalarModel
{
    public DateTime CreatedAt;
    public DateTimeOffset SavedAt;
    public Guid Id;
    public TimeSpan Elapsed;
    public DateTime? OptionalAt;
}