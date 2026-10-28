# LuminPack.Avalonia

Optional explicit binary value codecs for Avalonia. The baseline generator rejects representative immutable Avalonia constructors. These codecs reconstruct public values and include perspective matrix components and relative-coordinate units.

Supported data values: `Point`, `Vector`, `Size`, `Rect`, `Thickness`, `CornerRadius`, `PixelPoint`, `PixelVector`, `PixelSize`, `PixelRect`, `Matrix`, `Color`, `RelativePoint`, `RelativeRect`.

```csharp
using LuminPack.Avalonia;

LuminAvalonia.Register(); // Initialize once before concurrent serialization.
var value = new Avalonia.Point(1.25, 2.25);
byte[] bytes = LuminAvalonia.PointCodec.Serialize(value);
var restored = LuminAvalonia.PointCodec.Deserialize(bytes);
```

For a field or collection element in an ordinary generated DTO, select the existing Custom mechanism explicitly:

```csharp
using LuminPack.Attribute;
using LuminPack.Platforms;

[LuminPackable]
public partial class Snapshot
{
    public LuminPackValue<Avalonia.Point> Position;
}
```

The wrapper has implicit conversions to/from its platform value. It adds no type identifier or object frame. Call `LuminAvalonia.Register()` before using generated entries. Direct codec calls require no registration. Raw unmanaged APIs and ordinary unwrapped generated fields retain Core's original behavior; registration does not rewrite their contracts. JSON callbacks for these wrappers explicitly reject unsupported usage. Do not silently mix this public-component schema with a preexisting raw/native-layout record.



All fixed readers validate their complete range and construct a candidate before publishing either destination or cursor. Complete-record convenience readers additionally reject trailing bytes. Scalar encoding follows Core's representation and byte order. These are value records; they do not promise external object identity, reference cycles, native resource persistence, UI controls, dispatchers, bindings or renderer state. Framework constructors retain their own domain constraints.

`Codecs.json` owns the reviewed component order. Regenerate deterministic scalar source with `python src/LuminPack.Platforms/CodeGen~/generate_codecs.py`, or check it with `--verify`. The generator does not inspect or modify Core. Module-specific records remain in focused partial files.

Build Core separately, then build this optional project with an existing **net8.0** Core assembly:

```powershell
dotnet build src/LuminPack.Avalonia/LuminPack.Avalonia.csproj -c Release `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

Core references neither this module nor the shared platform adapter. This project uses a read-only assembly reference and never builds Core or its generator. These source projects are not published NuGet packages. See [the shared adapter](../LuminPack.Platforms/README.md) for focused verification and supported boundaries.
