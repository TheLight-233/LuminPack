using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;
/// <summary>Protects caller-owned append cursors, reference lifetimes and exceptional completion.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class BufferAppendTest
{
    /// <summary>Preserves fresh-writer capacity for large unchecked records even below the ordinary resize threshold.</summary>
    [Fact]
    public void LargeFixedRecordsAndBatchesAppendSafely()
    {
        using var buffer = new LuminBufferWriter(true);
        var raw = new AppendLargeFixed
        {
            Value = 911
        };
        byte[] rawBytes = LuminPackSerializer.Serialize(raw);
        LuminPackSerializer.Serialize(raw, buffer);
        LuminPackSerializer.Serialize<AppendLargeFixed>(raw, buffer);
        Assert.True(buffer.CurrentIndex <= buffer.TotalLength);
        Assert.True(buffer.GetSpan().SequenceEqual(rawBytes.Concat(rawBytes).ToArray()));
        buffer.ResetForReuse();
        var batch = new AppendLargeFixedContainer
        {
            Value = raw,
            Tail = 719
        };
        byte[] batchBytes = LuminPackSerializer.Serialize(batch);
        for (int index = 0; index < 8; index++)
            LuminPackSerializer.Serialize(batch, buffer);
        Assert.True(buffer.CurrentIndex <= buffer.TotalLength);
        for (int index = 0; index < 8; index++)
            Assert.True(buffer.GetSpan().Slice(index * batchBytes.Length, batchBytes.Length).SequenceEqual(batchBytes));
    }

    /// <summary>Preserves published-count precedence and live cursor visibility under either getter branch order.</summary>
    [Fact]
    public void PublishedAndLiveCursorPrecedenceIsUnchanged()
    {
        using var buffer = new LuminBufferWriter(true);
        buffer.GetFullSpan();
        buffer.CompleteWrite(123);
        int cursor = 45;
        buffer.SetCurrentIndexPtr(ref cursor);
        Assert.Equal(123, buffer.CurrentIndex);
        buffer.ResetCore();
        buffer.SetCurrentIndexPtr(ref cursor);
        Assert.Equal(45, buffer.CurrentIndex);
        cursor = 67;
        Assert.Equal(67, buffer.CurrentIndex);
        buffer.CompleteWrite(cursor);
        cursor = 89;
        Assert.Equal(67, buffer.CurrentIndex);
    }

    /// <summary>Crosses several native segments with unchecked fixed records at append boundaries.</summary>
    [Fact]
    public void FixedRecordsAppendAcrossSegmentBoundaries()
    {
        using var buffer = new LuminBufferWriter(true);
        for (int index = 0; index < 40000; index++)
            LuminPackSerializer.Serialize(index, buffer);
        Assert.Equal(40000 * sizeof(int), buffer.CurrentIndex);
        ReadOnlySpan<byte> bytes = buffer.GetSpan();
        var reader = new LuminPackReader(ref bytes);
        for (int index = 0; index < 40000; index++)
            Assert.Equal(index, reader.Read<int>());
    }

    /// <summary>Appends after an arbitrary prefix near the segment end, including through generic profile forwarders.</summary>
    [Fact]
    public void GenericProfilesPreservePrefixAndAppendExactBytes()
    {
        using var buffer = new LuminBufferWriter(true);
        byte[] prefix = Enumerable.Repeat((byte)0xA5, 32767).ToArray();
        buffer.LoadSequence(new System.Buffers.ReadOnlySequence<byte>(prefix));
        const string value = "appended \u4E2D\uD83D\uDE00";
        byte[] expected = LuminPackSerializer.Serialize<string, LuminPackUtf16TokenOptions>(value);
        LuminPackSerializer.Serialize<string, LuminPackUtf16TokenOptions>(value, buffer);
        Assert.Equal(prefix.Length + expected.Length, buffer.CurrentIndex);
        Assert.True(buffer.GetSpan()[..prefix.Length].SequenceEqual(prefix));
        Assert.True(buffer.GetSpan()[prefix.Length..].SequenceEqual(expected));
        var versioned = new CapacityTierTest.VersionedOrdinary
        {
            Text = new string ('v', 20000),
            Values = [new CapacityTierTest.SmallGuaranteed
            {
                Id = 519,
                Text = "nested"
            }

            ]
        };
        byte[] record = LuminPackSerializer.Serialize(versioned);
        int start = buffer.CurrentIndex;
        LuminPackSerializer.Serialize(versioned, buffer);
        LuminPackSerializer.Serialize<CapacityTierTest.VersionedOrdinary>(versioned, buffer);
        Assert.True(buffer.GetSpan().Slice(start, record.Length).SequenceEqual(record));
        Assert.True(buffer.GetSpan()[(start + record.Length)..].SequenceEqual(record));
        var restored = LuminPackSerializer.Deserialize<CapacityTierTest.VersionedOrdinary>(buffer.GetSpan()[start..]);
        Assert.Equal(versioned.Text, restored.Text);
        Assert.Equal(519, Assert.Single(restored.Values).Id);
    }

    /// <summary>Retains one reference table across separately appended generated roots.</summary>
    [Fact]
    public void AppendedRootsShareReferenceIdentityUntilExplicitReset()
    {
        using var buffer = new LuminBufferWriter(true);
        var value = new AppendReferenceNode
        {
            Value = 719
        };
        value.Next = value;
        LuminPackSerializer.Serialize(value, buffer);
        int firstLength = buffer.CurrentIndex;
        LuminPackSerializer.Serialize<AppendReferenceNode>(value, buffer);
        Assert.True(buffer.WriterState.GetOrAddReference(value).existsReference);
        Assert.True(buffer.CurrentIndex - firstLength < firstLength);
        var next = new AppendReferenceNode
        {
            Value = 911,
            Next = value
        };
        LuminPackSerializer.Serialize(next, buffer);
        ReadOnlySpan<byte> bytes = buffer.GetSpan();
        var reader = new LuminPackReader(ref bytes, buffer.ReaderState);
        AppendReferenceNode first = null!, second = null!, third = null!;
        LuminPackSerializer.Deserialize(ref reader, ref first);
        LuminPackSerializer.Deserialize(ref reader, ref second);
        LuminPackSerializer.Deserialize(ref reader, ref third);
        Assert.Same(first, first.Next);
        Assert.Same(first, second);
        Assert.NotSame(first, third);
        Assert.Equal(911, third.Value);
        Assert.Same(first, third.Next);
        Assert.Equal(buffer.CurrentIndex, reader.Position);
        buffer.ReaderState.AddObjectReference(17, value);
        int capacity = buffer.TotalLength;
        buffer.ResetForReuse();
        Assert.Equal(capacity, buffer.TotalLength);
        Assert.Equal(0, buffer.CurrentIndex);
        Assert.False(buffer.WriterState.GetOrAddReference(value).existsReference);
        Assert.ThrowsAny<Exception>(() => buffer.ReaderState.GetObjectReference(17));
        buffer.ResetForReuse();
        LuminPackSerializer.Serialize(value, buffer);
        Assert.Equal(firstLength, buffer.CurrentIndex);
    }

    /// <summary>Appends complete JSON documents without inserting delimiters or clearing caller state.</summary>
    [Fact]
    public void JsonAppendsWithBothDefaultAndClosedProfileEntries()
    {
        using var buffer = new LuminBufferWriter(true);
        var marker = new object ();
        buffer.WriterState.GetOrAddReference(marker);
        LuminPackSerializer.SerializeJson("first", buffer);
        byte[] first = Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson("first"));
        LuminPackSerializer.SerializeJson<string>("second", buffer);
        byte[] second = Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson("second"));
        Assert.True(buffer.GetSpan().SequenceEqual(first.Concat(second).ToArray()));
        Assert.True(buffer.WriterState.GetOrAddReference(marker).existsReference);
        buffer.ResetForReuse();
        LuminPackSerializer.SerializeJson<string, LuminPackUtf16LengthOptions>("first", buffer);
        int offset = buffer.CurrentIndex;
        LuminPackSerializer.SerializeJson<string, LuminPackUtf16LengthOptions>("second", buffer);
        Assert.Equal("\"first\"", Encoding.Unicode.GetString(buffer.GetSpan()[..offset]));
        Assert.Equal("\"second\"", Encoding.Unicode.GetString(buffer.GetSpan()[offset..]));
    }

    /// <summary>Detaches stack cursor bindings on callback or Custom failures without clearing the prefix or references.</summary>
    /// <param name = "json">Whether to exercise the JSON entry.</param>
    /// <param name = "generic">Whether to exercise a generic forwarder.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FailedAppendPreservesCallerStateAndDetachesCursor(bool json, bool generic)
    {
        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(123, buffer);
        byte[] prefix = buffer.GetSpan().ToArray();
        var marker = new object ();
        buffer.WriterState.GetOrAddReference(marker);
        var failure = new InvalidOperationException("Expected callback failure.");
        var value = new AppendFailureModel
        {
            Failure = failure
        };
        Exception actual = Assert.Throws<InvalidOperationException>(() =>
        {
            if (json)
            {
                var custom = new AppendCustomFailure
                {
                    Failure = failure
                };
                if (generic)
                    LuminPackSerializer.SerializeJson<AppendCustomFailure>(custom, buffer);
                else
                    LuminPackSerializer.SerializeJson(custom, buffer);
            }
            else if (generic)
                LuminPackSerializer.Serialize<AppendFailureModel>(value, buffer);
            else
                LuminPackSerializer.Serialize(value, buffer);
        });
        Assert.Same(failure, actual);
        int length = buffer.CurrentIndex;
        ClobberStack();
        Assert.Equal(length, buffer.CurrentIndex);
        Assert.True(buffer.GetSpan()[..prefix.Length].SequenceEqual(prefix));
        Assert.True(buffer.WriterState.GetOrAddReference(marker).existsReference);
        buffer.ResetForReuse();
        LuminPackSerializer.Serialize(456, buffer);
        Assert.Equal(456, LuminPackSerializer.Deserialize<int>(buffer));
    }

    /// <summary>Clears both maps even when an explicitly reset payload has no published bytes.</summary>
    [Fact]
    public void FullResetClearsEmptyPayloadReferenceMaps()
    {
        using var buffer = new LuminBufferWriter(true);
        var marker = new object ();
        buffer.WriterState.GetOrAddReference(marker);
        buffer.ReaderState.AddObjectReference(17, marker);
        buffer.Reset();
        Assert.False(buffer.WriterState.GetOrAddReference(marker).existsReference);
        Assert.ThrowsAny<Exception>(() => buffer.ReaderState.GetObjectReference(17));
    }

    /// <summary>Overwrites temporary stack storage to expose escaped pointers to a completed writer's cursor.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ClobberStack()
    {
        Span<int> scratch = stackalloc int[2048];
        scratch.Fill(-7919);
        GC.KeepAlive(scratch[17]);
    }
}

