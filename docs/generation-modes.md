# Automatic generation

[Chinese](generation-modes.cn.md) · [Back to README](../README.md)

LuminPack selects operations automatically. The `GeneratorType` option on each model selects its representation independently. See [defining types](defining-types.md).

- Every `[LuminPackable]` declaration contributes its serialized member graph. Libraries retain all four operations for that graph so other assemblies can consume the model.
- Serializer calls retain the concrete payloads they require, including closed generic wrappers, container elements and Union cases.
- Unmarked types observed in unrelated code do not acquire serializer entries merely because they are public.
- Convenience operations are selected by actual calls. Runtime-selected or opaque boundaries use registered fallback.

The removed `Sizeof` and `CalculateOffset` APIs are not generated. Direct allocating writes retain internal, concrete `MeasureRecord` helpers only for supported record shapes; they are not a general size query or a registered fallback operation. Ordinary complex objects, Custom formatters and Unions do not acquire placeholder size methods.

## Operation selection

A console or desktop application, or Unity's `Assembly-CSharp` and `Assembly-CSharp-Editor` without friend access, selects binary write, binary read, JSON write and JSON read independently for each payload. Calling only `Serialize(player)` retains binary write for `Player` and its required member and Union graphs. A JSON call for a separate model does not enable JSON for `Player`. A reference to the LuminPack runtime library does not affect the policy.

A library or a compilation with `InternalsVisibleTo` keeps all four for its discovered model graph because its callers can live outside the current compilation. A direct state call keeps its direction conservatively for the discovered graph. Registration, direct calls to generated formatter extensions, or an unresolved serializer expression can keep all four.

To describe a model in a referenced assembly, declare `[assembly: LuminPackable(typeof(ExternalModel))]` in the consuming assembly. This gives its generator a model to analyze. For an operation that is invoked indirectly and cannot be inferred from source, retain its module explicitly:

```csharp
using LuminPack.Attribute;

[assembly: LuminPackable(typeof(ExternalModel))]
[assembly: LuminPackGeneratorOptions(ForceGenerateDeserializeJson = true)]
```

`LuminPackGeneratorOptions` also exposes `ForceGenerateSerialize`, `ForceGenerateDeserialize`, and `ForceGenerateSerializeJson`. All four default to false and act independently. Each flag keeps its operation for the assembly's discovered payloads. Concrete convenience overloads still follow bound calls. Runtime-selected generic calls still need typed registration when they cannot bind a concrete entry.

## Static interception

Direct serializer calls select payload-specific convenience documentation. Concrete entries used only by specialized wrappers retain their executable bodies and share the generic facade documentation.

The generator package enables the `LuminPack` interceptor namespace automatically. With C# 11 or later and a compiler exposing versioned `GetInterceptableLocation`, the public facade binds a fixed generic signature set. Calls such as `Serialize(value)`, `Serialize<Player>(value)` and `Deserialize<Player>(bytes)` are intercepted into the existing concrete implementation. Each constructed signature shares one interceptor across its source locations. Internal state entries use unique payload names, and allocating/buffer/stream convenience entries live in separate `LuminEntry_<Type>` containers. Only used convenience families are emitted; formatter retention follows the automatic declaration and call graph described above.

Explicit profiles use both type arguments, for example `Serialize<Player, TOptions>(value)`. The profile argument can remain open: it selects compiled options without selecting a payload formatter at runtime. The generator does not emit payload-specific profile convenience overloads on the public facade. Each selected convenience operation retains its existing compiled-profile body; the interceptor supplies the declaration's profile directly for a default call. These are the existing options structs, not formatter Plans.

For source wrappers such as `static byte[] Save<T>(T value) => LuminPackSerializer.Serialize(value)`, closed calls can be intercepted into a concrete copy of the wrapper body. The call graph is indexed by method/type definition, and correlated type arguments are substituted together. Nested static wrappers and ordinary recursion share their concrete bodies. Original overload selection, caller-information constants and side effects are retained.

Specialization is deliberately conservative. Instance/virtual methods, referenced method bodies, inaccessible dependencies, explicit static initialization, async/iterator bodies, lambdas/local functions, reduced extension calls, expanded `params` and operators whose binding depends on an open payload remain on their original path. Specialization depth is bounded. These boundaries require the existing [registered fallback](custom-formatters.md#runtime-registration); registration is not inferred from reachability. Neither path adds a runtime Plan or reflection-based factory. Only explicit and Custom fallback registrations retain typed delegates; ordinary static paths retain no delegates. Existing Union virtual dispatch and cached generic slots remain intact.

The generator still targets Roslyn 4.3; only compiler-host capability discovery uses optional reflection. This does not add reflection to generated serialization code. To disable compiler interception and select the installed IL backend explicitly:

```xml
<PropertyGroup>
  <LuminPackUseInterceptors>false</LuminPackUseInterceptors>
</PropertyGroup>
```

The NuGet package also installs an IL postprocessor. When compiler interception is unavailable, including Roslyn 4.3 and C# 9, the generator emits the same concrete entries with build-only signature mappings. MSBuild replaces matching local static `call` operands after compilation and before copying output. This backend does not need concrete public overloads or PDB location matching. It preserves argument evaluation and verifies parameter and return stack types before writing. Portable PDBs are preserved; signed assemblies require their original `AssemblyOriginatorKeyFile`. It never rewrites referenced assemblies, virtual calls or method-group function pointers.

`LuminPackUseInterceptors=false` selects the IL backend when `LuminPackUseIlRewriting` remains enabled. Disable both properties to use the overload compatibility backend: ordinary default calls retain concrete overloads, while explicit profile calls use registered generic fallback. These are compiler integration controls, not generation tiers. Direct analyzer-DLL installations must install the appropriate integration themselves. Unity uses the same rewriter through `ILPostProcessor`; follow the [Unity installation steps](unity.md). Do not define `LUMINPACK_IL_REWRITE` without installing its postprocessor.

## Registration and migration

Registration remains an independent policy:

```csharp
[assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]
```

The default permits typed-delegate registration regardless of the unsafe compilation setting. This setting does not change static formatter selection.

Remove `LuminPackGenerationMode` MSBuild properties and their `CompilerVisibleProperty` entries. Remove the mode argument from assembly attributes, retaining a registration policy only if needed. The old `Full`, `Medium`, `Light` and `Minimal` enum values and constructor remain as obsolete compatibility syntax; all are ignored and produce the same automatic retention policy, including on older compiler hosts.

Compile consumer assemblies with the generator to obtain their own static bindings. Referenced implementations without usable metadata, method groups and runtime-selected targets require explicit or Custom registration. Reachability alone never registers a formatter. Missing registrations or operations throw `LuminPackException`.

Generation does not change the wire format, reader checks, ownership, reuse or Union dispatch. Intercepted concrete roots use declaration-owned options; registered fallback document boundaries use consuming-assembly defaults or an explicit profile.
