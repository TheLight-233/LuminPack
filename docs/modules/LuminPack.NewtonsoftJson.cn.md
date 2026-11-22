# LuminPack.NewtonsoftJson

[English](../../src/LuminPack.NewtonsoftJson/README.md)

这个可选适配包面向 .NET Standard 2.1。Newtonsoft.Json 通过显式对外契约读写 LuminPack 模型。对端可使用形状相同的普通 DTO。

可引用源码项目，也可通过 `dotnet pack` 构建独立 NuGet 包。在 Newtonsoft.Json 设置中添加闭合 converter。

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

输出为 `{"points":{"42":7}}`。调用方的 Newtonsoft.Json 序列化器控制 DTO 的属性名、字典键和 converter。对于多态模型，可在映射函数中把 `kind` 等对外判别字段映射到支持的 case。

契约 DTO 应独立于模型的继承层次。映射函数处理非空值，在读取成功时返回模型。对外保留哪些数据和对象身份，由应用的映射和 DTO 决定。运行时支持取决于 Newtonsoft.Json 契约解析器及其要求。需要使用源生成元数据时，参阅 [System.Text.Json 适配包](LuminPack.SystemTextJson.cn.md)。
