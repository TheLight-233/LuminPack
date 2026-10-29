# LuminPack API reference

## Generated serializer

The generator emits `LuminPack.LuminPackSerializer` into each consuming assembly. For a type `T` it has concrete overloads, plus generic ones (`<T>`) and profile ones (`<TOptions>` / `<T, TOptions>`) for every shape below. `TOptions` is a stateless profile struct (see Options).

```csharp
// Binary
byte[] Serialize(in T value);
void   Serialize(in T value, LuminBufferWriter writerBuffer);       // appends at the cursor
void   Serialize(in T value, IBufferWriter<byte> bufferWriter);
int    Deserialize(ReadOnlySpan<byte> buffer, ref T value);        // returns bytes consumed
int    Deserialize(ReadOnlySequence<byte> buffer, ref T value);
int    Deserialize(LuminBufferWriter bufferWriter, ref T value);
T      Deserialize<T>(ReadOnlySpan<byte> buffer);                  // also ReadOnlySequence, LuminBufferWriter

// JSON
string SerializeJson(T value);
void   SerializeJson(T value, LuminBufferWriter writerBuffer);
void   SerializeJson(T value, IBufferWriter<byte> bufferWriter);
int    DeserializeJson(string buffer, ref T value);                // also ReadOnlySpan<char>, ReadOnlySpan<byte>, LuminBufferWriter
T      DeserializeJson<T>(string buffer);

// Streams (the caller owns and closes the stream; reads buffer the whole input)
ValueTask    SerializeAsync(Stream stream, T value, CancellationToken cancellationToken = default);
ValueTask<T> DeserializeAsync(Stream stream, T value, CancellationToken cancellationToken = default);
ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

// Exact binary size
int Sizeof(T value);

// Profile forms, for example:
byte[] Serialize<LuminPackUtf8LengthOptions>(in T value);
int    Deserialize<LuminPackUtf8LengthOptions>(ReadOnlySpan<byte> buffer, ref T value);
```

Concrete overloads call the type's generated code directly. Generic overloads use explicit/Custom registrations first, then a per-type entry created on first use. Under `Light`/`Minimal` only the overload shapes actually called are generated; others fall back to the generic forms.

## Options

```csharp
using LuminPack.Attribute;
using LuminPack.Option;

[assembly: LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]   // assembly default

[LuminPackable]
[LuminPackOption(MaxJsonDepth = 64, ValidateJsonSyntax = true)]              // type scope
public partial class Message
{
    [LuminPackOption(StringRecording = LuminPackStringRecording.Token)]      // member scope (binary)
    public string Label = "";
}
```

| Property | Default | Notes |
| --- | --- | --- |
| `StringEncoding` | `UTF16` | Per member in binary; per document (root type) in JSON. |
| `StringRecording` | `Length` | `Token`: terminated, `null` reads back as `""`. |
| `MaxJsonDepth` | 1024 | ≤ 0 disables. |
| `ValidateJsonSyntax` | false | Enable for external JSON. |
| `StandardFormat` | `"G"` | Constant for Custom/formatted kernels. |
| `StructLayout` | `Tagged` | Assembly or struct only: `Tagged`, `Packed`, `Native`. |
| `UntrustedData` | false | Bounded protocol; limits `MaxInputBytes`, `MaxDecompressedBytes`, `MaxAllocationBytes`, `MaxCollectionLength`, `MaxStringLength`, `MaxObjectCount`, `MaxDepth` (1..64), `MaxReadWork`. Both ends must enable it. Unions, reference tracking, version tolerance, Custom and some other shapes are rejected under it. |

Only explicitly set properties override the enclosing scope. Built-in profiles: `LuminPackDefaultOptions` (UTF-16 length), `LuminPackUtf8LengthOptions`, `LuminPackUtf8TokenOptions`, `LuminPackUtf16LengthOptions`, `LuminPackUtf16TokenOptions`. A custom profile is a `struct` implementing `ILuminPackOptions`.

`StringSerializer.Serialize<TOptions>(ReadOnlySpan<char>, Span<byte>, out int charsRead, out int bytesWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)` and the matching `Deserialize<TOptions>` transcode text without headers and return `OperationStatus`.

## Buffers

```csharp
using var output = new LuminBufferWriter(true);     // LuminPack.Utility; pooled managed storage, must be disposed
LuminPackSerializer.Serialize(a, output);
LuminPackSerializer.Serialize(b, output);           // appended after a, shares reference state
ReadOnlySpan<byte> written = output.GetSpan();
output.ResetForReuse();                             // clear content + reference tables, keep capacity
```

- `Reset()`: also returns non-empty pooled storage. `ResetCore()`: content only, keeps reference tables.
- After a failed write, reset before the next independent write.
- Appended JSON documents have no separator.

## Registration

Requires `AllowUnsafeBlocks`. Generated only when unsafe code is enabled; disable with `[assembly: LuminPackGeneratorOptions(mode, LuminPackRegisterMode.Disabled)]`.

```csharp
static void Write(ref LuminPackWriter writer, in Legacy value) { writer.WriteValue(value.Id); }
static void Read(ref LuminPackReader reader, ref Legacy value) { value = new Legacy(); reader.ReadValue(ref value.Id); }

unsafe { LuminPackSerializer.Register<Legacy>(&Write, &Read); }
```

