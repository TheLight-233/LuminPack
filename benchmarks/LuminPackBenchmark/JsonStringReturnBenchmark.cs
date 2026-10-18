using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackBenchmark;

/// <summary>Provides cjk doc functionality.</summary>
[LuminPackable]
public sealed partial class CjkDoc
{
    /// <summary>Stores the title.</summary>
    public string Title = "";
    /// <summary>Stores the description.</summary>
    public string Description = "";
    /// <summary>Stores the level.</summary>
    public int Level;
    /// <summary>Stores the tags.</summary>
    public string[] Tags = System.Array.Empty<string>();
}

// Measures the string-returning JSON API under both internal encodings. The
// returned .NET string is identical for valid input; UTF-16 output avoids the
// UTF-8 transcode and the UTF-8 -> UTF-16 decode but produces two-byte output.
/// <summary>Provides json string return benchmark functionality.</summary>
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 6, iterationCount: 12)]
[HideColumns("StdDev", "RatioSD", "Error")]
[MemoryDiagnoser]
public class JsonStringReturnBenchmark
{
    private static readonly BenchmarkProfile Utf8Option = new() { StringEncoding = LuminPackStringEncoding.UTF8 };
    private static readonly BenchmarkProfile Utf16Option = new() { StringEncoding = LuminPackStringEncoding.UTF16 };

    private CjkDoc _cjk = null!;
    private CharacterSaveData _ascii = null!;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _cjk = new CjkDoc
        {
            Title = "\u6781\u81F4\u6027\u80FD\u5E8F\u5217\u5316\u6846\u67B6\u4E2D\u6587\u6D4B\u8BD5",
            Description = string.Concat(Enumerable.Repeat("\u4E2D\u6587\u5E8F\u5217\u5316\u6027\u80FD\u6D4B\u8BD5\uFF0C\u7F51\u7EDC\u540C\u6B65\u4E0E\u8D44\u6E90\u52A0\u8F7D\u3002", 128)),
            Level = 42,
            Tags = new[] { "\u4E2D\u6587\u6807\u7B7E\u4E00", "\u4E2D\u6587\u6807\u7B7E\u4E8C", "\u4E2D\u6587\u6807\u7B7E\u4E09", "\u4E2D\u6587\u6807\u7B7E\u56DB" },
        };
        _ascii = CharacterSaveData.Create();

        string utf8 = LuminPackSerializer.SerializeJson(_cjk, Utf8Option);
        string utf16 = LuminPackSerializer.SerializeJson(_cjk, Utf16Option);
        if (utf8 != utf16)
            throw new InvalidOperationException("UTF-8 and UTF-16 JSON strings differ for valid input.");
    }

    /// <summary>Measures cjk utf8 for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true)] public string Cjk_Utf8() => LuminPackSerializer.SerializeJson(_cjk, Utf8Option);
    /// <summary>Measures cjk utf16 for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public string Cjk_Utf16() => LuminPackSerializer.SerializeJson(_cjk, Utf16Option);
    /// <summary>Measures ascii utf8 for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public string Ascii_Utf8() => LuminPackSerializer.SerializeJson(_ascii, Utf8Option);
    /// <summary>Measures ascii utf16 for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public string Ascii_Utf16() => LuminPackSerializer.SerializeJson(_ascii, Utf16Option);
}
