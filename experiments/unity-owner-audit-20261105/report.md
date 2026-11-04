# Unity codec audit: allocation events and scalar batches

Baseline: `ea342a102281384cceee0f61163eff77abdb7dfd`. Scope: three native owners and all 21 explicit optional-module value codecs. This closes the first independent Unity family pass, not the library performance goal or zero-extra-GC certification.

## Changes and preserved contracts

- Fifteen scalar-only value readers validate the complete record, load explicit fields at constant byte offsets, and use one checked `Advance` before publishing the local result. Their nested callers benefit without flattening their contracts. Every typed access is an individual `float`, `int`, `byte` or `WeightedMode`, at most four bytes. No adjacency or private Unity layout is assumed.
- Eight-field Keyframe and sixteen-field Matrix4x4 writers reserve the full record and advance once. Smaller writers keep their existing implementation: an experimental Vector3 batch was about 3% slower with a larger native body and was withdrawn.
- Curve sizing uses its public length. Curve writes snapshot all keys before output callbacks, with modern Span APIs or older public indexers. Unity 6000.2+ gradient sizing/writing/reading and curve reading use public Span APIs. Existing field order, full prevalidation, destination reuse, native setters, metadata and cursor publication remain.
- Scratch uses at most 16 keys per set on the stack, otherwise returned shared arrays. Invalid counts/payloads are rejected before read scratch acquisition. Capture buffers are cleared before modern getters, preventing exposure of uninitialized storage on an unsupported concurrent shrink. The existing prohibition on concurrent source mutation remains.
- JSON remains explicitly unsupported for native owners. Maintained tests exercise null/non-null roots, null/object inputs, UTF-8 and UTF-16, and destination identity on rejection.

## Verification

Real licensed Windows Editors 2022.3.48f1c1 and 6000.3.2f1 ran the focused native-owner contracts, generated native fields/arrays/lists/reuse, 36 JSON rejection calls and all 21 value codecs. The value contracts include every truncated prefix, exact output windows, canaries, nonzero reader positions and destination/input aliasing. Final logs are `editor-2022-complete.log` and `editor-6000-complete.log`; both contain `LUMINPACK_UNITY_AUDIT_OK`.

The maintained value executable passed on net10 x64 and with the same netstandard module/Core assets hosted on net8 x86. The separate generated consumer build passed with one existing nullable warning in NativePayload.cs. Repository checks passed: 6,226 declarations in 729 C# files, zero violations, including the new modern Unity preprocessor branch. PowerShell syntax and installed-version detection passed. The extension-only CI matrix now includes 6000.3.2f1; its exact unityci image tag was verified through Docker Hub. CI has not run here. No full suite or full benchmark ran. ARM, IL2CPP, WebGL and Player execution remain unverified for this change.

## Allocation evidence and limits

Unity Mono's `GC.GetAllocatedBytesForCurrentThread` returned zero even for necessary result construction. Those preliminary byte results are invalid and not used. The final harness uses a thread-filtered `GC.Alloc` ProfilerRecorder, no frame aggregation, capacity/overflow checks and an allocating positive control. **Reported numbers are managed allocation events per operation, not bytes.** GC.Alloc sample values are timing data and are not summed as bytes. Each measured action is warmed separately; the maintained tests enforce exact zero scratch allocations only on stack paths, independent of pool retention.

Final modern rows: curve and gradient sizing/writing/reuse reading recorded zero events; fresh reading recorded one, matching direct owner construction. Baseline curve writes/reuse reads used one temporary array; gradients used two. RectOffset remained zero for write/size/reuse and one for fresh reads. Every measured value read/write was zero. Old Unity curve writes and sizes recorded zero; curve reads still allocate one key array, and gradient operations still allocate two key arrays. Fresh reads add the necessary owner wrapper.

A separate intermediate modern run recorded one additional pool allocation across 128 fresh reads at 17 keys. Old Unity also exposed a pool allocation after allocation-heavy reads. These are real cold/trim misses, not noise to discard: pooled paths are not unconditional zero-extra-GC. Tests log these paths without asserting that shared pools survive collection. The final size rows use direct construction in the third column as a result-allocation control, not as a competing size operation; size is not timed.

## Same-run performance

Intel Core i7-13650HX, Windows x64, actual Editor Mono, `-releaseCodeOptimization -disableManagedDebugger`. Baseline/candidate/MemoryPack execute in one process on the same fixture and retained output buffers. No builds or other experiments overlap measurement. Each operation uses 512 warmups, 24 samples of 4,096 operations, rotating implementation order, and a separate 128-operation allocation window after 64 per-action warmups. Native fresh reads are allocation-checked but not timed. All samples/outliers are retained.

