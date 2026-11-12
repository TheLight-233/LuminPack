using System;
using System.Buffers;
using LuminPack.SourceGenerator.Next.Diagnostics;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Builds transient generated text in an exclusively owned, bounded array-pool lease.</summary>
/// <remarks>Published strings own their storage. No pooled memory is retained by a model, source or incremental cache.</remarks>
internal sealed class PooledTextBuffer : IDisposable
{
    private const int MaximumPooledCapacity = 262144;
    private char[] _buffer;
    private int _length;

    /// <summary>Rents initial character storage.</summary>
    /// <param name="capacity">The expected character count.</param>
    internal PooledTextBuffer(int capacity = 2048) => _buffer = Allocate(Math.Max(capacity, 16));

    /// <summary>Gets the written character count or truncates the current text.</summary>
    internal int Length
    {
        get => _length;
        set
        {
            if ((uint)value > (uint)_length)
                LuminPackExceptionHelper.ThrowArgumentOutOfRange(nameof(value));
            _length = value;
        }
    }

    /// <summary>Reads one written character.</summary>
    /// <param name="index">The character offset.</param>
    /// <returns>The selected character.</returns>
    internal char this[int index] => _buffer[index];

    /// <summary>Appends a complete string.</summary>
    /// <param name="text">The text to copy.</param>
    /// <returns>This buffer for chained appends.</returns>
    internal PooledTextBuffer Append(string text) => Append(text, 0, text.Length);

    /// <summary>Copies a source range directly into the current lease.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The source offset.</param>
    /// <param name="length">The character count.</param>
    /// <returns>This buffer for chained appends.</returns>
    internal PooledTextBuffer Append(string text, int start, int length)
    {
        EnsureCapacity(length);
        text.CopyTo(start, _buffer, _length, length);
        _length += length;
        return this;
    }

    /// <summary>Appends one character.</summary>
    /// <param name="value">The character.</param>
    /// <returns>This buffer for chained appends.</returns>
    internal PooledTextBuffer Append(char value)
    {
        EnsureCapacity(1);
        _buffer[_length++] = value;
        return this;
    }

    /// <summary>Fills an indentation or separator range without a temporary string.</summary>
    /// <param name="value">The repeated character.</param>
    /// <param name="count">The number of repetitions.</param>
    /// <returns>This buffer for chained appends.</returns>
    internal PooledTextBuffer Append(char value, int count)
    {
        EnsureCapacity(count);
        _buffer.AsSpan(_length, count).Fill(value);
        _length += count;
        return this;
    }

    /// <summary>Inserts a completed header without first allocating a header string.</summary>
    /// <param name="prefix">The exclusively owned header buffer.</param>
    internal void Prepend(PooledTextBuffer prefix)
    {
        EnsureCapacity(prefix._length);
        _buffer.AsSpan(0, _length).CopyTo(_buffer.AsSpan(prefix._length));
        prefix._buffer.AsSpan(0, prefix._length).CopyTo(_buffer);
        _length += prefix._length;
    }

    /// <summary>Creates the immutable text that can safely outlive the lease.</summary>
    /// <returns>A string owning the written characters.</returns>
    public override string ToString() => new(_buffer, 0, _length);

    /// <summary>Returns a small lease once; oversized arrays are left for collection.</summary>
    public void Dispose()
    {
        char[] buffer = _buffer;
        _buffer = Array.Empty<char>();
        _length = 0;
        Release(buffer);
    }

    /// <summary>Reserves the complete appended range before copying or filling.</summary>
    /// <param name="additional">The number of characters to append.</param>
    private void EnsureCapacity(int additional)
    {
        int required = checked(_length + additional);
        if (required <= _buffer.Length)
            return;
        char[] buffer = Allocate(Math.Max(required, checked(_buffer.Length * 2)));
        _buffer.AsSpan(0, _length).CopyTo(buffer);
        Release(_buffer);
        _buffer = buffer;
    }

    /// <summary>Rents only bounded buffers so a large generated file cannot stay in the pool.</summary>
    /// <param name="capacity">The required capacity.</param>
    /// <returns>A character array owned by one buffer.</returns>
    private static char[] Allocate(int capacity) => capacity <= MaximumPooledCapacity
        ? ArrayPool<char>.Shared.Rent(capacity) : new char[capacity];

    /// <summary>Returns only arrays originating from the bounded pool path.</summary>
    /// <param name="buffer">The detached lease.</param>
    private static void Release(char[] buffer)
    {
        if (buffer.Length != 0 && buffer.Length <= MaximumPooledCapacity)
            ArrayPool<char>.Shared.Return(buffer);
    }
}
