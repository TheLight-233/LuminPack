using System.Globalization;
using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks buffered UTF-16 integer sorted-set output and its callback fallback.</summary>
public sealed class SortedSetBufferedJsonWriteTest
{
    /// <summary>Checks the default generated writers retain enumeration order at both integer widths.</summary>
    [Fact]
    public void DefaultUtf16WritesExactInt32AndInt64Arrays()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var integers = new SortedSet<int> { 9, int.MinValue, -1, int.MaxValue };
            var longs = new SortedSet<long>(Comparer<long>.Create((left, right) => right.CompareTo(left)))
            {
                long.MinValue, 7, 4_000_000_000L, long.MaxValue
            };

            Assert.Equal("[-2147483648,-1,9,2147483647]", LuminPackSerializer.SerializeJson(integers));
            Assert.Equal("[9223372036854775807,4000000000,7,-9223372036854775808]",
                LuminPackSerializer.SerializeJson(longs));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Checks the narrower reservation includes a complete final formatter window.</summary>
    [Fact]
    public void CompactInt32GateKeepsTheFinalElementReservation()
    {
        var values = new SortedSet<int> { 0, 9, 99999 };
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            // The UTF-16 scalar helper needs eleven character slots plus its optional comma.
            const int scalarReservation = 12 * sizeof(char);
            foreach (bool continuation in new[] { false, true })
            {
                string prefix = continuation ? "[7" : "[";
                string precedingElements = continuation ? ",0,9" : "0,9";
                int required = precedingElements.Length * sizeof(char) + scalarReservation;
                Assert.True(required < values.Count * scalarReservation);

                Assert.Equal((false, prefix), WriteWithinWindow(values, continuation, required - 1));
                Assert.Equal((true, prefix + precedingElements + ",99999"),
                    WriteWithinWindow(values, continuation, required));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        static (bool Written, string Prefix) WriteWithinWindow(SortedSet<int> input, bool continuation, int available)
        {
            using var owner = new LuminBufferWriter(true);
            var writer = new LuminPackJsonWriter(owner);
            int capacity = writer.Buffer.Length;
            int start = capacity - available - (continuation ? 2 : 1) * sizeof(char);
            writer.Position = start;
            writer.WriteArrayStart<LuminPackUtf16LengthOptions>();
            if (continuation)
                writer.Write<LuminPackUtf16LengthOptions>(7);

            Assert.Equal(available, capacity - writer.Position);
            bool written = LuminTreeStorage.TryWriteIntegerSortedSetJson<LuminPackUtf16LengthOptions, int>(ref writer, input);
            Assert.Equal(capacity, writer.Buffer.Length);
            return (written, Encoding.Unicode.GetString(writer.GetSpan().Slice(start)));
        }
    }

    /// <summary>Checks a number-format callback sees the original enumerator diagnostic and committed prefix.</summary>
    [Fact]
    public void CultureMutationKeepsFrameworkExceptionAndWrittenPrefix()
    {
        var control = new SortedSet<int> { int.MinValue, 7, 9 };
        var enumerator = control.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(control.Remove(9));
        string expectedMessage = Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext()).Message;

        var values = new SortedSet<int> { int.MinValue, 7, 9 };
        var culture = new MutatingNumberCulture(() => values.Remove(9));
        CultureInfo previous = CultureInfo.CurrentCulture;
        using var owner = new LuminBufferWriter(true);
        var writer = new LuminPackJsonWriter(owner);
        Exception? error = null;
        try
        {
            CultureInfo.CurrentCulture = culture;
            LuminPackSerializer.SerializeJson(ref writer, values);
        }
        catch (Exception exception)
        {
            error = exception;
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.Equal(expectedMessage, Assert.IsType<InvalidOperationException>(error).Message);
        Assert.Equal(new[] { int.MinValue, 7 }, values);
        Assert.Equal("[-2147483648", Encoding.Unicode.GetString(writer.GetSpan()));
        Assert.Equal(1, writer.Depth);
        Assert.False(writer.IsFirstElement);
    }

    private sealed class MutatingNumberCulture(Action mutate) : CultureInfo("en-US")
    {
        private bool _invoked;

        public override object? GetFormat(Type? formatType)
        {
            if (!_invoked && formatType == typeof(NumberFormatInfo))
            {
                _invoked = true;
                mutate();
            }
            return base.GetFormat(formatType);
        }
    }
}
