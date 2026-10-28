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
// System.Text.Json source generation context.
// Register the complete CharacterSaveData type graph, including polymorphic members.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides character save data json context functionality.</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = false,                                  // Use exact PascalCase matching for both writing and reading.
    IncludeFields = true,                                   // The data model uses fields.
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault, // Match DefaultValueHandling.Ignore and reduce the written payload.
    DefaultBufferSize = 64 * 1024                               // Reserve about 35 KB to avoid buffer growth.
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

/// <summary>Provides json benchmark functionality.</summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 3, warmupCount: 6, iterationCount: 15)]
[MemoryDiagnoser]
[GcServer(true)]
[MarkdownExporterAttribute.GitHub]
public class JsonBenchmark
{
    private CharacterSaveData _data = CharacterSaveData.Create();

    private string _luminPackJson;
    private string _systemTextJson;
    private string _newtonsoftJson;
    private string _redoxJson;

    private LuminBufferWriter _bufferWriter = LuminBufferWriterPool.Rent();

    // System.Text.Json source generation options.
    private static readonly System.Text.Json.JsonSerializerOptions StjOptions =
        CharacterSaveDataJsonContext.Default.Options;

    // Newtonsoft configuration.
    private static readonly JsonSerializerSettings NjSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.None,
        TypeNameHandling = TypeNameHandling.Auto,   // Enable polymorphic handling.
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        DateParseHandling = DateParseHandling.None,  // Skip date detection because the model has no DateTime members.
    };

    // RE:Dox configuration:
    //  Match Newtonsoft semantics for a comparable benchmark.
    //  Polymorphism uses AutoTypeNameDoxSettings, equivalent to TypeNameHandling.Auto.
    //  Preallocate the write buffer and enable parallel deserialization.
    private static readonly AutoTypeNameDoxSettings RedoxSettings = new()
    {
        NullValueHandling = REDox.Serialization.NullValueHandling.Ignore,
        DefaultValueHandling = REDox.Serialization.DefaultValueHandling.Ignore,
        ConstructorHandling = REDox.Serialization.ConstructorHandling.AllowNonPublicDefaultConstructor,
        FloatFormatHandling = REDox.Serialization.FloatFormatHandling.AlwaysIncludeDecimal,   // Match Newtonsoft's "200.0" representation.

        // Reserve about 35 KB to avoid repeated 16 KB to 32 KB to 64 KB string buffer growth.
        DefaultBufferSize = 64 * 1024,

        // Parallel deserialization applies to one-dimensional arrays, here ActiveBuff[100].
        // ParallelSzArrayConverter requires both the minimum element count and minimum token span.
        ParallelOptions = new REDox.Serialization.ParallelDeserializeOptions
        {
            ParallelDeserializeEnabled = true,
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            MinimumNumberOfElements = 4,     // Library default.
            MinimumNumberOfTokens = 256,   // Lower the default 1000-element threshold so the 35 KB dataset exercises parallel array reading.
        },
    };

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void SetUp()
    {
        _luminPackJson = LuminPackSerializer.SerializeJson<CharacterSaveData, LuminPackDefaultOptions>(_data);
        _systemTextJson = System.Text.Json.JsonSerializer.Serialize(_data, StjOptions);
        _newtonsoftJson = JsonConvert.SerializeObject(_data, NjSettings);
        _redoxJson = REDox.Json.JsonSerializer.Serialize(_data, RedoxSettings);
    }

    /// <summary>Measures clean up for the configured benchmark input.</summary>
    [GlobalCleanup]
    public void CleanUp() => _bufferWriter.Dispose();

    // ── Serialize ────────────────────────────────────────────────────────────

    /// <summary>Measures lumin pack serialize to json for the configured benchmark input.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Serialize")]
    public void LuminPackSerializeToJson()
    {
        _bufferWriter.ResetCore();
        _bufferWriter.WriterState.ResetOperationState();
        LuminPackSerializer.SerializeJson<CharacterSaveData, LuminPackDefaultOptions>(_data, _bufferWriter);
    }

    /// <summary>Measures system text json serialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public string SystemTextJsonSerialize()
        => System.Text.Json.JsonSerializer.Serialize(_data, StjOptions);

    /// <summary>Measures newtonsoft json serialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public string NewtonsoftJsonSerialize()
        => JsonConvert.SerializeObject(_data, NjSettings);

    /// <summary>Measures re do x json serialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public string ReDoXJsonSerialize()
        => REDox.Json.JsonSerializer.Serialize(_data, RedoxSettings);

    // ── Deserialize ──────────────────────────────────────────────────────────

    /// <summary>Measures lumin pack deserialize from json for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Deserialize")]
    public CharacterSaveData LuminPackDeserializeFromJson()
        => LuminPackSerializer.DeserializeJson<CharacterSaveData, LuminPackDefaultOptions>(_luminPackJson);

    /// <summary>Measures system text json deserialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    [BenchmarkCategory("Deserialize")]
    public CharacterSaveData SystemTextJsonDeserialize()
        => System.Text.Json.JsonSerializer.Deserialize<CharacterSaveData>(_systemTextJson, StjOptions);

    /// <summary>Measures newtonsoft json deserialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    [BenchmarkCategory("Deserialize")]
    public CharacterSaveData NewtonsoftJsonDeserialize()
        => JsonConvert.DeserializeObject<CharacterSaveData>(_newtonsoftJson, NjSettings);

    /// <summary>Measures re do x json deserialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    [BenchmarkCategory("Deserialize")]
    public CharacterSaveData ReDoXJsonDeserialize()
        => REDox.Json.JsonSerializer.Deserialize<CharacterSaveData>(_redoxJson, RedoxSettings);
}

