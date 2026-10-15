using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

/// <summary>Provides binary fallback optimization benchmark functionality.</summary>
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 8, iterationCount: 10)]
[MemoryDiagnoser]
public class BinaryFallbackOptimizationBenchmark
{
    /// <summary>Gets or sets the shape.</summary>
    [Params("Unicode", "LongAscii")]
    public string Shape { get; set; } = "Unicode";
    private BinaryStringBatch _value = null!;
    private byte[] _bytes = null!;
    private LuminBufferWriter _writer = null!;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _writer = LuminBufferWriterPool.Rent();
        _writer.Option.StringEncoding = LuminPackStringEncoding.UTF8;
        _value = new BinaryStringBatch
        {
            Values = Enumerable.Range(0, 512).Select(i => Shape == "Unicode"
                ? $"\u73a9\u5bb6_{i:D5}_😀" : new string('x', 64) + i.ToString("D5")).ToArray()
        };
        _bytes = LuminPackSerializer.Serialize(_value);
        if (!LuminPackSerializer.Deserialize<BinaryStringBatch>(_bytes).Values.SequenceEqual(_value.Values))
            throw new InvalidOperationException("Binary fallback fixture changed string values.");
    }

    /// <summary>Measures cleanup for the configured benchmark input.</summary>
    [GlobalCleanup] public void Cleanup() => LuminBufferWriterPool.Return(_writer);
    /// <summary>Writes  to the current output.</summary>
    [Benchmark] public void Write() => LuminPackSerializer.Serialize(_value, _writer);
    /// <summary>Reads  from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public BinaryStringBatch Read() => LuminPackSerializer.Deserialize<BinaryStringBatch>(_bytes);
}
