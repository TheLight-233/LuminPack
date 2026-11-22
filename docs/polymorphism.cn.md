# 🎭 多态（Union）

[English](polymorphism.md) · [返回 README](README.cn.md)

## Union 根类型

标了 `[LuminPackable]` 的接口或抽象类就是 Union 根。生成器会收集程序集中所有标了 `[LuminPackable]` 的实现类，并给每个实现分配一个标签（tag）。也可以用 `[LuminPackUnion(tag, typeof(Case))]` 显式列出 case。

```csharp
[LuminPackable]
[LuminPackUnion(0, typeof(Circle))]
[LuminPackUnion(1, typeof(Square))]
public abstract partial class Shape { }

[LuminPackable] public sealed partial class Circle : Shape { public float Radius; }
[LuminPackable] public sealed partial class Square : Shape { public float Side; }

Shape value = new Circle { Radius = 2 };
byte[] bytes = LuminPackSerializer.Serialize(value);

Shape copy = null!;
LuminPackSerializer.Deserialize(bytes, ref copy);   // copy 是 Circle
```

在带标记的类上启用 `IncludeDerivedTypes = true` 后，生成器会纳入源码中的派生类，无需为每个派生类重复添加 `[LuminPackable]`。发现范围沿类继承链穿过中间类型。各派生类使用自己的选项，仍遵循通常的构造和 Union 规则。

```csharp
[LuminPackable, LuminPackOption(IncludeDerivedTypes = true)]
public abstract partial class Message { }

public abstract partial class NetworkMessage : Message { }
public sealed partial class Ping : NetworkMessage { public int Sequence; }
```

- 在 Next 中，生成 case 数不超过 32 时，两种格式均使用精确运行时类型的 `if` / `else if` 链，也适用于泛型 case。超过 32 时，根类型、各 case 及其外层包含类型必须声明为 `partial`，以生成虚方法。
- 数字标签范围为 0 到 65535。声明使用的标签会保留，不能用于手工注册。生成 case 在两种格式中使用名称哈希。
- 支持泛型 case：开放泛型 case（`typeof(Case<>)`）覆盖实际用到的闭合类型，闭合 case（`typeof(Case<int>)`）优先于它。
- 在 .NET 11 上，两种生成器都支持标了 `[LuminPackable]` 的 C# `union` 声明。二进制和 JSON 都按 case 的声明顺序从零分配标签。未持有 case 的默认值使用空值标记。

各写入档位使用同一线格式。列表、数组、不可变数组和字典逐元素使用根选定的派发档位。读取时对 case 哈希做 `switch`。

Next 的类与接口 Union 使用每个生成 case 的规范 UTF-8 类型名的 32 位 FNV-1a 哈希。二进制写入四字节小端值，JSON 写入无符号十进制数。规范名称包含命名空间，以 `+` 分隔嵌套类型，以尖括号包围全限定类型参数。开放类型参数在规范名称中写为 `!0`、`!1` 等。例如 `Example.Case<System.Int32>` 和 `Example.Case<!0>` 的哈希不同。生成器拒绝冲突、零和 `0xFFFFFFFF`。重命名 case 会改变两种格式的模式。

二进制 `null` 写为四个零字节。注册 case 写入 `0xFFFFFFFF`，后接委托的数字标签和负载。`[LuminPackOption(WideTag = true)]` 选择该注册路径的标签宽度。

## 引用程序集中的类型

生成器只能看到当前程序集的源码。要为引用程序集中声明的类型生成代码，在当前程序集上列出它：

```csharp
[assembly: LuminPackable(typeof(MyLibrary.Model))]
[assembly: LuminPackable(typeof(MyLibrary.Generic<>))]     // 生成实际用到的闭合类型
```

这类类型的私有字段和自动属性与本地类型一样会被序列化。类型本身以及读取时使用的构造函数必须可访问。

可通过[外部类型专属选项](options.cn.md#引用程序集类型的专属选项)为单个导入类型配置生成选项。

引用程序集中的 Union 根也可以这样声明。当前程序集里的 `partial` case 会自动接入这个根类型，不需要手动注册。实现这一点的生成成员在智能提示中是隐藏的。

## 手动注册

如果生成器完全看不到某个 case，可以在运行时注册。注册接收强类型委托，不依赖 `AllowUnsafeBlocks`。直接传入方法组，无需 `&` 或 `unsafe` 块。可以用 `[assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]` 关闭它们。

根类型上生成的 `Register<TCase>` 接收一个标签和四个方法，参数类型都是根类型：

```csharp
static void Write(ref LuminPackWriter writer, ref MyLib.IShape value)
{
    writer.WriteUnionHeader(10);
    writer.WritePolymorphismValue(LuminPackMarshal.As<MyLib.IShape, Hexagon>(ref value));
}

static void Read(ref LuminPackReader reader, ref MyLib.IShape value)
{
    Hexagon item = default!;
    reader.ReadPolymorphismValue(ref item);
    value = LuminPackMarshal.As<Hexagon, MyLib.IShape>(ref item!);
}

// WriteJson(ref LuminPackJsonWriter, ref MyLib.IShape) 写出 {"$type":10,"$value":...}；
// ReadJson(ref LuminPackJsonReader, ref MyLib.IShape) 读取值。

MyLib.IShape.Register<Hexagon>(10, Write, Read, WriteJson, ReadJson);
```

- 标签必须唯一，不能与生成的 case 冲突；宽标签使用 `WriteWideUnionHeader`。
- 四个方法都必须提供。
- 注册时会校验：重复标签、不兼容的 case 类型、缺少方法都会被拒绝。
- 委托注册项随载荷类型存活。条件弱引用归属允许可回收载荷程序集卸载；不可回收载荷会持续保留注册委托及其目标对象。

不属于 Union、又没有生成格式化器的类型，使用 `LuminPackSerializer.Register<T>` 或 `GeneratorType.Custom`，见[自定义格式化器](custom-formatters.cn.md)。

## JSON

Next 将类或接口 Union 的生成 case 写为 `{"$type": hash, "$value": {...}}`。注册 case 使用委托写入的数字标签和负载。落在数字标签范围内的生成哈希均保留，不能用于手工注册。空 Union 写为 `null`。
