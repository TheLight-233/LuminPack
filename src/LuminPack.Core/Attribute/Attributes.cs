using LuminPack.Code;
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
        TargetType = targetType ?? LuminPackExceptionHelper.ThrowArgumentNullException<System.Type>(nameof(targetType));
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

/// <summary>Legacy generation values retained for source and binary compatibility; all values are ignored.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public enum LuminPackGenerationMode : byte
{
    /// <summary>Legacy value; generation is automatic.</summary>
    Full = 0,
    /// <summary>Legacy value; generation is automatic.</summary>
    Medium = 1,
    /// <summary>Legacy value; generation is automatic.</summary>
    Light = 2,
    /// <summary>Legacy value; generation is automatic.</summary>
    Minimal = 3,
}

/// <summary>Controls registration and can retain specific operation modules independently of call analysis.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackGeneratorOptionsAttribute : System.Attribute
{
    /// <summary>Gets the legacy mode value, which no longer affects generation.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public LuminPackGenerationMode Mode { get; }

    /// <summary>Gets the registration API policy.</summary>
    public LuminPackRegisterMode Register { get; }

    /// <summary>Gets or sets whether to retain binary write code for every discovered payload.</summary>
    public bool ForceGenerateSerialize { get; set; }

    /// <summary>Gets or sets whether to retain binary read code for every discovered payload.</summary>
    public bool ForceGenerateDeserialize { get; set; }

    /// <summary>Gets or sets whether to retain JSON write code for every discovered payload.</summary>
    public bool ForceGenerateSerializeJson { get; set; }

    /// <summary>Gets or sets whether to retain JSON read code for every discovered payload.</summary>
    public bool ForceGenerateDeserializeJson { get; set; }

    /// <summary>Selects registration support without a generation mode.</summary>
    /// <param name="register">Whether generated delegate registration is available.</param>
    public LuminPackGeneratorOptionsAttribute(LuminPackRegisterMode register = LuminPackRegisterMode.Auto)
    {
        Mode = LuminPackGenerationMode.Light;
        Register = register;
    }

    /// <summary>Accepts legacy assembly declarations; the mode is ignored.</summary>
    /// <param name="mode">The ignored legacy generation mode.</param>
    /// <param name="register">Whether generated delegate registration is available.</param>
    [Obsolete("Generation is automatic. Remove the mode argument and retain only the registration policy if needed.")]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public LuminPackGeneratorOptionsAttribute(LuminPackGenerationMode mode, LuminPackRegisterMode register = LuminPackRegisterMode.Auto)
    {
        Mode = mode;
        Register = register;
    }
}

/// <summary>Controls whether the manual-registration (Register) APIs are generated for an assembly.</summary>
public enum LuminPackRegisterMode : byte
{
    /// <summary>Generate typed-delegate registration independently of the unsafe compilation setting.</summary>
    Auto = 0,

    /// <summary>Never generate registration APIs or automatic Custom registration.</summary>
    Disabled = 1,
}

