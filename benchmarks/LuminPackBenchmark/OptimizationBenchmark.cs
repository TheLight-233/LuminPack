using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

// Keep the same input, options and return shapes when comparing revisions.
// Run this class on each revision, in separate processes, without other benchmarks running.
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 4, iterationCount: 8)]
[MemoryDiagnoser]
public class OptimizationBenchmark
{
    private CharacterSaveData _data = null!;
    private byte[] _binary = null!;
    private byte[] _json = null!;
    private string _jsonString = null!;
    private LuminBufferWriter _writer = null!;
    private static readonly LuminPackSerializerOption Strict = new() { ValidateJsonSyntax = true };

    [GlobalSetup]
    public void Setup()
    {
        _data = CreateDeterministicData();
        _writer = LuminBufferWriterPool.Rent();
        _binary = LuminPackSerializer.Serialize(_data);
        _jsonString = LuminPackSerializer.SerializeJson(_data);
        _json = Encoding.UTF8.GetBytes(_jsonString);
        // Setup also checks the complete object graph, including every union case.
        var roundtrip = LuminPackSerializer.DeserializeJson<CharacterSaveData>(_json, Strict);
        if (LuminPackSerializer.SerializeJson(roundtrip) != _jsonString)
            throw new InvalidOperationException("JSON roundtrip changed the fixture.");
        Console.WriteLine($"Fixture: binary={_binary.Length} B, JSON UTF-8={_json.Length} B");
    }

    internal static CharacterSaveData CreateDeterministicData()
    {
        var data = CharacterSaveData.Create();
        const long timestamp = 1_790_000_000;
        data.LastSaveAt = timestamp;
        data.StatsSnapshot.Timestamp = timestamp - 3600;
        data.GuildSnapshot.Timestamp = timestamp - 7200;
        data.Guild.CreatedAt = timestamp - 86400;
        data.GuildSnapshot.Payload.CreatedAt = timestamp - 86400;
        foreach (var item in data.Bag.Concat(data.Storage)) item.ObtainedAt = timestamp - item.ItemId;
        for (int i = 0; i < data.RecentEvents.Count; i++) data.RecentEvents[i].Timestamp = timestamp - i;
        return data;
    }

    [GlobalCleanup]
    public void Cleanup() => LuminBufferWriterPool.Return(_writer);

    [Benchmark] public CharacterSaveData JsonReadUtf8() => LuminPackSerializer.DeserializeJson<CharacterSaveData>(_json);
    [Benchmark] public CharacterSaveData JsonReadString() => LuminPackSerializer.DeserializeJson<CharacterSaveData>(_jsonString);
    [Benchmark] public CharacterSaveData JsonReadStrictUtf8() => LuminPackSerializer.DeserializeJson<CharacterSaveData>(_json, Strict);
    [Benchmark] public void JsonWriteBuffer() => LuminPackSerializer.SerializeJson(_data, _writer);
    [Benchmark] public string JsonWriteString() => LuminPackSerializer.SerializeJson(_data);
    [Benchmark] public void BinaryWriteBuffer() => LuminPackSerializer.Serialize(_data, _writer);
    [Benchmark] public CharacterSaveData BinaryRead() => LuminPackSerializer.Deserialize<CharacterSaveData>(_binary);
}

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 4, iterationCount: 8)]
[MemoryDiagnoser]
public class JsonNumberReadOptimizationBenchmark
{
    [Params("Fixed", "Exponent", "Roundtrip")]
    public string Shape { get; set; } = "Fixed";
    private byte[] _floats = null!;
    private byte[] _doubles = null!;
    private readonly LuminPackReaderOptionalState _state = new(new LuminPackSerializerOption());

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        var floats = Enumerable.Range(0, 1024).Select(_ => (float)(random.NextDouble() * 5000 - 2500)).ToArray();
        var doubles = Enumerable.Range(0, 1024).Select(_ => random.NextDouble() * 5000 - 2500).ToArray();
        string format = Shape == "Fixed" ? "F2" : Shape == "Exponent" ? "E" : "R";
        _floats = Encoding.UTF8.GetBytes("[" + string.Join(",", floats.Select(f => f.ToString(format, CultureInfo.InvariantCulture))) + "]");
        _doubles = Encoding.UTF8.GetBytes("[" + string.Join(",", doubles.Select(f => f.ToString(format, CultureInfo.InvariantCulture))) + "]");
    }

    [Benchmark]
    public double ReadFloats()
    {
        ReadOnlySpan<byte> buffer = _floats;
        var reader = new LuminPack.Core.LuminPackJsonReader(ref buffer, _state);
        reader.Read();
        double sum = 0;
        while (reader.Read() && reader.CurrentTokenType != LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)
            sum += reader.ReadFloat();
        return sum;
    }

    [Benchmark]
    public double ReadDoubles()
    {
        ReadOnlySpan<byte> buffer = _doubles;
        var reader = new LuminPack.Core.LuminPackJsonReader(ref buffer, _state);
        reader.Read();
        double sum = 0;
        while (reader.Read() && reader.CurrentTokenType != LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)
            sum += reader.ReadDouble();
        return sum;
    }
}

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 4, iterationCount: 8)]
[MemoryDiagnoser]
public class JsonInputShapeOptimizationBenchmark
{
    [Params("Reversed", "Indented", "Utf16")]
    public string Shape { get; set; } = "Reversed";
    private string _json = null!;
    private LuminPackSerializerOption _option = null!;

