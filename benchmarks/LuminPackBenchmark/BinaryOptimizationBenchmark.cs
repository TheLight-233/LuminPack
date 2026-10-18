using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

/// <summary>Provides binary string batch functionality.</summary>
[LuminPackable]
public sealed partial class BinaryStringBatch
{
    /// <summary>Stores the values.</summary>
    public string[] Values = Array.Empty<string>();
}

// CharacterSaveData is the existing mixed production-style graph; StringBatch
// isolates identifier-heavy traffic. UTF-16 is an unchanged-path control.
/// <summary>Provides binary optimization benchmark functionality.</summary>
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 8, iterationCount: 10)]
[MemoryDiagnoser]
public class BinaryOptimizationBenchmark
{
    /// <summary>Gets or sets the encoding.</summary>
    [Params(LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16)]
    public LuminPackStringEncoding Encoding { get; set; }
    private CharacterSaveData _character = null!;
    private BinaryStringBatch _strings = null!;
    private byte[] _characterBytes = null!;
    private byte[] _stringBytes = null!;
    private LuminBufferWriter _writer = null!;
    private BenchmarkProfile _option = null!;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _option = new BenchmarkProfile { StringEncoding = Encoding };
        _writer = LuminBufferWriterPool.Rent();
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

    /// <summary>Measures cleanup for the configured benchmark input.</summary>
    [GlobalCleanup] public void Cleanup() => LuminBufferWriterPool.Return(_writer);
    /// <summary>Measures character write for the configured benchmark input.</summary>
    [Benchmark] public void CharacterWrite()
    {
        _writer.ResetCore();
        _writer.WriterState.ResetOperationState();
        LuminPackSerializer.Serialize(_character, _writer, _option);
    }
    /// <summary>Measures character read for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public CharacterSaveData CharacterRead() => LuminPackSerializer.Deserialize<CharacterSaveData>(_characterBytes, _option);
    /// <summary>Measures string batch write for the configured benchmark input.</summary>
    [Benchmark] public void StringBatchWrite()
    {
        _writer.ResetCore();
        _writer.WriterState.ResetOperationState();
        LuminPackSerializer.Serialize(_strings, _writer, _option);
    }
    /// <summary>Measures string batch read for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public BinaryStringBatch StringBatchRead() => LuminPackSerializer.Deserialize<BinaryStringBatch>(_stringBytes, _option);
}
