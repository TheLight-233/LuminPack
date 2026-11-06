using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

[Collection(nameof(RuntimeStateCollection))]
public partial class JsonFormattedScalarTest
{
    /// <summary>Checks fixed binary component records, omitted components, fresh identity and truncated publication.</summary>
    [Fact]
    public void VersionBinaryRecordsKeepComponentsAndBounds()
    {
        foreach (var value in Versions())
        {
            byte[] bytes = LuminPackSerializer.Serialize(value);
            if (value is null)
                Assert.Equal(new byte[] { 255 }, bytes);
            else
            {
                byte[] expected = new byte[17];
                expected[0] = 4;
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(1), value.Major);
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(5), value.Minor);
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(9), value.Build);
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(13), value.Revision);
                Assert.Equal(expected, bytes);
            }
            var target = new Version(9, 8);
            var previous = target;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
            Assert.Equal(value, target);
            Assert.NotSame(previous, target);
            if (value is not null) Assert.NotSame(value, target);
            for (int cut = 0; cut < bytes.Length; cut++)
            {
                target = previous;
                Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, cut), ref target));
                Assert.Same(previous, target);
            }
        }
    }

    /// <summary>Checks both JSON encodings against the original BCL text and parsing behavior.</summary>
    [Fact]
    public void VersionJsonKeepsBclFormatsAndFailures()
    {
        var previous = CultureInfo.CurrentCulture;
        var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        custom.NumberFormat.PositiveSign = "p";
        custom.NumberFormat.NegativeSign = "n";
        try
        {
            CultureInfo.CurrentCulture = custom;
            CheckVersionJson<LuminPackUtf8LengthOptions>(Encoding.UTF8);
            CheckVersionJson<LuminPackUtf16LengthOptions>(Encoding.Unicode);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    /// <summary>Checks writes allocate no text and span reads allocate only their established Version result.</summary>
    [Fact]
    public void VersionJsonAvoidsIntermediateStrings()
    {
        var value = new Version(1, 2, 3, 4);
        using var output = new LuminBufferWriter(true);
        for (int i = 0; i < 256; i++) WriteVersionPair(value, output);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) WriteVersionPair(value, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        for (int i = 0; i < 256; i++) GC.KeepAlive(Version.Parse("1.2.3.4"));
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) GC.KeepAlive(Version.Parse("1.2.3.4"));
        long expected = GC.GetAllocatedBytesForCurrentThread() - before;
        CheckVersionReadAllocation<LuminPackUtf16LengthOptions>(Encoding.Unicode.GetBytes("\"1.2.3.4\""), expected);
        CheckVersionReadAllocation<LuminPackUtf8LengthOptions>(Encoding.UTF8.GetBytes("\"1.2.3.4\""), expected);
    }

    /// <summary>Checks unselected UTF-8 reads avoid token strings for bounded and pooled decoder storage.</summary>
    [Fact]
    public void DirectVersionReadsAvoidIntermediateStrings()
    {
        foreach (string text in new[] { "1.2.3.4", new string('0', 257) + "1.2.3.4" })
        {
            byte[] input = Encoding.UTF8.GetBytes("\"" + text + "\"");
            var state = new LuminPackReaderOptionalState();
            for (int i = 0; i < 64; i++) { GC.KeepAlive(Version.Parse(text)); GC.KeepAlive(ReadVersionDirect(input, state)); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++) GC.KeepAlive(Version.Parse(text));
            long necessary = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++) GC.KeepAlive(ReadVersionDirect(input, state));
            Assert.Equal(necessary, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.Equal(new Version(1, 2, 3, 4), ReadVersionDirect(input, state));
        }
    }

    /// <summary>Parses a complete unselected token while keeping portable input pinned.</summary>
    /// <param name="input">The UTF-8 document.</param>
    /// <param name="state">The reusable reader state.</param>
    /// <returns>The newly parsed result.</returns>
    private static unsafe Version? ReadVersionDirect(byte[] input, LuminPackReaderOptionalState state)
    {
        fixed (byte* pointer = input)
        {
            ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(pointer, input.Length);
            var reader = new LuminPackJsonReader(ref data, state);
            return reader.ReadVersion<LuminPackUtf8LengthOptions>();
        }
    }

    /// <summary>Provides null, omitted and maximum component forms.</summary>
    /// <returns>The representative version values.</returns>
    private static Version?[] Versions() => new Version?[] { null, new(), new(1, 2), new(1, 2, 3), new(1, 2, 3, 4), new(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue) };

    /// <summary>Compares JSON values and invalid component failures with the original string parser.</summary>
    /// <param name="encoding">The document encoding.</param>
    /// <typeparam name="TOptions">The compiled document profile.</typeparam>
    private static void CheckVersionJson<TOptions>(Encoding encoding) where TOptions : struct, ILuminPackOptions
    {
        foreach (var value in Versions())
        {
            string json = LuminPackSerializer.SerializeJson<TOptions>(value);
            Assert.Equal(value is null ? "null" : "\"" + value + "\"", json);
            var target = new Version(9, 8);
            var previous = target;
            LuminPackSerializer.DeserializeJson<TOptions>(encoding.GetBytes(json), ref target);
            Assert.Equal(value, target);
            Assert.NotSame(previous, target);
        }
        foreach (string text in new[] { "1.2", " 1 . +2 ", "1.2\0", "1.2\0\0", "1.2.3.4.5", "+1.+2", "-1.2", "1.-2", "1.2.-3", "1.2.3.-4", "2147483648.0", "1.", ".2", "1", "", "\u00a01.2", "\u0661.\u0662", "1\0.2", "\u22121.2", "1.2\u0085", "1.\t2" })
        {
            Version? expected = null;
            Exception? failure = Record.Exception(() => expected = Version.Parse(text));
            string json = LuminPackSerializer.SerializeJson<TOptions>(text);
            var target = new Version(9, 8);
            var previous = target;
            Exception? actual = Record.Exception(() => LuminPackSerializer.DeserializeJson<TOptions>(encoding.GetBytes(json), ref target));
            Assert.Equal(failure?.GetType(), actual?.GetType());
            Assert.Equal((failure as ArgumentException)?.ParamName, (actual as ArgumentException)?.ParamName);
            if (failure is null) Assert.Equal(expected, target);
            else Assert.Same(previous, target);
        }
        foreach (string json in new[] { "\"1.2", "\"1.\\u12\"", "123", "[]" })
        {
            var target = new Version(9, 8);
            var previous = target;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<TOptions>(encoding.GetBytes(json), ref target));
            Assert.Same(previous, target);
        }
    }

    /// <summary>Exercises both writer profiles through retained storage.</summary>
    /// <param name="value">The version value.</param>
    /// <param name="output">The reusable destination.</param>
    private static void WriteVersionPair(Version value, LuminBufferWriter output)
    {
        output.ResetCore(); LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(value, output);
        output.ResetCore(); LuminPackSerializer.SerializeJson<LuminPackUtf16LengthOptions>(value, output);
    }

    /// <summary>Compares complete generated reads with the allocation required by the original BCL constructor.</summary>
    /// <param name="input">The encoded JSON string.</param>
    /// <param name="expected">The warmed BCL allocation for the same iteration count.</param>
    /// <typeparam name="TOptions">The compiled document profile.</typeparam>
    private static void CheckVersionReadAllocation<TOptions>(byte[] input, long expected) where TOptions : struct, ILuminPackOptions
    {
        var target = new Version(9, 8);
        for (int i = 0; i < 256; i++) LuminPackSerializer.DeserializeJson<TOptions>(input, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) LuminPackSerializer.DeserializeJson<TOptions>(input, ref target);
        Assert.Equal(expected, GC.GetAllocatedBytesForCurrentThread() - before);
        GC.KeepAlive(target);
    }
}
