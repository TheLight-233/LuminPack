# 🔧 编译期选项

[English](options.md) · [返回 README](../README.cn.md)

LuminPack 没有运行时选项对象。设置通过 `[LuminPackOption]` 声明，并编译进生成的代码，所以没开启的功能在运行时没有任何开销。

## `[LuminPackOption]`

该特性可以标在程序集、类型、字段或属性上。只有你显式设置的属性会覆盖外层作用域，其余继承外层。

```csharp
using LuminPack.Attribute;
using LuminPack.Option;

[assembly: LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF16)]

[LuminPackable]
public partial class Message
{
    public string Title { get; set; } = "";

    [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8,
                     StringRecording = LuminPackStringRecording.Token)]
    public string Label { get; set; } = "";
}
```

| 属性 | 默认值 | 含义 |
| --- | --- | --- |
| `StringEncoding` | `UTF16` | 二进制字符串编码，可按成员设置。对 JSON 而言是整份文档的编码，由根类型决定。 |
| `StringRecording` | `Length` | 带长度前缀的字符串，或 `Token`（以终止符结束；`null` 和 `""` 读回来都是 `""`）。 |
| `MaxJsonDepth` | `1024` | JSON 嵌套深度上限；小于等于 0 表示不限制。 |
| `ValidateJsonSyntax` | `false` | 严格 JSON 语法校验。处理外部来源的 JSON 时请开启。 |
| `StandardFormat` | `"G"` | 传给显式格式化与 `Custom` 核心方法的常量，不会改变内置数值、日期格式。 |
| `StructLayout` | `Tagged` | 非托管结构体的存储方式，见下文。只能用于程序集和结构体。 |
| `UntrustedData` 及各项限额 | 关闭 | 面向不可信输入的受限协议，见[安全](security.cn.md)。 |

二进制数据可以按成员混用不同编码，但写入方和读取方必须使用相同的声明。JSON 的编码、深度和语法校验总是作用于整份文档。

## 显式配置

每个入口都接受一个无状态的配置类型作为泛型参数。该配置会传递到所有嵌套操作；自己声明了 `[LuminPackOption]` 的成员保持自己的设置。

```csharp
byte[] bytes = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(message);

Message copy = null!;
LuminPackSerializer.Deserialize<LuminPackUtf8LengthOptions>(bytes, ref copy);
```

内置配置：`LuminPackDefaultOptions`（UTF-16、长度记录）、`LuminPackUtf8LengthOptions`、`LuminPackUtf8TokenOptions`、`LuminPackUtf16LengthOptions`、`LuminPackUtf16TokenOptions`。也可以自己定义一个实现 `ILuminPackOptions` 的 `struct`。

## 字符串转码

`StringSerializer` 使用同一套配置转码文本，不添加任何头部：

```csharp
OperationStatus status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(
    text.AsSpan(), utf8Buffer, out int charsRead, out int bytesWritten);
OperationStatus decoded = StringSerializer.Deserialize<LuminPackUtf8LengthOptions>(
    utf8Buffer.AsSpan(0, bytesWritten), charBuffer, out int bytesRead, out int charsWritten);
```

请检查返回状态，处理目标缓冲区不足或输入块尚未结束的情况。`replaceInvalidSequences` 和 `isFinalBlock` 控制无效序列替换和分块语义。

<a id="struct-layout"></a>
## 32/64 位结构体布局

不含引用的结构体通常直接按内存复制。但它的原生布局在 32 位和 64 位进程之间可能不同（padding、对齐）。`StructLayout` 决定这类结构体的存储方式：

| 取值 | 存储 |
| --- | --- |
| `Tagged`（默认） | 所有建模平台布局一致时，直接复制原生字节，不写标签。布局不同时，连续集合写入一个布局标签和元素原始存储；读取方布局相同就直接复制，布局不同就转换。单个值按字段写入。 |
| `Packed` | 按声明顺序写字段，不含 padding，所有平台一致。经证明没有 padding 的结构体保留原始复制。 |
| `Native` | 完整的原生存储，包括 padding。双方布局必须完全一致。 |

```csharp
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public partial struct Packet
{
    public byte Kind;
    public long Sequence;
}

// 或者对本程序集声明的所有普通非托管结构体生效：
[assembly: LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
```

- 结构体自己的设置覆盖程序集设置。引用程序集保持各自的声明。
- 字段展开包含 private 字段、被忽略的字段和自动属性的 backing field，并递归展开嵌套结构体；`LuminPackOrder` 不影响它。`long` 总是占 8 字节。
- 在 .NET 8+ 上，tagged 或 packed 结构体的集合使用 SIMD 加速转换；其他平台（包括 IL2CPP）按字段逐个处理，产生相同的字节。
- `Packed` 遇到原生整数、指针、fixed buffer、InlineArray、运行时大小的向量、`Custom` 存储以及无法分析的布局时会报诊断。
- 这几种模式都不做字节序转换；LuminPack 要求小端。JSON 成员不受影响。
- 带 padding 的原始结构体可能带出残留的 padding 字节。对外协议请使用 `Packed` 或按字段建模。
