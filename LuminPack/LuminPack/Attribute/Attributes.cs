
using LuminPack.Enum;

namespace LuminPack.Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackableAttribute : System.Attribute
{
    public LuminPackableAttribute(GeneratorType generatorType = GeneratorType.Object)
    {
        
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class LuminPackParserAttribute : System.Attribute
{
    
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

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class LuminPackUnionAttribute : System.Attribute
{
    public ushort Tag { get; }

    public Type Type { get; }

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