# Architecture

[Chinese](architecture.cn.md)

All changes are subject to the [mechanism contract](mechanism-contract.md).

## Repository layout

| Path | Contents |
| --- | --- |
| `src/LuminPack.Core` | Runtime (NuGet package `LuminPack`, assembly `LuminPack.Core`): binary and JSON readers/writers, the exact-width evaluator behind direct allocating writes, buffers, collections, layout conversion, text kernels, compression, ChaCha20-Poly1305, security policy. Targets `netstandard2.1`, `net8.0` to `net11.0`. |
| `src/LuminPack.Generator` | Incremental source generator and analyzer (`netstandard2.0`, Roslyn 4.3). Shipped as its own package; the runtime package `LuminPack` depends on it. |
| `src/LuminPack.CodeFixes` | IDE quick actions (`netstandard2.0`, C# Workspaces 4.3), bundled in `LuminPack.Generator`. The generator has a build dependency without an assembly reference; neither runtime nor generator references Workspaces. See [code fixes](code-fixes.md). |
| `src/LuminPack.Unity`, `src/LuminPack.Burst` | Optional Unity value codecs and the Burst backend for the native hook table. |
| `src/LuminPack.Platforms` and adapters | Shared adapter plus ASP.NET Core, Avalonia, Drawing, MAUI, MonoGame, SkiaSharp and WPF codecs. |
| `tests/` | See [Tests](#tests). |
| `docs/` | English feature guides and all Chinese editions. Root-page translations live directly here; module README translations live in `docs/modules/<module>.cn.md`. English root pages and module READMEs keep their existing locations. |
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
- **Cursor-local generated writes.** Generated object writers keep the cursor in a `ref byte cursor` local, which the JIT keeps in a register, and call `ref`-returning writer operations. Consecutive bounded members share one `ReserveRecord` check and are then stored unchecked. The cursor is published with `CommitRecords` before any operation that uses the writer itself.
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

Closed external-input descriptors include generated fixed-width/domain facts and a finite depth bound. The binary validator specializes proven and runtime-checked depth paths while retaining cumulative work charges; descriptors from earlier generators keep interpreted validation. Boolean domains use bounded framework scans or portable four-byte groups and scalar tails; already budgeted domains bypass repeated schema interpretation. Fixed version writers reuse the original cached prefix and stamp the compiled profile. Int32/UInt32 reference lengths and ids keep their positive one-byte path small, with the original tagged conversions in outlined helpers.

Generated binary class guards use compact restoration tokens containing only their parent's binary accounting state, borrowed by readonly reference on exit. JSON retains its grammar and duplicate-name snapshots. Both paths preserve root ownership, nested quotas, reuse and exception-safe cleanup; generators referencing older runtimes retain the original token API.

### Incremental boundaries

Generation separates current semantic analysis from frozen output values. Each root's source, layout and diagnostics are compared before source production; unchanged values cause no output callback. Cross-compilation caches keep descriptions, syntax tree identities, copied settings and generated strings, never a `Compilation`, `SemanticModel` or symbol. Invalidation covers project-level facts (references, language settings, aliases, extension signatures) and source closures (observed types, members, attributes, partial declarations). `IncrementalGenerationMetrics` exposes internal counters for the incremental tests.

Type discovery filters declarations without attributes before semantic binding and selects the partial declaration that actually carries the packable attribute. Dependency closures are shared within their owning compilation across output caches. Project guards keep weak metadata-reference identities so compilation-backed references cannot retain old projects. Completed output batches discard obsolete file wrappers. Compilation-wide guards and aggregate planning still inspect current project facts after edits; incremental generation does not mean that every stage does zero work for an unrelated edit.

Within a compilation, the type and aggregate pipelines share frozen operation plans for the same type, complete generation settings and streaming decision. Concurrent requests publish one lazy plan without holding the publication lock during semantic construction. These contexts are weakly keyed by the compilation, hold completed plans weakly and discard failed factories; only symbol-free plans enter retained-driver output caches. Unchanged declaration-option overlays reuse their immutable values; input policy is copied only when an input setting is explicitly overlaid. Temporary operation syntax skips XML parsing while final caller-facing API documentation remains complete. Shared facade analysis also skips XML parsing and replaces only executable method spans in the original documented text. Final projected syntax supplies symbol-free Root navigation facts before it is discarded, avoiding a second complete parse; contributions containing conditional directives retain the text-parsing fallback.

Roslyn cancellation is checked before changed-type analysis and between aggregate formatter and facade contributions. Cancelling obsolete work propagates cancellation instead of reporting a generator failure. Completed contributions remain reusable; cancelled aggregate batches are not published. A single in-progress contribution or project-wide semantic guard can finish before the next cancellation boundary.

### Generated files

`LuminPackSerializer.g.cs` holds shared generic APIs; concrete type overloads live in hash-named partial files, stable when other types change. Every generated assembly owns a public `LuminPackSerializer`; generated files suppress CS0436 so a referenced assembly's copy is shadowed. Roots of each payload live in a `__LuminRoots_<hash>` container so overload resolution examines one payload per call.

Private shared facade helpers and adapters retain all 128 bits of their existing identity in a 26-character, identifier-safe base-32 suffix. This shortens generated source and metadata names without truncating identities or changing API signatures, operation bodies, file identities or dispatch.

Generic payload APIs invoke only registered method pointers. `LuminPack.Dispatch.Formatters.g.cs` has constant-size dispatch and assembly policy helpers, independent of the payload list; no `LuminPack.Dispatch.Lazy.g.cs`, per-payload factory or delegate cache is emitted. Its cache depends on assembly constants and whether generic staging needs an input limit. Concrete roots and their XML navigation facts remain independent of registration.

Closed generic payloads discovered through reachability are indexed before state-call projection. Generated calls to these known types bind their concrete state operations rather than the public registration-only generic APIs. Generic Union overrides retain their original virtual call and cached slots; observed closed cases use the same wire tag and their closed concrete operation.

### Documentation of generated code

`GeneratedSourceDocumentation` and `LuminPackSerializerDocumentation` add compact XML documentation only to directly callable `LuminPackSerializer` APIs and the facade type. API comments describe the same analyzed type, member order and filters, CLR versus wire layout, buffer semantics and return units. Internal state forwarders, execution roots, Union slots, layout helpers and private facade adapters omit XML; their executable code is unchanged. CS1591 is suppressed in generated files so enabling documentation warnings does not require XML on these implementation details.

Concrete model API remarks and built-in overloads with recognized execution roots link directly to the appropriate nested binary or JSON Root through a compiler-bound `cref`. Built-in navigation reuses the completed formatter's symbol-free forwarding facts, and those facts are included in facade cache identity. Raw unmanaged binary readers without a separate Root, Custom entries and size evaluation do not acquire fictitious read/write links. Final presentation removes internal prefixes from proven local roles, gives captured record members descriptive snapshot names and labels cursor record groups with field comments. These changes preserve user member names and executable statements.

Local helper functions use PascalCase with collision-safe numeric suffixes. Scalar and Union call projections retain their original operand syntax and annotations so the presentation pass can still prove which identifiers belong to generated locals; names observed through `nameof` remain unchanged.

Write-proof eligibility is memoized only within its projection's unchanged type and settings. Canonical runtime-profile checks use a weak compilation key and retain only a Boolean result; a new compilation always checks its own profile identities, including consumer-defined shadow types.

Final contract documentation is rendered as text changes limited to method-leading trivia. This avoids constructing XML syntax trees that would immediately be converted back to text, while preserving the exact comments, directives, indentation and executable source.

Root links use names relative to their containing serializer to avoid repeating its namespace on every overload. The compiler binds them to the same methods and emits complete canonical method identifiers in the documentation XML.

## Tests

| Project | Scope |
| --- | --- |
| `tests/LuminPack.Tests` | xUnit runtime tests. `LuminPackTestArea` selects `All` (default), `Safety`, `Simd`, or `Hooks` (the `netstandard2.1` asset with native hook probes). |
| `tests/LuminPack.Tests.Codegen` | Generated-code contracts with the SDK's Roslyn: XML docs, mechanisms, inlining, Union cursors, reuse, Union collections, external Unions, options. |
| `tests/LuminPack.Tests.Generator` | Generator pipeline with Roslyn 4.3: concurrency, emitter shapes, pruning modes, incremental invalidation (`--verify-incremental`). |
| `tests/LuminPack.Tests.CodeFixes` | Roslyn 4.3 workspace checks: real generator diagnostics, source edits, cross-document fixes, constructor selection, aliases, trivia and Fix All. |
| `tests/LuminPack.Tests.Aot` | Trimmed/NativeAOT smoke program and wire fixtures (`--wire verify`); with `-p:LuminPackPackageVersion=<v>` it runs against the packed NuGet packages. |
| `tests/LuminPack.Tests.Platforms` | Platform adapter contracts, selected with `LuminPackPlatform`. |
| `tests/Unity` | Unity Mono/IL2CPP smoke project. |

## Exceptions

Production throws are built in `LuminPackExceptionHelper`. Generated buffer-writing catch paths reset the buffer and rethrow through `ExceptionDispatchInfo`, preserving the original stack.
