# ♻️ 复用与零分配

[English](reuse.md) · [返回 README](README.cn.md)

## 读入已有对象

把已有目标用 `ref` 传入，二进制读取会填充你已有的对象图：

```csharp
var target = new Message();
LuminPackSerializer.Deserialize(payload, ref target);
LuminPackSerializer.Deserialize(nextPayload, ref target);   // 仍然是同一批对象
```

保留规则：

| 形态 | 行为 |
| --- | --- |
| 对象 | 根对象保持原身份。非空嵌套对象、自动属性存储和构造函数绑定的成员原地填充。缺失的子对象用构造函数或 `[LuminPackPoolRent]` 方法创建。 |
| 数组 | 精确类型和长度都相同时保留。 |
| 二进制 `ReadOnlyCollection<T>` | 在经探测验证的现代 CoreCLR 布局上，Next 可以填充类型精确匹配、直接包装同长度非空 `T[]` 的受支持原始标量集合。完整集合载荷校验后，原包装集合和后备数组都能看到新值。传入 null 目标可以获得独立的包装集合和后备数组。 |
| 元素为字符串或对象的二进制 `ReadOnlyCollection<T>` | 使用 Next 时，如果精确类型的包装集合直接包装长度与输入相同的精确 `T[]`，会像列表一样原位读取并复用已有元素；持有包装集合或数组的其他引用都能看到新元素。其他包装集合或长度不同时会被替换。 |
| 二进制 `Collection<T>`、`ReadOnlyCollectionBuilder<T>` | 使用 Next 时，包装精确 `List<T>` 的精确 `Collection<T>`，以及元素为字符串或对象的 `ReadOnlyCollectionBuilder<T>`，会保留存储并像列表一样复用已有元素。派生的 `Collection<T>` 仍通过自己的 `ClearItems` 清空。 |
| 二进制 `string[,]` | 默认 UTF-16 长度配置（`LuminPackUtf16LengthOptions`）在两个维度长度分别相等、两个下界均为零时复用矩阵，其他配置和形状会分配新矩阵。持有旧引用的调用方能按行看到更新。若记录被截断或字符串超过限额，只有此前成功解码的位置已更新。 |
| `List<T>` | 保留容量和已有元素；需要时扩容，缩短时清理被移除的引用。 |
| 二进制 `LinkedList<T>` | 使用 Next 时，按顺序把每个元素读入已有节点，再按新的元素数追加或移除节点。 |
| `Dictionary<K,V>` | 保留比较器，按传入的键复用值；读取成功后，被移除的键消失。 |
| 二进制 `ReadOnlyDictionary<string, int>` 和 `ReadOnlyDictionary<string, string>` | 在受支持的 64 位 CoreCLR 布局上，Next 可以更新直接包装精确类型且使用默认比较器的字典。此路径使用内置 UTF-16 长度配置，输入包含 1 至 4096 个键，且键与后备字典的枚举顺序一致。字典读取成功后，持有包装器或后备字典的其他引用都能看到更新。传入 null 目标即可读取独立字典。 |
| 二进制 `ReadOnlyDictionary<K,V>` | 其他情况下，使用 Next 时，精确类型包装器背后的精确 `Dictionary<K,V>` 会按复用字典的规则读取，包装器保持不变；持有后备字典的其他引用能看到更新。其他后备类型会换成新的包装器。 |
| `SortedList<K,V>` | 同一位置的键可无回调比较且匹配时，复用符合条件的值。字符串键按序数比较。 |
| 二进制 `SortedList<string, int>` 和 `SortedList<string, long>` | 在受支持的运行时布局和内置字符串配置下，如果使用默认比较器的列表类型精确、条目数与输入相同且在 1 至 4096 之间，Next 可以保留内容匹配的字符串键引用。复用仍会先清空列表，再逐个发布读取完成的键值对。 |
| 序列接口（`IEnumerable<T>`、`ICollection<T>`、`IList<T>`、`IReadOnlyList<T>`、`IReadOnlyCollection<T>`） | 元素为字符串、标量或普通对象时，复用背后的精确 `List<T>` 或 `T[]`。 |
| `IDictionary<K,V>`、`IReadOnlyDictionary<K,V>` | 复用背后的精确 `Dictionary<K,V>`。 |
| 二进制 `HashSet<T>`、`ISet<T>`、`IReadOnlySet<T>` | 使用 Next 时，精确类型且使用默认比较器的 `HashSet<T>` 会被清空后原位重新填充并保留其存储；只有传入元素数超过容量时才扩容。使用自定义比较器的集合和派生集合会被替换。 |
| `ConcurrentQueue<T>` | 使用 Next 时，队列先通过框架出队操作清空再重新填充，从而复用最后一个段，而不是重新分配整条增长链。 |
| 元素为字符串或对象的二进制 `Queue<T>` 和 `Stack<T>` | 使用 Next 时，已有元素作为复用种子：未变化的字符串和已有对象原位读取，并复用后备数组。读取失败时集合为空。 |
| `Memory<T>`、`ArraySegment<T>` | 长度相同时复用数组存储，保留 offset 和 count。 |
| 二进制 `ReadOnlyMemory<string>` 和 `ReadOnlySequence<string>` | 在现代 .NET 上使用内置 UTF-16 长度配置时，如果原数组切片长度相同且首个字符串匹配，Next 可以复用内容相同的字符串引用。每个结果都有独立的后备数组。 |
| 标量或字符串的 Immutable 集合 | 传入内容完全相同时保留。 |
| 受支持原始标量的二进制 `FrozenSet<T>` | 在受支持的 .NET 10 和 11 运行时上，如果 Next 在当前线程验证过一个复用读取结果，且这次输入的元素字节和顺序与其完全相同，则可保留该集合。 |
| 字符串 | 内容不变的字符串保留原引用，所有字符串配置都适用。 |
| Union | 只复用类型完全相同的 case；标签不同时构造新的 case。 |
| 版本容忍对象 | 复用，缺失成员重置为 `default`。 |
| 引用跟踪对象 | 复用，同时保留传入数据中的共享引用和循环。 |

