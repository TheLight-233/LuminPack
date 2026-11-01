using System;
using System.Buffers;

namespace LuminPackUnitTest;

/// <summary>Reads a binary record into a destination whose static type selects the generated concrete operation.</summary>
/// <typeparam name="T">The destination type.</typeparam>
/// <param name="bytes">The record.</param>
/// <param name="value">The destination.</param>
public delegate void ReadInto<T>(ReadOnlySpan<byte> bytes, ref T value);

/// <summary>Reads a JSON document through a concrete string overload.</summary>
/// <typeparam name="T">The destination type.</typeparam>
/// <param name="json">The document.</param>
/// <param name="value">The destination.</param>
public delegate void ReadJsonInto<T>(string json, ref T value);

/// <summary>Reads encoded JSON through a concrete byte-span overload.</summary>
/// <typeparam name="T">The destination type.</typeparam>
/// <param name="json">The encoded document.</param>
/// <param name="value">The destination.</param>
public delegate void ReadJsonBytesInto<T>(ReadOnlySpan<byte> json, ref T value);

/// <summary>Reads a segmented binary record through a concrete sequence overload.</summary>
/// <typeparam name="T">The destination type.</typeparam>
/// <param name="bytes">The segmented record.</param>
/// <param name="value">The destination.</param>
public delegate void ReadSequenceInto<T>(ReadOnlySequence<byte> bytes, ref T value);

/// <summary>Reads a caller-owned buffer without discarding its operation state.</summary>
/// <typeparam name="T">The destination type.</typeparam>
/// <param name="buffer">The buffer and its read state.</param>
/// <param name="value">The destination.</param>
public delegate void ReadBufferInto<T>(LuminPack.Utility.LuminBufferWriter buffer, ref T value);

/// <summary>Reads JSON through a concrete character-span overload.</summary>
/// <typeparam name="T">The destination type.</typeparam>
/// <param name="json">The document characters.</param>
/// <param name="value">The destination.</param>
public delegate void ReadJsonCharsInto<T>(ReadOnlySpan<char> json, ref T value);

/// <summary>Provides value-returning reads for tests without the registration-only generic fallback.</summary>
public static class ConcreteReads
{
    /// <summary>Reads one record through the concrete operation named by the caller's lambda.</summary>
    /// <typeparam name="T">The destination type.</typeparam>
    /// <param name="bytes">The record.</param>
    /// <param name="read">A lambda such as <c>(b, ref v) =&gt; LuminPackSerializer.Deserialize(b, ref v)</c>, which binds at compile time.</param>
    /// <returns>The decoded value.</returns>
    /// <remarks>A generic <c>Deserialize&lt;T&gt;</c> call needs a registered formatter; the lambda is compiled against the concrete type instead.</remarks>
    public static T Read<T>(ReadOnlySpan<byte> bytes, ReadInto<T> read)
    {
        T value = default!;
        read(bytes, ref value);
        return value;
    }

    /// <summary>Reads JSON through the concrete operation selected by the caller.</summary>
    /// <typeparam name="T">The destination type.</typeparam>
    /// <param name="json">The document.</param>
    /// <param name="read">The concrete reader.</param>
    /// <returns>The decoded value.</returns>
    public static T ReadJson<T>(string json, ReadJsonInto<T> read)
    {
        T value = default!;
        read(json, ref value);
        return value;
    }

    /// <summary>Reads encoded JSON through the concrete operation selected by the caller.</summary>
    /// <typeparam name="T">The destination type.</typeparam>
    /// <param name="json">The encoded document.</param>
    /// <param name="read">The concrete reader.</param>
    /// <returns>The decoded value.</returns>
    public static T ReadJson<T>(ReadOnlySpan<byte> json, ReadJsonBytesInto<T> read)
    {
        T value = default!;
        read(json, ref value);
        return value;
    }

    /// <summary>Reads a segmented binary record through the concrete operation selected by the caller.</summary>
    /// <typeparam name="T">The destination type.</typeparam>
    /// <param name="bytes">The segmented record.</param>
    /// <param name="read">The concrete reader.</param>
    /// <returns>The decoded value.</returns>
    public static T Read<T>(ReadOnlySequence<byte> bytes, ReadSequenceInto<T> read)
    {
        T value = default!;
        read(bytes, ref value);
        return value;
    }

    /// <summary>Reads the caller's buffer through its concrete binary operation.</summary>
    /// <typeparam name="T">The destination type.</typeparam>
    /// <param name="buffer">The buffer and its read state.</param>
    /// <param name="read">The concrete reader.</param>
    /// <returns>The decoded value.</returns>
    public static T Read<T>(LuminPack.Utility.LuminBufferWriter buffer, ReadBufferInto<T> read)
    {
        T value = default!;
        read(buffer, ref value);
        return value;
    }

    /// <summary>Reads the caller's buffer through its concrete JSON operation.</summary>
    /// <typeparam name="T">The destination type.</typeparam>
    /// <param name="buffer">The buffer and its read state.</param>
    /// <param name="read">The concrete reader.</param>
    /// <returns>The decoded value.</returns>
    public static T ReadJson<T>(LuminPack.Utility.LuminBufferWriter buffer, ReadBufferInto<T> read)
    {
        T value = default!;
        read(buffer, ref value);
        return value;
    }

    /// <summary>Reads JSON characters through the caller's concrete operation.</summary>
    /// <typeparam name="T">The destination type.</typeparam>
    /// <param name="json">The document characters.</param>
    /// <param name="read">The concrete reader.</param>
    /// <returns>The decoded value.</returns>
    public static T ReadJson<T>(ReadOnlySpan<char> json, ReadJsonCharsInto<T> read)
    {
        T value = default!;
        read(json, ref value);
        return value;
    }
}
