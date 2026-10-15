using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;

namespace LuminPackBenchmark;

/// <summary>Provides sequence input benchmark functionality.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 12)]
public class SequenceInputBenchmark
{
    private ReadOnlySequence<byte> _sequence;
    private byte[] _destination = null!;
    /// <summary>Gets or sets the length.</summary>
    [Params(1024, 65_536)] public int Length { get; set; }
    /// <summary>Provides segment functionality.</summary>
    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        /// <summary>Initializes a new instance of <c>Segment</c>.</summary>
        /// <param name="memory">The memory used by this operation.</param>
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;
        /// <summary>Measures append for the configured benchmark input.</summary>
        /// <param name="memory">The memory used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        byte[] data = Enumerable.Range(0, Length).Select(i => (byte)i).ToArray();
        byte[] payload = LuminPackSerializer.Serialize(data);
        var first = new Segment(payload.AsMemory(0, 2));
        Segment last = first;
        for (int index = 2; index < payload.Length; index += 1024)
            last = last.Append(payload.AsMemory(index, Math.Min(1024, payload.Length - index)));
        _sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        _destination = new byte[data.Length];
        StagedSequence();
        if (!_destination.AsSpan().SequenceEqual(data)) throw new InvalidOperationException("Output differs");
    }
    /// <summary>Measures allocate and flatten for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true)]
    public int AllocateAndFlatten()
    {
        byte[] contiguous = _sequence.ToArray();
        return LuminPackSerializer.Deserialize(contiguous.AsSpan(), ref _destination);
    }
    /// <summary>Measures staged sequence for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public int StagedSequence() => LuminPackSerializer.Deserialize(_sequence, ref _destination);
}
