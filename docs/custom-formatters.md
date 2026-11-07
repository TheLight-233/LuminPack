# ✍️ Custom Formatters

[Chinese](custom-formatters.cn.md) · [Back to README](../README.md)

## `GeneratorType.Custom`

`[LuminPackable(GeneratorType.Custom)]` replaces the generated representation with static methods you write on the type. The generator validates them and registers them in a module initializer; you never call `Register` yourself.

```csharp
using LuminPack.Attribute;
using LuminPack.Core;

[LuminPackable(GeneratorType.Custom)]
public sealed class MyVector
{
    public float X, Y, Z;

    public static void Serialize(ref LuminPackWriter writer, in MyVector value)
    {
        writer.WriteValue(value.X);
        writer.WriteValue(value.Y);
        writer.WriteValue(value.Z);
    }

    public static void Deserialize(ref LuminPackReader reader, ref MyVector value)
    {
        value ??= new MyVector();
        reader.ReadValue(ref value.X);
        reader.ReadValue(ref value.Y);
        reader.ReadValue(ref value.Z);
    }
}
```

Provide exactly one of these method sets:

| Set | Methods |
| --- | --- |
| Binary | `Serialize`, `Deserialize` |
| Binary and JSON | plus `SerializeJson(ref LuminPackJsonWriter, in T)` and `DeserializeJson(ref LuminPackJsonReader, ref T)` |

- Methods must be `static` and `public` or `internal`.
- Your methods own the complete representation: null handling, reuse and string encoding.
- Diagnostics `LuminPack100` to `LuminPack108` report missing or non-static methods, wrong signatures, half a JSON pair, unsupported generic types or Union roots, and skipped registration when unsafe code is disabled.
- When a `Custom` type is a member of another `[LuminPackable]` type, mark that member `[LuminPackableObject]` so it calls your formatter instead of being inlined.
- Default JSON registrations use UTF-16. A UTF-8 JSON document needs a registration for the matching closed profile.

## Runtime registration

`LuminPackSerializer.Register<T>` registers static method pointers for generic fallback entry points. Registration is required when the implementation is opaque or the call remains open, even for built-in and source-generated types. On the modern [interceptor backend](generation-modes.md#static-interception), closed generic syntax can bind a concrete Root directly and does not request registered dispatch. The installed IL backend provides the same static bindings on older compilers; without either integration, generic payload APIs require registration. Registration needs `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.

```csharp
static void Write(ref LuminPackWriter writer, in LegacyModel value)
{
    writer.Write(value.Id);
    writer.Write(value.Name);
}

static void Read(ref LuminPackReader reader, ref LegacyModel value)
{
    value = new LegacyModel();
    reader.Read(ref value.Id);
    reader.Read(ref value.Name);
}

unsafe
{
    LuminPackSerializer.Register<LegacyModel>(&Write, &Read);
}
```

| Overload | Methods |
| --- | --- |
| `Register<T>(write, read)` | Binary |
| `Register<T>(write, read, writeJson, readJson)` | Binary and JSON |
| `Register<T, TOptions>(...)` | The same, for one closed option profile |

For an already generated model, the callbacks can call its concrete state overloads:

```csharp
static void WritePerson(ref LuminPackWriter writer, in Person value)
    => LuminPackSerializer.Serialize(ref writer, in value);
static void ReadPerson(ref LuminPackReader reader, ref Person value)
    => LuminPackSerializer.Deserialize(ref reader, ref value);
unsafe { LuminPackSerializer.Register<Person>(&WritePerson, &ReadPerson); }
```

Those callback bodies select concrete operations. Calling `Serialize<Person>` inside `WritePerson` would re-enter the same registered generic operation.

- Registering the same type twice throws `ArgumentException`.
- Concrete roots and intercepted calls never consult the registry. Generic fallback bodies use explicit and `Custom` registrations only. An absent registration or missing operation throws `LuminPackException`; there is no automatic generated fallback. Without unsafe support, or with `LuminPackRegisterMode.Disabled`, fallback operations cannot use Register. Fallback JSON boundaries and input staging use the consuming assembly defaults or the explicit profile; the registered callback owns its representation and any declaration-specific policy. Intercepted roots retain declaration-owned policies.
- Registration holds no strong references, so plugins loaded into a collectible `AssemblyLoadContext` can still unload. Keep the registered code loaded while it can be called.

## Member-level formatters

To route one member through your own method instead of a whole type, use a delegate attribute whose target has the formatter signature of the member type:

```csharp
[LuminPackStaticDelegate(typeof(MySerializer), nameof(MySerializer.WriteSpecial))]
public SpecialData Data;

[LuminPackSingletonDelegate(typeof(MySerializer), nameof(MySerializer.Instance), nameof(MySerializer.WriteSpecial))]
public SpecialData OtherData;

// static void WriteSpecial(ref LuminPackWriter writer, in SpecialData value)
```

`[LuminPackDelegate(instanceName, methodName)]` uses an instance member of the containing type.

## Low-level API

Custom code works with the advancing writer and reader in `LuminPack.Core`:

- `LuminPackWriter`: `WriteValue`, `Write<TOptions>(string)`, unmanaged values and spans, null markers, object and Union headers.
- `LuminPackReader`: the matching `ReadValue`, `Read<TOptions>(ref string)`, spans and headers. `EnsureReadable(count)` validates input before you read it.
- `LuminPackJsonWriter` / `LuminPackJsonReader`: JSON tokens, property names and values.
- `LuminPackMarshal` (in `LuminPack.Code`): reinterpretation, collection storage access and unaligned copies. Respect the documented layout and lifetime rules.
