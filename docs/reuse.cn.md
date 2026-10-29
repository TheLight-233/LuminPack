# ♻️ 复用与零分配

[English](reuse.md) · [返回 README](../README.cn.md)

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
| `List<T>` | 保留容量和已有元素；需要时扩容，缩短时清理被移除的引用。 |
| `Dictionary<K,V>` | 保留比较器，按传入的键复用值；读取成功后，被移除的键消失。 |
| `SortedList<string,T>` | 同一位置传入的键与原键按序数比较相等时复用值。 |
| 序列接口（`IEnumerable<T>`、`ICollection<T>`、`IList<T>`、`IReadOnlyList<T>`、`IReadOnlyCollection<T>`） | 元素为字符串、标量或普通对象时，复用背后的精确 `List<T>` 或 `T[]`。 |
| `IDictionary<K,V>`、`IReadOnlyDictionary<K,V>` | 复用背后的精确 `Dictionary<K,V>`。 |
| `Memory<T>`、`ArraySegment<T>` | 长度相同时复用数组存储，保留 offset 和 count。 |
| 标量或字符串的 Immutable 集合 | 传入内容完全相同时保留。 |
| 字符串 | 内容不变的字符串保留原引用，所有字符串配置都适用。 |
| Union | 只复用类型完全相同的 case；标签不同时构造新的 case。 |
| 版本容忍对象 | 复用，缺失成员重置为 `default`。 |
| 引用跟踪对象 | 复用，同时保留传入数据中的共享引用和循环。 |

派生类、协变以及未知的集合实现会被替换，不会去检查其内容。引用类型元素的 `SortedSet<T>` 总是重建，因为修改一个被用户代码重新插入的元素可能破坏树的排序。

对象、集合存储和字符串都已经匹配时，预热后的重复读取可以达到 **0 字节托管分配**。新对象、更大的存储、长度不同的数组、变化的字符串、不可变框架值以及用户回调仍会分配。

### 需要注意

- 复用不是事务性的。读取失败或回调抛出异常时，已经读完的成员保持更新后的值。
- 普通模式的 `null` 对象记录不会覆盖传入的根对象。专用模式和集合遵循各自的 null 规则。
- 如果目标中的两个成员共享同一个实例，两次读取都会填充这个实例。需要还原传入数据的对象身份时使用 `CircleReference`。

## JSON 读取

JSON 支持相同的 `ref` 用法：

```csharp
var target = new Message();
LuminPackSerializer.DeserializeJson(json, ref target);
LuminPackSerializer.DeserializeJson(nextJson, ref target);
```

具体类型和泛型重载均支持 `string`、`ReadOnlySpan<char>`、`ReadOnlySpan<byte>` 和 `LuminBufferWriter`。已有对象（包括私有存储、构造函数绑定的成员）、嵌套对象、列表元素、精确类型且长度相同的数组，以及按传入键匹配的字典值都会复用。序列接口复用背后的精确 `List<T>` 或数组，字典接口复用精确 `Dictionary<K,V>`。可写且基于数组的 `Memory<T>`、`ArraySegment<T>` 在长度相同时保留原切片。已有池实例直接填充，不会再次租用。Union 只复用精确匹配的 case，引用跟踪对象则还原传入数据中的共享引用和循环。

UTF-8 和 UTF-16 下，JSON 解码后内容不变的字符串保留原引用，包括含转义的文本。对象、列表、数组和字符串都匹配时，预热后的读取可以没有托管分配；文本转码、变化的字符串、不同的数据形状、深层语法存储及用户代码仍可能分配。不可变集合的格式化器保留原有构造行为。

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

对普通（`Object` 模式）类型，二进制和 JSON 读取只要需要一个实例，都会先调用标记了 `[LuminPackPoolRent]` 的 Rent 方法租借，再进入与 `ref` 读取相同的 Reuse 路径。**不传 `ref` 的 API 也默认如此**，包括 `Deserialize<T>(payload)`、`DeserializeJson<T>(json)`，以及缺失的子对象。租到的对象原本持有的 List、子对象和字符串也会继续复用，因此池已预热且存储匹配时，不显式传入 `ref` 目标同样可以零分配。

LuminPack 只负责租用，归还实例由应用负责。把已经租到的实例用 `ref` 传入时会直接复用，不会再租一个。

## 输出缓冲区

`LuminBufferWriter` 把数据存在池化托管数组中。用完必须 Dispose，或者把租来的缓冲区还回池：

```csharp
using var buffer = new LuminBufferWriter(true);
LuminPackSerializer.Serialize(value, buffer);
ReadOnlySpan<byte> bytes = buffer.GetSpan();
```

写入你持有的缓冲区时会从当前游标追加，重置方法见[快速开始](getting-started.cn.md#reusing-a-buffer)。返回 `byte[]`、`string` 的重载以及流、`IBufferWriter<byte>` 重载会自己管理池化缓冲区。
