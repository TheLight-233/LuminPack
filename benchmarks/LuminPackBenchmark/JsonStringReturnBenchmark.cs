using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackBenchmark;

[LuminPackable]
public sealed partial class CjkDoc
{
    public string Title = "";
    public string Description = "";
    public int Level;
    public string[] Tags = System.Array.Empty<string>();
}

// Measures the string-returning JSON API under both internal encodings. The
// returned .NET string is identical for valid input; UTF-16 output avoids the
// UTF-8 transcode and the UTF-8 -> UTF-16 decode but produces two-byte output.
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 6, iterationCount: 12)]
[HideColumns("StdDev", "RatioSD", "Error")]
[MemoryDiagnoser]
public class JsonStringReturnBenchmark
{
    private static readonly LuminPackSerializerOption Utf8Option = new() { StringEncoding = LuminPackStringEncoding.UTF8 };
    private static readonly LuminPackSerializerOption Utf16Option = new() { StringEncoding = LuminPackStringEncoding.UTF16 };

    private CjkDoc _cjk = null!;
    private CharacterSaveData _ascii = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cjk = new CjkDoc
        {
            Title = "极致性能序列化框架中文测试",
            Description = string.Concat(Enumerable.Repeat("中文序列化性能测试，网络同步与资源加载。", 128)),
            Level = 42,
            Tags = new[] { "中文标签一", "中文标签二", "中文标签三", "中文标签四" },
        };
        _ascii = CharacterSaveData.Create();

        string utf8 = LuminPackSerializer.SerializeJson(_cjk, Utf8Option);
        string utf16 = LuminPackSerializer.SerializeJson(_cjk, Utf16Option);
        if (utf8 != utf16)
            throw new InvalidOperationException("UTF-8 and UTF-16 JSON strings differ for valid input.");
    }

    [Benchmark(Baseline = true)] public string Cjk_Utf8() => LuminPackSerializer.SerializeJson(_cjk, Utf8Option);
    [Benchmark] public string Cjk_Utf16() => LuminPackSerializer.SerializeJson(_cjk, Utf16Option);
    [Benchmark] public string Ascii_Utf8() => LuminPackSerializer.SerializeJson(_ascii, Utf8Option);
    [Benchmark] public string Ascii_Utf16() => LuminPackSerializer.SerializeJson(_ascii, Utf16Option);
}
