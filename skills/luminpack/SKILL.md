---
name: luminpack
description: Write correct code that uses the LuminPack binary and JSON serializer for .NET and Unity. Covers [LuminPackable] models and attributes, the generated LuminPackSerializer API, ref reuse, Unions, version tolerance, reference tracking, compile-time options and profiles, struct layouts, automatic generation, Custom formatters and registration.
---

# Using LuminPack

LuminPack generates a dedicated binary and JSON formatter for every type at compile time (incremental source generator, no reflection). It targets `netstandard2.1` (Unity Mono/IL2CPP) and .NET 8 to .NET 11, and works under NativeAOT.

This skill is about using the library. It does not describe how to maintain the LuminPack repository.

Generated models do not require a runtime plan or formatter witness supplied by the caller. Operation plans are internal source-generator data; they do not add a runtime API or dispatch layer.

JSON property-name lookup is generated automatically: sparse length groups use exact comparisons, and dense groups use a shared seed-zero hash followed by exact comparison. Generated hash calls use `XxHash3.Hash64(ref byte, uint)` over a validated range, without pointers or pinning. Do not persist these internal hashes; rebuild generated consumers with the matching Core after an algorithm change. See [JSON name lookup](../../docs/json-name-lookup.md).

Internal binary writes can share named static member kernels while keeping their existing wire layout and member profiles. Selection is automatic; do not introduce a generation-mode setting, require `default(TPlan)`, or call these internal helpers from user code. Keep using `LuminPackSerializer` and the supported Writer/Reader APIs.

