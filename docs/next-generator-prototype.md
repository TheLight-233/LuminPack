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
Run the focused retained-driver contracts with
`dotnet run -c Release --project src/LuminPack.Generator.Next/benchmarks/GeneratorBenchmarks -- --verify-incremental`.
They compare generated source and compiler diagnostics with a fresh driver after call-location, member, root, operation,
assembly-option, assembly-identity, wrapper, declaration and unsafe-setting edits.
Use the same command with `--verify-input-limits` for checked-copy contracts: trusted inline descendants, List/array/Dictionary
elements, malformed markers and Booleans, root quotas and reuse across all six string profiles.
`--verify-documentation` checks caller-facing XML in all three compiler documentation modes, compiled documentation output,
unchanged executable tokens and retained-driver invalidation of layout-only and added/removed documentation facts.
`--verify-assembly-options` checks both option constructors, registration policy, all sixteen forced-operation combinations,
legacy mode values, library/friend retention, safe compilation and default JSON fallback input policies. Six application fixtures
exercise compiler interception, the installed IL rewriter with portable symbols, and registered fallback with both static
integrations disabled, each with and without a friend assembly declaration.
`--verify-external` compiles a library and a consumer that marks the library's types with `[assembly: LuminPackable(typeof(T))]`,
compares their bytes with the library's own operations in both directions, and expects LuminPack112 against a reference assembly.

## How calls bind

The generic binary and JSON facade, including `Register` and the registration-only `FormatterDispatch`, is post-initialization
source, so call sites bind against it while the generator runs. A syntax provider records each closed call as a value-equal fact:
overload, payload, closed or declared profile (or an open profile type parameter) and the compiler-owned interception location.
Interceptors implement the document boundary and call the published static root directly. Calls with an open payload and calls
without a published root stay on the generic facade and use registered dispatch.

Without compiler interception, a build that defines `LUMINPACK_IL_REWRITE` (the `LuminPackUseIlRewriting` build integration) gets
the same entries. Each interceptor then carries a build-only `LuminPack.Generated.StaticCall` mapping of the exact facade
construction, and `LuminPack.ILPostProcessor` replaces the compiled calls after compilation. Compiler interception needs C# 12, so
older language versions bind through this backend.

## Compatibility targets

For netstandard2.1, generated reads and writes address the one-element cursor span through `MemoryMarshal.GetReference`. They publish advances through `MemoryMarshal.CreateSpan`. The compatibility facade and operations compile with C# 9. Modern targets retain the `ref byte` cursor.

## How operations are retained

An application, or a Unity predefined assembly, without friend assemblies keeps only the operations its code calls, as
[generation modes](generation-modes.md) describes; every other project keeps all four. Each closed call keeps its operation on the
payload identities its payload type reaches through member types, generic arguments, base types, interfaces and attribute types, and
each reached declaration passes its operations on. A base type or interface also reaches the declarations derived from it, which
Union roots dispatch to. A shared formatter owner keeps each operation of the payloads and roots that require it. A facade call
without a concrete root, a registration, a direct writer or reader state call, a facade method group, a generated extension name
and `LuminPackGeneratorOptions.ForceGenerate*` keep their operation for every payload. A payload that keeps no operation publishes no
file, and one without a JSON operation publishes no JSON file. In every project, an interceptor entry keeps only the directions its
calls use.

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

## Completed migration areas

A checked item covers the scope stated here. Related combinations still listed below remain open.

- [x] **Facade XML documentation.** The public facade documents parameters, type parameters, returns, buffer ownership,
  reuse and stream lifetimes. Per-payload convenience overloads and their XML are out of scope.
- [x] **Assembly registration policy.** Both `LuminPackGeneratorOptions` constructors select the same `Auto`/`Disabled` behavior.
  Disabled assemblies publish no facade registration APIs and do not probe generic or Union registrations or reserve Union tags.
  Known Union cases retain their existing dispatch.
- [x] **Assembly operation-retention settings.** All sixteen `ForceGenerate*` combinations, ignored legacy mode values,
  library/friend retention and registration with unsafe code disabled are covered by the focused assembly matrix.
- [x] **Assembly fallback document boundaries.** Default generic JSON reads use the assembly's JSON input profile. Strict
  numeric syntax and encoded input caps are verified for UTF-8 and UTF-16, including a compilation with no attributed payload.
