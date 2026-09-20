# LuminPack API 参考

## LuminPackSerializer（生成的入口类）

`LuminPackSerializer` 由源码生成器为每个编译生成，命名空间 `LuminPack`。每个 formatter 类型生成一套具体重载（无泛型派发开销），另有泛型便捷入口兜底。

### 二进制

```csharp
byte[] Serialize(in T value, LuminPackSerializerOption? option = null);
void   Serialize(in T value, LuminBufferWriter writerBuffer);       // 复用缓冲，高吞吐首选
int    Deserialize(ReadOnlySpan<byte> buffer, ref T value, LuminPackSerializerOption? options = null);
int    Deserialize(ReadOnlySequence<byte> buffer, ref T value, LuminPackSerializerOption? options = default);
int    Deserialize(LuminBufferWriter bufferWriter, ref T value);
T      Deserialize<T>(ReadOnlySpan<byte> buffer, ...);               // 泛型便捷入口
```

### JSON

```csharp
string SerializeJson(T value, LuminPackSerializerOption? option = null);
void   SerializeJson(T value, LuminBufferWriter writerBuffer);
int    DeserializeJson(string buffer, ref T value, LuminPackSerializerOption? options = null);
int    DeserializeJson(ReadOnlySpan<byte> buffer, ref T value, ...);
int    DeserializeJson(ReadOnlySpan<char> buffer, ref T value, ...);
int    DeserializeJson(LuminBufferWriter bufferWriter, ref T value);
```

另有 `SerializeAsync` / `DeserializeAsync`（ValueTask）与尺寸计算 `Sizeof` 重载。

### 压缩（LZ）

```csharp
int    Compress(LuminBufferWriter source, LuminBufferWriter destination);
byte[] Compress(ReadOnlySpan<byte> source);
int    Compress(ReadOnlySpan<byte> source, Span<byte> destination);
int    Decompress(ReadOnlySpan<byte> source, Span<byte> destination);
int    Decompress(LuminBufferWriter source, LuminBufferWriter destination);
byte[] Decompress(ReadOnlySpan<byte> source);
```

### Register 手动注册（unsafe，需 AllowUnsafeBlocks）

泛型派发兜底，注册源码生成器未覆盖的类型；以 **函数指针（`delegate*`）** 传静态方法，注册表以 MethodTable 地址（`nint`）为键、不持有强 Type 引用（ALC 安全）。三种重载：

| 重载 | 参数 |
| --- | --- |
| `Register<T>(writeValue, readValue)` | 二进制写/读：`(ref LuminPackWriter, in T)` / `(ref LuminPackReader, ref T)` |
| `Register<T>(writeValue, readValue, writeValueJson, readValueJson)` | 二进制 + JSON |
| `Register<T>(writeValue, readValue, writeValueJson, readValueJson, calculateOffset)` | 二进制 + JSON + 尺寸 |

重复注册同一类型抛 `ArgumentException`。多态基类另有 `BaseType.Register<TMember>(tag, &Write, &Read, &WriteJson, &ReadJson)` 用于跨程序集注册子类；注册的成员 tag 不能与生成成员冲突。

#### Register&lt;T&gt; 示例（方法参数为**具体类型**，不是 object）

```csharp
// 二进制写/读（必填）
private static void WriteLegacy(ref LuminPackWriter writer, in LegacyModel value)
{
    writer.WriteValue(value.Id);
    writer.WriteValue(value.Name);
}

private static void ReadLegacy(ref LuminPackReader reader, ref LegacyModel value)
{
    value = new LegacyModel();
    reader.ReadValue(ref value.Id);
    reader.ReadValue(ref value.Name);
}

// JSON 写/读（选填，需成对）
private static void WriteJsonLegacy(ref LuminPackJsonWriter writer, in LegacyModel value)
{
    writer.WriteObjectStart();
    writer.WritePropertyName("Id");
    writer.WriteValue(value.Id);
    writer.WritePropertyName("Name");
    writer.WriteValue(value.Name);
    writer.WriteObjectEnd();
}

private static void ReadJsonLegacy(ref LuminPackJsonReader reader, ref LegacyModel value)
{
    value = new LegacyModel();
    reader.ReadValue(ref value.Id);
    reader.ReadValue(ref value.Name);
}

// 尺寸计算（选填）
private static void CalcSizeLegacy(ref LuminPackEvaluator evaluator, in LegacyModel value)
{
    evaluator.WriteValue(value.Id);
    evaluator.WriteValue(value.Name);
}

// 注册（函数指针，需 unsafe 上下文）
unsafe
{
    LuminPackSerializer.Register<LegacyModel>(
        &WriteLegacy, &ReadLegacy,
        &WriteJsonLegacy, &ReadJsonLegacy,
        &CalcSizeLegacy);
}
```

#### 跨程序集多态 Register&lt;TMember&gt; 示例（基类在程序集 A，子类在程序集 B）

