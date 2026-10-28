# LuminPack.Platforms

Shared typed adapters for optional platform modules. Core depends on none of these projects. The adapter invokes existing Core Reader/Writer/Evaluator operations; it supplies no reflection serializer, runtime type map, SIMD algorithm or replacement layout mechanism.

| Module | Serialization purpose |
| --- | --- |
| LuminPack.AspNetCore | MVC/Minimal API binary bodies and logical HTTP value types, including multi-value/null-element headers. |
| LuminPack.Avalonia | Public immutable geometry, relative coordinates, colors and complete matrices for DTOs. |
| LuminPack.Drawing | WinForms/System.Drawing data including named/system/empty color identity. |
| LuminPack.Wpf | Public geometry, Empty sentinels, transforms, grid sizing and linear scRGB color. |
| LuminPack.Maui | MAUI Graphics portable geometry and nullable managed color; no mobile UI dependency. |
| LuminPack.MonoGame | Saved mathematical/game-data values without a Game or graphics context. |
| LuminPack.SkiaSharp | Geometry, color and perspective matrix values without native rendering handles. |

Use each module's typed `...Codec.Serialize/Deserialize` entries directly. For generated DTO fields and collection elements, explicitly use `LuminPackValue<T>` and initialize the relevant module with `Register()`. It uses the existing opaque Custom formatter mechanism and retains concrete generic registration. The wrapper adds no wire framing. Explicit application entries are preserved. Initialization must finish before concurrent serialization; registered code and module codecs must remain loaded.

Registration does not intercept ordinary statically generated/raw unmanaged operations. It does not opt every platform object into serialization. Existing native-layout records and these explicit component records are distinct schemas. JSON is deliberately unsupported by platform Custom wrappers. No native object handles, UI controls, resource ownership or reference identity are inferred.

`LuminPackCodec<T>` groups a complete binary write/read/size operation. Root conveniences check exact written length and reject trailing input before assigning the destination. Module readers check the complete fixed range and stage cursor/destination publication. Scalar width, architecture-specific access and byte order remain owned by Core. An arbitrary application codec must uphold the same value-publication and range contracts; the adapter does not make user callbacks transactional.

Core's existing unmanaged paths already cover many plain platform values. Add a module/type only when there is an actual serialization gap, constructor/representation issue or framework transport boundary. Do not add native-resource persistence by copying an internal pointer. External types that already work do not need another package.

The projects use read-only references to an existing net8.0 `LuminPack.Core.dll`; they never build Core or its generator. Supply `LuminPackCorePath` when outputs are elsewhere. The generator is needed only by the generated-consumer tests; supply `LuminPackGeneratorPath` with its matching existing assembly. No package is published by these projects.

Run only the new module contracts with matching prebuilt dependencies:

```powershell
& 'src/LuminPack.Platforms/Tests~/Run.ps1' `
  -CorePath 'C:/Artifacts/LuminPack.Core.dll' `
  -GeneratorPath 'C:/Artifacts/LuminPack.Generator.dll'
```

The script snapshots those assemblies, builds only these optional projects and executes their console contracts. Windows also runs WPF; other hosts skip that Windows-only module. It does not run the full solution, existing unit suites or benchmarks. Tests cover public/byte round trips, complete sizes, every truncated input/output window, canaries, malformed markers, retained registrations and actual generated fields/arrays/lists. HTTP tests additionally use the actual ASP.NET Core pipeline.

Managed type-contract checks are not execution coverage of a complete Avalonia/MAUI/MonoGame/Skia application, a mobile device, trimming or NativeAOT. Application deployment and target-platform execution remain separate validation. The common projects target net8.0; WPF targets net8.0-windows. Platform package versions are explicit and can be changed only with relevant contract verification.
