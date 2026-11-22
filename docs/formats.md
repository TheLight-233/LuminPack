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

| Record | Bytes |
| --- | --- |
| Full | `251, 0x40 + profile, byte count, u64 schema, u64 descriptors[count], payloads` |
| Short | `251, 0x50 + profile, canonical unsigned base-128 ordinal, payloads` |
| Null | `255` |

In a full record, each descriptor holds a name key, a type-contract hash and a width code. Codes 0–250 give the exact byte width, 254 uses the record's string framing, and 255 gives a payload prefixed by an unsigned four-byte length. A short record refers to an earlier descriptor table that is visible in its current scope. Readers also accept revision 1, which stores every payload length before the payloads. See [version tolerance](versioning.md#binary-framing).

### Reference-tracked object

| Record | Bytes |
| --- | --- |
| First occurrence | `byte memberCount, VarInt referenceId, positional or version-tolerant body` |
| Back reference | `250, VarInt referenceId` |
| Null | `255` |

The first marker gives the writer's member count (0–249). It is followed by the explicit reference id and a record of the selected format. A version-tolerant body can be a full or short record.

### String

Length mode, UTF-16: `(int header, payload)`. `-1` is `null`, `0` is `""`. A positive even header is the byte count of the UTF-16 code units that follow. When every character is at most U+00FF, a compact form is written instead: the odd header `(length << 1) | 1` followed by one Latin-1 byte per character. In both forms the character count is `header >> 1`, and readers accept both.

Length mode, UTF-8: `(int utf8ByteCount, int utf16Length, utf8Bytes)`. `utf16Length` may be `-1` when unknown.

Opt-in `VarInt` mode replaces each length field with canonical unsigned base-128 of `header + 1` (one to five bytes), preserving the payload. Zero is null; one is an empty length. UTF-16 keeps its Latin-1 discriminator before this mapping; UTF-8 writes both counts, using two zero headers for null and two one headers for empty. UTF-8 variable records require an exact nonnegative character count for a non-null string. Both endpoints must select `VarInt`; fixed length remains the default and collection counts stay fixed. Version-tolerant records use profile tags 4 for UTF-8 VarInt and 5 for UTF-16 VarInt.

Token mode writes a terminated string with no `null` marker; `null` and `""` both read back as `""`.

### Union

`(byte tag, value)` for tags 0 to 249, `(250, ushort tag, value)` for wider tags. `255` means `null`.

### Collection

`(int count, [values...])`. `-1` means `null`.

### Tuple

`(values...)`. A fixed-size sequence of non-null values; `KeyValuePair<K,V>` and `ValueTuple<...>` use it.

### Nullable wrappers

`Nullable<T>` scalars, reference `Tuple`, `Lazy`, `Version`, `Uri`, `CultureInfo`, `TimeZoneInfo` and `Type` write a non-null marker before their payload; `null` is the single byte `255`.

A binary `TimeZoneInfo` record with marker `0` carries the sender’s complete serialized snapshot. Marker `1` carries the system zone identifier as a string in the current profile. The Next writer selects marker `1` for `TimeZoneInfo.Utc` and for values whose exact runtime instance belongs to the system catalog. Custom zones preserve the full snapshot, including custom names and rules for a catalog ID. The receiver resolves a system identifier using its own rules. A missing identifier raises `TimeZoneNotFoundException`. Use a reader with marker `1` support to consume these records. JSON carries the complete snapshot.

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
| String-keyed framework dictionaries | `{"one":1,"two":2}`. Readers also accept `[["one",1],["two",2]]`. |
| Dictionaries with other key types | `[[key1, value1], [key2, value2]]` |
| `Tuple`, `ValueTuple` | array of members in order |
| `Lazy<T>` | `{"Value": value}`; a `null` instance is `null` |
| Class or interface Union | `{"$type": hash, "$value": {...}}` for a generated case. Registered cases use numeric tags. See [polymorphism](polymorphism.md#json). |
| Reference tracking | `$id` on the first occurrence, `{"$ref": id}` afterwards |

- A map's property names use the JSON string escaping rules, and duplicate keys follow its insertion or replacement behavior. An interface or read-only wrapper whose underlying map cannot be identified can still emit pairs, preserving keys such as `null` from a custom implementation. Priority queues and lists of key/value pairs keep pair framing.
- Unknown properties are skipped; missing members keep their default value. Property order does not matter.
- `NaN` and infinities are not valid JSON numbers: writing them throws, and out-of-range numbers such as `1e999` are rejected on read.
- Reading enforces `MaxJsonDepth` (1024 by default). Enable `ValidateJsonSyntax` for JSON from outside sources.
- `$type`, `$value`, `$id` and `$ref` are reserved names.
- The binary multidimensional format supports only zero lower bounds. NativeAOT cannot create arrays with non-zero lower bounds, so reading such JSON there throws `PlatformNotSupportedException`.