在受支持的现代目标上，二进制读取精确的字典类型时，若使用默认比较器、定长键的框架相等比较没有用户回调，且值格式器支持安全原位读取，则可直接复用已有槽位。匹配键保留目标顺序；失败时可能看到已读前缀。自定义键行为、自定义比较器或无法安全原位读取的值沿用暂存重建。目标字典为空、键无用户回调且键值均为原始数据时，可使用经过范围检查的批量重建。

派生类、协变以及未知的集合实现会被替换，不会去检查其内容。引用类型元素的 `SortedSet<T>` 总是重建，因为修改一个被用户代码重新插入的元素可能破坏树的排序。

对象、集合存储和字符串都已经匹配时，预热后的重复读取可以达到 **0 字节托管分配**。新对象、更大的存储、长度不同的数组、变化的字符串、不可变框架值以及用户回调仍会分配。

### 需要注意

- 复用不是事务性的。读取失败或回调抛出异常时，已经读完的成员保持更新后的值。
- 普通模式的 `null` 对象记录不会覆盖传入的根对象。专用模式和集合遵循各自的 null 规则。
- 如果目标中的两个成员共享同一个实例，两次读取都会填充这个实例。需要还原传入数据的对象身份时使用 `CircleReference`。

## 二进制对象矩阵复用

在 .NET 8 及更高版本上使用 Next 生成器时，普通对象的二进制 `T[,]` 会复用目标矩阵中已有的对象。若希望每次读取都替换矩阵，在元素类上设置 `ReuseRectangularArraySlots = false`。

```csharp
[LuminPackable, LuminPackOption(ReuseRectangularArraySlots = false)]
public sealed partial class Cell
{
    public List<int> Samples = new();
}
```

用 `ref` 传入已有矩阵，或用 `ref` 传入持有该矩阵的对象。读取器先校验形状和元素数量，再按行更新各位置。目标的数组类型和两个维度长度必须完全匹配，两个下界必须为零。后续位置读取失败时，持有目标引用的调用方仍可看到已读完的前缀。与数组和列表相同，共享同一可变对象的多个位置会看到最后读入该对象的记录，因此应让每个位置拥有独立的可变对象，包括嵌套集合。多个位置可以共享字符串等不可变值。

