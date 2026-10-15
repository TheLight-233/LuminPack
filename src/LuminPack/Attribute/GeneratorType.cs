namespace LuminPack.Attribute;

/// <summary>Specifies the source-generated serialization layout for a LuminPack type.</summary>
public enum GeneratorType : byte
{
    /// <summary>Specifies object.</summary>
    Object,
    /// <summary>Specifies version tolerant.</summary>
    VersionTolerant,
    /// <summary>Specifies circle reference.</summary>
    CircleReference,
    /// <summary>Specifies non generator.</summary>
    NonGenerator,

    /// <summary>
    /// The source generator emits no formatter for the type. The user implements static
    /// Serialize/Deserialize methods (optionally SerializeJson/DeserializeJson and
    /// CalculateOffset) and the generator automatically registers them through
    /// <c>LuminPackSerializer.Register&lt;T&gt;</c> at module initialization.
    /// </summary>
    Custom
}
