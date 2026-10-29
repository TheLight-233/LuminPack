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
| `StringRecording` | `Length` | 固定宽度长度头、可选的 `VarInt` 长度头，或 `Token`（以终止符结束；`null` 和 `""` 读回来都是 `""`）。 |
| `MaxJsonDepth` | `1024` | JSON 嵌套深度上限；小于等于 0 表示不限制。 |
| `ValidateJsonSyntax` | `false` | 严格 JSON 语法校验。处理外部来源的 JSON 时请开启。 |
| `StandardFormat` | `"G"` | 传给显式格式化与 `Custom` 核心方法的常量，不会改变内置数值、日期格式。 |
| `WideTag` | `false` | 当前类或接口接受宽 Union 标签；不继承外层选项。 |
| `SingleImplementation` | `false` | 当前类或接口保证所有程序集中只有一个实现；保留既有 Union 帧和虚方法派发。 |
| `StructLayout` | `Tagged` | 非托管结构体的存储方式，见下文。只能用于程序集和结构体。 |
| `UntrustedData` 及各项限额 | 关闭 | 面向不可信输入的受限协议，见[安全](security.cn.md)。 |

二进制数据可以按成员混用不同编码，但写入方和读取方必须使用相同的声明。JSON 的编码、深度和语法校验总是作用于整份文档。

## Union 类型契约开关

将 Union 的类型级声明集中到一个特性：

```csharp
[LuminPackable, LuminPackOption(WideTag = true, SingleImplementation = true)]
public abstract partial class MessageBase { }

[LuminPackable]
public sealed partial class MessageBody : MessageBase { public int Id; }
```

这两个开关只作用于被标注的类或接口。不能用于程序集、结构体、字段或属性，不从包含类型或基类型继承，也不能通过 `ILuminPackOptions` 选择。只声明这些开关不会固定字符串选项，显式操作配置仍正常传递。标签达到 250 时仍会自动使用宽标签帧。

`[LuminPackWideTag]` 和 `[LuminPackSingleImplementation]` 保留兼容，推荐集中使用 Option。旧标记存在时，即使对应 Option 显式为 false，也仍然启用旧标记。`SingleImplementation` 是调用者保证，不是移除 Union 帧或虚方法派发的指令；只有确定其他程序集不会增加实现时才能使用。当前生成器保留这些机制。

## 显式配置

每个入口都接受一个无状态的配置类型作为泛型参数。该配置会传递到所有嵌套操作；自己声明了 `[LuminPackOption]` 的成员保持自己的设置。

```csharp
byte[] bytes = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(message);

Message copy = null!;
LuminPackSerializer.Deserialize<LuminPackUtf8LengthOptions>(bytes, ref copy);
```

内置配置：`LuminPackDefaultOptions`（UTF-16、固定长度记录）、`LuminPackUtf8LengthOptions`、`LuminPackUtf8TokenOptions`、`LuminPackUtf16LengthOptions`、`LuminPackUtf16TokenOptions`、`LuminPackUtf8VarIntOptions`、`LuminPackUtf16VarIntOptions`。也可以自己定义一个实现 `ILuminPackOptions` 的 `struct`。

## 紧凑长度头

小字符串集合更重视字节数而不是最高编码速度时，可以选择 `VarInt`：

```csharp
[LuminPackable]
public partial class Labels
{
    [LuminPackOption(StringRecording = LuminPackStringRecording.VarInt)]
    public List<string> Names { get; set; } = new();
}

// 或为整次操作选择表示方式：
byte[] bytes = LuminPackSerializer.Serialize<List<string>, LuminPackUtf16VarIntOptions>(names);
List<string> copy = LuminPackSerializer.Deserialize<List<string>, LuminPackUtf16VarIntOptions>(bytes);
```

默认仍为 `Length`：UTF-16 使用 4 字节头，UTF-8 使用 8 字节头。`VarInt` 将每个原有有符号长度头加一，再以规范的无符号 base-128 编码存储，占 1–5 字节。null 映射为零，空字符串映射为一。UTF-16 保留原始 UTF-16 或紧凑 Latin-1 负载及原有最低位标记；UTF-8 保留字节数和字符数两个字段，分别编码，null 和空字符串也保留两个字段。例如，UTF-16 的 `"a"` 为 `04 61`，UTF-8 的 `"a"` 为 `02 02 61`。

包含 16 个单字符 ASCII 字符串的 `List<string>`，使用 UTF-16 `Length` 时共 84 字节，使用 UTF-16 `VarInt` 时共 36 字节，均包含不变的 4 字节集合元素数。可变长度头增加了分支和长度计算，可能降低吞吐量。读写双方必须选择相同表示，不能混用或自动识别。JSON 和集合元素数的头部不受影响。`StringRecordLength<TOptions>()` 对 `VarInt` 返回保守的头部上界（5 或 10 字节）；精确记录大小请使用 `Sizeof` 或 `GetStringLength<TOptions>`。

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
