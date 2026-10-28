# 📝 版本容忍与引用跟踪

[English](versioning.md) · [返回 README](../README.cn.md)

## 选择模式

默认的 `Object` 模式按位置识别成员，速度最快，但删除、重排成员或修改成员类型都会破坏已有数据。原始的非托管结构体同样不能通过增删字段保持兼容。

| 需求 | 模式 |
| --- | --- |
| 模型稳定，追求速度 | `GeneratorType.Object` |
| 成员会随版本增删、重排、重命名 | `GeneratorType.VersionTolerant` |
| 对象图中有共享对象和循环 | `GeneratorType.CircleReference` |

## 版本容忍对象

```csharp
[LuminPackable(GeneratorType.VersionTolerant)]
public partial class PlayerV1
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public long Removed { get; set; }
}

[LuminPackable(GeneratorType.VersionTolerant)]
public partial class PlayerV2
{
    public List<int>? Scores { get; set; }

    [LuminPackOldName("Name")]
    public string? DisplayName { get; set; }

    public int Id { get; set; }
}
```

这两个模型可以双向读取对方写出的数据。

- 成员按名称（精确、区分大小写）识别。`LuminPackOrder` 可选，只决定写入顺序。
- 未知成员按记录的长度跳过；缺失的成员设为 `default`，读入已有对象时也一样。
- `[LuminPackOldName]` 让重命名后的成员仍能读取旧数据。二进制写入保留最早的名称，所以旧版本读取方也能读懂新数据。多次重命名时，按时间顺序保留所有旧名：

  ```csharp
  [LuminPackOldName("Name")]
  [LuminPackOldName("DisplayName")]
  public string? Label;
  ```

- 名称和别名在整个成员集合（包括继承成员）中必须唯一。不要把已删除成员的名字挪给含义不同的新成员。
- JSON 读取同样接受旧名，写入使用当前名称。
- 需要独立演进的嵌套模型也必须是 `VersionTolerant`，包括数组、集合和泛型成员里的模型。嵌套的 `Object` 模型或原始结构体保持自己的按位置格式。
- 成员类型、类型所在命名空间以及二进制字符串选项要保持兼容。名称相同但类型元数据不兼容会被拒绝，不做自动类型转换。

读入已有对象时，对象及其子对象的复用方式与普通二进制读取相同（见[复用](reuse.cn.md)）。

<a id="binary-framing"></a>
### 二进制格式

`null` 是单字节 `255`。非空记录以 4 个字节开头：标记 `251`、格式版本 `1`、二进制字符串配置、成员数（0 到 249）。配置字节的 bit 1 表示字符串编码，bit 0 表示记录方式，必须与读取方的配置一致。

随后是元数据块，每个成员一项，包含 UTF-8 名称和 UTF-8 类型契约，各自带无符号 base-128 长度前缀。接着是所有负载长度（LuminPack VarInt），最后是所有负载，三者顺序一致。元数据按字节完整比较，哈希冲突不会让两个成员混淆。生成代码为每个模型缓存元数据并直接与输入比较，不会把名称解码成字符串，也不使用反射。

<a id="reference-tracking"></a>
## 引用跟踪

```csharp
[LuminPackable(GeneratorType.CircleReference)]
public partial class Node
{
    [LuminPackOrder(0)] public Node? Parent { get; set; }
    [LuminPackOrder(1)] public Node[]? Children { get; set; }
}
```

- 每个被序列化的成员都需要 `[LuminPackOrder]`。编号构成带长度的槽位表；增删成员时保留已有编号。
- 只有 `CircleReference` 类型的对象会被跟踪。需要跟踪其他对象时，把它包装进这种类型。
- 写入同一个 `LuminBufferWriter` 的多条记录共享一个引用表；读取时要用同一个读取器和读取状态按顺序读（见[快速开始](getting-started.cn.md#reusing-a-buffer)）。
- JSON 中对象第一次出现时用 `$id` 标记，之后的引用写为 `$ref`。这两个名称是保留名。
