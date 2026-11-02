# Fuzzing

[Chinese](fuzzing.cn.md)

Fuzzing automatically explores inputs that ordinary examples miss: truncated records, forged lengths, flipped bits, malformed strings and broken JSON grammar. LuminPack must either decode a value consistently or reject the input with a known parser error. Crashes, hangs, resource failures and inconsistent successful reads are findings.

The harness in `tests/LuminPack.Fuzzing` supplements unit tests. Deterministic smoke tests replay seeds and mutations; [SharpFuzz](https://github.com/Metalnem/sharpfuzz) instruments compiled code so [libFuzzer](https://llvm.org/docs/LibFuzzer.html) can guide mutations toward new execution paths. A passing bounded run is coverage of those inputs, not proof that every model or platform is safe.

## Quick start

Use PowerShell 7, the .NET 10 runtime and the .NET 11 preview SDK required by the repository. Run from the repository root:

```powershell
# Publish an isolated build, verify positive seeds, replay every seed prefix,
# then run 1,000 deterministic mutations per target.
pwsh -File .github/scripts/fuzz.ps1

# Change the mutation budget and seed, or test Debug.
pwsh -File .github/scripts/fuzz.ps1 -Configuration Debug -Iterations 2000 -Seed 42
```

The smoke runner saves each input before executing it under `.tmp/fuzz/repro`, together with the target, seed, case and replay command. The last input remains available even after a fatal process failure. Successful output reports accepted and rejected counts separately; rejection of malformed data is expected.

## Targets and limits

| Target | Input |
| --- | --- |
| `binary` | Ordinary binary objects with default UTF-16 fixed-length strings, including compact Latin-1. |
| `binary-utf8-varint` | The same graph with UTF-8 strings and canonical base-128 length headers. |
| `json-utf8` | Strict UTF-8 JSON bytes without a BOM. |
| `json-utf16` | Strict little-endian UTF-16 JSON bytes without a BOM, including malformed odd-byte tails. |

Models cover integer and nullable scalars, Boolean batches, strings, primitive and Boolean arrays, Boolean lists, nested object lists, dictionaries and recursive children. Binary declarations select `UntrustedData`, with a 16 KiB input cap, 128 elements per collection, 512 UTF-16 code units per string and recursive depth 16. JSON declarations select strict syntax and document depth 16. Each encoding uses a declaration-owned profile and concrete generated overloads. JSON collection/string sizes are bounded by the input cap, as in the library's existing policy.

These initial arbitrary-byte targets cover ordinary object contracts. Union, version-tolerant, reference-tracked, Custom, compression, encryption and other string profiles are not covered. They require separate targets with suitable contracts; do not silently enable unsupported untrusted shapes.

Positive seeds must reproduce their source graphs before mutation begins. After an accepted read, the harness checks consumed-length bounds, fresh/reuse value parity and stable serialization after a canonical round trip. Dictionaries compare by key/value rather than iteration order. Binary null roots preserve existing targets; JSON null roots clear them. Canonical JSON may add omitted members and exceed the document cap; that case still checks reuse parity but skips the capped round-trip read.

Only the initial parse filters expected input errors: `InvalidDataException`, `FormatException`, `DecoderFallbackException`, `EndOfStreamException`, LuminPack's private parser exception, exactly `ArgumentException` for duplicate dictionary keys, and specific JSON token-mismatch `InvalidOperationException` messages. Derived argument/bounds errors, arbitrary invariant errors, unsupported operations and resource failures escape. No exception is filtered from reuse, serialization or canonical reads.

## Coverage-guided runs

Download the appropriate pinned `v2025.05.02.0904` driver from [libfuzzer-dotnet releases](https://github.com/Metalnem/libfuzzer-dotnet/releases/tag/v2025.05.02.0904). Coverage mode supports the driver's Linux and Windows platforms. For example, after placing the Windows driver at `.tmp/libfuzzer-dotnet-windows.exe`:

```powershell
pwsh -File .github/scripts/fuzz.ps1 -Mode Coverage -Target binary `
    -LibFuzzer .tmp/libfuzzer-dotnet-windows.exe -Seconds 300 -Output .tmp/fuzz-coverage
```

On Linux, make the driver executable and pass its path instead. The script installs `SharpFuzz.CommandLine` 2.3.0 into its output directory and uses SharpFuzz 2.3.0 from the project. It publishes a fresh copy for each run, instruments `LuminPack.Core.dll` and the consuming assembly's `LuminPack.` namespace, which contains the generated formatters, and leaves ordinary build output untouched. Instrumenting Core alone misses generated reader branches. The runner lives in a separate namespace to avoid instrumentation before SharpFuzz initializes.

The native bridge launches `dotnet` with the assembly path; the script selects the target with the scoped `LUMINPACK_FUZZ_TARGET` environment variable and restores its previous value afterwards. Runs use a 16 KiB maximum input, a ten-second per-input timeout and a 1 GiB driver RSS budget. Fatal failures and timeouts remain visible to the native driver; these limits are operational safeguards, not a managed allocation guarantee.

Mutable, target-specific corpora live under `<Output>/corpus`, or the explicit `-Corpus` directory. Reuse that directory to retain discovered inputs. Raw failure artifacts go under `<Output>/failures/<target>`. Choose separate output roots for independent campaigns. Check the native log for increasing coverage features (`ft`); a script exit alone does not establish that useful branches were reached.

## Replay and regression inputs

```powershell
# A failure is raw input for its original target; replay needs no native driver.
dotnet run --project tests/LuminPack.Fuzzing -c Release -- `
    --replay binary .tmp/fuzz-coverage/failures/binary/crash-EXAMPLE

# Replay every file in a target's corpus.
dotnet run --project tests/LuminPack.Fuzzing -c Release -- `
    --replay json-utf8 tests/LuminPack.Fuzzing/Corpus/json-utf8

# Regenerate the complete deterministic seed corpus in a scratch directory.
dotnet run --project tests/LuminPack.Fuzzing -c Release -- --seed .tmp/fuzz-seeds
```

Replay reports either an accepted input with passing invariants or an expected rejection. An unexpected failure exits nonzero. Preserve its target and exact bytes. Minimize a finding with libFuzzer's `-minimize_crash=1` in an instrumented campaign, add the minimized file under `Corpus/<target>` with a unique descriptive name, and add a focused regression assertion when fixing the cause. Do not change the exception filter to hide a finding. The script preserves additional corpus files when regenerating named built-in seeds.

## Continuous integration

`.github/workflows/fuzz.yml` runs on pushes to `main` and `refactor`, pull requests, weekly schedules and manual dispatch:

- Push/PR/manual smoke runs cover Linux Debug/Release, Linux scalar fallback, Windows Release and macOS Release. Each target replays all seed truncations and 1,000 fixed-seed mutations.
- Coverage jobs run each target on Linux for 30 seconds on push/PR, 300 seconds weekly, or the manual `seconds` input. Each job instruments a fresh publication and verifies the native bridge's pinned SHA-256. Target-specific caches carry discovered corpus inputs into later runs, subject to GitHub's cache scope and eviction rules.
- Artifacts retain corpora and saved reproducers/failures, including when a job fails. Actual crash details remain in job logs. Download the matching target's artifact and replay it locally.

The timeout includes setup and the configured campaign; very large manual budgets may need a larger workflow timeout. These jobs do not establish Unity/IL2CPP, x86 or every ISA's coverage.
