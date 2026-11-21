# 🔧 Compile-time Options

[Chinese](options.cn.md) · [Back to README](../README.md)

LuminPack has no runtime options object. Settings are declared with `[LuminPackOption]` and compiled into the generated code, so a disabled feature costs nothing at run time.

## `[LuminPackOption]`

The attribute is valid on an assembly, a type, a field or a property. Inheritable settings use the enclosing value until you set them explicitly. Type contract switches apply at the declaration.

```csharp
using LuminPack.Attribute;
using LuminPack.Option;

[assembly: LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF16)]

[LuminPackable]
public partial class Message
{
    public string Title { get; set; } = "";

    [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8,
                     StringRecording = LuminPackStringRecording.Token)]
    public string Label { get; set; } = "";
}
```

| Property | Default | Meaning |
| --- | --- | --- |
| `GeneratorType` | `Default` | Selects `Default`, `Version` or `Custom` on an individual `[LuminPackable]` type. See [defining types](defining-types.md). |
| `IncludeDerivedTypes` | `false` | A marked class opts its source-derived classes into generation. See [Union roots](polymorphism.md#union-roots). |
| `StringEncoding` | `UTF16` | Binary string encoding, per member. For JSON, the encoding of the whole document, chosen by its root type. |
| `StringRecording` | `Length` | Fixed-width length headers, opt-in `VarInt` headers, or `Token` (terminated; `null` and `""` both read back as `""`). |
| `FramedGroupings` | `false` | Next assembly option for unambiguous binary `IGrouping<TKey, TElement>` records, including groups in an `ILookup<TKey, TElement>`. |
| `MaxJsonDepth` | `1024` | JSON nesting limit; zero or less disables it. |
| `ValidateJsonSyntax` | `false` | Strict JSON grammar checking. Turn it on for JSON from outside sources. |
| `StandardFormat` | `"G"` | A constant handed to explicit formatted and `Custom` kernels. It does not change built-in number or date formats. |
| `WideTag` | `false` | Wide numeric tags for the registered Union path in Next. The option does not inherit enclosing settings. |
| `SingleImplementation` | `false` | This class or interface promises one implementation everywhere. Next selects the write tier by generated case count. |
| `TrackReferences` | `false` | Track shared objects and cycles for the annotated class in binary and JSON. |
| `ReuseRectangularArraySlots` | `true` | Next reuses existing objects in binary rank-two arrays on .NET 8 and later. Set it to `false` directly on the ordinary object element class to replace the array. See [object matrix reuse](reuse.md#binary-object-matrix-reuse). |
| `StructLayout` | `Tagged` | Storage of unmanaged structs; see below. Only valid on assemblies and structs. |
| `UntrustedData` and limits | on, limits unbounded | Checked reads for untrusted input; declare limits for hostile input; see [Security](security.md). |
| `RequireCompleteBinaryInput` | `false` | Assembly only. Binary document reads that return a value reject trailing bytes. |

Binary data may mix encodings member by member, but the writer and reader must use the same declarations. JSON encoding, depth and syntax checking always apply to a complete document.

JSON property names use encoding-specific, generated [name lookups](json-name-lookup.md); hashes never replace exact name comparison.

## Complete binary input

Use `[assembly: LuminPackOption(RequireCompleteBinaryInput = true)]` to require one complete binary record in each `Deserialize<T>` or `DeserializeAsync<T>` call that returns a value. This applies to spans, sequences, reusable buffers and streams, including calls with an explicit operation profile. A successful decode followed by trailing bytes throws `InvalidDataException`. The `ref` overloads still return the bytes consumed by one record, and advancing readers still leave the cursor after that record.

## Binary grouping framing

Use `[assembly: LuminPackOption(FramedGroupings = true)]` in projects that need to distinguish a null grouping from a present grouping whose key begins with the byte `FF`. This includes null string keys under the default length profile and raw byte keys with the value `255`. Each present grouping starts with `00`, followed by the key and element sequence. A null grouping is one byte `FF`.

Configure the same option in the assemblies that generate both endpoints, including when a grouping is nested or part of a lookup. Existing binary data uses the default key-first layout if written without this option. The string profile continues to select the key's encoding, including `Token`'s shared representation for null and empty strings. JSON needs no option to preserve null-key groups.

## Union contract switches

Group type-level Union declarations in one attribute:

```csharp
[LuminPackable, LuminPackOption(WideTag = true, SingleImplementation = true)]
public abstract partial class MessageBase { }

[LuminPackable]
public sealed partial class MessageBody : MessageBase { public int Id; }
```

These switches apply only to the annotated class or interface. They are invalid on assemblies, structs, fields and properties, do not inherit from containing or base types, and cannot be selected through `ILuminPackOptions`. Declaring only these switches does not pin string settings, so explicit operation profiles still flow normally. A declared tag of 250 or more still selects wide numeric framing for explicitly registered cases.

`[LuminPackWideTag]` and `[LuminPackSingleImplementation]` remain compatible spellings; prefer the grouped options. A legacy marker remains enabled even when the corresponding option is explicitly false. `SingleImplementation` is a caller guarantee, not a request to remove Union framing or virtual dispatch; use it only when no other assembly can add an implementation. In Next, generated cases use hash framing and the write tier is selected by case count.

## Reference tracking

Set `[LuminPackOption(TrackReferences = true)]` directly on a class in `Default` or `Version` mode. The annotated class alone owns this contract and the attribute is its only configuration API. The normal string declarations and operation profiles still select the encoding. Using this option on an assembly, struct, interface, field or property reports `LuminPack110`. See [reference tracking](versioning.md#reference-tracking).

## Explicit profiles

Every entry point also accepts a stateless option profile as a type argument. The profile flows through all nested operations; members with their own `[LuminPackOption]` keep it.

```csharp
byte[] bytes = LuminPackSerializer.Serialize<Message, LuminPackUtf8LengthOptions>(message);

Message copy = null!;
LuminPackSerializer.Deserialize<Message, LuminPackUtf8LengthOptions>(bytes, ref copy);
```

Built-in profiles: `LuminPackDefaultOptions` (UTF-16, fixed length), `LuminPackUtf8LengthOptions`, `LuminPackUtf8TokenOptions`, `LuminPackUtf16LengthOptions`, `LuminPackUtf16TokenOptions`, `LuminPackUtf8VarIntOptions`, `LuminPackUtf16VarIntOptions`. You can define your own as a `struct` implementing `ILuminPackOptions`.

Use a closed composite profile to select the binary codec, numeric format, JSON depth and syntax policy. This example selects UTF-8 with variable-length headers and strict JSON.

```csharp
using TextOptions = LuminPack.Option.LuminPackOptions<
    LuminPack.Option.LuminPackUtf8VarIntStrings,
    LuminPack.Option.LuminPackGeneralFormat,
    LuminPack.Option.LuminPackDefaultDepth,
    LuminPack.Option.LuminPackStrictJson>;
```

A directly implemented profile forwards every member of `ILuminPackStringCodec` to its codec. The **Forward string operations to LuminPack…Strings** code fix adds the missing members and retains existing implementations. It selects the codec from constant encoding and recording properties, or offers the six built-in codecs when those values are computed. Keep the metadata properties consistent with the forwarded representation. A custom codec's record bound covers the complete framed record. Its reserved write returns the next cursor and preserves the writer's published prefix on failure. Its `Skip` method advances over one complete record under the same input and string-length limits as the corresponding read.

For a directly implemented profile, forward the skip operation to the codec that handles its reads. For example, a UTF-8 VarInt profile delegates as follows.

```csharp
public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue)
    => default(LuminPackUtf8VarIntStrings).Skip(ref reader, maxStringLength);
```

For a custom JSON profile, forward scalar reads and writes and the code-unit operations to the string codec. Forward token reads, grammar tracking and UTF-8 string validation to LuminPackStrictJson or LuminPackTrustedJson. Implement CheckJsonDepth by calling LuminPackJsonPolicy.CheckDepth(depth, MaxJsonDepth). The code fix can generate these forwards from the existing options. Keep metadata and the chosen policy consistent so registered formatters and generated calls agree.

A depth component can override the check or inherit the standard bound check from its Value property. To supply a dedicated check, implement both `Value` and `CheckJsonDepth(int depth)`. Forward the latter to `LuminPackJsonPolicy.CheckDepth(depth, Value)` for the standard limit semantics. A nonpositive limit permits unbounded nesting.

## Compact length headers

Select `VarInt` for collections of small strings when reducing bytes matters more than peak encoding speed:

```csharp
[LuminPackable]
public partial class Labels
{
    [LuminPackOption(StringRecording = LuminPackStringRecording.VarInt)]
    public List<string> Names { get; set; } = new();
}

// Or select the representation for the complete operation:
byte[] bytes = LuminPackSerializer.Serialize<List<string>, LuminPackUtf16VarIntOptions>(names);
List<string> copy = null!;
LuminPackSerializer.Deserialize<List<string>, LuminPackUtf16VarIntOptions>(bytes, ref copy);
```

The default remains `Length`: four header bytes for UTF-16 and eight for UTF-8. `VarInt` encodes each existing signed length header plus one as canonical unsigned base-128 (one to five bytes). Null maps to zero and empty maps to one. UTF-16 retains the raw UTF-16 or compact Latin-1 payload and its existing low-bit discriminator; UTF-8 retains both its byte and character counts, each encoded separately, including for null and empty strings. For example, UTF-16 `"a"` is `04 61`; UTF-8 `"a"` is `02 02 61`.

A `List<string>` of sixteen one-character ASCII strings takes 84 bytes with UTF-16 `Length` and 36 with UTF-16 `VarInt`, including its unchanged four-byte collection count. Variable headers add branches and length calculations, so they can reduce throughput. Both ends must select the same representation; these formats are not interchangeable or auto-detected. JSON and collection count headers are unaffected. `StringRecordLength<TOptions>()` returns a conservative header bound for `VarInt` (five or ten bytes), not an exact record size.

## String transcoding

`StringSerializer` transcodes text with the same profiles, without adding a header:

```csharp
OperationStatus status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(
    text.AsSpan(), utf8Buffer, out int charsRead, out int bytesWritten);
OperationStatus decoded = StringSerializer.Deserialize<LuminPackUtf8LengthOptions>(
    utf8Buffer.AsSpan(0, bytesWritten), charBuffer, out int bytesRead, out int charsWritten);
```

Check the returned status for a short destination or an unfinished input block. `replaceInvalidSequences` and `isFinalBlock` control replacement and chunking.

<a id="struct-layout"></a>
## 32/64-bit struct layouts

A struct without references is normally copied straight from memory. Its native layout can differ between 32-bit and 64-bit processes (padding, alignment). `StructLayout` picks how such structs are stored:

| Value | Storage |
| --- | --- |
| `Tagged` (default) | A block or value with one modeled layout uses headerless native bytes. A multi-model collection block writes the matching tag and copies its native storage after verifying local stride and offsets once per closed type. A reader with that layout copies the block. A reader with a different layout converts it. An unmodeled writer converts to the first model. Individual multi-model values omit the tag and use packed field order. |
| `Packed` | Fields in declaration order with no padding, identical on every platform. A struct proven to have no padding keeps the raw copy. |
| `Native` | The complete native storage, including padding. Both ends must have identical layouts. |

```csharp
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public partial struct Packet
{
    public byte Kind;
    public long Sequence;
}

// or for every ordinary unmanaged struct declared in this assembly:
[assembly: LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
```

- A struct's own setting overrides its assembly's. Referenced assemblies keep their own declarations.
- Packed storage propagates through enclosing structures, including framework pairs and value tuples. A struct explicitly declared `Native` keeps its native storage and stops this propagation from its fields.
- In Next, values that cannot carry a type-level `LuminPackOption` and have no LuminPack contract of their own use the consuming project's assembly option. This includes framework scalars, enums and value tuples in collections. The default is `Tagged`.
- On supported .NET 8+ targets, collections of eligible `Nullable<T>` use the underlying type's layout. `Native` stores the complete nullable stride, including padding. A contiguous `Tagged` payload writes one tag when a nonempty block has multiple models. Element-wise collections use headerless native records for proven single-layout scalars and the element's nullable formatter for other `Tagged` payloads. `Packed` uses the nullable marker and the packed payload of each present element.
- Older targets and unsupported nullable layouts use the element's nullable formatter. Both ends must select the same collection encoding.
- Field expansion includes private and ignored fields and auto-property backing fields, recursively; `LuminPackOrder` does not change it. `long` always takes 8 bytes.
- Layout conversions use SIMD where the layout, element count and runtime support it. The scalar path transfers the same field bytes on other layouts and targets. A .NET Standard consumer can use an installed Burst layout hook on eligible blocks.
- `Packed` reports a diagnostic for native integers, pointers, fixed buffers, inline arrays, runtime-sized vectors, `Custom` storage and layouts it cannot analyze.
- None of these modes converts byte order; LuminPack requires little-endian. JSON members are not affected.
- Raw padded structs can carry stale padding bytes. Use `Packed` or a field-oriented model for public protocols.
