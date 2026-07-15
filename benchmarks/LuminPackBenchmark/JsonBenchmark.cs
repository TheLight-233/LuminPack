using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;
using Newtonsoft.Json;

namespace LuminPackBenchmark;

// ────────────────────────────────────────────────────────────────────────────
// STJ 源生成器 Context
// 注册 CharacterSaveData 的完整类型图（含多态派生类）
// ────────────────────────────────────────────────────────────────────────────

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    IncludeFields               = true,
    WriteIndented               = false,
    DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(CharacterSaveData))]
[JsonSerializable(typeof(CharacterStats))]
[JsonSerializable(typeof(GuildInfo))]
[JsonSerializable(typeof(ItemBase))]
[JsonSerializable(typeof(WeaponItem))]
[JsonSerializable(typeof(ArmorItem))]
[JsonSerializable(typeof(ConsumableItem))]
[JsonSerializable(typeof(MountItem))]
[JsonSerializable(typeof(ActiveBuff))]
internal partial class CharacterSaveDataJsonContext : JsonSerializerContext { }

// ────────────────────────────────────────────────────────────────────────────
// Benchmark
// ────────────────────────────────────────────────────────────────────────────

[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer]
[MarkdownExporterAttribute.GitHub]
public class JsonBenchmark
{
    private CharacterSaveData _data = CharacterSaveData.Create();

    private string _luminPackJson;
    private string _systemTextJson;
    private string _newtonsoftJson;

    private LuminBufferWriter _bufferWriter = LuminBufferWriterPool.Rent();

    // STJ 源生成器选项
    private static readonly System.Text.Json.JsonSerializerOptions StjOptions =
        CharacterSaveDataJsonContext.Default.Options;

    // Newtonsoft 配置
    private static readonly JsonSerializerSettings NjSettings = new()
    {
        NullValueHandling    = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting           = Formatting.None,
        TypeNameHandling     = TypeNameHandling.Auto,   // 支持多态
        ConstructorHandling  = ConstructorHandling.AllowNonPublicDefaultConstructor,
    };

    private readonly LuminPackSerializerOption _luminOptions = new()
    {
        StringEncoding = LuminPackStringEncoding.UTF8,
    };

    [GlobalSetup]
    public void SetUp()
    {
        _luminPackJson  = LuminPackSerializer.SerializeJson(_data, _luminOptions);
        _systemTextJson = System.Text.Json.JsonSerializer.Serialize(_data, StjOptions);
        _newtonsoftJson = JsonConvert.SerializeObject(_data, NjSettings);
    }

    [GlobalCleanup]
    public void CleanUp() => _bufferWriter.Dispose();

    // ── Serialize ────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Serialize")]
    public void LuminPackSerializeToJson()
        => LuminPackSerializer.SerializeJson(_data, _bufferWriter, _luminOptions);

    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public string SystemTextJsonSerialize()
        => System.Text.Json.JsonSerializer.Serialize(_data, StjOptions);

    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public string NewtonsoftJsonSerialize()
        => JsonConvert.SerializeObject(_data, NjSettings);

    // ── Deserialize ──────────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Deserialize")]
    public CharacterSaveData LuminPackDeserializeFromJson()
        => LuminPackSerializer.DeserializeJson<CharacterSaveData>(_luminPackJson, _luminOptions);

    [Benchmark]
    [BenchmarkCategory("Deserialize")]
    public CharacterSaveData SystemTextJsonDeserialize()
        => System.Text.Json.JsonSerializer.Deserialize<CharacterSaveData>(_systemTextJson, StjOptions);

    [Benchmark]
    [BenchmarkCategory("Deserialize")]
    public CharacterSaveData NewtonsoftJsonDeserialize()
        => JsonConvert.DeserializeObject<CharacterSaveData>(_newtonsoftJson, NjSettings);
}