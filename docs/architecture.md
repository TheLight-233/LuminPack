# Architecture and maintenance boundaries

## Runtime and generator

`src/LuminPack` contains the binary and JSON readers and writers, size evaluation, operation state, collections, buffers, compression, cryptography, and mapping runtime. Unsafe storage views remain near the code that depends on their layout. Their field sequence and conditional layout are compatibility-sensitive.

`src/LuminPackGenerator` targets .NET Standard 2.0 and retains its existing Roslyn package compatibility. It analyzes consuming compilations and produces concrete formatter extensions, serializer overloads, union dispatch, layout mirrors, and mapper registration.

The runtime package depends on the separately packaged analyzer. The runtime does not execute that analyzer against itself. Each assembly owns an exception helper so neither needs an inverted dependency solely for throwing errors.

## Generator organization

| Component | Responsibility |
| --- | --- |
| `LuminPackSourceGenerator` | Registers incremental inputs and outputs. |
| `LuminPackSourceGenerator.Metadata` | Builds the ordered serialization model inside an isolated analysis scope. |
| `LuminPackSourceGenerator.Fields` | Analyzes member types, nested shapes, and inherited storage. |
| `LuminPackSourceGenerator.Constructors` | Selects and validates construction strategies. |
| `LuminPackSourceGenerator.TypeAnalysis` | Classifies symbols and discovers attributes and backing storage. |
| `LuminPackSourceGenerator.Unions` | Discovers union members and language union cases. |
| `LuminPackCodeGenerator.Serialization` / `Deserialization` | Emits binary field operations. |
| `LuminPackCodeGenerator.Evaluation` / `FieldSizes` | Emits size calculations and fixed-size expressions. |
| `LuminPackCodeGenerator.TypeNames` / `Unmanaged` | Resolves generated names and groups compatible unmanaged operations. |
| `LuminPackExtensionGenerator` parts | Builds formatter extensions, JSON support, union support, and dispatch. |
| `LuminPackSerializerGenerator` parts | Builds concrete and generic buffer, span, stream, JSON, compression, and size APIs. |
| `Api/SerializerApiUsageAnalysis` / `SerializerApiKind` | Resolves actual facade overload calls and generic formatter roots for Light and Minimal. |
| `SerializerApiFileCache` / `LuminPackSerializerGenerator.ApiSignatures` | Reuses guarded call bindings and analysis-only signatures without retaining old compilation symbols. |
| `CodeEmitters` | Implements scalar and collection-specific emission; independent emitters have independent files. |
| `Mapping` | Separates binding analysis, automatic/manual mapping analysis, generation, and metadata models. |
| `Incremental/Frozen*` | Stores comparable source, layout, registration, and diagnostic values at output boundaries. |
| `TypeEmissionCache` / `PackableGraphCache` | Reuses a root's analyzed model and owned type graph while its dependencies remain valid. |
| `TypeEmissionInput` | Compares validated dependency versions before Roslyn schedules per-type analysis. |
| `FileSemanticObservationCache` / `InvocationFileObservationCache` | Reuses per-file observations under project and complete source dependency guards. |
| `StableTypeIdentity` / `StableMethodIdentity` | Reconstructs verified current symbols from descriptions that can survive compilation replacement. |
| `SerializerFacadeCache` / `RegistrySourceCache` | Reuses concrete facade contributions and complete registry wrappers. |
| `PartitionedSourceCache` | Publishes stable concrete facade, layout, and no-owner formatter files without recopied aggregate bodies. |

Partial files preserve existing type identities and member signatures. They are organizational boundaries, not new runtime dispatch layers.

## Incremental generation boundaries

Generation separates current semantic analysis from frozen output values. `InitializeTypePipeline` compares each root's source, layout, and diagnostic contributions before source production. An unchanged value avoids another output callback; a dependency change still invokes the existing analysis and emitters. Serializer fragments, automatic/manual mapper results, custom registrations, and complete registry wrappers have separate reuse boundaries.

Before type analysis, the cache projects current guards into a comparable `TypeEmissionInput`. Equal dependency versions let Roslyn cache the analysis transform itself. Pending inputs keep weak references to the active upstream symbol and compilation; completed inputs keep only their version and frozen output. Successful analysis publishes the same version into its cache entry. This avoids retaining previous consumer compilations in unchanged pipeline values and preserves cancellation/retry behavior.

Cross-compilation entries retain descriptions, source tree identities, copied settings, and generated strings. Current `Compilation`, `SemanticModel`, and symbol resolution belong to compilation-local contexts. Stable identities preserve constructed owners, exact overload declarations, generic arguments and parameter owners, nullable/tuple/native distinctions, referenced assembly positions, and reduced extension receivers. Unsupported identities request fresh observation for the complete file.

Invalidation has two layers. Project guards cover references, assembly identity, language/compiler settings, global aliases, declaration lookup facts, and extension signatures. Source closures cover observed types, inference and binding targets, base/interface and member signatures, constraints, attributes, and partial declarations. A file is reusable only while its exact tree and every required source tree remain in the current compilation. Dependency observations must come from the actual current index; a fresh audit-only index must not stand in for generator execution.

