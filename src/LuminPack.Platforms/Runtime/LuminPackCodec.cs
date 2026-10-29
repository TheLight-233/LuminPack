using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Utility;

namespace LuminPack.Platforms;

/// <summary>Writes one explicit platform value through the existing Core writer.</summary>
/// <typeparam name="T">The platform value type.</typeparam>
/// <param name="writer">The caller-owned output cursor.</param><param name="value">The value to encode.</param>
public delegate void LuminPackWrite<T>(ref LuminPackWriter writer, in T value);

/// <summary>Reads one explicit platform value through the existing Core reader.</summary>
/// <typeparam name="T">The platform value type.</typeparam>
/// <param name="reader">The bounded input cursor.</param><param name="value">The destination.</param>
public delegate void LuminPackRead<T>(ref LuminPackReader reader, ref T value);

/// <summary>Groups an explicit platform codec with bounded, complete-record convenience entries.</summary>
/// <typeparam name="T">The platform value type.</typeparam>
/// <remarks>This adapter invokes Core cursors. It does not replace generated dispatch or reinterpret native layout.</remarks>
public sealed class LuminPackCodec<T>
{
    private readonly LuminPackWrite<T> _write;
    private readonly LuminPackRead<T> _read;

    /// <summary>Creates a codec from static, type-specific operations.</summary>
    /// <param name="write">The complete writer.</param><param name="read">The complete reader.</param>
    public LuminPackCodec(LuminPackWrite<T> write, LuminPackRead<T> read)
    {
        _write = write ?? LuminPackExceptionHelper.ThrowArgumentNullException<LuminPackWrite<T>>(nameof(write));
        _read = read ?? LuminPackExceptionHelper.ThrowArgumentNullException<LuminPackRead<T>>(nameof(read));
    }

    /// <summary>Writes one value into a caller-owned Core operation.</summary>
    /// <param name="writer">The output cursor.</param><param name="value">The value.</param>
    public void Write(ref LuminPackWriter writer, in T value) => _write(ref writer, value);

    /// <summary>Reads one value from a caller-owned Core operation.</summary>
    /// <param name="reader">The bounded input cursor.</param><param name="value">The destination.</param>
    public void Read(ref LuminPackReader reader, ref T value) => _read(ref reader, ref value);

    /// <summary>Serializes an explicit platform record into a new array holding exactly the written bytes.</summary>
    /// <param name="value">The value.</param><returns>The complete record bytes.</returns>
    public byte[] Serialize(in T value)
    {
        var buffer = LuminBufferWriterPool.Rent();
        try
        {
            var writer = new LuminPackWriter(buffer);
            _write(ref writer, value);
            byte[] bytes = new byte[writer.CurrentIndex];
            writer.GetSpan().CopyTo(bytes);
            return bytes;
        }
        finally
        {
            LuminBufferWriterPool.Return(buffer);
        }
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
