# LuminPack

Source-generated binary and JSON serializer for C#: .NET (`netstandard2.1`, `net8.0` to `net11.0`), Unity (Mono,
IL2CPP, Burst) and NativeAOT. The NuGet candidates are `LuminPack` (Core) and `LuminPack.Generator`; platform adapters
are source projects. Version 2.0 is being refactored on the `refactor` branch and is not on NuGet yet (NuGet's
`LuminPack` 1.x is the previous generation). Code, comments, XML docs, diagnostics, tests, commits and maintenance
documents are English; README files and `*.cn.md` editions are the exception.

This file is the instruction set for coding agents (the open `AGENTS.md` convention). Human contributors start with
[CONTRIBUTING.md](CONTRIBUTING.md); both describe the same rules.

## Commands

```bash
dotnet build LuminPack.sln -c Release                                                       # needs the .NET 11 SDK from global.json
dotnet run --project tools/LuminPack.RepositoryChecks -c Release -- .                        # source standards
dotnet run --project tests/LuminPack.Tests.Codegen -c Release -- --verify-<area>             # generated-code contracts, one area
dotnet run --project tests/LuminPack.Tests.Generator -c Release -- --verify-incremental      # generator pipeline and caching
dotnet test tests/LuminPack.Tests -c Release -f net10.0 --filter "FullyQualifiedName~<Test>" # runtime tests, focused
dotnet test tests/LuminPack.Tests -c Release -f net10.0 -p:LuminPackTestArea=Safety          # a test area (Safety, Simd, Hooks)
dotnet run --project tests/LuminPack.Tests.Aot -c Release -- --wire verify tests/LuminPack.Tests.Aot/Fixtures
```

The complete check list, the benchmark commands and the chart scripts are in [CONTRIBUTING.md](CONTRIBUTING.md). The
test project compiles slowly: batch edits, pick one framework and a filter, test area or `--verify-<area>` selector,
and run the full suites only when the change needs them.

## Layout

```
src/            LuminPack.Core (runtime), LuminPack.Generator (packaged generator) and LuminPack.Generator.Next
                (model-based successor), LuminPack.CodeFixes, LuminPack.ILPostProcessor (IL rewriting build task),
                LuminPack.Platforms (shared adapter code) and the adapters (AspNetCore, Avalonia, Drawing, Maui,
                MonoGame, SkiaSharp, Wpf, Unity, Burst)
tests/          runtime tests (LuminPack.Tests, with test areas), codegen and generator contracts, code fixes, AOT and
                wire fixtures, platform adapters, fuzzing, Unity projects
benchmarks/     LuminPack.Benchmark (BenchmarkDotNet suites), LuminPack.MatrixBenchmark (serialization matrix),
                make-charts.py and MatrixBenchmark/chart.py (README charts)
tools/          RepositoryChecks (source standards), performance probes, xjb tables
eng/            Versions.props, API compatibility suppressions, the Satori GC compiler runtime
docs/           user guides and contributor references; every page has an English and a Chinese edition
skills/         skills/luminpack, the public skill that teaches users' agents to write LuminPack code
.github/        workflows and their scripts (see docs/ci.md)
experiments/    untracked, isolated performance experiments; nothing here ships
.tmp/           untracked scratch output (logs, fixtures, downloaded CI artifacts)
```

## Read before changing code

| Read | Before |
| --- | --- |
| [Mechanism contract](docs/mechanism-contract.md) | any change to runtime code, the generators, generated code, optional modules or conditional targets. It is binding. |
| [Architecture](docs/architecture.md) | finding where something lives and how the pieces fit |
| [Next generator](docs/generator-next.md), [formatter contracts](docs/next-formatters.md), [migration status](docs/next-generator-prototype.md) | working in `src/LuminPack.Generator.Next` |
| [Formatter porting](docs/generator-formatter-porting.md) | adding or porting a generator formatter family |
| [JSON name lookup](docs/json-name-lookup.md), [pooling](docs/pooling.md), [Burst hooks](docs/burst-hooks.md), [compression](docs/compression.md) | changing those subsystems |
| [CI](docs/ci.md), [fuzzing](docs/fuzzing.md), [releasing](docs/releasing.md) | changing workflows, the fuzz harness or packaging |

## Mechanism rules

