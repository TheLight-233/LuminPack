<div align="center">

# 🎨 LuminPack.Drawing

**Identity-preserving codec for System.Drawing.Color**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![net8.0](https://img.shields.io/badge/target-net8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![System.Drawing](https://img.shields.io/badge/System.Drawing-0078D4?style=flat-square) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

**English** · [简体中文](README.cn.md)

Part of [LuminPack](../../README.md) · [all platform modules](../LuminPack.Platforms/README.md)

[Supported types](#-supported-types) · [Quick start](#-quick-start) · [Guarantees](#-guarantees) · [Building](#-building)

</div>

---

A `Color` can be Empty, a known/system color, a named color or explicit ARGB — persisting only ARGB loses that public identity. The baseline generated DTO has no `Color` formatter of its own.

## 📦 Supported types

| Category | Types |
| --- | --- |
| Color | `Color` — Empty, known/system, named and explicit ARGB |

Plain drawing geometry is already handled by Core and does not get a duplicate codec here.

## 🚀 Quick start

### Call a codec directly

```csharp
using LuminPack.Drawing;

LuminDrawing.Register(); // Initialize once before concurrent serialization.
var value = System.Drawing.Color.FromName("CustomColor");
byte[] bytes = LuminDrawing.ColorCodec.Serialize(value);
var restored = LuminDrawing.ColorCodec.Deserialize(bytes);
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
    public LuminPackValue<System.Drawing.Color> Color;
}
```

- `LuminPackValue<T>` converts implicitly to and from the platform value, and adds no type identifier or object frame.
- Call `LuminDrawing.Register()` before generated entries use it.
- JSON callbacks for these wrappers reject the call explicitly — the platform modules are binary-only.

### Unwrapped fields

The tested Core baseline already routes ordinary unwrapped `System.Drawing.Color` fields through its opaque BCL formatter fallback. After `Register()` that path works directly, and a generated DTO fixture verifies it. The explicit wrapper is still useful when you want the declaration to own this module's schema consistently.

### Wire record

Color records keep Empty, known/system identifiers, unknown names and explicit ARGB, using a kind byte followed by the matching logical payload. The focused color partial owns this representation. No drawing engine, GDI resource, `Font`, `Image` or `Bitmap` handle is persisted.

## ✅ Guarantees

- **Validated reads** — every fixed reader checks its complete range and builds a candidate before publishing the destination or the cursor. Complete-record convenience readers also reject trailing bytes.
- **Core scalar encoding** — scalar representation and byte order follow Core.
- **Core paths unchanged** — raw unmanaged APIs and ordinary unwrapped generated fields keep Core's original behavior; registration never rewrites their contracts.
- **Value records only** — no external object identity, reference cycles, native resources, UI controls, dispatchers, bindings or renderer state. Framework constructors keep their own domain constraints.

> [!WARNING]
> These public-component records are a schema of their own. Do not silently mix them with an existing raw/native-layout record of the same type.

## 🔨 Building

Build Core first, then build this optional project against an existing **net8.0** Core assembly:

```powershell
dotnet build src/LuminPack.Drawing/LuminPack.Drawing.csproj -c Release `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

The project uses a read-only assembly reference and never builds Core or its generator. Core references neither this module nor the shared adapter, and these source projects are not published as NuGet packages.

See the [shared platform adapter](../LuminPack.Platforms/README.md) for focused verification and supported boundaries.
