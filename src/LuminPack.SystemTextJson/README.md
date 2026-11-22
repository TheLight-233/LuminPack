# LuminPack.SystemTextJson

[简体中文](../../docs/modules/LuminPack.SystemTextJson.cn.md)

This optional .NET 8+ adapter lets System.Text.Json read and write a LuminPack model through a DTO that describes your public JSON contract. The receiving application uses its ordinary DTO and serializer.

Reference the project or build its standalone NuGet package with `dotnet pack`. Register a closed converter with your System.Text.Json options.

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using LuminPack.Attribute;
using LuminPack.SystemTextJson;

var options = new JsonSerializerOptions();
options.Converters.Add(new LuminPackJsonConverter<Scoreboard, ScoreboardContract>(
    ScoreboardJsonContext.Default.ScoreboardContract,
    static model => new ScoreboardContract(model.Points),
    static dto => new Scoreboard { Points = dto.Points }));
var context = new ScoreboardJsonContext(options);

string json = JsonSerializer.Serialize(
    new Scoreboard { Points = new() { [42] = 7 } }, context.Scoreboard);
Scoreboard? copy = JsonSerializer.Deserialize(json, context.Scoreboard);

[LuminPackable]
public sealed class Scoreboard
{
    public Dictionary<int, int> Points { get; set; } = new();
}

public sealed record ScoreboardContract(Dictionary<int, int> Points);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Scoreboard))]
[JsonSerializable(typeof(ScoreboardContract))]
public partial class ScoreboardJsonContext : JsonSerializerContext { }
```

The output is `{"points":{"42":7}}`. The supplied `JsonTypeInfo<TContract>` controls names, dictionary keys, and contract converters. The outer serializer's options control unadapted members. For NativeAOT, supply source-generated metadata for the DTO and the outer model, as in the example.

Use the mapping functions to translate case IDs or choose a public discriminator such as `kind`. When serializing a Union at the root, pass the declared base or interface's metadata so System.Text.Json selects its converter. Keep the contract DTO separate from the model hierarchy. The mappings operate on nonnull values and return a model on a successful read. The application's mapping and DTO determine which data and object identities the external contract preserves.

Call `LuminPackSerializer.SerializeJson` or `DeserializeJson` at the LuminPack boundary. Use the configured System.Text.Json serializer at the public JSON boundary.
