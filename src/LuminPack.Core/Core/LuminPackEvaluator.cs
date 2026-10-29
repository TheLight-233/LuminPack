using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Core;

/// <summary>Accumulates the exact binary width of a generated direct record before its allocating write.</summary>
/// <remarks>Generated code uses this accumulator to size the output array of a direct record exactly. It is not a public
/// size API; it supports no reference tracking, Union, Custom or registered payload.</remarks>
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Auto)]
public ref struct LuminPackEvaluator
{
    private int _value;

    /// <summary>Gets the accumulated byte count.</summary>
    /// <remarks>Generated code passes the evaluator by reference, so every contribution lands in this one instance.</remarks>
    public readonly int Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    /// <summary>Adds a byte count to the accumulated width.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(int value)
    {
        unchecked
        {
            _value += value;
        }
    }

    /// <summary>Determines whether the value type requires managed reference handling.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsReferenceOrContainsReferences<T>()
    {
        return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    }

    /// <summary>Obtains the string length for the supplied input.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetStringLength(scoped ref string? value)
    { return GetStringLength<global::LuminPack.Option.LuminPackDefaultOptions>(ref value); }

    /// <summary>Returns the size of the string header used by the current recording mode.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int StringRecordLength()
    { return StringRecordLength<global::LuminPack.Option.LuminPackDefaultOptions>(); }

    /// <summary>Calculates the serialized size contribution of string utf8.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateStringUtf8(scoped ref string? value)
    {
        return value is null ? 0 : LuminPackMarshal.CalculateStringByteCount(value);
    }

    /// <summary>Calculates the serialized size contribution of string utf16.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <remarks>The writer copies the raw code units, so the payload is two bytes per character; a string holds at most
    /// 0x3FFFFFDF characters, so the width always fits an int.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateStringUtf16(scoped ref string? value)
    {
        return value is null ? 0 : value.Length * sizeof(char);
    }

    /// <summary>Calculates the payload width of a UTF-16 length record.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The exact payload width the writer produces.</returns>
    /// <remarks>Latin-1 text is written one byte per character, other text as raw code units.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateStringUtf16Record(scoped ref string? value)
    {
        return string.IsNullOrEmpty(value) ? 0 : global::LuminPack.Utility.StringSerializer.Utf16RecordPayloadLength(value);
    }

    /// <summary>Obtains the string length for the supplied input.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int GetStringLength<TOptions>(scoped ref string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        bool utf8 = default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8;
        var recording = default(TOptions).StringRecording;
        if (recording == global::LuminPack.Option.LuminPackStringRecording.VarInt)
            return global::LuminPack.Core.LuminPackWriter.VarIntRecordLength<TOptions>(value);
        return (utf8, recording == global::LuminPack.Option.LuminPackStringRecording.Token) switch
        {
            (true, true) => checked(CalculateStringUtf8(ref value) + 1),
            (true, false) => checked(CalculateStringUtf8(ref value) + sizeof(int) * 2),
            (false, true) => checked(CalculateStringUtf16(ref value) + sizeof(ushort)),
            (false, false) => checked(CalculateStringUtf16Record(ref value) + sizeof(int))};
    }

    /// <summary>Returns the size of the string header used by the current recording mode.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int StringRecordLength<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        var recording = default(TOptions).StringRecording;
        if (recording == global::LuminPack.Option.LuminPackStringRecording.VarInt)
            return default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8 ? 10 : 5;
        return (recording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : sizeof(ushort)) : ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? sizeof(int) * 2 : sizeof(int));
    }

    /// <summary>Adds a byte count to the accumulated width.</summary>
    /// <param name="a">The evaluator whose width is extended.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>An evaluator holding the extended width; compound assignment stores it back into the caller's instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminPackEvaluator operator +(LuminPackEvaluator a, int value)
    {
        unchecked
        {
            a._value += value;
            return a;
        }
    }
}