- [x] **Bounded ordinary-object checked copies.** Supported copies propagate their root policy through trusted inline records
  and payload elements of List, arrays and Dictionary. The six-profile fixture covers markers, Boolean domains, quotas, nulls,
  ordinary wire bytes and reuse; warmed reads into its complete reused target allocate no bytes. Other payload kinds remain below.
- [x] **Normalized incremental publication.** Collected facts compare by value, repeated closed roots share compilation-owned
  analysis, unchanged formatter requirements reuse their driver-owned catalog, and owner aggregation retains first declarations
  without LINQ grouping.
- [x] **Trusted Boolean scalar reads.** Ordinary and inline trusted fields use the advancing reader operation and retain Reader
  bounds checks; untrusted fields keep their Boolean-domain checks.
- [x] **Compatibility targets.** The Roslyn 4.3 compatibility fixture tests C# 9 and 10 with unsafe code disabled through both registered and IL-rewritten entries.
- [x] **Declaration member order.** Members without an explicit order follow declaration order everywhere, including nested
  records expanded in their parents. The legacy rule that put each level's auto-properties before its fields is intentionally
  dropped, so payloads of nested classes that mix auto-properties and fields differ from legacy bytes; the four benchmark fixtures
  are unaffected.
- [x] **Payloads nested in generic types.** A packable type declared inside a generic type, such as `Outer<T>.Inner` or
  `Outer<T>.Mid<U>.Leaf`, is a generic definition over the containing parameters followed by its own. Closed uses bind the
  definition's generic owner, and closed member types get owners named with their containing arguments, such as
  `OuterOfIntInner`. A fixture with classes, a struct, two nesting levels, member and list uses, binary and JSON round-trips through
  interception and IL rewriting. The legacy generator never supported this shape (LuminPack099). Union cases nested in generic types
  are still rejected. `GenericContainerPayloadTest` covers the shapes with the Next generator.
- [x] **Distinct names for repeated simple type names.** Owner, entry and alias names drop namespaces, so two types with one
  simple name in different namespaces shared an owner: their payload operations merged into one partial class, and collection
  owners such as `List<Point>` and `List<Other.Point>` collided, leaving one of them unpublished. A compilation fact now lists the
  simple names its declarations repeat across namespaces or share with common framework types, and those names keep their
  namespace (`OtherPointFormatter`, `ListOfOtherPointFormatter`). Other names are unchanged; the benchmark corpus output is
  identical.
- **Source generic methods.** Open facade calls in generic source methods use the registered fallback.
- [x] **Assembly-marked external roots.** A referenced type marked with `[assembly: LuminPackable(typeof(T))]`, with the declared
  cases of a marked Union, is a payload of the consuming assembly, as legacy `CompilationTypeAnalysis` and `ReferencedTypeMetadata`
  made it. A compilation that declares markers is analyzed through a view importing all metadata, so private, internal, included and
  automatic-property storage of the referenced type is reached through the layout mirror; internal storage of another assembly is
  mirrored rather than accessed. A reference assembly that omits backing storage reports LuminPack112. An imported Union root writes
  through the compiled dispatch its own assembly generated and reads its cases here, and declares nothing inside the imported types.
  Unlike legacy, internal fields of marked types compile, and marked types work as members of local payloads. `--verify-external`
  covers records, structs, an imported Union and an unattributed type through binary and JSON, in both directions.
  For ordinary external records, an open generic marker such as `typeof(Box<>)` permits closed formatter owners discovered through
  calls and members. A closed marker such as `typeof(Box<int>)` selects that construction alone. The fixture also exercises both
  marker forms in an application with operation pruning.
  A generic Union with explicitly declared closed cases supports both marker forms when the defining library also uses Next. Writes call the declaring assembly's
  compiled slots and reads use the consumer's generated case owners. The fixture checks binary and JSON, a collection member,
  and a non-generic case implementing a constructed interface. It also verifies LuminPack112 for closed generic records reached
  through open markers and the consumer's assembly options with imported type-level overrides.
- [x] **LuminPack021.** Nothing to migrate: the legacy generator has declared but not reported this warning since layout mirrors
  made `[LuminPackInclude]` storage reachable on every target, and Next likewise reports nothing.