Frozen diagnostics keep unique syntax tree identities and weak handles to their exact source trees as well as positions. Type diagnostic output uses these validated tree handles directly, so unrelated compilation edits do not rerun diagnostic output. Duplicate and empty file paths remain distinct; replaced diagnostic trees produce new values. Other reporting boundaries can still materialize against an explicit current compilation. Retained entries must not keep a previous consumer compilation alive. Cancellation and retry must preserve completed values without publishing partially analyzed results.

Existing Minimal mode additionally guards root reuse with the current emission decision. Invocation caching preserves all calls and their original order, including generic forwarding. The graph still executes for an edited compilation, and declaration discovery, current-symbol projections, and compilation-wide callbacks remain. These boundaries reduce repeated semantic analysis and output rendering; they do not imply that every upstream pipeline step is cached.

`IncrementalGenerationMetrics` provides internal execution counts so regression checks can distinguish avoided analysis from identical regenerated text. It is not a public API. Any later output splitting, generated API reduction, or attribute extension needs its own compatibility review.

`AggregateEmissionInput` provides the same comparable boundary before facade, dispatch, and registry construction. Its version includes the current ordered formatter/owner identities, custom declarations, pruned emission sets, mapper contributions, project settings, and complete formatter/custom source dependencies. Unrelated edits leave the aggregate transform cached; changed or unsupported facts request the existing operation. Pending inputs retain only a weak compilation handle. Failed aggregate construction is never published as a reusable version. Current semantic indexing and reachability checks still run upstream of this boundary.

## Generated file boundaries

Production emits `LuminPackSerializer.g.cs` for shared generic APIs and helpers, with independently cached partial files for concrete type overloads. Complete canonical type identities determine hash-suffixed filenames. Adding a type leaves existing concrete filenames and text stable. The public generator helper that returns aggregate facade source remains available for compatibility; the production pipeline does not assemble that large string.

No-owner formatter bodies have separate files. Generic registration and the five binary/JSON/size dispatch operations have fixed partial files, guarded by exact type ids and operation capabilities. Layout mirrors and private accessors are partitioned by their existing deduplication keys, retaining first-registration selection and exact storage field order. Source projection compares individual complete file values so unchanged files avoid another output callback.

Per-type support caching includes array and pointer shapes as well as named types. File wrapping and XML completion run only after a contribution's source or immutable rendering settings change. Per-type JSON helpers and their cached property-name members are emitted as `internal` rather than `private` so inlined entry bodies in sibling partition files can reference them. The partition regression checks exact executable declarations and XML against legacy composition, stable identities, shared layouts, inherited storage, no-owner enum changes, and source callback counts after independent edits. File partitioning adds small repeated wrappers and is not itself an IL-size optimization.

## Serializer API roots

The existing project or assembly generation mode controls concrete facade selection as well as formatter reachability. Full and Medium build no call-site usage index and preserve every legacy overload for each retained type. Light and Minimal retain the concrete overloads selected by project invocations and contextual method groups. Light additionally retains every annotated type's formatter graph; those attribute roots do not retain unused concrete facade APIs. Minimal can have an empty formatter root set, including on the first build.

All concrete parameter-overload shapes have shared generic fallbacks, including asynchronous writing, buffer-owned writers and JSON ref readers. Convenience generic reads and text conversions forward to shared reader cores; binary writes share one core. The existing generic type dispatch is emitted once per operation. Buffer-owned wrappers preserve the buffer's options and clear only operation state; convenience wrappers initialize and reset thread-static state.

Explicit generic calls and forwarding retain closed formatter dependencies without adding unused concrete overloads. External consumers can use every generic shape. Retained types keep their formatter operations, supporting nested members and external binary/JSON calls. A serializer expression whose contextual type is supplied by another source generator conservatively retains formatter roots while using generic fallback. It does not cause the entire concrete API surface to be emitted. Concrete JSON text entries contain their complete read operation and need no unused private span/ref overloads.

First-build binding uses an analysis-only facade extracted from the actual emitters' signatures. C# overload resolution handles aliases, static imports, named arguments, conversions, ref kinds, generic inference, and delegate conversions. This syntax tree never enters generated output. Its cache includes the complete concrete type/accessibility universe and generation settings. Per-file method identity reuse additionally requires the exact signature tree, complete project guard, and original semantic source dependencies. Concrete overload masks participate in aggregate and per-type facade guards, including call-only edits that retain the same payload type.

## Concrete binary entries

Concrete binary entries expand cached operation templates before documentation is added. A temporary semantic model binds only one type's existing emitter output; completed plans retain immutable syntax, canonical type names, passing conventions, and call slots. They retain no compilation, semantic model, or symbols. The same dependency guards used for type emission also protect operation reuse.

