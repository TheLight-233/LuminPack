using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Option;

namespace LuminPackBenchmark;

// Serial builds also work on hosts that cannot start MSBuild worker nodes.
// This changes the build process, not the runtime benchmark job.
public sealed class CharacterRatioBuildConfig : ManualConfig
{
    public CharacterRatioBuildConfig() => AddJob(Job.Default
        .WithMsBuildArguments("/m:1", "/p:NuGetAudit=false", "/p:RestoreIgnoreFailedSources=true")
        .AsMutator());
}

public abstract class CharacterRatioFixture
{
    protected CharacterSaveData Data = null!;
    protected byte[] LuminBinary = null!, MemoryBinary = null!, LuminJson = null!, StjJson = null!;
    protected static readonly LuminPackSerializerOption Strict = new() { ValidateJsonSyntax = true };
    private protected static readonly OptimizationJsonContext Context = new(new JsonSerializerOptions
    {
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultBufferSize = 64 * 1024
    });

    [GlobalSetup]
    public void Setup()
    {
        Data = OptimizationBenchmark.CreateDeterministicData();
        LuminBinary = LuminPackSerializer.Serialize(Data);
        MemoryBinary = MemoryPack.MemoryPackSerializer.Serialize(Data);
        string expected = LuminPackSerializer.SerializeJson(Data);
        LuminJson = Encoding.UTF8.GetBytes(expected);
        StjJson = JsonSerializer.SerializeToUtf8Bytes(Data, Context.CharacterSaveData);
        foreach (var copy in new[] {
            LuminPackSerializer.Deserialize<CharacterSaveData>(LuminBinary),
            MemoryPack.MemoryPackSerializer.Deserialize<CharacterSaveData>(MemoryBinary)!,
            LuminPackSerializer.DeserializeJson<CharacterSaveData>(LuminJson, Strict),
            JsonSerializer.Deserialize(StjJson, Context.CharacterSaveData)! })
        {
            if (LuminPackSerializer.SerializeJson(copy) != expected)
                throw new InvalidOperationException("Ratio fixture lost object data.");
        }
        Console.WriteLine($"Ratio fixture: LP binary={LuminBinary.Length}, MP binary={MemoryBinary.Length}, LP JSON={LuminJson.Length}, STJ JSON={StjJson.Length}");
    }
}

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 2, warmupCount: 32, iterationCount: 10)]
[InvocationCount(65536)]
[MemoryDiagnoser, CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[Config(typeof(CharacterRatioBuildConfig))]
public class CharacterBinaryRatioBenchmark : CharacterRatioFixture
{
    [Benchmark(Baseline = true), BenchmarkCategory("WriteArray")]
    public byte[] MemoryPackWrite() => MemoryPack.MemoryPackSerializer.Serialize(Data);
    [Benchmark, BenchmarkCategory("WriteArray")]
    public byte[] LuminPackWrite() => LuminPackSerializer.Serialize(Data);
    [Benchmark(Baseline = true), BenchmarkCategory("ReadNewObject")]
    public CharacterSaveData MemoryPackRead() => MemoryPack.MemoryPackSerializer.Deserialize<CharacterSaveData>(MemoryBinary)!;
    [Benchmark, BenchmarkCategory("ReadNewObject")]
    public CharacterSaveData LuminPackRead() => LuminPackSerializer.Deserialize<CharacterSaveData>(LuminBinary);
}

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 2, warmupCount: 32, iterationCount: 10)]
[InvocationCount(8192)]
[MemoryDiagnoser, CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[Config(typeof(CharacterRatioBuildConfig))]
public class CharacterJsonRatioBenchmark : CharacterRatioFixture
{
    [Benchmark(Baseline = true), BenchmarkCategory("WriteString")]
    public string StjWrite() => JsonSerializer.Serialize(Data, Context.CharacterSaveData);
    [Benchmark, BenchmarkCategory("WriteString")]
    public string LuminPackWrite() => LuminPackSerializer.SerializeJson(Data);
    [Benchmark(Baseline = true), BenchmarkCategory("ReadNewObject")]
    public CharacterSaveData StjRead() => JsonSerializer.Deserialize(StjJson, Context.CharacterSaveData)!;
    [Benchmark, BenchmarkCategory("ReadNewObject")]
    public CharacterSaveData LuminPackReadStrict() => LuminPackSerializer.DeserializeJson<CharacterSaveData>(LuminJson, Strict);
    [Benchmark, BenchmarkCategory("ReadNewObject")]
    public CharacterSaveData LuminPackReadDefault() => LuminPackSerializer.DeserializeJson<CharacterSaveData>(LuminJson);
}

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 2, warmupCount: 32, iterationCount: 10)]
[InvocationCount(65536)]
[MemoryDiagnoser, CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[Config(typeof(CharacterRatioBuildConfig))]
public class CharacterFixedFieldsRatioBenchmark
{
    private CharacterFixedFieldsBatch _data = null!;
    private byte[] _luminBinary = null!, _memoryBinary = null!;

    [GlobalSetup]
    public void Setup()
    {
        _data = new CharacterFixedFieldsBatch { Values = Enumerable.Range(0, 512).Select(i => new CharacterStats
        {
            Hp = 1000 + i, MaxHp = 50000, Mp = i, MaxMp = 10000,
            Attack = 123.5f + i, Defense = 78.25f + i, Speed = 100.5f,
            CritRate = 0.25f, CritDamage = 1.5f, Level = 1 + i % 200
        }).ToArray() };
        string expected = LuminPackSerializer.SerializeJson(_data);
        byte[] lumin = LuminPackSerializer.Serialize(_data);
        byte[] memory = MemoryPack.MemoryPackSerializer.Serialize(_data);
        _luminBinary = lumin;
        _memoryBinary = memory;
        if (LuminPackSerializer.SerializeJson(LuminPackSerializer.Deserialize<CharacterFixedFieldsBatch>(lumin)) != expected ||
            LuminPackSerializer.SerializeJson(MemoryPack.MemoryPackSerializer.Deserialize<CharacterFixedFieldsBatch>(memory)!) != expected)
            throw new InvalidOperationException("Fixed-field fixture lost object data.");
        Console.WriteLine($"Fixed-field fixture: 512 records, LP={lumin.Length}, MP={memory.Length} binary bytes.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("WriteArray")]
    public byte[] MemoryPackWrite() => MemoryPack.MemoryPackSerializer.Serialize(_data);
    [Benchmark, BenchmarkCategory("WriteArray")]
    public byte[] LuminPackWrite() => LuminPackSerializer.Serialize(_data);
    [Benchmark(Baseline = true), BenchmarkCategory("ReadNewObject")]
    public CharacterFixedFieldsBatch MemoryPackRead() => MemoryPack.MemoryPackSerializer.Deserialize<CharacterFixedFieldsBatch>(_memoryBinary)!;
    [Benchmark, BenchmarkCategory("ReadNewObject")]
    public CharacterFixedFieldsBatch LuminPackRead() => LuminPackSerializer.Deserialize<CharacterFixedFieldsBatch>(_luminBinary);
}

[LuminPack.Attribute.LuminPackable, MemoryPack.MemoryPackable]
public sealed partial class CharacterFixedFieldsBatch
{
    public CharacterStats[] Values = null!;
}
