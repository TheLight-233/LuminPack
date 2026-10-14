using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 8, iterationCount: 10)]
[MemoryDiagnoser]
public class BinaryFallbackOptimizationBenchmark
{
    [Params("Unicode", "LongAscii")]
    public string Shape { get; set; } = "Unicode";
    private BinaryStringBatch _value = null!;
    private byte[] _bytes = null!;
    private LuminBufferWriter _writer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _writer = LuminBufferWriterPool.Rent();
        _writer.Option.StringEncoding = LuminPackStringEncoding.UTF8;
        _value = new BinaryStringBatch
        {
            Values = Enumerable.Range(0, 512).Select(i => Shape == "Unicode"
                ? $"玩家_{i:D5}_😀" : new string('x', 64) + i.ToString("D5")).ToArray()
        };
        _bytes = LuminPackSerializer.Serialize(_value);
        if (!LuminPackSerializer.Deserialize<BinaryStringBatch>(_bytes).Values.SequenceEqual(_value.Values))
            throw new InvalidOperationException("Binary fallback fixture changed string values.");
    }

    [GlobalCleanup] public void Cleanup() => LuminBufferWriterPool.Return(_writer);
    [Benchmark] public void Write() => LuminPackSerializer.Serialize(_value, _writer);
    [Benchmark] public BinaryStringBatch Read() => LuminPackSerializer.Deserialize<BinaryStringBatch>(_bytes);
}