// ────────────────────────────────────────────────────────────────────────────
// RE:Dox core polymorphism settings, equivalent to Newtonsoft TypeNameHandling.Auto.
// The default core serializer omits $type and loses derived members; deserialization yields null.
// Resolve polymorphic contracts by overriding the contract configuration:
//   Mark each type contract as IsPolymorphic.
//   Give each type a discriminator name in Newtonsoft's $type format: namespace.type, assembly.
//   Resolve concrete types from $type names during deserialization.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides auto type name dox settings functionality.</summary>
internal sealed class AutoTypeNameDoxSettings : REDox.DoxSerializerSettings
{
    /// <summary>Measures resolve contract for the configured benchmark input.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    protected override REDox.Serialization.Metadata.DataContract ResolveContract(Type type)
    {
        var contract = base.ResolveContract(type);

        return contract with
        {
            IsPolymorphic = true,
            TypeDiscriminator = new REDox.Serialization.Metadata.TypeDiscriminator(
                new REDox.Serialization.Utf8Symbol(GetSimpleTypeName(type))),
        };
    }

    /// <summary>Measures resolve contract for the configured benchmark input.</summary>
    /// <param name="baseType">The base type used by this operation.</param>
    /// <param name="typeDiscriminatorSymbol">The type discriminator symbol used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    protected override REDox.Serialization.Metadata.DataContract ResolveContract(
        REDox.Serialization.Metadata.DataContract baseType, ReadOnlySpan<byte> typeDiscriminatorSymbol)
    {
        var name = System.Text.Encoding.UTF8.GetString(typeDiscriminatorSymbol);
        var type = Type.GetType(name);

        return type != null ? GetContract(type) : base.ResolveContract(baseType, typeDiscriminatorSymbol);
    }

    /// <summary>Obtains the simple type name for the supplied input.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetSimpleTypeName(Type type)
    {
        var aqn = type.AssemblyQualifiedName!;
        var versionIndex = aqn.IndexOf(", Version", StringComparison.Ordinal);

        return versionIndex > 0 ? aqn.Substring(0, versionIndex) : aqn;
    }
}
