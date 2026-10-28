# ✍️ 自定义格式化器

[English](custom-formatters.md) · [返回 README](../README.cn.md)

## `GeneratorType.Custom`

`[LuminPackable(GeneratorType.Custom)]` 用你写在类型上的静态方法代替生成的数据表示。生成器会校验这些方法，并在模块初始化器中自动注册；你不需要自己调用 `Register`。

```csharp
using LuminPack.Attribute;
using LuminPack.Core;

[LuminPackable(GeneratorType.Custom)]
public sealed class MyVector
{
    public float X, Y, Z;

    public static void Serialize(ref LuminPackWriter writer, in MyVector value)
    {
        writer.WriteValue(value.X);
        writer.WriteValue(value.Y);
        writer.WriteValue(value.Z);
    }

    public static void Deserialize(ref LuminPackReader reader, ref MyVector value)
    {
        value ??= new MyVector();
        reader.ReadValue(ref value.X);
        reader.ReadValue(ref value.Y);
        reader.ReadValue(ref value.Z);
    }
}
```

必须恰好提供下面其中一组方法：

| 组合 | 方法 |
| --- | --- |
| 仅二进制 | `Serialize`、`Deserialize` |
| 二进制与 JSON | 再加 `SerializeJson(ref LuminPackJsonWriter, in T)` 和 `DeserializeJson(ref LuminPackJsonReader, ref T)` |
| 二进制、JSON 与大小计算 | 再加 `CalculateOffset(ref LuminPackEvaluator, in T)` |

- 方法必须是 `static`，并且是 `public` 或 `internal`。
- 你的方法负责完整的数据表示：null 处理、复用和字符串编码。
- 诊断 `LuminPack100` 到 `LuminPack108` 会报告：缺少方法或方法不是 static、签名不对、JSON 方法只写了一半、有大小计算但 JSON 不全、不支持的泛型类型或 Union 根，以及未开启 unsafe 导致注册被跳过。
- `Custom` 类型作为其他 `[LuminPackable]` 类型的成员时，给该成员标 `[LuminPackableObject]`，让它调用你的格式化器而不是被内联展开。
- 默认的 JSON 注册使用 UTF-16。UTF-8 的 JSON 文档需要为对应的闭合配置单独注册。

<a id="runtime-registration"></a>
## 运行时注册

`LuminPackSerializer.Register<T>` 为生成器没有生成的类型注册静态方法指针，例如处于所选[生成模式](generation-modes.cn.md)范围之外的类型。需要开启 `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`。

```csharp
static void Write(ref LuminPackWriter writer, in LegacyModel value)
{
    writer.WriteValue(value.Id);
    writer.WriteValue(value.Name);
}

static void Read(ref LuminPackReader reader, ref LegacyModel value)
{
    value = new LegacyModel();
    reader.ReadValue(ref value.Id);
    reader.ReadValue(ref value.Name);
}

unsafe
{
    LuminPackSerializer.Register<LegacyModel>(&Write, &Read);
}
```

| 重载 | 方法 |
| --- | --- |
| `Register<T>(write, read)` | 二进制 |
| `Register<T>(write, read, writeJson, readJson)` | 二进制与 JSON |
| `Register<T>(write, read, writeJson, readJson, calculateOffset)` | 二进制、JSON 与大小计算 |
| `Register<T, TOptions>(...)` | 同上，针对某个闭合配置 |

- 同一类型重复注册会抛出 `ArgumentException`。
- 已生成类型的具体重载从不查询注册表。泛型入口先查显式注册和 `Custom` 注册，再使用该类型的生成入口（按类型和配置在首次使用时创建）。
- 注册不持有强引用，加载在可回收 `AssemblyLoadContext` 中的插件仍然可以卸载。被注册的代码在可能被调用期间必须保持加载。

## 成员级格式化器

只想让某一个成员走你自己的方法时，使用委托类特性，目标方法的签名就是该成员类型的格式化器签名：

```csharp
[LuminPackStaticDelegate(typeof(MySerializer), nameof(MySerializer.WriteSpecial))]
public SpecialData Data;

[LuminPackSingletonDelegate(typeof(MySerializer), nameof(MySerializer.Instance), nameof(MySerializer.WriteSpecial))]
public SpecialData OtherData;

// static void WriteSpecial(ref LuminPackWriter writer, in SpecialData value)
```

`[LuminPackDelegate(instanceName, methodName)]` 使用所在类型的某个实例成员。

## 底层 API

自定义代码使用 `LuminPack.Core` 中的前进式写入器和读取器：

- `LuminPackWriter`：`WriteValue`、`Write<TOptions>(string)`、非托管值和 span、null 标记、对象头和 Union 头。
- `LuminPackReader`：对应的 `ReadValue`、`Read<TOptions>(ref string)`、span 和各种头。读取前用 `EnsureReadable(count)` 校验输入。
- `LuminPackJsonWriter` / `LuminPackJsonReader`：JSON token、属性名和值。
- `LuminPackMarshal`（位于 `LuminPack.Code`）：重新解释引用、访问集合存储、非对齐复制。请遵守其文档中的布局和生命周期规则。
