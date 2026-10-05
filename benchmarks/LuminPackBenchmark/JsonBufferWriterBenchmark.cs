using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Utility;

namespace LuminPackBenchmark;

[LuminPackable]
public partial class JsonOutputModel
{
    public int Id;
    public string Name = "";
    public int[] Values = [];
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 12)]
public class JsonBufferWriterBenchmark
{
    [Params(16, 30_000)] public int Length { get; set; }
    private JsonOutputModel _value = null!;
    private readonly ArrayBufferWriter<byte> _output = new(256 * 1024);
    private LuminBufferWriter _intermediate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _value = new JsonOutputModel { Id = 7, Name = new string('中', Length) + "😀\n", Values = Enumerable.Range(0, 100).ToArray() };
        _intermediate = LuminBufferWriterPool.Rent();
        BufferedCopy();
        byte[] expected = _output.WrittenSpan.ToArray();
        DirectOutput();
        if (!_output.WrittenSpan.SequenceEqual(expected)) throw new InvalidOperationException("Output differs");
    }

    [Benchmark(Baseline = true)]
    public int BufferedCopy()
    {
        _output.Clear();
        LuminPackSerializer.SerializeJson(_value, _intermediate);
        var bytes = _intermediate.GetSpan();
        bytes.CopyTo(_output.GetSpan(bytes.Length));
        _output.Advance(bytes.Length);
        return _output.WrittenCount;
    }

    [Benchmark]
    public int DirectOutput()
    {
        _output.Clear();
        LuminPackSerializer.SerializeJson(_value, _output);
        return _output.WrittenCount;
    }

    [GlobalCleanup] public void Cleanup() => LuminBufferWriterPool.Return(_intermediate);
}