/// <summary>Retains large raw storage whose complete unchecked write exceeds ordinary threshold headroom.</summary>
[LuminPackable, StructLayout(LayoutKind.Sequential, Size = 20000)]
public struct AppendLargeFixed
{
    public int Value;
}

/// <summary>Exercises the same large storage inside an ordinary fixed field batch.</summary>
[LuminPackable]
public sealed class AppendLargeFixedContainer
{
    public AppendLargeFixed Value;
    public int Tail;
}

/// <summary>Exercises cyclic references across independently appended root entries.</summary>
[LuminPackable(GeneratorType.CircleReference)]
public sealed partial class AppendReferenceNode
{
    [LuminPackOrder(0)]
    public int Value;
    [LuminPackOrder(1)]
    public AppendReferenceNode? Next;
}

/// <summary>Exercises exceptional cursor publication before user-owned state is reset.</summary>
[LuminPackable]
public sealed partial class AppendFailureModel
{
    [LuminPackIgnore]
    public Exception? Failure;
    public int Value;
    /// <summary>Fails before fields are written while preserving the caller exception's identity.</summary>
    [LuminPackOnSerializing]
    public void OnWriting() => throw Failure!;
}

/// <summary>Exercises an exception from a registered Custom method pointer after the cursor advances.</summary>
[LuminPackable(GeneratorType.Custom)]
public sealed class AppendCustomFailure
{
    public Exception? Failure;
    public int Value;
    /// <summary>Writes the raw value before propagating an optional caller failure.</summary>
    /// <param name = "writer">The active binary writer.</param>
    /// <param name = "value">The Custom record.</param>
    public static void Serialize(ref LuminPackWriter writer, in AppendCustomFailure value)
    {
        LuminPackSerializer.Serialize(ref writer, in value.Value);
        if (value.Failure is { } failure)
            throw failure;
    }

    /// <summary>Reads the matching raw value.</summary>
    /// <param name = "reader">The active binary reader.</param>
    /// <param name = "value">The restored record.</param>
    public static void Deserialize(ref LuminPackReader reader, ref AppendCustomFailure value)
    {
        value = new();
        LuminPackSerializer.Deserialize(ref reader, ref value.Value);
    }

    /// <summary>Writes a JSON number before propagating an optional caller failure.</summary>
    /// <param name = "writer">The active JSON writer.</param>
    /// <param name = "value">The Custom record.</param>
    public static void SerializeJson(ref LuminPackJsonWriter writer, in AppendCustomFailure value)
    {
        LuminPackSerializer.SerializeJson(ref writer, in value.Value);
        if (value.Failure is { } failure)
            throw failure;
    }

    /// <summary>Reads the matching JSON number.</summary>
    /// <param name = "reader">The active JSON reader.</param>
    /// <param name = "value">The restored record.</param>
    public static void DeserializeJson(ref LuminPackJsonReader reader, ref AppendCustomFailure value)
    {
        value = new();
        LuminPackSerializer.DeserializeJson(ref reader, ref value.Value);
    }
}
