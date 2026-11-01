# 🚀 Getting Started

[Chinese](getting-started.cn.md) · [Back to README](../README.md)

## Installation

### .NET

LuminPack supports `netstandard2.1`, `net8.0`, `net9.0`, `net10.0` and `net11.0`. Install the [`LuminPack`](https://www.nuget.org/packages/LuminPack) package; NuGet adds the matching `LuminPack.Generator` automatically.

```shell
dotnet add package LuminPack
```

```xml
<ItemGroup>
  <PackageReference Include="LuminPack" Version="2.0.0" />
</ItemGroup>
```

### Unity

See [Unity, IL2CPP and NativeAOT](unity.md).

### From source

```xml
<ItemGroup>
  <ProjectReference Include="path/to/LuminPack/src/LuminPack.Core/LuminPack.Core.csproj" />
  <ProjectReference Include="path/to/LuminPack/src/LuminPack.Generator/LuminPack.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## First type

```csharp
using LuminPack;
using LuminPack.Attribute;

[LuminPackable]
public partial class Person
{
    public int Age { get; set; }
    public string Name { get; set; } = "";
}
```

The generator emits a `LuminPackSerializer` class into your assembly with overloads for `Person`:

```csharp
var person = new Person { Age = 18, Name = "Light" };

// Binary
byte[] bytes = LuminPackSerializer.Serialize(person);
Person copy = null!;
LuminPackSerializer.Deserialize(bytes, ref copy);     // concrete overload, reuses copy when it exists
Person other = null!;
LuminPackSerializer.Deserialize(bytes, ref other);

// JSON
string json = LuminPackSerializer.SerializeJson(person);
Person fromJson = null!;
LuminPackSerializer.DeserializeJson(json, ref fromJson);
```

Concrete overloads (`Serialize(person)`, `Deserialize(bytes, ref copy)`) call the generated code for that type directly. Generic overloads (`Deserialize<Person>(bytes)`, `Serialize<T, TOptions>(value)`, or calls inside `Save<T>`) require an explicit or Custom formatter [registration](custom-formatters.md#runtime-registration), even for built-in and source-generated types. Missing registrations throw `LuminPackException`. The concrete profile overload `Serialize<TOptions>(person)` still calls the generated Root directly.

The XML remarks of concrete model overloads and built-in overloads with an execution root include a **Generated implementation** link to the binary or JSON `Root` method. Follow that link in your IDE to inspect the implementation without stepping through forwarding methods. Closed generic models, collections and compound values are supported when their formatter has a root. Raw unmanaged binary reads may call the runtime directly and therefore have no separate `RootRead`; Custom formatters keep their user-defined entry points. Generic type-selected fallback APIs have no single concrete root to link to. `Sizeof` describes evaluation rather than a read/write root.

Generated roots use four-space indentation and role-based local names such as `cursor` and `nameSnapshot1`. Cursor record groups label the fields they process with ordinary `// Fields:` and `// Field:` comments. Compiler-owned locals are renamed after their bindings are known; your own field names are preserved, including names beginning with underscores.

Read methods that take `ref` return the number of bytes (or characters) consumed.

## Entry points

| Input / output | Binary | JSON |
| --- | --- | --- |
| `byte[]` / `string` | `Serialize(value)` | `SerializeJson(value)` |
| `ReadOnlySpan<byte>`, `ReadOnlySequence<byte>` | `Deserialize(span, ref value)` | `DeserializeJson(span, ref value)`; also `ReadOnlySpan<char>` and `string` |
| `LuminBufferWriter` | `Serialize(value, buffer)`, `Deserialize(buffer, ref value)` | `SerializeJson(value, buffer)`, `DeserializeJson(buffer, ref value)` |
| `IBufferWriter<byte>` | `Serialize(value, bufferWriter)` | `SerializeJson(value, bufferWriter)` |
| `Stream` | `SerializeAsync(stream, value)`, `DeserializeAsync<T>(stream)` | |

Every entry also has a form with an option profile, for example `Serialize<LuminPackUtf8LengthOptions>(value)`; see [Options](options.md).

Streams stay owned by the caller and are not closed. Asynchronous reads buffer the complete input; framing consecutive messages is up to your protocol.

```csharp
await LuminPackSerializer.SerializeAsync(stream, person, cancellationToken);
Person result = await LuminPackSerializer.DeserializeAsync<Person>(stream, cancellationToken: cancellationToken);
```

<a id="reusing-a-buffer"></a>
## Reusing a buffer

Overloads that take a `LuminBufferWriter` append at its current cursor and keep everything written before, including reference tracking state. You own the buffer and decide when to reset it:

```csharp
using var output = new LuminPack.Utility.LuminBufferWriter(true);
LuminPackSerializer.Serialize(person, output);
int firstEnd = output.CurrentIndex;
LuminPackSerializer.Serialize(person, output);   // output.GetSpan() holds both records

output.ResetForReuse();                          // clear content and reference tables, keep capacity
LuminPackSerializer.Serialize(person, output);
```

| Method | Effect |
| --- | --- |
| `ResetForReuse()` | Clears content and both reference tables, keeps the pooled capacity. |
| `Reset()` | Clears content and both reference tables, and returns a non-empty pooled buffer. |
| `ResetCore()` | Clears content only; reference tables are kept. Copy/compress `AndReset` methods behave the same. |

- After a write throws, the written bytes and reference state remain; call `ResetForReuse()` before the next independent write.
- To restart writing while keeping the read state, call `output.ResetCore()` and `output.WriterState.ResetOperationState()`.
- Appended JSON documents get no separator; track their boundaries yourself.
- The `byte[]`, `string`, stream and `IBufferWriter<byte>` overloads manage their own buffers and state.

## Compression and encryption helpers

`LuminCompressor` (in `LuminPack.Utility`) compresses any byte span with the LuminPack LZ frame format, and `[LuminPackCompress]` compresses individual unmanaged array members:

```csharp
byte[] packed = LuminCompressor.Compress(bytes);
byte[] unpacked = LuminCompressor.Decompress(packed);
```

`LuminChaCha20Poly1305` (in `LuminPack.Cryptography`) provides RFC 8439 authenticated encryption (32-byte key, 12-byte nonce, 16-byte tag), synchronous and asynchronous. Decryption verifies the tag before writing plaintext. Key management and nonce uniqueness are your responsibility.

## Next steps

- [Defining types](defining-types.md): members, ordering, constructors, callbacks, supported types.
- [Polymorphism](polymorphism.md) and [version tolerance](versioning.md).
- [Reuse and zero allocation](reuse.md).
