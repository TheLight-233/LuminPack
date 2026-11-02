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

`(251, 2, byte profile, byte memberCount, [UTF-8 name/type metadata...], [byte widths...], [payloads...])`

A width below 255 is a raw member's exact width; 255 marks a payload preceded by its `int` length. Readers also accept revision `1`, which stores every payload length before the payloads.

`(251, 3, byte profile, byte memberCount, ulong fingerprint, [payloads...])`

After the first full record of a model in one write operation, later records of that model are short: the 64-bit FNV-1a fingerprint of the metadata and width table replaces them. A reader resolves the fingerprint to a table read earlier in the same operation.

`255` means `null`. See [Version tolerance](versioning.md#binary-framing).

### Reference-tracked object

`(byte slotCount, [byte slot widths...], VarInt referenceId, [values...])`
`(250, VarInt referenceId)`

A new object writes its slot table and then its reference id. A slot width is a raw member's width, `0` for an empty slot, or `255` for a value preceded by its `int` length (raw values of 255 bytes or more included). `250` refers back to an object already read, and `255` is `null`. The slot count is below 250.

### String

Length mode, UTF-16: `(int header, payload)`. `-1` is `null`, `0` is `""`. A positive even header is the byte count of the UTF-16 code units that follow. When every character is at most U+00FF, a compact form is written instead: the odd header `(length << 1) | 1` followed by one Latin-1 byte per character. In both forms the character count is `header >> 1`, and readers accept both.

Length mode, UTF-8: `(int utf8ByteCount, int utf16Length, utf8Bytes)`. `utf16Length` may be `-1` when unknown.

Opt-in `VarInt` mode replaces each length field with canonical unsigned base-128 of `header + 1` (one to five bytes), preserving the payload. Zero is null; one is an empty length. UTF-16 keeps its Latin-1 discriminator before this mapping; UTF-8 writes both counts, using two zero headers for null and two one headers for empty. UTF-8 variable records require an exact nonnegative character count for a non-null string. Both endpoints must select `VarInt`; fixed length remains the default and collection counts stay fixed. Named version records use distinct profile tags 4 (UTF-8 VarInt) and 5 (UTF-16 VarInt), preserving the four existing tags.

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
