using System.Buffers;
using BenchmarkDotNet.Attributes;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPack.Utility;
using MemoryPack;
using Nino.Core;

namespace LuminPackBenchmark;

/// <summary>Provides a fixed payload large enough to expose intermediate struct copies.</summary>
[LuminPackable, MemoryPackable, NinoType]
public partial struct DictionaryRawPayload
{
    /// <summary>Stores the identity and first payload lane.</summary>
    public long A;
    /// <summary>Stores the second payload lane.</summary>
    public long B;
    /// <summary>Stores the third payload lane.</summary>
    public long C;
    /// <summary>Stores the fourth payload lane.</summary>
    public long D;
}

/// <summary>Measures independently raw keys and values with matching UTF-8 strings and reusable destinations.</summary>
[MemoryDiagnoser]
public class DictionaryRawSideBenchmark
{
    private readonly Dictionary<DictionaryRawPayload, string> _keys = new();
    private readonly Dictionary<string, DictionaryRawPayload> _values = new();
    private readonly LuminBufferWriter _lumin = LuminBufferWriterPool.Rent();
    private readonly ArrayBufferWriter<byte> _memory = new(65536);
    private readonly NinoArrayBufferWriter _nino = new(65536);
    private readonly BenchmarkProfile _option = new() { StringEncoding = LuminPackStringEncoding.UTF8 };

    /// <summary>Creates deterministic dense entries and validates both mixed dictionary shapes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int index = 0; index < 64; index++)
        {
            var raw = new DictionaryRawPayload { A = index, B = index * 17, C = -index, D = index * 31 };
            string text = "entry-" + index.ToString("D4");
            _keys.Add(raw, text);
            _values.Add(text, raw);
        }
        var keys = LuminPackSerializer.Deserialize<Dictionary<DictionaryRawPayload, string>>(LuminPackSerializer.Serialize(_keys, _option), _option);
        var values = LuminPackSerializer.Deserialize<Dictionary<string, DictionaryRawPayload>>(LuminPackSerializer.Serialize(_values, _option), _option);
        if (!keys.SequenceEqual(_keys) || !values.SequenceEqual(_values))
            throw new InvalidOperationException("A mixed dictionary fixture lost data.");
    }

    /// <summary>Returns the reusable LuminPack destination.</summary>
    [GlobalCleanup]
    public void Cleanup() => _lumin.Dispose();

    /// <summary>Writes a raw key followed by a string value.</summary>
    [Benchmark]
    public void LuminPackKey()
    {
        _lumin.ResetCore();
        _lumin.WriterState.ResetOperationState();
        LuminPackSerializer.Serialize(_keys, _lumin);
    }

    /// <summary>Writes the same key fixture using MemoryPack and explicit destination reset.</summary>
    [Benchmark]
    public void MemoryPackKey()
    {
        _memory.ResetWrittenCount();
        MemoryPackSerializer.Serialize(_memory, _keys, MemoryPackSerializerOptions.Utf8);
    }

    /// <summary>Writes the same key fixture using Nino and explicit destination reset.</summary>
    [Benchmark]
    public void NinoKey()
    {
        _nino.ResetWrittenCount();
        NinoSerializer.Serialize(_keys, _nino);
    }

    /// <summary>Writes a string key followed by a raw value.</summary>
    [Benchmark]
    public void LuminPackValue()
    {
        _lumin.ResetCore();
        _lumin.WriterState.ResetOperationState();
        LuminPackSerializer.Serialize(_values, _lumin);
    }

    /// <summary>Writes the same value fixture using MemoryPack and explicit destination reset.</summary>
    [Benchmark]
    public void MemoryPackValue()
    {
        _memory.ResetWrittenCount();
        MemoryPackSerializer.Serialize(_memory, _values, MemoryPackSerializerOptions.Utf8);
    }

    /// <summary>Writes the same value fixture using Nino and explicit destination reset.</summary>
    [Benchmark]
    public void NinoValue()
    {
        _nino.ResetWrittenCount();
        NinoSerializer.Serialize(_values, _nino);
    }
}
