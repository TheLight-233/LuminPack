# 🎭 Polymorphism

[Chinese](polymorphism.cn.md) · [Back to README](../README.md)

## Union roots

An interface or abstract class marked `[LuminPackable]` is a Union root. The generator collects every `[LuminPackable]` implementation in the assembly and gives each one a tag. You can also list cases explicitly with `[LuminPackUnion(tag, typeof(Case))]`.

```csharp
[LuminPackable]
[LuminPackUnion(0, typeof(Circle))]
[LuminPackUnion(1, typeof(Square))]
public abstract partial class Shape { }

[LuminPackable] public sealed partial class Circle : Shape { public float Radius; }
[LuminPackable] public sealed partial class Square : Shape { public float Side; }

Shape value = new Circle { Radius = 2 };
byte[] bytes = LuminPackSerializer.Serialize(value);

Shape copy = null!;
LuminPackSerializer.Deserialize(bytes, ref copy);   // copy is Circle
```

Set `IncludeDerivedTypes = true` on a marked class to include its source-derived classes without repeating `[LuminPackable]` on each one. Discovery follows the class inheritance chain through intermediate types. Each derived type uses its own options and remains subject to the ordinary construction and Union rules.

```csharp
[LuminPackable, LuminPackOption(IncludeDerivedTypes = true)]
public abstract partial class Message { }

public abstract partial class NetworkMessage : Message { }
public sealed partial class Ping : NetworkMessage { public int Sequence; }
```

- In Next, if the root has more than 32 generated cases, the root, the cases and their containing types must be `partial` for virtual dispatch. Smaller roots use exact type comparisons, including for generic cases.
- Numeric tags range from 0 to 65535. They identify JSON and explicitly registered cases in Next.
- Generic cases are supported: an open case (`typeof(Case<>)`) covers the closed types actually used, and a closed case (`typeof(Case<int>)`) has priority over it.
- On .NET 11, both generators support C# `union` declarations marked `[LuminPackable]`. Binary and JSON assign tags in case declaration order beginning at zero. A default Union with no case uses the null marker.

For eligible local, non-generic abstract class roots, the generator selects the write tier once from the target framework and the root's declared cases. Every tier uses the same wire format.

| Target | Dispatch |
| --- | --- |
| Below .NET 8 | One exact type pattern `switch` covering every generated case. |
| .NET 8 and later | Direct call for a closed single case, exact type pattern `switch` for two to four cases or an open single case, virtual call for five or more cases. |

A direct call requires one sealed case, no other derived types in source, disabled registration and a root that other assemblies cannot extend. The pattern switch uses the existing virtual call for values outside its exact cases. Lists, arrays, immutable arrays and dictionaries follow the same tier for each element. Interface, generic and referenced roots retain their existing dispatch.

Next binary class and interface Unions write a four-byte little-endian FNV-1a hash of the canonical UTF-8 type name for each generated case. The canonical name uses namespaces, `+` for nesting, and fully qualified type arguments in angle brackets. An open type parameter is written in the canonical name as `!0`, `!1`, and so forth. For example, `Example.Case<System.Int32>` and `Example.Case<!0>` have different hashes. The generator rejects collisions, zero and `0xFFFFFFFF`. Renaming a case changes its binary schema. The type name text is never written to the stream.

Binary `null` is four zero bytes. Registered cases use `0xFFFFFFFF` followed by the writer delegate's existing numeric tag and payload. `[LuminPackOption(WideTag = true)]` selects the tag width in that registered path. Explicit numeric tags also remain the JSON case IDs. A new binary reader expects hash headers. The earlier one-byte and three-byte generated case headers belong to a different binary schema.

## Types from referenced assemblies

The generator only sees the source of the current assembly. To generate a type declared in a referenced assembly, list it on the current assembly:

```csharp
[assembly: LuminPackable(typeof(MyLibrary.Model))]
[assembly: LuminPackable(typeof(MyLibrary.Generic<>))]     // the closed types actually used
```

Their private fields and auto-properties are serialized just like those of local types. The type and the constructor used for reading must be accessible.

Use a [targeted assembly option](options.md#options-for-types-from-referenced-assemblies) to configure generation for one imported type.

A Union root from a referenced assembly can be listed the same way. Local `partial` cases then plug into the root automatically, so no manual registration is needed. The generated members that make this work are hidden from IntelliSense.

## Manual registration

When the generator cannot see a case at all, register it at runtime. Registration accepts typed delegates and is generated independently of `AllowUnsafeBlocks`. Pass method groups directly, without `&` or an `unsafe` block. Disable them with `[assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]`.

The root's generated `Register<TCase>` takes a tag and four methods, written against the root type:

```csharp
static void Write(ref LuminPackWriter writer, ref MyLib.IShape value)
{
    writer.WriteUnionHeader(10);
    writer.WritePolymorphismValue(LuminPackMarshal.As<MyLib.IShape, Hexagon>(ref value));
}

static void Read(ref LuminPackReader reader, ref MyLib.IShape value)
{
    Hexagon item = default!;
    reader.ReadPolymorphismValue(ref item);
    value = LuminPackMarshal.As<Hexagon, MyLib.IShape>(ref item!);
}

// WriteJson(ref LuminPackJsonWriter, ref MyLib.IShape) writes {"$type":10,"$value":...};
// ReadJson(ref LuminPackJsonReader, ref MyLib.IShape) reads the value.

MyLib.IShape.Register<Hexagon>(10, Write, Read, WriteJson, ReadJson);
```

- The tag must be unique and must not collide with a generated case; use `WriteWideUnionHeader` for wide tags.
- All four methods are required.
- Registration is validated: duplicate tags, incompatible case types and missing methods are rejected.
- Delegate registrations remain alive with their payload type. Conditional weak ownership allows collectible payload assemblies to unload; a noncollectible payload keeps its registered delegates and their targets alive.

For a type that is not part of a Union and has no generated formatter, use `LuminPackSerializer.Register<T>` or `GeneratorType.Custom`; see [Custom formatters](custom-formatters.md).

## JSON

A Union value is written as `{"$type": tag, "$value": {...}}`; `null` is written as `null`. Both ends must use the same tag-to-type mapping.
