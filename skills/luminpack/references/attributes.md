# LuminPack attribute catalog

Attributes are declared in `LuminPack.Attribute`.

## Type and assembly attributes

| Attribute | Target | Behavior |
| --- | --- | --- |
| `LuminPackable(GeneratorType)` | Class, struct, interface | Enables `Object`, `VersionTolerant`, `CircleReference`, `NonGenerator`, or `Custom` generation. |
| `LuminPackUnion` | Interface or abstract class | Discovers annotated derived types. The `(ushort tag, Type type)` form declares a member explicitly. Tags below 250 use the short representation. |
| `LuminPackWideTag` | Class or interface | Enables the wide union-tag representation. |
| `LuminPackGeneratorOptions` | Assembly | Configures `Full`, `Medium`, `Light`, or `Minimal` generation and `Auto` or `Disabled` registration. |

## Member attributes

| Attribute | Behavior |
| --- | --- |
| `LuminPackOrder(uint)` | Specifies member order. Required for every serialized member in version-tolerant and circular-reference contracts. |
| `LuminPackIgnore` | Excludes a member from ordinary member serialization. Unmanaged whole-storage copying follows its separate storage contract. |
| `LuminPackInclude` | Includes non-public storage through generated accessors and Local mirrors, including nested access on .NET Standard and modern .NET. |
| `LuminPackableObject` | Dispatches through the member type's own formatter instead of inlining its members. |
| `LuminPackDelegate(instanceName, methodName)` | Delegates a member operation to a named instance method. |
| `LuminPackStaticDelegate(type, methodName)` | Delegates a member operation to a static method. |
| `LuminPackSingletonDelegate(type, singletonFieldName, methodName)` | Uses the method on a singleton field. Overloads omitting the field name use `Instance`. |
| `LuminPackFixedLength(uint)` | Declares a fixed collection length for supported shapes. |
| `LuminPackCompress` | Applies LZ compression to supported unmanaged arrays or spans. Managed elements are rejected by LuminPack034. |

## Callbacks and construction

| Attribute | Target | Behavior |
| --- | --- | --- |
| `LuminPackOnSerializing` | Method | Runs before serialization. |
| `LuminPackOnSerialized` | Method | Runs after serialization. |
| `LuminPackOnDeserializing` | Method | Runs before deserialization. |
| `LuminPackOnDeserialized` | Method | Runs after deserialization. |
| `LuminPackPoolRent` | Static method | Supplies deserialization instances. The application owns the pool and returns instances. |
| `LuminPackConstructor` | Constructor | Selects the constructor used by the generated construction path. |

```csharp
// The application implements the pool and return lifecycle.
[LuminPackPoolRent]
public static SimpleClass Rent() => SimpleClassPool.Shared.Rent();
```

## Mapping attributes

| Attribute | Target | Behavior |
| --- | --- | --- |
| `LuminMapTo(typeof(TDestination))` | Source class or struct | Generates creating, in-place, and optional collection mappings. Repeat for multiple destinations. `MethodSuffix` overrides naming; `GenerateCollectionMethods` controls collection overloads. |
| `LuminMapper` | Static partial class | Implements declared static partial mapping methods with application-selected names and signatures. |
| `LuminMapIgnore` | Destination member | Skips assignment. |
| `LuminMapFromMember("SourceName")` | Destination member | Reads a differently named source member. |
| `LuminMapUsing(type, methodName)` | Destination member | Calls a static conversion from the source member type to the destination member type. |
| `LuminMapCondition(type, methodName)` | Destination member | Calls `static bool Method(in TSource source)` before assignment. |
| `LuminMapConstant(value)` | Destination member | Embeds a supported compile-time string, boolean, numeric, or null value. |

## Combining attributes

Include private members at each nesting level with `LuminPackInclude`. A generated mirror preserves the original inheritance and storage layout. Use `LuminPackableObject` when independent formatter dispatch is required.

Version-tolerant and circular-reference types combine the corresponding `LuminPackable` mode with explicit member orders.

Delegate members must use signatures matching their actual member type:

```csharp
[LuminPackStaticDelegate(typeof(MySerializer), nameof(MySerializer.WriteSpecial))]
public SpecialData Data;

[LuminPackSingletonDelegate(typeof(MySerializer), nameof(MySerializer.Instance), nameof(MySerializer.WriteSpecial))]
public SpecialData OtherData;

// Implement the required formatter signature, for example:
// static void WriteSpecial(ref LuminPackWriter writer, in SpecialData value)
```