Each expansion receives independent local identities. Compiler-bound declarations and references are marked before instantiation, so repeated fields and nested collections cannot share locals, and source members that happen to use a generated naming prefix remain unchanged. Formatter returns become local exits. Ordinary static dictionary helpers and private-member accessors are expanded too. Private layout hierarchies are copied into the concrete partial with root-specific names and unchanged storage order. Reader span storage shares the reader's lexical scope while initializers stay at their original execution positions.

Generic packable templates come from the original generic definition and are then closed through type substitution. Reanalyzing a closed construction can change unmanaged classification and wire framing. Nullable annotations on unconstrained parameters must likewise remain annotations rather than introduce a new nullable-value wrapper. Generic fallback remains supported through the existing formatter path. Recursive and custom formatter calls retain a shared boundary.

Concrete binary, JSON, and size entries use these operation plans, including buffered asynchronous and sequence paths. JSON string and character-span entries keep their original transcoding rules and contain the read operation directly. Generated property lookup helpers become switch/conditional expressions, with each span-producing argument evaluated once. Literal getters become literal expressions. Lookup fields are relocated with their bound identities; only support declarations actually referenced by selected operations are retained.

Class/interface union entries keep one virtual forwarding hop: the concrete entry emits `value.__LuminPackUnionSerialize_{suffix}(ref writer)`, `value.__LuminPackUnionSerializeJson_{suffix}(ref writer)`, or the corresponding size call, and the case override expands that case's binary, JSON, or size operation. When any member is a constructed generic, guarded by `RequiresClosedGenericDispatch` in `LuminPackUnionDispatchCodeGenerator`, the override body additionally switches on `__LuminPackUnionDispatchSlot_{suffix}`, which is resolved once per suffix from the current type; non-generic members take the plain single-hop path. Closed registrations precede open generic fallbacks. Unknown and cross-assembly runtime types keep the registry-backed virtual dispatch path, preserving inherited implementations and runtime registration. Open union member graphs are closed before reachability expansion, subject to their generic constraints, so substituted nested formatter types remain available to generic fallback. Unbound generic attribute operands are never concrete dispatch candidates. Private mirror families have operation-specific identities because the same base layout reached through differently constrained generic roots can require different declarations.

The maintained operation regressions inspect all 14 concrete payload entry shapes, execute repeated nested/private/generic graphs, and compare retained generic contracts. Dedicated checks cover known union cases, callback order, embedded-child invalidation, concurrent facade compilations, and collection of old compilations while a plan cache survives. Broader runtime performance and final scale evidence remain separate from these structural and correctness checks.

## Documentation generation

`GeneratedSourceDocumentation` fills missing XML comments in emitted methods by inserting comment text at declaration boundaries. Executable tokens are retained.

Documentation parsing is enabled locally even when the consuming compilation uses `DocumentationMode.None`. This preserves existing comments and supplies the same rich overload documentation for ordinary builds, IDE parsing, and XML-documentation builds. The regression runs all three documentation modes. A consuming project can enable `GenerateDocumentationFile` when it needs to export the generated member documentation with its assembly.

`LuminPackSerializerDocumentation` adds overload-specific type and member details. It reuses the packable metadata analysis, so the documented contract follows the formatter's member filtering and ordering instead of independently guessing from public reflection shape. Per-type XML fragments are reused across overloads within one generation call.

The generated documentation distinguishes:

- CLR layout kind, explicit offsets, pack and size attributes when present.
- Unmanaged storage copying, including padding and otherwise ignored storage fields.
- Ordinary binary member order and variable-length or reference framing.
- JSON's filtered named members, which need not match copied unmanaged storage.
- Custom formatter-owned payloads and generic formatter dispatch.
- Caller-owned buffers and streams, operation state, merge/reuse behavior, and encoded-byte return units.

Type and member links use compiler documentation identifiers. Generic and asynchronous overloads retain their own parameter and return-value contracts.

## Exception boundary

Production throws are constructed in `LuminPackExceptionHelper`. Generated buffer-writing catch paths reset the buffer and rethrow through `ExceptionDispatchInfo` in the helper. The existing `finally` cleanup stays in place. This keeps validation failures centralized without changing successful serialization paths.

## Verification

`tools/LuminPack.RepositoryChecks` parses .NET Standard, modern .NET, and Unity conditional branches. It checks explicit method XML, XML syntax and parameter coverage, English repository prose, and production/generated throw locations.

`tests/LuminPackDocumentationTest` compiles generated output using SDK Roslyn and validates documentation against representative serialization contracts. `tests/LuminPackGeneratorConcurrencyTest` retains the generator's concurrent-compilation and emitter-shape checks. Its `--verify-incremental` option compares retained-driver output with fresh current generation, checks affected model and analysis-transform counts plus Roslyn tracked-step reasons, and exercises inferred return types, overload additions, aliases, unsupported invocation fallback, and cached/shifted diagnostics with duplicate or empty paths. The xUnit suite and wire fixture harness cover runtime behavior and persisted-data compatibility.

See [CONTRIBUTING](../CONTRIBUTING.md) for commands and review requirements.
