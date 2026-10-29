using LuminPack.Option;

namespace LuminPack.Attribute;

/// <summary>
/// User guarantee that a type's complete serialized length never exceeds <see cref="MaxBytes"/>.
/// <para>
/// The generator relies on this guarantee to remove every container-layer capacity check emitted from the
/// type's own serialization body, and a container of such elements reserves count * MaxBytes once instead
/// of checking each element. A wrong guarantee can therefore produce an out-of-range write past the segment
/// end and heap corruption. In DEBUG builds the generator also emits a completed-length assertion.
/// The bound includes all headers, fields and nested values, in every supported writer option mode.
/// A root reserves this complete bound before writing. The cursor advances by the actual bytes written.
/// Bounds must be positive; a larger bound can cause additional buffer allocation.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackBoundedAttribute : System.Attribute
{
    /// <summary>Initializes the guarantee with the maximum complete serialized length of one value.</summary>
    /// <param name="maxBytes">The maximum complete serialized length of one value, in bytes.</param>
    public LuminPackBoundedAttribute(int maxBytes) => MaxBytes = maxBytes;

    /// <summary>Gets the maximum complete serialized length of one value, in bytes.</summary>
    public int MaxBytes { get; }
}

/// <summary>
/// User guarantee that a polymorphic base has exactly one implementation in every assembly that can
/// observe it. <para>
/// The declaration retains the existing Union framing and virtual dispatch contract.
/// Prefer <c>LuminPackOption(SingleImplementation = true)</c> to group contract switches.
/// The default "only one subclass exists in the current assembly" is NOT
/// sufficient: another assembly could still add a subclass. Only apply this guarantee when the base
/// cannot be extended elsewhere.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackSingleImplementationAttribute : System.Attribute
{
}

/// <summary>Marks a type for source-generated serialization or registers a referenced type for generation.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class LuminPackableAttribute : System.Attribute
{
    /// <summary>Gets the referenced type marked by an assembly-level attribute.</summary>
    public System.Type? TargetType { get; }

    /// <summary>Initializes a new instance of <c>LuminPackableAttribute</c>.</summary>
    /// <param name="generatorType">The generator type used by this operation.</param>
    public LuminPackableAttribute(GeneratorType generatorType = GeneratorType.Object)
    {
        TargetType = null;
    }

    /// <summary>Marks a referenced type for source-generated serialization in the current assembly.</summary>
    /// <param name="targetType">The referenced type to include in source generation.</param>
    public LuminPackableAttribute(System.Type targetType)
    {
        TargetType = targetType ?? global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException<System.Type>(nameof(targetType));
    }
}

/// <summary>Provides lumin pack order attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackOrderAttribute : System.Attribute
{
    /// <summary>Gets or sets the order.</summary>
    public uint Order { get; set; }

    /// <summary>Initializes a new instance of <c>LuminPackOrderAttribute</c>.</summary>
    /// <param name="order">The order used by this operation.</param>
    public LuminPackOrderAttribute(uint order)
    {
        Order = order;
    }
}

/// <summary>Retains a former binary member name in a version-tolerant contract.</summary>
/// <remarks>Keep names in chronological order. The first old name remains the written identity;
/// all old names and the current member name are accepted when reading.</remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class LuminPackOldNameAttribute : System.Attribute
{
    /// <summary>Gets the former serialized member name.</summary>
    public string Name { get; }

    /// <summary>Records a former serialized member name.</summary>
    /// <param name="name">The exact, case-sensitive former name.</param>
    public LuminPackOldNameAttribute(string name) => Name = name;
}

/// <summary>Provides lumin pack ignore attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackIgnoreAttribute : System.Attribute
{
}

/// <summary>Provides lumin pack include attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackIncludeAttribute : System.Attribute
{
}

