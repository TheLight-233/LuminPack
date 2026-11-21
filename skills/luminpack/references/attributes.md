# LuminPack attribute catalog

All attributes and `GeneratorType` are in `LuminPack.Attribute`. String option enums are in `LuminPack.Option`.

## Types and assemblies

| Attribute | Target | Behavior |
| --- | --- | --- |
| `[LuminPackable]` | class, struct, record, interface | Mark a type for generation. |
| `[assembly: LuminPackable(typeof(T))]` | assembly | Generate a type from a referenced assembly; repeatable; open generics (`typeof(G<>)`) cover the closed types used; works for Union roots. |
| `[LuminPackOption(GeneratorType = ...)]` | `[LuminPackable]` type | `Default` uses the default representation. `Version` gives record members name keys. `Custom` registers the type's own static methods. |
| `[LuminPackUnion(tag, typeof(Case))]` | interface, abstract class | Declare a Union case explicitly (tags 0..65535; below 250 is one byte). Cases marked `[LuminPackable]` are also collected automatically. |
| `[LuminPackOption(WideTag = true)]` | class/interface Union root | Accept wide tags for registered cases in Next. Generated cases use type-name hashes. Declaration only. `[LuminPackWideTag]` remains compatible. |
| `[LuminPackOption(SingleImplementation = true)]` | class/interface Union root | Promise one implementation everywhere; retains Union framing and, without generated `Register` APIs, lets the single sealed case be called directly. `[LuminPackSingleImplementation]` remains compatible. |
| `[LuminPackOption(TrackReferences = true)]` | class in Default or Version mode | Preserve shared objects and cycles in binary and JSON. The declaration applies only to the annotated class. |
| `[LuminPackBounded(maxBytes)]` | class, struct | Promise a maximum complete binary size; removes inner capacity checks. A wrong bound can overrun the buffer. |
| `[LuminPackOption(...)]` | assembly, type, field, property | Compile-time options; see below. |
| `[assembly: LuminPackGeneratorOptions(register)]` | assembly | `LuminPackRegisterMode.Auto/Disabled`. Generation is automatic; legacy mode arguments are ignored. |

## Members

| Attribute | Behavior |
| --- | --- |
| `[LuminPackOrder(n)]` | Optional write order. It never changes CLR layout. |
| `[LuminPackIgnore]` | Exclude a public member. |
| `[LuminPackInclude]` | Include a private/protected field or property (works through nested levels via generated layout mirrors). |
| `[LuminPackOldName("Name")]` | `Version` only: keep old names readable; list several in chronological order. |
| `[LuminPackableObject]` | Call the member type's own complete formatter. Next also accepts a marked interface member with a manually registered formatter. |
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
| `[LuminPackOnSerializing]`, `[LuminPackOnSerialized]` | instance or static method | Before / after a binary write. |
| `[LuminPackOnDeserializing]`, `[LuminPackOnDeserialized]` | instance or static method | Before / after a binary read. |

At each phase, static callbacks run before instance callbacks, and each group runs from base to derived. For a new instance whose constructor needs decoded members, only static before-read callbacks can run before those members are read. Reference tracking runs callbacks on the first full record for each object.

## `[LuminPackOption]` properties

| Property | Default | Scope notes |
| --- | --- | --- |
| `StringEncoding` | `LuminPackStringEncoding.UTF16` | Per member in binary; JSON uses the root type's. |
| `StringRecording` | `LuminPackStringRecording.Length` | `Token` merges `null` into `""`. |
| `FramedGroupings` | false | Next assembly option. Each present binary grouping in a record or lookup gains a `00` presence byte before its key. Match the setting on write and read. |
| `MaxJsonDepth` | 1024 | Whole JSON document. |
| `ValidateJsonSyntax` | false | Whole JSON document. |
| `StandardFormat` | `"G"` | Constant for Custom/formatted kernels. |
| `WideTag` | false | Class or interface declaration only; accepts wide numeric tags for registered cases in Next. |
| `SingleImplementation` | false | Class/interface declaration only; caller guarantees one implementation everywhere. Retains Union framing; without registration the single sealed case is called directly. |
| `TrackReferences` | false | Concrete class declaration only. The option applies to the annotated class without inheritance or a corresponding operation profile option. |
| `StructLayout` | `LuminPackStructLayout.Tagged` | Assembly or struct. `Packed`: no padding, same bytes on 32/64-bit. `Native`: full native storage, identical layouts required. |
| `UntrustedData` + `Max*` limits | on, limits unbounded | Root type or assembly; checked reads on the ordinary wire format. `false` restores trusted reads; declare limits for hostile input. |
| `RequireCompleteBinaryInput` | false | Assembly only. Rejects trailing bytes after value-returning binary document reads, including explicit profiles and registered reads. Ref and advancing reads consume a prefix. |

These Union switches do not inherit from assemblies, containing types or base types, cannot be selected through operation profiles, and do not pin string profiles. A legacy standalone marker takes precedence over an explicit false option. Only assert `SingleImplementation` when no other assembly can add an implementation.

In Next, values without a type-owned LuminPack contract use the consuming assembly's `StructLayout` option. The default is `Tagged`. Contiguous `Tagged` blocks with multiple models write a verified native layout tag and raw bytes. Matching readers copy the block, and other layouts use the layout converter. `Packed` writes portable bytes without padding and propagates through enclosing structs, pairs and value tuples. A type explicitly declared `Native` is a storage boundary and requires matching runtime layouts. On supported .NET 8+ targets, eligible `Nullable<T>` collection elements follow the underlying type's layout. Packed nullable elements write a marker and the packed payload of a present value.

## Examples

```csharp
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public partial class Player
{
    public int Id;
    [LuminPackOldName("Name")] public string? DisplayName;
}

[LuminPackable, LuminPackOption(TrackReferences = true)]
public partial class Node
{
    public Node? Parent;
    public List<Node>? Children;
}

[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public partial struct Packet { public byte Kind; public long Sequence; }

[LuminPackable]
public abstract partial class Shape { }
[LuminPackable] public sealed partial class Circle : Shape { public float Radius; }
```
