# 🎮 Unity, IL2CPP & NativeAOT

[Chinese](unity.cn.md) · [Back to README](../README.md)

## Unity

LuminPack supports Unity Mono and IL2CPP through its `netstandard2.1` asset. Set the API compatibility level to **.NET Standard 2.1**.

Install the `LuminPack` package with a NuGet workflow such as NuGetForUnity, or import it manually:

1. Put the `netstandard2.1` `LuminPack.Core.dll` from the package and its dependencies under `Assets/Plugins`.
2. Put `LuminPack.Generator.dll` under `Assets/RoslynAnalyzers` and give it the `RoslynAnalyzer` label in the Inspector.
3. Install `com.unity.nuget.mono-cecil` (1.11.4 or compatible). Copy the files from `src/LuminPack.ILPostProcessor/Core` and `src/LuminPack.ILPostProcessor/Unity` into one `Assets/LuminPack.CodeGen` directory, keeping `Unity.LuminPack.CodeGen.asmdef` with them.
4. Add `-define:LUMINPACK_IL_REWRITE` to `Assets/csc.rsp`. The matching generator and postprocessor are installed together; the define alone does not perform rewriting.
5. Declare one `[LuminPackable]` type and verify both `Serialize(value)` and a closed static `Save<T>` wrapper. Unity's postprocessor replaces their calls before Mono loading and IL2CPP conversion. Opaque targets retain registered fallback.

From a repository checkout, `python .github/scripts/stage_unity_il.py /path/to/project` installs the postprocessor sources, Cecil dependency and compiler define without changing the runtime DLL installation. The build-only rewriter is not a runtime dependency. See [automatic generation](generation-modes.md) for source-wrapper boundaries, compiler capability selection and compatibility controls.

On `netstandard2.1` the reader and writer cursors store managed references in one-element `Span<byte>` fields. The GC tracks caller-owned managed storage through the spans. `LuminBufferWriter` pins its own pooled array for the rental to preserve 64-byte alignment.

The 32-bit IL2CPP rule applies throughout: fixed values larger than four bytes are moved with block copies, never with typed unaligned access, which can crash IL2CPP.

Run an IL2CPP player build on each target platform before release; a successful compile is not enough. The repository's `tests/Unity` project is a Mono/IL2CPP smoke test.

### Optional Unity packages

| Package | Purpose |
| --- | --- |
| `LuminPack.Unity` | Codecs for Unity value types: vectors, `Quaternion`, `Color`, `Rect`, `Bounds`, `Matrix4x4`, `LayerMask`, `AnimationCurve`, `Gradient`, `RectOffset` and more. See [its README](../src/LuminPack.Unity/README.md). |
| `LuminPack.Burst` | Burst 1.8 backend for the `netstandard2.1` native kernel table (copies, UTF-8/Latin-1 text, layout conversion, compression and ChaCha20). See [its README](../src/LuminPack.Burst/README.md) and the [hook ABI](burst-hooks.md). |

Core references neither package. Both register before scene load.

### Native-owner scratch and Unity versions

`LuminPack.Unity` preserves the explicit binary fields of `AnimationCurve`, `Gradient` and `RectOffset`. Their JSON codecs remain unsupported and reject both UTF-8 and UTF-16 calls, including null roots.

On Unity 6000.2 or later, curve and gradient key snapshots use the public Span APIs. Up to 16 keys per set use bounded stack storage; larger sets use returned `ArrayPool<T>` storage. With retained output storage and a warmed pool, size queries, writes and reads into existing owners need no temporary managed allocation. A fresh read still creates its required native-owner wrapper. Pool misses, including after shared-pool trimming, can allocate, so this is not a cold-start zero-allocation guarantee.

Earlier Unity versions use the public curve indexer to capture write snapshots and `length` for sizing. Their curve reads and gradient operations still require managed key arrays because the corresponding Span APIs are unavailable. `RectOffset` needs no key scratch. All versions validate complete payloads before changing a reused owner, and size/write operations require that the source owner is not modified concurrently.

Unity source integration supplies version symbols automatically. When building the optional module outside Unity against 6000.2 or later engine assemblies, also pass `-p:DefineConstants=UNITY_6000_2_OR_NEWER`; omit that symbol for older engines. The extension validation script derives this choice from the selected Editor version.

## NativeAOT and trimming

Generated code uses no reflection, so serialization works under NativeAOT and full trimming. `tests/LuminPack.Tests.Aot` publishes and runs a trimmed and a native executable in CI.

`System.Type` is the exception: on .NET 8+, when dynamic code is not supported (NativeAOT), reading a non-null `Type` throws `PlatformNotSupportedException`. Writing a `Type` and reading `null` still work. Where dynamic code is available, types are resolved by name and their metadata must survive trimming.

With full trimming, if you never need dynamic code, set `<DynamicCodeSupport>false</DynamicCodeSupport>`. With dynamic code enabled, full trimming reports IL2057 for name-based `Type` resolution until you decide how to keep that metadata.