Reference: MemoryPack.Core 1.21.4 and its official Unity formatters from tag 1.21.4. The Gradient adapter adds colorSpace to the official formatter because LuminPack preserves it. Native fixture values and LuminPack baseline/candidate bytes are checked before measuring. Value fixtures use the unmanaged MemoryPack path; wire size/layout policy differs from LuminPack's explicit-field contract. The reference sources are a local sparse clone with its MIT license, not product code.

Ratios below are median LuminPack time / MemoryPack time, lower is better. They are codec observations, not whole-library speedups.

| Engine | Operation | Before/reference | After/reference |
| --- | --- | ---: | ---: |
| 6.3 | curve 16 keys, write | 2.556 | 1.459 |
| 6.3 | curve 16 keys, reuse read | 3.449 | 1.949 |
| 6.3 | curve 257 keys, write | 5.031 | 2.775 |
| 6.3 | curve 257 keys, reuse read | 6.635 | 4.015 |
| 6.3 | gradient 4 keys, write | 1.032 | 0.514 |
| 6.3 | gradient 4 keys, reuse read | 2.243 | 1.565 |
| 2022.3 | curve 16 keys, write | 1.925 | 1.213 |
| 2022.3 | curve 16 keys, reuse read | 3.497 | 2.725 |
| 6.3 | Vector3 read | 4.445 | 3.518 |
| 6.3 | Matrix4x4 read | 6.601 | 4.271 |
| 6.3 | Keyframe read | 8.762 | 6.547 |

Dispersion is material. For modern 257-key curve writes, CV was 1.55% before, 1.25% after, 4.72% reference; for 16-key reads it was 6.54%, 2.32%, 20.20%. Some small fixtures/reference rows exceed 30% CV. See both complete `summary.csv` files for every CV and median, and `timing.csv` for all 1,488 rows per engine. Matrix writer timings favor the candidate slightly in both engines, but its native body is 22 bytes larger; no robust large speedup is claimed. LayerMask read is effectively neutral. Old Gradient write and RectOffset controls are unchanged and fluctuate. Debug-optimization runs and discarded candidates are retained locally but are not the final performance evidence.

## IL and native evidence

The final `il.csv` and `native-summary.csv` files include both implementations. Native bytes come from the actual warmed Mono JIT methods using its public jit-info accessors, decoded with Capstone 5.0.6; they include slow paths. The three modern read bodies shrink from 2,213 to 928 bytes (Vector3), 11,110 to 2,256 (Matrix4x4), and 5,782 to 1,624 (Keyframe). IL local counts fall from 4/17/9 to 2/2/2. Read bounds and the final checked cursor advance remain. Keyframe write shrinks from 2,312 to 2,270 native bytes; Matrix4x4 write grows from 1,840 to 1,862. Small writes were restored, e.g. Vector3 stays exactly 752 bytes.

Native inspection also found integer division by one in Mono's translation of byte-pointer subtraction in Core Position. This is a concrete next Runtime experiment, not a change included here. It affects more than the optional module and requires its own before/reference/candidate evidence and checked-position tests.

## Reproduction and exclusions

`probe/UnityOwnerAudit.cs` contains the fixture and timing/allocation loops; `probe/NativeDump.cs` captures native bodies. Baseline codec sources are the four files under Runtime at the baseline revision, renamed only to `LuminPack.UnityBaseline`. Compile those plus current Runtime and the official MemoryPack UnityFormatters.cs into a netstandard2.1 probe against the selected engine; use the modern version symbol only for 6000.2+. Build Core/Generator first. Compile the maintained Consumer sources with the generator. An isolated Editor project loads the probe, Core, Unsafe, Immutable and MemoryPack.Core; its entry runs UnityObjectContract, the maintained value checks, ContractEntry and UnityOwnerAudit. Keep managed low-level input pinned across native calls/allocations. Do not run the original local `prepare.py` over retained baselines or final probe projects; it is initial setup only.

Final raw CSVs and the harness are retained with this report. Large engine libraries, Library folders, cloned reference source, preliminary logs and raw native binaries remain local. The family ledger may now say all families have reviewed/partial coverage records; portable BitArray scratch, Type/CultureInfo BCL reads, old Unity arrays, cold pools, earlier wrapper/reuse gaps and the global Runtime/generator/JIT/SIMD audit remain open.

Primary API references: [Unity curve 6000.2](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.2.0f1/Runtime/Export/Animation/AnimationCurve.bindings.cs), [Unity gradient 6000.2](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.2.0f1/Runtime/Export/Math/Gradient.bindings.cs), [thread-scoped GC recorder](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread.html), [Mono JIT metadata](https://raw.githubusercontent.com/Unity-Technologies/mono/unity-main/mono/metadata/appdomain.h). These informed API selection, not copied product implementations.