/// <summary>Provides lumin pack delegate attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackDelegateAttribute : System.Attribute
{
    /// <summary>Gets or sets the instance name.</summary>
    public string InstanceName { get; set; }
    /// <summary>Gets or sets the method name.</summary>
    public string MethodName { get; set; }

    /// <summary>Initializes a new instance of <c>LuminPackDelegateAttribute</c>.</summary>
    /// <param name="instanceName">The instance name used by this operation.</param>
    /// <param name="methodName">The method name used by this operation.</param>
    public LuminPackDelegateAttribute(string instanceName, string methodName)
    {
        InstanceName = instanceName;
        MethodName = methodName;
    }
}

/// <summary>Provides lumin pack static delegate attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackStaticDelegateAttribute : System.Attribute
{
    /// <summary>Gets or sets the type name.</summary>
    public Type TypeName { get; set; }

    /// <summary>Gets or sets the method name.</summary>
    public string MethodName { get; set; }

    /// <summary>Initializes a new instance of <c>LuminPackStaticDelegateAttribute</c>.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="methodName">The method name used by this operation.</param>
    public LuminPackStaticDelegateAttribute(Type typeName, string methodName)
    {
        TypeName = typeName;
        MethodName = methodName;
    }
}

/// <summary>Provides lumin pack singleton delegate attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackSingletonDelegateAttribute : System.Attribute
{

    /// <summary>Gets or sets the type name.</summary>
    public Type TypeName { get; set; }

    /// <summary>Gets or sets the singleton filed name.</summary>
    public string SingletonFiledName { get; set; }

    /// <summary>Gets or sets the method name.</summary>
    public string MethodName { get; set; }

    /// <summary>Initializes a new instance of <c>LuminPackSingletonDelegateAttribute</c>.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="singletonFiledName">The singleton filed name used by this operation.</param>
    /// <param name="methodName">The method name used by this operation.</param>
    public LuminPackSingletonDelegateAttribute(Type typeName, string singletonFiledName, string methodName)
    {
        TypeName = typeName;
        SingletonFiledName = singletonFiledName;
        MethodName = methodName;
    }

    //Default is TypeName.Instance
    /// <summary>Initializes a new instance of <c>LuminPackSingletonDelegateAttribute</c>.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="methodName">The method name used by this operation.</param>
    public LuminPackSingletonDelegateAttribute(Type typeName, string methodName)
    {
        TypeName = typeName;
        MethodName = methodName;
    }
}

/// <summary>Provides lumin packable object attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackableObjectAttribute : System.Attribute
{
}

/// <summary>Provides lumin pack constructor attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackConstructorAttribute : System.Attribute
{
}

/// <summary>Provides lumin pack union attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class LuminPackUnionAttribute : System.Attribute
{
    /// <summary>Gets the tag.</summary>
    public ushort Tag { get; }

    /// <summary>Gets the type.</summary>
    public Type Type { get; }

    /// <summary>Initializes a new instance of <c>LuminPackUnionAttribute</c>.</summary>
    public LuminPackUnionAttribute()
    {
    }

    /// <summary>Initializes a new instance of <c>LuminPackUnionAttribute</c>.</summary>
    /// <param name="tag">The tag used by this operation.</param>
    /// <param name="type">The type or symbol to analyze.</param>
    public LuminPackUnionAttribute(ushort tag, Type type)
    {
        this.Tag = tag;
        this.Type = type;
    }
}


/// <summary>Provides lumin pack on serializing attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackOnSerializingAttribute : System.Attribute
{
}

/// <summary>Provides lumin pack on serialized attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackOnSerializedAttribute : System.Attribute
{
}

/// <summary>Provides lumin pack on deserializing attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackOnDeserializingAttribute : System.Attribute
{
}

/// <summary>Provides lumin pack on deserialized attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackOnDeserializedAttribute : System.Attribute
{
}

/// <summary>Provides lumin pack pool rent attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackPoolRentAttribute : System.Attribute
{
}

