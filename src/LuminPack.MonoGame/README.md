<div align="center">

# 🕹️ LuminPack.MonoGame

**Binary codecs for MonoGame math and collision types**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![net8.0](https://img.shields.io/badge/target-net8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![MonoGame](https://img.shields.io/badge/MonoGame-E73C00?style=flat-square) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

**English** · [简体中文](README.cn.md)

Part of [LuminPack](../../README.md) · [all platform modules](../LuminPack.Platforms/README.md)

[Supported types](#-supported-types) · [Quick start](#-quick-start) · [Guarantees](#-guarantees) · [Building](#-building)

</div>

---

The baseline generator rejects representative MonoGame constructors. These data codecs cover saved positions, transforms, colors and collision volumes — no `Game` or `GraphicsDevice` required.

## 📦 Supported types

| Category | Types |
| --- | --- |
| Vectors & rotation | `Vector2` · `Vector3` · `Vector4` · `Quaternion` |
| Integer geometry | `Point` · `Rectangle` |
| Transforms & color | `Matrix` · `Color` |
| Collision | `BoundingBox` · `BoundingSphere` · `Ray` · `Plane` |

## 🚀 Quick start

### Call a codec directly

```csharp
using LuminPack.MonoGame;

LuminMonoGame.Register(); // Initialize once before concurrent serialization.
var value = new Microsoft.Xna.Framework.Vector2(1, 2);
byte[] bytes = LuminMonoGame.Vector2Codec.Serialize(value);
var restored = LuminMonoGame.Vector2Codec.Deserialize(bytes);
```

Direct codec calls need no registration.

### Use it inside a generated type

For a field or collection element of an ordinary generated DTO, opt in explicitly with the existing Custom mechanism:

```csharp
using LuminPack.Attribute;
using LuminPack.Platforms;

[LuminPackable]
public partial class Snapshot
{
    public LuminPackValue<Microsoft.Xna.Framework.Vector2> Position;
}
```

- `LuminPackValue<T>` converts implicitly to and from the platform value, and adds no type identifier or object frame.
- Call `LuminMonoGame.Register()` before generated entries use it.
- JSON callbacks for these wrappers reject the call explicitly — the platform modules are binary-only.

## ✅ Guarantees

- **Validated reads** — every fixed reader checks its complete range and builds a candidate before publishing the destination or the cursor. Complete-record convenience readers also reject trailing bytes.
- **Core scalar encoding** — scalar representation and byte order follow Core.
- **Core paths unchanged** — raw unmanaged APIs and ordinary unwrapped generated fields keep Core's original behavior; registration never rewrites their contracts.
- **Value records only** — no external object identity, reference cycles, native resources, UI controls, dispatchers, bindings or renderer state. Framework constructors keep their own domain constraints.

> [!WARNING]
> These public-component records are a schema of their own. Do not silently mix them with an existing raw/native-layout record of the same type.

## 🏭 Code generation

`Codecs.json` owns the reviewed component order. Regenerate the deterministic scalar source with `python src/LuminPack.Platforms/CodeGen~/generate_codecs.py`, or check it with `--verify`. The generator neither inspects nor modifies Core; module-specific records stay in focused partial files.

## 🔨 Building

Build Core first, then build this optional project against an existing **net8.0** Core assembly:

```powershell
dotnet build src/LuminPack.MonoGame/LuminPack.MonoGame.csproj -c Release `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

The project uses a read-only assembly reference and never builds Core or its generator. Core references neither this module nor the shared adapter, and these source projects are not published as NuGet packages.

See the [shared platform adapter](../LuminPack.Platforms/README.md) for focused verification and supported boundaries.
