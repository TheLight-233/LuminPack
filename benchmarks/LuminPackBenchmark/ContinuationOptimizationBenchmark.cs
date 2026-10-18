using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

/// <summary>Provides continuation optimization benchmark functionality.</summary>
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 2, warmupCount: 8, iterationCount: 10)]
[MemoryDiagnoser]
public class ContinuationOptimizationBenchmark
{
    private CharacterSaveData _data = null!;
    private byte[] _binary = null!;
    private byte[] _json = null!;
    private LuminBufferWriter _writer = null!;
    private static readonly BenchmarkProfile Strict = new() { ValidateJsonSyntax = true };

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _data = OptimizationBenchmark.CreateDeterministicData();
        _writer = LuminBufferWriterPool.Rent();
        _binary = LuminPackSerializer.Serialize(_data);
        string json = LuminPackSerializer.SerializeJson(_data);
        _json = Encoding.UTF8.GetBytes(json);
        if (LuminPackSerializer.SerializeJson(LuminPackSerializer.Deserialize<CharacterSaveData>(_binary)) != json ||
            LuminPackSerializer.SerializeJson(LuminPackSerializer.DeserializeJson<CharacterSaveData>(_json, Strict)) != json)
            throw new InvalidOperationException("Continuation benchmark lost object data.");
        Console.WriteLine($"Full graph: binary={_binary.Length}, JSON={_json.Length}");
    }

    /// <summary>Measures cleanup for the configured benchmark input.</summary>
    [GlobalCleanup] public void Cleanup() => LuminBufferWriterPool.Return(_writer);
    /// <summary>Measures binary write for the configured benchmark input.</summary>
    [Benchmark] public void BinaryWrite()
    {
        _writer.ResetCore();
        _writer.WriterState.ResetOperationState();
        LuminPackSerializer.Serialize(_data, _writer);
    }
    /// <summary>Measures binary read for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public CharacterSaveData BinaryRead() => LuminPackSerializer.Deserialize<CharacterSaveData>(_binary);
    /// <summary>Measures json write for the configured benchmark input.</summary>
    [Benchmark] public void JsonWrite()
    {
        _writer.ResetCore();
        _writer.WriterState.ResetOperationState();
        LuminPackSerializer.SerializeJson(_data, _writer);
    }
    /// <summary>Measures json write string for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public string JsonWriteString() => LuminPackSerializer.SerializeJson(_data);
    /// <summary>Measures json read for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public CharacterSaveData JsonRead() => LuminPackSerializer.DeserializeJson<CharacterSaveData>(_json);
    /// <summary>Measures json read strict for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public CharacterSaveData JsonReadStrict() => LuminPackSerializer.DeserializeJson<CharacterSaveData>(_json, Strict);
}

/// <summary>Provides medium string optimization benchmark functionality.</summary>
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 2, warmupCount: 8, iterationCount: 10)]
[MemoryDiagnoser]
public class MediumStringOptimizationBenchmark
{
    private BinaryStringBatch _data = null!;
    private byte[] _binary = null!;
    private byte[] _json = null!;
    private LuminBufferWriter _writer = null!;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _writer = LuminBufferWriterPool.Rent();
        _data = new BinaryStringBatch
        {
            Values = Enumerable.Range(0, 512).Select(i => $"PLAYER_{i:D5}_MEDIUM_ASCII").ToArray()
        };
        if (_data.Values.Any(static value => value.Length != 25))
            throw new InvalidOperationException("Medium-string fixture must have 25-character values.");
        _binary = LuminPackSerializer.Serialize(_data);
        _json = Encoding.UTF8.GetBytes(LuminPackSerializer.SerializeJson(_data));
        if (!LuminPackSerializer.Deserialize<BinaryStringBatch>(_binary).Values.SequenceEqual(_data.Values) ||
            !LuminPackSerializer.DeserializeJson<BinaryStringBatch>(_json).Values.SequenceEqual(_data.Values))
            throw new InvalidOperationException("Medium-string benchmark lost string data.");
        Console.WriteLine($"Medium strings: binary={_binary.Length}, JSON={_json.Length}");
    }

    /// <summary>Measures cleanup for the configured benchmark input.</summary>
    [GlobalCleanup] public void Cleanup() => LuminBufferWriterPool.Return(_writer);
    /// <summary>Measures binary write for the configured benchmark input.</summary>
    [Benchmark] public void BinaryWrite()
    {
        _writer.ResetCore();
        _writer.WriterState.ResetOperationState();
        LuminPackSerializer.Serialize(_data, _writer);
    }
    /// <summary>Measures binary read for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public BinaryStringBatch BinaryRead() => LuminPackSerializer.Deserialize<BinaryStringBatch>(_binary);
    /// <summary>Measures json write for the configured benchmark input.</summary>
    [Benchmark] public void JsonWrite()
    {
        _writer.ResetCore();
        _writer.WriterState.ResetOperationState();
        LuminPackSerializer.SerializeJson(_data, _writer);
    }
    /// <summary>Measures json read for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public BinaryStringBatch JsonRead() => LuminPackSerializer.DeserializeJson<BinaryStringBatch>(_json);
}
