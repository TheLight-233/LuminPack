#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace LuminPack.Internal;

/// <summary>Provides short ascii transcoder functionality.</summary>
internal static class ShortAsciiTranscoder
{
    // Two overlapping loads/stores cover 4..32 characters without touching bytes
    // outside either span. Check the entire input before writing anything.
    /// <summary>Attempts to encode and reports whether it succeeded.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="escapeJson">The escape json used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryEncode(ReadOnlySpan<char> source, Span<byte> destination, bool escapeJson)
    {
        int length = source.Length;
        if (!Vector128.IsHardwareAccelerated || (uint)(length - 4) > 28 || destination.Length < length)
            return false;

        // Non-ASCII names commonly start with a non-ASCII character. Avoid two
        // vector loads and a reduction before entering their normal transcoder.
        if (source[0] > 0x7F)
            return false;

        if (length > 16)
            return TryEncodeMedium(source, destination, escapeJson);

        ref ushort input = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(source));
        if (length < 8)
        {
            var chars = Unsafe.SizeOf<IntPtr>() == 8
                ? Vector128.Create(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref input)),
                    Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref input, length - 4)))).AsUInt16()
                : Vector128.Create(Vector64.LoadUnsafe(ref input),
                    Vector64.LoadUnsafe(ref input, (nuint)(length - 4)));
            if (Vector128.GreaterThanAny(chars, Vector128.Create((ushort)0x7F)))
                return false;
            var packed = Vector128.Narrow(chars, chars);
            if (escapeJson && NeedsJsonEscape(packed))
                return false;
            ref byte shortOutput = ref MemoryMarshal.GetReference(destination);
            Unsafe.WriteUnaligned(ref shortOutput, packed.AsUInt32().GetElement(0));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref shortOutput, length - 4), packed.AsUInt32().GetElement(1));
            return true;
        }

        var first = Vector128.LoadUnsafe(ref input);
        var last = Vector128.LoadUnsafe(ref input, (nuint)(length - 8));
        if (Vector128.GreaterThanAny(first | last, Vector128.Create((ushort)0x7F)))
            return false;

        var bytes = Vector128.Narrow(first, last);
        if (escapeJson && NeedsJsonEscape(bytes))
            return false;

        ref byte output = ref MemoryMarshal.GetReference(destination);
        if (Unsafe.SizeOf<IntPtr>() == 8)
        {
            Unsafe.WriteUnaligned(ref output, bytes.AsUInt64().GetElement(0));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, length - 8), bytes.AsUInt64().GetElement(1));
        }
        else
        {
            Unsafe.WriteUnaligned(ref output, bytes.GetLower());
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, length - 8), bytes.GetUpper());
        }
        return true;
    }

    /// <summary>Attempts to encode medium and reports whether it succeeded.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="escapeJson">The escape json used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryEncodeMedium(ReadOnlySpan<char> source, Span<byte> destination, bool escapeJson)
    {
        if (!Vector256.IsHardwareAccelerated)
            return false;

        ref ushort input = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(source));
        var first = Vector256.LoadUnsafe(ref input);
        var last = Vector256.LoadUnsafe(ref input, (nuint)(source.Length - 16));
        if (Vector256.GreaterThanAny(first | last, Vector256.Create((ushort)0x7F)))
            return false;
        var bytes = Vector256.Narrow(first, last);
        if (escapeJson)
        {
            var escapes = Vector256.LessThan(bytes, Vector256.Create((byte)0x20)) |
                Vector256.Equals(bytes, Vector256.Create((byte)'"')) |
                Vector256.Equals(bytes, Vector256.Create((byte)'\\'));
            if (!Vector256.EqualsAll(escapes, Vector256<byte>.Zero))
                return false;
        }
        ref byte output = ref MemoryMarshal.GetReference(destination);
        bytes.GetLower().StoreUnsafe(ref output);
        bytes.GetUpper().StoreUnsafe(ref output, (nuint)(source.Length - 16));
        return true;
    }

    // Callers supply a bounded payload. Reject non-ASCII before allocating or
    // publishing a string, so their original validating/replacing decoder can run.
    /// <summary>Attempts to decode medium and reports whether it succeeded.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string? TryDecodeMedium(ref byte source, int length)
    {
        if (!Vector128.IsHardwareAccelerated || (uint)(length - 17) > 15 || source >= 0x80)
            return null;

        var first = Vector128.LoadUnsafe(ref source);
        var last = Vector128.LoadUnsafe(ref source, (nuint)(length - 16));
        if ((first | last).ExtractMostSignificantBits() != 0)
            return null;

        string result = new string('\0', length);
        ref ushort output = ref Unsafe.As<char, ushort>(ref Unsafe.AsRef(in result.GetPinnableReference()));
        if (Vector256.IsHardwareAccelerated)
        {
            var bytes = Vector256.Create(first, last);
            Vector256.WidenLower(bytes).StoreUnsafe(ref output);
            Vector256.WidenUpper(bytes).StoreUnsafe(ref output, (nuint)(length - 16));
        }
        else
        {
            Vector128.WidenLower(first).StoreUnsafe(ref output);
            Vector128.WidenUpper(first).StoreUnsafe(ref output, 8);
            Vector128.WidenLower(last).StoreUnsafe(ref output, (nuint)(length - 16));
            Vector128.WidenUpper(last).StoreUnsafe(ref output, (nuint)(length - 8));
        }
        return result;
    }

    /// <summary>Performs the needs json escape step for the current operation.</summary>
    /// <param name="bytes">The bytes used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool NeedsJsonEscape(Vector128<byte> bytes)
    {
        var escapes = Vector128.LessThan(bytes, Vector128.Create((byte)0x20)) |
            Vector128.Equals(bytes, Vector128.Create((byte)'"')) |
            Vector128.Equals(bytes, Vector128.Create((byte)'\\'));
        return !Vector128.EqualsAll(escapes, Vector128<byte>.Zero);
    }
}
#endif
