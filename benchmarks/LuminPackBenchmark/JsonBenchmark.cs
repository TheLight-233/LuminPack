using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;
using Newtonsoft.Json;
using REDox.Json;

namespace LuminPackBenchmark;

// ────────────────────────────────────────────────────────────────────────────
// STJ 源生成器 Context
// 注册 CharacterSaveData 的完整类型图（含多态派生类）
// ────────────────────────────────────────────────────────────────────────────

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = false,                                  // 精确匹配更快；写/读都是 PascalCase
    IncludeFields               = true,                                   // 数据模型是字段
    WriteIndented               = false,
    DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingDefault, // 与 Newtonsoft/RE:Dox 的 DefaultValueHandling.Ignore 对齐，同时少写字节
    DefaultBufferSize           = 64 * 1024                               // 一次容下 ~35KB 输出，省掉缓冲扩容
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
    private string _redoxJson;

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
        DateParseHandling    = DateParseHandling.None,  // 模型里没有 DateTime 成员，跳过字符串的日期嗅探
    };

    // RE:Dox 配置：
    //  · 语义对齐 Newtonsoft（保证与 Newtonsoft 行可比）
    //  · 多态由 AutoTypeNameDoxSettings 提供，等价于 TypeNameHandling.Auto
    //  · 性能项：写缓冲一次到位 + 打开并行反序列化
    private static readonly AutoTypeNameDoxSettings RedoxSettings = new()
    {
        NullValueHandling    = REDox.Serialization.NullValueHandling.Ignore,
        DefaultValueHandling = REDox.Serialization.DefaultValueHandling.Ignore,
        ConstructorHandling  = REDox.Serialization.ConstructorHandling.AllowNonPublicDefaultConstructor,
        FloatFormatHandling  = REDox.Serialization.FloatFormatHandling.AlwaysIncludeDecimal,   // 对齐 Newtonsoft 的 "200.0" 写法

        // 一次容纳 ~35KB 输出，省掉字符串缓冲 16K→32K→64K 的多次扩容
        DefaultBufferSize = 64 * 1024,

        // 并行反序列化：只对一维数组生效（本数据集里是 ActiveBuff[100]）
        // 判定条件（库内 ParallelSzArrayConverter）：元素数 ≥ MinimumNumberOfElements 且 该数组 token 跨度 ≥ MinimumNumberOfTokens
        ParallelOptions = new REDox.Serialization.ParallelDeserializeOptions
        {
            ParallelDeserializeEnabled = true,
            MaxDegreeOfParallelism     = Environment.ProcessorCount,
            MinimumNumberOfElements    = 4,     // 库默认值
            MinimumNumberOfTokens      = 256,   // 库默认 1000 对 35KB 数据集偏保守，调低以确保数组真正走并行分支
        },
    };

    private readonly LuminPackSerializerOption _luminOptions = new()
    {
        StringEncoding = LuminPackStringEncoding.UTF8,
    };

    [GlobalSetup]
    public void SetUp()
    {
        _bufferWriter.Option.StringEncoding = _luminOptions.StringEncoding;
        _bufferWriter.Option.StringRecording = _luminOptions.StringRecording;
        _bufferWriter.Option.StandardFormat = _luminOptions.StandardFormat;
        _luminPackJson  = LuminPackSerializer.SerializeJson(_data, _luminOptions);
        _systemTextJson = System.Text.Json.JsonSerializer.Serialize(_data, StjOptions);
        _newtonsoftJson = JsonConvert.SerializeObject(_data, NjSettings);
        _redoxJson = REDox.Json.JsonSerializer.Serialize(_data, RedoxSettings);
    }

    [GlobalCleanup]
    public void CleanUp() => _bufferWriter.Dispose();

    // ── Serialize ────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Serialize")]
    public void LuminPackSerializeToJson()
        => LuminPackSerializer.SerializeJson(_data, _bufferWriter);

    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public string SystemTextJsonSerialize()
        => System.Text.Json.JsonSerializer.Serialize(_data, StjOptions);

    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public string NewtonsoftJsonSerialize()
        => JsonConvert.SerializeObject(_data, NjSettings);

    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public string ReDoXJsonSerialize()
        => REDox.Json.JsonSerializer.Serialize(_data, RedoxSettings);

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

    [Benchmark]
    [BenchmarkCategory("Deserialize")]
    public CharacterSaveData ReDoXJsonDeserialize()
        => REDox.Json.JsonSerializer.Deserialize<CharacterSaveData>(_redoxJson, RedoxSettings);
}

// ────────────────────────────────────────────────────────────────────────────
// RE:Dox 多态设置（纯 core 实现，等价 Newtonsoft 的 TypeNameHandling.Auto）
// 核心包默认不写 $type，且派生类型字段会被静默丢弃（反序列化得到 null）；
// 通过覆写契约解析：
//   1) 每个类型契约标记 IsPolymorphic；
//   2) 每个类型带自己的判别名（"命名空间.类型, 程序集"，与 Newtonsoft 的 $type 写法一致）；
//   3) 反序列化时按 $type 名称反射还原具体类型。
// ────────────────────────────────────────────────────────────────────────────

internal sealed class AutoTypeNameDoxSettings : REDox.DoxSerializerSettings
{
    protected override REDox.Serialization.Metadata.DataContract ResolveContract(Type type)
    {
        var contract = base.ResolveContract(type);

        return contract with
        {
            IsPolymorphic     = true,
            TypeDiscriminator = new REDox.Serialization.Metadata.TypeDiscriminator(
                new REDox.Serialization.Utf8Symbol(GetSimpleTypeName(type))),
        };
    }

    protected override REDox.Serialization.Metadata.DataContract ResolveContract(
        REDox.Serialization.Metadata.DataContract baseType, ReadOnlySpan<byte> typeDiscriminatorSymbol)
    {
        var name = System.Text.Encoding.UTF8.GetString(typeDiscriminatorSymbol);
        var type = Type.GetType(name);

        return type != null ? GetContract(type) : base.ResolveContract(baseType, typeDiscriminatorSymbol);
    }

    private static string GetSimpleTypeName(Type type)
    {
        var aqn = type.AssemblyQualifiedName!;
        var versionIndex = aqn.IndexOf(", Version", StringComparison.Ordinal);

        return versionIndex > 0 ? aqn.Substring(0, versionIndex) : aqn;
    }
}
