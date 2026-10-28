<div align="center">

# 🔥 LuminPack.Burst

**Burst-compiled SIMD kernels for LuminPack in Unity**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![Burst 1.8](https://img.shields.io/badge/Burst-1.8-000000?style=flat-square&logo=unity&logoColor=white) ![ABI v3](https://img.shields.io/badge/kernel%20ABI-v3-F97316?style=flat-square) ![x64 | ARM64](https://img.shields.io/badge/arch-x64%20%7C%20ARM64-2EA043?style=flat-square) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

**English** · [简体中文](README.cn.md)

Part of [LuminPack](../../README.md) · [Unity guide](../../docs/unity.md) · [Burst hooks](../../docs/burst-hooks.md) · [Unity value codecs](../LuminPack.Unity/README.md)

[Quick start](#-quick-start) · [Kernels](#-kernels) · [Hardware](#-hardware) · [Lifetime](#-lifetime) · [Installing & building](#-installing--building) · [Testing](#-testing)

</div>

---

Optional Burst 1.8 backend for version 3 of the netstandard2.1 `LuminPack.Core.LuminPackBurst` ABI. It references Core, `Unity.Burst` and `UnityEngine.CoreModule`; Core and LuminPack.Unity do not reference it. Modern .NET builds keep using their own native SIMD.

## 🚀 Quick start

```csharp
using LuminPack.Burst;

bool installed = BurstBackend.TryRegister();
```

- `Register()` has a `BeforeSceneLoad` runtime hook, and its declaring class carries the IL2CPP eager-construction attribute.
- Manual `TryRegister()` is still available; it returns `false` without replacing an existing Core table.
- Disabled Burst, WebGL and a probe that reports no compiled SIMD all keep Core's paths.
- Compilation finishes before a table is published, so a failure can never publish partial entries.

## 🧮 Kernels

The table fills all **sixteen** ABI slots:

| Area | Slots |
| --- | --- |
| Memory | copy · ordinary equality · equal-prefix matching · periodic match expansion · XOR |
| UTF-8 | encode · decode · equality · short ASCII encode |
| Compact Latin1 | classification · narrowing · widening · equality |
| Layout | prefix conversion · precomputed layout prefixes |
| Cipher | ChaCha20 block generation |

### Projected from Core, never forked

The kernels are compiled from an **automatic projection of existing Core methods**. `CodeGen~` reads the authoritative Core syntax with Roslyn, and `Runtime/Generated` is regenerated on .NET builds with source hashes. The original branches, traversal order, complete-block bounds and tails all come from Core. `KernelVectors`, `KernelMemory` and `PortableUtf8` map native primitives and Core's portable platform fallback.

> [!CAUTION]
> Never edit generated algorithms by hand. Run the projection tool before exporting source into Unity, and use `--verify` to detect stale output. `--core-source-root` selects a fixed source copy for reproducible isolated validation without building Core.

<details>
<summary><b>Kernel details</b></summary>

**Copy.** `LuminKernelPolicy` is shared by Core and Burst. Copy uses the original head/middle/tail schedule up to 256 bytes, or up to 128 bytes when only narrow lanes exist. A 64-bit process and the required hardware are mandatory. Unsupported sizes or features return zero without writing, and Core runs its existing `Unsafe.CopyBlockUnaligned` fallback. There is no second large-copy loop and no alternative streaming policy.

**Matching and XOR** keep Core's exact vector thresholds and scalar tails. Periodic expansion keeps Core's fill/non-overlap/overlap branches and its eight-byte remainder path.

**Text.** UTF codecs keep replacement behavior, streaming status and consumed/produced counts. ASCII keeps Core's eligibility rules and validates before writing. Native text entry points represent UTF-16 code units as `ushort*`, because Burst 1.8 rejects `char*` external functions; table publication uses Core's unchanged, binary-equivalent pointer signatures. Ordinary equality maps the platform memory-compare primitive, while authentication-tag comparison stays on Core's original constant-time path.

**Compact Latin1.** The default UTF-16 profile's compact Latin1 records use dedicated classification, narrowing, widening and reuse-comparison slots. They keep complete code units and the existing compact/raw record headers. Missing slots keep Core's portable path. UTF-8 slots serve their own encoding; choose the encoding for the application's data contract and use the same profile on both sides of a comparison.

**Layout.** Layout conversion shares Core's immutable plan construction and complete-vector prefix. Core computes each converter's masks once, pins that descriptor for native calls and finishes its original byte-run tail. The original offset-based slot is still available. Planned native batches use Core's shared minimum of 512 elements; smaller ranges keep Core's field-wise path.

**Cipher and tables.** Cipher blocks use Core's original rounds and dispatch over a private state copy. The mixed Latin1 UTF-8 shuffle table is evaluated by the original Core table builder during projection and emitted as primitive constants — it is never maintained separately. Where Burst cannot call the platform UTF-8 APIs, it uses Core's portable codec and the existing ASCII widening kernel.

</details>

## 💻 Hardware

| Platform | Path |
| --- | --- |
| x64 with AVX2 / SSE2 | Up to 256-bit blocks (the widest Burst 1.8 exposes) |
| x64 with AVX-512 | Supported 256/128-bit blocks with exact tails — no unsupported 512-bit operation is invented |
| ARM64 | Neon |
| ARM32, x86, disabled backends | Core's paths |

Managed Burst intrinsic feature properties report `false`, so a compiled capability probe prevents managed scalar callbacks from being registered as native SIMD. A probe is feature evidence — not a correctness or speed measurement.

## 🔄 Lifetime

Core pins managed owners for every native call, and static delegate roots live with this backend assembly. Calling Core's `Clear()` stops new table publication but does not wait for calls that already captured the table.

> [!WARNING]
> Only the application owner may replace or clear the table, at a controlled lifecycle boundary. Keep this assembly and its native code alive until in-flight calls finish. The backend deliberately offers no unconditional global unregister that could erase another owner's table.

## 📥 Installing & building

Import `Runtime` and its asmdef into a Unity project that contains Core's netstandard DLL, its dependencies and `com.unity.burst` 1.8.x.

- The asmdef is enabled by a package version define; without Burst the optional assembly is excluded.
- Import the source **or** a precompiled extension, not both.
- The source uses C# 9 unsafe function pointers and Cdecl signatures.
- Unity engine DLLs and Burst binaries come from your project and are not vendored here.

For an isolated build that does not invoke Core or its generator:

```powershell
dotnet build src/LuminPack.Burst/LuminPack.Burst.csproj -c Release `
  -p:UnityManagedPath="C:/Unity/Editor/Data/Managed/UnityEngine" `
  -p:UnityBurstAssemblyPath="C:/UnityProject/Library/ScriptAssemblies/Unity.Burst.dll" `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

## 🧪 Testing

`Tests~` holds portable differential checks and an Editor fixture that uses real Burst function pointers. The contracts cover complete ranges, alignments, every mismatch position, copy refusal and native fallback, aliasing, periodic overlap, codec flags/status/counts, malformed text, JSON escapes, layout declaration order, and cipher RFC vectors with counter wrap.

To run the native checks, copy `KernelContract.cs`, `Shared`, `Editor` and the test asmdef into a Unity project's test folder (not the console `Program`). Run the fixture on x64 SSE2/AVX2 and ARM64, and test Mono and IL2CPP players before a release.

The manual **Unity extensions** workflow uses prelicensed Windows x64 and macOS ARM64 runners and `.github/scripts/invoke-unity-extensions.ps1`. It runs only the extension contracts, in an isolated temporary project.

> [!IMPORTANT]
> Portable execution does not prove hardware coverage, and a skipped test or a successful C# build is not native validation. ARM64 and IL2CPP release validation still need real runs on those targets — a configured workflow is not execution evidence. Core's existing hardware-policy requirements stay in force.

---

These kernels implement LuminPack's independently defined ABI. API conventions follow [Unity's official function-pointer documentation](https://docs.unity.cn/Packages/com.unity.burst%401.8/manual/csharp-function-pointers.html).
