namespace LuminPack.Attribute;

/// <summary>Specifies the source-generated serialization layout for a LuminPack type.</summary>
public enum GeneratorType : byte
{
    /// <summary>Uses the default representation for the declared type.</summary>
    Default = 0,

    /// <summary>Identifies binary record members by name so their schema can evolve.</summary>
    Version = 1,

    /// <summary>
    /// The source generator emits no formatter for the type. The user implements static
    /// Serialize/Deserialize methods (optionally SerializeJson/DeserializeJson) and the
    /// generator automatically registers them on first use of the serializer.
    /// </summary>
    Custom = 4,
}
