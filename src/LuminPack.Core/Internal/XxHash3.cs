using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Internal;

/// <summary>Computes standard seed-zero XXH3 hashes for JSON property-name dispatch.</summary>
/// <remarks>Hashes select a candidate group; generated readers still compare the complete name.</remarks>
public static class XxHash3
{
    /// <summary>Hashes an already validated byte range using a GC-tracked reference.</summary>
    /// <param name="source">The first readable byte; a null reference is valid only when byteLength is zero.</param>
    /// <param name="byteLength">The complete readable byte count. The caller must validate the range before calling.</param>
    /// <returns>The standard XXH3 64-bit hash with seed zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Hash64(ref byte source, uint byteLength) => XxHash3Algorithm.Compute(ref source, byteLength);

    /// <summary>Hashes the exact encoded UTF-8 bytes without allocating.</summary>
    /// <param name="data">The complete encoded name.</param>
    /// <returns>The standard XXH3 64-bit hash with seed zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Hash64Utf8(ReadOnlySpan<byte> data) => Hash64(ref MemoryMarshal.GetReference(data), (uint)data.Length);

    /// <summary>Hashes the UTF-16 code units as their in-memory bytes without transcoding.</summary>
    /// <param name="chars">The complete UTF-16 name.</param>
    /// <returns>The standard seed-zero XXH3 hash of the name's bytes, distinct from its UTF-8 hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Hash64Utf16(ReadOnlySpan<char> chars) =>
        Hash64(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(chars)), (uint)chars.Length * sizeof(char));
}