    [GlobalSetup]
    public void Setup()
    {
        _option = new LuminPackSerializerOption
        {
            StringEncoding = Shape == "Utf16" ? LuminPackStringEncoding.UTF16 : LuminPackStringEncoding.UTF8,
            ValidateJsonSyntax = true
        };
        string original = LuminPackSerializer.SerializeJson(OptimizationBenchmark.CreateDeterministicData());
        using var doc = JsonDocument.Parse(original);
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = Shape == "Indented" }))
            WriteElement(writer, doc.RootElement, Shape == "Reversed");
        _json = Encoding.UTF8.GetString(output.WrittenSpan);
        string roundtrip = LuminPackSerializer.SerializeJson(LuminPackSerializer.DeserializeJson<CharacterSaveData>(_json, _option));
        if (!System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(roundtrip), System.Text.Json.Nodes.JsonNode.Parse(original)))
            throw new InvalidOperationException("Reshaping JSON changed its values.");
    }

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element, bool reverse)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            var properties = element.EnumerateObject().ToArray();
            // Union metadata requires $type before $value in the existing wire contract.
            if (reverse && !element.TryGetProperty("$type", out _)) Array.Reverse(properties);
            foreach (var property in properties)
            {
                writer.WritePropertyName(property.Name);
                WriteElement(writer, property.Value, reverse);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var child in element.EnumerateArray()) WriteElement(writer, child, reverse);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }

    [Benchmark] public CharacterSaveData Read() => LuminPackSerializer.DeserializeJson<CharacterSaveData>(_json, _option);
}

// No ignored default fields; both JSON writers return a newly allocated string.
// Each reader consumes its own library's full representation (union wire formats differ).
[JsonSourceGenerationOptions(IncludeFields = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(CharacterSaveData))]
internal partial class OptimizationJsonContext : JsonSerializerContext { }

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 4, iterationCount: 8)]
[MemoryDiagnoser]
public class SerializerComparisonBenchmark
{
    private CharacterSaveData _data = null!;
    private byte[] _luminJson = null!;
    private byte[] _stjJson = null!;
    private byte[] _luminBinary = null!;
    private byte[] _memoryBinary = null!;
    private static readonly LuminPackSerializerOption Strict = new() { ValidateJsonSyntax = true };
    private static readonly OptimizationJsonContext Context = new(new JsonSerializerOptions
    {
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultBufferSize = 64 * 1024
    });

    [GlobalSetup]
    public void Setup()
    {
        _data = OptimizationBenchmark.CreateDeterministicData();
        _luminJson = Encoding.UTF8.GetBytes(LuminPackSerializer.SerializeJson(_data));
        _stjJson = JsonSerializer.SerializeToUtf8Bytes(_data, Context.CharacterSaveData);
        _luminBinary = LuminPackSerializer.Serialize(_data);
        _memoryBinary = MemoryPack.MemoryPackSerializer.Serialize(_data);
        foreach (var copy in new[]
        {
            LuminPackSerializer.DeserializeJson<CharacterSaveData>(_luminJson, Strict),
            JsonSerializer.Deserialize(_stjJson, Context.CharacterSaveData)!,
            LuminPackSerializer.Deserialize<CharacterSaveData>(_luminBinary),
            MemoryPack.MemoryPackSerializer.Deserialize<CharacterSaveData>(_memoryBinary)!
        })
        {
            if (LuminPackSerializer.SerializeJson(copy) != Encoding.UTF8.GetString(_luminJson))
                throw new InvalidOperationException("Comparison fixture lost data.");
        }
        Console.WriteLine($"Full graph bytes: LP JSON={_luminJson.Length}, STJ JSON={_stjJson.Length}, " +
            $"LP binary={_luminBinary.Length}, MemoryPack binary={_memoryBinary.Length}");
    }

    [Benchmark] public string LuminJsonWriteString() => LuminPackSerializer.SerializeJson(_data);
    [Benchmark] public string StjJsonWriteString() => JsonSerializer.Serialize(_data, Context.CharacterSaveData);
    [Benchmark] public CharacterSaveData LuminJsonReadStrict() => LuminPackSerializer.DeserializeJson<CharacterSaveData>(_luminJson, Strict);
    [Benchmark] public CharacterSaveData StjJsonRead() => JsonSerializer.Deserialize(_stjJson, Context.CharacterSaveData)!;
    [Benchmark] public byte[] LuminBinaryWriteArray() => LuminPackSerializer.Serialize(_data);
    [Benchmark] public byte[] MemoryPackWriteArray() => MemoryPack.MemoryPackSerializer.Serialize(_data);
    [Benchmark] public CharacterSaveData LuminBinaryRead() => LuminPackSerializer.Deserialize<CharacterSaveData>(_luminBinary);
    [Benchmark] public CharacterSaveData MemoryPackRead() => MemoryPack.MemoryPackSerializer.Deserialize<CharacterSaveData>(_memoryBinary)!;
}
