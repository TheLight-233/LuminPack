# 🎯 定义类型

[English](defining-types.md) · [返回 README](../README.cn.md)

## `[LuminPackable]`

给 `class`、`abstract class`、`struct`、`record`、`record struct` 或 `interface` 标记 `[LuminPackable]`，源代码生成器就会在编译期为它生成二进制和 JSON 读写代码。

```csharp
using LuminPack.Attribute;

[LuminPackable]
public partial class Player
{
    public int Id;
    public string Name { get; set; } = "";
    public List<int> Scores = new();
}
```

Union 的根类型、它的所有 case 以及外层包含类型都必须声明为 `partial`，生成器才能生成虚方法派发（否则报 `LuminPack035`）。其他类型也建议声明为 `partial`。

不含引用字段的 struct 或 record struct（C# 非托管类型）直接按内存复制。这类结构体的存储方式见 [32/64 位结构体布局](options.cn.md#struct-layout)。

`GeneratorType` 选择数据表示：

| 模式 | 用途 |
| --- | --- |
| `Object`（默认） | 按位置写成员，最快、最紧凑，适合结构稳定的模型。 |
| `VersionTolerant` | 按名称识别成员，可以增删、重排、重命名字段。见[版本容忍](versioning.cn.md)。 |
| `CircleReference` | 引用跟踪，支持共享对象和循环。见[引用跟踪](versioning.cn.md#reference-tracking)。 |
| `Custom` | 使用你手写的静态读写方法。见[自定义格式化器](custom-formatters.cn.md)。 |
| `NonGenerator` | 不为该类型生成格式化器。 |

## 成员

默认序列化公共实例字段和公共自动属性（包括 setter 为 private 的属性）。计算属性没有存储，不会被序列化。

| 特性 | 作用 |
| --- | --- |
| `[LuminPackIgnore]` | 排除一个公共成员。 |
| `[LuminPackInclude]` | 纳入 private 或 protected 的字段、属性。 |
| `[LuminPackOrder(n)]` | 指定写入顺序，不改变 CLR 内存布局。 |
| `[LuminPackableObject]` | 调用成员类型自己的完整格式化器，而不是把它的成员内联展开。用于 `Custom` 等专用类型的成员。 |
| `[LuminPackCompress]` | 用 LuminPack LZ 压缩支持的非托管数组或 span 成员。 |
| `[LuminPackDelegate]`、`[LuminPackStaticDelegate]`、`[LuminPackSingletonDelegate]` | 把单个成员交给你自己的方法处理，方法可以在实例、静态类型或单例上。 |

```csharp
[LuminPackable]
public partial class Inventory
{
    [LuminPackInclude] private int _version;
    [LuminPackIgnore] public int CachedTotal;

    [LuminPackOrder(1)] public string Owner = "";
    [LuminPackOrder(0)] public long Id;
}
```

`Object` 模式按位置写成员：先父类后子类，按声明顺序或 `LuminPackOrder` 顺序。读取方必须使用相同的成员和顺序。一个对象可以有 0 到 249 个成员。

`[LuminPackInclude]` 在每一层嵌套都有效：嵌套类型中标了 `[LuminPackInclude]` 的私有成员会按同样规则纳入。私有成员由生成的代码直接访问，从不使用反射，在 .NET Standard 2.1 和新版 .NET 上都一样。

### 构造

LuminPack 不要求无参构造函数。标了 `[LuminPackConstructor]` 的构造函数（或自动选中的构造函数）的参数名必须与被序列化的成员同名，读取时这些成员作为参数传入。读入已有对象时（见[复用](reuse.cn.md)）根对象完全不会被构造。

标了 `[LuminPackPoolRent]` 的静态方法会在需要实例时代替 `new`。对 `Object` 模式类型的二进制读取，租到的对象随后被原地填充，和读入已有对象完全一样，因此它原本持有的内容（List、子对象、字符串）都会被复用：

```csharp
[LuminPackPoolRent]
public static Player Rent() => PlayerPool.Rent();
```

LuminPack 不会把实例还回你的池；池和归还策略由应用自己负责。

### 回调

标了下列特性的实例方法或静态方法会在每次读写前后执行：

```csharp
[LuminPackOnSerializing]   public void BeforeWrite() { }
[LuminPackOnSerialized]    public void AfterWrite() { }
[LuminPackOnDeserializing] public static void BeforeRead() { }
[LuminPackOnDeserialized]  public void AfterRead() { }
```

## 大小担保

如果一个类型的完整二进制长度有已知上限，`[LuminPackBounded(maxBytes)]` 让生成代码一次性预留这个上限，并去掉内部所有容量检查。这种元素组成的集合也只预留一次 `count * maxBytes`。

```csharp
[LuminPackable, LuminPackBounded(128)]
public sealed partial class SmallRecord
{
    public int Id;
    public string Label = "";      // 本应用中最多 10 个字符
    public int[] Values = [];      // 本应用中最多 16 个元素
}
```

上限要覆盖记录头和所有嵌套数据，并且在该类型用到的每种字符串选项下都成立。这是你做出的担保：担保错误可能写出缓冲区边界。游标仍按实际长度前进，数据格式不变。没有可靠上限的类型不要使用。

## 内置支持的类型

除 `[LuminPackable]` 类型外，以下类型内置支持二进制和 JSON：

| 类别 | 类型 |
| --- | --- |
| 基础类型 | `sbyte`、`byte`、`short`、`ushort`、`int`、`uint`、`long`、`ulong`、`nint`、`nuint`、`float`、`double`、`decimal`、`bool`、`char`、枚举 |
| .NET 8+ 标量 | `Half`、`Int128`、`UInt128`、`Rune`、`DateOnly`、`TimeOnly` |
| 时间与标识 | `Guid`、`DateTime`、`DateTimeOffset`、`TimeSpan`、`TimeZoneInfo` |
| 数学 | `BigInteger`、`Complex`、`Vector2`、`Vector3`、`Vector4`、`Quaternion`、`Plane`、`Matrix3x2`、`Matrix4x4` |
| 文本与系统 | `string`、`StringBuilder`、`Uri`、`Version`、`Type`、`BitArray`、`CultureInfo` |
| 包装类型 | `Nullable<T>`、`Lazy<T>`、`KeyValuePair<K,V>`、`Tuple<...>` 和 `ValueTuple<...>`（1 到 7 项） |
| 内存 | `T[]`、多维数组、`Memory<T>`、`ReadOnlyMemory<T>`、`ArraySegment<T>`、`ReadOnlySequence<T>` |
| 集合 | `List<T>`、`LinkedList<T>`、`Queue<T>`、`Stack<T>`、`HashSet<T>`、`SortedSet<T>`、`PriorityQueue<T,P>`、`Dictionary<K,V>`、`SortedDictionary<K,V>`、`SortedList<K,V>`、`ReadOnlyDictionary<K,V>` |
| 并发集合 | `ConcurrentBag<T>`、`ConcurrentQueue<T>`、`ConcurrentStack<T>`、`ConcurrentDictionary<K,V>`、`BlockingCollection<T>` |
| ObjectModel | `Collection<T>`、`ReadOnlyCollection<T>`、`ObservableCollection<T>`、`ReadOnlyObservableCollection<T>` |
| Immutable 与 Frozen | `ImmutableArray`、`ImmutableList`、`ImmutableQueue`、`ImmutableStack`、`ImmutableDictionary`、`ImmutableHashSet`、`ImmutableSortedDictionary`、`ImmutableSortedSet`、`ReadOnlyCollectionBuilder`、`FrozenDictionary`、`FrozenSet`（.NET 8+） |
| 接口 | `IEnumerable<T>`、`ICollection<T>`、`IReadOnlyCollection<T>`、`IList<T>`、`IReadOnlyList<T>`、`ISet<T>`、`IReadOnlySet<T>`、`IDictionary<K,V>`、`IReadOnlyDictionary<K,V>`、`ILookup<K,V>`、`IGrouping<K,V>` 以及 `IImmutable*` 接口 |

引擎和 UI 的值类型（Unity、MonoGame、WPF、Avalonia、MAUI、SkiaSharp、System.Drawing、ASP.NET Core）由 [README](../README.cn.md#-平台扩展包) 中列出的可选平台包提供。
