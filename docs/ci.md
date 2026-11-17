# Continuous integration

[Chinese](ci.cn.md) · [Contributing](../CONTRIBUTING.md)

Pushes to `main` and `refactor`, and pull requests, trigger the hosted checks. The helper scripts in `.github/scripts` are part of the workflows. CI concurrency is scoped by event, so a push does not cancel a manually started benchmark run.

| Workflow | Runs |
| --- | --- |
| `ci.yml` | Repository checks, the build, codegen and generator contracts, incremental verification, the runtime suite, the Hooks area, wire fixtures and an installation test from freshly packed NuGet packages. Manual runs can add the benchmarks. |
| `test-areas.yml` | The Safety area in Debug and Release, and the platform adapter contracts, on Windows, Linux and macOS. |
| `runtime-compatibility.yml` | Trimmed and NativeAOT programs, SIMD fallbacks, x86 and AVX-512 hardware tests. |
| `unity.yml`, `unity-extensions.yml` | Unity Editor tests, IL2CPP and WebGL players, Unity object and Burst contracts. |
| `benchmark-matrix.yml` | Round-trip validation of the serialization matrix; timing on manual runs. |
| `fuzz.yml` | Bounded SharpFuzz/libFuzzer campaigns; see [fuzzing](fuzzing.md). |
| `release.yml` | Unpublished release candidates and API comparison; see [releasing](releasing.md). |

## Tests and platforms

- `test-areas.yml` runs the AspNetCore, Avalonia, Maui, MonoGame and SkiaSharp adapter contracts on all three systems, plus Drawing and Wpf on Windows. Core is built before the adapters because they consume its existing binary.
- Registry worker tests launch their adjacent apphost to keep the test process architecture, including self-contained x86.
- `runtime-compatibility.yml` runs trimmed and NativeAOT programs on Windows, Linux and macOS (x64 and ARM64), the scalar/SSSE3/AVX2 fallbacks and x86. Vendor and ARM32 runs are manual and need their labeled self-hosted runners.

### AVX-512

AVX-512 hardware tests run automatically on pushes and same-repository pull requests, and are enabled by default for manual runs. Uncheck `run_avx512` to skip them in a manual run, or select `avx512_only` to run only these checks.

Four `ubuntu-latest` candidates probe for real AVX-512F/BW. Every supported candidate runs the tests, and the check fails if a candidate fails or is cancelled. If no candidate supports the features, the check succeeds with a skipped-coverage notice; it does not claim hardware validation. GitHub does not guarantee that a supported host is available. Set the repository variable `LUMINPACK_AVX512_RUNNER` to an AVX-512-capable Linux x64 runner label, such as `avx512`, to pin a dedicated runner. Fork pull requests do not run this job, including when a self-hosted runner is selected.

### Unity

Configure `UNITY_EMAIL`, `UNITY_PASSWORD` and `UNITY_SERIAL` for online activation. A Personal serial can be extracted from a local `.ulf` with the GameCI activation documentation; a usable `UNITY_LICENSE` can replace the serial. Missing secrets fail the license check.

Unity 2022.3.51f1 and 6000.0.43f1 run Editor tests, an IL2CPP smoke player, the Unity object and Burst Editor contracts, and a WebGL IL2CPP smoke player in headless Chrome, one after another on one license. Runs queue (up to 100 pending) instead of cancelling an earlier run. Fork pull requests skip license-backed jobs. The manual native Windows and macOS player checks need prelicensed self-hosted runners; hosted Linux does not verify those platforms.

## Benchmarks

Benchmarks run only on manual dispatch of `ci.yml` with `run_benchmarks` enabled. That run times both the BenchmarkDotNet suites and the [serialization matrix](../benchmarks/LuminPack.MatrixBenchmark).

| Input | Default | Effect |
| --- | --- | --- |
| `run_benchmarks` | off | Run BenchmarkDotNet on three hosts and time the matrix. |
| `benchmark_jit` | `both` | `TieredPGO` (tiered compilation and dynamic PGO) and `FullOpts` (both disabled) as separate jobs. FullOpts is a JIT stability control, not a substitute for production PGO results. |
| `benchmark_satori` | `only` | `only` measures the Satori GC runtime prepared by `eng/SatoriCompiler`, `both` adds the stock-runtime jobs, `off` measures only the stock runtime. |

### BenchmarkDotNet

- The job selects `SimpleBenchmark`, `SerializeBenchmark`, `JsonBenchmark` and `PolymorphismBenchmark` on Linux x64, macOS Intel x64 and Linux ARM64 hosts. It checks the process and OS architecture and the Intel CPUID vendor before timing. Each host uploads its own `benchmark-artifacts-*` results and metadata; compare library ratios within a host.
- Each job uses three processes, 8–30 adaptive warmup iterations and 15 measurements targeting one second each, keeping outliers. Fixed seeds and timestamps keep input bytes identical across processes; `--verify-fixtures` checks fresh instances and repeated setup before timing.
- Full JSON and measurement CSV artifacts keep every process. `process-stability.md` and the job summary report process means, CV, max/min, allocations and LuminPack/reference ratios within the same operation and JIT job. A process spread above 10% produces a warning; inspect the distribution and native code before claiming a win. Independently launched libraries are not paired by process index.
- BenchmarkDotNet polymorphism disassembly runs on Windows and Linux; the installed version does not support CoreCLR disassembly on macOS. Separate, untimed JIT probes capture optimized LuminPack and Nino polymorphism read/write assembly and generated-root metadata on the Linux hosts. Validation, execution or required probe failures fail the job.

### Serialization matrix

`benchmark-matrix.yml` validates round trips for relevant pushes and pull requests without timing. Manual dispatch, or a `ci.yml` run with `run_benchmarks`, samples every format and scenario in three processes of nine rounds on one runner. The matrix enables Server GC, tiered compilation, dynamic PGO and Quick JIT for loops. Each fixture, library and operation warms up for at least 64 calls and 15 milliseconds before calibrating its sample size. The `benchmark-matrix` artifact contains the JSONL samples, the machine information and `REPORT.md`.

Chart generation from these artifacts is described in [Contributing](../CONTRIBUTING.md#benchmarks).
