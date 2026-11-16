using System.Buffers;
using System.Runtime.CompilerServices;

namespace LuminPack.Option;

/// <summary>Selects each known type's declared profile inside the generated generic dispatch.</summary>
/// <remarks>This marker is used by generated forwarding methods; it is never a mutable operation configuration.</remarks>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public readonly struct LuminPackDeclaredOptions : ILuminPackOptions
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

/// <summary>Provides the fixed UTF-8 length profile for generated or caller-authored kernels.</summary>
public readonly struct LuminPackUtf8LengthOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringEncoding.UTF8;
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

/// <summary>Provides the fixed UTF-8 token profile for generated or caller-authored kernels.</summary>
public readonly struct LuminPackUtf8TokenOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringEncoding.UTF8;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringRecording.Token;
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

/// <summary>Provides the fixed UTF-16 length profile for generated or caller-authored kernels.</summary>
public readonly struct LuminPackUtf16LengthOptions : ILuminPackOptions
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

/// <summary>Provides the fixed UTF-16 token profile for generated or caller-authored kernels.</summary>
public readonly struct LuminPackUtf16TokenOptions : ILuminPackOptions
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
        get => LuminPackStringRecording.Token;
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

/// <summary>Provides opt-in base-128 UTF-8 length headers with distinct null and empty records.</summary>
public readonly struct LuminPackUtf8VarIntOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringEncoding.UTF8;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringRecording.VarInt;
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

/// <summary>Provides opt-in base-128 UTF-16 length headers while retaining compact Latin-1 payloads.</summary>
public readonly struct LuminPackUtf16VarIntOptions : ILuminPackOptions
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
        get => LuminPackStringRecording.VarInt;
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

/// <summary>Supplies a stateless standard-format constant.</summary>
public interface ILuminPackFormatOptions
{
    /// <summary>Gets the selected format.</summary>
    StandardFormat Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
}

/// <summary>Supplies a stateless JSON depth constant.</summary>
public interface ILuminPackDepthOptions
{
    /// <summary>Gets the maximum depth.</summary>
    int Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
}

/// <summary>Supplies a stateless JSON grammar policy.</summary>
public interface ILuminPackValidationOptions
{
    /// <summary>Gets whether strict grammar is required.</summary>
    bool Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
}

/// <summary>Provides the standard general format.</summary>
public readonly struct LuminPackGeneralFormat : ILuminPackFormatOptions
{
    /// <inheritdoc/>
    public StandardFormat Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new('G');
    }
}

/// <summary>Provides the default maximum JSON depth.</summary>
public readonly struct LuminPackDefaultDepth : ILuminPackDepthOptions
{
    /// <inheritdoc/>
    public int Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1024;
    }
}

/// <summary>Selects the established trusted JSON parser.</summary>
public readonly struct LuminPackTrustedJson : ILuminPackValidationOptions
{
    /// <inheritdoc/>
    public bool Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }
}

/// <summary>Selects the established strict JSON parser.</summary>
public readonly struct LuminPackStrictJson : ILuminPackValidationOptions
{
    /// <inheritdoc/>
    public bool Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => true;
    }
}

/// <summary>Composes declaration constants without generating another profile implementation for each combination.</summary>
/// <typeparam name="TStrings">The fixed binary string representation and JSON encoding.</typeparam>
/// <typeparam name="TFormat">The fixed standard format.</typeparam>
/// <typeparam name="TDepth">The complete JSON document depth bound.</typeparam>
/// <typeparam name="TValidation">The complete JSON document grammar policy.</typeparam>
public readonly struct LuminPackOptions<TStrings, TFormat, TDepth, TValidation> : ILuminPackOptions
    where TStrings : struct, ILuminPackOptions
    where TFormat : struct, ILuminPackFormatOptions
    where TDepth : struct, ILuminPackDepthOptions
    where TValidation : struct, ILuminPackValidationOptions
{
    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TStrings).StringEncoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TStrings).StringRecording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TFormat).Value;
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TDepth).Value;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TValidation).Value;
    }
}
