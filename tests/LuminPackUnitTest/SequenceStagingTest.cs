using System.Buffers;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

public class SequenceStagingTest
{
    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;
        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

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

    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void StagedSequencesPreserveKnownOptionsAndConsumedLength(LuminPackStringEncoding encoding)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding };
        var value = new IBufferModel { Id = 42, Name = new string('中', 10_000) + "😀", Numbers = [1, 2, 3] };
        byte[] payload = LuminPackSerializer.Serialize(value, option);
        ReadOnlySequence<byte> sequence = Split(payload);
        IBufferModel result = default!;
        int consumed = LuminPackSerializer.Deserialize(sequence, ref result, option);
        Assert.Equal(payload.Length, consumed);
        Assert.Equal(value.Id, result.Id);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Numbers, result.Numbers);
    }

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
