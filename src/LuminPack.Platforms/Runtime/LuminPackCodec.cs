using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Platforms;

/// <summary>Writes one explicit platform value through the existing Core writer.</summary>
/// <typeparam name="T">The platform value type.</typeparam>
/// <param name="writer">The caller-owned output cursor.</param><param name="value">The value to encode.</param>
public delegate void LuminPackWrite<T>(ref LuminPackWriter writer, in T value);

/// <summary>Reads one explicit platform value through the existing Core reader.</summary>
/// <typeparam name="T">The platform value type.</typeparam>
/// <param name="reader">The bounded input cursor.</param><param name="value">The destination.</param>
public delegate void LuminPackRead<T>(ref LuminPackReader reader, ref T value);

/// <summary>Accumulates the exact Core binary representation size.</summary>
/// <typeparam name="T">The platform value type.</typeparam>
/// <param name="evaluator">The caller-owned size accumulator.</param><param name="value">The value to measure.</param>
public delegate void LuminPackSize<T>(ref LuminPackEvaluator evaluator, in T value);

/// <summary>Groups an explicit platform codec with bounded, complete-record convenience entries.</summary>
/// <typeparam name="T">The platform value type.</typeparam>
/// <remarks>This adapter invokes Core cursors. It does not replace generated dispatch or reinterpret native layout.</remarks>
public sealed class LuminPackCodec<T>
{
    private readonly LuminPackWrite<T> _write;
    private readonly LuminPackRead<T> _read;
    private readonly LuminPackSize<T> _size;

    /// <summary>Creates a codec from static, type-specific operations.</summary>
    /// <param name="write">The complete writer.</param><param name="read">The complete reader.</param><param name="size">The exact size operation.</param>
    public LuminPackCodec(LuminPackWrite<T> write, LuminPackRead<T> read, LuminPackSize<T> size)
    {
        _write = write ?? LuminPackExceptionHelper.ThrowArgumentNullException<LuminPackWrite<T>>(nameof(write));
        _read = read ?? LuminPackExceptionHelper.ThrowArgumentNullException<LuminPackRead<T>>(nameof(read));
        _size = size ?? LuminPackExceptionHelper.ThrowArgumentNullException<LuminPackSize<T>>(nameof(size));
    }

    /// <summary>Writes one value into a caller-owned Core operation.</summary>
    /// <param name="writer">The output cursor.</param><param name="value">The value.</param>
    public void Write(ref LuminPackWriter writer, in T value) => _write(ref writer, value);

    /// <summary>Reads one value from a caller-owned Core operation.</summary>
    /// <param name="reader">The bounded input cursor.</param><param name="value">The destination.</param>
    public void Read(ref LuminPackReader reader, ref T value) => _read(ref reader, ref value);

    /// <summary>Adds the exact binary size to a caller-owned evaluator.</summary>
    /// <param name="evaluator">The size accumulator.</param><param name="value">The value.</param>
    public void Size(ref LuminPackEvaluator evaluator, in T value) => _size(ref evaluator, value);

    /// <summary>Evaluates a value without materializing serialized output.</summary>
    /// <param name="value">The value.</param><returns>The complete record size.</returns>
    public int GetSize(in T value)
    {
        int bytes = 0;
        var evaluator = new LuminPackEvaluator(ref bytes);
        try { _size(ref evaluator, value); }
        finally { evaluator.Dispose(); }
        if (bytes < 0) LuminPackExceptionHelper.ThrowInvalidDataException("Platform record size overflowed.");
        return bytes;
    }

    /// <summary>Serializes an explicit platform record into exactly sized output.</summary>
    /// <param name="value">The value.</param><returns>The complete record bytes.</returns>
    public byte[] Serialize(in T value)
    {
        byte[] bytes = new byte[GetSize(value)];
        Span<byte> output = bytes;
        var writer = new LuminPackWriter(ref output);
        _write(ref writer, value);
        if (writer.Position != bytes.Length) LuminPackExceptionHelper.ThrowInvalidDataException("Platform codec size and writer disagree.");
        return bytes;
    }

    /// <summary>Reads exactly one record and rejects trailing input before publishing the destination.</summary>
    /// <param name="source">The complete record.</param><param name="value">The destination assigned only on complete success.</param>
    public void Deserialize(ReadOnlySpan<byte> source, ref T value)
    {
        var reader = new LuminPackReader(ref source);
        T candidate = value;
        _read(ref reader, ref candidate);
        if (reader.Position != source.Length) LuminPackExceptionHelper.ThrowInvalidDataException("Trailing bytes follow the platform record.");
        value = candidate;
    }

    /// <summary>Creates a value from exactly one complete record.</summary>
    /// <param name="source">The complete record.</param><returns>The decoded value.</returns>
    public T Deserialize(ReadOnlySpan<byte> source)
    {
        T value = default!;
        Deserialize(source, ref value);
        return value;
    }
}
