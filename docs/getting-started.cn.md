# 🚀 快速开始

[English](getting-started.md) · [返回 README](../README.cn.md)

## 安装

### .NET

LuminPack 支持 `netstandard2.1`、`net8.0`、`net9.0`、`net10.0` 和 `net11.0`。只需安装 [`LuminPack`](https://www.nuget.org/packages/LuminPack) 包，NuGet 会自动安装匹配版本的 `LuminPack.Generator`。

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
Person other = LuminPackSerializer.Deserialize<Person>(bytes);

// JSON
string json = LuminPackSerializer.SerializeJson(person);
Person fromJson = LuminPackSerializer.DeserializeJson<Person>(json);
```

具体类型重载（`Serialize(person)`、`Deserialize(bytes, ref copy)`）直接调用为该类型生成的代码。泛型重载（`Deserialize<Person>(bytes)`）经过一个按类型在首次使用时创建的入口。

带 `ref` 参数的读取方法返回消耗的字节数（或字符数）。

## 入口一览

| 输入 / 输出 | 二进制 | JSON |
| --- | --- | --- |
| `byte[]` / `string` | `Serialize(value)` | `SerializeJson(value)` |
| `ReadOnlySpan<byte>`、`ReadOnlySequence<byte>` | `Deserialize(span, ref value)` | `DeserializeJson(span, ref value)`；也支持 `ReadOnlySpan<char>` 和 `string` |
| `LuminBufferWriter` | `Serialize(value, buffer)`、`Deserialize(buffer, ref value)` | `SerializeJson(value, buffer)`、`DeserializeJson(buffer, ref value)` |
| `IBufferWriter<byte>` | `Serialize(value, bufferWriter)` | `SerializeJson(value, bufferWriter)` |
| `Stream` | `SerializeAsync(stream, value)`、`DeserializeAsync<T>(stream)` | |

每个入口还有一个带选项配置的形式，例如 `Serialize<LuminPackUtf8LengthOptions>(value)`，见[选项](options.cn.md)。

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

## 压缩与加密工具

`LuminCompressor`（位于 `LuminPack.Utility`）用 LuminPack LZ 帧格式压缩任意字节序列；`[LuminPackCompress]` 用于压缩单个非托管数组成员：

```csharp
byte[] packed = LuminCompressor.Compress(bytes);
byte[] unpacked = LuminCompressor.Decompress(packed);
```

`LuminChaCha20Poly1305`（位于 `LuminPack.Cryptography`）提供 RFC 8439 认证加密（32 字节密钥、12 字节 nonce、16 字节标签），支持同步和异步。解密时先校验标签，再写出明文。密钥管理和 nonce 唯一性由你负责。

## 下一步

- [定义类型](defining-types.cn.md)：成员、顺序、构造、回调、支持的类型。
- [多态](polymorphism.cn.md)与[版本容忍](versioning.cn.md)。
- [复用与零分配](reuse.cn.md)。
