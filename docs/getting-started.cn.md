# 🚀 快速开始

[English](getting-started.md) · [返回 README](README.cn.md)

## 安装

### .NET

LuminPack 支持 `netstandard2.1`、`net8.0`、`net9.0`、`net10.0` 和 `net11.0`。

> [!IMPORTANT]
> LuminPack 2.0 还没有发布到 NuGet；NuGet 上的 [`LuminPack`](https://www.nuget.org/packages/LuminPack) 包是 API 不同的 1.x 版本。在 2.0 发布之前，请使用下面的[源码引用](#从源码引用)，或按[发布文档](releasing.cn.md#本地构建候选包)在本地构建候选包。

2.0 发布后，只需安装 `LuminPack` 包，NuGet 会自动安装匹配版本的 `LuminPack.Generator`。

```shell
dotnet add package LuminPack
```

```xml
<ItemGroup>
  <PackageReference Include="LuminPack" Version="2.0.0" />
</ItemGroup>
```

### Unity

见 [Unity、IL2CPP 与 NativeAOT](unity.cn.md)。

### 从源码引用

```xml
<ItemGroup>
  <ProjectReference Include="path/to/LuminPack/src/LuminPack.Core/LuminPack.Core.csproj" />
  <ProjectReference Include="path/to/LuminPack/src/LuminPack.Generator/LuminPack.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## 第一个类型

```csharp
using LuminPack;
using LuminPack.Attribute;

[LuminPackable]
public partial class Person
{
    public int Age { get; set; }
    public string Name { get; set; } = "";
}
```

生成器会在你的程序集中生成一个 `LuminPackSerializer` 类，其中包含 `Person` 的各种重载：

```csharp
var person = new Person { Age = 18, Name = "Light" };

// 二进制
byte[] bytes = LuminPackSerializer.Serialize(person);
Person copy = null!;
LuminPackSerializer.Deserialize(bytes, ref copy);     // 具体类型重载；copy 已存在时会被复用
Person other = null!;
LuminPackSerializer.Deserialize(bytes, ref other);

// JSON
string json = LuminPackSerializer.SerializeJson(person);
Person fromJson = null!;
LuminPackSerializer.DeserializeJson(json, ref fromJson);
```

在支持的现代编译器上，`Serialize(person)`、`Deserialize(bytes, ref copy)` 以及 `Deserialize<Person>(bytes)` 等闭合的显式泛型调用被拦截到类型的既有具体实现。可安全迁移的静态 `Save<T>` 包装方法也可在闭合调用处特化。显式配置使用 `Serialize<Person, TOptions>(person)`。不透明调用需要显式或 Custom 格式化器[注册](custom-formatters.cn.md#runtime-registration)，缺少注册时抛出 `LuminPackException`。旧编译器通过包内 IL 后处理器绑定相同静态入口，Unity 需要安装配套编辑器接入。详见[静态拦截与兼容边界](generation-modes.cn.md#静态拦截)。

具体模型重载，以及具有执行根方法的内置重载，其 XML 说明中提供 **Generated implementation** 链接，可在 IDE 中直接跳转到二进制或 JSON 的 `Root` 方法，不必逐层经过转发方法。闭合泛型模型、集合与复合值在格式器具有根方法时都支持此导航。原始非托管二进制读取可能直接调用运行时，因此没有独立的 `RootRead`；Custom 格式化器保留用户定义的入口。根据泛型实参选择格式器的回退 API 没有唯一具体 Root 可供链接。

生成的 Root 使用四空格缩进，并采用 `cursor`、`nameSnapshot1` 等按用途命名的局部变量。游标记录组使用普通的 `// Fields:` 和 `// Field:` 注释标明正在处理的字段。只在绑定关系已确定后重命名生成器自己的局部变量，用户字段名保持不变，包括以下划线开头的名称。

带 `ref` 参数的读取方法返回消耗的字节数（或字符数）。

## 入口一览

| 输入 / 输出 | 二进制 | JSON |
| --- | --- | --- |
| `byte[]` / `string` | `Serialize(value)` | `SerializeJson(value)` |
| `ReadOnlySpan<byte>`、`ReadOnlySequence<byte>` | `Deserialize(span, ref value)` | `DeserializeJson(span, ref value)`；也支持 `ReadOnlySpan<char>` 和 `string` |
| `LuminBufferWriter` | `Serialize(value, buffer)`、`Deserialize(buffer, ref value)` | `SerializeJson(value, buffer)`、`DeserializeJson(buffer, ref value)` |
| `IBufferWriter<byte>` | `Serialize(value, bufferWriter)` | `SerializeJson(value, bufferWriter)` |
| `Stream` | `SerializeAsync(stream, value)`、`DeserializeAsync<T>(stream)` | |

每个入口还有一个带选项配置的形式，例如 `Serialize<Person, LuminPackUtf8LengthOptions>(value)`，见[选项](options.cn.md)。

流由调用方持有，不会被关闭。异步读取会缓冲完整输入；连续消息的分帧由你的协议负责。

```csharp
await LuminPackSerializer.SerializeAsync(stream, person, cancellationToken);
Person result = await LuminPackSerializer.DeserializeAsync<Person>(stream, cancellationToken: cancellationToken);
```

<a id="reusing-a-buffer"></a>
## 复用缓冲区

接收 `LuminBufferWriter` 的重载从当前游标追加，保留之前写入的所有内容，包括引用跟踪状态。缓冲区由你持有，何时重置由你决定：

```csharp
using var output = new LuminPack.Utility.LuminBufferWriter(true);
LuminPackSerializer.Serialize(person, output);
int firstEnd = output.CurrentIndex;
LuminPackSerializer.Serialize(person, output);   // output.GetSpan() 中按顺序包含两条记录

output.ResetForReuse();                          // 清空内容和引用表，保留容量
LuminPackSerializer.Serialize(person, output);
```

| 方法 | 作用 |
| --- | --- |
| `ResetForReuse()` | 清空内容和读写两张引用表，保留原生容量。 |
| `Reset()` | 清空内容和两张引用表，并归还非空的池化数组。 |
| `ResetCore()` | 只清空内容，保留引用表。复制/压缩的 `AndReset` 方法行为相同。 |

- 写入抛出异常后，已写入的字节和引用状态仍然保留；开始下一次独立写入前调用 `ResetForReuse()`。
- 只想重新开始写入、同时保留读取状态时，依次调用 `output.ResetCore()` 和 `output.WriterState.ResetOperationState()`。
- 追加的 JSON 文档之间不会插入分隔符，请自己记录边界。
- 返回 `byte[]`、`string` 的重载以及流、`IBufferWriter<byte>` 重载自己管理缓冲区和状态。

## 压缩

`LuminCompressor`（位于 `LuminPack.Utility`）用 LuminPack LZ 帧格式压缩任意字节序列；`[LuminPackCompress]` 用于压缩单个非托管数组成员：

```csharp
byte[] packed = LuminCompressor.Compress(bytes);
byte[] unpacked = LuminCompressor.Decompress(packed);
```

编解码器采用带紧凑长度扩展的 `LUM2` 帧。缓冲区容量、帧编码和验证规则参见[压缩](compression.cn.md)。

## 下一步

- [定义类型](defining-types.cn.md)：成员、顺序、构造、回调、支持的类型。
- [多态](polymorphism.cn.md)与[版本容忍](versioning.cn.md)。
- [复用与零分配](reuse.cn.md)。
