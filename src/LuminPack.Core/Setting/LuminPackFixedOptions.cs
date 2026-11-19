using System.Buffers;
using LuminPack.Core;
using System.Runtime.CompilerServices;

namespace LuminPack.Option;

/// <summary>Marks a declaration-owned profile which an ambient operation profile cannot override.</summary>
/// <typeparam name="TOptions">The complete profile selected by the declaration.</typeparam>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public readonly struct LuminPackFixedOptions<TOptions> : ILuminPackOptions where TOptions : struct, ILuminPackOptions
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        default(TOptions).RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(TOptions).WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        default(TOptions).Write(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        default(TOptions).Read(ref reader, current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        default(TOptions).ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        default(TOptions).ReadSlots(ref reader, slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(TOptions).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TOptions).Encoding;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TOptions).Recording;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TOptions).BinaryProfile;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TOptions).StringEncoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TOptions).StringRecording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TOptions).StandardFormat;
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TOptions).MaxJsonDepth;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TOptions).ValidateJsonSyntax;
    }
}

/// <summary>Resolves the declaration marker to a consuming assembly's constants at closed generic initialization.</summary>
/// <typeparam name="TOptions">The explicit operation profile or declaration marker.</typeparam>
/// <typeparam name="TFallback">The consuming assembly's compiled defaults for unknown registered types.</typeparam>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public readonly struct LuminPackFallbackOptions<TOptions, TFallback> : ILuminPackOptions
    where TOptions : struct, ILuminPackOptions where TFallback : struct, ILuminPackOptions
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).RecordBound(value) : default(TOptions).RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref (typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? ref default(TFallback).WriteRecord(ref writer, ref cursor, value) : ref default(TOptions).WriteRecord(ref writer, ref cursor, value));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value)
    {
        if (typeof(TOptions) == typeof(LuminPackDeclaredOptions))
            default(TFallback).Write(ref writer, value);
        else
            default(TOptions).Write(ref writer, value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).Read(ref reader, current) : default(TOptions).Read(ref reader, current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second)
    {
        if (typeof(TOptions) == typeof(LuminPackDeclaredOptions))
            default(TFallback).ReadPair(ref reader, ref first, ref second);
        else
            default(TOptions).ReadPair(ref reader, ref first, ref second);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots)
    {
        if (typeof(TOptions) == typeof(LuminPackDeclaredOptions))
            default(TFallback).ReadSlots(ref reader, slots);
        else
            default(TOptions).ReadSlots(ref reader, slots);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock) : default(TOptions).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).Encoding : default(TOptions).Encoding;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).Recording : default(TOptions).Recording;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).BinaryProfile : default(TOptions).BinaryProfile;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).StringEncoding : default(TOptions).StringEncoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).StringRecording : default(TOptions).StringRecording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).StandardFormat : default(TOptions).StandardFormat;
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).MaxJsonDepth : default(TOptions).MaxJsonDepth;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => typeof(TOptions) == typeof(LuminPackDeclaredOptions) ? default(TFallback).ValidateJsonSyntax : default(TOptions).ValidateJsonSyntax;
    }
}
