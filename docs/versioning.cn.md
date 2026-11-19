# 📝 版本容忍与引用跟踪

[English](versioning.md) · [返回 README](README.cn.md)

## 选择模式

默认的 `Object` 模式按位置识别成员，适合读写双方使用相同成员顺序和类型的稳定模型。需要增删、重排或重命名成员时使用 `VersionTolerant`。两种格式均可在具体类上声明 `[LuminPackOption(TrackReferences = true)]`，以支持共享对象与循环。

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

这两个模型可以双向读取对方写出的数据。读取方跳过未知成员，将缺失的成员重置为 `default`，读入已有对象时也一样。`LuminPackOrder` 可选，只决定写入顺序。

- 名称区分大小写。当前名与旧名（包括继承成员的名称）重复或发生名称键碰撞时，生成器报告 `LuminPack062`。模型的序列化成员数超过 249 时，生成器报告 `LuminPack018`。删除成员后，请保留其名称的原有语义。
- `[LuminPackOldName]` 让读取方能匹配原来的名称。二进制写入使用最早的名称键。多次重命名时，按时间顺序列出所有旧名。

  ```csharp
  [LuminPackOldName("Name")]
  [LuminPackOldName("DisplayName")]
  public string? Label;
  ```

- JSON 读取接受旧名，写入使用当前名称。
- 需要独立演进的嵌套模型也应使用 `VersionTolerant`，包括集合元素和泛型成员。嵌套的 `Object` 模型及原始结构体各自使用按位置定义的布局。
- 成员类型、类型所在命名空间及二进制字符串选项须保持兼容。匹配到已知名称键时，读取方要求类型契约哈希和宽度都符合预期；任一不同都会报错。需要修改成员类型时，请使用新名称并在应用代码中转换。

已有对象及其子对象遵循[复用规则](reuse.cn.md)。

<a id="binary-framing"></a>
### 二进制格式

未跟踪引用的空对象写成单字节 `255`。非空记录以标记 `251` 开头。后续的 `form` 字节用高四位表示修订号、位 0–2 表示字符串配置，保留位 3 必须为零。读写双方须使用相同配置。配置标签 0–5 依次表示 UTF-8 Length、UTF-8 Token、UTF-16 Length、UTF-16 Token、UTF-8 VarInt、UTF-16 VarInt。

| 修订号 | 记录 |
| --- | --- |
| 4，完整 | `251, form, count, u64 schema, u64 descriptors[count], payloads` |
| 5，简写 | `251, form, 规范无符号 base-128 序号, payloads` |

Schema 是对成员数字节及按线格式顺序排列的描述符字节计算的 64 位 FNV-1a 哈希。每个小端描述符的位 0–31 存放 UTF-8 成员名称的 FNV-1a-32 键，位 32–55 存放类型契约 FNV-1a-32 哈希的低 24 位，位 56–63 存放宽度码。宽度码 0–250 表示负载的确切字节数，例如 `DateTime`、`Guid`、`decimal`、枚举和经证明为定宽的结构体。254 表示使用记录字符串配置及自身分帧的字符串，255 表示负载前带四字节无符号长度。使用不同成员配置的字符串使用码 255，便于其他模型跳过。定宽 `Nullable<T>` 写入存在性标记和负载槽位，空值与非空值的槽位宽度相同，空值的负载槽位填零。

读取方检查完整记录的描述符数量、宽度码和名称键唯一性。在按位置读取或复用缓存的成员映射前，它会逐字节比较整个描述符表。Schema 哈希本身无法证明两张表相同。单个名称和类型字段使用固定位数的哈希，因此独立定义的契约仍可能碰撞。

### Schema 登记与作用域

每次读写都维护一张有序表，保存当前位置可见的完整 schema。完整记录追加一项，简写记录使用从零开始的序号。某个 schema 不在当前登记表时，第一条记录携带完整表，后续记录可以使用序号。

读写进入带长度前缀的成员（宽度码 255）时，外层登记项仍然可见。成员结束时，移除在其内部新增的条目。这样，读取方即使直接跳过整个未知成员，后续序号的含义也与写入方一致。如果外层没有登记，同级的两个集合成员可以各自写出同一 schema 的完整记录。读取简写记录时，需使用先前读取过对应完整记录的状态。

### 读取已有数据

版本容忍读取器还接受修订版 1，其字段长度统一存于负载之前。修订版 2、3 的开发期记录及旧的引用跟踪槽位格式，需要用支持相应字节的读取器解码。转换已有记录时，先用相应读取器解码，再将所得对象按当前格式序列化。

<a id="reference-tracking"></a>
## 引用跟踪

```csharp
[LuminPackable, LuminPackOption(TrackReferences = true)]
public partial class Node
{
    public Node? Parent { get; set; }
    public Node[]? Children { get; set; }
}
```

声明只作用于被标注的类。对于多态值，引用跟踪由具体 case 的声明决定。类同时需要版本演进时，请在 `[LuminPackable]` 上指定 `GeneratorType.VersionTolerant`。两种格式中的 `LuminPackOrder` 都只决定写入顺序。

二进制中，对象初次出现时以显式 id 跟踪，随后写 Object 或 VersionTolerant 记录体。后续回引用只写标记 `250` 和对应 id。追加到同一 `LuminBufferWriter` 的记录共享引用状态，读取时须用同一读取状态按顺序读取（见[复用缓冲区](getting-started.cn.md#reusing-a-buffer)）。回引用只能解析读取方已读到的对象。演进版本容忍模型时，被引用对象首次出现的位置应放在旧读取方会读取的成员内。JSON 初次出现时用 `$id`，之后用 `$ref`。这两个名称均保留。

已弃用的 `GeneratorType.CircleReference` 等同于启用引用跟踪的 `GeneratorType.VersionTolerant`。可应用 IDE 代码修复，或改为 `[LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(TrackReferences = true)]`，已有的 `LuminPackOrder` 值可以保留。
