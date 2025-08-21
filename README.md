# **LuminPack**



C#平台最快性能的二进制序列化器！


LuminPack是迄今为止C#平台性能最快的二进制序列化库，对于嵌套类，LuminPack比Memorypack，Messagepack快50%，200%。对于泛型类，LuminPack比Memorypack，Messagepack快400%。对于一些特殊集合，LuminPack甚至快上100倍。



LuminPack最初是专门为Unity开发的，为Unity在存档，网络等高性能场景下提供接近手搓特定类解析器的性能，LuminPack性能如何快的原因也是如此。对于LuminPack的基础类型，LuminPack会直接解析写入解析器，与此同时，LuminPack大量学习并借鉴了MemoryPack对于特定于C#的内存操纵，并通过指针反射，内存映射实现更高性能。



除了性能，LuminPack包含MemoryPack绝大部分特性，对于仅包含LuminPack基础类型的数据，二者甚至可以互相序列化。



LuminPack包含以下特性：

*   支持现代I / O api ( `IBufferWriter<byte>` ,  `ReadOnlySpan<byte>` ,  `ReadOnlySequence<byte>` )
*   基于增量源代码生成器的代码生成
*   运行时无GC
*   基于非托管内存的WriterBuffer
*   无反射
*   多态序列化
*   有限的版本容忍（快速/默认）和完全的版本容忍支持
*   循环引用序列化
*   基于PipeWriter/Reader的流序列化
*   通过增量源代码生成器支持Unity



## Installation 安装





## 后续更新计划：

因我目前还是大二，没有很多精力经常更新维护，因此不要抱有太大期望。

1.  序列化回调
2.  内置压缩功能
3.  内置加密功能



## Quick Start 快速启动

定义要序列化的结构体或类，并用 `[LuminPackable]` 属性对其进行注释。

    using LuminPack;

    [LuminPackable]
    public class Person
    {
        public int Age { get; set; }
        public string Name { get; set; }
    }



序列化代码将由c#源代码生成器功能生成，您可以查询生成的名为ClassNameParser.g.cs的文件查看详细代码。



调用 `LuminPackSerializer.Serialize<T>/Deserialize<T>` 序列化/反序列化对象实例。

    var item = new Person { Age = 18, Name = "Light" };

    var buffer = LuminPackSerializer.Serialize(item);
    var result = LuminPackSerializer.Deserialize<Person>(buffer);





## LuminPack基础类型

默认情况下，LuminPack基础类型将实现最高性能

    Int,
    UInt,
    Byte,
    Short,
    UShort,
    Long,
    ULong,
    Float,
    Double,
    String,
    Bool,
    Enum,
    Struct,
    Class,
    List,
    Array,





## 内置支持的类型

默认情况下，这些类型可以被序列化：

*   .Net所有非托管类型 (`byte`, `int`, `bool`, `char`, `double`, etc.)
*   `string`, `decimal`, `Half`, `Int128`, `UInt128`, `Guid`, `Rune`, `BigInteger`
*   `TimeSpan`, `DateTime`, `DateTimeOffset`, `TimeOnly`, `DateOnly`, `TimeZoneInfo`
*   `Complex`, `Plane`, `Quaternion` `Matrix3x2`, `Matrix4x4`, `Vector2`, `Vector3`, `Vector4`
*   `Uri`, `Version`, `StringBuilder`, `Type`, `BitArray`, `CultureInfo`
*   `T[]`, `T[,]`, `T[,,]`, `T[,,,]`, `Memory<>`, `ReadOnlyMemory<>`, `ArraySegment<>`, `ReadOnlySequence<>`
*   `Nullable<>`, `Lazy<>`, `KeyValuePair<,>`, `Tuple<,...>`, `ValueTuple<,...>`
*   `List<>`, `LinkedList<>`, `Queue<>`, `Stack<>`, `HashSet<>`, `SortedSet<>`, `PriorityQueue<,>`
*   `Dictionary<,>`, `SortedList<,>`, `SortedDictionary<,>`, `ReadOnlyDictionary<,>`
*   `Collection<>`, `ReadOnlyCollection<>`, `ObservableCollection<>`, `ReadOnlyObservableCollection<>`, `ReadOnlyCollectionBuilder<>`
*   `IEnumerable<>`, `ICollection<>`, `IList<>`, `IReadOnlyCollection<>`, `IReadOnlyList<>`, `ISet<>`
*   `IDictionary<,>`, `IReadOnlyDictionary<,>`, `ILookup<,>`, `IGrouping<,>`,
*   `ConcurrentBag<>`, `ConcurrentQueue<>`, `ConcurrentStack<>`, `ConcurrentDictionary<,>`, `BlockingCollection<>`
*   Immutable collections (`ImmutableList<>`, etc.) and interfaces (`IImmutableList<>`, etc.)





