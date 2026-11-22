# LuminPack.NewtonsoftJson

[简体中文](../../docs/modules/LuminPack.NewtonsoftJson.cn.md)

This optional adapter targets .NET Standard 2.1. It lets Newtonsoft.Json read and write a LuminPack model through an explicit public contract. The receiving application uses an ordinary DTO with the same shape.

Reference the project or build its standalone NuGet package with `dotnet pack`. Add a closed converter to your Newtonsoft.Json settings.

```csharp
using LuminPack.Attribute;
using LuminPack.NewtonsoftJson;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

var settings = new JsonSerializerSettings
{
    ContractResolver = new CamelCasePropertyNamesContractResolver()
};
settings.Converters.Add(new LuminPackJsonConverter<Scoreboard, ScoreboardContract>(
    static model => new ScoreboardContract(model.Points),
    static dto => new Scoreboard { Points = dto.Points }));

string json = JsonConvert.SerializeObject(
    new Scoreboard { Points = new() { [42] = 7 } }, settings);
Scoreboard? copy = JsonConvert.DeserializeObject<Scoreboard>(json, settings);

[LuminPackable]
public sealed class Scoreboard
{
    public Dictionary<int, int> Points { get; set; } = new();
}

public sealed record ScoreboardContract(Dictionary<int, int> Points);
```

The output is `{"points":{"42":7}}`. The calling Newtonsoft.Json serializer controls the DTO's property names, dictionary keys, and converters. For polymorphic models, map a public discriminator such as `kind` to the supported cases in your mapping functions.

Keep the DTO separate from the model hierarchy. The mappings operate on nonnull values and return a model on a successful read. The application's mapping and DTO determine which data and object identities the external contract preserves. Runtime support follows the Newtonsoft.Json contract resolver and its requirements. See the [System.Text.Json adapter](../LuminPack.SystemTextJson/README.md) for a source-generated metadata path.
