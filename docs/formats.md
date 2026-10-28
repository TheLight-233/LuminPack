# 📐 Wire Formats

[Chinese](formats.cn.md) · [Back to README](../README.md)

Binary and JSON are two separate protocols. Never pass `Serialize` output to `DeserializeJson`, or the reverse.

## Binary

All values are little-endian.

### Unmanaged values

A struct without references is stored as its raw memory, padding included, unless its `StructLayout` option says otherwise (see [32/64-bit struct layouts](options.md#struct-layout)).

### Object

`(byte memberCount, [values...])`

The member count is 0 to 249. `255` means `null`. Values follow in member order.

### Version-tolerant object

`(251, 1, byte profile, byte memberCount, [UTF-8 name/type metadata...], [VarInt payload lengths...], [payloads...])`

`255` means `null`. See [Version tolerance](versioning.md#binary-framing).

### Reference-tracked object

`(byte memberCount, [VarInt slot lengths...], VarInt referenceId, [values...])`
`(250, VarInt referenceId)`

A new object writes its slot length table and then its reference id. `250` refers back to an object already read.

### String

Length mode, UTF-16: `(int header, payload)`. `-1` is `null`, `0` is `""`. A positive even header is the byte count of the UTF-16 code units that follow. When every character is at most U+00FF, a compact form is written instead: the odd header `(length << 1) | 1` followed by one Latin-1 byte per character. In both forms the character count is `header >> 1`, and readers accept both.

Length mode, UTF-8: `(int utf8ByteCount, int utf16Length, utf8Bytes)`. `utf16Length` may be `-1` when unknown.

Token mode writes a terminated string with no `null` marker; `null` and `""` both read back as `""`.

### Union

`(byte tag, value)` for tags 0 to 249, `(250, ushort tag, value)` for wider tags. `255` means `null`.

### Collection

`(int count, [values...])`. `-1` means `null`.

### Tuple

`(values...)`. A fixed-size sequence of non-null values; `KeyValuePair<K,V>` and `ValueTuple<...>` use it.

### Nullable wrappers

`Nullable<T>` scalars, reference `Tuple`, `Lazy`, `Version`, `Uri`, `CultureInfo`, `TimeZoneInfo` and `Type` write a non-null marker before their payload; `null` is the single byte `255`.

## JSON

The default text encoding is UTF-16; select UTF-8 with `[LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]` on the root type. Both ends must use the same encoding.

| C# value | JSON |
| --- | --- |
| `null` | `null` |
| Integers, floating point, `decimal` | number |
| `bool` | `true` / `false` |
| `string`, `char` | string, escaped by JSON rules |
| `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan` and similar | string |
| `[LuminPackable]` object | object; property names are the C# member names |
| Arrays and sequential collections | `[1, 2, 3]` |
| Multidimensional arrays | `{"Lengths":[2,2],"LowerBounds":[0,0],"Values":[1,2,3,4]}` |
| `KeyValuePair<K,V>` | `[key, value]` |
| Dictionaries | `[[key1, value1], [key2, value2]]`, so keys can be any supported type |
| `Tuple`, `ValueTuple` | array of members in order |
| `Lazy<T>` | `{"Value": value}`; a `null` instance is `null` |
| Union | `{"$type": tag, "$value": {...}}` |
| Reference tracking | `$id` on the first occurrence, `{"$ref": id}` afterwards |

- Unknown properties are skipped; missing members keep their default value. Property order does not matter.
- `NaN` and infinities are not valid JSON numbers: writing them throws, and out-of-range numbers such as `1e999` are rejected on read.
- Reading enforces `MaxJsonDepth` (1024 by default). Enable `ValidateJsonSyntax` for JSON from outside sources.
- `$type`, `$value`, `$id` and `$ref` are reserved names.
- The binary multidimensional format supports only zero lower bounds. NativeAOT cannot create arrays with non-zero lower bounds, so reading such JSON there throws `PlatformNotSupportedException`.