## 定义 `[LuminPackable]` 数据



`[LuminPackable]` 可以注释到任何 `class` ,  `abstract class`  ,  `struct`  ,  `record` ,  `record struct` 和 `interface` 。如果类型 `struct` 或 `record struct` 不包含引用类型（c#非托管类型），则不使用任何直接从内存序列化/反序列化的规则，LuminPack会直接复制内存。



默认情况下， `[LuminPackable]` 序列化公共实例属性或字段。可以使用 `[LuminPackIgnore]` 删除序列化目标， `[LuminPackInclude]` 将私有成员提升为序列化目标。



    [LuminPackable]
    public class MyClass
    {
        [LuminPackInclude]
        private MyClass2 myClass;
    }

    [LuminPackable]
    public class MyClass2
    {
        [LuminPackInclude]
        private int num1;
        [LuminPackIgnore]
        public long num2;
        public short num3;
        public double num4;
        public float num5;
        public string[] strings;
    }



LuminPack有27条诊断规则（ `LuminPack001` 到`LuminPack027` ）



LuminPack不序列化成员名或其他信息，而是按照声明的顺序序列化字段。如果类型是继承的，则按照父级→子级的内存布局顺序执行序列化。成员的顺序不能因反序列化而改变。关于模式演变，请参阅版本容忍部分。



默认的序列化顺序和布局是按照声明顺序的，如果想要更改，您可以使用 `[LuminPackOrder()]`

注：对于循环引用和完全版本容忍模式，每个字段和属性必须注释`[LuminPackOrder()]`

    // serialize Prop0 -> Prop1
    [LuminPackable]
    public class MyClass
    {
        [LuminPackOrder(1)]
        public int Prop1 { get; set; }
        [LuminPackOrder(0)]
        public int Prop0 { get; set; }
    }



LuminPack不依赖构造函数反序列化，因此您可以随意定义构造函数。



LuminPack默认支持**`0 ~ 249`**个成员字段





## &#x20;`[LuminPackableObject]`

\[LuminPackableObject]可以作用于任何字段以及属性，这将告诉LuminPackCodeGenerator不要直接解析该字段并写入Myclass的解析器，而是通过注册在LuminPack的Myclass2的解析器去解析。通常情况下，这会损失大概30%的性能，因此如果您遇到源代码生成器生成错误代码等情况，可以尝试用\[LuminPackableObject]标记字段或属性。



**以LuminPackable的示例代码为例**。在**.Net8**以上的平台, 对于嵌套类私有字段的解析，注释\[LuminPackInclude]将会**正常工作**。但在**.Net Standard2.1**平台，**这将不会工作**。

例如以上示例代码，对于MyClass的**”private MyClass2 myClass;"**字段，使用\[LuminPackInclude]将会正常工作并解析MyClass2的所有**public**字段，但是不会解析Myclass2的**private**字段, 即使您在Myclass2的**private**字段标记\[LuminPackInclude]。如果想要正常工作，请使用\[LuminPackableObject]来取消基础类型的解析。



    [LuminPackable]
    public class MyClass
    {
        [LuminPackInclude]
        [LuminPackableObject] //这将使MyClass2的私有字段num1正常解析
        private MyClass2 myClass;
    }





### 序列化回调



还没写，别急。





## 多态序列化

