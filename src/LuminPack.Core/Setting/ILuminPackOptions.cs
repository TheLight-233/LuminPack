using System.Buffers;
using System.Runtime.CompilerServices;

namespace LuminPack.Option;

/// <summary>Describes a stateless value-type profile whose constant properties specialize runtime kernels.</summary>
/// <remarks>Implementations must not contain instance state. Generated profiles return declaration-time constants.</remarks>
public interface ILuminPackOptions
{
    /// <summary>Gets the selected string encoding.</summary>
    LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
    /// <summary>Gets the binary string boundary representation.</summary>
    LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
    /// <summary>Gets the standard format for supported formatted values.</summary>
    StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
    /// <summary>Gets the complete JSON document's maximum depth.</summary>
    int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
    /// <summary>Gets whether the complete JSON document requires strict grammar validation.</summary>
    bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
}

/// <summary>Provides the fixed UTF-16, length-prefixed, trusted-JSON library defaults.</summary>
public readonly struct LuminPackDefaultOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringEncoding.UTF16;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringRecording.Length;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new('G');
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1024;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }
}
