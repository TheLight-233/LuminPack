# Automatic generation

[Chinese](generation-modes.cn.md) · [Back to README](../README.md)

LuminPack uses one automatic generation policy. No generation mode or `CompilerVisibleProperty` is required.

- Every `[LuminPackable]` declaration retains its formatter and serialized member graph, even without a call in its declaring assembly. This keeps model libraries usable by consumers.
- Serializer calls retain the concrete payloads they require, including closed generic wrappers, container elements and Union cases.
- Unmarked types observed in unrelated code do not acquire serializer entries merely because they are public.
- Convenience operations are selected by actual calls. Runtime-selected or opaque boundaries use registered fallback.

## Static interception

The generator package enables the `LuminPack` interceptor namespace automatically. With C# 11 or later and a compiler exposing versioned `GetInterceptableLocation`, the public facade binds a fixed generic signature set. Calls such as `Serialize(value)`, `Serialize<Player>(value)` and `Deserialize<Player>(bytes)` are intercepted into the existing concrete implementation. Each constructed signature shares one interceptor across its source locations. Internal state entries use unique payload names, and allocating/buffer/stream convenience entries live in separate `LuminEntry_<hash>` containers. Only used convenience families are emitted; formatter retention follows the automatic declaration and call graph described above.

The profile-only spelling `Serialize<TOptions>(value)` remains supported through signatures generated only for the payloads that use it. Profile type parameters can remain open: they select compiled options without selecting a payload formatter at runtime. Each selected convenience operation retains its existing compiled-profile body; the interceptor supplies the declaration's profile directly for a default call, so no separate default forwarding shell is needed. These are the existing options structs, not formatter Plans.

For source wrappers such as `static byte[] Save<T>(T value) => LuminPackSerializer.Serialize(value)`, closed calls can be intercepted into a concrete copy of the wrapper body. The call graph is indexed by method/type definition, and correlated type arguments are substituted together. Nested static wrappers and ordinary recursion share their concrete bodies. Original overload selection, caller-information constants and side effects are retained.

Specialization is deliberately conservative. Instance/virtual methods, referenced method bodies, inaccessible dependencies, explicit static initialization, async/iterator bodies, lambdas/local functions, reduced extension calls, expanded `params` and operators whose binding depends on an open payload remain on their original path. Specialization depth is bounded. These boundaries require the existing [registered fallback](custom-formatters.md#runtime-registration); registration is not inferred from reachability. Neither path adds a runtime Plan or reflection-based factory. Only explicit and Custom fallback registrations retain typed delegates; ordinary static paths retain no delegates. Existing Union virtual dispatch and cached generic slots remain intact.

The generator still targets Roslyn 4.3; only compiler-host capability discovery uses optional reflection. This does not add reflection to generated serialization code. To disable compiler interception and select the installed IL backend explicitly:

```xml
<PropertyGroup>
  <LuminPackUseInterceptors>false</LuminPackUseInterceptors>
</PropertyGroup>
```

The NuGet package also installs an IL postprocessor. When compiler interception is unavailable, including Roslyn 4.3 and C# 9, the generator emits the same concrete entries with build-only signature mappings. MSBuild replaces matching local static `call` operands after compilation and before copying output. This backend does not need concrete public overloads or PDB location matching. It preserves argument evaluation and verifies parameter and return stack types before writing. Portable PDBs are preserved; signed assemblies require their original `AssemblyOriginatorKeyFile`. It never rewrites referenced assemblies, virtual calls or method-group function pointers.

`LuminPackUseInterceptors=false` selects the IL backend when `LuminPackUseIlRewriting` remains enabled. Disable both properties to use the overload compatibility backend. These are compiler integration controls, not generation tiers. Direct analyzer-DLL installations must install the appropriate integration themselves. Unity uses the same rewriter through `ILPostProcessor`; follow the [Unity installation steps](unity.md). Do not define `LUMINPACK_IL_REWRITE` without installing its postprocessor.

## Registration and migration

Registration remains an independent policy:

```csharp
[assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]
```

The default permits typed-delegate registration regardless of the unsafe compilation setting. This setting does not change static formatter selection.

Remove `LuminPackGenerationMode` MSBuild properties and their `CompilerVisibleProperty` entries. Remove the mode argument from assembly attributes, retaining a registration policy only if needed. The old `Full`, `Medium`, `Light` and `Minimal` enum values and constructor remain as obsolete compatibility syntax; all are ignored and produce the same automatic retention policy, including on older compiler hosts.

Compile consumer assemblies with the generator to obtain their own static bindings. Referenced implementations without usable metadata, method groups and runtime-selected targets require explicit or Custom registration. Reachability alone never registers a formatter. Missing registrations or operations throw `LuminPackException`.

Generation does not change the wire format, reader checks, ownership, reuse or Union dispatch. Intercepted concrete roots use declaration-owned options; registered fallback document boundaries use consuming-assembly defaults or an explicit profile.
