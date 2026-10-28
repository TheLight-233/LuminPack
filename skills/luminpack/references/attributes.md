# LuminPack attribute catalog

All attributes are in `LuminPack.Attribute`; option enums are in `LuminPack.Option`.

## Types and assemblies

| Attribute | Target | Behavior |
| --- | --- | --- |
| `[LuminPackable]` | class, struct, record, interface | Generate a formatter. `GeneratorType`: `Object` (default, positional), `VersionTolerant` (by name), `CircleReference` (reference tracking), `Custom` (your static methods), `NonGenerator`. |
| `[assembly: LuminPackable(typeof(T))]` | assembly | Generate a type from a referenced assembly; repeatable; open generics (`typeof(G<>)`) cover the closed types used; works for Union roots. |
| `[LuminPackUnion(tag, typeof(Case))]` | interface, abstract class | Declare a Union case explicitly (tags 0..65535; below 250 is one byte). Cases marked `[LuminPackable]` are also collected automatically. |
| `[LuminPackWideTag]` | Union root | Use the wide tag form for all cases. |
| `[LuminPackSingleImplementation]` | Union root | Promise exactly one implementation everywhere, allowing direct calls. Only when no other assembly can add a case. |
| `[LuminPackBounded(maxBytes)]` | class, struct | Promise a maximum complete binary size; removes inner capacity checks. A wrong bound can overrun the buffer. |
| `[LuminPackOption(...)]` | assembly, type, field, property | Compile-time options; see below. |
| `[assembly: LuminPackGeneratorOptions(mode, register)]` | assembly | `LuminPackGenerationMode.Full/Medium/Light/Minimal`; `LuminPackRegisterMode.Auto/Disabled`. |

## Members

| Attribute | Behavior |
| --- | --- |
| `[LuminPackOrder(n)]` | Write order. Required on every member of `CircleReference` types; optional elsewhere. Never changes CLR layout. |
| `[LuminPackIgnore]` | Exclude a public member. |
| `[LuminPackInclude]` | Include a private/protected field or property (works through nested levels via generated layout mirrors). |
| `[LuminPackOldName("Name")]` | `VersionTolerant` only: keep old names readable; list several in chronological order. |
| `[LuminPackableObject]` | Call the member type's own formatter instead of inlining it (needed for `Custom` members). |
| `[LuminPackFixedLength(n)]` | Fixed collection length for supported shapes. |
| `[LuminPackCompress]` | LZ-compress a supported unmanaged array/span member. Managed elements are rejected (LuminPack034). |
| `[LuminPackDelegate(instanceName, methodName)]` | Route the member through a method on an instance member. |
| `[LuminPackStaticDelegate(typeof(T), methodName)]` | Route the member through a static method. |
| `[LuminPackSingletonDelegate(typeof(T), singletonField, methodName)]` | Route through a method of a singleton; the two-argument form uses `Instance`. |

Delegate targets use the formatter signature of the member type, for example `static void WriteSpecial(ref LuminPackWriter writer, in SpecialData value)`.

## Construction and callbacks

| Attribute | Target | Behavior |
| --- | --- | --- |
| `[LuminPackConstructor]` | constructor | Constructor used for fresh reads; parameter names must match serialized members. |
| `[LuminPackPoolRent]` | static method | Supplies fresh instances instead of `new`. The application returns them to its pool. |
| `[LuminPackOnSerializing]`, `[LuminPackOnSerialized]` | instance or static method | Before / after writing. |
| `[LuminPackOnDeserializing]`, `[LuminPackOnDeserialized]` | instance or static method | Before / after reading. |

## `[LuminPackOption]` properties

| Property | Default | Scope notes |
| --- | --- | --- |
| `StringEncoding` | `LuminPackStringEncoding.UTF16` | Per member in binary; JSON uses the root type's. |
| `StringRecording` | `LuminPackStringRecording.Length` | `Token` merges `null` into `""`. |
| `MaxJsonDepth` | 1024 | Whole JSON document. |
| `ValidateJsonSyntax` | false | Whole JSON document. |
| `StandardFormat` | `"G"` | Constant for Custom/formatted kernels. |
| `StructLayout` | `LuminPackStructLayout.Tagged` | Assembly or struct. `Packed`: no padding, same bytes on 32/64-bit. `Native`: full native storage, identical layouts required. |
| `UntrustedData` + `Max*` limits | off | Root type or assembly; bounded protocol for hostile input. |

## Examples

```csharp
[LuminPackable(GeneratorType.VersionTolerant)]
public partial class Player
{
    public int Id;
    [LuminPackOldName("Name")] public string? DisplayName;
}

[LuminPackable(GeneratorType.CircleReference)]
public partial class Node
{
    [LuminPackOrder(0)] public Node? Parent;
    [LuminPackOrder(1)] public List<Node>? Children;
}

[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public partial struct Packet { public byte Kind; public long Sequence; }

[LuminPackable]
public abstract partial class Shape { }
[LuminPackable] public sealed partial class Circle : Shape { public float Radius; }
```