此选项适用于没有继承的序列化存储、选定构造函数、回调或池租用的普通对象元素类型。其他数组秩、全新目标以及形状或类型不匹配的目标沿用替换读取。此选项只对二进制读取生效。

## JSON 读取

JSON 支持相同的 `ref` 用法：

```csharp
var target = new Message();
LuminPackSerializer.DeserializeJson(json, ref target);
LuminPackSerializer.DeserializeJson(nextJson, ref target);
```

具体类型和泛型重载均支持 `string`、`ReadOnlySpan<char>`、`ReadOnlySpan<byte>` 和 `LuminBufferWriter`。已有对象（包括私有存储、构造函数绑定的成员）、嵌套对象、列表元素、精确类型且长度相同的数组，以及按传入键匹配的字典值都会复用。序列接口复用背后的精确 `List<T>` 或数组，字典接口复用精确 `Dictionary<K,V>`。可写且基于数组的 `Memory<T>`、`ArraySegment<T>` 在长度相同时保留原切片。已有池实例直接填充，不会再次租用。Union 只复用精确匹配的 case，引用跟踪对象则还原传入数据中的共享引用和循环。

在已验证的 .NET 10、11 CoreCLR 上，二进制与 JSON 连续读取同一规范区域性名称时，可以复用框架当前的实例。每个线程最多保留一个不超过 85 个字符的名称和一个只读区域性实例。仅接纳已在框架名称缓存中确认的 ASCII 名称。通过缓存的弱引用识别 `CultureInfo.ClearCachedData()`，未命中时调用 `CultureInfo.GetCultureInfo`。

UTF-8 和 UTF-16 下，JSON 解码后内容不变的字符串保留原引用，包括含转义的文本。对象、列表、数组和字符串都匹配时，预热后的读取可以没有托管分配；文本转码、变化的字符串、不同的数据形状、深层语法存储及用户代码仍可能分配。在 .NET 10 及更高版本，JSON 完整读取具体的 `ImmutableSortedSet<T>` 后，可以将至少两个严格升序的元素直接连接成平衡的冻结树。枚举及 CoreLib 中实现自身比较接口的非泛型值类型使用默认排序。字符串必须为非 null 引用，且当前区域性为标准类型。用户自定义比较、重复、乱序或缺少树节点绑定时使用框架 Builder。

JSON 的 `null` 会清空传入的引用。对已有目标，缺失的对象成员重置为 `default`；全新读取保留原有构造函数选择及缺失成员行为。列表移除尾部多余元素，字典在传入条目读取后移除旧键。复用不是事务性的：发生异常时，已经访问过的嵌套对象和存储可能保持修改。编码、语法验证和文档结束检查仍作用于整个文档。

## 实例池

标了 `[LuminPackPoolRent]` 的静态方法代替 `new` 提供新实例：

```csharp
[LuminPackable]
public partial class Bullet
{
    public float X, Y;

    [LuminPackPoolRent]
    public static Bullet Rent() => BulletPool.Rent();
}
```

对普通（`Object` 模式）类型，二进制和 JSON 读取只要需要一个实例，都会先调用标记了 `[LuminPackPoolRent]` 的 Rent 方法租借，再进入与 `ref` 读取相同的 Reuse 路径。**不传 `ref` 的 API 也默认如此**，包括已完成显式或 Custom 注册的 `Deserialize<T>(payload)`、`DeserializeJson<T>(json)`，以及缺失的子对象。租到的对象原本持有的 List、子对象和字符串也会继续复用，因此池已预热且存储匹配时，不显式传入 `ref` 目标同样可以零分配。

LuminPack 只负责租用，归还实例由应用负责。把已经租到的实例用 `ref` 传入时会直接复用，不会再租一个。

## 输出缓冲区

`LuminBufferWriter` 把数据存在池化托管数组中。用完必须 Dispose，或者把租来的缓冲区还回池：

```csharp
using var buffer = new LuminBufferWriter(true);
LuminPackSerializer.Serialize(value, buffer);
ReadOnlySpan<byte> bytes = buffer.GetSpan();
```

写入你持有的缓冲区时会从当前游标追加，重置方法见[快速开始](getting-started.cn.md#reusing-a-buffer)。返回 `byte[]`、`string` 的重载以及流、`IBufferWriter<byte>` 重载会自己管理池化缓冲区。
