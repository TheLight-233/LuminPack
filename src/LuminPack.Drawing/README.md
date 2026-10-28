# LuminPack.Drawing

Optional explicit binary value codecs for Drawing. Color can be Empty, known/system, named or explicit ARGB. Persisting only ARGB loses that public identity. The baseline generated DTO lacks a Color formatter.

Supported data value: `Color`. Plain drawing geometry does not acquire a duplicate codec in this module.

```csharp
using LuminPack.Drawing;

LuminDrawing.Register(); // Initialize once before concurrent serialization.
var value = System.Drawing.Color.FromName("CustomColor");
byte[] bytes = LuminDrawing.ColorCodec.Serialize(value);
var restored = LuminDrawing.ColorCodec.Deserialize(bytes);
```

For a field or collection element in an ordinary generated DTO, select the existing Custom mechanism explicitly:

```csharp
using LuminPack.Attribute;
using LuminPack.Platforms;

[LuminPackable]
public partial class Snapshot
{
    public LuminPackValue<System.Drawing.Color> Color;
}
```

The wrapper has implicit conversions to/from its platform value. It adds no type identifier or object frame. Call `LuminDrawing.Register()` before using generated entries. Direct codec calls require no registration. Raw unmanaged APIs and ordinary unwrapped generated fields retain Core's original behavior; registration does not rewrite their contracts. JSON callbacks for these wrappers explicitly reject unsupported usage. Do not silently mix this public-component schema with a preexisting raw/native-layout record.

The tested Core baseline already routes ordinary unwrapped System.Drawing.Color fields through its opaque BCL formatter fallback. After `Register()`, that path works directly; a generated DTO fixture verifies it. The explicit wrapper remains useful when selecting a declaration-owned module schema consistently.

Color records retain Empty, known/system identifiers, unknown names and explicit ARGB. They use a kind byte with the matching logical payload.

All fixed readers validate their complete range and construct a candidate before publishing either destination or cursor. Complete-record convenience readers additionally reject trailing bytes. Scalar encoding follows Core's representation and byte order. These are value records; they do not promise external object identity, reference cycles, native resource persistence, UI controls, dispatchers, bindings or renderer state. Framework constructors retain their own domain constraints.

The focused color partial owns this identity representation. No drawing engine, GDI resource, Font, Image or Bitmap handle is persisted.

Build Core separately, then build this optional project with an existing **net8.0** Core assembly:

```powershell
dotnet build src/LuminPack.Drawing/LuminPack.Drawing.csproj -c Release `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

Core references neither this module nor the shared platform adapter. This project uses a read-only assembly reference and never builds Core or its generator. These source projects are not published NuGet packages. See [the shared adapter](../LuminPack.Platforms/README.md) for focused verification and supported boundaries.