`LuminPackSourceGenerator` publishes the binary facade, shared support and each `[LuminPackable]` model's binary code. `LuminPackJsonGenerator` publishes the JSON facade and each model's JSON code. `LuminPackFormatterGenerator` publishes framework and closed generic formatters for both formats, dispatch and storage views. Files are named `LuminPackGen.<Namespace>.<Name>.g.cs`, with the generic arity in place of the arguments (``LuminPackGen.System.Collections.Generic.List`1.g.cs``). Built-in scalar formatters share `LuminPackGen.System.g.cs` and arrays share `LuminPackGen.Array.g.cs`. Applications and Unity predefined assemblies without friend access retain only the operations reached per payload and its dependencies. Libraries retain all four for their discovered model graphs. Import referenced models in the consumer with `[assembly: LuminPackable(typeof(ExternalModel))]`. Use `LuminPackGeneratorOptions` with `ForceGenerateSerialize`, `ForceGenerateDeserialize`, `ForceGenerateSerializeJson`, or `ForceGenerateDeserializeJson` to retain a module for every discovered payload when source calls cannot describe the requirement. Runtime-selected generic calls still use typed registration.

When a project explicitly selects the Next generator, its single incremental pipeline groups output under the legacy generator names for binary, JSON and formatters. Closed facade calls bind directly to static roots through interceptors. A source generic wrapper with an open payload can use built-in, Custom or emitted closed-type operations registered on first use of the Next facade. Explicit registrations take precedence and cover opaque types or operations outside the generated set. Assembly markers select referenced payloads and can name open generic definitions or exact closed types. Imported generic Unions with explicitly declared closed cases write through the declaring assembly's compiled slots. Register additional Union cases through `LuminPackSerializer.Register<TUnion, TMember>(tag, Write, Read, WriteJson, ReadJson)`. Core's hidden `LuminPackUnionFormatter<TUnion>` owns the shared table; Next roots do not publish per-root Register methods. Continue using public serializer entry points rather than generated writer aliases or framing markers. See [Next generator](../../docs/generator-next.md).

For repository builds using Next, `-p:LuminPackReportGeneratorOutput=true -p:EmitCompilerGeneratedFiles=true` writes a comment-only output report with the final source size and selected operation counts. See [Next output accounting](../../docs/next-generator-prototype.md#output-accounting).

The `LuminPack` NuGet package loads the model-based `LuminPack.SourceGenerator.dll` analyzer automatically. Rider can show all generated files under one `LuminPack.SourceGenerator.LuminPackSourceGenerator` node. See [Next generator](../../docs/generator-next.md) for the installation check and output paths.

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

`LuminPackSerializer` is generated into the consuming assembly (namespace `LuminPack`). For 2.0, use the single `LuminPack` package or reference `src/LuminPack.Core` and `src/LuminPack.Generator.Next` (as an analyzer) from source. See [Getting started](../../docs/getting-started.md#installation) for the package version and project references.

IDE quick actions are bundled in `LuminPack.Generator`, which comes automatically with `LuminPack`; no additional package is needed. They fix supported generator diagnostics such as missing packable markers, Union partial declarations, invalid attributes and accessibility. Constructor diagnostics offer an explicit choice; other mechanical fixes support Fix All. Review edits that change modifiers. See [IDE code fixes](../../docs/code-fixes.md) for supported IDs and loading requirements.

## Rules that matter

- In Next, declare class/interface Union roots with more than 32 generated cases, their cases and their containing types partial (LuminPack035). Smaller Unions use exact type comparisons. On .NET 11, mark a C# union declaration [LuminPackable]. Compiler Union structs keep their ordinal representation.
- The default `Default` mode is positional: members in declaration order (or `[LuminPackOrder]`), parents first, 0 to 249 members. Both ends need the same members in the same order. Removing, reordering or retyping members breaks old data; use `Version` for evolving schemas.
- `Version` matches members by case-sensitive name key and requires compatible type and width descriptors. It skips unknown members and defaults missing ones, including on reuse reads. Keep every previous name with `[LuminPackOldName("Old")]` in chronological order. Keys must be unique across the model and inherited members. `[LuminPackOrder]` is optional and only affects write order.
- Use `[LuminPackOption(TrackReferences = true)]` on a concrete class for binary and JSON object identity and cycles. It works with both `Default` and `Version` and applies only to the annotated class. Convert stored slot-format data using its original reader. See [version tolerance and reference tracking](../../docs/versioning.md).
- Binary and JSON are separate protocols; never mix their outputs.
- For public JSON consumed by another library, register a closed `LuminPackJsonConverter<TModel, TContract>` on the [System.Text.Json](../../src/LuminPack.SystemTextJson/README.md) or [Newtonsoft.Json](../../src/LuminPack.NewtonsoftJson/README.md) side. Supply an explicit DTO and typed mappings in both directions. Use source-generated `JsonTypeInfo<TContract>` with System.Text.Json when the application uses NativeAOT.
- Optional Unity native-owner codecs remain binary-only. For precompiled `LuminPack.Unity` builds against Unity 6000.2 or later, pass `-p:DefineConstants=UNITY_6000_2_OR_NEWER` to enable public Span key APIs; Unity source integration supplies it automatically. Retained output and warmed scratch pools avoid temporary managed allocations for modern curve/gradient writes and reuse reads. Earlier engines still allocate key arrays for curve reads and gradient operations; pool misses can allocate on modern engines. See [Unity version limits](../../docs/unity.md#native-owner-scratch-and-unity-versions).
- Default strings are UTF-16 with length prefixes. Reader and writer must use the same options (attribute declarations or explicit profile).
- Use `[assembly: LuminPackOption(FramedGroupings = true)]` at both binary endpoints to round-trip `IGrouping` keys whose encoded first byte is `FF`, including groups in `ILookup`. The default preserves the key-first wire layout. JSON preserves null-key groups without this setting.
- Binary is little-endian. Raw unmanaged structs are copied with padding unless `StructLayout` says otherwise.
- Next class/interface Unions use 32-bit FNV-1a hashes of canonical case names. Binary writes four little-endian bytes and JSON writes the unsigned decimal hash as `$type`. Explicitly registered cases use numeric tags. WideTag controls their binary tag width. Names are part of both schemas. See [polymorphism](../../docs/polymorphism.md).
- There is **no runtime options object**. Options are `[LuminPackOption(...)]` declarations or an explicit payload and profile, such as `Serialize<Message, LuminPackUtf8LengthOptions>(value)`.
- `LuminBufferWriter` is an `IBufferWriter<byte>` over an `ArrayPool<byte>` rental. Writes append at its current cursor. Call `Reset()` between independent records to clear the written count, cursor, and both operation states while keeping capacity. Use `ResetWrittenCount()` to reset the count and cursor while preserving both operation states. Dispose a directly constructed writer or return a rented one to its pool. Borrowed spans expire on growth, storage release or pool return.
- Use an application-owned pool for model instances rented through `[LuminPackPoolRent]`. The generic `ObjectPool<T>` and its policy are internal to Core. See [pool ownership and retention](../../docs/pooling.md).
- On the modern interceptor backend, closed calls (`Serialize(value)`, `Serialize<T>`, `Deserialize<T>` and `<T, TOptions>`) bind existing concrete Roots. Safely relocatable static source wrappers can be specialized at closed calls. Opaque implementations, runtime-selected targets and unsupported wrappers use explicit/Custom registration; missing operations throw `LuminPackException`. No runtime Plan or reflection factory is introduced; only registered fallback retains delegates. Older Roslyn hosts and `LuminPackUseInterceptors=false` use the packaged IL postprocessor when `LuminPackUseIlRewriting` is enabled. Unity installs the shared rewriter through `Unity.LuminPack.CodeGen` and `LUMINPACK_IL_REWRITE` (see `docs/unity.md`); never add the define without the processor. With both integrations disabled, ordinary default calls retain concrete overloads and explicit profiles require registered fallback. IL rewriting runs before runtime/AOT, preserves portable symbols, validates exact stack types and requires the original key for signed assemblies. See [generation and fallback boundaries](../../docs/generation-modes.md#static-interception). Intercepted Roots retain declaration options; fallback document policies use assembly defaults or the explicit profile.
- `Register` APIs take typed delegates. Pass method groups directly (`Register<T>(Write, Read)`), without `&`, an `unsafe` block or `AllowUnsafeBlocks`. Registration is available unless explicitly disabled. Delegate registrations follow the payload type's lifetime; captured targets remain alive with that registration. Platform and Unity module registration follows the same rule; `PlatformRegistration.Add<T>(Write, Read, codec)` takes method groups without address-of operators.
- Do not use or regenerate `Sizeof` or `CalculateOffset`. Internal concrete `MeasureRecord` helpers only size supported direct allocating writes; there is no generic size fallback or registration slot.
- Reuse is not transactional: a failed read can leave members already updated.
- On probed modern CoreCLR layouts, Next binary reads can refill a same-length nonempty array directly behind an exact `ReadOnlyCollection<T>` of supported raw scalars. Aliases to the wrapper and array see the updated values after the collection check. Pass a null target to keep a separate snapshot.
- On probed 64-bit CoreCLR layouts, Next binary reads can update exact `ReadOnlyDictionary<string, int>` and `ReadOnlyDictionary<string, string>` values with the built-in UTF-16 length profile when 1 to 4,096 ordered keys match the exact default-comparer backing dictionary. Existing aliases see the values after a successful dictionary read. Pass a null target to read a separate dictionary.
- On modern .NET, Next binary reads of array-backed `ReadOnlyMemory<string>` and `ReadOnlySequence<string>` can reuse equal strings under the built-in UTF-16 length profiles. A same-length slice and equal first string enable reuse from the remaining old slots. The result owns a separate backing array.
- On probed layouts, Next binary reads can reuse matching keys in exact default-comparer `SortedList<string, int>` and `SortedList<string, long>` targets with 1 to 4,096 entries and the same incoming count under a built-in profile. The reader still clears the list and publishes each completed pair in sequence.
- On supported .NET 10 and 11 runtimes, a Next binary `FrozenSet<T>` reuse read can retain a previously verified immutable raw set when its complete item bytes and order match the incoming record. The verification is local to the current thread and element type.
- Next binary reads clear and refill an exact default-comparer `HashSet<T>` target (also behind `ISet<T>`/`IReadOnlySet<T>`) in place, keeping its storage; custom-comparer and derived sets are replaced. Reused `ConcurrentQueue<T>` targets are drained and refilled so their last segment is kept.
- The default binary UTF-16 length reader reuses a zero-based `string[,]` when both dimensions match. Aliases observe completed slots even if a later record fails its bounds or string quota. Other string profiles allocate a new matrix.
- With Next on .NET 8 or later, a binary `T[,]` of ordinary objects updates an exact zero-based destination of the same shape in row-major order, reusing each existing element like arrays and lists do. `[LuminPackOption(ReuseRectangularArraySlots = false)]` on the element class restores replacement. Slots sharing one mutable object observe the last record read into it. A failed read can expose the completed prefix. See [object matrix reuse](../../docs/reuse.md#binary-object-matrix-reuse).
- Binary and JSON reads can reuse the current framework instance for a repeated canonical culture name on validated .NET 10 and 11 CoreCLR runtimes. Each thread retains one name of at most 85 characters and at most one read-only culture. Only an ASCII name confirmed in the framework's name cache is admitted. A weak reference to that cache detects `CultureInfo.ClearCachedData()`; a miss uses `CultureInfo.GetCultureInfo`.
- On supported modern targets, an exact binary dictionary with its default comparer can refill existing slots when its fixed-width key uses callback-free framework equality and the value formatter supports safe in-place reading. Matching keys retain the destination order. A failed read can expose the completed prefix. User-defined key behavior, custom comparers and unsupported value formatters use staged reconstruction. Empty destinations with callback-free keys can use the checked bulk rebuild for raw pairs.
- JSON `ref` reads reuse objects, list elements, exact same-length arrays, dictionary values matched by key, mutable array slices, exact Union cases and tracked references. Unchanged decoded strings retain identity. JSON `null` clears the target; omitted members reset to `default` on an existing object. After parsing JSON into a concrete `ImmutableSortedSet<T>`, .NET 10 and later can link a balanced frozen tree from two or more strictly ascending elements. Enums and non-generic comparable CoreLib value types use their default ordering. Strings must be non-null and use an ordinary current culture. User comparisons, duplicates, unordered input and unavailable tree bindings use the builder.

## Where to look

- Aggregate generated filenames expose the namespace/type or declaration before a stable suffix (`LuminPackSerializer.Namespace.Type.<suffix>.g.cs`, `LuminPack.Formatter.*`, `LuminPack.LocalLayouts.*`). Search the readable prefix when inspecting generator output; the suffix disambiguates identities and is not a public API.

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
| Opt in derived classes | `[LuminPackable, LuminPackOption(IncludeDerivedTypes = true)]` on the base class |
| Type from another assembly | `[assembly: LuminPackable(typeof(Lib.Model))]` (also open generics, also Union roots) |
| Options for an imported type | `[assembly: LuminPackOption(typeof(Lib.Model), ...)]` alongside the assembly marker for that type |
| Schema evolution | `[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]` |
| Shared objects or cycles | `[LuminPackOption(TrackReferences = true)]` on the class, with `[LuminPackable]` or `[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]` |
| Same bytes on 32-bit and 64-bit | `[LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]` on the struct or assembly |
| UTF-8 strings | `[LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]` or the `LuminPackUtf8LengthOptions` profile |
| Hostile input | Checked reads are the default; add `[LuminPackOption(MaxInputBytes = ..., MaxCollectionLength = ..., MaxDepth = ...)]` limits on the root type or assembly |
| Require a complete binary record | `[assembly: LuminPackOption(RequireCompleteBinaryInput = true)]` rejects trailing bytes at value-returning binary document reads; use `ref` or advancing reads for concatenated records |
| Trusted data only | `[LuminPackOption(UntrustedData = false)]` restores unchecked reads |
| Hand-written format | `[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Custom)]` with static `Serialize`/`Deserialize` |
| Generated code selection | Automatic: declared model graphs plus statically required operations; no generation mode setting |
| Pool instances | `[LuminPackPoolRent] static T Rent()` |

For ordinary objects with `[LuminPackPoolRent]`, binary and JSON reads without `ref` also rent first and then reuse the rented instance's existing graph. Supplying a non-null target by `ref` skips the rent.

## Pitfalls

- For collections of small strings, opt in with `[LuminPackOption(StringRecording = LuminPackStringRecording.VarInt)]` on the member/type/assembly, or use `LuminPackUtf16VarIntOptions` / `LuminPackUtf8VarIntOptions` as the operation profile. Both endpoints must match. This saves header bytes at a possible throughput cost; fixed `Length` remains the default. UTF-16 retains compact Latin-1, UTF-8 retains both length counts, and null stays distinct from empty. Collection count headers and JSON do not change. See [options](../../docs/options.md).

- Don't pass `null!` targets expecting reuse; reuse needs an existing instance.
- Runtime-selected types and opaque calls require registered fallback. Generation is automatic; legacy mode settings are ignored.
- Don't use `$type`, `$value`, `$id`, `$ref` as JSON member names.
- `NaN`/infinity cannot be written to JSON.
- `DateOnly`, `TimeOnly`, `Int128` and `UInt128` use JSON strings: round-trip dates/times and invariant decimal integers. Modern Core assets expose typed `LuminPackJsonWriter.Write` / `Write<TOptions>` overloads that avoid intermediate strings. Generated code selects them from the referenced Core API; older assets automatically retain their text conversion fallback.
- `Index` writes a JSON integer. From-end indexes use the bitwise complement of the value, so `^0` is `-1`. `Range` writes an array of its start and end indexes.
- Apply `[LuminPackOption(IncludeCollectionBase = true)]` to an ordinary `[LuminPackable]` class with a closed `List<T>` base to include inherited elements after the declared binary members or in the JSON `$items` property. Select the same option at both binary endpoints.
- A closed source subclass of `List<T>` or `SortedDictionary<TKey, TValue>` can use its base formatter when it has an accessible parameterless constructor and adds no instance storage. Explicitly registered formatters take precedence.
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

The optional Burst ABI 5 Boolean kernel accelerates checked binary `bool[]` and `List<bool>` reads on `netstandard2.1` with at least 128 bytes of payload. Ordinary serializer calls use it automatically when the Unity Burst backend registers a supported kernel. Older tables and unavailable SIMD use the Core fallback.

Fresh JSON sequence readers can use the sizing hint to avoid backing-array growth; fresh Capacity can differ and the extra scan can cost CPU. Reuse and callbacks retain their original order. Hash collections and arrays keep their existing construction. Deep CountArrayElements lookahead uses an exclusive native pool lease; ordinary deep reader state and cold pool misses can still allocate.
