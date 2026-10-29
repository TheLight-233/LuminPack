---
name: luminpack
description: Write correct code that uses the LuminPack binary and JSON serializer for .NET and Unity. Covers [LuminPackable] models and attributes, the generated LuminPackSerializer API, ref reuse, Unions, version tolerance, reference tracking, compile-time options and profiles, struct layouts, generation modes, Custom formatters and registration.
---

# Using LuminPack

LuminPack generates a dedicated binary and JSON formatter for every type at compile time (incremental source generator, no reflection). It targets `netstandard2.1` (Unity Mono/IL2CPP) and .NET 8 to .NET 11, and works under NativeAOT.

This skill is about using the library. It does not describe how to maintain the LuminPack repository.

## Quick start

```csharp
using LuminPack;
using LuminPack.Attribute;

[LuminPackable]
public partial class Person
{
    public int Age { get; set; }
    public string Name { get; set; } = "";
}

byte[] bytes = LuminPackSerializer.Serialize(person);
Person copy = null!;
LuminPackSerializer.Deserialize(bytes, ref copy);          // reuses copy if it is not null
Person other = LuminPackSerializer.Deserialize<Person>(bytes);

string json = LuminPackSerializer.SerializeJson(person);
Person fromJson = LuminPackSerializer.DeserializeJson<Person>(json);
int size = LuminPackSerializer.Sizeof(person);
```

`LuminPackSerializer` is generated into the consuming assembly (namespace `LuminPack`). Install with `dotnet add package LuminPack`; the generator package comes with it.

## Rules that matter

- Declare models `partial`. Union roots, their cases and every containing type **must** be `partial` (LuminPack035).
- The default `Object` mode is positional: members in declaration order (or `[LuminPackOrder]`), parents first, 0 to 249 members. Both ends need the same members in the same order. Removing, reordering or retyping members breaks old data; use `VersionTolerant` for evolving schemas.
- `VersionTolerant` matches members by name; `[LuminPackOrder]` is optional there. `[LuminPackOldName("Old")]` keeps renamed members readable.
- `CircleReference` (reference tracking) requires `[LuminPackOrder]` on **every** serialized member.
- Binary and JSON are separate protocols; never mix their outputs.
- Default strings are UTF-16 with length prefixes. Reader and writer must use the same options (attribute declarations or explicit profile).
- Binary is little-endian. Raw unmanaged structs are copied with padding unless `StructLayout` says otherwise.
- Group Union switches with `[LuminPackOption(WideTag = true, SingleImplementation = true)]` on the root class/interface only. These settings do not inherit or pin string profiles; legacy standalone markers remain compatible. `SingleImplementation` preserves Union framing and virtual dispatch. Collections always use their actual lengths.
- There is **no runtime options object**. Options are `[LuminPackOption(...)]` declarations or a profile type argument such as `Serialize<LuminPackUtf8LengthOptions>(value)`.
- `LuminBufferWriter` is an `IBufferWriter<byte>` over an `ArrayPool<byte>` rental: dispose it (or return it to its writer pool). Borrowed spans expire on growth, release or pool return. Writes into it append; reset with `ResetForReuse()` between independent records.
- `Register` APIs take function pointers: the project needs `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` and an `unsafe` block.
- Reuse is not transactional: a failed read can leave members already updated.

## Where to look

- [API reference](references/api.md): every entry point, profiles, buffers, registration, Custom formatters, low-level API, helpers.
- [Attribute catalog](references/attributes.md): every attribute and option with examples.

## Common tasks

| Task | Use |
| --- | --- |
| Allocation-free repeated reads | `Deserialize(bytes, ref existing)` |
| Polymorphism | `[LuminPackable]` interface/abstract root; cases collected automatically or `[LuminPackUnion(tag, typeof(Case))]` |
| Type from another assembly | `[assembly: LuminPackable(typeof(Lib.Model))]` (also open generics, also Union roots) |
| Schema evolution | `[LuminPackable(GeneratorType.VersionTolerant)]` |
| Shared objects or cycles | `[LuminPackable(GeneratorType.CircleReference)]` + `[LuminPackOrder]` on every member |
| Same bytes on 32-bit and 64-bit | `[LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]` on the struct or assembly |
| UTF-8 strings | `[LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]` or the `LuminPackUtf8LengthOptions` profile |
| Hostile input | `[LuminPackOption(UntrustedData = true, MaxInputBytes = ..., ...)]` on the root type or assembly |
| Hand-written format | `[LuminPackable(GeneratorType.Custom)]` with static `Serialize`/`Deserialize` |
| Smaller generated code | `<LuminPackGenerationMode>Light</LuminPackGenerationMode>` + `<CompilerVisibleProperty Include="LuminPackGenerationMode" />` |
| Pool instances | `[LuminPackPoolRent] static T Rent()` |

## Pitfalls

- For collections of small strings, opt in with `[LuminPackOption(StringRecording = LuminPackStringRecording.VarInt)]` on the member/type/assembly, or use `LuminPackUtf16VarIntOptions` / `LuminPackUtf8VarIntOptions` as the operation profile. Both endpoints must match. This saves header bytes at a possible throughput cost; fixed `Length` remains the default. UTF-16 retains compact Latin-1, UTF-8 retains both length counts, and null stays distinct from empty. Collection count headers and JSON do not change. See [options](../../docs/options.md).

- Don't pass `null!` targets expecting reuse; reuse needs an existing instance.
- Don't rely on reflection- or `typeof`-driven serialization under `Light`/`Minimal`; those modes only keep statically reachable types.
- Don't use `$type`, `$value`, `$id`, `$ref` as JSON member names.
- `NaN`/infinity cannot be written to JSON.
- Under NativeAOT, reading a non-null `System.Type` throws `PlatformNotSupportedException`.
- A `Custom` type used as a member of another model needs `[LuminPackableObject]` on that member.
- `[LuminPackBounded(n)]` is a promise; a wrong bound can write past the buffer.

- Binary and JSON reader/writer `Options` are properties. Readers constructed from a buffer bind its reader state; writers prefer explicit state to the associated buffer state. Use `OptionState` when an initialized context is required.
