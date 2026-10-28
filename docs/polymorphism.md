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

- The root, every case and every containing type must be `partial`.
- Tags range from 0 to 65535. Tags 0 to 249 take one byte; larger tags take three. Prefer small tags, and keep them stable: the tag is what identifies a case on the wire.
- `[LuminPackWideTag]` on the root selects the wide tag form for all of its cases.
- Generic cases are supported: an open case (`typeof(Case<>)`) covers the closed types actually used, and a closed case (`typeof(Case<int>)`) has priority over it.
- On .NET 11, a C# `union` declaration marked `[LuminPackable]` is recognized as a Union root and uses the same wire format.

Picking the right case costs a single virtual call — no type checks, no lookups. `[LuminPackSingleImplementation]` tells the generator that a root has exactly one implementation everywhere, so it can call it directly; only use it when no other assembly can add a case.

## Types from referenced assemblies

The generator only sees the source of the current assembly. To generate a type declared in a referenced assembly, list it on the current assembly:

```csharp
[assembly: LuminPackable(typeof(MyLibrary.Model))]
[assembly: LuminPackable(typeof(MyLibrary.Generic<>))]     // the closed types actually used
```

Their private fields and auto-properties are serialized just like those of local types. The type and the constructor used for reading must be accessible.

A Union root from a referenced assembly can be listed the same way. Local `partial` cases then plug into the root automatically, so no manual registration is needed. The generated members that make this work are hidden from IntelliSense.

## Manual registration

When the generator cannot see a case at all, register it at runtime. Registration passes static method pointers, so the project needs `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`; the generator only emits `Register` APIs when unsafe code is enabled. Disable them with `[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Full, LuminPackRegisterMode.Disabled)]`.

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

unsafe
{
    MyLib.IShape.Register<Hexagon>(10, &Write, &Read, &WriteJson, &ReadJson);
}
```

- The tag must be unique and must not collide with a generated case; use `WriteWideUnionHeader` for wide tags.
- All four methods are required. `Sizeof` throws for registered cases.
- Registration is validated: duplicate tags, incompatible case types and missing methods are rejected.
- Registration holds no strong references, so plugins loaded into a collectible `AssemblyLoadContext` can still unload. The code you register must stay loaded while it can be called.

For a type that is not part of a Union and has no generated formatter, use `LuminPackSerializer.Register<T>` or `GeneratorType.Custom`; see [Custom formatters](custom-formatters.md).

## JSON

A Union value is written as `{"$type": tag, "$value": {...}}`; `null` is written as `null`. Both ends must use the same tag-to-type mapping.
