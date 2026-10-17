# Retained serializer performance probes

Keep a complete baseline benchmark output directory before changing the runtime or generator. Retain a second output directory after building a candidate, including all dependency DLLs. These probes load the retained assemblies without rebuilding them.

```text
dotnet run --project tools/LuminPack.PerformanceProbe -c Release -- --ratios .tmp/serializer-perf/baseline-bin .tmp/serializer-perf/candidate-bin

dotnet run --project tools/LuminPack.PerformanceProbe -c Release -- --ratios-character .tmp/serializer-perf/baseline-bin .tmp/serializer-perf/candidate-bin
dotnet run --project tools/LuminPack.PerformanceProbe -c Release -- --baseline .tmp/serializer-perf/baseline-bin --candidate .tmp/serializer-perf/candidate-bin --filter * --launchCount 2 --warmupCount 8 --iterationCount 8 --affinity 256
dotnet run --project tools/LuminPack.PerformanceProbe -c Release -- --il .tmp/serializer-perf/candidate-bin LuminPackBenchmark.SimpleClass
dotnet run --project tools/LuminPack.PerformanceProbe -c Release -- --codegen .tmp/serializer-perf/candidate-bin

dotnet run --project tools/LuminPack.PerformanceProbe -c Release -- --codegen-character .tmp/serializer-perf/candidate-bin

dotnet run --project tools/LuminPack.PerformanceProbe -c Release -- --codegen-character-utf8 .tmp/serializer-perf/candidate-bin

dotnet run --project tools/LuminPack.PerformanceProbe -c Release -- --codegen-strings .tmp/serializer-perf/candidate-bin

dotnet run --project tools/LuminPack.PerformanceProbe -c Release -- --verify-character .tmp/serializer-perf/baseline-bin .tmp/serializer-perf/candidate-bin
dotnet run --project tools/LuminPack.PerformanceProbe -c Release -- --native-summary .tmp/serializer-perf/candidate.asm
```

The ratio probe binds the unchanged SimpleBenchmark operations, interleaves LuminPack, MemoryPack, and Nino across all selected builds, warms for six seconds, and records 36 rotating samples plus allocation counts. It pins logical processor 8 on this Windows development machine. Compare each sample's LuminPack/reference ratio and its dispersion; a single absolute latency is insufficient evidence. The common delegate boundary and standalone GC configuration mean the probe does not replace the original BenchmarkDotNet entry for absolute latency claims.

`--ratios-write` retains the same Simple write operations, reference libraries, warmup and samples while omitting allocating read batches. It is useful for investigating write-entry overhead without GC work from an intervening read sample. Keep this profile separate from combined read/write samples.

`--ratios-union <fixed-percent> <images...>` measures 32 dictionary entries in a Union family containing fixed and managed-string cases. Use 0, 25 and 100 to expose fallback costs separately from the fixed-case benefit. All libraries use the same logical values, UTF-8 and reusable/reset destinations. Setup validates each library's round trip and the probe compares exact LuminPack bytes between images before timing.

`--verify-simple <baseline-image> <candidate-image>` reads each unchanged Simple fixture payload with both retained implementations, then rewrites it through the original reusable-buffer entry and compares exact bytes. It checks all four image combinations without timing and includes the original mixed Union dictionary.

`--verify-json` checks four cross-image JSON read/rewrite combinations using the original polymorphic Simple graph. `--verify-json-output` executes the original small/large direct-output equality checks. Neither measures latency.

`--ratios-json` measures the original JsonBenchmark operations against its unchanged System.Text.Json and Newtonsoft.Json operations. Keep its original API distinctions visible: LuminPack writes a reusable UTF-8 buffer while the reference writes return allocated strings. Use these references to assess before/after variability; their absolute allocation and time ratios are not an equivalent-destination API comparison.

`--baseline <image> --candidate <image> --filter *SimpleWriteReferenceComparison*` runs BenchmarkDotNet against both retained Simple writes and the baseline image's unchanged MemoryPack/Nino writes. Each operation has the same delegate boundary. This avoids rebuilding an archived implementation from current sources and provides common reference measurements, dispersion and allocations. Use the original SimpleBenchmark separately for its absolute target.

