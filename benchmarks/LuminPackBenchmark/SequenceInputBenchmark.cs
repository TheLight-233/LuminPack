using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;

namespace LuminPackBenchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 12)]
public class SequenceInputBenchmark
{
    private ReadOnlySequence<byte> _sequence;
    private byte[] _destination = null!;
    [Params(1024, 65_536)] public int Length { get; set; }
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
    [GlobalSetup] public void Setup()
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
    [Benchmark(Baseline = true)] public int AllocateAndFlatten()
    {
        byte[] contiguous = _sequence.ToArray();
        return LuminPackSerializer.Deserialize(contiguous.AsSpan(), ref _destination);
    }
    [Benchmark] public int StagedSequence() => LuminPackSerializer.Deserialize(_sequence, ref _destination);
}
