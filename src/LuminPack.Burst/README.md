# LuminPack.Burst

Optional Burst 1.8 backend for version 3 of the netstandard2.1 `LuminPack.Core.LuminPackBurst` ABI. It references Core, Unity.Burst and UnityEngine.CoreModule. Core and LuminPack.Unity do not reference it. Modern .NET assets continue using their own native SIMD.

```csharp
using LuminPack.Burst;

bool installed = BurstBackend.TryRegister();
```

`Register()` has a BeforeSceneLoad runtime hook; the declaring class has the IL2CPP eager-construction attribute. Manual `TryRegister()` remains available and returns false without replacing an existing Core table. Disabled Burst, WebGL and a probe reporting no compiled SIMD retain Core. Compilation finishes before publishing a table; failures cannot publish partial entries.

The kernels are compiled from an automatic projection of existing Core methods. `CodeGen~` reads the authoritative Core syntax with Roslyn; `Runtime/Generated` is regenerated on .NET builds and carries source hashes. Never edit generated algorithms independently. `KernelVectors`, `KernelMemory` and `PortableUtf8` map native primitives and Core's portable platform fallback. The original branches, traversal order, complete-block bounds and tails come from Core. Run the projection tool before exporting source into Unity, and use `--verify` to detect stale output. `--core-source-root` selects a fixed source copy for reproducible isolated validation without building Core.

`LuminKernelPolicy` is shared by Core and Burst. Copy uses the original head/middle/tail schedule through 256 bytes, or through 128 bytes when only narrow lanes are available. A 64-bit process and the required hardware are mandatory. Unsupported sizes/features return zero without writes; Core executes its existing `Unsafe.CopyBlockUnaligned` fallback. There is no second large-copy loop or alternative streaming policy. Matching and XOR retain Core's exact vector thresholds and scalar tails; periodic expansion retains Core's fill/non-overlap/overlap branches and eight-byte remainder path.

The table installs all sixteen ABI slots: copy, ordinary equality, UTF-8 encode/decode/equality, short ASCII encode, layout prefix conversion, equal-prefix matching, periodic match expansion, ChaCha20 block generation, XOR and compact Latin1 classification/narrowing/widening/equality and precomputed layout prefixes. UTF codecs preserve replacement, streaming status and consumed/produced counts. ASCII preserves Core's eligibility and validates before writing. Native text entry points represent UTF-16 code units as `ushort*` because Burst 1.8 rejects `char*` external functions; table publication uses Core's unchanged, binary-equivalent pointer signatures. Ordinary equality maps the platform memory comparison primitive; authentication tag comparison stays on Core's original constant-time path.

Layout conversion shares Core's immutable plan construction and complete-vector prefix. Core computes each converter's masks once, pins that descriptor for native calls, and finishes its original byte-run tail. The original offset-based slot remains available. Planned native batches use Core's shared minimum of 512 elements; smaller ranges retain Core's field-wise path. Cipher blocks use Core's original rounds and dispatch over a private state copy. The mixed Latin1 UTF-8 shuffle table is evaluated by the original Core table builder during projection and emitted as primitive constants; it is never maintained independently. Burst's unavailable platform UTF-8 calls use Core's portable codec and existing ASCII widening kernel.

The default UTF-16 profile's compact Latin1 records use dedicated classification, narrowing, widening and reuse-comparison slots. These preserve complete code units and existing compact/raw record headers. Missing slots retain Core's portable path. UTF-8 slots serve their own encoding; choose encoding for the application's data contract, and use the same profile on both sides of a comparison.

Burst 1.8 exposes at most 256-bit x86 intrinsics. No unsupported 512-bit operation is invented: AVX-512 machines use supported 256/128-bit blocks, with exact tails. ARM64 uses Neon; unsupported ARM32/x86 and disabled backends keep Core paths. Managed Burst intrinsic feature properties report false, so a compiled capability probe prevents registering managed scalar callbacks as native SIMD. A probe is feature evidence, not a correctness or speed measurement.

Core pins managed owners for each native invocation. Static delegate roots live with this backend assembly. Calling Core `Clear()` stops new table publication but does not wait for captured calls. Only the application owner may replace/clear the table at a controlled lifecycle boundary; keep this assembly and native code alive until in-flight calls finish. The backend deliberately provides no unconditional global unregister that could erase another owner's table.

Import `Runtime` and its asmdef into a Unity project containing Core's netstandard DLL, its dependencies and `com.unity.burst` 1.8.x. The asmdef is enabled by a package version define; without Burst the optional assembly is excluded. Import source or a precompiled extension, not both. The source uses C# 9 unsafe function pointers and Cdecl signatures. Unity-engine DLLs and Burst binaries are supplied by the project and are not vendored here.

For an isolated build without invoking Core or its generator:

```powershell
dotnet build src/LuminPack.Burst/LuminPack.Burst.csproj -c Release `
  -p:UnityManagedPath="C:/Unity/Editor/Data/Managed/UnityEngine" `
  -p:UnityBurstAssemblyPath="C:/UnityProject/Library/ScriptAssemblies/Unity.Burst.dll" `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

`Tests~` contains portable differential checks and an Editor fixture using real Burst function pointers. The contracts cover complete ranges, alignments, every mismatch position, copy refusal/native fallback, aliasing, periodic overlap, codec flags/status/counts, malformed text, JSON escapes, layout declaration order and cipher RFC vectors/counter wrap. Portable execution does not establish hardware coverage. To run native checks, copy `KernelContract.cs`, `Shared`, `Editor` and the test asmdef into a Unity project's test folder; do not copy the console Program. Execute the fixture on x64 SSE2/AVX2 and ARM64, and test Mono/IL2CPP players before release. A skipped test or a successful C# build is not native validation. Existing Core hardware-policy requirements remain in force.

These kernels implement LuminPack's independently defined ABI. API conventions follow [Unity's official function-pointer documentation](https://docs.unity.cn/Packages/com.unity.burst%401.8/manual/csharp-function-pointers.html).

The manual `Unity extensions` workflow uses prelicensed Windows x64 and macOS ARM64 runners and `.github/scripts/invoke-unity-extensions.ps1`. It runs only the extension contracts in an isolated temporary project. ARM64 and IL2CPP release validation still require actual runs on their respective targets; configuring a workflow is not execution evidence.



