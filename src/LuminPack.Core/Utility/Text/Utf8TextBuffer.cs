using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace LuminPack.Utility;

/// <summary>Owns bounded caller scratch or a returned character lease for one complete UTF-8 token.</summary>
internal readonly ref struct Utf8TextBuffer
{
    private readonly char[]? _rented;
    private readonly Span<char> _buffer;

    /// <summary>Acquires enough character storage for the maximum decoded size of the token.</summary>
    /// <param name="byteLength">The validated UTF-8 byte count.</param>
    /// <param name="scratch">The caller's bounded temporary storage.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Utf8TextBuffer(int byteLength, Span<char> scratch)
    {
        _rented = byteLength <= scratch.Length ? null : ArrayPool<char>.Shared.Rent(byteLength);
        _buffer = _rented is null ? scratch : _rented;
    }

    /// <summary>Decodes complete text with the same replacement fallback as the ordinary UTF-8 string reader.</summary>
    /// <param name="source">The complete token without JSON quotes or escapes.</param>
    /// <returns>The initialized decoded characters, valid until this lease is disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<char> Decode(ReadOnlySpan<byte> source) => _buffer.Slice(0, Utf8PlatformDecoder.Decode(source, _buffer));

    /// <summary>Returns pooled storage after the caller has finished consuming the characters.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_rented is not null) ArrayPool<char>.Shared.Return(_rented);
    }
}