```csharp
// 基类：global::MyLib.IFoo（程序集 A）；子类：global::MyApp.FooA（程序集 B，[LuminPackable]）
private static void WriteFooA(ref LuminPackWriter writer, ref global::MyLib.IFoo value)
{
    writer.WriteUnionHeader(0);  // tag 由你指定，须与 Register 的 tag 一致
    writer.WritePolymorphismValue(LuminPackMarshal.As<global::MyLib.IFoo, global::MyApp.FooA>(ref value));
}

private static void ReadFooA(ref LuminPackReader reader, ref global::MyLib.IFoo value)
{
    global::MyApp.FooA tempValue = default!;
    reader.ReadPolymorphismValue(ref tempValue);
    value = LuminPackMarshal.As<global::MyApp.FooA, global::MyLib.IFoo>(ref tempValue!);
}

private static void WriteJsonFooA(ref LuminPackJsonWriter writer, ref global::MyLib.IFoo value)
{
    writer.WriteObjectStart();
    writer.WritePropertyName("$type");
    writer.WriteInt(0);
    writer.WritePropertyName("$value");
    writer.WriteValue(LuminPackMarshal.As<global::MyLib.IFoo, global::MyApp.FooA>(ref value)!);
    writer.WriteObjectEnd();
}

private static void ReadJsonFooA(ref LuminPackJsonReader reader, ref global::MyLib.IFoo value)
{
    global::MyApp.FooA tempValue = default!;
    reader.ReadValue(ref tempValue);
    value = LuminPackMarshal.As<global::MyApp.FooA, global::MyLib.IFoo>(ref tempValue!);
}

// 最后调用基类的 Register（TMember 为具体子类，tag 与写入的 union header 一致）
unsafe
{
    global::MyLib.IFoo.Register<global::MyApp.FooA>(0,
        &WriteFooA, &ReadFooA, &WriteJsonFooA, &ReadJsonFooA);
}
```

说明：四个方法指针（二进制写/读 + JSON 写/读）均必填；`Sizeof` 对该注册成员仍抛异常。注册表定义在基类 partial 上，`Register` 调用与序列化在同一进程内即可。

## Option 配置

`LuminPackSerializerOption`（record，`LuminPack.Option`）：

- 预置实例：`Default`、`Utf8`、`Utf16`、`Token`、`Length`、`Utf16WithLength`。
- 属性：`StringEncoding`（`UTF8`/`UTF16`）、`StringRecording`（`Length`/`Token`）、`StandardFormat`、`MaxJsonDepth`（默认 1024，≤0 关闭限制）。
- 字符串编码/记录方式在序列化与反序列化两侧必须一致。

## LuminBufferWriter（非托管缓冲）

- 通过 Marshal 申请非托管内存，扩容与复用几乎零分配；**所有 WriteBuffer 必须调用 `Dispose()` 释放非托管内存**。
- 线程静态 / 集中池支持；带 option 的 Buffer 重载使用 writer 自身持有的 option 与状态，避免 ThreadStatic 开销。

## 低级 Writer / Reader（自定义 formatter 与 Register 方法内使用）

### LuminPackWriter（`LuminPack.Core`）

```csharp
WriteValue<T>(in T value) / WriteValue<T>(ref int index, in T value);   // 泛型派发（默认分支走 Register 表）
WriteUnmanaged<T>(in T value) / WriteUnmanagedArray / WriteUnmanagedSpan; // unmanaged 快速路径
WriteSpan<T>(Span<T>) / WriteSpanWithOutHeader<T>(...);                 // 集合写
WriteString(string?) / WriteString(ReadOnlySpan<char>);                 // 字符串
WriteUnionHeader(ushort tag) / WriteNullUnionHeader();                  // union 头
WriteNullObjectHeader() / WriteNullCollectionHeader() / WriteNullStringHeader();
WriteInt / WriteFloat / WriteDouble / ...                                // 标量原语
```

带 `ref int index` 的重载可预先跳写头部（out-of-order header）。

### LuminPackReader（`LuminPack.Core`）

镜像 API：`ReadValue<T>(ref T)`、`ReadUnmanaged<T>(out T)`、`ReadSpan<T>`、`ReadString()`、`ReadUnionHeader()`、`ReadNullObjectHeader()` 等。读 unmanaged 数组/跨度有 `Dangerous*` 变体走直接内存复制。

### LuminPackMarshal（`LuminPack.Code`）

自定义 formatter / 注册方法的底层工具：`As<TForm,TTo>(ref TForm)`（引用重解释）、`GetListSpan/SetListSize`、`GetStackSpan`、`GetQueueSpan/SetQueueSize`、`GetDictionaryView`、`GetHashSetView`、`AsRawBytes`、`ReinterpretCast`、`GetMethodTable`、`AllocateUninitializedArray`、`CopyBlockUnaligned` 等。

## GeneratorType.Custom 手写 formatter

