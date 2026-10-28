# 📐 数据格式

[English](formats.md) · [返回 README](../README.cn.md)

二进制与 JSON 是两套独立的协议。不要把 `Serialize` 的输出交给 `DeserializeJson`，反之亦然。

## 二进制

所有值都是小端序。

### 非托管值

不含引用的结构体按原始内存存储（包括 padding），除非它的 `StructLayout` 选项另有规定（见 [32/64 位结构体布局](options.cn.md#struct-layout)）。

### Object 对象

`(byte memberCount, [values...])`

成员数为 0 到 249，`255` 表示 `null`。之后按成员顺序写值。

### 版本容忍对象

`(251, 1, byte profile, byte memberCount, [UTF-8 名称/类型元数据...], [VarInt 负载长度...], [负载...])`

`255` 表示 `null`。详见[版本容忍](versioning.cn.md#binary-framing)。

### 引用跟踪对象

`(byte memberCount, [VarInt 槽位长度...], VarInt referenceId, [values...])`
`(250, VarInt referenceId)`

新对象先写槽位长度表，再写引用 id。`250` 表示引用一个已经读过的对象。

### 字符串

长度模式，UTF-16：`(int header, payload)`。`-1` 表示 `null`，`0` 表示 `""`。正偶数 header 表示随后 UTF-16 码元的字节数。当所有字符都不超过 U+00FF 时，改写紧凑形式：奇数 header `(length << 1) | 1`，随后每个字符一个 Latin-1 字节。两种形式的字符数都是 `header >> 1`，读取时都接受。

长度模式，UTF-8：`(int utf8ByteCount, int utf16Length, utf8Bytes)`。未知时 `utf16Length` 可以为 `-1`。

Token 模式写以终止符结束的字符串，没有 `null` 标记；`null` 和 `""` 读回来都是 `""`。

### Union

标签 0 到 249 写 `(byte tag, value)`，更大的标签写 `(250, ushort tag, value)`。`255` 表示 `null`。

### 集合

`(int count, [values...])`。`-1` 表示 `null`。

### 元组

`(values...)`。固定数量、非空的值序列；`KeyValuePair<K,V>` 和 `ValueTuple<...>` 使用这种格式。

### 可空包装

标量 `Nullable<T>`、引用类型 `Tuple`、`Lazy`、`Version`、`Uri`、`CultureInfo`、`TimeZoneInfo` 和 `Type` 在负载前写一个非空标记；`null` 是单字节 `255`。

## JSON

默认文本编码为 UTF-16；在根类型上使用 `[LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]` 选择 UTF-8。双方必须使用相同编码。

| C# 值 | JSON |
| --- | --- |
| `null` | `null` |
| 整数、浮点数、`decimal` | number |
| `bool` | `true` / `false` |
| `string`、`char` | string，按 JSON 规则转义 |
| `Guid`、`DateTime`、`DateTimeOffset`、`TimeSpan` 等 | string |
| `[LuminPackable]` 对象 | object；属性名就是 C# 成员名 |
| 数组和顺序集合 | `[1, 2, 3]` |
| 多维数组 | `{"Lengths":[2,2],"LowerBounds":[0,0],"Values":[1,2,3,4]}` |
| `KeyValuePair<K,V>` | `[key, value]` |
| 字典 | `[[key1, value1], [key2, value2]]`，因此键可以是任意支持的类型 |
| `Tuple`、`ValueTuple` | 按成员顺序排列的数组 |
| `Lazy<T>` | `{"Value": value}`；实例为 `null` 时写 `null` |
| Union | `{"$type": tag, "$value": {...}}` |
| 引用跟踪 | 第一次出现时写 `$id`，之后写 `{"$ref": id}` |

- 未知属性会被跳过；缺失的成员保持默认值。属性顺序不影响匹配。
- `NaN` 和无穷大不是合法的 JSON 数字：写入时抛出异常，读取时 `1e999` 这类超出范围的数字会被拒绝。
- 读取时执行 `MaxJsonDepth`（默认 1024）。处理外部来源的 JSON 请开启 `ValidateJsonSyntax`。
- `$type`、`$value`、`$id`、`$ref` 是保留名。
- 二进制多维数组格式只支持下界为 0。NativeAOT 无法创建下界非 0 的数组，因此在 NativeAOT 上读取这种 JSON 会抛出 `PlatformNotSupportedException`。
