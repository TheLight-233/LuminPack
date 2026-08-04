using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 5, printSource: true, printInstructionAddresses: true)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BufferWriterOperationContextBenchmark
{
    [ThreadStatic] private static LuminPackWriterOptionalState? t_legacyWriterState;
    [ThreadStatic] private static LuminPackReaderOptionalState? t_legacyReaderState;

    private readonly LuminPackSerializerOption _customOption = new()
    {
        StringEncoding = LuminPackStringEncoding.UTF16,
        StringRecording = LuminPackStringRecording.Length
    };

    private readonly ContextSmall _small = new() { Id = 42, Active = true };
    private readonly ContextMedium _medium = new()
    {
        Id = 42,
        Name = "BufferWriter operation context benchmark 😀",
        Values = Enumerable.Range(0, 32).ToArray()
    };
    private readonly ContextNested _nested = new()
    {
        Name = "root",
        Child = new ContextMedium
        {
            Id = 84,
            Name = "nested 中文",
            Values = Enumerable.Range(0, 64).ToArray()
        }
    };

    private LuminBufferWriter _legacyBuffer = null!;
    private LuminBufferWriter _fastBuffer = null!;
    private LuminBufferWriter _fastCustomBuffer = null!;
    private LuminBufferWriter _fastReadBuffer = null!;
    private byte[] _smallPayload = null!;

    [GlobalSetup]
    public void Setup()
    {
        _legacyBuffer = new LuminBufferWriter(true);
        _fastBuffer = new LuminBufferWriter(true);
        _fastCustomBuffer = new LuminBufferWriter(true);
        _fastReadBuffer = new LuminBufferWriter(true);
        _fastCustomBuffer.Option.StringEncoding = LuminPackStringEncoding.UTF16;
        _fastCustomBuffer.Option.StringRecording = LuminPackStringRecording.Length;

        _smallPayload = LuminPackSerializer.Serialize(_small);
        LuminPackSerializer.Serialize(_small, _fastReadBuffer);

        LegacySerialize(_small, _legacyBuffer, null);
        LuminPackSerializer.Serialize(_small, _fastBuffer);
        LegacyDeserialize(_smallPayload);
        _ = LuminPackSerializer.Deserialize<ContextSmall>(_fastReadBuffer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _legacyBuffer.ResetCore();
        _legacyBuffer.Dispose();
        _fastBuffer.Dispose();
        _fastCustomBuffer.Dispose();
        _fastReadBuffer.Dispose();
        t_legacyWriterState?.Reset();
        t_legacyReaderState?.Reset();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SerializeSmallDefault")]
    public void LegacySerializeSmallDefault() => LegacySerialize(_small, _legacyBuffer, null);

    [Benchmark]
    [BenchmarkCategory("SerializeSmallDefault")]
    public void BufferContextSerializeSmallDefault() => LuminPackSerializer.Serialize(_small, _fastBuffer);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SerializeSmallCustom")]
    public void LegacySerializeSmallCustom() => LegacySerialize(_small, _legacyBuffer, _customOption);

    [Benchmark]
    [BenchmarkCategory("SerializeSmallCustom")]
    public void BufferContextSerializeSmallCustom() => LuminPackSerializer.Serialize(_small, _fastCustomBuffer);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SerializeMedium")]
    public void LegacySerializeMedium() => LegacySerialize(_medium, _legacyBuffer, null);

    [Benchmark]
    [BenchmarkCategory("SerializeMedium")]
    public void BufferContextSerializeMedium() => LuminPackSerializer.Serialize(_medium, _fastBuffer);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SerializeNested")]
    public void LegacySerializeNested() => LegacySerialize(_nested, _legacyBuffer, null);

    [Benchmark]
    [BenchmarkCategory("SerializeNested")]
    public void BufferContextSerializeNested() => LuminPackSerializer.Serialize(_nested, _fastBuffer);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DeserializeSmall")]
    public ContextSmall? LegacyDeserializeSmall() => LegacyDeserialize(_smallPayload);

    [Benchmark]
    [BenchmarkCategory("DeserializeSmall")]
    public ContextSmall? BufferContextDeserializeSmall() =>
        LuminPackSerializer.Deserialize<ContextSmall>(_fastReadBuffer);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LegacySerialize<T>(T? value, LuminBufferWriter buffer, LuminPackSerializerOption? option)
    {
        var state = t_legacyWriterState ??= new LuminPackWriterOptionalState();
        state.Init(option);
        try
        {
            var writer = new LuminPackWriter(buffer, state);
            writer.WriteValue(value);
        }
        finally
        {
            state.Reset();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ContextSmall? LegacyDeserialize(ReadOnlySpan<byte> payload)
    {
        var state = t_legacyReaderState ??= new LuminPackReaderOptionalState();
        state.Init(null);
        try
        {
            var reader = new LuminPackReader(ref payload, state);
            return reader.ReadValue<ContextSmall>();
        }
        finally
        {
            state.Reset();
        }
    }
}

[LuminPackable]
public partial class ContextSmall
{
    public int Id { get; set; }
    public bool Active { get; set; }
}

[LuminPackable]
public partial class ContextMedium
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int[]? Values { get; set; }
}

[LuminPackable]
public partial class ContextNested
{
    public string? Name { get; set; }
    public ContextMedium? Child { get; set; }
}
