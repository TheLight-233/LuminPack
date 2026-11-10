using System;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>The serialization operations a project calls.</summary>
[Flags]
internal enum GeneratedOperations
{
    /// <summary>No operation is called.</summary>
    None = 0,

    /// <summary>Binary serialization.</summary>
    BinaryWrite = 1 << 0,

    /// <summary>Binary deserialization.</summary>
    BinaryRead = 1 << 1,

    /// <summary>JSON serialization.</summary>
    JsonWrite = 1 << 2,

    /// <summary>JSON deserialization.</summary>
    JsonRead = 1 << 3,

    /// <summary>Every operation.</summary>
    All = BinaryWrite | BinaryRead | JsonWrite | JsonRead,
}
