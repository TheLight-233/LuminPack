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
LuminPackSerializer.DeserializeJson(json, ref fromJson);  // reuses the existing JSON graph
```

`LuminPackSerializer` is generated into the consuming assembly (namespace `LuminPack`). Install with `dotnet add package LuminPack`; the generator package comes with it.

IDE quick actions are bundled in `LuminPack.Generator`, which comes automatically with `LuminPack`; no additional package is needed. They fix supported generator diagnostics such as missing packable markers, Union partial declarations, invalid attributes and accessibility. Constructor diagnostics offer an explicit choice; other mechanical fixes support Fix All. Review edits that change modifiers. See [IDE code fixes](../../docs/code-fixes.md) for supported IDs and loading requirements.

## Rules that matter

- Declare models `partial`. Union roots, their cases and every containing type **must** be `partial` (LuminPack035).
- The default `Object` mode is positional: members in declaration order (or `[LuminPackOrder]`), parents first, 0 to 249 members. Both ends need the same members in the same order. Removing, reordering or retyping members breaks old data; use `VersionTolerant` for evolving schemas.
- `VersionTolerant` matches members by name; `[LuminPackOrder]` is optional there. `[LuminPackOldName("Old")]` keeps renamed members readable.
- `CircleReference` (reference tracking) requires `[LuminPackOrder]` on **every** serialized member.
- Binary and JSON are separate protocols; never mix their outputs.
- Optional Unity native-owner codecs remain binary-only. For precompiled `LuminPack.Unity` builds against Unity 6000.2 or later, pass `-p:DefineConstants=UNITY_6000_2_OR_NEWER` to enable public Span key APIs; Unity source integration supplies it automatically. Retained output and warmed scratch pools avoid temporary managed allocations for modern curve/gradient writes and reuse reads. Earlier engines still allocate key arrays for curve reads and gradient operations; pool misses can allocate on modern engines. See [Unity version limits](../../docs/unity.md#native-owner-scratch-and-unity-versions).
- Default strings are UTF-16 with length prefixes. Reader and writer must use the same options (attribute declarations or explicit profile).
- Binary is little-endian. Raw unmanaged structs are copied with padding unless `StructLayout` says otherwise.
- Group Union switches with `[LuminPackOption(WideTag = true, SingleImplementation = true)]` on the root class/interface only. These settings do not inherit or pin string profiles; legacy standalone markers remain compatible. `SingleImplementation` preserves Union framing and, without generated `Register` APIs, allows a direct call of the single sealed case. Union writes are tiered automatically (direct call, exact guards for one to four cases, one virtual call above that). `List<T>`, arrays and `ImmutableArray<T>` use the selected per-element dispatch in input order for fixed and variable payloads; this preserves wire bytes and requires no annotations. Collections always use their actual lengths.
- There is **no runtime options object**. Options are `[LuminPackOption(...)]` declarations or a profile type argument such as `Serialize<LuminPackUtf8LengthOptions>(value)`.
- `LuminBufferWriter` is an `IBufferWriter<byte>` over an `ArrayPool<byte>` rental: dispose it (or return it to its writer pool). Borrowed spans expire on growth, release or pool return. Writes into it append; reset with `ResetForReuse()` between independent records.
- Generic payload APIs (`Serialize<T>`, `Deserialize<T>`, `<T, TOptions>` and `Save<T>` wrappers) require explicit/Custom registration even for built-in and generated types; missing operations throw `LuminPackException`. Concrete calls and `<TOptions>` concrete overloads bind Roots directly. With unsafe disabled or registration disabled, generic payload APIs have no generated fallback. Generic document policies use assembly defaults or the explicit profile.
- `Register` APIs take function pointers: the project needs `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` and an `unsafe` block.
- Reuse is not transactional: a failed read can leave members already updated.
- JSON `ref` reads reuse objects, list elements, exact same-length arrays, dictionary values matched by key, mutable array slices, exact Union cases and tracked references. Unchanged decoded strings retain identity. JSON `null` clears the target; omitted members reset to `default` on an existing object. Immutable collections keep their construction behavior.

## Where to look

- Concrete overload XML remarks include a **Generated implementation** link when the model or built-in formatter has a recognized binary or JSON Root. This covers closed generic models, collections and compound values. Raw unmanaged reads without a separate Root, Custom formatter entry points and generic type-selected fallback APIs keep their existing paths.
- [English overview](../../README.md) and [Chinese overview](../../docs/README.cn.md). All Chinese editions live under `docs`; module translations are in `docs/modules`.
- [API reference](references/api.md): every entry point, profiles, buffers, registration, Custom formatters, low-level API, helpers.
- [Attribute catalog](references/attributes.md): every attribute and option with examples.

## Common tasks

| Task | Use |
| --- | --- |
| Allocation-free repeated reads | `Deserialize(bytes, ref existing)` |
| Reuse an existing JSON graph | `DeserializeJson(json, ref existing)`; UTF-8/UTF-16, concrete/generic and text/span/buffer entries |
| Polymorphism | `[LuminPackable]` interface/abstract root; cases collected automatically or `[LuminPackUnion(tag, typeof(Case))]` |
| Type from another assembly | `[assembly: LuminPackable(typeof(Lib.Model))]` (also open generics, also Union roots) |
| Schema evolution | `[LuminPackable(GeneratorType.VersionTolerant)]` |
| Shared objects or cycles | `[LuminPackable(GeneratorType.CircleReference)]` + `[LuminPackOrder]` on every member |
| Same bytes on 32-bit and 64-bit | `[LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]` on the struct or assembly |
| UTF-8 strings | `[LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]` or the `LuminPackUtf8LengthOptions` profile |
| Hostile input | Checked reads are the default; add `[LuminPackOption(MaxInputBytes = ..., MaxCollectionLength = ..., MaxDepth = ...)]` limits on the root type or assembly |
| Trusted data only | `[LuminPackOption(UntrustedData = false)]` restores unchecked reads |
| Hand-written format | `[LuminPackable(GeneratorType.Custom)]` with static `Serialize`/`Deserialize` |
| Smaller generated code | `<LuminPackGenerationMode>Light</LuminPackGenerationMode>` + `<CompilerVisibleProperty Include="LuminPackGenerationMode" />` |
| Pool instances | `[LuminPackPoolRent] static T Rent()` |

For ordinary objects with `[LuminPackPoolRent]`, binary and JSON reads without `ref` also rent first and then reuse the rented instance's existing graph. Supplying a non-null target by `ref` skips the rent.

## Pitfalls

- For collections of small strings, opt in with `[LuminPackOption(StringRecording = LuminPackStringRecording.VarInt)]` on the member/type/assembly, or use `LuminPackUtf16VarIntOptions` / `LuminPackUtf8VarIntOptions` as the operation profile. Both endpoints must match. This saves header bytes at a possible throughput cost; fixed `Length` remains the default. UTF-16 retains compact Latin-1, UTF-8 retains both length counts, and null stays distinct from empty. Collection count headers and JSON do not change. See [options](../../docs/options.md).

- Don't pass `null!` targets expecting reuse; reuse needs an existing instance.
- Don't rely on reflection- or `typeof`-driven serialization under `Light`/`Minimal`; those modes only keep statically reachable types.
- Don't use `$type`, `$value`, `$id`, `$ref` as JSON member names.
- `NaN`/infinity cannot be written to JSON.
- `DateOnly`, `TimeOnly`, `Int128` and `UInt128` use JSON strings: round-trip dates/times and invariant decimal integers. Modern Core assets expose typed `LuminPackJsonWriter.Write` / `Write<TOptions>` overloads that avoid intermediate strings. Generated code selects them from the referenced Core API; older assets automatically retain their text conversion fallback.
- Under NativeAOT, reading a non-null `System.Type` throws `PlatformNotSupportedException`.
- A `Custom` type used as a member of another model needs `[LuminPackableObject]` on that member.
- `[LuminPackBounded(n)]` is a promise; a wrong bound can write past the buffer.

- Binary and JSON reader/writer `Options` are properties. Readers constructed from a buffer bind its reader state; writers prefer explicit state to the associated buffer state. Use `OptionState` when an initialized context is required.
- For low-level JSON writing of a `StringBuilder`, use `WriteStringBuilder` or `WriteStringBuilder<TOptions>`. The operation snapshots the complete value before output callbacks can mutate it. Generated serializers select this API automatically when the referenced runtime exposes it.
- The matching reader methods are `ReadStringBuilder(ref value)` and `ReadStringBuilder<TOptions>(ref value)`. They retain fresh capacity and publication behavior and reuse an existing builder after complete token decoding. UTF-8 and UTF-16 reads avoid intermediate strings; fresh reads still allocate the builder and its character storage. UTF-8 scratch can allocate on a pool miss.
- Low-level JSON version operations are `WriteVersion(value)` / `WriteVersion<TOptions>(value)` and `ReadVersion()` / `ReadVersion<TOptions>()`. They preserve nulls and the original omitted-component format; reads return a fresh `Version`. Older UTF-8 readers use bounded or pooled characters without an intermediate string, and the modern unselected-token path retains character-parser failures. Generated serializers select these named APIs when available in the referenced runtime.
- Generated binary BigInteger reads use the available `LuminPackMarshal.ReadBigInteger(ReadOnlySpan<byte>, out BigInteger)` helper after validating the payload. It preserves signed little-endian bytes and avoids negative sign-trimming temporary arrays using bounded or pooled scratch; older runtime references retain the BCL constructor. The helper does not advance a reader. Pool misses and the BCL fallback near the signed bit-count limit can allocate temporary storage.
- Low-level JSON arbitrary-precision operations are `WriteBigInteger(value)` / `WriteBigInteger<TOptions>(value)` and `ReadBigInteger()` / `ReadBigInteger<TOptions>()`. They preserve invariant quoted decimal text and BCL null/invalid-text failures. Retained-buffer writes avoid temporary managed allocations after pool warmup; reads retain BCL parsing behavior and can still allocate runtime-specific temporaries for very large values. A pool miss or creating the output buffer can allocate.

## Low-level structural JSON sizing

`LuminPackJsonReader.GetArrayLengthHint<TOptions>()` reads a structural count after `ArrayStart`, returning -1 when unavailable. `GetArrayCapacityHint<TOptions>(minimum)` and `GetEntryArrayCapacityHint<TOptions>()` return nonnegative capacity hints. They do not validate syntax or replace ordinary reads and bounds checks; preserve growth fallback. Entry hints require scalar/string/null pair members. Core and optional Burst share the scanner. Burst ABI 4 adds these optional slots while accepting older tables. See [native kernel contracts](../../docs/burst-hooks.md).

Fresh JSON sequence readers can use the sizing hint to avoid backing-array growth; fresh Capacity can differ and the extra scan can cost CPU. Reuse and callbacks retain their original order. Hash collections and arrays keep their existing construction. Deep CountArrayElements lookahead uses an exclusive native pool lease; ordinary deep reader state and cold pool misses can still allocate.
