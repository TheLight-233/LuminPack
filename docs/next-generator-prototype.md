# Next generator prototype and migration priorities

[Chinese](next-generator-prototype.cn.md)

First deliver a Next prototype that works through ordinary serialization APIs, covers common object graphs, and supports runtime and generator benchmarks. Use practical workloads and measurements to improve performance and behavior while the complete migration continues.

P0 is the current high priority. Start the corresponding technical evaluation as soon as a complete call path runs. P1 covers the first improvements after it runs. P2 covers lower-priority complete migration work. Promote the smallest required portion of a later item to P0 when an actual consumer or selected benchmark depends on it.

## Handoff baseline

The repository is `C:\Users\28027\Desktop\C#\LuminPack-direct`. The generator project is `C:\Users\28027\Desktop\C#\LuminPack-direct\src\LuminPack.Generator.Next`. The handoff baseline is commit `aa24595`.

Existing sources include formatter family entries, default recursive element contracts, all 48 legacy diagnostic descriptors and validation entry points, and portions of construction, reuse, rental, callbacks, storage, version tolerance and reference tracking. Reuse these implementations and repair concrete integration gaps.

Baseline verification consists of source-only semantic diagnostics across 175 generator files with zero errors. Generated-consumer compilation, the complete diagnostic corpus and serialization regressions remain outstanding. Historical corpus results in the design document belong to their corresponding earlier revisions.

An existing uncommitted change in the legacy generator's `DirectJsonEmitter.cs` predates this work and retains its workspace state.

## Prototype scope

Start with .NET 10, matching the existing runtime benchmark project, and use the current modern cursor path. Cover at least these shapes.

- Ordinary classes, structs, enums, nullable values, common framework scalars and strings.
- Fields, automatic properties, readonly and Include storage, and ordinary inheritance.
- Arrays, lists, dictionaries, hash sets, existing common collection families and recursively nested elements.
- Closed generic payloads, Tuple, ValueTuple and Rest.
- Non-generic abstract-class and interface Unions, including Union elements in arrays, lists and dictionaries.
- Ordinary binary and JSON writes, allocating results, reads into existing targets and reusable-buffer entries.
- Default options and explicit profiles used by the selected benchmarks. Exercised unmanaged storage retains Native, Packed and Tagged contracts.

This initial scope is the priority selection made for this handoff. Add corresponding implementation and verification when extending the prototype to other runtimes or protocol combinations.

## Implementation order

1. First connect ordinary binary APIs, common object graphs and one representative consumer. Batch related implementation before integrated checks.
2. As soon as the first complete call path runs, start its runtime and generator benchmarks to obtain initial time and allocation measurements.
3. Add JSON, remaining ordinary entries and the non-generic Unions used by existing benchmarks. Expand representative consumers in batches.
4. Use practical workloads and measurements to order P1. Keep P2 as the complete migration scope.

Continue measuring working scenarios while completing further prototype scenarios in batches.

## Prototype status

The P0 entry layer is in place. A consumer that references `LuminPack.Generator.Next` calls the ordinary `LuminPackSerializer` API
and every closed call is bound to a Next body.

- The generic binary and JSON facade, including `Register` and the registration-only `FormatterDispatch`, is post-initialization
  source, so call sites bind against it while the generator runs.
- A syntax provider records each closed call as a value-equal fact: overload, payload, closed or declared profile (or an open profile
  type parameter) and the compiler-owned interception location.
- Interceptors forward to per-payload `LuminEntry_` owners whose bodies follow the established concrete entries. Packable payloads call
  their `Serialize_`/`Deserialize_` roots. Other closed roots are classified like a field of that type, join the shared owner
  requirements and call the owner's static `Write`/`Read` operations. Calls without a published root stay on the generic facade.
- Compiler interception is the only static binding backend so far. IL rewriting, older compilers and other backends remain P2.

### Running the prototype

The runtime benchmark project selects Next by default and prints the generator it was built with. BenchmarkDotNet child builds receive
the same selection.

```bash
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --verify-fixtures
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --verify-roundtrip
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --filter "*"
```

The legacy baseline uses `-p:LuminPackGenerator=Legacy`. On this branch the legacy generator project does not compile, so a prebuilt
legacy analyzer is passed with `-p:LuminPackLegacyAnalyzer=<path to LuminPack.Generator.dll>`. Next and prebuilt-analyzer builds pass
`LuminPackLegacyGeneratorReference=false` to `LuminPack.Core`, which skips its packaging reference to the legacy project.

