using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPackBenchmark;

// Isolates the formatted-scalar change: both rows run the same writer code path
// and differ only in whether the text comes from an allocated string or from a
// stack buffer.
/// <summary>Provides json formatted scalar benchmark functionality.</summary>
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 6, iterationCount: 12)]
[HideColumns("StdDev", "RatioSD", "Error")]
[MemoryDiagnoser]
public class JsonFormattedScalarBenchmark
{
    private const int Count = 1000;

    private static readonly BenchmarkProfile Utf8Option = new() { StringEncoding = LuminPackStringEncoding.UTF8 };
    private static readonly BenchmarkProfile Utf16Option = new() { StringEncoding = LuminPackStringEncoding.UTF16 };

    private DateTimeOffset[] _offsets = null!;
    private DateTime[] _dates = null!;
    private Guid[] _guids = null!;
    private TimeSpan[] _spans = null!;
    private byte[] _buffer = null!;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _offsets = Enumerable.Range(0, Count)
            .Select(i => DateTimeOffset.FromUnixTimeSeconds(1_700_000_000 + i).ToOffset(TimeSpan.FromHours(8)))
            .ToArray();
        _dates = Enumerable.Range(0, Count)
            .Select(i => new DateTime(2023, 11, 15, 6, 13, 20, DateTimeKind.Utc).AddSeconds(i))
            .ToArray();
        _guids = Enumerable.Range(0, Count)
            .Select(i => Guid.Parse(i.ToString("D8") + "-89ab-cdef-0123-456789abcdef"))
            .ToArray();
        _spans = Enumerable.Range(0, Count).Select(i => TimeSpan.FromMilliseconds(i * 1.5)).ToArray();
        // Sized for the widest payload in both encodings: UTF-16 needs two
        // bytes per character, so the UTF-8-sized buffer is doubled.
        _buffer = GC.AllocateUninitializedArray<byte>(Count * 96 + 64);
    }

    /// <summary>Measures date time offset to string then write string for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true)]
    public int DateTimeOffset_ToStringThenWriteString()
    {
        var state = new LuminPackWriterOptionalState();
        Span<byte> buffer = _buffer;
        var writer = new LuminPackJsonWriter(ref buffer, state);
        foreach (var value in _offsets)
            writer.WriteString<LuminPackDefaultOptions>(value.ToString("O"));
        return writer.CurrentIndex;
    }

    /// <summary>Measures date time offset direct format for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int DateTimeOffset_DirectFormat()
    {
        var state = new LuminPackWriterOptionalState();
        Span<byte> buffer = _buffer;
        var writer = new LuminPackJsonWriter(ref buffer, state);
        foreach (var value in _offsets)
            writer.WriteDateTimeOffset(value);
        return writer.CurrentIndex;
    }

    /// <summary>Measures date time to string then write string for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int DateTime_ToStringThenWriteString()
    {
        var state = new LuminPackWriterOptionalState();
        Span<byte> buffer = _buffer;
        var writer = new LuminPackJsonWriter(ref buffer, state);
        foreach (var value in _dates)
            writer.WriteString<LuminPackDefaultOptions>(value.ToString("O"));
        return writer.CurrentIndex;
    }

    /// <summary>Measures date time direct format for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int DateTime_DirectFormat()
    {
        var state = new LuminPackWriterOptionalState();
        Span<byte> buffer = _buffer;
        var writer = new LuminPackJsonWriter(ref buffer, state);
        foreach (var value in _dates)
            writer.WriteDateTime(value);
        return writer.CurrentIndex;
    }

    /// <summary>Measures guid to string then write string for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int Guid_ToStringThenWriteString()
    {
        var state = new LuminPackWriterOptionalState();
        Span<byte> buffer = _buffer;
        var writer = new LuminPackJsonWriter(ref buffer, state);
        foreach (var value in _guids)
            writer.WriteString<LuminPackDefaultOptions>(value.ToString());
        return writer.CurrentIndex;
    }

    /// <summary>Measures guid direct format for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int Guid_DirectFormat()
    {
        var state = new LuminPackWriterOptionalState();
        Span<byte> buffer = _buffer;
        var writer = new LuminPackJsonWriter(ref buffer, state);
        foreach (var value in _guids)
            writer.WriteGuid(value);
        return writer.CurrentIndex;
    }

    /// <summary>Measures time span to string then write string for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int TimeSpan_ToStringThenWriteString()
    {
        var state = new LuminPackWriterOptionalState();
        Span<byte> buffer = _buffer;
        var writer = new LuminPackJsonWriter(ref buffer, state);
        foreach (var value in _spans)
            writer.WriteString<LuminPackDefaultOptions>(value.ToString());
        return writer.CurrentIndex;
    }

    /// <summary>Measures time span direct format for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int TimeSpan_DirectFormat()
    {
        var state = new LuminPackWriterOptionalState();
        Span<byte> buffer = _buffer;
        var writer = new LuminPackJsonWriter(ref buffer, state);
        foreach (var value in _spans)
            writer.WriteTimeSpan(value);
        return writer.CurrentIndex;
    }

    /// <summary>Measures date time offset direct format utf16 for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int DateTimeOffset_DirectFormat_Utf16()
    {
        var state = new LuminPackWriterOptionalState();
        Span<byte> buffer = _buffer;
        var writer = new LuminPackJsonWriter(ref buffer, state);
        foreach (var value in _offsets)
            writer.WriteDateTimeOffset<LuminPackUtf16LengthOptions>(value);
        return writer.CurrentIndex;
    }
}
