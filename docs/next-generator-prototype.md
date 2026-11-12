# Next generator migration status

[Chinese](next-generator-prototype.cn.md)

`src/LuminPack.Generator.Next` is the model-based successor of the legacy generator in `src/LuminPack.Generator`. This page records
how to run it, what is verified, and every legacy capability that is not migrated yet. Passing the unit tests only proves the shapes
the test corpus exercises; the inventory below is the authoritative list of remaining work.

Priorities: P1 items block ordinary use in some project or measurably regress performance. P2 items complete the migration. Promote
the smallest required part of a P2 item when an actual consumer depends on it.

## Running Next

The unit tests and the runtime benchmark project select the generator with `-p:LuminPackGenerator=Next` or
`-p:LuminPackGenerator=Legacy`. Combine the test selection with a test area, for example
`-p:LuminPackGenerator=Next -p:LuminPackTestArea=All -f net10.0`. A prebuilt legacy analyzer can be passed with
`-p:LuminPackLegacyAnalyzer=<path to LuminPack.Generator.dll>`.

```bash
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --verify-fixtures
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --verify-roundtrip
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --filter "*"
```

The generator benchmarks accept a real corpus and its references:
`GeneratorBenchmarks --corpus benchmarks/LuminPack.Benchmark --refs <benchmark output directory> --old-dll <legacy analyzer>`.

## How calls bind

The generic binary and JSON facade, including `Register` and the registration-only `FormatterDispatch`, is post-initialization
source, so call sites bind against it while the generator runs. A syntax provider records each closed call as a value-equal fact:
overload, payload, closed or declared profile (or an open profile type parameter) and the compiler-owned interception location.
Interceptors forward to per-payload `LuminEntry_` owners. Packable payloads call their `Serialize_`/`Deserialize_` roots; other
closed roots call the static `Write`/`Read` operations of their shared owners. Calls without a published root stay on the generic
facade.

## Verified state

- The legacy baseline is commit 8af1579, the last legacy commit that passes the full test area. With Next, the full net10.0 test
  area passes all 1135 tests. The net9.0 and net11.0 test targets have not been run with Next.
- The four benchmark fixtures produce byte-identical binary and JSON payloads under Next and the legacy generator, and
  `--verify-roundtrip` passes fresh reads, existing-target reads, declared and explicit profiles, `LuminBufferWriter`,
  `IBufferWriter<byte>` and JSON without registration.
- Every payload of the unit-test corpus is completely modeled; only Custom payloads are owned outside the payload renderers.
- The generated public method surface matches the legacy one by name, and the generic facade has the same overloads, including
  the async entries.
- Assembly comparison (FullOpts JIT disassembly of the full test run, both generators): over 4344 matching methods Next emits 0.992
  times the legacy code bytes and slightly fewer call instructions. The systematic differences are listed under performance below.

## Not yet migrated

### Static binding and publication

| Priority | Gap | Current state |
| --- | --- | --- |
| P1 | IL post-processing backend | `LuminPack.ILPostProcessor` rewrites calls marked with the generated `LuminPack.Generated.StaticCallAttribute` (legacy `StaticSerializerBinding.Il`). Next never emits it, so compilers without interceptors and Unity get no static binding; those calls stay on the generic facade. |
| P1 | Cross-assembly payloads | Next discovers only `[LuminPackable]` declared on types in the current compilation. Assembly-level `[LuminPackable(typeof(T))]` external roots, referenced metadata import (legacy `ReferencedTypeMetadata`, including diagnostic LuminPack112), external owners, external Unions and cross-assembly options are missing. |
| P1 | Call-based operation retention | Every payload publishes all four operations: the pipeline passes `GeneratedOperations.All` everywhere, although the emitters can suppress unused operations. Legacy reachability analysis and the operation filter, and `LuminPackGeneratorOptions.ForceGenerateSerialize`, `ForceGenerateDeserialize`, `ForceGenerateSerializeJson` and `ForceGenerateDeserializeJson` are not read. |
| P2 | Wrapper specialization of source generic methods | Legacy specializes calls inside user generic methods through generated wrappers (`StaticSerializerBinding.Wrappers`); Next closes those payloads through generic method expansion but does not reproduce the wrapper entries. |
| P2 | Remaining backend combinations | Friend assemblies, disabled-backend combinations and older language versions are not verified. |

### Generated source

| Priority | Gap | Current state |
| --- | --- | --- |
| P2 | XML documentation | Legacy documents generated members (`GeneratedSourceDocumentation`, `LuminPackSerializerDocumentation`). For the unit-test corpus legacy emits about 54,000 documentation lines, Next about 860. |
| P2 | Output accounting | Generated file counts, source size and retained operations are not recorded per build. |

### Type shapes and protocols

