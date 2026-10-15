---
name: luminpack
description: Use LuminPack binary and JSON serialization correctly. Covers packable models, attributes, generated serializer overloads, generation tiers, unions, version tolerance, custom formatters, mapping, compression, and reusable buffers.
---

# LuminPack usage

LuminPack targets Unity save data and network payloads, supporting .NET Standard 2.1 and .NET 8 through .NET 11. Source generators produce concrete formatter calls for supported types, including AOT environments.

This skill documents library usage. It does not prescribe a development workflow or modify repository maintenance instructions.

## Quick start

Annotate a supported class, struct, record, or interface with `[LuminPackable]`, then use the generated `LuminPackSerializer` facade:

```csharp
using LuminPack;
using LuminPack.Attribute;

[LuminPackable]
public class Person
{
    public int Age { get; set; }
    public string Name { get; set; } = "";
}

// Inside an application method:
// byte[] data = LuminPackSerializer.Serialize(person);
// Person result = LuminPackSerializer.Deserialize<Person>(data);
// string json = LuminPackSerializer.SerializeJson(person);
// Person jsonResult = LuminPackSerializer.DeserializeJson<Person>(json);
```

For repeated writes, reuse a `LuminBufferWriter`. It owns native memory and must be disposed. Buffer overloads use the writer's configured options and operation state.

## Serialization contracts

- Ordinary object members follow their declared or explicit order, including inherited members. Readers and writers must agree on that contract.
- Object member counts range from 0 through 249. The null object marker is 255; a null collection length is -1.
- Binary payloads use little-endian encoding. Unmanaged struct fast paths copy the runtime storage representation, including padding.
- Binary and JSON are separate formats. Use matching serialization and deserialization APIs.
- `VersionTolerant` and `CircleReference` require `[LuminPackOrder]` on every serialized member.
- Preserve existing member order, types, and protocol configuration when evolving ordinary object contracts. Use an appropriate version-tolerant contract when compatibility requirements demand it.
- Generated overload XML documentation identifies the concrete type, member contract, generation mode, buffer semantics, and CLR layout. Memory layout is distinct from wire offsets for variable-length members.

## Attributes and APIs

See [the attribute catalog](references/attributes.md) and [the API reference](references/api.md) for signatures and examples.

Common attributes include `LuminPackable`, `LuminPackOrder`, `LuminPackIgnore`, `LuminPackInclude`, `LuminPackableObject`, `LuminPackUnion`, `LuminPackWideTag`, `LuminPackCompress`, `LuminPackFixedLength`, serialization callbacks, and `LuminPackPoolRent`.

## Generation tiers

`Full` emits the broadest formatter set. `Medium`, `Light`, and `Minimal` reduce generated code according to reachability and optional-feature rules.

```xml
<PropertyGroup>
  <LuminPackGenerationMode>Light</LuminPackGenerationMode>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackGenerationMode" />
</ItemGroup>
```

Alternatively use `[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]`. Aggressive pruning can omit types reached only through reflection or external assemblies; select a tier that preserves those use cases.

## Custom formatters and registration

`GeneratorType.Custom` validates and registers handwritten static methods instead of emitting the type's formatter extensions. Keep binary methods paired, JSON methods paired, and supply size evaluation only with the required preceding tiers. See the API reference.

Manual `Register<T>` and union member registration use function pointers and require an unsafe context and `AllowUnsafeBlocks`. Registered tags must not collide with generated union members.

## Performance and correctness

- Reuse buffers in repeated operations and avoid boxing values on hot paths.
- A `LuminPackPoolRent` method supplies deserialization instances. The application owns the pool and its return lifecycle.
- `LuminPackInclude` supports nested private members through generated Local mirrors on .NET Standard and modern .NET. Mirrors must preserve inheritance and layout attributes.
- Annotate a custom-formatted nested member with `LuminPackableObject` when it must dispatch through its own formatter.
- Match UTF-8/UTF-16 and length/token recording options at both ends.
- JSON floating-point values must be finite. Invalid or out-of-range numeric input is rejected according to the reader's validation rules.
- Dispose owned buffers and preserve exclusive ownership during each reusable-buffer operation.
