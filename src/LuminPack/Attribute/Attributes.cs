
using System;

namespace LuminPack.Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackableAttribute : System.Attribute
{
    
    public LuminPackableAttribute(GeneratorType generatorType = GeneratorType.Object)
    {
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackOrderAttribute : System.Attribute
{
    public uint Order { get; set; }

    public LuminPackOrderAttribute(uint order)
    {
        Order = order;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackIgnoreAttribute : System.Attribute
{
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackIncludeAttribute : System.Attribute
{
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackDelegateAttribute : System.Attribute
{
    public string InstanceName { get; set; }
    public string MethodName { get; set; }
    
    public LuminPackDelegateAttribute(string instanceName, string methodName)
    {
        InstanceName = instanceName;
        MethodName = methodName;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackStaticDelegateAttribute : System.Attribute
{
    public Type TypeName { get; set; }
    
    public string MethodName { get; set; }

    public LuminPackStaticDelegateAttribute(Type typeName, string methodName)
    {
        TypeName = typeName;
        MethodName = methodName;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackSingletonDelegateAttribute : System.Attribute
{
    
    public Type TypeName { get; set; }
    
    public string SingletonFiledName { get; set; }
    
    public string MethodName { get; set; }

    public LuminPackSingletonDelegateAttribute(Type typeName, string singletonFiledName, string methodName)
    {
        TypeName = typeName;
        SingletonFiledName = singletonFiledName;
        MethodName = methodName;
    }

    //Default is TypeName.Instance
    public LuminPackSingletonDelegateAttribute(Type typeName, string methodName)
    {
        TypeName = typeName;
        MethodName = methodName;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackFixedLengthAttribute : System.Attribute
{
    public uint FixedLength { get; set; }

    public LuminPackFixedLengthAttribute(uint fixedLength)
    {
        FixedLength = fixedLength;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackableObjectAttribute : System.Attribute
{
}

[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackConstructorAttribute : System.Attribute
{
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class LuminPackUnionAttribute : System.Attribute
{
    public ushort Tag { get; }

    public Type Type { get; }

    public LuminPackUnionAttribute()
    {
    }
    
    public LuminPackUnionAttribute(ushort tag, Type type)
    {
        this.Tag = tag;
        this.Type = type;
    }
}


[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackOnSerializingAttribute : System.Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackOnSerializedAttribute : System.Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackOnDeserializingAttribute : System.Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackOnDeserializedAttribute : System.Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackPoolRentAttribute : System.Attribute
{
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackWideTagAttribute : System.Attribute
{
}

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
    public LuminPackGenerationMode Mode { get; }

    public LuminPackGeneratorOptionsAttribute(LuminPackGenerationMode mode)
    {
        Mode = mode;
    }
}
