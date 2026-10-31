# 🎭 多态（Union）

[English](polymorphism.md) · [返回 README](../README.cn.md)

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

- 根类型、每个 case 以及所有外层包含类型都必须是 `partial`。
- 标签范围 0 到 65535。0 到 249 占 1 字节，更大的占 3 字节。尽量用小标签，并保持稳定：数据中靠标签识别 case。
- 在根类型上标 `[LuminPackWideTag]`，所有 case 都使用宽标签格式。
- 支持泛型 case：开放泛型 case（`typeof(Case<>)`）覆盖实际用到的闭合类型，闭合 case（`typeof(Case<int>)`）优先于它。
- 在 .NET 11 上，标了 `[LuminPackable]` 的 C# `union` 声明会被识别为 Union 根，使用相同的数据格式。

选择 case 的方式在编译期分级，各级的线上格式完全相同：

- **单个 case、无注册。** 未生成 `Register` API，且其他程序集无法再添加 case（根对外不可见、没有对外可访问的构造函数，或声明了 `SingleImplementation`）时，只有一个 sealed case 的根会直接调用它。
- **1 到 4 个 case。** 先逐个做精确类型判断，再回到虚调用；虚调用继续服务注册的和外部的 case。
- **更多 case。** 单次虚调用。
- **固定 case 的列表。** 在 .NET 8 及以上（非 Unity），如果 `List<T>`、数组或 `ImmutableArray<T>` 的所有 case 都是固定记录（public 非托管字段、无回调），不可预测的 256 元素块会按 case 分组，在预先计算的偏移处逐组写入或读取，避免每个元素一次分派误预测。可预测的块（长段同类、单一类型占多数、规律周期）保留逐元素路径。

用 `[LuminPackOption(WideTag = true)]` 选择宽标签。`[LuminPackOption(SingleImplementation = true)]` 声明该根在所有程序集中只有一个实现；只有确定其他程序集不会再加 case 时才能使用。它保留既有 Union 帧，并在无注册时允许直接调用。独立的 `[LuminPackWideTag]` 和 `[LuminPackSingleImplementation]` 特性保留兼容，参见[选项](options.cn.md#union-类型契约开关)。

## 引用程序集中的类型

生成器只能看到当前程序集的源码。要为引用程序集中声明的类型生成代码，在当前程序集上列出它：

```csharp
[assembly: LuminPackable(typeof(MyLibrary.Model))]
[assembly: LuminPackable(typeof(MyLibrary.Generic<>))]     // 生成实际用到的闭合类型
```

这类类型的私有字段和自动属性与本地类型一样会被序列化。类型本身以及读取时使用的构造函数必须可访问。

引用程序集中的 Union 根也可以这样声明。当前程序集里的 `partial` case 会自动接入这个根类型，不需要手动注册。实现这一点的生成成员在智能提示中是隐藏的。

## 手动注册

如果生成器完全看不到某个 case，可以在运行时注册。注册传入的是静态方法指针，所以项目需要开启 `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`；只有开启 unsafe 时生成器才会生成 `Register` API。可以用 `[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Full, LuminPackRegisterMode.Disabled)]` 关闭它们。

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

unsafe
{
    MyLib.IShape.Register<Hexagon>(10, &Write, &Read, &WriteJson, &ReadJson);
}
```

- 标签必须唯一，不能与生成的 case 冲突；宽标签使用 `WriteWideUnionHeader`。
- 四个方法都必须提供。
- 注册时会校验：重复标签、不兼容的 case 类型、缺少方法都会被拒绝。
- 注册不持有强引用，加载在可回收 `AssemblyLoadContext` 中的插件仍然可以卸载。被注册的代码在可能被调用期间必须保持加载。

不属于 Union、又没有生成格式化器的类型，使用 `LuminPackSerializer.Register<T>` 或 `GeneratorType.Custom`，见[自定义格式化器](custom-formatters.cn.md)。

## JSON

Union 值写为 `{"$type": tag, "$value": {...}}`，`null` 写为 `null`。双方必须使用相同的标签与类型对应关系。
