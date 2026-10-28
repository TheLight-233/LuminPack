<div align="center">

# 🎮 LuminPack.Unity

**Codecs for Unity value types, curves and gradients**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![netstandard2.1](https://img.shields.io/badge/target-netstandard2.1-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![Unity](https://img.shields.io/badge/Unity-Mono%20%7C%20IL2CPP-000000?style=flat-square&logo=unity&logoColor=white) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

**English** · [简体中文](README.cn.md)

Part of [LuminPack](../../README.md) · [Unity guide](../../docs/unity.md) · [Burst backend](../LuminPack.Burst/README.md)

[Supported types](#-supported-types) · [Quick start](#-quick-start) · [How it works](#-how-it-works) · [Safety](#-safety) · [Installing & building](#-installing--building) · [Testing](#-testing)

</div>

---

Optional codecs for Unity value types, independent of `LuminPack.Burst`. Core references neither extension. The runtime assembly targets netstandard2.1 and uses `UnityEngine.CoreModule`.

## 📦 Supported types

| Category | Types |
| --- | --- |
| Vectors & rotation | `Vector2` · `Vector3` · `Vector4` · `Vector2Int` · `Vector3Int` · `Quaternion` |
| Color | `Color` · `Color32` |
| Bounds & rectangles | `Rect` · `RectInt` · `Bounds` · `BoundsInt` · `RangeInt` |
| Geometry | `Ray` · `Ray2D` · `Plane` · `Matrix4x4` · `LayerMask` |
| Animation keys | `Keyframe` · `GradientColorKey` · `GradientAlphaKey` |
| Native-owner objects | `AnimationCurve` · `Gradient` · `RectOffset` (through public API values; native handles are never serialized) |

<details>
<summary><b>Component layout</b></summary>

- Values are written as their named scalar components, without object framing.
- `Matrix4x4` uses column-major component order.
- `Bounds` stores center and extents; `BoundsInt` stores position and size.
- Rays keep their direction bits through two sequential vector storage views instead of calling normalizing setters.
- `AnimationCurve` writes an object marker, pre/post wrap modes and counted 32-byte `Keyframe` records. A `Keyframe` keeps time, value, in/out tangents, the editor tangent mode, the weighted mode and in/out weights through Unity's public properties; Unity Player semantics decide whether editor tangent metadata is available and what it means.
- `Gradient` carries its mode, `colorSpace`, counted color keys and alpha keys. `RectOffset` carries left/right/top/bottom.

These records follow the LuminPack wire format. Field-oriented codecs are an explicit representation: both ends must choose them consistently.

</details>

## 🚀 Quick start

```csharp
using LuminPack.Unity;

LuminUnity.Register(); // Idempotent manual entry; also invoked automatically before scene loading.
LuminUnity.Write(ref writer, position);
LuminUnity.Read(ref reader, ref position);
```

Use `LuminUnity.Write/Read` in custom formatters when a schema needs these exact field codecs.

## 🔧 How it works

- **Automatic registration** — the static class carries the IL2CPP eager-construction attribute and `Register` runs at `BeforeSceneLoad`. Registration turns on the existing generic formatter fallback and keeps application formatters that are already registered.
- **Generator forwarding** — the generator forwards `AnimationCurve`, `Gradient` and `RectOffset` operations to this module, for ordinary fields and collection elements alike, without inspecting native handles. Reference this module when you generate contracts that contain these types.
- **Core stays in charge** — other value-type contracts keep Core's generation and unmanaged paths; registration never forces a raw unmanaged instance call through a formatter. The Writer/Reader and the source generator still belong to Core — there is no replacement serializer, type-id map or dispatch mechanism.
- **Binary only** — the codecs install binary and exact-size callbacks; generated native-owner JSON operations reject the call explicitly.

> [!NOTE]
> Resource objects, scene references, GameObjects, textures and native handles are not serialized.

## 🔒 Safety

- Every codec reserves its full output and checks the complete input before touching a fixed field. Reads stage the value locally and publish it only when everything succeeded.
- Native-owner reads stage a local cursor and all payload values before touching the destination. Counts and complete payloads are checked before key arrays are allocated. A complete read reuses an existing native owner; a null record assigns null.
- The codecs do not impose portable endianness on Core's native scalar representation.

> [!WARNING]
> - Low-level netstandard Reader/Writer storage must stay pinned for its whole lifetime, including allocations and callbacks. The generated convenience entries keep their existing pinning responsibility.
> - Native API exceptions after a validated record can still affect an existing owner — there is no general Unity-native transaction.
> - Size and write operations observe public native state, so do not mutate the owner concurrently.

## 📥 Installing & building

For a Unity source integration, import `Runtime` and its asmdef next to the netstandard Core DLL and its required dependencies. Do not also import a precompiled copy of LuminPack.Unity.

The project deliberately uses read-only assembly references, so building it never rebuilds Core or runs the generator against it:

```powershell
dotnet build src/LuminPack.Unity/LuminPack.Unity.csproj -c Release `
  -p:UnityManagedPath="C:/Unity/Editor/Data/Managed/UnityEngine" `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

- `UnityManagedPath` comes from an installed Editor; its engine DLLs are not copied into the extension distribution.
- `LuminPackCorePath` defaults to the existing Release/Debug netstandard2.1 Core output.
- The .NET project is not a standalone NuGet distribution, because the host has to supply the Unity engine references.

## 🧪 Testing

| Fixture | What it checks |
| --- | --- |
| `Tests~` console project (ignored by Unity) | Real Unity engine value types, every shortened input window, completed cursor positions, exact sizes, binary round-trip stability, canaries, repeated registration, and 21 fixed value codecs. Needs the same build properties. |
| `Tests~/Editor/UnityObjectContract.cs` | Real curve/gradient/offset construction, reused and fresh reads, every truncated window, null framing, forged counts and curve evaluation inside Unity |
| `Tests~/Consumer` | Builds a real generated contract with native-owner fields, arrays and lists, then runs that assembly inside Unity |

The manual extension workflow runs these fixtures alongside the native Burst kernels.

> [!IMPORTANT]
> The console host proves managed host behavior, not Unity Player or IL2CPP execution. Add real engine-version and runtime tests before enabling these codecs in a deployed protocol — especially for the `Ray` storage assumptions.
