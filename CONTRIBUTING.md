# Contributing to LuminPack

[中文](docs/CONTRIBUTING.cn.md)

Read the [mechanism contract](docs/mechanism-contract.md) and the [architecture](docs/architecture.md) before changing runtime or generator code.

## Building

The solution includes `net11.0` targets. Use .NET SDK 11.0.100 or a later .NET 11.0 feature band, as specified by `global.json`.

```text
dotnet build LuminPack.sln -c Release
```

With a .NET 11 SDK on x64 or Arm64 Windows, Linux or macOS, repository MSBuild builds run the SDK C# compiler in an isolated Satori GC runtime by default. Restore downloads the pinned experimental runtime pack. The build host, IDE design-time compilation and application runtime use their regular runtimes. To use the SDK compiler's regular runtime for a build, pass `-p:LuminPackUseSatori=false`.

## Checks

Run what covers your change rather than every suite. The full runtime suite is large; prefer one framework and a focused `--filter`.

Runtime profile matrices use the test-only `LuminPackUnitTest.TestProfileSerializer` adapter. Keep these helpers separate from the generated `LuminPack.LuminPackSerializer`; a handwritten partial declaration can hide missing generated members during IDE analysis. Ordinary serializer calls must continue to use the production API.

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
```

- Reader bounds changes: run `ReaderFixedBoundsPolicyTest` and `ReaderPreaccessBoundsTest` in Debug and Release, and `--verify-codegen-performance` in the codegen contracts.
- Compatibility refactors: write fixtures with the original build (`--wire write .tmp/wire-baseline`), then verify them with the new build. Keep temporary output under `.tmp`.
- Test areas and platform selections build into separate `obj/<area>` and `bin/<area>` folders.
- Add new cases to the existing suites and matrices instead of creating a project or class per optimization.

## Continuous integration

- [Release validation and API governance](docs/releasing.md): `pwsh -File .github/scripts/release.ps1 -ApiBaseRef HEAD` builds unpublished candidates, validates package/SourceLink metadata, compares public APIs and verifies installation. `release.yml` never publishes packages or creates tags/releases.

- [Fuzzing](docs/fuzzing.md): `pwsh -File .github/scripts/fuzz.ps1` runs deterministic malformed-input smoke tests. `fuzz.yml` also runs bounded SharpFuzz/libFuzzer campaigns on push/PR, weekly and manually, retaining corpora and reproducers.

- `ci.yml` runs repository checks, the build, the codegen and generator contracts, incremental verification, the runtime suite, the hook area, wire fixtures and an installation test from freshly packed NuGet packages.
- `test-areas.yml` runs the Safety area in Debug and Release on Windows, Linux and macOS. It executes AspNetCore, Avalonia, Maui, MonoGame and SkiaSharp adapter contracts on all three systems, plus Drawing and Wpf on Windows. Core is built before adapters because they consume its existing binary.
- Pushes to `main` and `refactor`, and pull requests, trigger the hosted checks. Unity execution requires configured license secrets; the checked-in `.github/scripts` helpers are part of those workflows.
- `runtime-compatibility.yml` runs trimmed and NativeAOT programs on Windows, Linux and macOS (x64 and ARM64), scalar/SSSE3/AVX2 fallbacks and x86. AVX-512 hardware tests run automatically on pushes and same-repository pull requests, and are enabled by default for manual runs; uncheck `run_avx512` to opt out of a manual run, or select `avx512_only` to run only these hardware checks. Four `ubuntu-latest` candidates probe for real AVX-512F/BW by default; every supported candidate runs the tests, and the hardware check fails if a candidate fails or is cancelled. If no candidate supports the features, the check succeeds with a skipped-coverage notice and summary; it does not claim hardware validation. GitHub does not guarantee that a supported host will be available. Set the repository variable `LUMINPACK_AVX512_RUNNER` to an AVX-512-capable Linux x64 runner label, such as `avx512`, to pin a dedicated runner. Fork pull requests do not execute this job, including when a self-hosted runner is selected. Vendor and ARM32 runs remain manual and need their labeled self-hosted runners.
- Configure `UNITY_EMAIL`, `UNITY_PASSWORD` and `UNITY_SERIAL` for online activation of the Linux hosted Unity workflow on every branch push and same-repository pull requests. A Personal serial can be extracted from its local `.ulf` using the GameCI activation documentation; the existing `UNITY_LICENSE` may remain configured. A usable `UNITY_LICENSE` can also be used in place of a serial. Missing secrets fail the license check. Runs share one license and queue up to 100 pending runs without cancelling an earlier run. Unity 2022.3.51f1 and 6000.0.43f1 run Editor tests, an IL2CPP smoke player, Unity object and Burst Editor contracts, and a WebGL IL2CPP smoke player in headless Chrome sequentially using one license. Fork pull requests skip license-backed jobs. Manual native Windows and macOS player checks still require prelicensed self-hosted runners; hosted Linux does not verify those platforms.
- The BenchmarkDotNet job in `ci.yml` runs through the manual `run_benchmarks` input and selects `SimpleBenchmark`, `SerializeBenchmark`, `JsonBenchmark` and `PolymorphismBenchmark`.
- The BenchmarkDotNet job uses Linux x64, macOS Intel x64 and Linux ARM64 hosts. It checks the process and OS architecture and the Intel CPUID vendor before timing. Each host uploads its own results and metadata. Compare library ratios within a host.
- The manual `benchmark_jit` choice defaults to `both`: separate `TieredPGO` (tiered compilation and dynamic PGO enabled) and `FullOpts` (both disabled) jobs. FullOpts is a JIT stability control, not a substitute for production PGO results. Each job uses three processes, 8–30 adaptive warmup iterations and 15 measurements targeting one second each, retaining outliers. Fixed seeds and timestamps keep input bytes identical across processes; `--verify-fixtures` checks fresh instances and repeated setup before timing.
- Full JSON and measurement CSV artifacts retain each process. `process-stability.md` and the job summary report process means, CV, max/min, allocations and LuminPack/reference ratios within the same operation and JIT job. A process spread above 10% produces a warning; inspect the distribution and native code before claiming a win. Independently launched libraries are not paired by process index.
- BenchmarkDotNet polymorphism disassembly runs on Windows/Linux; the installed version does not support CoreCLR disassembly on macOS. Separate, untimed JIT probes capture optimized LuminPack and Nino polymorphism read/write assembly and generated-root metadata on the Linux benchmark hosts. The Intel macOS host runs all four timing and allocation fixtures without these probes. Benchmark validation, execution or required probe failures fail the job.
- CI concurrency is scoped by event so a push does not cancel a manually started benchmark run. Registry worker tests launch their adjacent apphost to preserve the test process architecture, including self-contained x86.

## Benchmarks

The independent [serialization matrix](benchmarks/LuminPack.MatrixBenchmark) exercises framework types, model graphs and 51 collection families. The [Benchmark matrix workflow](.github/workflows/benchmark-matrix.yml) validates round trips for relevant pushes and pull requests. For timing and allocation reports, dispatch it manually or run CI with `run_benchmarks` enabled.

To validate a matrix scenario locally, build its Release executable and pass the format, scenario, round count and JSONL output path. Use zero rounds for validation only.

```bash
dotnet run --project benchmarks/LuminPack.MatrixBenchmark -c Release -p:LuminPackLegacyGeneratorReference=false -p:LuminPackUseSatori=false -- binary small 0 matrix.jsonl
```

Binary compares with MemoryPack and Nino, including Nino's official immutable collection formatters. JSON compares with System.Text.Json using source-generated metadata. Write returns a new byte array or string. Fresh reads into an empty target and Reuse reads into an existing target. System.Text.Json has no equivalent root Reuse operation. The report lists unsupported operations and operations that fail round-trip validation. Each reference has its own ratios, computed from valid pairs in the same process.

```text
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --filter *SimpleBenchmark*
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --jit-mode both --filter *PolymorphismBenchmark*
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --verify-fixtures
python .github/scripts/analyze_benchmarks.py .tmp/benchmark-artifacts
python benchmarks/make-charts.py .tmp/benchmark-artifacts .tmp/benchmark-charts --job TieredPGO
```

Judge results by LuminPack's time divided by the reference library's, from the same run on the same machine; see section 5 of the contract.
Binary tables use MemoryPack as the baseline within each operation; JSON tables use System.Text.Json. Chart generation requires `--job` when reports contain multiple JIT jobs, and never merges their rows.
`LUMINPACK_BENCHMARK_SATORI` selects the measured runtime: `1` or `only` measures only the Satori GC runtime prepared by `eng/SatoriCompiler`, `both` adds `TieredPGO-Satori` and `FullOpts-Satori` jobs next to the stock jobs, and `off` keeps the stock runtime. The host keeps using the installed SDK; each measured process launches its apphost against the private .NET 11 framework, and the run fails if Satori GC is not active. `--verify-runtime` prints the current process runtime, server-GC state and Satori-GC state. The manual `benchmark_satori` CI input defaults to `only`; select `off` or `both` for stock-only or same-run comparisons. Use the `-Satori` job id with `--job` when charting a Satori run.

## Source conventions

- Follow `.editorconfig`: four spaces, UTF-8.
- English for code, comments, XML documentation, diagnostics, tests and maintenance documents. README files and `*.cn.md` editions are the exception. Write multilingual test strings with Unicode escapes.
- Every maintained source method, constructor, operator and conversion has XML documentation with a summary, parameters, type parameters and return value. Put comments before attributes. Generated code documents directly callable `LuminPackSerializer` APIs and the facade type; internal roots, forwarders and support declarations omit XML and suppress CS1591.
- Keep emitters in separate files; split large generators into partial files by responsibility, keeping type and member names.
- Put one statement on each line under `src` and do not use identifiers with two consecutive underscores; `tools/LuminPack.RepositoryChecks` enforces both. ChaCha20 and Poly1305 keep their rounds on one line each, and the public `MethodTable.__PInvoke` keeps its name.
- Throw through `LuminPackExceptionHelper` and keep exception types, argument names and cleanup. Use `Rethrow` to keep an original exception's identity.
- Changing names, member order, enum values, null markers, string modes, framing or storage views can break persisted data. Public members without call sites here can still be used by consumers or generated code.
- Keep third-party notices (`docs/xjb-LICENSE.txt`, `docs/xjb-NOTICE.md`) when editing the ported formatters.

## Documentation

- README is the project cover: introduction, installation, usage and links. No release notes, version histories, migration notices or experiment reports in README or `docs`.
- Every README, AGENTS, CONTRIBUTING and feature guide has an English `name.md` and a Chinese `name.cn.md`. Update both. Keep all Chinese editions under `docs`: root-page translations directly in `docs`, module README translations in `docs/modules/<module>.cn.md`, and feature translations beside their English guides. English documents keep their existing locations.
- Update `skills/luminpack` when public usage changes.

## Commit conventions

- Every commit title follows `type(scope): description`. Use lowercase types and component scopes, and a concise English imperative description without a trailing period.
- Types: `feat` (feature), `fix` (bug fix), `perf` (performance optimization), `refactor` (restructuring), `test` (tests), `docs` (documentation), `ci` (workflows), `build` (build tooling), and `chore` (maintenance). The performance prefix is `perf`, never `pref`.
- Choose the affected component as the scope, such as `core`, `generator`, `benchmarks`, `unity`, `burst`, or `repo`. Classify benchmark fixture changes by their purpose: `fix(benchmarks)` corrects a fixture, while `ci(benchmarks)` changes its CI execution or capture.
- Every commit must have a substantive English body, separated from the title by a blank line. Explain why the change is needed, what changed, and the relevant validation and its results. State material limitations, failed checks and unverified items when applicable. Scale the detail to the change; title-only messages, repeated titles and generic boilerplate do not satisfy this requirement.
- Keep independent changes in separate commits. Mark breaking changes with `!` after the scope and explain them in a `BREAKING CHANGE:` footer.
- Examples: `perf(core): reduce fixed record cursor writes`, `fix(generator): reserve large union payloads`, and `ci(benchmarks): capture polymorphism JIT assembly on all hosts`.
- After completing a feature or fix and its relevant verification, commit the task's changes by default using these conventions; do not ask for commit permission each time. Exclude unrelated or unfinished changes. After committing, ask whether to push unless the owner has already authorized that push. Tags and published-history rewrites still require explicit authorization. When rewriting an authorized remote branch, use an explicit expected revision with `--force-with-lease` so concurrent changes are not overwritten.
- Every commit created on the owner's behalf uses `TheLight-233 <2802799687@qq.com>` for both author and committer. Never add `Co-Authored-By` trailers or any AI attribution. The environment supplies `GIT_COMMITTER_NAME=trae-solo-agent` and `GIT_COMMITTER_EMAIL=solo-agent@trae.ai`; override both variables in the same shell as every `git commit`, then verify the stored identities with `git log -1 --format='%an <%ae> | %cn <%ce>'`.
