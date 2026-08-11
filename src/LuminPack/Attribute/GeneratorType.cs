namespace LuminPack.Attribute;

/// <summary>Specifies the source-generated serialization layout for a LuminPack type.</summary>
public enum GeneratorType : byte
{
    Object,
    VersionTolerant,
    CircleReference,
    NonGenerator
}
