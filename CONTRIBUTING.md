# Contributing to LuminPack

[中文](CONTRIBUTING.cn.md)

Read the [mechanism contract](docs/mechanism-contract.md) and the [architecture](docs/architecture.md) before changing runtime or generator code.

## Building

The solution includes `net11.0` targets, so use a .NET 11 preview SDK. `global.json` rolls forward from .NET 10 and allows prerelease SDKs.

```text
dotnet build LuminPack.sln -c Release
```

## Checks

Run what covers your change rather than every suite. The full runtime suite is large; prefer one framework and a focused `--filter`.

```text
# Source standards: XML docs, English prose, exception routing, raw copy routing
dotnet run --project tools/LuminPack.RepositoryChecks/LuminPack.RepositoryChecks.csproj -c Release -- .

# Generated-code contracts (SDK Roslyn); add --verify-<area> to run one area
dotnet run --project tests/LuminPack.Tests.Codegen/LuminPack.Tests.Codegen.csproj -c Release

# Generator pipeline on Roslyn 4.3, then incremental invalidation
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release -- --verify-incremental

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

- `ci.yml` runs repository checks, the build, the codegen and generator contracts, incremental verification, the runtime suite, the hook area, wire fixtures and an installation test from freshly packed NuGet packages.
- `test-areas.yml` runs the Safety area in Debug and Release on Windows, Linux and macOS. It executes AspNetCore, Avalonia, Maui, MonoGame and SkiaSharp adapter contracts on all three systems, plus Drawing and Wpf on Windows. Core is built before adapters because they consume its existing binary.
- Pushes to `main` and `refactor`, and pull requests, trigger the hosted checks. Unity execution still requires its configured license or native runners; the checked-in `.github/scripts` helpers are part of those workflows.
- `runtime-compatibility.yml` runs trimmed and NativeAOT programs on Windows, Linux and macOS (x64 and ARM64), scalar/SSSE3/AVX2 fallbacks and x86. Vendor, ARM32 and AVX-512 runs are manual and need labeled self-hosted runners.
- The Unity workflow builds and runs Mono and IL2CPP players; it needs Unity license secrets.
- Benchmarks run only through the manual `run_benchmarks` input and explicitly select the four maintained classes: `SimpleBenchmark`, `SerializeBenchmark`, `JsonBenchmark` and `PolymorphismBenchmark`. They are not a correctness gate.
- The benchmark matrix uses Linux x64, macOS Intel x64 and Linux ARM64 hosts. It checks the actual process/OS architecture and Intel CPUID vendor before timing, and uploads separate results and host metadata for each host. Compare library ratios within a host; the operating systems and CPUs differ across hosts.
- BenchmarkDotNet polymorphism disassembly runs on Windows/Linux; the installed version does not support CoreCLR disassembly on macOS. Separate, untimed JIT probes capture optimized LuminPack and Nino polymorphism read/write assembly and generated-root metadata on each benchmark host, including macOS. All four timing and allocation fixtures still run there. Benchmark validation, execution or required probe failures fail the job.
- CI concurrency is scoped by event so a push does not cancel a manually started benchmark run. Registry worker tests launch their adjacent apphost to preserve the test process architecture, including self-contained x86.

## Benchmarks

```text
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --filter *SimpleBenchmark*
```

Judge results by LuminPack's time divided by the reference library's, from the same run on the same machine; see section 5 of the contract.

## Source conventions

- Follow `.editorconfig`: four spaces, UTF-8.
- English for code, comments, XML documentation, diagnostics, tests and maintenance documents. README files and `*.cn.md` editions are the exception. Write multilingual test strings with Unicode escapes.
- Every method, constructor, operator and conversion has XML documentation with a summary, parameters, type parameters and return value. Put comments before attributes.
- Keep emitters in separate files; split large generators into partial files by responsibility, keeping type and member names.
- Throw through `LuminPackExceptionHelper` and keep exception types, argument names and cleanup. Use `Rethrow` to keep an original exception's identity.
- Changing names, member order, enum values, null markers, string modes, framing or storage views can break persisted data. Public members without call sites here can still be used by consumers or generated code.
- Keep third-party notices (`docs/xjb-LICENSE.txt`, `docs/xjb-NOTICE.md`) when editing the ported formatters.

## Documentation

- README is the project cover: introduction, installation, usage and links. No release notes, version histories, migration notices or experiment reports in README or `docs`.
- Every README, AGENTS, CONTRIBUTING and `docs` page has an English `name.md` and a Chinese `name.cn.md`. Update both.
- Update `skills/luminpack` when public usage changes.

## Commit conventions

- Every commit title follows `type(scope): description`. Use lowercase types and component scopes, and a concise English imperative description without a trailing period.
- Types: `feat` (feature), `fix` (bug fix), `perf` (performance optimization), `refactor` (restructuring), `test` (tests), `docs` (documentation), `ci` (workflows), `build` (build tooling), and `chore` (maintenance). The performance prefix is `perf`, never `pref`.
- Choose the affected component as the scope, such as `core`, `generator`, `benchmarks`, `unity`, `burst`, or `repo`. Classify benchmark fixture changes by their purpose: `fix(benchmarks)` corrects a fixture, while `ci(benchmarks)` changes its CI execution or capture.
- Keep independent changes in separate commits. A body may explain the reason and relevant validation. Mark breaking changes with `!` after the scope and explain them in a `BREAKING CHANGE:` footer.
- Examples: `perf(core): reduce fixed record cursor writes`, `fix(generator): reserve large union payloads`, and `ci(benchmarks): capture polymorphism JIT assembly on all hosts`.
- Commit, push, tag, or rewrite published history only when requested by the owner. When rewriting an authorized remote branch, use an explicit expected revision with `--force-with-lease` so concurrent changes are not overwritten.
