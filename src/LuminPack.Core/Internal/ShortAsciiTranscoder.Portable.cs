#if NETSTANDARD2_1
using System;
using LuminPack.Core;

namespace LuminPack.Internal;

/// <summary>Exposes optional ASCII acceleration while older runtimes retain their established fallback.</summary>
internal static class ShortAsciiTranscoder
{
    /// <summary>Gets whether an optional native encoder is available.</summary>
    internal static bool IsSupported => LuminPackBurst.HasAsciiEncode;

    /// <summary>Attempts complete encoding without changing the destination on a miss.</summary>
    /// <param name="source">The complete character range.</param>
    /// <param name="destination">The complete writable byte range.</param>
    /// <param name="escapeJson">Whether JSON escapes must decline the operation.</param>
    /// <returns>Whether every character was encoded.</returns>
    internal static bool TryEncode(ReadOnlySpan<char> source, Span<byte> destination, bool escapeJson)
        => LuminPackBurst.TryAsciiEncode(source, destination, escapeJson);
}
#endif