`--ratios-dictionary-sides` uses 64 dense entries with a 32-byte unmanaged key and string value, or the reversed types. All three libraries use UTF-8, reset or replace the reusable destination on each operation, and write the same values. `--codegen-dictionary-sides` invokes the LuminPack paths once for native inspection without measuring throughput. These supplemental fixtures expose struct copy costs; they are not original SimpleBenchmark results.

`--ratios-field-groups <shape> <images...>` compares Primitives (eight adjacent ints), PaddedStructs (two padded raw structs between long fields), and Gapped (bytes alternating with aligned longs). Each library uses the same logical values, reusable/reset write destinations, fresh reads and a common fixture delegate boundary. Record both reference ratios and allocations. `--codegen-field-groups` executes all three LuminPack shapes once for native inspection. Address proofs preserve the packed wire format; inter-field alignment gaps remain excluded. A struct's own raw padding remains included.

`--ratios-character` uses the original SerializeBenchmark operations, buffer reuse, options, and graph. Its shorter sample batches account for the larger graph. Returned read objects pass through the same non-reflective wrapper for each library. LuminPack and MemoryPack use UTF-16 here; Nino retains its own fixed UTF-8 format and is only a secondary comparison across formats. Do not present its ratio as an identical-encoding comparison.

`--ratios-strings <shape> <images...>` measures the original BinaryStringRatioBenchmark operations against MemoryPack with matching encoding. Original shapes are Short, Medium, Long, Unicode, and Utf16. Supplemental Tiny and Mixed shapes replace only the input array, keeping 512 values; both libraries' payloads and round trips are verified before timing. Mixed includes null, empty, tiny and longer ASCII, and Unicode. These distributions validate general string-path tradeoffs without changing production code for a benchmark.

The BenchmarkDotNet comparison also covers the existing Character serialization graph. Run measurements after builds and correctness checks finish. Preserve the runtime, encoding, options, fixture shape, and allocation policy when comparing results. BenchmarkDotNet can rebuild archived executables against a current project, so use retained-image probes when a historical library binary must remain fixed.

For code-generation inspection under an unstable machine load, set `DOTNET_TieredCompilation=0`, `DOTNET_JitDisasm=LuminPack.LuminPackSerializer:*`, and `DOTNET_JitStdOutFile` to an absolute output path before running `--codegen`. Restore these variables afterward. The probe invokes the unchanged Simple entries once without a stopwatch, affinity changes, or a throughput loop. Compare identical runtime and JIT settings across retained builds. FullOpts without profile data is distinct from Tier1 with dynamic PGO; retain that distinction in reports.

The native summary counts static instructions and call sites, including cold blocks. Its prologue stack figure includes explicit stack subtraction and pushed registers, excluding the return address. Frame-relative move sites include arguments and do not by themselves prove a spill. Checked versus known-heap reference-store figures count explicit helper call sites, not executions or inlined write barriers. These structural indicators support inspection; none establishes elapsed-time improvement by itself.

`--il <image> <payload-fragment> [exact-method-name]` can select a specific Serializer method, including an internal bridge in an isolated experiment. Omitting the name retains the standard binary operation selection. This inspects retained IL without executing the selected method.

The summary also reports covariant array-store helper sites. Replacing a checked reference barrier with an array covariance helper is not evidence of cheaper element publication. For the original character graph and UTF-16 options, use `--codegen-character`; its setup and read/write operations each execute once without throughput measurement.

`--verify-character` runs the unchanged SerializeBenchmark setup in each retained image, then reads both original UTF-16 payloads in both implementations and checks exact rewritten bytes. Each source can contain different timestamps; comparisons use the source's actual bytes. This checks four wire combinations without timing or a throughput loop.

`--codegen-character-utf8` changes only the original character fixture's LuminPack encoding to UTF-8 before setup. It is a supplemental encoding variant, not the original UTF-16 benchmark result. `--codegen-strings` executes a finite null/empty/tiny/short/medium/Unicode string matrix through the retained BinaryStringRatioBenchmark API methods and verifies restored values. Neither mode measures throughput. The native summary also counts residual process-bitness property calls: large parsers can exhaust the JIT's inlining budget before those getters fold.

`--ratios-monomorphic` is a supplemental distribution test. It replaces the dictionary values with the same first Union object before setup, consistently for all reference libraries. It preserves the original benchmark methods but changes their input graph and is not an original SimpleBenchmark latency result.
