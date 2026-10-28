using System.Buffers;

namespace LuminPack.Option;

/// <summary>Marks a declaration-owned profile which an ambient operation profile cannot override.</summary>
/// <typeparam name="TOptions">The complete profile selected by the declaration.</typeparam>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public readonly struct LuminPackFixedOptions<TOptions> : ILuminPackOptions where TOptions : struct, ILuminPackOptions
{
    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding => default(TOptions).StringEncoding;
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording => default(TOptions).StringRecording;
    /// <inheritdoc/>
    public StandardFormat StandardFormat => default(TOptions).StandardFormat;
    /// <inheritdoc/>
    public int MaxJsonDepth => default(TOptions).MaxJsonDepth;
    /// <inheritdoc/>
    public bool ValidateJsonSyntax => default(TOptions).ValidateJsonSyntax;
}

/// <summary>Resolves the declaration marker to a consuming assembly's constants at closed generic initialization.</summary>
/// <typeparam name="TOptions">The explicit operation profile or declaration marker.</typeparam>
/// <typeparam name="TFallback">The consuming assembly's compiled defaults for unknown registered types.</typeparam>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public readonly struct LuminPackFallbackOptions<TOptions, TFallback> : ILuminPackOptions
    where TOptions : struct, ILuminPackOptions where TFallback : struct, ILuminPackOptions
{
    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).StringEncoding : default(TOptions).StringEncoding;
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).StringRecording : default(TOptions).StringRecording;
    /// <inheritdoc/>
    public StandardFormat StandardFormat => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).StandardFormat : default(TOptions).StandardFormat;
    /// <inheritdoc/>
    public int MaxJsonDepth => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).MaxJsonDepth : default(TOptions).MaxJsonDepth;
    /// <inheritdoc/>
    public bool ValidateJsonSyntax => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).ValidateJsonSyntax : default(TOptions).ValidateJsonSyntax;
}