The unit tests select Next with `-p:LuminPackGenerator=Next`; the default stays Legacy. Combine it with a test area, for example
`-p:LuminPackGenerator=Next -p:LuminPackTestArea=Safety -f net10.0`.

The generator benchmarks accept a real corpus and its references:
`GeneratorBenchmarks --corpus benchmarks/LuminPack.Benchmark --refs <benchmark output directory> --old-dll <legacy analyzer>`.

### Verified state

- The benchmark corpus and the unit-test Safety corpus compile with Next alone with zero generated errors.
- The four benchmark fixtures produce byte-identical LuminPack binary and JSON payloads under Next and the legacy generator.
- `--verify-roundtrip` passes fresh reads, existing-target reads, declared and explicit profiles, `LuminBufferWriter`,
  `IBufferWriter<byte>` and JSON without any registration.
- The Safety test area passes 87 of 94 tests under Next.

### Recorded gaps

- Declared input options are ported; with Next, the Safety test area passes 92 of 94. The registered dispatch follows
  the assembly declaration; each concrete root freezes its own input cap and checks nothing when trusted; explicitly untrusted
  roots read JSON with the strict profile and fail closed on input-selected shapes; declared collection, string and depth limits
  are checked at the read sites of the root body, and nested reads under a bounded root run as checked copies of their entries.
  Known differences: only roots with a declared collection, string or depth limit take checked copies; inline children inside a
  copied trusted payload keep their trusted marker reads; versioned, mirrored, reference-tracked and struct payloads, rejected
  structs and Union roots are not covered.
  Stream and async calls bind to concrete entries that stage at the payload's cap; compressed members use the compressed kernels.
  Both are not yet verified by tests.
- The legacy baseline is commit 8af1579 (the last legacy commit that passes the full test area). With Next, the full test area
  passes 1109 of 1135. The remaining failures each map to a legacy algorithm that is not ported yet: the in-place Union-slot read of
  existing dictionaries, string reuse inside tuple and pair arrays, the inline child schema that orders auto-properties first, sorted
  list reuse and validation, per-string limits in nested string matrices, the immutable queue and stack writes, the concurrent stack
  JSON order, the time zone snapshot, wrapper specialization of source generic methods, and custom-profile interface dictionaries.
- Assembly comparison (FullOpts JIT disassembly of the full test run, both generators): over 4284 matching methods, including 2096
  test methods whose source is identical, Next emits 0.988 times the legacy code bytes. Two systematic differences remain. Next
  calls the owner of a nested record where legacy expands it inline (records with mirrored members, callbacks, version tolerance or
  pooled construction), and Next inlines several interface and collection roots into their entries where legacy kept them
  `NoInlining`. Adjacent fixed fields that legacy copies as one block after field-address proofs are written field by field.
- Closed generic payloads bind the generic roots of their definitions. Payloads whose readable names coincide get numbered entries.
- Generic Union roots, closed and open generic cases, and Union roots with a declared string profile are ported; every payload the
  original generator emits for the unit-test corpus now has Next output. None of this is verified by tests yet.
- Struct cases of interface Union roots write and read through the case's payload owner; their bytes have not been compared with
  the original output.
- The compressed `List<string>` read still uses the caller's reader; the ordinary root publishes its cursor only on success.
- Record bodies call the owners of collection members, such as a list or dictionary of Union values, where the original pipeline
  expands those writes in place. Writing such records is slower than the original pipeline (P1 fast paths).
- A JSON read of the benchmark graph allocates about 2 KB more than the original pipeline; the source is not identified yet.
- The call list is an `ImmutableArray`, so the shared owner set is recomputed on every edit before its value-equal output is cached.
- Other target frameworks and the legacy generator's compile errors were not addressed.

## P0 high priority

### Ordinary API entries and static binding

- [x] Connect ordinary binary and JSON `LuminPackSerializer` APIs to concrete roots generated by Next.
- [x] Cover inferred payload calls, explicit payload calls, and explicit payload plus profile calls.
- [x] Connect allocating results, caller-owned `LuminBufferWriter` entries and reads into existing targets.
- [x] First integrate modern compiler interception or existing concrete static entries sufficient for these calls. Complete backend combinations continue under P2.
- [x] Supply the type discovery, closed-operation discovery, owner publication and support declarations required by these entries.

Completion means a representative consumer can use ordinary APIs and their calls can be traced to Next-generated bodies.

