using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

[LuminPackable]
public sealed partial class BinaryStringBatch
{
    public string[] Values = Array.Empty<string>();
}

// CharacterSaveData is the existing mixed production-style graph; StringBatch
// isolates identifier-heavy traffic. UTF-16 is an unchanged-path control.
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 8, iterationCount: 10)]
[MemoryDiagnoser]
public class BinaryOptimizationBenchmark
{
    [Params(LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16)]
    public LuminPackStringEncoding Encoding { get; set; }
    private CharacterSaveData _character = null!;
    private BinaryStringBatch _strings = null!;
    private byte[] _characterBytes = null!;
    private byte[] _stringBytes = null!;
    private LuminBufferWriter _writer = null!;
    private LuminPackSerializerOption _option = null!;

    [GlobalSetup]
    public void Setup()
    {
        _option = new LuminPackSerializerOption { StringEncoding = Encoding };
        _writer = LuminBufferWriterPool.Rent();
        _writer.Option.StringEncoding = Encoding;
        _character = OptimizationBenchmark.CreateDeterministicData();
        _strings = new BinaryStringBatch
        {
            Values = Enumerable.Range(0, 512).Select(i => $"PLAYER_{i:D5}").ToArray()
        };
        _characterBytes = LuminPackSerializer.Serialize(_character, _option);
        _stringBytes = LuminPackSerializer.Serialize(_strings, _option);
        if (!LuminPackSerializer.Deserialize<BinaryStringBatch>(_stringBytes, _option).Values.SequenceEqual(_strings.Values))
            throw new InvalidOperationException("Binary string fixture did not roundtrip.");
        var copy = LuminPackSerializer.Deserialize<CharacterSaveData>(_characterBytes, _option);
        // Older revisions leave the second UTF-8 null/empty header word unwritten.
        // Compare every field, since those ignored bytes are not canonical on that baseline.
        if (LuminPackSerializer.SerializeJson(copy) != LuminPackSerializer.SerializeJson(_character))
            throw new InvalidOperationException("Binary character fixture changed its object graph.");
        Console.WriteLine($"{Encoding}: Character={_characterBytes.Length} B, StringBatch={_stringBytes.Length} B");
    }

    [GlobalCleanup] public void Cleanup() => LuminBufferWriterPool.Return(_writer);
    [Benchmark] public void CharacterWrite() => LuminPackSerializer.Serialize(_character, _writer);
    [Benchmark] public CharacterSaveData CharacterRead() => LuminPackSerializer.Deserialize<CharacterSaveData>(_characterBytes, _option);
    [Benchmark] public void StringBatchWrite() => LuminPackSerializer.Serialize(_strings, _writer);
    [Benchmark] public BinaryStringBatch StringBatchRead() => LuminPackSerializer.Deserialize<BinaryStringBatch>(_stringBytes, _option);
}
