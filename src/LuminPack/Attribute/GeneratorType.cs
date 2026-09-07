namespace LuminPack.Attribute;

/// <summary>Specifies the source-generated serialization layout for a LuminPack type.</summary>
public enum GeneratorType : byte
{
    Object,
    VersionTolerant,
    CircleReference,
    NonGenerator,

    /// <summary>
    /// The source generator emits no formatter for the type. The user implements static
    /// Serialize/Deserialize methods (optionally SerializeJson/DeserializeJson and
    /// CalculateOffset) and the generator automatically registers them through
    /// <c>LuminPackSerializer.Register&lt;T&gt;</c> at module initialization.
    /// </summary>
    Custom
}
