# Architecture

[Chinese](architecture.cn.md)

All changes are subject to the [mechanism contract](mechanism-contract.md).

## Repository layout

| Path | Contents |
| --- | --- |
| `src/LuminPack.Core` | Runtime (NuGet package `LuminPack`, assembly `LuminPack.Core`): binary and JSON readers/writers, the exact-width evaluator behind direct allocating writes, buffers, collections, layout conversion, text kernels, compression, ChaCha20-Poly1305, security policy. Targets `netstandard2.1`, `net8.0` to `net11.0`. |
| `src/LuminPack.Generator` | Incremental source generator and analyzer (`netstandard2.0`, Roslyn 4.3). Shipped as its own package; the runtime package `LuminPack` depends on it. |
| `src/LuminPack.Unity`, `src/LuminPack.Burst` | Optional Unity value codecs and the Burst backend for the native hook table. |
| `src/LuminPack.Platforms` and adapters | Shared adapter plus ASP.NET Core, Avalonia, Drawing, MAUI, MonoGame, SkiaSharp and WPF codecs. |
| `tests/` | See [Tests](#tests). |
| `benchmarks/LuminPack.Benchmark` | BenchmarkDotNet comparisons: `SimpleBenchmark`, `SerializeBenchmark`, `JsonBenchmark`, `PolymorphismBenchmark`. |
| `tools/` | `LuminPack.RepositoryChecks` (source standards), performance probes, xjb table generation. |
| `skills/luminpack` | Usage skill for AI assistants. |
| `experiments/` | Local experiments; not part of the product. |

### Core source layout

Runtime entry points and cursor state live in `src/LuminPack.Core/Core`; the optional `netstandard2.1` hook dispatch lives in `Core/Acceleration`. Utilities are grouped by responsibility under `Utility`:

| Directory | Responsibility |
| --- | --- |
| `Buffers` | Owned and segmented output, sequence construction. |
| `Collections`, `Collections/Hashing`, `Collections/Trees` | Collection storage views, hash maps, tree helpers. |
| `References` | Reference identity and Union maps. |
| `Memory`, `Memory/Views`, `Memory/Simd` | Marshal operations, raw arrays, unmanaged views, copy kernels. |
| `Layout`, `Layout/Simd` | Field offsets, layout tags, conversion and byte shuffles. |
| `Text` | `StringSerializer` and text kernels. |
| `Compression`, `Pool` | LZ compression and pooled storage. |
| `Configuration`, `Diagnostics`, `Protocol` | Option scopes, exceptions, protocol constants. |
| `Compatibility` | Public forwarding entries used by generated consumers. |

Physical placement never changes namespaces or representations.

## Runtime execution model

- **Cursors.** Each reader and writer advances a single cursor: `ref byte` on modern targets, `byte*` on `netstandard2.1`. The retained `Buffer` window supplies lengths and back-patching.
- **Two registers.** Beside the cursor, each state caches the end of its window (`_end`, or the writer's `_recordLimit` growth threshold). Range checks are one subtraction and one signed comparison. Setting `Buffer`, `BufferCheckLimit` or `Owner` (internal setters) refreshes the cache.
- **Cursor-local generated writes.** Generated object writers keep the cursor in a `ref byte __cursor` local, which the JIT keeps in a register, and call `ref`-returning writer operations. Consecutive bounded members share one `ReserveRecord` check and are then stored unchecked. The cursor is published with `CommitRecords` before any operation that uses the writer itself.
- **Reader checks.** `EnsureReadable(count)` compares against the cached end before every unsafe load; it is active in Debug and Release.
- **Options** are stateless value-type profiles (`ILuminPackOptions`) passed as generic arguments, so profile decisions fold to constants.
- **Buffers.** `LuminBufferWriter` is an `IBufferWriter<byte>` over one `ArrayPool<byte>.Shared` array, together with the binary/JSON reference states of an append session. Its usable range starts on a 64-byte boundary observed at rent time. Logical capacity and growth thresholds remain independent of pool bucket sizes; a growth that fits the rented array extends the range in place. Writers take a `ref byte` when they bind to the storage or refresh it after growth; on netstandard2.1 they take a `byte*`, and the buffer keeps the rented array pinned with a `GCHandle` from rent until return.

Binary and JSON readers constructed from a buffer bind its reader reference context. Writers resolve an explicit reference context first, then the associated buffer context. `OptionState` provides an initialized context.

## Generator

Two incremental generators run in every consuming compilation:

| Generator | Output |
| --- | --- |
| `LuminPackSourceGenerator` | Per-type formatter extensions, layout mirrors and private accessors, Union slots and overrides. |
| `LuminPackAggregateGenerator` | The per-assembly `LuminPackSerializer` facade (shared generic APIs plus per-type concrete partials), generic dispatch, option profile declarations and the `Custom` formatter registry. |

Main components:

| Component | Responsibility |
| --- | --- |
| `LuminPackSourceGenerator.*` | Model analysis: metadata, fields and inherited storage, constructors, type classification, Union discovery. |
| `LuminPackCodeGenerator.*` | Binary write/read, direct-record width evaluation, unmanaged grouping, cursor-local writes (`CursorWrite`). |
| `LuminPackExtensionGenerator.*` | Operation templates, JSON helpers, dispatch contributions. |
| `LuminPackSerializerGenerator.*` | Concrete and generic buffer, span, stream, async, JSON and compression APIs. |
| `CodeEmitters/` | One emitter per scalar or collection family. |
| `Api/` | Facade call analysis used by `Light` and `Minimal`. |
| Projections (`SerializerCoreProjection`, `BinaryCursorProjection`, `CompiledOperationProjection`, `RecursionGuardProjection`, `GeneratedOperationScopes`, ...) | Rewrite completed operations: state ownership, cursor arithmetic with checks, profile propagation, recursion guards, readable locals. |
| `PortableStructPlan`, `PortableStructEmitter` | Packed and tagged struct storage. |
| `Incremental/` | Frozen values, dependency guards and caches (`TypeEmissionCache`, `AggregateEmissionCache`, `SerializerFacadeCache`, `RegistrySourceCache`, `PartitionedSourceCache`, ...). |

### Operation pipeline

1. Analyze the canonical type contract and freeze operation templates.
2. Bind the root and compose nested operations, keeping recursive, `Custom`, virtual and specialized boundaries.
3. Bind calls to the serializer state bodies and propagate compiled profiles.
4. Normalize reader storage lifetimes and cursor slots.
5. Simplify blocks and choose readable local names (presentation only).
6. Format, add XML documentation, and publish the text through the cache boundary.

### Incremental boundaries

Generation separates current semantic analysis from frozen output values. Each root's source, layout and diagnostics are compared before source production; unchanged values cause no output callback. Cross-compilation caches keep descriptions, syntax tree identities, copied settings and generated strings, never a `Compilation`, `SemanticModel` or symbol. Invalidation covers project-level facts (references, language settings, aliases, extension signatures) and source closures (observed types, members, attributes, partial declarations). `IncrementalGenerationMetrics` exposes internal counters for the incremental tests.

### Generated files

`LuminPackSerializer.g.cs` holds shared generic APIs; concrete type overloads live in hash-named partial files, stable when other types change. Every generated assembly owns a public `LuminPackSerializer`; generated files suppress CS0436 so a referenced assembly's copy is shadowed. Roots of each payload live in a `__LuminRoots_<hash>` container so overload resolution examines one payload per call.

### Documentation of generated code

`GeneratedSourceDocumentation` and `LuminPackSerializerDocumentation` add XML documentation to generated members, derived from the same model analysis: type, member order and filters, CLR layout versus wire layout, buffer semantics and return units.

## Tests

| Project | Scope |
| --- | --- |
| `tests/LuminPack.Tests` | xUnit runtime tests. `LuminPackTestArea` selects `All` (default), `Safety`, `Simd`, or `Hooks` (the `netstandard2.1` asset with native hook probes). |
| `tests/LuminPack.Tests.Codegen` | Generated-code contracts with the SDK's Roslyn: XML docs, mechanisms, inlining, Union cursors, reuse, partitions, external Unions, options. |
| `tests/LuminPack.Tests.Generator` | Generator pipeline with Roslyn 4.3: concurrency, emitter shapes, pruning modes, incremental invalidation (`--verify-incremental`). |
| `tests/LuminPack.Tests.Aot` | Trimmed/NativeAOT smoke program and wire fixtures (`--wire verify`); with `-p:LuminPackPackageVersion=<v>` it runs against the packed NuGet packages. |
| `tests/LuminPack.Tests.Platforms` | Platform adapter contracts, selected with `LuminPackPlatform`. |
| `tests/Unity` | Unity Mono/IL2CPP smoke project. |

## Exceptions

Production throws are built in `LuminPackExceptionHelper`. Generated buffer-writing catch paths reset the buffer and rethrow through `ExceptionDispatchInfo`, preserving the original stack.
