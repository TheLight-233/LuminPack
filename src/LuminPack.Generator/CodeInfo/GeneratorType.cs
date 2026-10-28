namespace LuminPack.Code;

/// <summary>Provides generator type functionality.</summary>
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

    /// <summary>User-supplied static formatters are validated and registered; formatter extensions are not generated.</summary>
    Custom
}
