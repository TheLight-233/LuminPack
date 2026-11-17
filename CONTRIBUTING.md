# Contributing to LuminPack

[中文](docs/CONTRIBUTING.cn.md)

Thanks for helping! Issues and pull requests are welcome. LuminPack 2.0 is being refactored on the `refactor` branch; base your work on it.

Before changing runtime or generator code, read the [mechanism contract](docs/mechanism-contract.md) (the rules every change must keep) and the [architecture](docs/architecture.md). Coding agents follow [AGENTS.md](AGENTS.md), which states the same rules.

## Building

The solution includes `net11.0` targets. Use .NET SDK 11.0.100 or a later .NET 11.0 feature band, as specified by `global.json`.

```text
dotnet build LuminPack.sln -c Release
```

With a .NET 11 SDK on x64 or Arm64 Windows, Linux or macOS, repository MSBuild builds run the SDK C# compiler in an isolated Satori GC runtime by default. Restore downloads the pinned experimental runtime pack. The build host, IDE design-time compilation and application runtime use their regular runtimes. To use the SDK compiler's regular runtime for a build, pass `-p:LuminPackUseSatori=false`.

## Checks

Run what covers your change rather than every suite. The full runtime suite is large and slow to compile; prefer one framework and a focused `--filter`.

```text
# Source standards: XML docs, English prose, exception routing, raw copy routing
dotnet run --project tools/LuminPack.RepositoryChecks/LuminPack.RepositoryChecks.csproj -c Release -- .

# Generated-code contracts (SDK Roslyn); add --verify-<area> to run one area
dotnet run --project tests/LuminPack.Tests.Codegen/LuminPack.Tests.Codegen.csproj -c Release

# Generator pipeline on Roslyn 4.3, then incremental invalidation
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release -- --verify-incremental
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release -- --verify-source-references

# Static entry backends, including real Roslyn 4.3 and the portable runtime
dotnet run --project tests/LuminPack.Tests.Codegen/LuminPack.Tests.Codegen.csproj -c Release -- --verify-interceptors
dotnet build src/LuminPack.Core/LuminPack.Core.csproj -c Release -f netstandard2.1 -p:CopyLocalLockFileAssemblies=true
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release -- --verify-il-rewriting

# IDE code fixes: real diagnostics, cross-document edits and Fix All
dotnet run --project tests/LuminPack.Tests.CodeFixes/LuminPack.Tests.CodeFixes.csproj -c Release

# Runtime tests, one framework, focused
dotnet test tests/LuminPack.Tests/LuminPack.Tests.csproj -c Release -f net10.0 --filter "FullyQualifiedName~ReaderFixedBoundsPolicyTest"

# Test areas with their own, smaller source sets
dotnet test tests/LuminPack.Tests/LuminPack.Tests.csproj -c Release -f net10.0 -p:LuminPackTestArea=Safety
dotnet test tests/LuminPack.Tests/LuminPack.Tests.csproj -c Release -f net10.0 -p:LuminPackTestArea=Simd
dotnet test tests/LuminPack.Tests/LuminPack.Tests.csproj -c Release -f net10.0 -p:LuminPackTestArea=Hooks

# Wire compatibility against the checked-in fixtures
dotnet run --project tests/LuminPack.Tests.Aot/LuminPack.Tests.Aot.csproj -c Release -- --wire verify tests/LuminPack.Tests.Aot/Fixtures

# Platform adapters (AspNetCore, Avalonia, Drawing, Maui, MonoGame, SkiaSharp, Wpf)
dotnet run --project tests/LuminPack.Tests.Platforms -c Release -p:LuminPackPlatform=Drawing

# Release candidate: packages, API comparison and installation (never publishes)
pwsh -File .github/scripts/release.ps1 -ApiBaseRef HEAD

# Deterministic malformed-input smoke tests
pwsh -File .github/scripts/fuzz.ps1
```

