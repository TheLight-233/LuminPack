# ✍️ 自定义格式化器

[English](custom-formatters.md) · [返回 README](README.cn.md)

## `GeneratorType.Custom`

`[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Custom)]` 用你写在类型上的静态方法代替生成的数据表示。生成器会校验这些方法，并自动注册。注册在首次使用 `LuminPackSerializer` 时运行。

```csharp
using LuminPack.Attribute;
using LuminPack.Core;

[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Custom)]
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
- 诊断 `LuminPack100` 到 `LuminPack108` 会报告：缺少方法或方法不是 static、签名不对、JSON 方法只写了一半、不支持的泛型类型或 Union 根，以及显式关闭注册导致注册被跳过。
- `Custom` 类型作为其他 `[LuminPackable]` 类型的成员时，给该成员标 `[LuminPackableObject]`，让它调用你的格式化器而不是被内联展开。
- 默认的 JSON 注册使用 UTF-16。UTF-8 的 JSON 文档需要为对应的闭合配置单独注册。

<a id="runtime-registration"></a>
## 运行时注册

`LuminPackSerializer.Register<T>` 为泛型兜底入口注册强类型委托。不透明的操作需要显式注册。使用 Next 时，开放调用也可以使用受支持闭合类型的自动注册操作。现代 [Interceptor 后端](generation-modes.cn.md#静态拦截)可将闭合的泛型写法直接绑定到具体 Root，此时泛型写法不表示请求注册分发。旧编译器通过已安装的 IL 后端获得相同静态绑定；两种接入均禁用时，泛型负载 API 使用注册兜底。直接传入方法组即可；注册不需要 `AllowUnsafeBlocks` 或 `unsafe` 块。

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

LuminPackSerializer.Register<LegacyModel>(Write, Read);
```

引用类型没有类型标记且使用手动注册的格式化器时，可在该类型的成员声明上添加 `[LuminPackableObject]` 以选择注册的格式化器。

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
LuminPackSerializer.Register<Person>(WritePerson, ReadPerson);
```

这些回调绑定具体操作。如果在 `WritePerson` 内调用 `Serialize<Person>`，会再次进入同一个已注册泛型操作。


- 同一类型重复注册会抛出 `ArgumentException`。
- 具体 Root 和被拦截的调用从不查询注册表。Next 还会在首次使用时注册内置标量、字符串和已生成的闭合类型操作。显式注册优先。原生成器通过显式注册和 `Custom` 注册执行泛型兜底。缺少注册或所需操作时抛出 `LuminPackException`。设置 `LuminPackRegisterMode.Disabled` 时，兜底操作无法使用 Register。兜底 JSON 边界和输入暂存使用调用程序集的默认选项或显式配置；注册回调负责自身表示以及声明特有的策略。被拦截的 Root 保留声明所属的策略。
- 委托注册项随载荷类型存活。条件弱引用归属允许可回收载荷程序集卸载；不可回收载荷会持续保留注册委托及其目标对象。

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