## Output accounting

In repository builds, pass `-p:LuminPackReportGeneratorOutput=true -p:EmitCompilerGeneratedFiles=true`. Next writes
`Formatters/LuminPackGen.OutputReport.g.cs` under the compiler's generated-files directory. It counts final files and source size
by family, excluding itself, and selected operation directions for complete payloads and unambiguous shared roots. The report
also identifies the directions retained globally by project settings, direct API use and facade fallback.

## Not yet migrated

### Static binding and publication

| Priority | Gap | Current state |
| --- | --- | --- |
| P2 | Cross-assembly payloads | Imported Union cases discovered only in the declaring assembly's source, open generic cases, and the wider option and backend matrix still need coverage. Legacy libraries expose a different Union slot signature from the compiled Next slots checked by the current importer. Unmarked referenced payloads use registered fallback. |
| P2 | Remaining backend combinations | The modern application matrix executes state calls compiled in a separate friend assembly for all three binding modes. The Roslyn 4.3 harness executes C# 9 and 10 consumers with the netstandard2.1 runtime, covering IL rewriting with portable PDBs and registered fallback. Next uses flat source hints on older hosts that reject directory separators. A local NuGet candidate passes all three binding modes on .NET 11. The wider target matrix remains. On .NET 8 and later the generated code needs C# 13 (ref fields, `ref readonly` parameters, ref struct interfaces). |

### Type shapes and protocols

| Priority | Gap | Current state |
| --- | --- | --- |
| P2 | Generic reference tracking | Closed constructions used directly or as members get owners and keep reference identity, including cycles and repeated list elements (verified with `Node<string>`, `Node<int>` and `Node<double>` through interception). Open calls use registered fallback. Complex construction, ref-return rentals and additional schema-evolution and failure combinations still need coverage. |
| P2 | Version tolerance combinations | Generic and inherited combinations, compressed fields, former names, schema evolution and complete legacy cross reads are covered only as far as the tests reach. |
| P2 | Public annotations | `LuminPackableObject` selects the member's complete formatter for generated and registered reference payloads. The annotation fixture checks manual registration for a `NonGenerator` member and an unannotated interface type. When combined with `LuminPackCompress`, jagged arrays of known value types and lists of those arrays preserve the original element encoding and use a length-prefixed version-tolerant field. `--verify-alias-diagnostics` compares seven invalid name cases with the built legacy analyzer. The broader diagnostic location and precedence matrix remains open. |
| P2 | JSON compatibility | All envelopes, escaping, former names, duplicate and unknown properties, omitted members and failure publication are covered only as far as the tests reach. |
| P2 | Formatter behavior matrix | Comparers, duplicates, concurrent observation, failed prefixes, cleanup and older-runtime fallbacks across all families are covered only as far as the tests reach. |

### Input limits

| Priority | Gap | Current state |
| --- | --- | --- |
| P1 | Checked copies | Roots with declared bounds or a modeled dependency on trusted reads redirect known nested reads to root-owned copies. Generic declarations can apply that scope to statically known children, and published closed class and managed struct owners apply it after substitution. Fixtures exercise closed roots through interception and generated registration, including a profiled string limit. Forwarding array and list owners carry the root's limits through nested arrays, fused Boolean array reads and string lists while preserving reuse. Open type-parameter dispatch, opaque fallback and additional container combinations remain. |
| P1 | Uncovered payload kinds | Direct read-site checks cover versioned records, mirrored string storage, reference-tracked records and managed structs. Explicitly untrusted struct and Union roots reject input-selected shapes. An unbounded Union applies its domain checks to trusted fixed cases and supported own-entry cases. Reference-tracked Union cases inherit the Union's limits and use the selected record body. Local non-generic Union members and elements in arrays, lists and dictionaries inherit their enclosing record's limits, including fresh list and dictionary reads. Local closed generic Union members and list elements apply the same limits after case substitution and retain the definition's tag width. Imported Union members with explicit case metadata and modeled storage also inherit the enclosing limits, including closed members reached through an open assembly marker. Open generic member reads, source-only imported cases and the wider profile matrix remain. |
| P2 | Stream, async and compression | The binary async stream fixture checks the encoded cap and single-byte lookahead. The input-limit fixture checks the declared element cap on compressed arrays, each level of a compressed jagged array or list, and jagged fields read through version evolution. The remaining protocol and reuse combinations still need coverage. |