### Common object graphs and recursive dependencies

- [ ] Close analysis-to-rendering gaps for ordinary objects, inherited objects, automatic properties, readonly fields and Include members.
- [ ] Publish dependencies of ordinary closed generics, arrays, collection elements, dictionary components and Tuple/Rest.
- [ ] Use default recursive contracts for combinations such as `ConcurrentQueue<List<int[]>>` and `Dictionary<string, List<MyClass>>`.
- [ ] Resolve recursion and depth limits encountered by representative graphs. Complex generic recursion continues under P1.
- [ ] Publish complete models, mirrors, roots and child owners for every shape inside prototype coverage.

Completion means another supported element combination can reuse the existing container implementation and static child operations.

### Non generic Unions and existing benchmark models

Existing benchmarks depend on inheritance and interface polymorphism. `SimpleBenchmark` uses inheritance, the Packed `Transform` struct and a dictionary of Union values. `SerializeBenchmark` and `JsonBenchmark` use the `CharacterSaveData` graph. `PolymorphismBenchmark` uses abstract-class roots, interface roots and Union collections.

- [x] Complete discovery, tags, virtual dispatch, case operations and dependencies for those non-generic Unions.
- [x] Cover `List<IFoo>`, `Dictionary<int, IFoo>` and inherited members.
- [ ] Connect construction, Local mirrors, fixed cursors and profiles required by actual cases.
- [x] Preserve existing narrow/wide tags, tiered writing and field-layout contracts.

Completion means those benchmark models run with their existing data structures. Their Union and Packed dependencies are P0.

### Correct behavior inside prototype coverage

- [ ] Preserve nulls, empty collections, member order, array shape and ordinary JSON representations.
- [x] Preserve ordinary construction, target reuse, buffer growth and state publication.
- [ ] Retain default Reader validation, Boolean checks, existing capacity checks and stack guards.
- [x] Connect the `LuminPackUtf16LengthOptions` and `LuminPackDefaultOptions` calls used by the benchmarks.
- [ ] Check standalone and collection representations of exercised Native, Packed and Tagged types.
- [x] Keep ordinary calls on their concrete static path. Explicit and Custom registrations retain the established typed-delegate boundary.

Complete diagnostic edge cases and protocol combinations continue under P2. Fix generation errors and behavior errors encountered by the prototype here.

### Runtime benchmark integration

The runtime benchmark project currently references the legacy generator analyzer. Provide an identifiable Next selection and retain a way to run the legacy baseline.

- [x] Identify the analyzer and generated bodies actually used by a benchmark run.
- [x] First run `SimpleBenchmark`, then connect `SerializeBenchmark`, `JsonBenchmark` and `PolymorphismBenchmark`.
- [x] Reuse existing models, seeds, input data, profiles and benchmark configuration.
- [x] Use the ordinary application call path in benchmarks.
- [x] Record elapsed-time ratios to reference implementations, repeated-sample dispersion, allocations and payload size under the same machine, input and runtime conditions in the same run.

Completion means Next serialization and deserialization can be measured repeatedly with an identifiable call path and input contract.

### Generator benchmarks and integrated checks

- [x] Connect existing GeneratorBenchmarks and record full-generation, unrelated-edit and member-edit time and allocations separately.
- [ ] Record generated file counts, source size and retained operations.
- [x] Batch a related group of P0 implementation, then build Next and representative consumers together and fix blocking compilation and basic read/write failures.
- [x] Perform limited basic read/write, target-reuse and legacy-format interoperability checks on representative models before measuring them.
- [x] Keep the target, payload, profile and generator identity with each measurement.

Record source diagnostics, consumer execution, runtime benchmarks and generator benchmarks separately. The prototype can support technical evaluation while complete migration and broad platform regression work continue.

## P1 first improvements after the prototype runs

