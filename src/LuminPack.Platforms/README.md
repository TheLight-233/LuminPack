<div align="center">

# 🧩 LuminPack.Platforms

**The shared typed adapter behind every optional platform module**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![net8.0](https://img.shields.io/badge/target-net8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

**English** · [简体中文](README.cn.md)

Part of [LuminPack](../../README.md)

[Modules](#-modules) · [How it works](#-how-it-works) · [Guarantees](#-guarantees) · [Building & testing](#-building--testing)

</div>

---

Shared typed adapters for the optional platform modules. Core depends on none of these projects. The adapter calls existing Core Reader/Writer operations; it brings no reflection serializer, runtime type map, SIMD algorithm or replacement layout mechanism.

## 📦 Modules

| Module | What it serializes |
| --- | --- |
| 🌐 [LuminPack.AspNetCore](../LuminPack.AspNetCore/README.md) | MVC/Minimal API binary bodies and logical HTTP value types, including multi-value and null-element headers |
| 🖼️ [LuminPack.Avalonia](../LuminPack.Avalonia/README.md) | Public immutable geometry, relative coordinates, colors and complete matrices |
| 🎨 [LuminPack.Drawing](../LuminPack.Drawing/README.md) | WinForms/System.Drawing data, including named/system/empty color identity |
| 🪟 [LuminPack.Wpf](../LuminPack.Wpf/README.md) | Public geometry, Empty sentinels, transforms, grid sizing and linear scRGB color |
| 📱 [LuminPack.Maui](../LuminPack.Maui/README.md) | MAUI Graphics portable geometry and nullable managed color, with no mobile UI dependency |
| 🕹️ [LuminPack.MonoGame](../LuminPack.MonoGame/README.md) | Saved math and game-data values, without a Game or graphics context |
| 🖌️ [LuminPack.SkiaSharp](../LuminPack.SkiaSharp/README.md) | Geometry, color and perspective matrix values, without native rendering handles |

Unity has its own modules: [LuminPack.Unity](../LuminPack.Unity/README.md) and [LuminPack.Burst](../LuminPack.Burst/README.md).

## 🔧 How it works

**Direct codecs.** Every module exposes typed `...Codec.Serialize/Deserialize` entries you can call directly.

**Generated fields.** For fields and collection elements of a generated DTO, use `LuminPackValue<T>` explicitly and initialize the module with its `Register()`:

- It rides on the existing opaque Custom formatter mechanism and keeps concrete generic registration.
- The wrapper adds no wire framing, and explicit application entries are preserved.
- Initialization must finish before concurrent serialization; registered code and module codecs must stay loaded.

**`LuminPackCodec<T>`** groups a complete binary write/read/size operation. Root conveniences check the exact written length and reject trailing input before assigning the destination.

## ✅ Guarantees

- **Staged publication** — module readers check the complete fixed range and stage cursor and destination publication. Scalar width, architecture-specific access and byte order stay owned by Core.
- **No hidden interception** — registration does not intercept ordinary statically generated or raw unmanaged operations, and it does not opt every platform object into serialization.
- **Distinct schemas** — existing native-layout records and these explicit component records are different schemas.
- **Binary only** — JSON is deliberately unsupported by platform Custom wrappers.
- **Nothing inferred** — no native object handles, UI controls, resource ownership or reference identity.

> [!IMPORTANT]
> An application codec of your own must uphold the same value-publication and range contracts — the adapter does not make user callbacks transactional.

### When to add a module

Core's existing unmanaged paths already cover many plain platform values. Add a module or type only for a real serialization gap, a constructor/representation problem or a framework transport boundary. Never add native-resource persistence by copying an internal pointer, and don't add a package for external types that already work.

## 🔨 Building & testing

The projects use read-only references to an existing net8.0 `LuminPack.Core.dll` and never build Core or its generator. Pass `LuminPackCorePath` when the outputs live elsewhere. The generator is needed only by the generated-consumer tests; pass `LuminPackGeneratorPath` with its matching assembly. These projects publish no package.

Run only the module contracts against matching prebuilt dependencies:

```powershell
& 'src/LuminPack.Platforms/Tests~/Run.ps1' `
  -CorePath 'C:/Artifacts/LuminPack.Core.dll' `
  -GeneratorPath 'C:/Artifacts/LuminPack.Generator.dll'
```

The script snapshots those assemblies, builds only these optional projects and runs their console contracts. Windows also runs WPF; other hosts skip that Windows-only module. It does not run the full solution, the existing unit suites or the benchmarks.

| Covered | Not covered |
| --- | --- |
| Public and byte round trips, complete sizes | A complete Avalonia/MAUI/MonoGame/Skia application |
| Every truncated input/output window, canaries | Mobile devices |
| Malformed markers, retained registrations | Trimming or NativeAOT |
| Real generated fields, arrays and lists | Application deployment and target-platform execution |
| HTTP through the real ASP.NET Core pipeline | |

The common projects target net8.0; WPF targets net8.0-windows. Platform package versions are explicit and may change only with the relevant contract verification.
