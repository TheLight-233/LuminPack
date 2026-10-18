using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Utility;

namespace LuminPackBenchmark;

/// <summary>Provides json output model functionality.</summary>
[LuminPackable]
public partial class JsonOutputModel
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the name.</summary>
    public string Name = "";
    /// <summary>Stores the values.</summary>
    public int[] Values = [];
}

/// <summary>Provides json buffer writer benchmark functionality.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 12)]
public class JsonBufferWriterBenchmark
{
    /// <summary>Gets or sets the length.</summary>
    [Params(16, 30_000)] public int Length { get; set; }
    private JsonOutputModel _value = null!;
    private readonly ArrayBufferWriter<byte> _output = new(256 * 1024);
    private LuminBufferWriter _intermediate = null!;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _value = new JsonOutputModel { Id = 7, Name = new string('\u4e2d', Length) + "😀\n", Values = Enumerable.Range(0, 100).ToArray() };
        _intermediate = LuminBufferWriterPool.Rent();
        BufferedCopy();
        byte[] expected = _output.WrittenSpan.ToArray();
        DirectOutput();
        if (!_output.WrittenSpan.SequenceEqual(expected)) throw new InvalidOperationException("Output differs");
    }

    /// <summary>Measures buffered copy for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true)]
    public int BufferedCopy()
    {
        _output.Clear();
        _intermediate.ResetCore();
        _intermediate.WriterState.ResetOperationState();
        LuminPackSerializer.SerializeJson(_value, _intermediate);
        var bytes = _intermediate.GetSpan();
        bytes.CopyTo(_output.GetSpan(bytes.Length));
        _output.Advance(bytes.Length);
        return _output.WrittenCount;
    }

    /// <summary>Measures direct output for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int DirectOutput()
    {
        _output.Clear();
        LuminPackSerializer.SerializeJson(_value, _output);
        return _output.WrittenCount;
    }

    /// <summary>Measures cleanup for the configured benchmark input.</summary>
    [GlobalCleanup] public void Cleanup() => LuminBufferWriterPool.Return(_intermediate);
}
