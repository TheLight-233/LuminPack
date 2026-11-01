# ✍️ 自定义格式化器

[English](custom-formatters.md) · [返回 README](README.cn.md)

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

- 方法必须是 `static`，并且是 `public` 或 `internal`。
- 你的方法负责完整的数据表示：null 处理、复用和字符串编码。
- 诊断 `LuminPack100` 到 `LuminPack108` 会报告：缺少方法或方法不是 static、签名不对、JSON 方法只写了一半、不支持的泛型类型或 Union 根，以及未开启 unsafe 导致注册被跳过。
- `Custom` 类型作为其他 `[LuminPackable]` 类型的成员时，给该成员标 `[LuminPackableObject]`，让它调用你的格式化器而不是被内联展开。
- 默认的 JSON 注册使用 UTF-16。UTF-8 的 JSON 文档需要为对应的闭合配置单独注册。

<a id="runtime-registration"></a>
## 运行时注册

`LuminPackSerializer.Register<T>` 为泛型入口注册静态方法指针。调用 `Serialize<T>`、`Deserialize<T>` 或 `Serialize<T, TOptions>` 等泛型负载 API 时，内置类型和源生成类型也需要注册。具体类型重载仍直接调用生成的 Root。注册需要 `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`。

```csharp
static void Write(ref LuminPackWriter writer, in LegacyModel value)
{
    writer.Write(value.Id);
    writer.Write(value.Name);
}

static void Read(ref LuminPackReader reader, ref LegacyModel value)
{
    value = new LegacyModel();
    reader.Read(ref value.Id);
    reader.Read(ref value.Name);
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
| `Register<T, TOptions>(...)` | 同上，针对某个闭合配置 |

对于已经生成的模型，回调可以直接调用其具体状态重载：

```csharp
static void WritePerson(ref LuminPackWriter writer, in Person value)
    => LuminPackSerializer.Serialize(ref writer, in value);
static void ReadPerson(ref LuminPackReader reader, ref Person value)
    => LuminPackSerializer.Deserialize(ref reader, ref value);
unsafe { LuminPackSerializer.Register<Person>(&WritePerson, &ReadPerson); }
```

这些回调绑定具体操作。如果在 `WritePerson` 内调用 `Serialize<Person>`，会再次进入同一个已注册泛型操作。


- 同一类型重复注册会抛出 `ArgumentException`。
- 已生成类型的具体重载从不查询注册表。泛型入口仅使用显式注册和 `Custom` 注册。缺少注册或所需操作时抛出 `LuminPackException`，不存在自动生成兜底。未启用 unsafe 或设置 `LuminPackRegisterMode.Disabled` 时，泛型负载操作无法使用 Register。泛型 JSON 边界和输入暂存使用调用程序集的默认选项或显式配置；注册回调负责自身表示以及声明特有的策略。
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