LuminPack支持序列化接口和抽象类对象，实现多态序列化。与MemoryPack的Union相同。只有接口和抽象类允许使用 `[LuminPackUnion]` 属性进行注释。需要唯一的联合标记。

    // Annotate [LuminPackable] and inheritance types with [LuminPackUnion]
    // Union also supports interface class
    [LuminPackable]
    [LuminPackUnion(0, typeof(Child1))]
    [LuminPackUnion(1, typeof(Child2))]
    public abstract class IUnionSample
    {
    }

    [LuminPackable]
    public class Child1 : IUnionSample
    {
        public int num;
    }

    [LuminPackable]
    public class Child2 : IUnionSample
    {
        public string str;
    }

    IUnionSample data = new Child1() { num = 114514};

    // Serialize
    var buffer = LuminPackSerializer.Serialize(data);

    // Deserialize
    var result = LuminPackSerializer.Deserialize<IUnionSample>(buffer);

    switch (result)
    {
        case Child1 x:
            Console.WriteLine(x.num);
            break;
        case Child2 x:
            Console.WriteLine(x.str);
            break;
        default:
            break;
    }



对于`LuminPackUnion`的Tag，支持 `0`  \~  `65535`， 对与`250`以下的性能更佳。因此推荐使用`250`以下的值作为Tag





## 版本容忍



在默认情况下 LuminPack的代码生成模式（ `GenerateType.Object` ）， 仅支持有限的模式演化。



*   如果数据类型是非托管数据，例如Struct（不包含引用类型）。不能更改数据
*   可以添加成员，不能删除成员。
*   不能更改成员名称
*   不能更改成员顺序
*   不能更改成员类型

<!---->

    [LuminPackable]
    public class MyClass
    {
        public int Prop1 { get; set; }
        public long Prop2 { get; set; }
    }

    // Add is OK.
    [LuminPackable]
    public class MyClass
    {
        public int Prop1 { get; set; }
        public long Prop2 { get; set; }
        public int? AddedProp { get; set; }
    }

    // Remove is NG.
    [LuminPackable]
    public class MyClass
    {
        // public int Prop1 { get; set; }
        public long Prop2 { get; set; }
    }

    // Change order is NG.
    [LuminPackable]
    public class MyClass
    {
        public long Prop2 { get; set; }
        public int Prop1 { get; set; }
    }



当使用 `GenerateType.VersionTolerant` 时，它支持完全的版本容忍。

```


[LuminPackable(GenerateType.VersionTolerant)]
public class VersionTolerantObject1
{
    [LuminPackOrder(0)]
    public int MyProperty0 { get; set; } = default;

    [LuminPackOrder(1)]
    public long MyProperty1 { get; set; } = default;

    [LuminPackOrder(2)]
    public short MyProperty2 { get; set; } = default;
}

[LuminPackable(GenerateType.VersionTolerant)]
public class VersionTolerantObject2
{
    [LuminPackOrder(0)]
    public int MyProperty0 { get; set; } = default;

    // deleted
    //[LuminPackOrder(1)]
    //public long MyProperty1 { get; set; } = default;

    [LuminPackOrder(2)]
    public short MyProperty2 { get; set; } = default;

    // added
    [LuminPackOrder(3)]
    public short MyProperty3 { get; set; } = default;
}
```



`GenerateType.VersionTolerant` 比 `GenerateType.Object` 性能更差，使用时请注意。





## 循环引用



    // to enable circular-reference, use GenerateType.CircularReference
    [LuminPackable(GenerateType.CircularReference)]
    public class Node
    {
        [LuminPackOrder(0)]
        public Node? Parent { get; set; }
        [LuminPackOrder(1)]
        public Node[]? Children { get; set; }
    }



`GenerateType.CircularReference` 具有与版本容忍相同的特性。

对象引用跟踪只对标记为 `GenerateType.CircularReference` 的对象进行。如果要跟踪任何其他对象，请对其进行包装。





## WriteBuffer池



LuminPack的序列化池通过Marshal申请非托管内存，这极大提高了Buffer扩容的性能。

因此，请确保所有WriteBuffer调用Dispose方法，以释放非托管内存。



