# 📝 Version Tolerance & Reference Tracking

[Chinese](versioning.cn.md) · [Back to README](../README.md)

## Choosing a mode

The default `Default` mode identifies members by position. Use it for stable models whose readers and writers agree on member order and type. Select `Version` when members may be added, removed, reordered or renamed. Either format supports shared objects and cycles when the concrete class declares `[LuminPackOption(TrackReferences = true)]`.

## Version-tolerant objects

```csharp
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public partial class PlayerV1
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public long Removed { get; set; }
}

[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public partial class PlayerV2
{
    public List<int>? Scores { get; set; }

    [LuminPackOldName("Name")]
    public string? DisplayName { get; set; }

    public int Id { get; set; }
}
```

These two models can read each other's data. The reader skips unknown members and resets missing members to `default`, including when it fills an existing object. `LuminPackOrder` is optional and determines write order.

- Names are case-sensitive. Diagnostic `LuminPack062` reports duplicate names and name-key collisions among current and old names, including inherited members. `LuminPack018` reports a model with more than 249 serialized members. Reserve the names of removed members for their original meaning.
- `[LuminPackOldName]` lets readers match a previous name. Binary writes use the oldest name key. List every previous name in chronological order after multiple renames.

  ```csharp
  [LuminPackOldName("Name")]
  [LuminPackOldName("DisplayName")]
  public string? Label;
  ```

- JSON readers accept the old names and JSON writers use the current name.
- Use `Version` for nested models that evolve independently, including elements of a collection and members of a generic model. Nested `Default` models and raw structs keep their own positional layout.
- Keep member types, their namespaces and binary string options compatible. For a known name key, the reader requires the expected type-contract hash and width. A mismatch is an error. To change a member's type, introduce a new name and handle the conversion in application code.

Existing objects and children follow the [reuse rules](reuse.md).

### Binary framing

An untracked null is the single byte `255`. Non-null records start with marker `251`. The high nibble of the next byte, `form`, selects the revision. Bits 0–2 give the string profile and bit 3 is reserved at zero. The reader requires the same profile as the writer. Tags 0–5 represent UTF-8 Length, UTF-8 Token, UTF-16 Length, UTF-16 Token, UTF-8 VarInt and UTF-16 VarInt, respectively.

| Revision | Record |
| --- | --- |
| 4, full | `251, form, count, u64 schema, u64 descriptors[count], payloads` |
| 5, short | `251, form, canonical unsigned base-128 ordinal, payloads` |

The schema is the 64-bit FNV-1a hash of the count byte and descriptor bytes in wire order. Each little-endian descriptor packs the FNV-1a-32 key of the UTF-8 member name in bits 0–31, the low 24 bits of the type contract's FNV-1a-32 hash in bits 32–55 and the width code in bits 56–63. Widths 0–250 give the exact payload length, for example for `DateTime`, `Guid`, `decimal`, enums, and structs with a proven fixed representation. Code 254 uses the record's string codec and its own framing. Code 255 prefixes the payload with a four-byte unsigned length. A string with a different member profile uses code 255 so another model can skip it. A fixed-width `Nullable<T>` uses a presence marker and a payload slot of the same width for null and present values. The writer fills the absent payload slot with zeros.

The reader checks a full record's descriptor count, widths and unique name keys. It compares the complete descriptor bytes before selecting a positional path or reusing a cached member map. A schema hash alone cannot establish that two tables match. The individual name and type fields are finite hashes, so independently defined contracts can still collide.

### Schema registration and scope

Each read or write operation keeps an ordered table of the complete schemas visible at the current position. A full record appends an entry. A short record uses its zero-based ordinal. The first record of a schema that is absent from the active table carries the full table. Subsequent records can use the ordinal.

When a reader or writer enters a length-prefixed member (width code 255), outer entries remain visible. Entries added inside the member disappear when it ends. A reader can skip an unknown member in one step, and its later ordinals still have the same meaning as the writer's. Two sibling collections may each contain a full record for the same schema if the enclosing scope has no entry for it. Read a short record with the state that read its preceding full record.

Records appended to a `LuminBufferWriter` share the schema table. After flushing a chunk, `ResetWrittenCount()` retains the table so the next chunk can use its ordinals. Read the chunks in order with the same reader state. Call `Reset()` before writing an independent record so its first occurrence of each schema carries a full table. See [reusing a buffer](getting-started.md#reusing-a-buffer).

### Reading persisted data

Version-tolerant readers also accept revision 1, whose field lengths are stored before the payloads. Revision 2 and 3 development records and the old reference-tracked slot format require a reader that supports those bytes. To convert a stored record, decode it with that reader and serialize the resulting value using the current format.

## Reference tracking

```csharp
[LuminPackable, LuminPackOption(TrackReferences = true)]
public partial class Node
{
    public Node? Parent { get; set; }
    public Node[]? Children { get; set; }
}
```

The declaration applies only to the annotated class. For polymorphic values, the concrete case's declaration controls tracking. Set `GeneratorType = GeneratorType.Version` in the same option when the class also needs schema evolution. `LuminPackOrder` only determines write order in either format.

Binary tracks the first occurrence with an explicit id followed by its positional or version-tolerant body. A back reference uses marker `250` and that id instead of a body. Records appended to the same `LuminBufferWriter` share reference state. Read them in order with the same reader state (see [reusing a buffer](getting-started.md#reusing-a-buffer)). A back reference can only resolve an object the reader has seen. When evolving a version-tolerant model, keep the first occurrence of a referenced object in a member the older reader will read. JSON uses `$id` on the first occurrence and `$ref` for later references. These property names are reserved.
