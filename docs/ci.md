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
| `benchmark-matrix-paired.yml` | Manual same-runner comparison of two or three revisions for selected Matrix fixtures, in FullOpts and TieredPGO. |
| `fuzz.yml` | Bounded SharpFuzz/libFuzzer campaigns; see [fuzzing](fuzzing.md). |
| `release.yml` | Release candidate builds, API comparison and NuGet publishing from the configured version tag. See [releasing](releasing.md). |

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

Manually dispatching `ci.yml` with `run_benchmarks` enabled times the BenchmarkDotNet suites, the Nino Workloads and AppData suites, and the [serialization matrix](../benchmarks/LuminPack.MatrixBenchmark).

| Input | Default | Effect |
| --- | --- | --- |
| `run_benchmarks` | off | Run the main BenchmarkDotNet suites on three hosts and time the Nino comparison and matrix. |
| `benchmark_jit` | `both` | Selects the main BenchmarkDotNet jobs. `TieredPGO` enables tiered compilation and dynamic PGO, and `FullOpts` disables both. |
| `benchmark_satori` | `only` | For the main BenchmarkDotNet suites, `only` measures the Satori GC runtime prepared by `eng/SatoriCompiler`, `both` adds the stock-runtime jobs, and `off` measures only the stock runtime. |

### BenchmarkDotNet

- The job selects `SimpleBenchmark`, `SerializeBenchmark`, `JsonBenchmark` and `PolymorphismBenchmark` on Linux x64, macOS Intel x64 and Linux ARM64 hosts. It checks the process and OS architecture and the Intel CPUID vendor before timing. Each host uploads its own `benchmark-artifacts-*` results and metadata; compare library ratios within a host.
- Each job uses three processes, 8–30 adaptive warmup iterations and 15 measurements targeting one second each, keeping outliers. Fixed seeds and timestamps keep input bytes identical across processes; `--verify-fixtures` checks fresh instances and repeated setup before timing.
- Full JSON and measurement CSV artifacts keep every process. `process-stability.md` and the job summary report process means, CV, max/min, allocations and LuminPack/reference ratios within the same operation and JIT job. A process spread above 10% produces a warning; inspect the distribution and native code before claiming a win. Independently launched libraries are not paired by process index.
- BenchmarkDotNet polymorphism disassembly runs on Windows and Linux; the installed version does not support CoreCLR disassembly on macOS. Separate, untimed JIT probes capture optimized LuminPack and Nino polymorphism read/write assembly and generated-root metadata on the Linux hosts. Validation, execution or required probe failures fail the job.

### Nino Workloads and AppData

`LuminPack.NinoBenchmark` is part of the solution. It compares LuminPack with Nino on 12 Workloads scenarios and eight AppData scenarios adapted from the [Nino benchmark suite](https://github.com/JasonXuDeveloper/Nino/tree/185220ad24f07a9aeff53c21c4f30a0556c17e23/runtimes/csharp/benchmarks/Nino.Benchmark). Each operation uses Nino as its baseline and reports allocations and timing ratios. Regular CI verifies the fixtures on Linux, Windows and macOS. The manual timing job uses three launches on Linux x64 with stock .NET 10, TieredPGO and Server GC. It uploads the `nino-benchmark` artifact with per-measurement CSV and full JSON reports.

```bash
dotnet run --project benchmarks/LuminPack.NinoBenchmark -c Release -- --verify
dotnet run --project benchmarks/LuminPack.NinoBenchmark -c Release -- --filter '*' --artifacts .tmp/nino-benchmark
```

### Serialization matrix

`benchmark-matrix.yml` validates round trips on relevant pushes and pull requests. Manual runs of that workflow, or `ci.yml` with `run_benchmarks`, time every format and scenario in FullOpts with Server GC on one runner. Each scenario runs in three processes with nine rounds per process. FullOpts disables tiered compilation and dynamic PGO.

Before final sample calibration, the matrix interleaves shuffled warmup batches across libraries for the same operation. Each library and operation accumulates at least 256 calls and 15 ms of active time in FullOpts or 300 ms in TieredPGO. TieredPGO also waits for 50 ms without observed process-wide JIT activity, subject to a two-second warmup limit once the call and active-time minimums are met. The report flags cases whose first and last three sample medians within a process differ by at least 20%, and retains all samples.

The `benchmark-matrix` artifact includes the raw JSONL with warmup observations, the machine information, `stability.csv` and `REPORT.md`. Compare library ratios within the same run.

Dispatch `benchmark-matrix-paired.yml` with the baseline revision, one format and scenario, and exact fixture names. An optional comparison revision supplies a third variant. Each JIT mode builds all variants with the same fixture source and rotates their process order on one host. For each round without observed global JIT activity in either measurement, divide LuminPack time by the reference time in that round. The CSV reports the median of these ratios within each process and then across processes (`over_reference`). Divide this value by the baseline's value to compare revisions (`ratio_over_stock`). The CSV also records the spread of the process ratios, absolute times and allocations.

The optional `initial_warmup` input writes `post-validation-initial-warmup.csv` with a separate comparison of the first 256 timed calls per operation, after validation and before calibration for that fixture. Fixture preparation and validation may already invoke the serializers. Earlier operations and fixtures can also warm shared code, so this metric describes initial post-validation warmup only.

Chart generation from these artifacts is described in [Contributing](../CONTRIBUTING.md#benchmarks).