### Performance mechanisms

These preserve each selected format while generating different code from the legacy backend.

| Priority | Gap | Current state |
| --- | --- | --- |
| P1 | Nested record expansion | Legacy expands nested records inline, including records with mirrored members, callbacks, version tolerance or pooled construction; Next calls their owners. |
| P1 | Collection members in record bodies | Record bodies call the owners of collection members, such as a list or dictionary of Union values, where legacy expands those writes in place. |
| P1 | Adjacent fixed-field blocks | Ordinary fixed record runs use the 64-bit field-address proof and scalar fallback. Version-tolerant fields use their descriptors and checked reads. More layout and construction combinations still need comparison. |
| P2 | Unmanaged root sharing coverage | Next already shares eligible generated array and generic formatter bodies through unmanaged placeholders and binds closed owners to them. Check which remaining closed bodies lack a matching shared plan and compare their output with legacy before extending the shared families. |
| P2 | Root inlining policy | Allocating static entries write UTF-16 length-prefixed time zone snapshots and exact scalar `IList<T>`, `ISet<T>` and `IReadOnlySet<T>` implementations directly into the result array. Other profiles or implementations retain the pooled path. Binary `List<string>` writes and reads and `SortedSet<string>` reads keep the legacy `NoInlining` boundary. Scalar enumerable, collection and list interfaces use the JIT's default read policy and keep their outlined writes. Other collection and element combinations still need comparison. |
| P2 | JSON read allocations | A JSON read of the benchmark graph allocates about 2 KB more than legacy; the source is not identified. |

### Platforms and targets

| Priority | Gap | Current state |
| --- | --- | --- |
| P2 | Unity, IL2CPP, 32-bit, ARM64, AOT | The Windows .NET 10 smoke app and retained wire fixtures pass with Next in x64 JIT, trimmed and NativeAOT builds and a self-contained x86 JIT process. The build analyzer still warns about dynamic `System.Type` resolution. The netstandard2.1 and C# 9 consumer passes the Unity 6 Windows Editor integration check. IL2CPP players and the wider runtime and architecture matrix remain pending. |

The ASP.NET Core, Avalonia, Drawing, MAUI, MonoGame, SkiaSharp and WPF adapter contracts run with Next on Windows .NET 10.
The Drawing contract covers direct `System.Drawing.Color` members through the existing typed registration as well as the Custom wrapper.
The Unity extension script runs the Next-generated Unity consumer with the native Unity object and Burst kernel contracts.

```text
dotnet run --project tests/LuminPack.Tests.Platforms -c Release -p:LuminPackPlatform=Drawing -p:LuminPackGenerator=Next
pwsh -File .github/scripts/invoke-unity-extensions.ps1 -Project .tmp/unity-next -Artifacts .tmp/unity-next-results -LuminPackGenerator Next
```

### Diagnostics

| Priority | Gap | Current state |
| --- | --- | --- |
| P2 | Diagnostic semantics | The descriptors exist; conditions, precedence, counts, locations, related declarations, code fixes and incomplete IDE source are not compared with legacy. |

### Generator performance

| Priority | Gap | Current state |
| --- | --- | --- |
| P2 | Incremental work | Per-operation shared-owner dependency walks and other repeated model projections and semantic queries still need reduction. The completed normalization and cache work is listed above. |

### Acceptance

The AOT smoke project selects Next with `-p:LuminPackGenerator=Next` and keeps its build outputs in a separate directory.
Its 18 retained legacy binary fixtures pass both decoding and byte-for-byte write comparison. Fresh Legacy and Next builds also
verify each other's fixture output. Broader wire and diagnostic corpora, the remaining platform and language matrix, and fuzzing still need
validation. The local Next package installation fixture runs compiler interception, IL rewriting and registered fallback from a candidate NuGet feed.

```text
dotnet run --project tests/LuminPack.Tests.Aot -c Release -p:LuminPackGenerator=Next -- --wire verify tests/LuminPack.Tests.Aot/Fixtures
```

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
