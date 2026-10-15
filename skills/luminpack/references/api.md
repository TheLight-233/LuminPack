# LuminPack API reference

## Generated serializer facade

The source generator emits `LuminPack.LuminPackSerializer` for each consuming compilation. Concrete overloads bind to concrete formatters; generic convenience overloads use generated or registered dispatch.

XML documentation on each concrete overload identifies its type, generation mode, serialized members, order, CLR layout attributes, and buffer semantics. Type and member links use C# XML documentation references. Unmanaged storage layout, ordinary binary member order, and JSON named members are documented separately.

Representative signatures below use `T` to stand for the concrete type:

```csharp
// Binary serialization and deserialization.
byte[] Serialize(in T value, LuminPackSerializerOption? option = null);
void Serialize(in T value, LuminBufferWriter writerBuffer);
int Deserialize(ReadOnlySpan<byte> buffer, ref T value, LuminPackSerializerOption? options = null);
int Deserialize(ReadOnlySequence<byte> buffer, ref T value, LuminPackSerializerOption? options = default);
int Deserialize(LuminBufferWriter bufferWriter, ref T value);
T Deserialize<T>(ReadOnlySpan<byte> buffer, LuminPackSerializerOption? options = null);

// JSON serialization and deserialization.
string SerializeJson(T value, LuminPackSerializerOption? option = null);
void SerializeJson(T value, LuminBufferWriter writerBuffer);
int DeserializeJson(string buffer, ref T value, LuminPackSerializerOption? options = null);
int DeserializeJson(ReadOnlySpan<byte> buffer, ref T value, LuminPackSerializerOption? options = null);
int DeserializeJson(ReadOnlySpan<char> buffer, ref T value, LuminPackSerializerOption? options = null);
int DeserializeJson(LuminBufferWriter bufferWriter, ref T value);
```

The facade also exposes asynchronous stream operations, `IBufferWriter<byte>` output, and binary size evaluation through `Sizeof`. A deserialization overload returning an integer reports consumed bytes in the selected representation, including transcoding performed by character-input overloads.

## Options and reusable buffers

`LuminPackSerializerOption` is declared in `LuminPack.Option`. Presets include `Default`, `Utf8`, `Utf16`, `Token`, `Length`, and `Utf16WithLength`. String encoding and recording settings must agree at both ends. `MaxJsonDepth` defaults to 1024; non-positive values disable that depth limit.

`LuminBufferWriter` owns native memory. Dispose buffers owned by the application, or return rented buffers to their pool. Overloads accepting a reusable buffer use its own configuration and operation state. Preserve exclusive ownership throughout each operation.

## Manual registration

`Register<T>` accepts static function pointers and requires an unsafe context with `AllowUnsafeBlocks`. Supported tiers are:

| Arguments | Supported operations |
| --- | --- |
| `writeValue, readValue` | Binary writing and reading. |
| The binary pair plus `writeValueJson, readValueJson` | Binary and JSON. |
| Both pairs plus `calculateOffset` | Binary, JSON, and binary size evaluation. |

Binary callbacks have signatures `(ref LuminPackWriter, in T)` and `(ref LuminPackReader, ref T)`. JSON callbacks use the corresponding JSON reader and writer. Size evaluation uses `(ref LuminPackEvaluator, in T)`.

```csharp
private static void WriteLegacy(ref LuminPackWriter writer, in LegacyModel value)
{
    writer.WriteValue(value.Id);
    writer.WriteValue(value.Name);
}

private static void ReadLegacy(ref LuminPackReader reader, ref LegacyModel value)
{
    value = new LegacyModel();
    reader.ReadValue(ref value.Id);
    reader.ReadValue(ref value.Name);
}

// Invoke once in an unsafe initialization context:
// LuminPackSerializer.Register<LegacyModel>(&WriteLegacy, &ReadLegacy);
```

Duplicate registration raises `ArgumentException`. Registration keys use MethodTable addresses without retaining a strong `Type` reference.

### Cross-assembly union registration

A generated union root exposes `Register<TMember>(tag, &Write, &Read, &WriteJson, &ReadJson)`. All four pointers are required. Their union-root callback signatures differ from ordinary `Register<T>` callbacks: the value is passed by reference using the root type. Tags must agree with the written union header and must not collide with existing members.

For a root `MyLib.IFoo` and a member `MyApp.FooA`:

