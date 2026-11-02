# 📝 Version Tolerance & Reference Tracking

[Chinese](versioning.cn.md) · [Back to README](../README.md)

## Choosing a mode

The default `Object` mode identifies members by position. It is the fastest form, but you cannot remove, reorder or retype members without breaking existing data. Raw unmanaged structs can't add or remove fields compatibly either.

| Need | Mode |
| --- | --- |
| Stable model, best speed | `GeneratorType.Object` |
| Members added, removed, reordered or renamed over time | `GeneratorType.VersionTolerant` |
| Shared objects and cycles in the graph | `GeneratorType.CircleReference` |

## Version-tolerant objects

```csharp
[LuminPackable(GeneratorType.VersionTolerant)]
public partial class PlayerV1
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public long Removed { get; set; }
}

[LuminPackable(GeneratorType.VersionTolerant)]
public partial class PlayerV2
{
    public List<int>? Scores { get; set; }

    [LuminPackOldName("Name")]
    public string? DisplayName { get; set; }

    public int Id { get; set; }
}
```

These two models read each other's data in both directions.

Each model caches its complete record prefix (header, name/type metadata and member widths) and writes it in one copy, with the selected compiled binary profile stamped into the header. Records are written in one pass; a record of fixed-width members has a constant size and is written straight into the result array. A reader whose model matches compares the prefix once and reads the members in order; other readers map the members by name.

- Members are identified by their exact, case-sensitive names. `LuminPackOrder` is optional and only sets the write order.
- Unknown members are skipped by their recorded length. Missing members are set to `default`, also when reading into an existing object.
- `[LuminPackOldName]` keeps a renamed member readable. Binary writes keep the oldest name, so older readers still understand new data. After several renames, keep every old name in chronological order:

  ```csharp
  [LuminPackOldName("Name")]
  [LuminPackOldName("DisplayName")]
  public string? Label;
  ```

- Names and aliases must be unique across the whole member set, including inherited members. Do not reuse a removed member's name for a different meaning.
- JSON reads accept old names too; JSON writes use the current name.
- Nested models that evolve independently must also be `VersionTolerant`, including those inside arrays, collections and generic members. A nested `Object` model or raw struct keeps its own positional format.
- Keep member types, their namespaces and binary string options compatible. Matching names with incompatible type metadata are rejected; there is no automatic type conversion.

Reading into an existing object reuses it and its children like an ordinary binary read (see [Reuse](reuse.md)).

### Binary framing

`null` is the single byte `255`. A record starts with four bytes: marker `251`, format revision `2`, the binary string profile and the member count (0 to 249). The profile stores the string encoding in bit 1 and the recording mode in bit 0, and it must match the reader's profile.

A metadata block follows, one entry per member with its UTF-8 name and UTF-8 type contract, each prefixed by an unsigned base-128 length. Then comes one width byte per member: the exact width of a raw member below 255, or 255 for a payload that starts with its four-byte length. The payloads follow in the same order. Metadata is compared byte for byte, so hash collisions cannot alias members. Generated code caches the metadata per model and compares it directly against the input; it never decodes names into strings or uses reflection.

Within one write operation, only the first record of a model carries the metadata and width table. Later records of the same model use revision `3`: the same four-byte header followed by the 64-bit fingerprint of the table, then the payloads. Readers resolve a fingerprint only to a table read earlier in the same operation and reject any other. A record read alone is therefore always a full record.

Readers also accept revision `1`, written by earlier releases, which stores every payload length (LuminPack VarInt) before the payloads. Earlier releases cannot read revisions `2` and `3`.

## Reference tracking

```csharp
[LuminPackable(GeneratorType.CircleReference)]
public partial class Node
{
    [LuminPackOrder(0)] public Node? Parent { get; set; }
    [LuminPackOrder(1)] public Node[]? Children { get; set; }
}
```

- Every serialized member needs a `[LuminPackOrder]`. The numbers form a slot table of one width byte per slot; when adding or removing members, keep the existing numbers. A variable-size value records its length in front of the value, so a record is written in one pass. A reader skips trailing slots it does not know and leaves its own extra members at their defaults.
- Records written by earlier releases, which stored variable slot lengths in the table, are not readable.
- Only objects of `CircleReference` types are tracked. Wrap any other object you need to track in such a type.
- Records written into the same `LuminBufferWriter` share one reference table; read them back in order with one reader and one reader state (see [Getting started](getting-started.md#reusing-a-buffer)).
- JSON uses `$id` for the first occurrence of an object and `$ref` for later references. These names are reserved.
