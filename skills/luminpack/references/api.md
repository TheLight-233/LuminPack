# LuminPack API reference

## Generated serializer

The generator emits `LuminPack.LuminPackSerializer` into each consuming assembly. Modern compilers bind generic signatures (`<T>` and `<T, TOptions>`) and intercept closed calls into existing concrete implementations; inference keeps ordinary `Serialize(value)` usage. Explicit profiles use both type arguments. Older hosts use build-time IL substitution into the same concrete entries when the package or Unity postprocessor is installed. `TOptions` is a stateless profile struct (see Options).

Generated XML documents these directly callable facade APIs. Internal roots, state forwarders, dispatch slots and layout helpers omit XML; this does not remove their methods or change serialization behavior.

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

// Profile calls, for example:
byte[] bytes = LuminPackSerializer.Serialize<Message, LuminPackUtf8LengthOptions>(message);
int consumed = LuminPackSerializer.Deserialize<Message, LuminPackUtf8LengthOptions>(bytes, ref message);
```

Intercepted calls and legacy concrete overloads call the type's generated code directly. With Next, generic fallback uses explicit registrations first, then automatically registered built-in, Custom and emitted closed-type operations. First use of the facade initializes registration. The original generator uses explicit and Custom registrations for generic fallback. Generation automatically retains declared model graphs and used convenience families. With both compiler and IL integration disabled, explicit generic payload calls use registered fallback; use concrete `Deserialize(bytes, ref value)` for direct roots. See [static interception](../../../docs/generation-modes.md#static-interception) for wrapper and compiler boundaries.

Binary and JSON `ref` overloads fill compatible existing storage. JSON reuses object graphs, lists, exact same-length arrays, dictionary values matched by key, mutable array slices, exact Union cases and tracked identities; unchanged decoded strings retain their references. JSON `null` clears a reference and omitted members reset to `default` on an existing object. Reuse is not transactional. Immutable collection formatters retain their existing construction behavior.

For ordinary types marked with `[LuminPackPoolRent]`, even the APIs without `ref` (`Deserialize<T>` and `DeserializeJson<T>`) rent an instance and read through its existing graph using reuse. An existing non-null `ref` target is reused without renting again.

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
| `UntrustedData` | true | Checked binary reads on the ordinary wire format (no envelope; only the reader changes), for objects, version tolerance, reference tracking and Unions. `false` restores trusted reads. Limits are unbounded unless declared: `MaxInputBytes`, `MaxCollectionLength`, `MaxStringLength`, `MaxDepth` (≥ 1, recursive values and JSON depth); `UntrustedData = true` or any declared limit also makes JSON strict. `MaxDecompressedBytes`, `MaxAllocationBytes`, `MaxObjectCount`, `MaxReadWork` are obsolete. Input-selected types (`object`, `System.Type`) fail closed. |

Only explicitly set properties override the enclosing scope. Built-in profiles: `LuminPackDefaultOptions` (UTF-16 length), `LuminPackUtf8LengthOptions`, `LuminPackUtf8TokenOptions`, `LuminPackUtf16LengthOptions`, `LuminPackUtf16TokenOptions`. A custom profile is a `struct` implementing `ILuminPackOptions`.

`StringSerializer.Serialize<TOptions>(ReadOnlySpan<char>, Span<byte>, out int charsRead, out int bytesWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)` and the matching `Deserialize<TOptions>` transcode text without headers and return `OperationStatus`.

## Buffers

```csharp
using var output = new LuminBufferWriter(true);     // LuminPack.Utility; pooled managed storage, must be disposed
LuminPackSerializer.Serialize(a, output);
LuminPackSerializer.Serialize(b, output);           // appended after a, shares reference state
ReadOnlySpan<byte> written = output.GetSpan();
output.Reset();                                     // reset count and both operation states, keep capacity
```

- `Reset()` resets the written count and live cursor and clears the reader and writer operation states. The array rental stays with the writer.
- `ResetWrittenCount()` resets the written count and live cursor while preserving the operation states and storage.
- `ResetAndRelease()` also returns the current array rental and leaves the writer usable. Dispose a directly constructed writer or call `LuminBufferWriterPool.Return(writer)` when finished with a rented writer.
- After a failed write, call `Reset()` before the next independent write.
- Appended JSON documents have no separator.

## Registration

Accepts typed delegates independently of `AllowUnsafeBlocks`; disable with `[assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]`.

```csharp
static void Write(ref LuminPackWriter writer, in Legacy value) { writer.Write(value.Id); }
static void Read(ref LuminPackReader reader, ref Legacy value) { value = new Legacy(); reader.Read(ref value.Id); }