1. **Keep every established mechanism.** Local + As storage views, virtual Union dispatch and its cached generic
   slots, version-tolerant name tables, reference tracking, raw unmanaged paths and `StructLayout`,
   constructor/pool/reuse behavior, callbacks, specialized formatters, registration through typed delegates, compiled
   options and state ownership. A faster design needs an isolated experiment in `experiments/` with same-run reference
   ratios, allocations and IL/native evidence, and the owner's explicit approval of that concrete proposal. A
   performance goal is not approval.
2. **Reader checks stay on in Release.** `EnsureReadable` and its callers (`Advance`, fixed storage batches, Union
   framing) run in Debug and Release. Generated cursor arithmetic keeps its check before moving the cursor. Collection,
   string and bulk-payload checks stay. Fixed Writer stores and address-only raw kernels keep their separate unchecked
   contracts.
3. **32-bit fixed access.** Only individual unmanaged values of at most four bytes may use typed unaligned access on
   32-bit; larger values and field batches use `LuminPackMarshal.CopyBlockUnaligned` and packed views (IL2CPP).
   Adjacent-field bulk copies need real field-address proofs, exclude volatile fields and keep a field-wise fallback.
   Only `LuminPackMarshal.CopyBlockUnaligned` calls `Unsafe.CopyBlockUnaligned`.
4. **SIMD.** Review scalar, 128/256/512-bit and ARM64 paths; guard by exact ISA features; keep bounded tails and a
   portable fallback; never touch memory outside a validated range. Unavailable hardware needs CI execution.
5. **Wire bytes are a contract.** Names, member order, enum values, null markers, string modes, framing and storage
   views reach persisted data. A wire change needs explicit approval and updated wire fixtures.
6. **Don't remove** storage placeholders or externally usable or generated members based only on call-site searches.

## Evidence

- **Performance** is LuminPack's time divided by the reference library's, on the same machine, fixture, runtime,
  options and run, with repeated samples, dispersion and allocations. One isolated figure proves nothing; local
  machines are noisy. Published numbers (README charts, docs) come only from CI runs.
- **Reports are honest**: say what ran, what failed and what was not verified.
- Keep scratch output, downloaded artifacts and throwaway probes under `.tmp`.

## Documentation

- README is the cover page; feature documentation lives in `docs`. No release notes, version histories, migration
  notices or experiment reports in README or `docs`.
- Every README, CONTRIBUTING and `docs` page has an English `name.md` and a Chinese `name.cn.md`; update both. Chinese
  editions live under `docs`: root-page translations directly in `docs`, module README translations in
  `docs/modules/<module>.cn.md`. English documents keep their locations. This file is English only.
- When public usage changes, update the guides (both editions) and `skills/luminpack`.

## Commits

- **Commit completed work by default.** After a feature or fix and its relevant verification, commit the task's
  changes without asking. Keep unrelated or unfinished changes (including another agent's uncommitted work) out.
  Ask before pushing unless the owner already authorized that push. Tags and published-history rewrites need explicit
  authorization; an authorized rewrite uses `--force-with-lease` with an explicit expected revision.
- **Messages** follow `type(scope): description` with a lowercase type and scope and an English imperative
  description. Types: `feat`, `fix`, `perf` (never `pref`), `refactor`, `test`, `docs`, `ci`, `build`, `chore`; the
  scope is the component (`core`, `generator`, `benchmarks`, `unity`, `burst`, `repo`, ...). Every commit has a
  substantive English body explaining why, what changed, and the validation and its results, including limitations
  and unverified items. Keep independent changes in separate commits. Details: [CONTRIBUTING.md](CONTRIBUTING.md#commit-conventions).
- **Identity.** Commits made for the owner use `TheLight-233 <2802799687@qq.com>` as both author and committer, with
  no `Co-Authored-By` or other AI attribution. Agent hosts may inject `GIT_COMMITTER_NAME`/`GIT_COMMITTER_EMAIL`,
  which override Git config, so set all four variables on the commit itself and check the result:

  ```bash
  GIT_AUTHOR_NAME=TheLight-233 GIT_AUTHOR_EMAIL=2802799687@qq.com \
  GIT_COMMITTER_NAME=TheLight-233 GIT_COMMITTER_EMAIL=2802799687@qq.com git commit
  git log -1 --format='%an <%ae> | %cn <%ce>'
  ```

## Working with the owner

- The owner writes in Chinese: reply in Chinese. Everything committed stays English except README files and
  `*.cn.md` editions.
- Decide what clearly follows these rules without asking; ask only what is the owner's call (a mechanism or wire
  change, a public API removal, spending CI time on benchmarks).
