#if LUMINPACK_NEXT_GENERATOR
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Covers segmented input, stream cancellation and nullable collection cases found in the Nino test inventory.</summary>
public sealed class NinoCoverageRegressionTest
{
    /// <summary>Reads both Boolean collection shapes at every framing and payload split, including empty segments.</summary>
    [Fact]
    public void SegmentedBooleanCollectionsValidateTheWholePayload()
    {
        bool[] expected = Enumerable.Range(0, 65).Select(i => i % 3 == 1).ToArray();
        byte[] input = [0xcc, .. LuminPackSerializer.Serialize(expected), 0xcc];
        int length = input.Length - 2;
        bool[] array = new bool[expected.Length];
        List<bool> list = new(expected.Length);
        var originalArray = array;
        var originalList = list;

        for (int split = 0; split <= length; split++)
        {
            var sequence = Split(input.AsMemory(1, split), input.AsMemory(1 + split, length - split));
            Assert.Equal(length, LuminPackSerializer.Deserialize(sequence, ref array));
            Assert.Equal(length, LuminPackSerializer.Deserialize(sequence, ref list));
            Assert.Same(originalArray, array);
            Assert.Same(originalList, list);
            Assert.Equal(expected, array);
            Assert.Equal(expected, list);

            var truncated = sequence.Slice(0, length - 1);
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(truncated, ref array));
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(truncated, ref list));
            Assert.Same(originalArray, array);
            Assert.Same(originalList, list);
            Assert.Equal(expected, array);
            Assert.Equal(expected, list);

