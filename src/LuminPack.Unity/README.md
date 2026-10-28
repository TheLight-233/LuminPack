# LuminPack.Unity

Optional Unity value-type codecs, independent of `LuminPack.Burst`. Core references neither extension. The runtime assembly targets netstandard2.1 and uses UnityEngine.CoreModule.

Supported binary values: Vector2/3/4, Vector2Int/Vector3Int, Quaternion, Color/Color32, Rect/RectInt, Bounds/BoundsInt, Ray/Ray2D, Plane, Matrix4x4, LayerMask, Keyframe, GradientColorKey, GradientAlphaKey and RangeInt. Native-owner codecs additionally support AnimationCurve, Gradient and RectOffset through public API values; native handles are never serialized. Values are written as their named scalar components without object framing. Matrix4x4 uses column-major component order. Bounds stores center and extents; BoundsInt stores position and size. Rays preserve direction bits through two sequential vector storage views instead of calling normalizing setters. Field-oriented codecs are an explicit representation and must be selected consistently by both ends.

```csharp
using LuminPack.Unity;

LuminUnity.Register(); // Idempotent manual entry; also invoked automatically before scene loading.
LuminUnity.Write(ref writer, position);
LuminUnity.Read(ref reader, ref position);
```

The static class carries the IL2CPP eager-construction attribute and `Register` carries BeforeSceneLoad runtime initialization. Registration enables the existing generic formatter fallback and preserves already registered application formatters. The generator forwards AnimationCurve, Gradient and RectOffset operations to this module, including ordinary fields and collection elements, without inspecting native handles. Reference this module when generating contracts containing these types. Other value-type contracts retain Core's existing generation and unmanaged paths; registration does not force a raw unmanaged instance call to select a formatter. Use `LuminUnity.Write/Read` in custom formatters when a schema needs these exact field codecs. Both the Writer/Reader and the source generator remain owned by Core and its existing generator; this extension introduces no replacement serializer, type-id map or dispatch mechanism. Resource objects, scene references, GameObjects, textures and native handles are not serialized. These codecs install binary and exact-size callbacks; generated native-owner JSON operations explicitly reject unsupported usage.

Every codec reserves its full output and checks the complete input before accessing a fixed field. Reads stage the value locally and publish it only on complete success. Low-level netstandard Reader/Writer storage must remain pinned for its lifetime, including allocations and callbacks. The generated convenience entries retain their existing pinning responsibility. The codec does not impose portable endianness on Core's native scalar representation.

For Unity source integration, import `Runtime` and its asmdef alongside the netstandard Core DLL and its required dependencies. Do not also import a precompiled copy of LuminPack.Unity. This project deliberately uses read-only assembly references, so building it never rebuilds Core or runs the generator against it:

```powershell
dotnet build src/LuminPack.Unity/LuminPack.Unity.csproj -c Release `
  -p:UnityManagedPath="C:/Unity/Editor/Data/Managed/UnityEngine" `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

`UnityManagedPath` comes from an installed Editor. Its engine DLLs are not copied into the extension distribution. `LuminPackCorePath` defaults to the existing Release/Debug netstandard2.1 Core output. The .NET project is not a standalone NuGet distribution because Unity engine references must be supplied by the host.

The ignored-by-Unity `Tests~` console project checks actual Unity engine value types, all shortened input windows, completed cursor positions, exact sizes, binary round-trip stability, canaries and repeated registration. It requires the same properties. It establishes managed host behavior, not Unity Player/IL2CPP execution. Include actual engine-version/runtime tests before enabling these codecs in a deployed protocol, especially Ray storage assumptions.

AnimationCurve uses an object marker, pre/post wrap modes and counted 32-byte Keyframe records. Keyframe retains time, value, in/out tangents, editor tangent mode, weighted mode and in/out weights through Unity public properties. Unity Player semantics determine the availability/meaning of editor tangent metadata. Gradient carries mode, colorSpace, counted color keys and alpha keys. RectOffset carries left/right/top/bottom. These records follow the LuminPack wire format.

Native-owner reads stage a local cursor and all payload values before touching the destination. Counts and complete payloads are checked before key-array allocation. A complete read reuses an existing native owner; a null record assigns null. Native API exceptions after a validated record can still affect an existing owner; there is no general Unity-native transaction. Size and write operations observe public native state, so callers must not mutate the owner concurrently.

`Tests~/Editor/UnityObjectContract.cs` runs actual curve/gradient/offset construction, reused and fresh reads, every truncated window, null framing, forged counts and curve evaluation in Unity. `Tests~/Consumer` builds a real generated contract with native-owner fields, arrays and lists, then executes that assembly inside Unity. The manual extension workflow runs these fixtures alongside native Burst kernels. The managed console host additionally checks 21 fixed value codecs.
