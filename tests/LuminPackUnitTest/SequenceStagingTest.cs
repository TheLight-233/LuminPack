using System.Buffers;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides sequence staging test functionality.</summary>
public class SequenceStagingTest
{
    /// <summary>Provides segment functionality.</summary>
    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        /// <summary>Initializes a new instance of <c>Segment</c>.</summary>
        /// <param name="memory">The memory used by this operation.</param>
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;
        /// <summary>Verifies append.</summary>
        /// <param name="memory">The memory used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

    /// <summary>Verifies split.</summary>
    /// <param name="bytes">The bytes used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static ReadOnlySequence<byte> Split(byte[] bytes)
    {
        var first = new Segment(ReadOnlyMemory<byte>.Empty);
        Segment last = first;
        for (int index = 0; index < bytes.Length;)
        {
            int length = Math.Min(index < 16 ? 1 : 127, bytes.Length - index);
            last = last.Append(bytes.AsMemory(index, length));
            index += length;
        }
        last = last.Append(ReadOnlyMemory<byte>.Empty);
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    /// <summary>Verifies staged sequences preserve known options and consumed length.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void StagedSequencesPreserveKnownOptionsAndConsumedLength(LuminPackStringEncoding encoding)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding };
        var value = new IBufferModel { Id = 42, Name = new string('\u4e2d', 10_000) + "😀", Numbers = [1, 2, 3] };
        byte[] payload = LuminPackSerializer.Serialize(value, option);
        ReadOnlySequence<byte> sequence = Split(payload);
        IBufferModel result = default!;
        int consumed = LuminPackSerializer.Deserialize(sequence, ref result, option);
        Assert.Equal(payload.Length, consumed);
        Assert.Equal(value.Id, result.Id);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Numbers, result.Numbers);
    }

    /// <summary>Verifies reusing array destination does not allocate payload staging.</summary>
    [Fact]
    public void ReusingArrayDestinationDoesNotAllocatePayloadStaging()
    {
        byte[] value = Enumerable.Range(0, 65_536).Select(i => (byte)i).ToArray();
        var sequence = Split(LuminPackSerializer.Serialize(value));
        byte[] destination = new byte[value.Length];
        LuminPackSerializer.Deserialize(sequence, ref destination);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 10; iteration++)
            LuminPackSerializer.Deserialize(sequence, ref destination);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(value, destination);
    }

    /// <summary>Verifies loading sequences publishes only the latest bytes.</summary>
    [Fact]
    public void LoadingSequencesPublishesOnlyTheLatestBytes()
    {
        using var buffer = new LuminBufferWriter(true);
        byte[] large = new byte[100_000];
        buffer.LoadSequence(Split(large));
        Assert.Equal(large.Length, buffer.CurrentIndex);
        byte[] small = [1, 2, 3];
        buffer.LoadSequence(Split(small));
        Assert.Equal(small, buffer.GetSpan().ToArray());
        buffer.LoadSequence(ReadOnlySequence<byte>.Empty);
        Assert.Empty(buffer.GetSpan().ToArray());
    }
}
