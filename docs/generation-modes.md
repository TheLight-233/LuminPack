# 🪶 Generation Modes

[Chinese](generation-modes.cn.md) · [Back to README](../README.md)

By default the generator emits code for every type it observes. Large projects can prune that output to the types that are actually serialized, which shrinks the assembly and speeds up builds.

| Mode | Generated formatters | Concrete `LuminPackSerializer` overloads |
| --- | --- | --- |
| `Full` (default) | Every observed type. | Every overload shape for every kept type. |
| `Medium` | Reachable types, plus every `public` type (a conservative surface for libraries). | Every overload shape. |
| `Light` | Types reachable from `LuminPackSerializer.*` calls and from every `[LuminPackable]` member graph. | Only the overload shapes that are actually called. |
| `Minimal` | Only types reachable from `LuminPackSerializer.*` calls. Unused `[LuminPackable]` types are pruned too. | Only the overload shapes that are actually called. |

Reachability starts from:

- every `LuminPackSerializer.*` call and the type it receives;
- generic wrappers: `void Save<T>(T x) => LuminPackSerializer.Serialize(x);` called as `Save<Player>()` roots `Player`;
- type arguments, array elements and member types, recursively;
- the cases of any reachable Union root.

## Selecting a mode

With an MSBuild property:

```xml
<PropertyGroup>
  <LuminPackGenerationMode>Light</LuminPackGenerationMode>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackGenerationMode" />
</ItemGroup>
```

`CompilerVisibleProperty` is required so that the generator can read the property.

Or with an assembly attribute:

```csharp
[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]
```

When both are present, the more aggressive mode wins. The attribute's second argument, `LuminPackRegisterMode.Disabled`, also turns off the generated `Register` APIs.

## Caveats

- `Light` and `Minimal` retain only statically proven concrete API shapes. Use `Full` or `Medium` to retain more concrete APIs for external callers; calls that bind a generic payload API still require registration.
- `Minimal` assumes every serialization happens through static `LuminPackSerializer` calls in the same assembly. Otherwise use `Light`.
- Pruning never changes the wire format or the formatter used for a type; only which code exists.
- Every generic payload `LuminPackSerializer` call needs [registration](custom-formatters.md#runtime-registration), including wrapper calls and types inside the generated set. Reachability alone does not register a formatter. `Full` and `Medium` retain more concrete APIs but do not provide automatic generic fallback.
