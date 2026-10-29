# 🔧 Compile-time Options

[Chinese](options.cn.md) · [Back to README](../README.md)

LuminPack has no runtime options object. Settings are declared with `[LuminPackOption]` and compiled into the generated code, so a disabled feature costs nothing at run time.

## `[LuminPackOption]`

The attribute is valid on an assembly, a type, a field or a property. Only the properties you set override the enclosing scope; the rest are inherited.

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
| `StringEncoding` | `UTF16` | Binary string encoding, per member. For JSON, the encoding of the whole document, chosen by its root type. |
| `StringRecording` | `Length` | Fixed-width length headers, opt-in `VarInt` headers, or `Token` (terminated; `null` and `""` both read back as `""`). |
| `MaxJsonDepth` | `1024` | JSON nesting limit; zero or less disables it. |
| `ValidateJsonSyntax` | `false` | Strict JSON grammar checking. Turn it on for JSON from outside sources. |
| `StandardFormat` | `"G"` | A constant handed to explicit formatted and `Custom` kernels. It does not change built-in number or date formats. |
| `WideTag` | `false` | Wide Union reader support on this class or interface; does not inherit enclosing options. |
| `SingleImplementation` | `false` | This class or interface promises one implementation everywhere; retains existing Union framing and virtual dispatch. |
| `StructLayout` | `Tagged` | Storage of unmanaged structs; see below. Only valid on assemblies and structs. |
| `UntrustedData` and limits | off | Bounded protocol for hostile input; see [Security](security.md). |

Binary data may mix encodings member by member, but the writer and reader must use the same declarations. JSON encoding, depth and syntax checking always apply to a complete document.

## Union contract switches

Group type-level Union declarations in one attribute:

```csharp
[LuminPackable, LuminPackOption(WideTag = true, SingleImplementation = true)]
public abstract partial class MessageBase { }

[LuminPackable]
public sealed partial class MessageBody : MessageBase { public int Id; }
```

These switches apply only to the annotated class or interface. They are invalid on assemblies, structs, fields and properties, do not inherit from containing or base types, and cannot be selected through `ILuminPackOptions`. Declaring only these switches does not pin string settings, so explicit operation profiles still flow normally. A tag of 250 or more still requires wide framing automatically.

`[LuminPackWideTag]` and `[LuminPackSingleImplementation]` remain compatible spellings; prefer the grouped options. A legacy marker remains enabled even when the corresponding option is explicitly false. `SingleImplementation` is a caller guarantee, not a request to remove Union framing or virtual dispatch; use it only when no other assembly can add an implementation. The current generator retains those mechanisms.

## Explicit profiles

Every entry point also accepts a stateless option profile as a type argument. The profile flows through all nested operations; members with their own `[LuminPackOption]` keep it.

```csharp
byte[] bytes = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(message);

Message copy = null!;
LuminPackSerializer.Deserialize<LuminPackUtf8LengthOptions>(bytes, ref copy);
```

Built-in profiles: `LuminPackDefaultOptions` (UTF-16, fixed length), `LuminPackUtf8LengthOptions`, `LuminPackUtf8TokenOptions`, `LuminPackUtf16LengthOptions`, `LuminPackUtf16TokenOptions`, `LuminPackUtf8VarIntOptions`, `LuminPackUtf16VarIntOptions`. You can define your own as a `struct` implementing `ILuminPackOptions`.

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
List<string> copy = LuminPackSerializer.Deserialize<List<string>, LuminPackUtf16VarIntOptions>(bytes);
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
| `Tagged` (default) | When every modeled platform has the same layout, the native bytes are copied with no tag. When layouts differ, a contiguous collection writes one layout tag and its raw elements; a reader with the same layout copies them, another layout converts them. Single values are stored field by field. |
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
- Field expansion includes private and ignored fields and auto-property backing fields, recursively; `LuminPackOrder` does not change it. `long` always takes 8 bytes.
- Collections of tagged or packed structs are converted with SIMD acceleration on .NET 8+; other platforms, including IL2CPP, produce the same bytes field by field.
- `Packed` reports a diagnostic for native integers, pointers, fixed buffers, inline arrays, runtime-sized vectors, `Custom` storage and layouts it cannot analyze.
- None of these modes converts byte order; LuminPack requires little-endian. JSON members are not affected.
- Raw padded structs can carry stale padding bytes. Use `Packed` or a field-oriented model for public protocols.
