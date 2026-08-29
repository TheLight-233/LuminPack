# **LuminPack**

## 📑 目录

- [📖 简介](#intro)
- [📦 Installation 安装](#installation)
- [🔮 后续更新计划](#roadmap)
- [🚀 Quick Start 快速开始](#quickstart)
- [📋 LuminPack基础类型](#basetypes)
- [🔧 内置支持的类型](#builtin-types)
- [🎯 定义 `[LuminPackable]` 数据](#define-data)
- [🔍 `[LuminPackableObject]`](#luminpackableobject)
- [⚙️ 生成模式（源码生成器）](#generation-modes)
- [🔄 反序列化缓存池](#deserialize-pool)
- [🎭 多态序列化](#polymorphism)
- [🌐 跨程序集多态 / Register 手动注册](#cross-assembly)
- [📝 版本容忍](#version-tolerant)
- [🔗 循环引用](#circular-reference)
- [💾 WriteBuffer池](#writebuffer)
- [🎮 Unity](#unity)
- [📜 JSON 格式规范](#json-spec)
- [📐 二进制格式规范](#binary-spec)

<a id="intro"></a>
## 📖 简介

LuminPack 是一款面向 Unity 存档、网络传输等场景的高性能序列化库，同时支持二进制与 JSON 序列化。项目通过增量源代码生成器为具体类型生成专用解析代码，并针对 Unity、AOT 以及 .NET Standard 2.1 等使用环境进行适配。

LuminPack 在早期设计和实现过程中学习、借鉴了 MemoryPack 的优秀思路与实践，README 的组织方式也受到了其启发。随着功能持续迭代，LuminPack 已根据自身目标和使用场景进行独立设计与演进。

### ✨ 主要特性

*   **几乎零运行时开销**：无反射、无运行时委托 / 字典缓存、无对象图查找——热路径全部是生成代码对具体方法的直接调用，JIT 可直接内联，序列化过程不产生虚调用与托管分配。
*   **ALC 安全**：运行时不存在进程级 `Type` / 委托静态缓存；泛型派发与多态派发以 MethodTable 地址为键、值仅为 `int` 标识，可回收 `AssemblyLoadContext` 中的插件程序集可以正常卸载，无静态 provider 泄漏问题。
*   **基于增量源代码生成器**：为每个 `[LuminPackable]` 类型在编译期生成专用解析代码；支持按需剪枝的生成模式（`Full` / `Medium` / `Light` / `Minimal`），在生成代码量与编译时长之间取舍。
*   **支持现代 I/O API**：`ReadOnlySpan<byte>`、`ReadOnlySequence<byte>`，并可直接序列化进任意 `IBufferWriter<byte>`（如 `System.IO.Pipelines.PipeWriter`）。
*   **同时支持二进制与 JSON**：两套独立协议，按场景选择。
*   **基于非托管内存的 WriterBuffer**：原生缓冲 + 线程静态 / 集中池，扩容与复用几乎零分配。
*   **多态序列化**：接口 / 抽象类 union 自动收集派生类型，支持跨程序集注册；原生支持 .NET 11 / C# 15 的 `union` 类型（含泛型 union 与值类型 case）。
*   **版本容忍**：快速（Object）与完全（VersionTolerant）两种模式。
*   **循环引用序列化**：引用跟踪，支持对象环。
*   **反序列化缓存池**：可复用实例，进一步降低 GC。
*   **AOT 友好**：无反射、无动态代码生成，适配 .NET AOT、Unity Mono 与 IL2CPP，覆盖 `netstandard2.1` 至 `net11.0`。

<a id="installation"></a>
## 📦 Installation 安装

### .NET 项目

LuminPack 支持 `netstandard2.1`、`net8.0`、`net9.0`、`net10.0` 和 `net11.0`。只需安装 `LuminPack`，NuGet 会自动安装匹配版本的 `LuminPackGenerator` 依赖：

```shell
dotnet add package LuminPack
```

也可以在 IDE 的 NuGet 包管理器中搜索 `LuminPack`，或直接在项目文件中添加：

```xml
<ItemGroup>
  <PackageReference Include="LuminPack" Version="1.1.7" />
</ItemGroup>
```

`LuminPack` 和 `LuminPackGenerator` 是两个独立发布的 NuGet 包。`LuminPack` 通过公开依赖自动引入匹配版本的源代码生成器，生成器不会被打包进 `LuminPack` 本身。

### Unity 项目

Unity 项目需启用 **.NET Standard 2.1** API 兼容级别。可以通过 NuGetForUnity 等 NuGet 工作流安装 `LuminPack`；如果手动导入，请确保：

1. 将 `netstandard2.1` 版本的 `LuminPack.dll` 及其依赖放入 Unity 项目的 `Assets/Plugins` 目录。
2. 将 `LuminPackGenerator.dll` 放入 `Assets/RoslynAnalyzers` 目录，并在 Unity Inspector 中为它添加 `RoslynAnalyzer` 标签。
3. 完成导入后，用 `[LuminPackable]` 声明一个类型并确认项目能正常编译，以验证源代码生成器已被 Unity 识别。

LuminPack 支持 Unity Mono 和 IL2CPP。建议在发布前至少执行一次目标平台的 IL2CPP Player 运行测试，不要仅以编译成功作为验证结果。

### 从源码引用

也可以将仓库中的两个项目加入解决方案，并按以下方式引用：

```xml
<ItemGroup>
  <ProjectReference Include="path/to/LuminPack/src/LuminPack/LuminPack.csproj" />
  <ProjectReference Include="path/to/LuminPack/src/LuminPackGenerator/LuminPackGenerator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

<a id="roadmap"></a>
## 🔮 后续更新计划

1.  内置加密模块

<a id="quickstart"></a>
## 🚀 Quick Start 快速开始

定义要序列化的结构体或类，并用 `[LuminPackable]` 属性对其进行注释。

```csharp
using LuminPack;

[LuminPackable]
public class Person
{
    public int Age { get; set; }
    public string Name { get; set; }
}
```

序列化代码将由c#源代码生成器功能生成。

调用 `LuminPackSerializer.Serialize<T>/Deserialize<T>` 序列化/反序列化二进制对象实例。

调用 `LuminPackSerializer.SerializeJson<T>/DeserializeJson<T>` 序列化/反序列化Json对象实例。

```csharp
var item = new Person { Age = 18, Name = "Light" };

var buffer = LuminPackSerializer.Serialize(item);
var bufferJson = LuminPackSerializer.SerializeJson(item);
var result = LuminPackSerializer.Deserialize<Person>(buffer);
var resultJson = LuminPackSerializer.DeserializeJson<Person>(bufferJson);
```

<a id="basetypes"></a>
## 📋 LuminPack基础类型

默认情况下，LuminPack基础类型将实现最高性能

```
Int, UInt, Byte, Short, UShort, Long, ULong, 
Float, Double, Char, String, Decimal, Bool, 
Enum, Struct, Class, List, Array
```

<a id="builtin-types"></a>
## 🔧 内置支持的类型

默认情况下，这些类型可以被序列化：

*   .Net所有非托管类型 (`byte`, `int`, `bool`, `char`, `double`, etc.)
*   `string`, `decimal`, `Half`, `Int128`, `UInt128`, `Guid`, `Rune`, `BigInteger`
*   `TimeSpan`, `DateTime`, `DateTimeOffset`, `TimeOnly`, `DateOnly`, `TimeZoneInfo`
*   `Complex`, `Plane`, `Quaternion` `Matrix3x2`, `Matrix4x4`, `Vector2`, `Vector3`, `Vector4`
*   `Uri`, `Version`, `StringBuilder`, `Type`, `BitArray`, `CultureInfo`
*   `T[]`, `T[,]`, `T[,,]`, `T[,,,]`, `Memory<>`, `ReadOnlyMemory<>`, `ArraySegment<>`, `ReadOnlySequence<>`
*   `Nullable<>`, `Lazy<>`, `KeyValuePair<,>`, `Tuple<,...>`, `ValueTuple<,...>`
*   `List<>`, `LinkedList<>`, `Queue<>`, `Stack<>`, `HashSet<>`, `SortedSet<>`, `PriorityQueue<,>`
*   `Dictionary<,>`, `SortedList<,>`, `SortedDictionary<,>`, `ReadOnlyDictionary<,>`
*   `Collection<>`, `ReadOnlyCollection<>`, `ObservableCollection<>`, `ReadOnlyObservableCollection<>`, `ReadOnlyCollectionBuilder<>`
*   `IEnumerable<>`, `ICollection<>`, `IList<>`, `IReadOnlyCollection<>`, `IReadOnlyList<>`, `ISet<>`
*   `IDictionary<,>`, `IReadOnlyDictionary<,>`, `ILookup<,>`, `IGrouping<,>`,
*   `ConcurrentBag<>`, `ConcurrentQueue<>`, `ConcurrentStack<>`, `ConcurrentDictionary<,>`, `BlockingCollection<>`
*   Immutable collections (`ImmutableList<>`, etc.) and interfaces (`IImmutableList<>`, etc.)

<a id="define-data"></a>
## 🎯 定义 `[LuminPackable]` 数据

`[LuminPackable]` 可以注释到任何 `class` ,  `abstract class`  ,  `struct`  ,  `record` ,  `record struct` 和 `interface` 。如果类型 `struct` 或 `record struct` 不包含引用类型（c#非托管类型），则不使用任何直接从内存序列化/反序列化的规则，LuminPack会直接复制内存。

默认情况下， `[LuminPackable]` 序列化公共实例属性或字段。可以使用 `[LuminPackIgnore]` 删除序列化目标， `[LuminPackInclude]` 将私有成员提升为序列化目标。

```csharp
[LuminPackable]
public class MyClass
{
    [LuminPackInclude]
    private MyClass2 myClass;
}

[LuminPackable]
public class MyClass2
{
    [LuminPackInclude]
    private int num1;
    [LuminPackIgnore]
    public long num2;
    public short num3;
    public double num4;
    public float num5;
    public string[] strings;
}
```

LuminPack不序列化成员名或其他信息，而是按照声明的顺序序列化字段。如果类型是继承的，则按照父级→子级的内存布局顺序执行序列化。成员的顺序不能因反序列化而改变。关于模式演变，请参阅版本容忍部分。

默认的序列化顺序和布局是按照声明顺序的，如果想要更改，您可以使用 `[LuminPackOrder()]`

注：对于循环引用和完全版本容忍模式，每个字段和属性必须注释`[LuminPackOrder()]`

```csharp
// serialize Prop0 -> Prop1
[LuminPackable]
public class MyClass
{
    [LuminPackOrder(1)]
    public int Prop1 { get; set; }
    [LuminPackOrder(0)]
    public int Prop0 { get; set; }
}
```

LuminPack不依赖构造函数反序列化，因此您可以随意定义构造函数。

LuminPack默认支持 **`0 ~ 249`** 个成员字段

<a id="luminpackableobject"></a>
## 🔍 `[LuminPackableObject]`

\[LuminPackableObject]可以作用于任何字段以及属性，这将告诉LuminPackCodeGenerator不要直接解析该字段并写入Myclass的解析器，而是通过注册在LuminPack的Myclass2的解析器去解析。通常情况下，这会损失大概30%的性能，因此如果您遇到源代码生成器生成错误代码等情况，可以尝试用\[LuminPackableObject]标记字段或属性。

**以LuminPackable的示例代码为例**。在 **.Net8** 以上的平台, 对于嵌套类私有字段的解析，注释\[LuminPackInclude]将会 **正常工作**。但在 **.Net Standard2.1** 平台，**这将不会工作**。

例如以上示例代码，对于MyClass的 **"private MyClass2 myClass;"** 字段，使用\[LuminPackInclude]将会正常工作并解析MyClass2的所有 **public** 字段，但是不会解析Myclass2的 **private** 字段, 即使您在Myclass2的 **private** 字段标记\[LuminPackInclude]。如果想要正常工作，请使用\[LuminPackableObject]来取消基础类型的解析。

```csharp
[LuminPackable]
public class MyClass
{
    [LuminPackInclude]
    [LuminPackableObject] //这将使MyClass2的私有字段num1正常解析
    private MyClass2 myClass;
}
```

### 序列化回调

在实例方法或静态方法标记\[LuminPackOnSerialized]，\[LuminPackOnSerializing]，\[LuminPackOnDeserialized]，\[LuminPackOnDeserializing]等特性

```csharp
[LuminPackOnSerializing]
public static void OnSerializing()
{
    Console.WriteLine("OnSerializing");
}
    
[LuminPackOnSerialized]
public void OnSerialized()
{
    Console.WriteLine("OnSerialized");
}
    
[LuminPackOnDeserialized]
public void OnDeserialized()
{
    Console.WriteLine("OnDeserialized");
}
    
[LuminPackOnDeserializing]
public static void OnDeserializing()
{
    Console.WriteLine("OnDeserializing");
}
```

<a id="generation-modes"></a>
## ⚙️ 生成模式（源码生成器）

默认情况下，LuminPack 的源码生成器会为项目中**观察到的每一个类型**生成格式化扩展方法（`Full` 模式）。
对于大型项目，这会产生大量从未真正序列化的类型的扩展方法，拖慢编译。你可以通过生成模式开关，
让生成器**只生成真正会被序列化的类型**的扩展方法，从而减少生成代码、加快编译。

### 四种模式（从最省心到最激进）

| 模式 | 生成范围 |
| --- | --- |
| `Full`（默认） | 为项目中观察到的每一个类型生成扩展方法（最省心，但生成的代码最多） |
| `Medium` | 只生成可达类型，但**额外保留 `public` 非 `[LuminPackable]` 类型**（公共类型属于保守的 API 表面，可能被跨程序集或反射序列化） |
| `Light` | 只生成从 `LuminPackSerializer.*` 调用点（含泛型包装方法的具体实例化）和**所有** `[LuminPackable]` 成员图可达的类型；`internal`/`private` 类型被严格剪枝 |
| `Minimal` | 最激进。**只从 `LuminPackSerializer.*` 调用点出发**，只有真正被调用的类型才会被生成；即使标了 `[LuminPackable]` 但从未被序列化的类型也会被剪掉 |

各档位的差异在于"根"的范围：

*   `Full`：根 = 项目中所有类型。
*   `Medium` / `Light`：根 = 所有 `[LuminPackable]` 类型 + 所有序列化调用点；`Medium` 再多保留 `public` 类型。
*   `Minimal`：根 = 只有 `LuminPackSerializer.*` 调用点。

### 开启方式（两种任选其一）

**方式一：MSBuild 属性（推荐）**，在 `.csproj` 的 `<PropertyGroup>` 中声明，并在项目中让生成器能读取该属性：

```xml
<PropertyGroup>
  <LuminPackGenerationMode>Light</LuminPackGenerationMode>
  <!-- 或 <LuminPackGenerationMode>Medium</LuminPackGenerationMode> -->
  <!-- 或 <LuminPackGenerationMode>Minimal</LuminPackGenerationMode> -->
</PropertyGroup>

<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackGenerationMode" />
</ItemGroup>
```

> 说明：`CompilerVisibleProperty` 是 Roslyn 生成器读取 MSBuild 属性的标准机制，必须同时添加，
> `build_property.LuminPackGenerationMode` 才会暴露给生成器。

**方式二：程序集特性**，在任意 `.cs` 文件中：

```csharp
using LuminPack.Attribute;

[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]
```

两种方式都开启时，取更激进的一档生效。

### 剪枝档位的可达性规则

*   **调用点**：所有 `LuminPackSerializer.*` 调用（`Serialize` / `Deserialize` / `SerializeJson` / `DeserializeJson` 等）传入的类型。
*   **泛型包装方法**：例如 `void Save<T>(T x) => Serialize(x);`，生成器会从 `Save<MyStruct>()` 等具体调用点
    解析 `T`，并生成 `MyStruct` 的扩展方法。
*   **类型实参图**：对种子类型递归展开其类型实参、数组元素、包含类型。
*   **多态成员**：可达的 union（接口/抽象类）会连带展开其派生类型，保证多态派发完整。

### 注意事项

*   `Light` / `Minimal` 模式下，**无法静态证明可达的序列化**（如通过反射、`typeof` 驱动、跨程序集直接序列化非 `[LuminPackable]`
    类型）会像"未注册 formatter"一样在运行期抛出异常。这是刻意的取舍：剪枝只保留可证明会被序列化的类型。
    若你的场景依赖这类用法，请使用 `Full` 或 `Medium`。
*   `Minimal` 会剪掉**声明了但从未被序列化**的 `[LuminPackable]` 类型，请确认你的序列化都发生在当前程序集的静态调用点上，
    否则请回退到 `Light`。
*   剪枝只影响**生成代码量**，不影响**运行期性能**——可达类型的格式化方法完全相同。

<a id="deserialize-pool"></a>
## 🔄 反序列化缓存池

LuminPack支持反序列化从缓存池取代new创建实例，减少GC开销。

LuminPack并不实现缓存池逻辑，需要用户自行实现，同时用户也应该注意Return实例，LuminPack并不追踪对象去Return

要启用缓存池，需要在静态方法上标记 **\[LuminPackPoolRent]** 特性

```csharp
[LuminPackPoolRent]
public static SimpleClass Rent()
{
    return this.Pool.Rent();
}
```

<a id="polymorphism"></a>
## 🎭 多态序列化

LuminPack支持序列化接口和抽象类对象，实现多态序列化。

LuminPack支持自动收集继承类，对于标记了\[LuminPackable]特性的abstract，interface，LuminPack会自动收集子类

LuminPack 同时支持手动注册Union。只有接口和抽象类可以使用 `[LuminPackUnion]` 属性，每个派生类型需要配置唯一的 Union Tag。

```csharp
// Annotate [LuminPackable] and inheritance types with [LuminPackUnion]
// Union also supports interface class
[LuminPackable]
[LuminPackUnion(0, typeof(Child1))] //Child1和Child2会被自动收集，也可以像示例一样手动注册
[LuminPackUnion(1, typeof(Child2))]
public abstract class IUnionSample
{
}

[LuminPackable]
public class Child1 : IUnionSample
{
    public int num;
}

[LuminPackable]
public class Child2 : IUnionSample
{
    public string str;
}

IUnionSample data = new Child1() { num = 114514};

// Serialize
var buffer = LuminPackSerializer.Serialize(data);

// Deserialize
var result = LuminPackSerializer.Deserialize<IUnionSample>(buffer);

switch (result)
{
    case Child1 x:
        Console.WriteLine(x.num);
        break;
    case Child2 x:
        Console.WriteLine(x.str);
        break;
    default:
        break;
}
```

对于`LuminPackUnion`的Tag，支持 `0`  \~  `65535`， 对与`250`以下的性能更佳。因此推荐使用`250`以下的值作为Tag

<a id="cross-assembly"></a>
## 🌐 跨程序集多态 / Register 手动注册

> **⚠️ 使用 Register 需要开启 Unsafe**：Register 注册 API 以**函数指针（`delegate*`）** 方式传入静态方法，调用处需要 `unsafe` 上下文。
> 因此使用 Register 的项目必须在 `.csproj` 中开启 `AllowUnsafeBlocks`：
>
> ```xml
> <PropertyGroup>
>   <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
> </PropertyGroup>
> ```
>
> 开启后源生成器才会生成 `Register` 方法。如果你开启了 unsafe 但**不希望生成 Register 相关代码**，
> 可以通过 `[LuminPackGeneratorOptions]` 的 `register` 参数显式关闭：
>
> ```csharp
> using LuminPack.Attribute;
>
> [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Full, LuminPackRegisterMode.Disabled)]
> ```
>
> `LuminPackRegisterMode.Auto`（默认）：跟随 unsafe 设置，开启 unsafe 才生成；`Disabled`：即使开启 unsafe 也不生成 Register 相关代码。

LuminPack 有两处运行时手动注册机制，均以 **函数指针（`delegate*`）** 方式传入静态方法。注册表以 **MethodTable 地址（`nint`）** 为键、
存储方法指针，不持有强 `Type` 引用，保持 ALC 安全。注册后生成类型的**热路径不受任何影响**，
只有未生成代码的类型（或未列入 union 的类型）才会走注册表。

### 1️⃣ `LuminPackSerializer.Register<T>` —— 泛型派发兜底

用于源码生成器**没有生成对应代码**的类型（例如未标记 `[LuminPackable]`、或处于剪枝模式且从未出现在任何序列化调用点上的类型）。
泛型派发 `WriteValue<T>/ReadValue<T>` 的 default 分支按 MethodTable 查询该注册表并直接调用注册的方法指针（类型化签名，零装箱）。

用户需要手写与下面签名一致的静态方法（参数为**具体类型**，而不是 `object`）：

```csharp
private static void WriteLegacy(ref LuminPackWriter writer, in LegacyModel value)
{
    writer.WriteValue(value.Id);
    writer.WriteValue(value.Name);
}

private static void ReadLegacy(ref LuminPackReader reader, ref LegacyModel value)
{
    value = new LegacyModel();
    reader.ReadValue(ref value.Id);
    reader.ReadValue(ref value.Name);
}

// 二进制只注册写/读即可；JSON 与大小计算为可选重载
unsafe
{
    LuminPackSerializer.Register<LegacyModel>(
        &WriteLegacy, &ReadLegacy,
        &WriteJsonLegacy, &ReadJsonLegacy);   // 可选：&WriteJsonLegacy / &ReadJsonLegacy / &CalcSizeLegacy
}
```

可用重载：

| 重载 | 参数 |
| --- | --- |
| `Register<T>(writeValue, readValue)` | 二进制写 / 读 |
| `Register<T>(writeValue, readValue, writeValueJson, readValueJson)` | 二进制 + JSON |
| `Register<T>(writeValue, readValue, writeValueJson, readValueJson, calculateOffset)` | 二进制 + JSON + 大小计算 |

JSON 写/读方法签名分别为 `(ref LuminPackJsonWriter writer, in T value)` 与 `(ref LuminPackJsonReader reader, ref T value)`，
大小计算方法签名为 `(ref LuminPackEvaluator evaluator, in T value)`。重复注册同一类型会抛出 `ArgumentException`。

### 2️⃣ 多态基类 `.Register<TMember>` —— 跨程序集多态注册

如果程序集 A 定义了 `[LuminPackable]` 的接口 / abstract 类（union 基类），程序集 B 的类继承了它，由于源生成器只能分析本程序集，
A 的生成器**看不到 B 的子类**，不会为它生成 union 槽实现——基类生成的 `__LuminPackUnionSerialize_xxx` 等槽方法内部会直接 Throw。
此时需要用户手动注册：源生成器会为基类生成一个静态字段 `LuminCircleReferenceMap`（MethodTable → 方法指针）以及 `Register<TMember>` 方法。

用户手写的方法签名以**基类类型**为参数，内部把基类引用转为具体子类后写字段：

```csharp
// 基类：global::MyLib.IFoo（程序集 A）；子类：global::MyApp.FooA（程序集 B，[LuminPackable]）

private static void WriteFooA(ref LuminPackWriter writer, ref global::MyLib.IFoo value)
{
    writer.WriteUnionHeader(0);                                  // tag 由你指定，须与 Register 的 tag 一致
    writer.WritePolymorphismValue(LuminPackMarshal.As<global::MyLib.IFoo, global::MyApp.FooA>(ref value));
}

private static void ReadFooA(ref LuminPackReader reader, ref global::MyLib.IFoo value)
{
    global::MyApp.FooA tempValue = default!;
    reader.ReadPolymorphismValue(ref tempValue);
    value = LuminPackMarshal.As<global::MyApp.FooA, global::MyLib.IFoo>(ref tempValue!);
}

private static void WriteJsonFooA(ref LuminPackJsonWriter writer, ref global::MyLib.IFoo value)
{
    writer.WriteObjectStart();
    if (writer.Option.StringEncoding == LuminPack.Option.LuminPackStringEncoding.UTF8)
        writer.WritePropertyName(LuminPackConstUtf8.TypeU8);
    else
        writer.WritePropertyName(LuminPackConstUtf8.TypeU16);
    writer.WriteInt(0);
    if (writer.Option.StringEncoding == LuminPack.Option.LuminPackStringEncoding.UTF8)
        writer.WritePropertyName(LuminPackConstUtf8.ValueU8);
    else
        writer.WritePropertyName(LuminPackConstUtf8.ValueU16);
    writer.WriteValue(LuminPackMarshal.As<global::MyLib.IFoo, global::MyApp.FooA>(ref value)!);
    writer.WriteObjectEnd();
}

private static void ReadJsonFooA(ref LuminPackJsonReader reader, ref global::MyLib.IFoo value)
{
    global::MyApp.FooA tempValue = default!;
    reader.ReadValue(ref tempValue);
    value = LuminPackMarshal.As<global::MyApp.FooA, global::MyLib.IFoo>(ref tempValue!);
}

// 最后调用基类的 Register（TMember 为具体子类，tag 与写入的 union header 一致）
unsafe
{
    global::MyLib.IFoo.Register<global::MyApp.FooA>(0,
        &WriteFooA, &ReadFooA, &WriteJsonFooA, &ReadJsonFooA);
}
```

说明：

* `tag` 是写入线格式的 union 标签（`WriteUnionHeader(tag)`），反序列化时按 tag 查询注册表；tag 不能与基类生成器已生成的成员 tag 冲突。
* 二进制写 / 读、JSON 写 / 读四个方法指针均为必填；`Sizeof` 对该类注册成员仍会抛出（与生成成员的槽行为一致）。
* 注册表定义在基类 partial 上（基类自身程序集生成），`Register` 调用与序列化发生在同一进程内即可，跨程序集使用。
* 同样适用于"基类声明在序列化程序集、子类未被生成器列入 union"的场景（如未标记 `[LuminPackable]` 的子类）。

<a id="version-tolerant"></a>
## 📝 版本容忍

在默认情况下 LuminPack的代码生成模式（ `GenerateType.Object` ）， 仅支持有限的模式演化。

*   如果数据类型是非托管数据，例如Struct（不包含引用类型）。不能更改数据
*   可以添加成员，不能删除成员。
*   不能更改成员名称
*   不能更改成员顺序
*   不能更改成员类型

```csharp
[LuminPackable]
public class MyClass
{
    public int Prop1 { get; set; }
    public long Prop2 { get; set; }
}

// Add is OK.
[LuminPackable]
public class MyClass
{
    public int Prop1 { get; set; }
    public long Prop2 { get; set; }
    public int? AddedProp { get; set; }
}

// Remove is NG.
[LuminPackable]
public class MyClass
{
    // public int Prop1 { get; set; }
    public long Prop2 { get; set; }
}

// Change order is NG.
[LuminPackable]
public class MyClass
{
    public long Prop2 { get; set; }
    public int Prop1 { get; set; }
}
```

当使用 `GenerateType.VersionTolerant` 时，它支持完全的版本容忍。

```csharp
[LuminPackable(GenerateType.VersionTolerant)]
public class VersionTolerantObject1
{
    [LuminPackOrder(0)]
    public int MyProperty0 { get; set; } = default;

    [LuminPackOrder(1)]
    public long MyProperty1 { get; set; } = default;

    [LuminPackOrder(2)]
    public short MyProperty2 { get; set; } = default;
}

[LuminPackable(GenerateType.VersionTolerant)]
public class VersionTolerantObject2
{
    [LuminPackOrder(0)]
    public int MyProperty0 { get; set; } = default;

    // deleted
    //[LuminPackOrder(1)]
    //public long MyProperty1 { get; set; } = default;

    [LuminPackOrder(2)]
    public short MyProperty2 { get; set; } = default;

    // added
    [LuminPackOrder(3)]
    public short MyProperty3 { get; set; } = default;
}
```

`GenerateType.VersionTolerant` 比 `GenerateType.Object` 性能更差，使用时请注意。

<a id="circular-reference"></a>
## 🔗 循环引用

```csharp
// to enable circular-reference, use GenerateType.CircularReference
[LuminPackable(GenerateType.CircularReference)]
public class Node
{
    [LuminPackOrder(0)]
    public Node? Parent { get; set; }
    [LuminPackOrder(1)]
    public Node[]? Children { get; set; }
}
```

`GenerateType.CircularReference` 具有与版本容忍相同的特性。

对象引用跟踪只对标记为 `GenerateType.CircularReference` 的对象进行。如果要跟踪任何其他对象，请对其进行包装。

<a id="writebuffer"></a>
## 💾 WriteBuffer池

LuminPack的序列化池通过Marshal申请非托管内存，这极大提高了Buffer扩容的性能。

因此，请确保所有WriteBuffer调用Dispose方法，以释放非托管内存。

<a id="unity"></a>
## 🎮 Unity

LuminPack 针对 Unity 和 .NET Standard 2.1 进行了专门适配与优化，并支持增量源代码生成。

*   针对常用集合和非托管泛型提供 Unity 专用实现。
*   支持 Mono 与 IL2CPP 运行环境。

<a id="json-spec"></a>
## 📜 JSON 格式规范

LuminPack 按照以下规则生成 JSON 文本。默认使用 UTF-8 处理字符串，也可通过 `LuminPackSerializerOption.Utf16` 选择 UTF-16。序列化和反序列化必须使用一致的字符编码选项。

### 基础值

| C# 值 | JSON 形式 |
| --- | --- |
| `null` | `null` |
| 整数、浮点数、`decimal` | JSON number |
| `bool` | `true` / `false` |
| `string`、`char` | JSON string，并按 JSON 规则转义 |
| `Guid`、`DateTime`、`DateTimeOffset`、`TimeSpan` 等 | JSON string |

JSON number 只能保证表示有限浮点值；`NaN`、`PositiveInfinity` 和 `NegativeInfinity` 不属于标准 JSON 数字，不应用于需要跨库交换的 JSON 数据。LuminPack 在序列化这些值时抛出异常，反序列化时超出目标类型表示范围的数字（如 `1e999`）同样被拒绝。反序列化默认限制 JSON 嵌套深度（`LuminPackSerializerOption.MaxJsonDepth`，默认 1024）。

### 对象

标记了 `[LuminPackable]` 的类和结构体序列化为 JSON object，属性名与生成器选中的 C# 成员名一致。例如：

```json
{
  "Age": 18,
  "Name": "Light"
}
```

反序列化时，未知属性会被跳过；JSON 中缺失的成员保持其默认值。属性顺序不用于匹配，但名称和值类型应与目标模型一致。

### 集合、字典与元组

*   一维数组和顺序集合写为 JSON array：`[1, 2, 3]`。
*   多维数组写为嵌套 JSON array：`[[1, 2], [3, 4]]`；反序列化时各维必须是规则的矩形结构。
*   `KeyValuePair<TKey, TValue>` 和其他键值对写为两个元素的 JSON array：`[key, value]`。
*   字典写为键值对数组：`[[key1, value1], [key2, value2]]`。这种表示允许键使用任意 LuminPack 支持的类型，不限于字符串。
*   `Tuple`、`ValueTuple` 等固定长度组合值写为按成员顺序排列的 JSON array。

### Union 多态

Union 值使用包含 `$type` 和 `$value` 的对象表示。`$type` 是 `[LuminPackUnion]` 配置的 Tag，`$value` 是实际派生对象：

```json
{
  "$type": 0,
  "$value": {
    "num": 114514
  }
}
```

Union 的 `null` 值直接写为 `null`。客户端与服务端交换 JSON 时，必须保持 Union Tag 和对应类型的映射一致。

### 循环引用

`GenerateType.CircularReference` 模式使用 `$id` 记录首次出现的对象，后续重复引用使用 `$ref` 指向该 ID。这两个名称是 LuminPack 保留的元数据属性，不应用作普通成员名。

> JSON 格式与二进制格式是两套独立协议，不能将 `Serialize` 的二进制结果传给 `DeserializeJson`，反之亦然。

<a id="binary-spec"></a>
## 📐 二进制格式规范

端序必须 `Little Endian` 。

### 非托管结构

非托管结构是不包含引用类型的c#结构，类似于c#非托管类型的约束。序列化结构布局，包括填充。

### Object 对象

`(byte memberCount, [values...])`

对象头文件中的成员计数为1字节无符号字节。成员数允许 `0` 到 `249` ,  `255` 表示对象 `null` 。值存储成员数的内存包值。

### Version Tolerant Object 版本容忍对象

`(byte memberCount, [varint byte-length-of-values...], [values...])`

版本容忍对象与 Object 类似，但在头部包含值的字节长度。变长整数遵循以下规范：第一个有符号字节（sbyte）是值或类型代码，接下来的 X 个字节是具体值。其中，0 到 127 对应无符号字节值，-1 到 - 120 对应有符号字节值，-121 对应字节（byte），-122 对应有符号字节（sbyte），-123 对应无符号短整数（ushort），-124 对应短整数（short），-125 对应无符号整数（uint），-126 对应整数（int），-127 对应无符号长整数（ulong），-128 对应长整数（long）。

### Circular Reference Object 循环引用对象

`(byte memberCount, [varint byte-length-of-values...], varint referenceId, [values...])`\
`(250, varint referenceId)`

循环引用对象类似于版本容忍对象，但如果memberCount为250，则下一个变量（unsigned-int32）为referenceId。如果不是，则在字节长度值之后写入变量referenceId。

### String 字符串

`(int utf16-length, utf16-value)`\
`(int utf8-byte-count, int utf16-length, utf8-bytes)`

字符串有两种形式，UTF16和UTF8。Length 模式下第一个4字节有符号整数 `-1` 表示 `null`， `0` 表示空字符串 `""`。UTF16与collection相同（序列化为 `ReadOnlySpan<char>` ， UTF16 -value的字节数为UTF16 -length \* 2）。UTF8 模式下第一个整数为 utf8 字节数，第二个整数为 utf16 字符数（`-1` 表示未知，读取时按慢路径解码）。Utf8-bytes存储 utf8-byte-count 的字节数。

Token 模式没有 null 标记，`null` 与空字符串统一编码为空 token，反序列化结果均为 `string.Empty`。

### Union 多态

`(byte tag, value)`\
`(250, ushort tag, value)`

第一个无符号字节是用于区分值类型或标志的标记， `0` 到 `249` 表示标记， `250` 表示下一个无符号短标记， `255` 表示 `null` 。

### Collection 集合

`(int length, [values...])`

集合头的数据计数为4字节有符号整数， `-1` 表示 `null` 。头字节存储数据长度。

### Tuple 元组

`(values...)`

元组是固定大小的非空值集合。 `KeyValuePair<TKey, TValue>` 和 `ValueTuple<T,...>` 被序列化为Tuple。

## 📄 License 许可证

This library is licensed under the MIT License.
