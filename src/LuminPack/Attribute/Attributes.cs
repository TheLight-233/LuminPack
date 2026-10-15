namespace LuminPack.Attribute;

/// <summary>Provides lumin packable attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackableAttribute : System.Attribute
{

    /// <summary>Initializes a new instance of <c>LuminPackableAttribute</c>.</summary>
    /// <param name="generatorType">The generator type used by this operation.</param>
    public LuminPackableAttribute(GeneratorType generatorType = GeneratorType.Object)
    {
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

/// <summary>Provides lumin pack fixed length attribute functionality.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackFixedLengthAttribute : System.Attribute
{
    /// <summary>Gets or sets the fixed length.</summary>
    public uint FixedLength { get; set; }

    /// <summary>Initializes a new instance of <c>LuminPackFixedLengthAttribute</c>.</summary>
    /// <param name="fixedLength">The fixed length used by this operation.</param>
    public LuminPackFixedLengthAttribute(uint fixedLength)
    {
        FixedLength = fixedLength;
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