- Reader bounds changes: run `ReaderFixedBoundsPolicyTest` and `ReaderPreaccessBoundsTest` in Debug and Release, and `--verify-codegen-performance` in the codegen contracts.
- Compatibility refactors: write fixtures with the original build (`--wire write .tmp/wire-baseline`), then verify them with the new build. Keep temporary output under `.tmp`.
- Test areas and platform selections build into separate `obj/<area>` and `bin/<area>` folders.
- Runtime profile matrices use the test-only `LuminPackUnitTest.TestProfileSerializer` adapter. Keep it separate from the generated `LuminPack.LuminPackSerializer`; a handwritten partial declaration can hide missing generated members during IDE analysis. Ordinary serializer calls use the production API.
- Add new cases to the existing suites and matrices instead of creating a project or class per optimization.

What each workflow runs, and how to configure the Unity, AVX-512 and benchmark jobs, is described in [continuous integration](docs/ci.md).

## Benchmarks

Judge performance by LuminPack's time divided by the reference library's, from the same run on the same machine, with repeated samples, dispersion and allocations (section 5 of the [contract](docs/mechanism-contract.md)). Published numbers come from CI runs only.

The [BenchmarkDotNet suites](benchmarks/LuminPack.Benchmark) compare binary serialization with Nino, MemoryPack and MessagePack, and JSON with System.Text.Json, Newtonsoft.Json and RE:Dox.

```text
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --filter *SimpleBenchmark*
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --jit-mode both --filter *PolymorphismBenchmark*
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --verify-fixtures
```

`LUMINPACK_BENCHMARK_SATORI` selects the measured runtime: `1` or `only` measures only the Satori GC runtime prepared by `eng/SatoriCompiler`, `both` adds `TieredPGO-Satori` and `FullOpts-Satori` jobs next to the stock jobs, and `off` keeps the stock runtime. The host keeps using the installed SDK; each measured process launches its apphost against the private .NET 11 framework, and the run fails if Satori GC is not active. `--verify-runtime` prints the current process runtime, server-GC state and Satori-GC state.

The independent [serialization matrix](benchmarks/LuminPack.MatrixBenchmark) exercises framework types, model graphs and 51 collection families. Binary compares with MemoryPack and Nino, including Nino's official immutable collection formatters; JSON compares with System.Text.Json using source-generated metadata. Write returns a new byte array or string, Fresh reads into an empty target and Reuse reads into an existing target; System.Text.Json has no root Reuse operation. The report lists unsupported operations and operations that fail round-trip validation, and each reference has its own ratios from valid pairs in the same process. To validate a scenario locally, pass the format, scenario, round count and JSONL output path; zero rounds only validates.

```bash
dotnet run --project benchmarks/LuminPack.MatrixBenchmark -c Release -p:LuminPackUseSatori=false -- binary small 0 matrix.jsonl
```

### README charts

The README charts are rendered from a manual `ci.yml` run with `run_benchmarks` enabled. Download its artifacts under `.tmp`, then:

```text
python .github/scripts/analyze_benchmarks.py .tmp/benchmark-artifacts
python benchmarks/make-charts.py .tmp/benchmark-artifacts/benchmark-artifacts-linux-x64 docs/assets/benchmarks --job TieredPGO-Satori
python benchmarks/LuminPack.MatrixBenchmark/chart.py .tmp/benchmark-artifacts/benchmark-matrix docs/assets/benchmarks
```

`make-charts.py` draws one host and one JIT job; it requires `--job` when a report contains several and never merges their rows. `chart.py` needs a timed matrix artifact (validation-only runs have no samples). Update the machine table in both README editions with the run.

## Source conventions

