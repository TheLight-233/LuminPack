using System.Buffers;
using BenchmarkDotNet.Attributes;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPack.Utility;
using MemoryPack;
using Nino.Core;

namespace LuminPackBenchmark;

/// <summary>Measures both fixed-case wins and managed-case fallback costs on the same Union family.</summary>
[MemoryDiagnoser]
public class UnionCursorBenchmark
{
    private readonly Dictionary<int, CursorBenchmarkRoot> _data = new();
    private readonly LuminBufferWriter _lumin = new(true);
    private readonly ArrayBufferWriter<byte> _memory = new(65536);
    private readonly NinoArrayBufferWriter _nino = new(65536);

    /// <summary>Gets or sets the percentage of entries using the fixed case.</summary>
    [Params(0, 25, 100)]
    public int FixedPercent { get; set; }

    /// <summary>Creates identical logical values and validates all reference-library round trips.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _lumin.Option.StringEncoding = LuminPackStringEncoding.UTF8;
        for (int index = 0; index < 32; index++)
            _data.Add(index, index < 32 * FixedPercent / 100
                ? new CursorFixedPayload { Number = index * 7 }
                : new CursorTextPayload { Value = "managed-union-entry-" + index.ToString("D2") });
        string expected = Describe(_data);
        if (Describe(LuminPackSerializer.Deserialize<Dictionary<int, CursorBenchmarkRoot>>(Snapshot())) != expected ||
            Describe(MemoryPackSerializer.Deserialize<Dictionary<int, CursorBenchmarkRoot>>(
                MemoryPackSerializer.Serialize(_data, MemoryPackSerializerOptions.Utf8), MemoryPackSerializerOptions.Utf8)!) != expected ||
            Describe(NinoDeserializer.Deserialize<Dictionary<int, CursorBenchmarkRoot>>(NinoSerializer.Serialize(_data))) != expected)
            throw new InvalidOperationException("A Union distribution lost its case or field values.");
    }

    /// <summary>Releases the caller-owned native destination after measurement.</summary>
    [GlobalCleanup]
    public void Cleanup() => _lumin.Dispose();

    /// <summary>Captures deterministic bytes outside the timed operations for cross-image comparisons.</summary>
    /// <returns>The complete LuminPack dictionary payload.</returns>
    public byte[] Snapshot() => LuminPackSerializer.Serialize(_data);

    /// <summary>Writes the selected Union distribution using the existing reusable destination.</summary>
    [Benchmark]
    public void LuminPackWrite() => LuminPackSerializer.Serialize(_data, _lumin);

    /// <summary>Writes the same UTF-8 values with an explicitly reset MemoryPack destination.</summary>
    [Benchmark]
    public void MemoryPackWrite()
    {
        _memory.ResetWrittenCount();
        MemoryPackSerializer.Serialize(_memory, _data, MemoryPackSerializerOptions.Utf8);
    }

    /// <summary>Writes the same values with an explicitly reset Nino destination.</summary>
    [Benchmark]
    public void NinoWrite()
    {
        _nino.ResetWrittenCount();
        NinoSerializer.Serialize(_data, _nino);
    }

    /// <summary>Describes logical data independently of the libraries' wire representations.</summary>
    /// <param name="values">The ordered dictionary to validate.</param>
    /// <returns>Its keys, case identities and complete field values.</returns>
    private static string Describe(Dictionary<int, CursorBenchmarkRoot> values) => string.Join("|", values.Select(pair => pair.Key + ":" +
        (pair.Value is CursorFixedPayload fixedValue ? "fixed:" + fixedValue.Number : "text:" + ((CursorTextPayload)pair.Value).Value)));
}

/// <summary>Contains both a raw fixed case and a managed fallback case.</summary>
[LuminPackable, MemoryPackable, NinoType]
[LuminPackUnion(1, typeof(CursorFixedPayload)), LuminPackUnion(2, typeof(CursorTextPayload))]
[MemoryPackUnion(1, typeof(CursorFixedPayload)), MemoryPackUnion(2, typeof(CursorTextPayload))]
public abstract partial class CursorBenchmarkRoot { }

/// <summary>Provides the callback-free fixed Union payload.</summary>
[LuminPackable, MemoryPackable, NinoType]
public sealed partial class CursorFixedPayload : CursorBenchmarkRoot
{
    /// <summary>Stores the serialized fixed value.</summary>
    public int Number;
}

/// <summary>Exercises the original managed formatter fallback within the same dictionary loop.</summary>
[LuminPackable, MemoryPackable, NinoType]
public sealed partial class CursorTextPayload : CursorBenchmarkRoot
{
    /// <summary>Stores the variable-length UTF-8 field.</summary>
    public string Value = string.Empty;
}
