#if LUMINPACK_NEXT_GENERATOR
using System.Buffers;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the valid ranges of standalone dates and times in binary input.</summary>
public class NinoTemporalRegressionTest
{
    /// <summary>Rejects negative day numbers and days beyond the last Gregorian date.</summary>
    [Fact]
    public async Task DateRootRejectsOutOfRangeDayNumbers()
    {
        foreach (int day in new[] { -1, DateOnly.MaxValue.DayNumber + 1, int.MaxValue })
        {
            byte[] bytes = BitConverter.GetBytes(day);
            DateOnly destination = DateOnly.MaxValue;
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<DateOnly>(bytes));
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<DateOnly, LuminPackUtf16TokenOptions>(bytes));
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref destination));
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<DateOnly>(new ReadOnlySequence<byte>(bytes)));
            Assert.Throws<InvalidDataException>(() => ReadDateFromState(bytes));
            using var stream = new MemoryStream(bytes);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await LuminPackSerializer.DeserializeAsync<DateOnly>(stream));
            Assert.Equal(DateOnly.MaxValue, destination);
        }
    }

    /// <summary>Rejects negative ticks and ticks at or after the end of one day.</summary>
    [Fact]
    public async Task TimeRootRejectsOutOfRangeTicks()
    {
        foreach (long ticks in new[] { -1L, TimeSpan.TicksPerDay, long.MaxValue })
        {
            byte[] bytes = BitConverter.GetBytes(ticks);
            TimeOnly destination = TimeOnly.MaxValue;
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<TimeOnly>(bytes));
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<TimeOnly, LuminPackUtf16TokenOptions>(bytes));
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref destination));
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<TimeOnly>(new ReadOnlySequence<byte>(bytes)));
            Assert.Throws<InvalidDataException>(() => ReadTimeFromState(bytes));
            using var stream = new MemoryStream(bytes);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await LuminPackSerializer.DeserializeAsync<TimeOnly>(stream));
            Assert.Equal(TimeOnly.MaxValue, destination);
        }
    }

    /// <summary>Reads both boundaries using the original native representation and a distinct profile.</summary>
    [Fact]
    public void TemporalRootsKeepValidBoundaryBytes()
    {
        foreach (DateOnly date in new[] { DateOnly.MinValue, DateOnly.MaxValue })
        {
            byte[] bytes = LuminPackSerializer.Serialize(date);
            Assert.Equal(BitConverter.GetBytes(date.DayNumber), bytes);
            Assert.Equal(date, LuminPackSerializer.Deserialize<DateOnly, LuminPackUtf16TokenOptions>(bytes));
            DateOnly destination = default;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref destination));
            Assert.Equal(date, destination);
        }
        foreach (TimeOnly time in new[] { TimeOnly.MinValue, TimeOnly.MaxValue })
        {
            byte[] bytes = LuminPackSerializer.Serialize(time);
            Assert.Equal(BitConverter.GetBytes(time.Ticks), bytes);
            Assert.Equal(time, LuminPackSerializer.Deserialize<TimeOnly, LuminPackUtf16TokenOptions>(bytes));
            TimeOnly destination = default;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref destination));
            Assert.Equal(time, destination);
        }
    }

    /// <summary>Uses the generated advancing date read on caller-owned state.</summary>
    /// <param name="bytes">The date input.</param>
    private static void ReadDateFromState(byte[] bytes)
    {
        ReadOnlySpan<byte> span = bytes;
        var reader = new LuminPackReader(ref span);
        DateOnly value = default;
        LuminPackSerializer.Deserialize(ref reader, ref value);
    }

    /// <summary>Uses the generated advancing time read on caller-owned state.</summary>
    /// <param name="bytes">The time input.</param>
    private static void ReadTimeFromState(byte[] bytes)
    {
        ReadOnlySpan<byte> span = bytes;
        var reader = new LuminPackReader(ref span);
        TimeOnly value = default;
        LuminPackSerializer.Deserialize(ref reader, ref value);
    }
}
#endif
