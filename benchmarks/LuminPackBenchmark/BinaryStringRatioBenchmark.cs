using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Option;

namespace LuminPackBenchmark;

[LuminPack.Attribute.LuminPackable, MemoryPack.MemoryPackable]
public sealed partial class BinaryRatioStrings
{
    public string?[] Values = [];
}

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 32, iterationCount: 10)]
[InvocationCount(65536)]
[MemoryDiagnoser, CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[Config(typeof(CharacterRatioBuildConfig))]
public class BinaryStringRatioBenchmark
{
    [Params("Short", "Medium", "Long", "Unicode", "Utf16")]
    public string Shape { get; set; } = "Short";
    private BinaryRatioStrings _data = null!;
    private byte[] _lp = null!, _mp = null!;
    private LuminPackSerializerOption _option = null!;
    private MemoryPack.MemoryPackSerializerOptions _mpOption = null!;

    [GlobalSetup]
    public void Setup()
    {
        _option = new() { StringEncoding = Shape == "Utf16" ? LuminPackStringEncoding.UTF16 : LuminPackStringEncoding.UTF8 };
        _mpOption = Shape == "Utf16" ? MemoryPack.MemoryPackSerializerOptions.Utf16 : MemoryPack.MemoryPackSerializerOptions.Utf8;
        _data = new() { Values = Enumerable.Range(0, 512).Select(i => Shape switch
        {
            "Medium" => $"PLAYER_{i:D5}_SESSION_ACTIVE",
            "Long" => new string('a', 64) + i.ToString("D5"),
            "Unicode" => $"角色_{i:D5}_装备_🌟",
            _ => $"PLAYER_{i:D5}"
        }).ToArray() };
        _lp = LuminPackSerializer.Serialize(_data, _option);
        _mp = MemoryPack.MemoryPackSerializer.Serialize(_data, _mpOption);
        if (!LuminPackRead().Values.SequenceEqual(_data.Values) || !MemoryPackRead().Values.SequenceEqual(_data.Values))
            throw new InvalidOperationException("String ratio fixture lost data.");
        Console.WriteLine($"String ratio {Shape}: LP={_lp.Length}, MP={_mp.Length} bytes.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("WriteArray")]
    public byte[] MemoryPackWrite() => MemoryPack.MemoryPackSerializer.Serialize(_data, _mpOption);
    [Benchmark, BenchmarkCategory("WriteArray")]
    public byte[] LuminPackWrite() => LuminPackSerializer.Serialize(_data, _option);
    [Benchmark(Baseline = true), BenchmarkCategory("ReadNewObject")]
    public BinaryRatioStrings MemoryPackRead() => MemoryPack.MemoryPackSerializer.Deserialize<BinaryRatioStrings>(_mp, _mpOption)!;
    [Benchmark, BenchmarkCategory("ReadNewObject")]
    public BinaryRatioStrings LuminPackRead() => LuminPackSerializer.Deserialize<BinaryRatioStrings>(_lp, _option);
}
