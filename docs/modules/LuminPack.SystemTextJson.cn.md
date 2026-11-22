# LuminPack.SystemTextJson

[English](../../src/LuminPack.SystemTextJson/README.md)

这个可选适配包支持 .NET 8 及以上版本。System.Text.Json 通过描述对外 JSON 契约的 DTO 读写 LuminPack 模型。对端使用自己的普通 DTO 和序列化器即可。

可引用源码项目，也可通过 `dotnet pack` 构建独立 NuGet 包。在 System.Text.Json 选项中注册闭合 converter。

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

输出为 `{"points":{"42":7}}`。传入的 `JsonTypeInfo<TContract>` 控制契约的属性名、字典键和 converter。外层序列化器选项控制未适配的成员。面向 NativeAOT 时，按示例为 DTO 和外层模型提供源生成元数据。

可在映射函数中转换 case ID，或选用 `kind` 等对外判别字段。直接序列化 Union 根时，传入声明的基类或接口的元数据，System.Text.Json 据此选择 converter。契约 DTO 应独立于模型的继承层次。映射函数处理非空值，在读取成功时返回模型。对外保留哪些数据和对象身份，由应用的映射和 DTO 决定。

在 LuminPack 边界调用 `LuminPackSerializer.SerializeJson` 或 `DeserializeJson`，在对外 JSON 边界调用配置好的 System.Text.Json 序列化器。