/// <summary>Declares serialization options that are compiled into generated operations.</summary>
/// <remarks>
/// Unspecified properties inherit the enclosing declaration's options. Assembly options override the library defaults;
/// type and member declarations override the corresponding enclosing scope. Binary string settings can vary by member.
/// JSON encoding, depth and syntax policy describe a complete document and are selected at its root.
/// WideTag, SingleImplementation, TrackReferences and ReuseRectangularArraySlots are declaration-only contracts and do not inherit enclosing options.
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

    /// <summary>Gets or sets whether this class preserves shared references and cycles.</summary>
    /// <remarks>Applies only to the annotated class and works with either generated binary record format.</remarks>
    public bool TrackReferences { get; set; }

    /// <summary>Gets or sets whether Next reuses existing slots when reading a binary rank-two array of this class.</summary>
    /// <remarks>
    /// Applies to supported ordinary object element declarations on .NET 8 or later. The destination must have the exact array type,
    /// matching dimensions and zero lower bounds. Like single-dimensional arrays and lists, each existing element is read in place, so
    /// slots that share one object observe the last record read into it. A failed read can update the existing array and its elements
    /// in place. The default is true; set it to false to replace the array on every read.
    /// </remarks>
    public bool ReuseRectangularArraySlots { get; set; } = true;

    /// <summary>Gets or sets the binary string encoding, or the JSON document encoding at its root.</summary>
    public LuminPackStringEncoding StringEncoding { get; set; } = LuminPackStringEncoding.UTF16;

    /// <summary>Gets or sets whether binary strings use fixed lengths, variable lengths or encoding-specific terminators.</summary>
    public LuminPackStringRecording StringRecording { get; set; } = LuminPackStringRecording.Length;

    /// <summary>Gets or sets whether binary groupings use an explicit presence marker before their keys.</summary>
    /// <remarks>Next assembly option. Select the same setting at both endpoints, including for groups within lookups.</remarks>
    public bool FramedGroupings { get; set; }

    /// <summary>Gets or sets whether an ordinary packable list subclass serializes its inherited elements.</summary>
    /// <remarks>Apply the same type option at both binary endpoints.</remarks>
    public bool IncludeCollectionBase { get; set; }

    /// <summary>Gets or sets the profile's standard-format constant, available to explicit formatted and Custom kernels.</summary>
    public string StandardFormat { get; set; } = "G";

    /// <summary>Gets or sets the maximum JSON nesting depth; nonpositive values disable the limit.</summary>
    public int MaxJsonDepth { get; set; } = 1024;

    /// <summary>Gets or sets whether the JSON document uses the strict grammar validator.</summary>
    public bool ValidateJsonSyntax { get; set; }

    /// <summary>Gets or sets whether generated binary reads check untrusted input at their read sites. The default is true;
    /// set false on an assembly or type to restore the unchecked reads of trusted data.</summary>
    /// <remarks>Records keep the ordinary wire format. Reads check object markers, Boolean domains, back-reference types and the
    /// declared limits below, frozen as constants. Setting this explicitly, or any limit, also selects strict JSON with the depth limit. Input must remain stable during
    /// decoding; failed reads can retain completed updates. Input-selected types fail closed.</remarks>
    public bool UntrustedData { get; set; } = true;
    /// <summary>Gets or sets the maximum encoded input size accepted by the generated facades; unbounded unless declared.</summary>
    public int MaxInputBytes { get; set; } = int.MaxValue;

    /// <summary>Requires binary document overloads that return a value to consume the entire supplied input.</summary>
    /// <remarks>Assembly declaration only. By-reference and advancing reads retain prefix consumption.</remarks>
    public bool RequireCompleteBinaryInput { get; set; }
    /// <summary>Retained for source compatibility; compressed collections are bounded by their encoded size and the collection limit.</summary>
    [Obsolete("Compressed collections are bounded by their encoded size and MaxCollectionLength; this limit has no effect.")]
    public int MaxDecompressedBytes { get; set; } = 16 * 1024 * 1024;
    /// <summary>Retained for source compatibility; allocations are bounded by the input size instead.</summary>
    [Obsolete("Every untrusted read allocates in proportion to the input it consumes; bound MaxInputBytes instead. This limit has no effect.")]
    public long MaxAllocationBytes { get; set; } = 64L * 1024 * 1024;
    /// <summary>Gets or sets the maximum element count of one decoded binary collection; unbounded unless declared.</summary>
    public int MaxCollectionLength { get; set; } = int.MaxValue;
    /// <summary>Gets or sets the maximum binary string length in UTF-16 code units; unbounded unless declared.</summary>
    public int MaxStringLength { get; set; } = int.MaxValue;
    /// <summary>Retained for source compatibility; object counts are bounded by the input size instead.</summary>
    [Obsolete("Every decoded object consumes input bytes; bound MaxInputBytes instead. This limit has no effect.")]
    public int MaxObjectCount { get; set; } = 100_000;
    /// <summary>Gets or sets the maximum nesting of recursive values and the untrusted JSON document depth, at least one.</summary>
    /// <remarks>Without a declared limit, recursive reads keep only their stack guard and JSON keeps its own depth bound.</remarks>
    public int MaxDepth { get; set; } = int.MaxValue;
    /// <summary>Retained for source compatibility; read work is bounded by the input size instead.</summary>
    [Obsolete("Untrusted reads perform work in proportion to the input they consume; bound MaxInputBytes instead. This limit has no effect.")]
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
