using System.Text.Json;
using System.Text.Json.Serialization;
using LuminPack.Attribute;
using Newtonsoft.Json.Serialization;
using JsonNet = Newtonsoft.Json.JsonConvert;
using JsonNetException = Newtonsoft.Json.JsonSerializationException;
using JsonNetSettings = Newtonsoft.Json.JsonSerializerSettings;
using StjAdapter = LuminPack.SystemTextJson.LuminPackJsonConverter<LuminPack.Tests.JsonAdapters.Message, LuminPack.Tests.JsonAdapters.MessageContract>;
using JsonNetAdapter = LuminPack.NewtonsoftJson.LuminPackJsonConverter<LuminPack.Tests.JsonAdapters.Message, LuminPack.Tests.JsonAdapters.MessageContract>;

namespace LuminPack.Tests.JsonAdapters;

internal static class Program
{
    /// <summary>Checks both adapters against the native format and ordinary JSON clients.</summary>
    private static void Main()
    {
        var stjOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        stjOptions.Converters.Add(new StjAdapter(AdapterContext.Default.MessageContract, ToContract, FromContract));
        stjOptions.Converters.Add(new LuminPack.SystemTextJson.LuminPackJsonConverter<Quantity, int>(
            AdapterContext.Default.Int32, value => value.Value, value => new Quantity(value)));
        var stj = new AdapterContext(stjOptions);

        var jsonNet = new JsonNetSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        jsonNet.Converters.Add(new JsonNetAdapter(ToContract, FromContract));
        jsonNet.Converters.Add(new LuminPack.NewtonsoftJson.LuminPackJsonConverter<Quantity, int>(
            value => value.Value, value => new Quantity(value)));
        var plainJsonNet = new JsonNetSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        const string expected = """{"items":[{"kind":"score","scores":{"42":7}},null],"tail":9}""";
        var model = new Envelope { Items = [new ScoreMessage { Scores = new() { [42] = 7 } }, null], Tail = 9 };
        string native = LuminPackSerializer.SerializeJson(model);
        Check(native.Contains("\"$value\":", StringComparison.Ordinal) && native.Contains("[[42,7]]", StringComparison.Ordinal),
            "The native fixture contains a Union envelope and integer-key pairs.");
        Envelope fromNative = LuminPackSerializer.DeserializeJson<Envelope>(native);

        string stjJson = JsonSerializer.Serialize(fromNative, stj.Envelope);
        string jsonNetJson = JsonNet.SerializeObject(fromNative, jsonNet);
        Check(stjJson == expected && jsonNetJson == expected, "Both adapters produce the declared shape.");
        CheckContract(JsonSerializer.Deserialize(jsonNetJson, AdapterContext.Default.EnvelopeContract));
        CheckContract(JsonNet.DeserializeObject<EnvelopeContract>(stjJson, plainJsonNet));

        var external = new EnvelopeContract([new MessageContract("score", new() { [42] = 7 }), null], 9);
        string externalStj = JsonSerializer.Serialize(external, AdapterContext.Default.EnvelopeContract);
        string externalJsonNet = JsonNet.SerializeObject(external, plainJsonNet);
        Envelope fromStj = JsonNet.DeserializeObject<Envelope>(externalStj, jsonNet)!;
        Envelope fromJsonNet = JsonSerializer.Deserialize(externalJsonNet, stj.Envelope)!;
        Check(LuminPackSerializer.SerializeJson(fromStj) == native && LuminPackSerializer.SerializeJson(fromJsonNet) == native,
            "Both adapters map ordinary client JSON back to the native model.");

        Check(JsonSerializer.Deserialize("null", stj.Message) is null && JsonNet.DeserializeObject<Message>("null", jsonNet) is null,
            "A null reference passes through either serializer.");
        Check(JsonSerializer.Deserialize("5", stj.Quantity).Value == 5 && JsonNet.DeserializeObject<Quantity>("5", jsonNet).Value == 5,
            "Both adapters can map a value type to and from a scalar contract.");
        Check(JsonSerializer.Serialize(new Quantity(5), stj.Quantity) == "5" && JsonNet.SerializeObject(new Quantity(5), jsonNet) == "5",
            "Both serializers write the projected scalar.");
        Expect<JsonException>(() => JsonSerializer.Deserialize("null", stj.Quantity));
        Expect<JsonNetException>(() => JsonNet.DeserializeObject<Quantity>("null", jsonNet));
        Expect<InvalidDataException>(() => JsonSerializer.Deserialize("""{"kind":"unknown","scores":{}}""", stj.Message));
        Expect<InvalidDataException>(() => JsonNet.DeserializeObject<Message>("""{"kind":"unknown","scores":{}}""", jsonNet));
        Expect<JsonNetException>(() => JsonNet.DeserializeObject<NoticeMessage>("""{"kind":"score","scores":{}}""", jsonNet));
        Expect<ArgumentException>(() => new LuminPack.SystemTextJson.LuminPackJsonConverter<Message, Message>(
            stj.Message, static value => value, static value => value));
        Expect<ArgumentException>(() => new LuminPack.NewtonsoftJson.LuminPackJsonConverter<Message, Message>(
            static value => value, static value => value));

        Console.WriteLine("Both JSON adapters passed native model, external contract, null, scalar and invalid case checks.");
    }

    /// <summary>Maps a native Union case to the public DTO.</summary>
    /// <param name="model">The application message.</param>
    /// <returns>The public contract value.</returns>
    private static MessageContract ToContract(Message model) => model switch
    {
        ScoreMessage score => new("score", score.Scores),
        _ => throw new InvalidDataException("Unknown model case."),
    };

    /// <summary>Maps the public discriminator to a known native case.</summary>
    /// <param name="dto">The decoded public contract value.</param>
    /// <returns>The mapped application message.</returns>
    private static Message FromContract(MessageContract dto) => dto.Kind switch
    {
        "score" => new ScoreMessage { Scores = dto.Scores },
        _ => throw new InvalidDataException("Unknown contract case."),
    };

    /// <summary>Checks a public contract decoded without an adapter.</summary>
    /// <param name="envelope">The decoded DTO.</param>
    private static void CheckContract(EnvelopeContract? envelope)
    {
        Check(envelope is { Items: [{ Kind: "score" } entry, null], Tail: 9 } && entry.Scores[42] == 7,
            "The plain serializer reads the projected dictionary, case and sibling value.");
    }

    /// <summary>Stops the test when a contract assertion fails.</summary>
    /// <param name="condition">The asserted condition.</param>
    /// <param name="message">The name of the assertion.</param>
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>Checks that the operation fails with the expected exception.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The operation to execute.</param>
    private static void Expect<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException)}.");
    }
}

[LuminPackable]
public sealed class Envelope
{
    public Message?[] Items { get; set; } = [];
    public int Tail { get; set; }
}

[LuminPackable, LuminPackUnion(1, typeof(ScoreMessage)), LuminPackUnion(2, typeof(NoticeMessage))]
public abstract partial class Message;

[LuminPackable]
public sealed class ScoreMessage : Message
{
    public Dictionary<int, int> Scores { get; set; } = new();
}

[LuminPackable]
public sealed class NoticeMessage : Message
{
    public string Text { get; set; } = "";
}

public readonly record struct Quantity(int Value);
public sealed record MessageContract(string Kind, Dictionary<int, int> Scores);
public sealed record EnvelopeContract(MessageContract?[] Items, int Tail);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Envelope))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(EnvelopeContract))]
[JsonSerializable(typeof(Quantity))]
[JsonSerializable(typeof(int))]
internal partial class AdapterContext : JsonSerializerContext;