`[LuminPackable(GeneratorType.Custom)]`：生成器不生成扩展方法，校验并自动注册手写静态方法（模块初始化器，无需手动调用 Register）。方法必须 `public`/`internal` 且 `static`：

```csharp
public static void Serialize(ref LuminPackWriter writer, in MyVector value);
public static void Deserialize(ref LuminPackReader reader, ref MyVector value);
// 可选成对：
public static void SerializeJson(ref LuminPackJsonWriter writer, in MyVector value);
public static void DeserializeJson(ref LuminPackJsonReader reader, ref MyVector value);
// 可选：
public static void CalculateOffset(ref LuminPackEvaluator evaluator, in MyVector value);
```

组合约束（LuminPack100~108 校验）：仅二进制 = Serialize+Deserialize；二进制+JSON = 再加 JSON 成对；二进制+JSON+尺寸 = 再算 CalculateOffset。泛型类型按封闭类型注册；union 根不支持 Custom。项目未开启 `AllowUnsafeBlocks` 时注册被跳过（警告）。

#### Custom formatter 完整示例

```csharp
using LuminPack;
using LuminPack.Core;

[LuminPackable(GeneratorType.Custom)]
public sealed class MyVector
{
    public float X, Y, Z;

    // 必需：二进制写/读
    public static void Serialize(ref LuminPackWriter writer, in MyVector value)
    {
        writer.WriteValue(value.X);
        writer.WriteValue(value.Y);
        writer.WriteValue(value.Z);
    }

    public static void Deserialize(ref LuminPackReader reader, ref MyVector value)
    {
        value = new MyVector();
        reader.ReadValue(ref value.X);
        reader.ReadValue(ref value.Y);
        reader.ReadValue(ref value.Z);
    }

    // 可选（需成对）：JSON 写/读
    public static void SerializeJson(ref LuminPackJsonWriter writer, in MyVector value)
    {
        writer.WriteObjectStart();
        writer.WritePropertyName("X"); writer.WriteValue(value.X);
        writer.WritePropertyName("Y"); writer.WriteValue(value.Y);
        writer.WritePropertyName("Z"); writer.WriteValue(value.Z);
        writer.WriteObjectEnd();
    }

    public static void DeserializeJson(ref LuminPackJsonReader reader, ref MyVector value)
    {
        value = new MyVector();
        reader.ReadValue(ref value.X);
        reader.ReadValue(ref value.Y);
        reader.ReadValue(ref value.Z);
    }

    // 可选：尺寸计算
    public static void CalculateOffset(ref LuminPackEvaluator evaluator, in MyVector value)
    {
        evaluator.WriteValue(value.X);
        evaluator.WriteValue(value.Y);
        evaluator.WriteValue(value.Z);
    }
}

// 使用与普通类型一致，运行期自动走注册的手写方法
var buffer = LuminPackSerializer.Serialize(new MyVector());
```

> 注意：`SerializeJson/DeserializeJson` 的 JSON 写法是 LuminPack JSON 协议（对象/属性名），不是 `JsonSerializer`。

## 加密（LuminChaCha20Poly1305，`LuminPack.Cryptography`）

RFC 8439 AEAD：32 字节密钥（256-bit，另有 `KeySize128`=16）、12 字节 nonce、16 字节 tag。

```csharp
unsafe void Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad,
                    ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag);
unsafe bool Decrypt(...);   // 密文+tag → 明文，认证失败返回 false
ValueTask EncryptAsync(...) / ValueTask<bool> DecryptAsync(...);
```

约束：ciphertext 长度 == plaintext 长度，tag 缓冲 ≥ 16 字节。ChaCha20 内部实现保持 RFC 8439 标准兼容（32 位计数器 + 96 位 nonce 布局）。

## 二进制格式要点

- 端序：一律 Little Endian。
- 非托管 struct：直接内存布局（含填充）。
- Object：`(byte memberCount, [values...])`；memberCount 0~249，255 = null。
- VersionTolerant：`(byte memberCount, varint length, [values...])`。
- CircularReference：同版本容忍 + `varint referenceId`；`(250, varint referenceId)` 表示已引用。
- String：Length 模式 `(int utf16len, utf16)` 或 `(int utf8bytes, int utf16len, utf8)`；`-1` = null，`0` = 空串。Token 模式无 null 标记，null 与空串编码一致。
- Union：`(byte tag, value)`；`250` 后跟 `ushort` 宽 tag；`255` = null。
- Collection：`(int length, [values...])`；`-1` = null。
- Tuple / KeyValuePair / ValueTuple：`(values...)`。

## JSON 格式要点

- 对象为 JSON object，属性名 = C# 成员名；未知属性跳过，缺失成员保持默认值。
- 数组/顺序集合 → array；多维数组 → 嵌套 array；字典 → `[[key, value], ...]`；Tuple/ValueTuple/KVP → 两元素 array。
- Union → `{"$type": tag, "$value": ...}`；null 直接写 `null`。
- CircularReference → `$id` / `$ref`（保留属性名，勿用作普通成员名）。