/// <summary>Provides lumin pack wide tag attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackWideTagAttribute : System.Attribute
{
}

/// <summary>Provides lumin pack compress attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackCompressAttribute : System.Attribute
{
}

/// <summary>Selects the source-generator emission strategy for an assembly.</summary>
public enum LuminPackGenerationMode : byte
{
    /// <summary>
    /// Generates formatter extensions for every type observed in the project.  The default and the
    /// most conservative option: any type can be serialized, but generated code is the largest.
    /// </summary>
    Full = 0,

    /// <summary>
    /// Generates the reachable types plus always keeps <c>public</c> non-<c>[LuminPackable]</c> types.
    /// Public types are conservative API surfaces (they may be serialized across assembly boundaries
    /// or via reflection); <c>internal</c> / <c>private</c> types are strictly pruned.  Suitable for
    /// library projects.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// Generates formatter extensions only for types reachable from <c>LuminPackSerializer.*</c> call sites
    /// (including concrete instantiations of generic wrapper methods) and from every <c>[LuminPackable]</c>
    /// member graph.  <c>[LuminPackable]</c> types are always kept even if never serialized.
    /// </summary>
    Light = 2,

    /// <summary>
    /// The most aggressive tier.  Only types actually passed to <c>LuminPackSerializer.*</c> call sites
    /// (and their transitively reachable graphs) are generated.  Even <c>[LuminPackable]</c> types that are
    /// declared but never serialized in this assembly are pruned.  Use only when serialization happens
    /// exclusively through static <c>LuminPackSerializer</c> calls in this assembly.
    /// </summary>
    Minimal = 3,
}

/// <summary>
/// Assembly-level option for the LuminPack source generators.
/// <c>Prune</c> mode only emits formatter extensions that can actually be serialized from this assembly's roots
/// and <c>LuminPackSerializer.*</c> call sites, and suppresses optional formatter variants (compress / fresh-read)
/// that are not referenced. Runtime behavior is unchanged for reachable types.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackGeneratorOptionsAttribute : System.Attribute
{
    /// <summary>Gets the mode.</summary>
    public LuminPackGenerationMode Mode { get; }

    /// <summary>Gets the register.</summary>
    public LuminPackRegisterMode Register { get; }

    /// <summary>Initializes a new instance of <c>LuminPackGeneratorOptionsAttribute</c>.</summary>
    /// <param name="mode">The mode used by this operation.</param>
    /// <param name="register">The register used by this operation.</param>
    public LuminPackGeneratorOptionsAttribute(
        LuminPackGenerationMode mode,
        LuminPackRegisterMode register = LuminPackRegisterMode.Auto)
    {
        Mode = mode;
        Register = register;
    }
}

/// <summary>Controls whether the manual-registration (Register) APIs are generated for an assembly.</summary>
public enum LuminPackRegisterMode : byte
{
    /// <summary>Follow the project's unsafe setting: Register code is generated only when <c>AllowUnsafeBlocks</c> is enabled.</summary>
    Auto = 0,

    /// <summary>Never generate Register code, even when the project enables <c>AllowUnsafeBlocks</c>.</summary>
    Disabled = 1,
}