LuminPack内置了高性能`ObjectPool`



    #if NET8_0_OR_GREATER
        private static readonly ObjectPool<ReusableLinkedArrayBufferWriter> _pool = 
            new(MaxPoolSize);
    #else
        private static readonly ObjectPool<ReusableLinkedArrayBufferWriter> _pool = 
            new(new BufferWriterPolicy(), MaxPoolSize);
    #endif

    public static ReusableLinkedArrayBufferWriter Rent() => _pool.Rent();
        
    public static void Return(ReusableLinkedArrayBufferWriter writer) => _pool.Return(writer);



.Net8以上版本，ObjectPool的对象需要继承IPooledObjectPolicy接口。

.Net Standard2.1版本，则需要单独定义继承继承IPooledObjectPolicy接口的类，通过依赖注入的方式。





## Unity



Unity的安装非常简单，直接通过导入.unityPackage文件即可。



LuminPack对于Unity有特殊优化，以达到.Net8版本相同的性能。



*   对于`List<>，Stack<>，Queue<>，Collection<>，ReadonlyCollection<>，ObserveableCollection<>，ReadonlyObserveableCollection<>，ReadOnlyCollectionBuilder<>` 的非托管泛型，LuminPack比MemoryPack快22倍 （1024数据量）
*   运行时0GC （除必要的序列化byte\[]开销和反序列化Value的开销）
*   Serialize API和Deserialize API的类型检查优化，提高处理特殊数据的性能。





## 二进制格式规范



端序必须 `Little Endian` 。



### 非托管结构



非托管结构是不包含引用类型的c#结构，类似于c#非托管类型的约束。序列化结构布局，包括填充。



### Object 对象



`(byte memberCount, [values...])`



对象头文件中的成员计数为1字节无符号字节。成员数允许 `0` 到 `249` ,  `255` 表示对象 `null` 。值存储成员数的内存包值。



### Version Tolerant Object 版本容忍对象



`(byte memberCount, [varint byte-length-of-values...], [values...])`



版本容忍对象与 Object 类似，但在头部包含值的字节长度。变长整数遵循以下规范：第一个有符号字节（sbyte）是值或类型代码，接下来的 X 个字节是具体值。其中，0 到 127 对应无符号字节值，-1 到 - 120 对应有符号字节值，-121 对应字节（byte），-122 对应有符号字节（sbyte），-123 对应无符号短整数（ushort），-124 对应短整数（short），-125 对应无符号整数（uint），-126 对应整数（int），-127 对应无符号长整数（ulong），-128 对应长整数（long）。



### Circular Reference Object 循环引用对象



`(byte memberCount, [varint byte-length-of-values...], varint referenceId, [values...])`\
`(250, varint referenceId)`



循环引用对象类似于版本容忍对象，但如果memberCount为250，则下一个变量（unsigned-int32）为referenceId。如果不是，则在字节长度值之后写入变量referenceId。



### String 字符串



`(int utf16-length, utf16-value)`\
`(int ~utf8-byte-count, int utf16-length, utf8-bytes)`



字符串有两种形式，UTF16和UTF8。如果第一个4byte有符号整数 `-1` ，表示null。 `0` ，表示空。UTF16与collection相同（序列化为 `ReadOnlySpan<char>` ， UTF16 -value的字节数为UTF16 -length \* 2）。如果第一个有符号整数<=  `-2` ，则value用UTF8编码。Utf8-byte-count以补码形式编码， `~utf8-byte-count` 检索字节数。下一个有符号整数是utf16-length，它允许 `-1` 表示未知长度。Utf8-bytes存储utf8-byte-count的字节数。



### Union 多态



`(byte tag, value)`\
`(250, ushort tag, value)`



第一个无符号字节是用于区分值类型或标志的标记， `0` 到 `249` 表示标记， `250` 表示下一个无符号短标记， `255` 表示 `null` 。



### Collection 集合



`(int length, [values...])`

集合头的数据计数为4字节有符号整数， `-1` 表示 `null` 。头字节存储数据长度。



### Tuple 元组



`(values...)`



元组是固定大小的非空值集合。 `KeyValuePair<TKey, TValue>` 和 `ValueTuple<T,...>` 被序列化为Tuple。





## License 许可证



This library is licensed under the MIT License.