- Follow `.editorconfig`: four spaces, UTF-8.
- English for code, comments, XML documentation, diagnostics, tests and maintenance documents. README files and `*.cn.md` editions are the exception. Write multilingual test strings with Unicode escapes.
- Every maintained source method, constructor, operator and conversion has XML documentation with a summary, parameters, type parameters and return value. Put comments before attributes. Generated code documents directly callable `LuminPackSerializer` APIs and the facade type; internal roots, forwarders and support declarations omit XML and suppress CS1591.
- Keep emitters in separate files; split large generators into partial files by responsibility, keeping type and member names.
- Generator changes go only to `src/LuminPack.Generator.Next`. The legacy generator (`src/LuminPack.Generator`) is frozen and no longer maintained: do not modify it, mirror Next changes into it, or keep its parity tests current.
- Put one statement on each line under `src` and do not use identifiers with two consecutive underscores; `tools/LuminPack.RepositoryChecks` enforces both. ChaCha20 and Poly1305 keep their rounds on one line each, and the public `MethodTable.__PInvoke` keeps its name.
- Throw through `LuminPackExceptionHelper` and keep exception types, argument names and cleanup. Use `Rethrow` to keep an original exception's identity.
- Changing names, member order, enum values, null markers, string modes, framing or storage views can break persisted data. Public members without call sites here can still be used by consumers or generated code.
- Keep third-party notices (`docs/xjb-LICENSE.txt`, `docs/xjb-NOTICE.md`) when editing the ported formatters.

## Documentation

- README is the project cover: introduction, installation, usage and links. No release notes, version histories, migration notices or experiment reports in README or `docs`.
- Every README, CONTRIBUTING and `docs` page has an English `name.md` and a Chinese `name.cn.md`. Update both. Chinese editions live under `docs`: root-page translations directly in `docs`, module README translations in `docs/modules/<module>.cn.md`, and feature translations beside their English guides. English documents keep their locations. `AGENTS.md` is English only.
- Update `skills/luminpack` when public usage changes.

### Contributor references

| Page | Covers |
| --- | --- |
| [Mechanism contract](docs/mechanism-contract.md) | Binding rules for runtime, generator and generated code |
| [Architecture](docs/architecture.md) | Projects, layers and how an operation is built |
| [Next generator](docs/generator-next.md) | The model-based generator, its output layout and package selection |
| [Next migration status](docs/next-generator-prototype.md) | Verified capabilities and remaining legacy work |
| [Next formatter contracts](docs/next-formatters.md) | Formatter models and element contracts |
| [Formatter porting](docs/generator-formatter-porting.md) | Registering and porting formatter families |
| [JSON name lookup](docs/json-name-lookup.md) | Generated property-name tables and XXH3 grouping |
| [Pooling](docs/pooling.md) | Reuse caches and retained memory |
| [Burst hooks](docs/burst-hooks.md) | The `netstandard2.1` native kernel table |
| [Fuzzing](docs/fuzzing.md) | The fuzz harness and campaigns |
| [Releasing](docs/releasing.md) | Release candidates, API governance and SourceLink |
| [Continuous integration](docs/ci.md) | Workflows, runners and benchmark jobs |

## Commit conventions

- Every commit title follows `type(scope): description`. Use lowercase types and component scopes, and a concise English imperative description without a trailing period.
- Types: `feat` (feature), `fix` (bug fix), `perf` (performance optimization), `refactor` (restructuring), `test` (tests), `docs` (documentation), `ci` (workflows), `build` (build tooling), and `chore` (maintenance). The performance prefix is `perf`, never `pref`.
- Choose the affected component as the scope, such as `core`, `generator`, `benchmarks`, `unity`, `burst`, or `repo`. Classify benchmark fixture changes by their purpose: `fix(benchmarks)` corrects a fixture, while `ci(benchmarks)` changes its CI execution or capture.
- Every commit has a substantive English body, separated from the title by a blank line. Explain why the change is needed, what changed, and the relevant validation and its results. State material limitations, failed checks and unverified items when applicable. Scale the detail to the change; title-only messages, repeated titles and generic boilerplate do not satisfy this requirement.
- Keep independent changes in separate commits. Mark breaking changes with `!` after the scope and explain them in a `BREAKING CHANGE:` footer.
- Examples: `perf(core): reduce fixed record cursor writes`, `fix(generator): reserve large union payloads`, and `ci(benchmarks): capture polymorphism JIT assembly on all hosts`.

## Pull requests

- Describe the problem, the change and the checks you ran, with their results. Performance claims need same-run ratios against the reference library.
- A change to wire bytes or to an established mechanism needs the maintainer's approval first; open an issue describing the proposal and its evidence.