/// <summary>Declares serialization options that are compiled into generated operations.</summary>
/// <remarks>
/// Unspecified properties inherit the enclosing declaration's options. Assembly options override the library defaults;
/// type and member declarations override the corresponding enclosing scope. Binary string settings can vary by member.
/// JSON encoding, depth and syntax policy describe a complete document and are selected at its root.
/// WideTag and SingleImplementation are declaration-only class/interface contracts and do not inherit enclosing options.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class LuminPackOptionAttribute : System.Attribute
{
    /// <summary>Gets or sets whether this class or interface accepts the wide Union tag form.</summary>
    /// <remarks>Applies only to this declaration, not enclosing scopes or explicit operation profiles. The legacy wide-tag attribute also enables it.</remarks>
    public bool WideTag { get; set; }

    /// <summary>Gets or sets the declaration's guarantee that the polymorphic root has one implementation everywhere.</summary>
    /// <remarks>Applies only to this class or interface. Retains the existing Union framing and virtual dispatch contract.</remarks>
    public bool SingleImplementation { get; set; }

    /// <summary>Gets or sets the binary string encoding, or the JSON document encoding at its root.</summary>
    public LuminPackStringEncoding StringEncoding { get; set; } = LuminPackStringEncoding.UTF16;

    /// <summary>Gets or sets whether binary strings use fixed lengths, variable lengths or encoding-specific terminators.</summary>
    public LuminPackStringRecording StringRecording { get; set; } = LuminPackStringRecording.Length;

    /// <summary>Gets or sets the profile's standard-format constant, available to explicit formatted and Custom kernels.</summary>
    public string StandardFormat { get; set; } = "G";

    /// <summary>Gets or sets the maximum JSON nesting depth; nonpositive values disable the limit.</summary>
    public int MaxJsonDepth { get; set; } = 1024;

    /// <summary>Gets or sets whether the JSON document uses the strict grammar validator.</summary>
    public bool ValidateJsonSyntax { get; set; }

    /// <summary>Gets or sets the generated bounded external-data policy; trusted operations retain their original code when false.</summary>
    /// <remarks>Enables a schema-checked envelope and streaming read quotas. Input must remain stable during decoding; failed reads can retain completed updates. Unsupported external schemas fail closed.</remarks>
    public bool UntrustedData { get; set; }
    /// <summary>Gets or sets the maximum complete encoded input frame size.</summary>
    public int MaxInputBytes { get; set; } = 8 * 1024 * 1024;
    /// <summary>Gets or sets the maximum decompressed payload size.</summary>
    public int MaxDecompressedBytes { get; set; } = 16 * 1024 * 1024;
    /// <summary>Gets or sets the conservative cumulative library allocation budget in bytes.</summary>
    public long MaxAllocationBytes { get; set; } = 64L * 1024 * 1024;
    /// <summary>Gets or sets the maximum element count of one decoded collection.</summary>
    public int MaxCollectionLength { get; set; } = 100_000;
    /// <summary>Gets or sets the maximum string length in UTF-16 code units.</summary>
    public int MaxStringLength { get; set; } = 1024 * 1024;
    /// <summary>Gets or sets the maximum cumulative decoded reference-value count.</summary>
    public int MaxObjectCount { get; set; } = 100_000;
    /// <summary>Gets or sets the maximum external-data value depth, from one through 64.</summary>
    public int MaxDepth { get; set; } = 64;
    /// <summary>Gets or sets the cumulative validation byte, element and comparison work budget.</summary>
    public long MaxReadWork { get; set; } = 32L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the unmanaged binary storage strategy: packed fields, automatic tagged layouts, or native storage.
    /// </summary>
    /// <remarks>
    /// Valid on a struct, which overrides an assembly setting, and on an assembly, where it applies to the ordinary
    /// unmanaged structs declared in that assembly; referenced assemblies keep their own declarations. Nested storage is
    /// expanded recursively in packed storage. Tagged collections copy a matching native layout and otherwise convert
    /// its data bytes. Native storage requires matching layouts at both ends. JSON retains its separate member contract.
    /// </remarks>
    public LuminPackStructLayout StructLayout { get; set; } = LuminPackStructLayout.Tagged;
}

/// <summary>Selects the binary storage contract of unmanaged structs without changing their CLR layout.</summary>
public enum LuminPackStructLayout
{
    /// <summary>Uses tagged contiguous and packed individual storage for differing modeled layouts; stable layouts keep native storage.</summary>
    Tagged = 0,
    /// <summary>Uses declaration-ordered fields without padding on every platform.</summary>
    Packed = 1,
    /// <summary>Uses the complete native storage, requiring identical layouts at both ends.</summary>
    Native = 2
}
