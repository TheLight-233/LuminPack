using System.Buffers;

namespace LuminPack.Option;

/// <summary>Describes a stateless value-type profile whose constant properties specialize runtime kernels.</summary>
/// <remarks>Implementations must not contain instance state. Generated profiles return declaration-time constants.</remarks>
public interface ILuminPackOptions
{
    /// <summary>Gets the selected string encoding.</summary>
    LuminPackStringEncoding StringEncoding { get; }
    /// <summary>Gets the binary string boundary representation.</summary>
    LuminPackStringRecording StringRecording { get; }
    /// <summary>Gets the standard format for supported formatted values.</summary>
    StandardFormat StandardFormat { get; }
    /// <summary>Gets the complete JSON document's maximum depth.</summary>
    int MaxJsonDepth { get; }
    /// <summary>Gets whether the complete JSON document requires strict grammar validation.</summary>
    bool ValidateJsonSyntax { get; }
}

/// <summary>Provides the fixed UTF-8, length-prefixed, trusted-JSON library defaults.</summary>
public readonly struct LuminPackDefaultOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF8;
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
    /// <inheritdoc/>
    public StandardFormat StandardFormat => new('G');
    /// <inheritdoc/>
    public int MaxJsonDepth => 1024;
    /// <inheritdoc/>
    public bool ValidateJsonSyntax => false;
}
