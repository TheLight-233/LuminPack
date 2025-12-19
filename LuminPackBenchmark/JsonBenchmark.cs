
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LuminPack.Utility;

namespace LuminPackBenchmark;

[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer]
[MarkdownExporterAttribute.GitHub]
public class JsonBenchmark
{
    private SimpleClass myClass = SimpleClass.Create();
    
    // 序列化结果存储
    private string _luminPackJson;
    private string _systemTextJson;
    private string _newtonsoftJson;
    
    private byte[] _luminPackUtf8Bytes;
    private byte[] _systemTextUtf8Bytes;

    private LuminBufferWriter _bufferWriter = LuminBufferWriterPool.Rent();
    
    // System.Text.Json 配置选项
    private static readonly JsonSerializerOptions SystemTextJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true, // 包含字段序列化
        WriteIndented = false, // 非格式化输出以提升性能
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // 多态支持配置
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = 
            {
                static typeInfo =>
                {
                    if (typeInfo.Type == typeof(IFoo))
                    {
                        typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                        {
                            TypeDiscriminatorPropertyName = "$type",
                            IgnoreUnrecognizedTypeDiscriminators = true,
                            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                            DerivedTypes =
                            {
                                new JsonDerivedType(typeof(FooA), "FooA"),
                                new JsonDerivedType(typeof(FooB), "FooB"),
                                new JsonDerivedType(typeof(FooC), "FooC"),
                                new JsonDerivedType(typeof(FooD), "FooD"),
                                new JsonDerivedType(typeof(FooE), "FooE"),
                                new JsonDerivedType(typeof(FooF), "FooF"),
                                new JsonDerivedType(typeof(FooG), "FooG"),
                                new JsonDerivedType(typeof(FooH), "FooH"),
                                new JsonDerivedType(typeof(FooI), "FooI"),
                                new JsonDerivedType(typeof(FooJ), "FooJ"),
                                new JsonDerivedType(typeof(FooK), "FooK"),
                                new JsonDerivedType(typeof(FooL), "FooL"),
                                new JsonDerivedType(typeof(FooM), "FooM"),
                                new JsonDerivedType(typeof(FooN), "FooN"),
                                new JsonDerivedType(typeof(FooO), "FooO"),
                                new JsonDerivedType(typeof(FooP), "FooP"),
                                new JsonDerivedType(typeof(FooQ), "FooQ"),
                                new JsonDerivedType(typeof(FooR), "FooR"),
                                new JsonDerivedType(typeof(FooS), "FooS"),
                                new JsonDerivedType(typeof(FooZ), "FooZ")
                            }
                        };
                    }
                }
            }
        }
    };
    
    // Newtonsoft.Json 配置选项
    private static readonly JsonSerializerSettings NewtonsoftJsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        TypeNameHandling = TypeNameHandling.Auto, // 多态支持
        Formatting = Formatting.None, // 非格式化输出
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
    };
    
    [GlobalSetup]
    public void SetUp()
    {
        _luminPackJson = LuminPackSerializer.SerializeJson(myClass);
        _systemTextJson = System.Text.Json.JsonSerializer.Serialize(myClass, SystemTextJsonOptions);
        _newtonsoftJson = JsonConvert.SerializeObject(myClass, NewtonsoftJsonSettings);
        
        _systemTextUtf8Bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(myClass, SystemTextJsonOptions);
    }
    
    [Benchmark(Baseline = true)]
    public void LuminPackSerializeToJson()
    {
        LuminPackSerializer.SerializeJson(myClass, _bufferWriter);
    }
    
    [Benchmark]
    public string SystemTextJsonSerialize()
    {
        return System.Text.Json.JsonSerializer.Serialize(myClass, SystemTextJsonOptions);
    }
    
    [Benchmark]
    public string NewtonsoftJsonSerialize()
    {
        return JsonConvert.SerializeObject(myClass, NewtonsoftJsonSettings);
    }
    
    // ==================== 字符串反序列化测试 ====================
    [Benchmark]
    public SimpleClassBase LuminPackDeserializeFromJson()
    {
        return LuminPackSerializer.DeserializeJson<SimpleClass>(_luminPackJson);
    }
    
    [Benchmark]
    public SimpleClassBase SystemTextJsonDeserialize()
    {
        return System.Text.Json.JsonSerializer.Deserialize<SimpleClass>(_systemTextJson, SystemTextJsonOptions);
    }
    
    [Benchmark]
    public SimpleClassBase NewtonsoftJsonDeserialize()
    {
        return JsonConvert.DeserializeObject<SimpleClass>(_newtonsoftJson, NewtonsoftJsonSettings);
    }
}  


[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    IncludeFields = true,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(SimpleClassBase))]
[JsonSerializable(typeof(SimpleClass))]
[JsonSerializable(typeof(Transform))]
[JsonSerializable(typeof(IFoo))]
[JsonSerializable(typeof(FooA))]
[JsonSerializable(typeof(FooB))]
[JsonSerializable(typeof(FooC))]
[JsonSerializable(typeof(FooD))]
[JsonSerializable(typeof(FooE))]
[JsonSerializable(typeof(FooF))]
[JsonSerializable(typeof(FooG))]
[JsonSerializable(typeof(FooH))]
[JsonSerializable(typeof(FooI))]
[JsonSerializable(typeof(FooJ))]
[JsonSerializable(typeof(FooK))]
[JsonSerializable(typeof(FooL))]
[JsonSerializable(typeof(FooM))]
[JsonSerializable(typeof(FooN))]
[JsonSerializable(typeof(FooO))]
[JsonSerializable(typeof(FooP))]
[JsonSerializable(typeof(FooQ))]
[JsonSerializable(typeof(FooR))]
[JsonSerializable(typeof(FooS))]
[JsonSerializable(typeof(FooZ))]
internal partial class LuminPackJsonSerializationContext : JsonSerializerContext
{
}