| Work | Scope | Completion |
| --- | --- | --- |
| Generator allocation and incremental performance | Reduce repeated model projections, semantic queries, collection copies and sorting. Check unrelated edits and child-type invalidation. | Repeated measurements, with retained-driver output matching fresh output. |
| Legacy unmanaged root sharing | Port closed-generic and array root sharing, substitution proofs, binding proofs and retained entries. | Restore the existing sharing mechanism while failed candidates retain individual roots. |
| Call-based operation retention | Connect four independent operation masks, dependencies and ForceGenerate settings. | Applications retain required operations and libraries retain their complete surface. |
| Complex generics and recursion | Payloads nested in generic enclosing types, deeper graphs and mutual recursion. | Expand coverage through complete contracts with correct analysis termination. |
| Declared profile propagation | Member scopes in specialized inline collections and common nondefault string profiles. | Different entries retain the same declaration options. |
| Explicit input limits | Port the limits and recursive-depth checks enforced by the old implementation and carry root constants to read sites. | Preserve check placement, defaults and state ownership. |
| Practical coverage of existing payload protocols | Common VersionTolerant and CircleReference shapes, construction, rental and callback combinations. | Representative use runs with focused exception and publication checks. |
| Common Custom use | Required registration APIs, automatic registration and missing-operation exceptions. | User types register and execute with their established signatures. |
| Existing formatter fast paths | Review traversal, reuse, snapshots, builders and pools reached by actual prototype or benchmark workloads. | Preserve algorithms, cleanup and observable behavior. |

Order P1 work using practical usability and benchmark results. Fix an item immediately when it blocks actual use.

## P2 lower priority complete migration

| Work | Remaining scope |
| --- | --- |
| Generic Unions | Generic roots, closed cases, argument mapping, constraints and cached slots after virtual dispatch. |
| Referenced payloads | Complete metadata import, rebinding, external owners, external Unions and cross-assembly options. |
| Complete CircleReference coverage | Generic and value-type shapes, complex construction, ref-return rentals, slot evolution and failure combinations. |
| Complete version tolerance | Generic/inherited combinations, compressed fields, former names, schema evolution and complete legacy cross reads. |
| Complete facade surface | Streams, async, compression, remaining public entries, exact sizing and exception lifecycles. |
| Complete static binding backends | IL postprocessing, older compilers, source-generic wrappers, friend assemblies and disabled-backend combinations. |
| Complete platform support | netstandard2.1 pointer cursors, Unity, IL2CPP, 32-bit, ARM64, AOT and optional modules. |
| Complete diagnostic semantics | Conditions, precedence, counts, locations, related declarations, code fixes and incomplete IDE source for all 48 diagnostics. |
| Public annotations and project options | NonGenerator, LuminPackableObject, alias conflicts and remaining assembly configuration. |
| Full formatter behavior matrix | Comparers, duplicates, concurrent observation, failed prefixes, cleanup and older-runtime fallback across all families and combinations. |
| Complete JSON compatibility | All envelopes, escaping, former names, duplicate/unknown properties, omitted members and failure publication. |
| Complete input and compression combinations | Protocols, nested scopes, counts, limits, exceptions and reuse combinations. |
| Distribution and documentation | Analyzer/package, build and IDE integration, paired documentation and affected usage skills. |
| Complete acceptance | Legacy wire fixtures, diagnostic corpora, platform/language matrices, incremental equivalence and relevant fuzzing. |

Additional cross-language generators, extra formats, ASP.NET product extensions and the next scalar/string/LuminPackable generic design can enter later product planning.

## Prototype completion

- [x] An ordinary consumer can call the Next public API.
- [ ] Common classes/structs, inheritance, automatic properties, closed generics, collections and nested elements generate complete bodies and run.
- [x] The non-generic Unions and Packed types used by existing benchmarks run.
- [x] Ordinary binary and JSON writes, reads, existing targets and reusable-buffer entries run.
- [x] Runtime and generator benchmarks execute with identifiable Next output.
- [x] Basic validity checks are complete and actual coverage and outstanding gaps are recorded.

At this point, use the prototype for measurement and improvement while expanding coverage through P1 and P2.

## Claude execution constraints

Read repository AGENTS, the mechanism contract, architecture and contribution guidance first. Preserve Analysis → immutable value models → Render. Roslyn symbols remain inside analysis boundaries and caches with the corresponding lifetime.

Reuse migrated formatters and renderers. Select algorithms through open definitions, storage facts and capabilities. Default element operations recursively call concrete static owners. Preserve the family-directory organization and specialization extension points selected by the owner.

Implement a related group of P0 gaps before integrated checks. Turn compilation failures, behavior errors and measurements into concrete fixes. Make each completed group of ordinary use cases runnable and measurable promptly.

Priority controls implementation order. Every feature admitted into prototype coverage retains the mechanism contract, including wire representation, Local + As views, reference tables, construction/reuse, callbacks, typed delegates, layout, Release checks and state ownership.

Whitespace and line layout may differ. Commit completed changes under repository conventions and preserve other contributors' workspace changes.