| Overload | Methods |
| --- | --- |
| `Register<T>(write, read)` | binary |
| `Register<T>(write, read, writeJson, readJson)` | + JSON |
| `Register<T>(write, read, writeJson, readJson, calculateOffset)` | + size |
| `Register<T, TOptions>(...)` | same, for one closed profile |

Signatures: `(ref LuminPackWriter, in T)`, `(ref LuminPackReader, ref T)`, `(ref LuminPackJsonWriter, in T)`, `(ref LuminPackJsonReader, ref T)`, `(ref LuminPackEvaluator, in T)`. Duplicate registration throws `ArgumentException`. Registries store method pointers keyed by method table, so they don't keep plugin assemblies alive; keep registered code loaded while it can run.

### Union cases the generator cannot see

The root's generated `Register<TCase>(ushort tag, write, read, writeJson, readJson)` takes methods written against the **root** type:

```csharp
static void Write(ref LuminPackWriter writer, ref Lib.IShape value)
{
    writer.WriteUnionHeader(10);                                  // same tag as Register
    writer.WritePolymorphismValue(LuminPackMarshal.As<Lib.IShape, Hexagon>(ref value));
}
static void Read(ref LuminPackReader reader, ref Lib.IShape value)
{
    Hexagon item = default!;
    reader.ReadPolymorphismValue(ref item);
    value = LuminPackMarshal.As<Hexagon, Lib.IShape>(ref item!);
}
// WriteJson writes {"$type":10,"$value":...}; ReadJson reads the value.

unsafe { Lib.IShape.Register<Hexagon>(10, &Write, &Read, &WriteJson, &ReadJson); }
```

All four methods are required; tags must not collide with generated cases (`WriteWideUnionHeader` for wide tags); `Sizeof` throws for registered cases. Prefer `[assembly: LuminPackable(typeof(Lib.IShape))]` with local `partial` cases, which needs no registration.

## Custom formatters

```csharp
[LuminPackable(GeneratorType.Custom)]
public sealed class MyVector
{
    public float X, Y, Z;
    public static void Serialize(ref LuminPackWriter writer, in MyVector value)
    { writer.WriteValue(value.X); writer.WriteValue(value.Y); writer.WriteValue(value.Z); }
    public static void Deserialize(ref LuminPackReader reader, ref MyVector value)
    { value ??= new MyVector(); reader.ReadValue(ref value.X); reader.ReadValue(ref value.Y); reader.ReadValue(ref value.Z); }
    // Optional pair: SerializeJson(ref LuminPackJsonWriter, in T) + DeserializeJson(ref LuminPackJsonReader, ref T)
    // Optional, requires JSON: CalculateOffset(ref LuminPackEvaluator, in T)
}
```

The generator validates the methods (LuminPack100–108) and registers them automatically. Methods must be `static` and `public`/`internal`. Default JSON registration is UTF-16; UTF-8 documents need a closed-profile registration. Mark members of a Custom type with `[LuminPackableObject]`.

## Low-level API (`LuminPack.Core`)

- `LuminPackWriter`: `WriteValue(...)`, `Write<TOptions>(string)`, unmanaged values/spans, null markers, object and Union headers.
- `LuminPackReader`: `ReadValue(ref ...)`, `Read<TOptions>(ref string)`, spans, headers, `EnsureReadable(count)`.
- `LuminPackJsonWriter` / `LuminPackJsonReader`: tokens, property names, values.
- `LuminPackMarshal` (`LuminPack.Code`): `As<TFrom,TTo>`, collection storage access, `CopyBlockUnaligned`.

Readers and writers advance a cursor (`ref byte`, or `byte*` on netstandard2.1). On netstandard2.1, memory you wrap in a reader/writer yourself must stay pinned.

## Helpers

- `LuminCompressor` (`LuminPack.Utility`): `Compress(ReadOnlySpan<byte>)`, `Decompress(ReadOnlySpan<byte>)`, span and `LuminBufferWriter` overloads; LuminPack LZ frames.
- `LuminChaCha20Poly1305` (`LuminPack.Cryptography`): RFC 8439 AEAD (32-byte key, 12-byte nonce, 16-byte tag), sync and async; decryption verifies the tag first and reports failure with a `bool`.

## Formats

- Binary object: `(byte memberCount 0..249, values...)`, `255` = null. Collection: `(int count, values...)`, `-1` = null. Union: `(byte tag, value)` or `(250, ushort tag, value)`, `255` = null.
- Version-tolerant: `(251, 1, profile, count, metadata..., lengths..., payloads...)`. Reference tracking: slot lengths + reference id; `250` refers back.
- UTF-16 strings: `(int header, payload)`; `-1` null, `0` empty, odd header = compact Latin-1. UTF-8: `(int byteCount, int utf16Length, bytes)`.
- JSON: objects by member name; arrays for sequences and tuples; dictionaries as `[[k, v], ...]`; Unions `{"$type":tag,"$value":{...}}`; references `$id` / `$ref`; multidimensional arrays `{"Lengths":[..],"LowerBounds":[..],"Values":[..]}`.