            byte last = input[length];
            input[length] = 128;
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<bool[]>(sequence));
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<List<bool>>(sequence));
            input[length] = last;
        }
        Assert.Equal(0xcc, input[0]);
        Assert.Equal(0xcc, input[^1]);
    }

    /// <summary>Stages a sequence that borrows bytes from the destination before the old array can be replaced or reused.</summary>
    /// <param name="reuse">Whether the existing array has the decoded length.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SegmentedByteArrayCanBorrowTheDestination(bool reuse)
    {
        byte[] expected = [11, 22, 33, 44, 55, 66];
        byte[] payload = LuminPackSerializer.Serialize(expected);
        byte[] original = reuse ? [33, 44, 55, 66, 99, 99] : [33, 44, 55, 66];
        byte[] target = original;
        var sequence = Split(payload.AsMemory(0, sizeof(int) + 2), original.AsMemory(0, 4));

        Assert.Equal(payload.Length, LuminPackSerializer.Deserialize(sequence, ref target));
        Assert.Equal(expected, target);
        if (reuse)
            Assert.Same(original, target);
        else
        {
            Assert.NotSame(original, target);
            Assert.Equal(new byte[] { 33, 44, 55, 66 }, original);
        }
    }

    /// <summary>Aborts after pooled staging has grown, then reads an independent payload successfully.</summary>
    /// <returns>Completion of the cancellation and recovery checks.</returns>
    [Fact]
    public async Task StreamCancellationAfterSeveralChunksAllowsNextRead()
    {
        byte[] values = Enumerable.Range(0, 16384).Select(i => unchecked((byte)(i * 17))).ToArray();
        byte[] payload = LuminPackSerializer.Serialize(values);
        using var cancellation = new CancellationTokenSource();
        using var input = new CancelingStream(payload, cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await LuminPackSerializer.DeserializeAsync<byte[]>(input, cancellationToken: cancellation.Token));
        Assert.Equal(3 * 2048, input.BytesRead);

        using var next = new MemoryStream(payload);
        Assert.Equal(values, await LuminPackSerializer.DeserializeAsync<byte[]>(next));
    }

    /// <summary>Checks nullable Boolean values through wrapped queue, LIFO, immutable and memory storage.</summary>
    /// <param name="length">Number of values, including empty and vector-tail cases.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(37)]
    [InlineData(300)]
    public void NullableBooleansKeepNullFalseAndTrueAcrossCollections(int length)
    {
        bool?[] expected = Enumerable.Range(0, length).Select(i => (i % 3) switch
        {
            0 => (bool?)null,
            1 => false,
            _ => true
        }).ToArray();
        var queue = new Queue<bool?>(length);
        if (length != 0)
        {
            for (int i = 0; i < length / 2; i++) queue.Enqueue(false);
            queue.Enqueue(expected[0]);
            for (int i = 0; i < length / 2; i++) queue.Dequeue();
            for (int i = 1; i < length; i++) queue.Enqueue(expected[i]);
        }
        var stack = new ConcurrentStack<bool?>(expected);
        var immutable = ImmutableQueue.CreateRange(expected);
        var memory = new Memory<bool?>(expected);

        Assert.Equal(expected, LuminPackSerializer.Deserialize<Queue<bool?>>(LuminPackSerializer.Serialize(queue)));
        Assert.Equal(Enumerable.Reverse(expected), LuminPackSerializer.Deserialize<ConcurrentStack<bool?>>(LuminPackSerializer.Serialize(stack)));
        Assert.Equal(expected, LuminPackSerializer.Deserialize<ImmutableQueue<bool?>>(LuminPackSerializer.Serialize(immutable)));
        Assert.Equal(expected, LuminPackSerializer.Deserialize<Memory<bool?>>(LuminPackSerializer.Serialize(memory)).ToArray());
    }

    /// <summary>Round-trips IEEE bit patterns that ordinary floating-point equality cannot distinguish.</summary>
    /// <param name="half">The binary16 bits.</param>
    /// <param name="single">The binary32 bits.</param>
    /// <param name="wide">The binary64 bits.</param>
    /// <param name="signalingNaNs">Whether the values use signaling NaN encodings.</param>
    [Theory]
    [InlineData((ushort)0x8000, 0x80000000u, 0x8000000000000000ul, false)]
    [InlineData((ushort)0x03ff, 0x007fffffu, 0x000ffffffffffffful, false)]
    [InlineData((ushort)0x7e01, 0xffc00001u, 0x7ff8deadbeef0001ul, false)]
    [InlineData((ushort)0x7c01, 0x7f800001u, 0x7ff0000000000001ul, true)]
    public void BinaryFloatingPointRoundTripsExactBits(ushort half, uint single, ulong wide, bool signalingNaNs)
    {
        byte[] halfBytes = new byte[sizeof(ushort)];
        byte[] singleBytes = new byte[sizeof(uint)];
        byte[] wideBytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt16LittleEndian(halfBytes, half);
        BinaryPrimitives.WriteUInt32LittleEndian(singleBytes, single);
        BinaryPrimitives.WriteUInt64LittleEndian(wideBytes, wide);

        Assert.Equal(halfBytes, LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<Half>(halfBytes)));
        float singleValue = default;
        double wideValue = default;
        Assert.Equal(singleBytes.Length, LuminPackSerializer.Deserialize(singleBytes, ref singleValue));
        Assert.Equal(wideBytes.Length, LuminPackSerializer.Deserialize(wideBytes, ref wideValue));
        Assert.Equal(singleBytes, LuminPackSerializer.Serialize(in singleValue));
        Assert.Equal(wideBytes, LuminPackSerializer.Serialize(in wideValue));

        byte[] singleReturn = LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<float>(singleBytes));
        byte[] wideReturn = LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<double>(wideBytes));
        if (signalingNaNs && RuntimeInformation.ProcessArchitecture == Architecture.X86)
        {
            // The x86 CLR uses the x87 return register for by-value floating-point results.
            if (BinaryPrimitives.ReadUInt32LittleEndian(singleReturn) != single)
                BinaryPrimitives.WriteUInt32LittleEndian(singleBytes, single | 0x00400000u);
            if (BinaryPrimitives.ReadUInt64LittleEndian(wideReturn) != wide)
                BinaryPrimitives.WriteUInt64LittleEndian(wideBytes, wide | 0x0008000000000000ul);
        }
        Assert.Equal(singleBytes, singleReturn);
        Assert.Equal(wideBytes, wideReturn);
    }

    /// <summary>Checks versioned sequence records and preserves a previously borrowed read-only memory slice.</summary>
    [Fact]
    public void VersionedReadOnlyMemoryKeepsTheOldBackingUntouched()
    {
        int[] old = [99, -1, -2, 77];
        var target = new NinoReadOnlyVersionRecord { Values = old.AsMemory(1, 2) };
        var original = target;
        var source = new NinoReadOnlyVersionRecord
        {
            Values = new[] { 12, 13 },
            Sequence = Split(new byte[] { 11, 12 }, new byte[] { 13, 14 }),
            Tail = 42
        };
        byte[] payload = LuminPackSerializer.Serialize(source);

        Assert.Equal(payload.Length, LuminPackSerializer.Deserialize(payload, ref target));
        Assert.Same(original, target);
        Assert.Equal(new[] { 99, -1, -2, 77 }, old);
        Assert.Equal(new[] { 12, 13 }, target.Values.ToArray());
        Assert.Equal(new byte[] { 11, 12, 13, 14 }, target.Sequence.ToArray());
        Assert.Equal(42, target.Tail);
    }

    /// <summary>Builds a sequence with an empty segment between the two ranges.</summary>
    /// <param name="prefix">The first range.</param>
    /// <param name="suffix">The final range.</param>
    /// <returns>The complete logical input.</returns>
    private static ReadOnlySequence<byte> Split(ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> suffix)
    {
        var first = new Segment(prefix);
        Segment last = first.Append(ReadOnlyMemory<byte>.Empty).Append(suffix);
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        /// <summary>Starts a sequence segment over the supplied memory.</summary>
        /// <param name="memory">The bytes referenced by the segment.</param>
        internal Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        /// <summary>Links the next segment at the current logical end.</summary>
        /// <param name="memory">The bytes referenced by the new segment.</param>
        /// <returns>The linked segment.</returns>
        internal Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

    private sealed class CancelingStream(byte[] bytes, CancellationTokenSource cancellation) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        internal int BytesRead { get; private set; }

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            int read = await base.ReadAsync(buffer[..Math.Min(buffer.Length, 2048)], cancellationToken);
            BytesRead += read;
            if (BytesRead == 3 * 2048)
                cancellation.Cancel();
            return read;
        }
    }
}

/// <summary>Stores read-only wrappers as independently framed versioned members.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class NinoReadOnlyVersionRecord
{
    public ReadOnlyMemory<int> Values;
    public ReadOnlySequence<byte> Sequence;
    public int Tail;
}
#endif