| Priority | Gap | Current state |
| --- | --- | --- |
| P1 | Payloads nested in generic types | A packable type declared inside a generic type, such as `Outer<T>.Inner`, is rejected with "a payload nested in a generic definition is not modeled yet". |
| P1 | Generic CircleReference | Only the closed constructions observed in the compilation get owners; the open definition that legacy publishes is missing, so other closed constructions have no generated path. Value-type shapes, complex construction, ref-return rentals, slot evolution and failure combinations are not verified. |
| P2 | Struct inline order | Legacy expands a nested member with each level's auto-properties before its fields. Next reproduces this order for classes through inline-order roots; nested structs keep declaration order. |
| P2 | Version tolerance combinations | Generic and inherited combinations, compressed fields, former names, schema evolution and complete legacy cross reads are covered only as far as the tests reach. |
| P2 | Public annotations | `NonGenerator`, `LuminPackableObject`, alias conflicts and the remaining assembly options are read but their complete semantics are not compared with legacy. |
| P2 | JSON compatibility | All envelopes, escaping, former names, duplicate and unknown properties, omitted members and failure publication are covered only as far as the tests reach. |
| P2 | Formatter behavior matrix | Comparers, duplicates, concurrent observation, failed prefixes, cleanup and older-runtime fallbacks across all families are covered only as far as the tests reach. |

### Input limits

| Priority | Gap | Current state |
| --- | --- | --- |
| P1 | Checked copies | Declared collection, string and depth limits are checked at the read sites of the root body, and nested reads under a bounded root run as checked copies. Only roots with a declared limit take checked copies; inline children inside a copied trusted payload keep their trusted marker reads. |
| P1 | Uncovered payload kinds | Versioned, mirrored, reference-tracked and struct payloads, rejected structs and Union roots are not covered by the limit paths. |
| P2 | Stream, async and compression | Stream and async calls stage at the payload's cap and compressed members use the compressed kernels; neither is verified by focused tests, nor are the remaining protocol, nested scope, count and reuse combinations. |

### Performance mechanisms

These keep the wire format but produce different code from legacy.

| Priority | Gap | Current state |
| --- | --- | --- |
| P1 | Nested record expansion | Legacy expands nested records inline, including records with mirrored members, callbacks, version tolerance or pooled construction; Next calls their owners. |
| P1 | Collection members in record bodies | Record bodies call the owners of collection members, such as a list or dictionary of Union values, where legacy expands those writes in place. |
| P1 | Adjacent fixed-field blocks | Legacy copies adjacent fixed fields as one block after field-address proofs; Next writes them field by field. |
| P1 | Unmanaged root sharing | Legacy shares closed-generic and array roots through substitution and binding proofs; Next publishes individual roots. |
| P2 | Root inlining policy | Several interface and collection roots that legacy keeps `NoInlining` are inlined into Next entries, and the time zone and exact-interface entry forms are not specialized. |
| P2 | JSON read allocations | A JSON read of the benchmark graph allocates about 2 KB more than legacy; the source is not identified. |

### Platforms and targets

| Priority | Gap | Current state |
| --- | --- | --- |
| P1 | Compatibility targets | The pointer-based reader of compatibility targets such as netstandard2.1 keeps managed input pinned in legacy (`PinnedReaderProjection`); Next has no equivalent and has only been run on .NET 10. |
| P2 | Unity, IL2CPP, 32-bit, ARM64, AOT | Not verified with Next. Unity also depends on the IL post-processing backend. |
| P2 | Optional modules | The platform and integration modules are not built or tested against Next. |

### Diagnostics

| Priority | Gap | Current state |
| --- | --- | --- |
| P2 | LuminPack021 | The warning for `[LuminPackInclude]` on targets without a runtime accessor is not reported. |
| P2 | Diagnostic semantics | The other 48 descriptors exist; conditions, precedence, counts, locations, related declarations, code fixes and incomplete IDE source are not compared with legacy. |

### Generator performance

| Priority | Gap | Current state |
| --- | --- | --- |
| P2 | Incremental work | The call list is an `ImmutableArray`, so the shared owner set is recomputed on every edit before its value-equal output is cached. Repeated model projections, semantic queries, collection copies and sorting are not reduced yet. |

### Acceptance

Legacy wire fixtures beyond the benchmark fixtures, diagnostic corpora, platform and language matrices, incremental equivalence and
fuzzing have not been run for Next. Packaging, build and IDE integration and the affected usage skills still describe the legacy
generator.

## Execution constraints

Read repository AGENTS, the mechanism contract, architecture and contribution guidance first. Preserve Analysis → immutable value
models → Render. Roslyn symbols remain inside analysis boundaries and caches with the corresponding lifetime.

Reuse migrated formatters and renderers. Select algorithms through open definitions, storage facts and capabilities. Default
element operations recursively call concrete static owners. Preserve the family-directory organization and specialization
extension points selected by the owner.

Every migrated feature retains the mechanism contract, including wire representation, Local + As views, reference tables,
construction and reuse, callbacks, typed delegates, layout, Release checks and state ownership. Judge performance parity by
comparing generated code and JIT disassembly with legacy before running benchmarks.

Whitespace and line layout may differ. Commit completed changes under repository conventions and preserve other contributors'
workspace changes.
