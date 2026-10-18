using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Utility;

namespace LuminPackBenchmark;

/// <summary>Compares ordinary and guaranteed records in an independent benchmark assembly.</summary>
[MemoryDiagnoser, SimpleJob(RuntimeMoniker.Net10_0)]
public class CapacityTierBenchmark
{
    private List<GuaranteedCapacityRecord> _guaranteed = null!;
    private List<CheckedCapacityRecord> _checked = null!;
    private LuminBufferWriter _guaranteedBuffer = null!, _checkedBuffer = null!;
    private MemoryPack.Internal.ReusableLinkedArrayBufferWriter _memoryPackBuffer = null!;

    /// <summary>Initializes equal payloads and verifies exact LuminPack wire parity before measurement.</summary>
    [GlobalSetup]
    public void SetUp()
    {
        _guaranteed = Enumerable.Range(0, 4096).Select(index => new GuaranteedCapacityRecord
            { Id = index, Label = "value-" + index, Values = Enumerable.Range(index, 16).ToArray() }).ToList();
        _checked = _guaranteed.Select(value => new CheckedCapacityRecord { Id = value.Id, Label = value.Label, Values = value.Values }).ToList();
        _guaranteedBuffer = new LuminBufferWriter(true);
        _checkedBuffer = new LuminBufferWriter(true);
        _memoryPackBuffer = MemoryPack.Internal.ReusableLinkedArrayBufferWriterPool.Rent();
        LuminPackSerialize(); CheckedSerialize();
        if (!_guaranteedBuffer.GetSpan().SequenceEqual(_checkedBuffer.GetSpan()))
            throw new InvalidOperationException("The capacity guarantee changed the serialized representation.");
        var copy = LuminPackSerializer.Deserialize<List<GuaranteedCapacityRecord>>(_guaranteedBuffer.GetSpan());
        if (copy.Count != _guaranteed.Count || copy[^1].Values[^1] != _guaranteed[^1].Values[^1])
            throw new InvalidOperationException("The guaranteed record fixture lost data.");
    }

    /// <summary>Measures one aggregate reservation followed by check-free guaranteed records.</summary>
    [Benchmark]
    public void LuminPackSerialize() => LuminPackSerializer.Serialize(_guaranteed, _guaranteedBuffer);

    /// <summary>Measures the original per-element and nested collection checks on identical field data.</summary>
    [Benchmark(Baseline = true)]
    public void CheckedSerialize() => LuminPackSerializer.Serialize(_checked, _checkedBuffer);

    /// <summary>Measures the unchanged reference library over the same field values and reusable output.</summary>
    [Benchmark]
    public void MemoryPackSerialize()
    {
        _memoryPackBuffer.Reset();
        MemoryPack.MemoryPackSerializer.Serialize(_memoryPackBuffer, _checked);
    }

    /// <summary>Releases each existing native or managed reusable writer.</summary>
    [GlobalCleanup]
    public void CleanUp()
    {
        _guaranteedBuffer.Dispose(); _checkedBuffer.Dispose();
        _memoryPackBuffer.Reset();
        MemoryPack.Internal.ReusableLinkedArrayBufferWriterPool.Return(_memoryPackBuffer);
    }
}

/// <summary>Declares a complete bound including headers, fixed storage and the longest fixture string in every encoding.</summary>
[LuminPackable, LuminPackBounded(128)]
public sealed class GuaranteedCapacityRecord
{
    public int Id;
    public string Label = "";
    public int[] Values = [];
}

/// <summary>Retains the same unbounded field contract as the guaranteed fixture.</summary>
[LuminPackable, MemoryPack.MemoryPackable]
public sealed partial class CheckedCapacityRecord
{
    public int Id;
    public string Label = "";
    public int[] Values = [];
}