LuminPackSerializer.Register<Legacy>(Write, Read);
```

| Overload | Methods |
| --- | --- |
| `Register<T>(write, read)` | binary |
| `Register<T>(write, read, writeJson, readJson)` | + JSON |
| `Register<T, TOptions>(...)` | same, for one closed profile |

Signatures: `(ref LuminPackWriter, in T)`, `(ref LuminPackReader, ref T)`, `(ref LuminPackJsonWriter, in T)`, `(ref LuminPackJsonReader, ref T)`. Duplicate registration throws `ArgumentException`. Conditional weak tables own delegates for their payload type's lifetime, with weak MethodTable indexes. Collectible payload assemblies can unload. Delegate targets remain alive while registered; registering plugin code for a noncollectible payload keeps that code alive.

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

Lib.IShape.Register<Hexagon>(10, Write, Read, WriteJson, ReadJson);
```

All four methods are required; tags must not collide with generated cases (`WriteWideUnionHeader` for wide tags). Prefer `[assembly: LuminPackable(typeof(Lib.IShape))]` with local `partial` cases, which needs no registration.

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
}
```

The generator validates the methods (LuminPack100–108) and registers them automatically. Methods must be `static` and `public`/`internal`. Default JSON registration is UTF-16; UTF-8 documents need a closed-profile registration. Mark members of a Custom type with `[LuminPackableObject]`.

## Low-level API (`LuminPack.Core`)

- `LuminPackWriter`: `WriteValue(...)`, `Write<TOptions>(string)`, unmanaged values/spans, null markers, object and Union headers.
- `LuminPackReader`: `ReadValue(ref ...)`, `Read<TOptions>(ref string)`, spans, headers, `EnsureReadable(count)`.
- `LuminPackJsonWriter` / `LuminPackJsonReader`: tokens, property names, values.
- `LuminPackMarshal` (`LuminPack.Code`): `As<TFrom,TTo>`, collection storage access, `CopyBlockUnaligned`.

Readers and writers advance a managed cursor (`ref byte` on modern .NET and a one-element `Span<byte>` on netstandard2.1). Use `MemoryMarshal.GetReference(state.Cursor)` to access the netstandard2.1 cursor after checking the complete range. `LuminBufferWriter` pins its own pooled array for the rental to preserve alignment. For raw span storage use `LuminPackMarshal.GetUnderlyingReference` and pass it by reference to block operations.

## Helpers

- `LuminCompressor` (`LuminPack.Utility`): `Compress(ReadOnlySpan<byte>)`, `Decompress(ReadOnlySpan<byte>)`, span and `LuminBufferWriter` overloads; `LUM2` LZ frames with short length bytes and escaped canonical base-128 remainders. Size span destinations with `GetMaxCompressedSize` (compression) or `GetDecompressedSize` (decompression). The latter validates high-ratio frame structure before allocation; ordinary sequence validation remains in `Decompress`. See [compression](../../../docs/compression.md).

## Formats

- Binary object: `(byte memberCount 0..249, values...)`, `255` = null. Collection: `(int count, values...)`, `-1` = null. Union: `(byte tag, value)` or `(250, ushort tag, value)`, `255` = null.
- Version-tolerant: `(251, 2, profile, count, metadata..., widths..., payloads...)`; later records of the same model in one write operation are `(251, 3, profile, count, u64 fingerprint, payloads...)`. Reference tracking: `(slotCount, width byte per slot, VarInt id, values...)`, width `255` means an `int` length precedes the value; `250` refers back.
- UTF-16 strings: `(int header, payload)`; `-1` null, `0` empty, odd header = compact Latin-1. UTF-8: `(int byteCount, int utf16Length, bytes)`.
- JSON: objects by member name; arrays for sequences and tuples; framework string-key dictionaries as objects (readers also accept `[[k, v], ...]`), other key types and unrecognized dictionary implementations as pairs; Unions `{"$type":tag,"$value":{...}}`; references `$id` / `$ref`; multidimensional arrays `{"Lengths":[..],"LowerBounds":[..],"Values":[..]}`.
