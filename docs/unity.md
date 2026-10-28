# 🎮 Unity, IL2CPP & NativeAOT

[Chinese](unity.cn.md) · [Back to README](../README.md)

## Unity

LuminPack supports Unity Mono and IL2CPP through its `netstandard2.1` asset. Set the API compatibility level to **.NET Standard 2.1**.

Install the `LuminPack` package with a NuGet workflow such as NuGetForUnity, or import it manually:

1. Put the `netstandard2.1` `LuminPack.Core.dll` from the package and its dependencies under `Assets/Plugins`.
2. Put `LuminPack.Generator.dll` under `Assets/RoslynAnalyzers` and give it the `RoslynAnalyzer` label in the Inspector.
3. Declare one `[LuminPackable]` type and make sure the project compiles, which confirms that Unity runs the generator.

On `netstandard2.1` the reader and writer cursors are native pointers. Generated entry points pin managed input for the whole call; low-level readers and writers you create over managed memory need that memory pinned for their whole lifetime.

The 32-bit IL2CPP rule applies throughout: fixed values larger than four bytes are moved with block copies, never with typed unaligned access, which can crash IL2CPP.

Run an IL2CPP player build on each target platform before release; a successful compile is not enough. The repository's `tests/Unity` project is a Mono/IL2CPP smoke test.

### Optional Unity packages

| Package | Purpose |
| --- | --- |
| `LuminPack.Unity` | Codecs for Unity value types: vectors, `Quaternion`, `Color`, `Rect`, `Bounds`, `Matrix4x4`, `LayerMask`, `AnimationCurve`, `Gradient`, `RectOffset` and more. See [its README](../src/LuminPack.Unity/README.md). |
| `LuminPack.Burst` | Burst 1.8 backend for the `netstandard2.1` native kernel table (copies, UTF-8/Latin-1 text, layout conversion, compression and ChaCha20). See [its README](../src/LuminPack.Burst/README.md) and the [hook ABI](burst-hooks.md). |

Core references neither package. Both register before scene load.

## NativeAOT and trimming

Generated code uses no reflection, so serialization works under NativeAOT and full trimming. `tests/LuminPack.Tests.Aot` publishes and runs a trimmed and a native executable in CI.

`System.Type` is the exception: on .NET 8+, when dynamic code is not supported (NativeAOT), reading a non-null `Type` throws `PlatformNotSupportedException`. Writing a `Type` and reading `null` still work. Where dynamic code is available, types are resolved by name and their metadata must survive trimming.

If you use `Full` generation with full trimming and never need dynamic code, set `<DynamicCodeSupport>false</DynamicCodeSupport>`. With dynamic code enabled, full trimming reports IL2057 for name-based `Type` resolution until you decide how to keep that metadata.