```csharp
private static void WriteFooA(ref LuminPackWriter writer, ref MyLib.IFoo value)
{
    writer.WriteUnionHeader(0);
    writer.WritePolymorphismValue(LuminPackMarshal.As<MyLib.IFoo, MyApp.FooA>(ref value));
}

private static void ReadFooA(ref LuminPackReader reader, ref MyLib.IFoo value)
{
    MyApp.FooA item = default!;
    reader.ReadPolymorphismValue(ref item);
    value = LuminPackMarshal.As<MyApp.FooA, MyLib.IFoo>(ref item!);
}

private static void WriteJsonFooA(ref LuminPackJsonWriter writer, ref MyLib.IFoo value)
{
    writer.WriteObjectStart();
    writer.WritePropertyName("$type");
    writer.WriteInt(0);
    writer.WritePropertyName("$value");
    writer.WriteValue(LuminPackMarshal.As<MyLib.IFoo, MyApp.FooA>(ref value)!);
    writer.WriteObjectEnd();
}

private static void ReadJsonFooA(ref LuminPackJsonReader reader, ref MyLib.IFoo value)
{
    MyApp.FooA item = default!;
    reader.ReadValue(ref item);
    value = LuminPackMarshal.As<MyApp.FooA, MyLib.IFoo>(ref item!);
}

// MyLib.IFoo.Register<MyApp.FooA>(0, &WriteFooA, &ReadFooA, &WriteJsonFooA, &ReadJsonFooA);
```

The union registration table belongs to the root partial type. Register in the same process before serialization. Size evaluation remains unsupported for a member registered through this four-pointer union API.

## Custom formatters

`[LuminPackable(GeneratorType.Custom)]` validates and automatically registers handwritten static methods. These methods must be public or internal and match one supported registration tier:

```csharp
public static void Serialize(ref LuminPackWriter writer, in MyVector value);
public static void Deserialize(ref LuminPackReader reader, ref MyVector value);

// Optional paired JSON methods.
public static void SerializeJson(ref LuminPackJsonWriter writer, in MyVector value);
public static void DeserializeJson(ref LuminPackJsonReader reader, ref MyVector value);

// Optional after the required binary and JSON tiers.
public static void CalculateOffset(ref LuminPackEvaluator evaluator, in MyVector value);
```

The generator reports invalid signatures through LuminPack100-LuminPack108. Generic custom types register their closed instances. Custom generation is not supported on union roots. Registration is skipped with a diagnostic when unsafe code or registration generation is disabled.

For a binary-only formatter, write and read the same members in the same order. For JSON, use JSON object boundaries and property names, and implement the matching reader token handling. Custom size evaluation must reflect exactly the binary payload produced by the writer.

## Low-level readers, writers, and marshal helpers

Use `LuminPack.Core` readers and writers within custom formatters and registration callbacks. Generated `WriteValue`, `ReadValue`, and `CalculateOffset` extensions are available through `LuminPack.Generated`.

- `LuminPackWriter` provides unmanaged value, array, span, string, null-marker, and union-header primitives. Explicit `ref int index` overloads support writing headers at reserved positions.
- `LuminPackReader` provides matching value, array, span, string, and header operations. `Dangerous` variants require the caller to establish their buffer and layout preconditions.
- `LuminPackMarshal`, in `LuminPack.Code`, provides references, reinterpretation, collection views, list/stack/queue storage access, MethodTable lookup, uninitialized allocation, and unaligned copying. Preserve documented layout and lifetime assumptions.

## Compression

```csharp
int Compress(LuminBufferWriter source, LuminBufferWriter destination);
byte[] Compress(ReadOnlySpan<byte> source);
int Compress(ReadOnlySpan<byte> source, Span<byte> destination);
int Decompress(ReadOnlySpan<byte> source, Span<byte> destination);
int Decompress(LuminBufferWriter source, LuminBufferWriter destination);
byte[] Decompress(ReadOnlySpan<byte> source);
```

Compression uses the LuminPack LZ frame format. Select matching compression and decompression operations.

## Cryptography

`LuminChaCha20Poly1305`, in `LuminPack.Cryptography`, provides synchronous and asynchronous authenticated encryption. The RFC 8439 path uses a 32-byte key, 12-byte nonce, and 16-byte authentication tag; the API also exposes its existing 16-byte key option. Ciphertext and plaintext lengths must match, and the tag destination must have sufficient capacity. Authentication failure is reported by the boolean decryption result.

## Format overview

Binary object contracts use member counts and ordered payloads; version-tolerant contracts add length framing, and circular-reference contracts add reference identifiers. Unmanaged storage copies include padding. Strings follow the selected UTF encoding and length/token recording mode. Collections carry a length, with -1 representing null. Union tags select the member formatter; tags requiring the wide representation use the dedicated wide-tag encoding.

JSON uses named object properties, arrays for ordered collections and tuples, nested arrays for multidimensional arrays, and key/value pair arrays for dictionaries. Union payloads use `$type` and `$value`; circular references use `$id` and `$ref`. Avoid those reserved names in ordinary application members.
