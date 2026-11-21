# 🔧 编译期选项

[English](options.md) · [返回 README](README.cn.md)

LuminPack 没有运行时选项对象。设置通过 `[LuminPackOption]` 声明，并编译进生成的代码，所以没开启的功能在运行时没有任何开销。

## `[LuminPackOption]`

该特性可以标在程序集、类型、字段或属性上。可继承的选项默认使用外层设置，可显式覆盖。类型契约开关仅作用于声明处。

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
| `GeneratorType` | `Default` | 为单个 `[LuminPackable]` 类型选择 `Default`、`Version` 或 `Custom`。见[定义类型](defining-types.cn.md)。 |
| `IncludeDerivedTypes` | `false` | 在带标记的类上启用，让源码中的派生类自动进入生成范围。见 [Union 根类型](polymorphism.cn.md#union-根类型)。 |
| `StringEncoding` | `UTF16` | 二进制字符串编码，可按成员设置。对 JSON 而言是整份文档的编码，由根类型决定。 |
| `StringRecording` | `Length` | 固定宽度长度头、可选的 `VarInt` 长度头，或 `Token`（以终止符结束；`null` 和 `""` 读回来都是 `""`）。 |
| `FramedGroupings` | `false` | Next 程序集选项，为二进制 `IGrouping<TKey, TElement>` 提供明确的分组帧，也适用于 `ILookup<TKey, TElement>` 内的分组。 |
| `MaxJsonDepth` | `1024` | JSON 嵌套深度上限；小于等于 0 表示不限制。 |
| `ValidateJsonSyntax` | `false` | 严格 JSON 语法校验。处理外部来源的 JSON 时请开启。 |
| `StandardFormat` | `"G"` | 传给显式格式化与 `Custom` 核心方法的常量，不会改变内置数值、日期格式。 |
| `WideTag` | `false` | 在 Next 中，为注册 Union 路径启用宽数字标签；不继承外层选项。 |
| `SingleImplementation` | `false` | 当前类或接口保证所有程序集中只有一个实现。Next 按生成 case 数选择写入档位。 |
| `TrackReferences` | `false` | 在二进制和 JSON 中跟踪标注类的共享对象和循环。 |
| `ReuseRectangularArraySlots` | `true` | 在 .NET 8 及更高版本上，Next 复用二进制二维数组中已有的对象。在普通对象的元素类上设为 `false` 可改为替换数组。参见[对象矩阵复用](reuse.cn.md#二进制对象矩阵复用)。 |
| `StructLayout` | `Tagged` | 非托管结构体的存储方式，见下文。只能用于程序集和结构体。 |
| `UntrustedData` 及各项限额 | 开启，限额不设上限 | 面向不可信输入的带检查读取；面对恶意输入时请声明限额，见[安全](security.cn.md)。 |
| `RequireCompleteBinaryInput` | `false` | 仅限程序集。返回值的二进制文档读取会拒绝尾随字节。 |

二进制数据可以按成员混用不同编码，但写入方和读取方必须使用相同的声明。JSON 的编码、深度和语法校验总是作用于整份文档。

JSON 属性名使用针对各编码生成的[名称查找](json-name-lookup.cn.md)，哈希不会代替完整名称比较。

## 完整二进制输入

使用 `[assembly: LuminPackOption(RequireCompleteBinaryInput = true)]` 后，每次返回值的 `Deserialize<T>` 或 `DeserializeAsync<T>` 调用都必须消费一条完整的二进制记录。该选项适用于 span、序列、可复用缓冲区和流，也适用于显式指定操作配置的调用。解码成功后若仍有尾随字节，将抛出 `InvalidDataException`。`ref` 重载仍返回单条记录消费的字节数，推进式读取仍将游标停在该记录之后。

## 二进制分组帧

通过 `[assembly: LuminPackOption(FramedGroupings = true)]` 区分 null 分组与键编码首字节为 `FF` 的非 null 分组。这样的键包括默认长度配置下的 null 字符串，以及值为 `255` 的原始 byte 键。每个非 null 分组先写 `00`，然后写入键和元素序列。null 分组占一个字节 `FF`。

生成读写端的程序集应采用同一设置，包括分组嵌套在记录或 Lookup 内的情况。未启用该选项时写入的既有二进制数据采用默认的从键开始的布局。键的编码仍由字符串配置决定，例如 `Token` 对 null 与空字符串使用同一表示。JSON 可直接保留 null 键分组。

## Union 类型契约开关

将 Union 的类型级声明集中到一个特性：

```csharp
[LuminPackable, LuminPackOption(WideTag = true, SingleImplementation = true)]
public abstract partial class MessageBase { }

[LuminPackable]
public sealed partial class MessageBody : MessageBase { public int Id; }
```

这两个开关只作用于被标注的类或接口。不能用于程序集、结构体、字段或属性，不从包含类型或基类型继承，也不能通过 `ILuminPackOptions` 选择。只声明这些开关不会固定字符串选项，显式操作配置仍正常传递。声明标签达到 250 时，显式注册的 case 自动使用宽数字标签帧。

`[LuminPackWideTag]` 和 `[LuminPackSingleImplementation]` 保留兼容，推荐集中使用 Option。旧标记存在时，即使对应 Option 显式为 false，也仍然启用旧标记。`SingleImplementation` 是调用者保证，不是移除 Union 帧或虚方法派发的指令；只有确定其他程序集不会增加实现时才能使用。在 Next 中，生成 case 使用哈希帧，写入档位由 case 数决定。

## 引用跟踪

将 `[LuminPackOption(TrackReferences = true)]` 直接声明在采用 Default 或 Version 格式的类上。该契约仅作用于标注的类，也只能通过此特性配置。字符串编码仍由通常的选项声明或操作配置决定。在程序集、结构体、接口、字段或属性上声明该属性会报告 `LuminPack110`。详见[引用跟踪](versioning.cn.md#reference-tracking)。

## 显式配置

每个入口都接受一个无状态的配置类型作为泛型参数。该配置会传递到所有嵌套操作；自己声明了 `[LuminPackOption]` 的成员保持自己的设置。

```csharp
byte[] bytes = LuminPackSerializer.Serialize<Message, LuminPackUtf8LengthOptions>(message);

Message copy = null!;
LuminPackSerializer.Deserialize<Message, LuminPackUtf8LengthOptions>(bytes, ref copy);
```

内置配置：`LuminPackDefaultOptions`（UTF-16、固定长度记录）、`LuminPackUtf8LengthOptions`、`LuminPackUtf8TokenOptions`、`LuminPackUtf16LengthOptions`、`LuminPackUtf16TokenOptions`、`LuminPackUtf8VarIntOptions`、`LuminPackUtf16VarIntOptions`。也可以自己定义一个实现 `ILuminPackOptions` 的 `struct`。

组合配置的四个类型参数依次选择二进制字符串编解码器、数字格式、JSON 深度和语法策略。下例使用 UTF-8 可变长度头和严格 JSON。

```csharp
using TextOptions = LuminPack.Option.LuminPackOptions<
    LuminPack.Option.LuminPackUtf8VarIntStrings,
    LuminPack.Option.LuminPackGeneralFormat,
    LuminPack.Option.LuminPackDefaultDepth,
    LuminPack.Option.LuminPackStrictJson>;
```

直接实现 `ILuminPackOptions` 的结构体要把 `ILuminPackStringCodec` 的各成员转发给所选编解码器。**Forward string operations to LuminPack…Strings** 代码修复会补齐缺少的成员并保留已有实现。编码与记录方式为常量时会匹配相应编解码器，计算属性可从六种内置实现中选择。元数据属性须与实际使用的编码一致。自定义编解码器返回的预留上界须涵盖完整记录。预留写入返回下一个游标，异常时须保留 writer 已发布的前缀。`Skip` 方法跳过一条完整字符串记录，遵循与读取相同的输入边界和字符串长度限制。

直接实现配置时，应将跳过操作转发给负责读取的编解码器。例如 UTF-8 VarInt 配置可这样转发。

```csharp
public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue)
    => default(LuminPackUtf8VarIntStrings).Skip(ref reader, maxStringLength);
```

自定义 JSON profile 将标量读写与编码单元操作转发给字符串编解码器，将 token 读取、语法状态跟踪和 UTF-8 字符串校验转发给 LuminPackStrictJson 或 LuminPackTrustedJson。实现 CheckJsonDepth 时调用 LuminPackJsonPolicy.CheckDepth(depth, MaxJsonDepth)。代码修复可以根据已有选项补齐这些实现。元数据应与所选策略一致，供注册的 formatter 和生成代码使用。

仅提供 Value 的深度组件沿用接口提供的标准上限检查。如需指定检查行为，可同时实现 `Value` 和 `CheckJsonDepth(int depth)`。将深度检查转发给 `LuminPackJsonPolicy.CheckDepth(depth, Value)` 即可采用标准上限语义。非正上限允许任意嵌套深度。

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
List<string> copy = null!;
LuminPackSerializer.Deserialize<List<string>, LuminPackUtf16VarIntOptions>(bytes, ref copy);
```

默认仍为 `Length`：UTF-16 使用 4 字节头，UTF-8 使用 8 字节头。`VarInt` 将每个原有有符号长度头加一，再以规范的无符号 base-128 编码存储，占 1–5 字节。null 映射为零，空字符串映射为一。UTF-16 保留原始 UTF-16 或紧凑 Latin-1 负载及原有最低位标记；UTF-8 保留字节数和字符数两个字段，分别编码，null 和空字符串也保留两个字段。例如，UTF-16 的 `"a"` 为 `04 61`，UTF-8 的 `"a"` 为 `02 02 61`。

包含 16 个单字符 ASCII 字符串的 `List<string>`，使用 UTF-16 `Length` 时共 84 字节，使用 UTF-16 `VarInt` 时共 36 字节，均包含不变的 4 字节集合元素数。可变长度头增加了分支和长度计算，可能降低吞吐量。读写双方必须选择相同表示，不能混用或自动识别。JSON 和集合元素数的头部不受影响。`StringRecordLength<TOptions>()` 对 `VarInt` 返回保守的头部上界（5 或 10 字节），不是精确的记录大小。

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
| `Tagged`（默认） | 块或单值只有一个建模布局时，直接复制原生字节，省略标签。多布局集合块的写入端为每个封闭类型校验一次本机步幅和字段偏移，写入匹配的标签后直接复制整块。同布局读取端直接复制，异布局读取端做转换。写入端匹配不到模型时转换为第一个模型。多布局单值按 Packed 字段顺序编码，省略标签。 |
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
- `Packed` 会递归影响外层结构体，包括框架的键值对和值元组。显式声明为 `Native` 的结构体保留自身原生布局，其字段的 `Packed` 设置不会穿透这一层。
- 在 Next 中，无法在类型上声明 `LuminPackOption` 且没有独立 LuminPack 契约的值使用消费项目的程序集选项，包括集合里的框架标量、枚举和值元组。默认使用 `Tagged`。
- 在受支持的 .NET 8+ 目标上，符合条件的 `Nullable<T>` 集合采用底层类型的布局。`Native` 存储包括 padding 在内的完整可空步幅。`Tagged` 的连续负载在非空块存在多套布局模型时写一个标签。逐元素集合为经证明只有单一布局的标量使用无标签原始记录，其余 `Tagged` 元素使用各自的 nullable formatter。`Packed` 写可空标记，并为非空元素写入紧凑负载。
- 较旧目标和不支持的可空布局使用逐元素 nullable formatter。读写双方须使用同一种集合编码。
- 字段展开包含 private 字段、被忽略的字段和自动属性的 backing field，并递归展开嵌套结构体；`LuminPackOrder` 不影响它。`long` 总是占 8 字节。
- 布局、元素数量和运行时满足条件时，布局转换使用 SIMD。其他布局或平台通过标量路径转移相同的字段字节。.NET Standard 使用方安装 Burst 布局钩子后，符合条件的块可使用该钩子。
- `Packed` 遇到原生整数、指针、fixed buffer、InlineArray、运行时大小的向量、`Custom` 存储以及无法分析的布局时会报诊断。
- 这几种模式都不做字节序转换；LuminPack 要求小端。JSON 成员不受影响。
- 带 padding 的原始结构体可能带出残留的 padding 字节。对外协议请使用 `Packed` 或按字段建模。
