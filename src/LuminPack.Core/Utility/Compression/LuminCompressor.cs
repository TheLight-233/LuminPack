// LuminZ uses a per-thread, uncleared table of 16-bit positions and a 64 KiB window.
// A modular distance is validated before reading a stale candidate. Matching keeps
// skip acceleration, backward extension and one-step lazy evaluation.
//
// Frame: "LUM2", original length (Int32 LE), then sequences.
// Each sequence has a token (literal length in the high nibble, match length minus
// four in the low nibble), optional literal-length extension, literals, a UInt16 LE
// distance and an optional match-length extension. A saturated nibble adds a
// byte extension (0..254), or 255 followed by a canonical base-128 remainder.
// The last sequence has literals only.
// Empty input has only the eight-byte header. Earlier LUMZ frames are rejected.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;

#if NET6_0_OR_GREATER
using System.Numerics;
#endif

#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace LuminPack.Utility;

/// <summary>
/// LuminZ: a high-performance in-memory compression algorithm for LuminPack binary payloads.
/// </summary>
public static class LuminCompressor
{
    private const uint Magic = 0x324D554C; // "LUM2" LE
    private const int HeaderSize = 8;
    private const int OffsetBytes = 2;           // 16-bit offset → 64 KB window
    private const int MaxOffset = 0xFFFF;
    private const int MinMatch = 4;
    private const int LastLiterals = 5;
    private const int MatchGuard = 12;

    // Knuth multiplicative hash — same constant as LZ4 / Snappy
    private const uint HashMul4 = 2654435761u;

    /// <summary>Performs the h4 step for the current operation.</summary>
    /// <param name="v">The v used by this operation.</param>
    /// <param name="shift">The shift used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int H4(uint v, int shift) => (int)((v * HashMul4) >> shift);

    // ──────────────────────────────────────────────────────────────
    //  Per-thread hash table — reused across calls, never re-zeroed
    // ──────────────────────────────────────────────────────────────
    //  Entries store the low 16 bits of source positions (0-based).
    //  A candidate is reconstructed relative to the current position, so
    //  inputs larger than 64 KiB keep using the immediately preceding window.
    //
    //  Stale entries from previous calls are safe because:
    //    (a) modular distance + current-position checks — reject any
    //        candidate that is not a valid prior position within the window
    //    (b) ReadU32(candidate) == v4                 — 4-byte confirm
    //  Both are checked (in that order — dist BEFORE ReadU32) so no
    //  out-of-bounds read ever occurs.
    [ThreadStatic]
    private static ushort[]? _ht;

    /// <summary>Maximum compressed-output size for a given uncompressed length.</summary>
    /// <param name="sourceLength">The source length used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMaxCompressedSize(int sourceLength)
    {
        if (sourceLength == 0)
            return HeaderSize;
        // A two-byte literal extension needs at least 270 literals. Longer
        // extensions grow logarithmically; matches pay for their own framing.
        // One extra byte per 128 input bytes plus terminal headroom bounds both.
        const int MaxSourceLength = 2_130_836_464;
        if ((uint)sourceLength > MaxSourceLength)
            ThrowSourceTooLarge();
        return HeaderSize + sourceLength + (sourceLength >> 7) + 16;
    }

    /// <summary>Original uncompressed size stored in a LuminZ header.</summary>
    /// <param name="compressed">The compressed used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int GetDecompressedSize(ReadOnlySpan<byte> compressed)
    {
        if (compressed.Length < HeaderSize)
            ThrowInvalidData();
        ref byte p = ref MemoryMarshal.GetReference(compressed);
        if (Unsafe.ReadUnaligned<uint>(ref p) != Magic)
            ThrowInvalidData();
        int len = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref p, (nint)(uint)4));
        if (len < 0)
            ThrowInvalidData();
        ValidateDeclaredOutputSize(compressed, len);
        return len;
    }

    /// <summary>Compresses the input using the LuminPack compression format.</summary>
    /// <param name="source">The input data to process.</param>
    /// <returns>The result of the operation.</returns>
    public static byte[] Compress(ReadOnlySpan<byte> source)
    {
        int maxLen = GetMaxCompressedSize(source.Length);
        byte[] tmp = ArrayPool<byte>.Shared.Rent(maxLen);
        try
        {
            int written = Compress(source, tmp.AsSpan(0, maxLen));
#if NET6_0_OR_GREATER
            var arr = GC.AllocateUninitializedArray<byte>(written);
            LuminPackMarshal.CopyBlockUnaligned(ref MemoryMarshal.GetReference(arr.AsSpan()), ref MemoryMarshal.GetReference(tmp.AsSpan()), (uint)written);
            return arr;
#else
            var result = new byte[written];
            LuminPackMarshal.CopyBlockUnaligned(ref MemoryMarshal.GetReference(result.AsSpan()), ref MemoryMarshal.GetReference(tmp.AsSpan()), (uint)written);
            return result;
#endif
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tmp);
        }
    }

    /// <summary>Restores the uncompressed bytes from a LuminPack compression frame.</summary>
    /// <param name="source">The input data to process.</param>
    /// <returns>The result of the operation.</returns>
    public static byte[] Decompress(ReadOnlySpan<byte> source)
    {
        int origLen = GetDecompressedSize(source);
        if (origLen == 0)
            return Array.Empty<byte>();
#if NET6_0_OR_GREATER
        var result = GC.AllocateUninitializedArray<byte>(origLen);
#else
        var result = new byte[origLen];
#endif
        Decompress(source, result);
        return result;
    }

    /// <summary>Compresses the input using the LuminPack compression format.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="destination">The destination receiving the result.</param>
    /// <returns>The result of the operation.</returns>
    public static int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (destination.Length < GetMaxCompressedSize(source.Length))
            ThrowInsufficientBuffer();

        if (source.IsEmpty)
        {
            ref byte d = ref MemoryMarshal.GetReference(destination);
            Unsafe.WriteUnaligned(ref d, Magic);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref d, (nint)(uint)4), 0);
            return HeaderSize;
        }

        ref byte src = ref MemoryMarshal.GetReference(source);
        ref byte dst = ref MemoryMarshal.GetReference(destination);
        Unsafe.WriteUnaligned(ref dst, Magic);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, (nint)(uint)4), source.Length);
        int bodyLen = CompressCore(ref src, source.Length, ref Unsafe.Add(ref dst, (nint)(uint)HeaderSize));
        return HeaderSize + bodyLen;
    }

    /// <summary>Restores the uncompressed bytes from a LuminPack compression frame.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="destination">The destination receiving the result.</param>
    /// <returns>The result of the operation.</returns>
    public static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length < HeaderSize)
            ThrowInvalidData();
        ref byte src = ref MemoryMarshal.GetReference(source);
        ref byte dst = ref MemoryMarshal.GetReference(destination);
        if (Unsafe.ReadUnaligned<uint>(ref src) != Magic)
            ThrowInvalidData();
        int origLen = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref src, (nint)(uint)4));
        if (origLen < 0)
            ThrowInvalidData();
        if (origLen == 0)
        {
            ValidateEmptyFrame(source.Length);
            return 0;
        }
        if (destination.Length < origLen)
            ThrowInsufficientBuffer();
        return DecompressCore(ref Unsafe.Add(ref src, (nint)(uint)HeaderSize), source.Length - HeaderSize, ref dst, origLen);
    }

    /// <summary>Compresses the input using the LuminPack compression format.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="destination">The destination receiving the result.</param>
    /// <returns>The result of the operation.</returns>
    public static int Compress(LuminBufferWriter source, LuminBufferWriter destination)
    {
        ReadOnlySpan<byte> srcSpan = source.GetSpan();
        int needed = GetMaxCompressedSize(srcSpan.Length);

        // EnsureCapacity and the output write both invalidate/corrupt srcSpan
        // when source and destination are the same unmanaged buffer.
        if (ReferenceEquals(source, destination))
            return CompressAliased(source, srcSpan, needed);

        destination.EnsureCapacity(needed);
        int written = Compress(srcSpan, destination.GetFullSpan());
        destination.SetWrittenCount(written);
        return written;
    }

    /// <summary>Restores the uncompressed bytes from a LuminPack compression frame.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="destination">The destination receiving the result.</param>
    /// <returns>The result of the operation.</returns>
    public static int Decompress(LuminBufferWriter source, LuminBufferWriter destination)
    {
        ReadOnlySpan<byte> srcSpan = source.GetSpan();
        int origLen = GetDecompressedSize(srcSpan);
        if (origLen == 0)
        {
            destination.SetWrittenCount(0);
            return 0;
        }

        if (ReferenceEquals(source, destination))
            return DecompressAliased(source, srcSpan, origLen);

        destination.EnsureCapacity(origLen);
        int written = Decompress(srcSpan, destination.GetFullSpan());
        destination.SetWrittenCount(written);
        return written;
    }

    /// <summary>Compresses the input using the LuminPack compression format.</summary>
    /// <param name="buffer">The buffer used by this operation.</param>
    /// <param name="source">The input data to process.</param>
    /// <param name="maxLength">The max length used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CompressAliased(
        LuminBufferWriter buffer,
        ReadOnlySpan<byte> source,
        int maxLength)
    {
        byte[] temp = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = Compress(source, temp.AsSpan(0, maxLength));
            buffer.EnsureCapacity(written);
            temp.AsSpan(0, written).CopyTo(buffer.GetFullSpan());
            buffer.SetWrittenCount(written);
            return written;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(temp);
        }
    }

    /// <summary>Restores the uncompressed bytes from a LuminPack compression frame.</summary>
    /// <param name="buffer">The buffer used by this operation.</param>
    /// <param name="source">The input data to process.</param>
    /// <param name="decompressedLength">The decompressed length used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int DecompressAliased(
        LuminBufferWriter buffer,
        ReadOnlySpan<byte> source,
        int decompressedLength)
    {
        byte[] temp = ArrayPool<byte>.Shared.Rent(decompressedLength);
        try
        {
            int written = Decompress(source, temp.AsSpan(0, decompressedLength));
            buffer.EnsureCapacity(written);
            temp.AsSpan(0, written).CopyTo(buffer.GetFullSpan());
            buffer.SetWrittenCount(written);
            return written;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(temp);
        }
    }

    /// <summary>Compresses the input using the LuminPack compression format.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="writer">The writer receiving the output data.</param>
    public static void CompressAndWriteTo(ReadOnlySpan<byte> source, ref LuminPackWriter writer)
    {
        if (source.IsEmpty)
            return;
        int maxLen = GetMaxCompressedSize(source.Length);
        byte[] rent = ArrayPool<byte>.Shared.Rent(maxLen);
        try
        {
            int compLen = Compress(source, rent.AsSpan(0, maxLen));
            writer.EnsureAdditionalCapacity(compLen);
            ref byte p = ref LuminPackMarshal.GetArrayDataReference(rent);
            LuminPackMarshal.CopyBlockUnaligned(ref writer.CurrentReference, ref p, (uint)compLen);
            writer.Advance(compLen);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
    }

    /// <summary>Compresses the input using the LuminPack compression format.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="srcLen">The src len used by this operation.</param>
    /// <param name="dst">The destination receiving the result.</param>
    /// <returns>The result of the operation.</returns>
    private static int CompressCore(ref byte src, int srcLen, ref byte dst)
    {
        // ── Compute optimal table size ────────────────────────────
        // tableBits = ceil(log2(srcLen)), clamped [8, 15].
        // srcLen=2143 → tableBits=12 → 4096 entries × 2B = 8 KB (L1 hot).
#if NET6_0_OR_GREATER
        int tableBits = 32 - BitOperations.LeadingZeroCount((uint)((srcLen - 1) | 1));
#else
        int tableBits = 8;
        for (int _v = (srcLen - 1) | 1; _v >= (1 << tableBits); tableBits++) { }
#endif
        if (tableBits < 8)
            tableBits = 8;
        if (tableBits > 15)
            tableBits = 15;
        int tableSize = 1 << tableBits;
        int hashShift = 32 - tableBits;

        // ── Reuse or allocate the per-thread table ────────────────
        // CLR zero-inits on first creation → 0 = empty sentinel (ONE-TIME cost).
        // All subsequent calls: no init, immediate reuse.
        // Stale entries are handled correctly by the inner loop's dist + confirm checks.
        var htArr = _ht;
        if (htArr == null || htArr.Length < tableSize)
            _ht = htArr = new ushort[tableSize];
        // ⚠ NO zero-init here — see class-level comment for safety proof.

        ref ushort ht = ref Unsafe.As<byte, ushort>(ref LuminPackMarshal.GetArrayDataReference(htArr));
        return CompressInner(ref src, srcLen, ref dst, ref ht, hashShift);
    }

    /// <summary>Compresses the input using the LuminPack compression format.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="srcLen">The src len used by this operation.</param>
    /// <param name="dst">The destination receiving the result.</param>
    /// <param name="ht">The ht used by this operation.</param>
    /// <param name="hashShift">The hash shift used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static int CompressInner(ref byte src, int srcLen, ref byte dst, ref ushort ht, int hashShift)
    {
        ref byte ip = ref src;
        ref byte srcEnd = ref Unsafe.Add(ref src, unchecked((nint)(uint)(srcLen)));
        ref byte op = ref dst;
        ref byte anchor = ref src;
        ref byte matchLimit = ref Unsafe.Subtract(ref srcEnd, (nint)(uint)MatchGuard);
        ref byte extLimit = ref Unsafe.Subtract(ref srcEnd, (nint)(uint)LastLiterals);

        if (srcLen < MinMatch + LastLiterals + MatchGuard)
            goto EMIT_LAST;

        // Seed position 0; start scanning from position 1.
        Unsafe.Add(ref ht, unchecked((nint)(uint)(H4(ReadU32(ref ip), hashShift)))) = 0;
        ip = ref Unsafe.Add(ref ip, (nint)(uint)1);

        // ── Skip counter — Snappy-style exponential backoff ───────
        //  step = skip >> 5
        //  skip=32 → step=1  (every byte — default for compressible data)
        //  Each miss: skip++; step grows slowly.
        //  Each match: skip=32 (reset to step=1).
        uint skip = 32;

        while (true)
        {
            // candidate must be initialised before the inner for-loop because
            // C# ref locals require definite assignment; src is a safe placeholder.
            ref byte candidate = ref src;
            uint v4;

            // ── Inner scan: find 4-byte match with skip acceleration ──
            for (; ; )
            {
                v4 = ReadU32(ref ip);
                int currentPosition = (int)Unsafe.ByteOffset(ref src, ref ip);
                int h = H4(v4, hashShift);
                ushort storedPosition = Unsafe.Add(ref ht, unchecked((nint)(uint)(h))); // read OLD candidate position
                Unsafe.Add(ref ht, unchecked((nint)(uint)(h))) = unchecked((ushort)currentPosition); // write CURRENT (read-before-write)

                // Reconstruct the nearest preceding position with the stored
                // low 16 bits. This is the key to retaining matches beyond
                // 64/128 KiB instead of treating wrapped positions as offsets
                // into the first 64 KiB of the input.
                int distance = unchecked((ushort)(currentPosition - storedPosition));
                if ((uint)(distance - 1) < (uint)MaxOffset
                    && (uint)distance <= (uint)currentPosition)
                {
                    candidate = ref Unsafe.Subtract(ref ip, (nint)(uint)distance);
                    if (ReadU32(ref candidate) == v4)
                        break;
                }

                ip = ref Unsafe.Add(ref ip, (nint)(uint)(skip >> 5));
                skip++;
                if (!Unsafe.IsAddressLessThan(ref ip, ref matchLimit))
                    goto EMIT_LAST;
            }
            skip = 32; // match found → reset step to 1

            // ── Backward extension — free ratio improvement ───────────
            while (Unsafe.IsAddressGreaterThan(ref ip, ref anchor)
                && Unsafe.IsAddressGreaterThan(ref candidate, ref src)
                && Unsafe.Subtract(ref ip, (nint)(uint)1) == Unsafe.Subtract(ref candidate, (nint)(uint)1))
            {
                ip = ref Unsafe.Subtract(ref ip, (nint)(uint)1);
                candidate = ref Unsafe.Subtract(ref candidate, (nint)(uint)1);
            }

            // ── Forward extension ─────────────────────────────────────
            int matchLen = MinMatch + ExtendMatch(
                ref Unsafe.Add(ref ip, (nint)(uint)MinMatch),
                ref Unsafe.Add(ref candidate, (nint)(uint)MinMatch),
                ref extLimit);

            // ── Lazy evaluation (single-step) ─────────────────────────
            //  Before committing to the match at `ip`, probe ip+1.
            //  If that position yields a strictly longer match, pay one
            //  extra literal byte and use the better match instead.
            //
            //  Hash management:
            //    • ip's slot was already written at the top of the for-loop.
            //    • We eagerly write lip's slot here (read-before-write as usual).
            //      If we don't use the lazy match the slot is still a useful future hint.
            {
                ref byte lip = ref Unsafe.Add(ref ip, (nint)(uint)1);
                if (Unsafe.IsAddressLessThan(ref lip, ref matchLimit))
                {
                    uint lv4 = ReadU32(ref lip);
                    int lh = H4(lv4, hashShift);
                    int lazyPosition = (int)Unsafe.ByteOffset(ref src, ref lip);
                    ushort ls = Unsafe.Add(ref ht, unchecked((nint)(uint)(lh))); // read OLD candidate
                    Unsafe.Add(ref ht, unchecked((nint)(uint)(lh))) = unchecked((ushort)lazyPosition); // write lip (read-before-write)
                    int lazyDistance = unchecked((ushort)(lazyPosition - ls));

                    if ((uint)(lazyDistance - 1) < (uint)MaxOffset
                        && (uint)lazyDistance <= (uint)lazyPosition)
                    {
                        ref byte lcand = ref Unsafe.Subtract(ref lip, (nint)(uint)lazyDistance);
                        // A winning lazy match must reach beyond the current
                        // length. Reject a mismatching final word before rescanning.
                        if ((nint)Unsafe.ByteOffset(ref lip, ref extLimit) > matchLen
                            && ReadU32(ref lcand) == lv4
                            && ReadU32(ref Unsafe.Add(ref lip, (nint)(uint)(matchLen - 3)))
                                == ReadU32(ref Unsafe.Add(ref lcand, (nint)(uint)(matchLen - 3))))
                        {
                            int lazyLen = MinMatch + ExtendMatch(
                                ref Unsafe.Add(ref lip, (nint)(uint)MinMatch),
                                ref Unsafe.Add(ref lcand, (nint)(uint)MinMatch),
                                ref extLimit);
                            if (lazyLen > matchLen)
                            {
                                // The lazy match wins: treat `ip` as one more literal,
                                // shift the match start forward by 1.
                                ip = ref lip;
                                candidate = ref lcand;
                                matchLen = lazyLen;
                                // Re-run backward extension from the new (lazy) ip.
                                int backwardLength = 0;
                                while (Unsafe.IsAddressGreaterThan(ref ip, ref anchor)
                                    && Unsafe.IsAddressGreaterThan(ref candidate, ref src)
                                    && Unsafe.Subtract(ref ip, (nint)(uint)1) == Unsafe.Subtract(ref candidate, (nint)(uint)1))
                                {
                                    ip = ref Unsafe.Subtract(ref ip, (nint)(uint)1);
                                    candidate = ref Unsafe.Subtract(ref candidate, (nint)(uint)1);
                                    backwardLength++;
                                }
                                matchLen += backwardLength;
                            }
                        }
                    }
                }
            }

            int litLen = (int)Unsafe.ByteOffset(ref anchor, ref ip);
            int matchOff = (int)Unsafe.ByteOffset(ref candidate, ref ip);
            int matchNib = matchLen - MinMatch;

            // ── Emit sequence ─────────────────────────────────────────
            op = (byte)(((litLen < 15 ? litLen : 15) << 4)
                       | (matchNib < 15 ? matchNib : 15));
            op = ref Unsafe.Add(ref op, (nint)(uint)1);
            if (litLen >= 15)
                op = ref WriteExtendedLength(ref op, litLen - 15);
            LuminPackMarshal.CopyBlockUnaligned(ref op, ref anchor, (uint)litLen);
            op = ref Unsafe.Add(ref op, unchecked((nint)(uint)(litLen)));
            Unsafe.WriteUnaligned(ref op, (ushort)matchOff);
            op = ref Unsafe.Add(ref op, (nint)(uint)OffsetBytes);
            if (matchNib >= 15)
                op = ref WriteExtendedLength(ref op, matchNib - 15);

            ip = ref Unsafe.Add(ref ip, unchecked((nint)(uint)(matchLen)));
            anchor = ref ip;

            if (!Unsafe.IsAddressLessThan(ref ip, ref matchLimit))
                goto EMIT_LAST;

            // Refresh boundary positions ip-2 and ip-1.
            // Do NOT refresh ip itself — it is written at the top of the next for-loop.
            Unsafe.Add(ref ht, unchecked((nint)(uint)(H4(ReadU32(ref Unsafe.Subtract(ref ip, (nint)(uint)2)), hashShift))))
                = unchecked((ushort)(int)Unsafe.ByteOffset(ref src, ref Unsafe.Subtract(ref ip, (nint)(uint)2)));
            Unsafe.Add(ref ht, unchecked((nint)(uint)(H4(ReadU32(ref Unsafe.Subtract(ref ip, (nint)(uint)1)), hashShift))))
                = unchecked((ushort)(int)Unsafe.ByteOffset(ref src, ref Unsafe.Subtract(ref ip, (nint)(uint)1)));
        }

    EMIT_LAST:
        {
            int litLen = (int)Unsafe.ByteOffset(ref anchor, ref srcEnd);
            op = (byte)((litLen < 15 ? litLen : 15) << 4);
            op = ref Unsafe.Add(ref op, (nint)(uint)1);
            if (litLen >= 15)
                op = ref WriteExtendedLength(ref op, litLen - 15);
            LuminPackMarshal.CopyBlockUnaligned(ref op, ref anchor, (uint)litLen);
            op = ref Unsafe.Add(ref op, unchecked((nint)(uint)(litLen)));
        }

        return (int)Unsafe.ByteOffset(ref dst, ref op);
    }

    /// <summary>Writes a short length byte or an escaped canonical base-128 remainder.</summary>
    /// <param name="op">The op used by this operation.</param>
    /// <param name="extraLen">The extra len used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref byte WriteExtendedLength(ref byte op, int extraLen)
    {
        if (extraLen < 255)
        {
            op = (byte)extraLen;
            return ref Unsafe.Add(ref op, (nint)(uint)1);
        }
        op = 255;
        op = ref Unsafe.Add(ref op, (nint)(uint)1);
        uint remaining = (uint)(extraLen - 255);
        while (remaining >= 128)
        {
            op = (byte)(remaining | 0x80);
            op = ref Unsafe.Add(ref op, (nint)(uint)1);
            remaining >>= 7;
        }
        op = (byte)remaining;
        return ref Unsafe.Add(ref op, (nint)(uint)1);
    }

    /// <summary>Restores the uncompressed bytes from a LuminPack compression frame.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="srcLen">The src len used by this operation.</param>
    /// <param name="dst">The destination receiving the result.</param>
    /// <param name="origLen">The orig len used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static int DecompressCore(ref byte src, int srcLen, ref byte dst, int origLen)
    {
        ref byte ip = ref src;
        ref byte srcEnd = ref Unsafe.Add(ref src, unchecked((nint)(uint)(srcLen)));
        ref byte op = ref dst;
        ref byte dstBase = ref dst;
        ref byte dstEnd = ref Unsafe.Add(ref dst, unchecked((nint)(uint)(origLen)));

        while (Unsafe.IsAddressLessThan(ref ip, ref srcEnd))
        {
            byte token = ip;
            ip = ref Unsafe.Add(ref ip, (nint)(uint)1);
            int litNib = token >> 4;
            int matchNib = token & 0x0F;

            // ── Literal length ────────────────────────────────────
            int litLen = litNib;
            if (litNib == 15)
                ip = ref ReadExtendedLength(
                    ref ip,
                    ref srcEnd,
                    litLen,
                    (int)Unsafe.ByteOffset(ref op, ref dstEnd),
                    out litLen);

            // ── Copy literals ─────────────────────────────────────
            if (litLen > 0)
            {
                if ((uint)litLen > (uint)(int)Unsafe.ByteOffset(ref ip, ref srcEnd)
                 || (uint)litLen > (uint)(int)Unsafe.ByteOffset(ref op, ref dstEnd))
                    ThrowInvalidData();
                LuminPackMarshal.CopyBlockUnaligned(ref op, ref ip, (uint)litLen);
                ip = ref Unsafe.Add(ref ip, unchecked((nint)(uint)(litLen)));
                op = ref Unsafe.Add(ref op, unchecked((nint)(uint)(litLen)));
            }

            // ── End-of-stream ─────────────────────────────────────
            if (Unsafe.AreSame(ref ip, ref srcEnd))
            {
                // A final sequence has literals only.  Requiring a zero match
                // nibble makes a token whose offset/match body was truncated
                // distinguishable from a valid final token.
                if (matchNib != 0 || !Unsafe.AreSame(ref op, ref dstEnd))
                    ThrowInvalidData();
                return origLen;
            }

            // One trailing byte cannot contain the required 16-bit offset.
            if ((uint)(int)Unsafe.ByteOffset(ref ip, ref srcEnd) < OffsetBytes)
                ThrowInvalidData();

            // ── Match offset (2 bytes LE) ─────────────────────────
            int offset = Unsafe.ReadUnaligned<ushort>(ref ip);
            ip = ref Unsafe.Add(ref ip, (nint)(uint)OffsetBytes);

            if (offset == 0 || offset > (int)Unsafe.ByteOffset(ref dstBase, ref op))
                ThrowInvalidData();

            // ── Match length ──────────────────────────────────────
            int matchLen = matchNib + MinMatch;
            if (matchNib == 15)
                ip = ref ReadExtendedLength(
                    ref ip,
                    ref srcEnd,
                    matchLen,
                    (int)Unsafe.ByteOffset(ref op, ref dstEnd),
                    out matchLen);

            if ((uint)matchLen > (uint)(int)Unsafe.ByteOffset(ref op, ref dstEnd))
                ThrowInvalidData();

            // ── Copy match ────────────────────────────────────────
            CopyMatch(ref op, ref Unsafe.Subtract(ref op, (nint)(uint)offset), matchLen, offset);
            op = ref Unsafe.Add(ref op, unchecked((nint)(uint)(matchLen)));
        }

        // Every valid non-empty frame terminates in the literals-only branch
        // above. Reaching the physical end immediately after a match means the
        // final sequence was truncated, even if its match happened to fill dst.
        ThrowInvalidData();
        return 0;
    }

    /// <summary>Reads extended length from the current input.</summary>
    /// <param name="ip">The ip used by this operation.</param>
    /// <param name="srcEnd">The src end used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <param name="maxLength">The max length used by this operation.</param>
    /// <param name="decodedLength">Receives the decoded length produced by the operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref byte ReadExtendedLength(
        ref byte ip,
        ref byte srcEnd,
        int length,
        int maxLength,
        out int decodedLength)
    {
        if ((uint)length > (uint)maxLength)
            ThrowInvalidData();
        if (!Unsafe.IsAddressLessThan(ref ip, ref srcEnd))
            ThrowInvalidData();
        byte first = ip;
        ip = ref Unsafe.Add(ref ip, (nint)(uint)1);
        if (first > maxLength - length)
            ThrowInvalidData();
        length += first;
        if (first == 255)
            return ref ReadLongLength(ref ip, ref srcEnd, length, maxLength, out decodedLength);
        decodedLength = length;
        return ref ip;
    }

    /// <summary>Reads the remaining bytes of a canonical multi-byte length without signed overflow.</summary>
    /// <param name="ip">The cursor after the escape byte.</param>
    /// <param name="srcEnd">The exclusive end of the compressed input.</param>
    /// <param name="length">The saturated nibble's base length plus 255.</param>
    /// <param name="maxLength">The remaining output capacity.</param>
    /// <param name="decodedLength">Receives the complete bounded length.</param>
    /// <returns>The cursor after the extension.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref byte ReadLongLength(
        ref byte ip,
        ref byte srcEnd,
        int length,
        int maxLength,
        out int decodedLength)
    {
        uint extra = 0;
        int shift = 0;
        while (true)
        {
            if (!Unsafe.IsAddressLessThan(ref ip, ref srcEnd))
                ThrowInvalidData();
            byte next = ip;
            ip = ref Unsafe.Add(ref ip, (nint)(uint)1);
            if (shift == 28 && next > 7)
                ThrowInvalidData();
            extra |= (uint)(next & 0x7F) << shift;
            if (extra > (uint)(maxLength - length))
                ThrowInvalidData();
            if (next < 128)
            {
                if (shift != 0 && next == 0)
                    ThrowInvalidData();
                break;
            }
            shift += 7;
        }
        length += (int)extra;

        decodedLength = length;
        return ref ip;
    }

    /// <summary>Copies match to the supplied destination.</summary>
    /// <param name="dst">The destination receiving the result.</param>
    /// <param name="src">The input data to process.</param>
    /// <param name="len">The len used by this operation.</param>
    /// <param name="offset">The position at which the operation starts.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyMatch(ref byte dst, ref byte src, int len, int offset)
    {
#if NETSTANDARD2_1
        if (global::LuminPack.Core.LuminPackBurst.TryCopyMatch(ref dst, len, offset))
            return;
#endif
        if (offset == 1)
        {
            Unsafe.InitBlockUnaligned(ref dst, src, (uint)len);
            return;
        }
        if (len <= offset)
        {
            LuminPackMarshal.CopyBlockUnaligned(ref dst, ref src, (uint)len);
            return;
        }

        if (len >= 256 || (offset < 8 && len >= 64))
        {
            CopyLongMatch(ref dst, ref src, len, offset);
            return;
        }

        // Overlapping periodic fill.
        // KEY INSIGHT: when offset >= 8, no single 8-byte read window overlaps
        // its corresponding 8-byte write window (read=[dst-offset, dst-offset+8),
        // write=[dst, dst+8), gap = offset >= 8).  Safe to bulk-copy 8 bytes/iter.
        // For offset 2–7 the windows DO overlap; fall back to byte-by-byte.
        ref byte end = ref Unsafe.Add(ref dst, unchecked((nint)(uint)(len)));
        if (offset >= 8)
        {
#if NET8_0_OR_GREATER
            // LZ4 wild-copy style: widen the bulk copy when the offset guarantees the
            // read window stays fully behind the current write position (offset >= width),
            // and the length is long enough that a vectorized copy pays off over the
            // chain-check overhead (short copies stay scalar).
            if (offset >= 64 && Vector512.IsHardwareAccelerated && Avx512BW.IsSupported && len >= 128)
            {
                CopyMatch512(ref dst, ref src, ref end);
                return;
            }
            if (offset >= 32 && Vector256.IsHardwareAccelerated && len >= 64)
            {
                CopyMatch256(ref dst, ref src, ref end);
                return;
            }
            if (offset >= 16 && Vector128.IsHardwareAccelerated && len >= 32)
            {
                CopyMatch128(ref dst, ref src, ref end);
                return;
            }
#endif
            CopyMatchCore8(ref dst, ref src, ref end);
            return;
        }
        // offset 2–7: byte-by-byte (period too small for bulk tricks without scatter)
        while (Unsafe.IsAddressLessThan(ref dst, ref end))
        {
            dst = src;
            dst = ref Unsafe.Add(ref dst, (nint)(uint)1);
            src = ref Unsafe.Add(ref src, (nint)(uint)1);
        }
    }

    /// <summary>Expands a periodic prefix with bounded non-overlapping copies of increasing size.</summary>
    /// <param name="dst">The complete writable match range.</param>
    /// <param name="src">The initialized prefix at the match distance before the destination.</param>
    /// <param name="length">The validated output length.</param>
    /// <param name="offset">The positive match distance, smaller than the output length.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CopyLongMatch(ref byte dst, ref byte src, int length, int offset)
    {
        // Each copy reads only the initialized prefix ending at its write
        // position. Doubling that prefix breaks the per-vector dependency
        // of short periods and lets the shared SIMD copy handle long runs.
        uint copied = 0;
        uint width = (uint)offset;
        while (copied < (uint)length)
        {
            uint count = Math.Min(width, (uint)length - copied);
            LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref dst, (nint)(uint)copied), ref src, count);
            copied += count;
            width += count;
        }
    }

    /// <summary>Bounded 8-byte copies for distances of at least eight, followed by a byte tail.</summary>
    /// <param name="dst">The destination receiving the result.</param>
    /// <param name="src">The input data to process.</param>
    /// <param name="end">The end used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyMatchCore8(ref byte dst, ref byte src, ref byte end)
    {
        while (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref dst, (nint)(uint)8), ref end))
        {
            if (Unsafe.SizeOf<IntPtr>() == 8)
                Unsafe.WriteUnaligned(ref dst, Unsafe.ReadUnaligned<ulong>(ref src));
            else
                LuminPackMarshal.CopyBlockUnaligned(ref dst, ref src, 8);
            dst = ref Unsafe.Add(ref dst, (nint)(uint)8);
            src = ref Unsafe.Add(ref src, (nint)(uint)8);
        }
        while (Unsafe.IsAddressLessThan(ref dst, ref end))
        {
            dst = src;
            dst = ref Unsafe.Add(ref dst, (nint)(uint)1);
            src = ref Unsafe.Add(ref src, (nint)(uint)1);
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>Bounded 64-byte copy (offset ≥ 64); delegates the residual to the 32-byte path.</summary>
    /// <param name="dst">The destination receiving the result.</param>
    /// <param name="src">The input data to process.</param>
    /// <param name="end">The end used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyMatch512(ref byte dst, ref byte src, ref byte end)
    {
        while (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref dst, (nint)(uint)64), ref end))
        {
            Vector512.StoreUnsafe(Vector512.LoadUnsafe(ref src), ref dst);
            dst = ref Unsafe.Add(ref dst, (nint)(uint)64);
            src = ref Unsafe.Add(ref src, (nint)(uint)64);
        }
        CopyMatch256(ref dst, ref src, ref end);
    }

    /// <summary>Bounded 32-byte copy (offset ≥ 32); delegates the residual to the 16-byte path.</summary>
    /// <param name="dst">The destination receiving the result.</param>
    /// <param name="src">The input data to process.</param>
    /// <param name="end">The end used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyMatch256(ref byte dst, ref byte src, ref byte end)
    {
        while (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref dst, (nint)(uint)32), ref end))
        {
            Vector256.StoreUnsafe(Vector256.LoadUnsafe(ref src), ref dst);
            dst = ref Unsafe.Add(ref dst, (nint)(uint)32);
            src = ref Unsafe.Add(ref src, (nint)(uint)32);
        }
        CopyMatch128(ref dst, ref src, ref end);
    }

    /// <summary>Bounded 16-byte copy (offset ≥ 16); delegates the residual to the 8-byte path.</summary>
    /// <param name="dst">The destination receiving the result.</param>
    /// <param name="src">The input data to process.</param>
    /// <param name="end">The end used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyMatch128(ref byte dst, ref byte src, ref byte end)
    {
        while (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref dst, (nint)(uint)16), ref end))
        {
            Vector128.StoreUnsafe(Vector128.LoadUnsafe(ref src), ref dst);
            dst = ref Unsafe.Add(ref dst, (nint)(uint)16);
            src = ref Unsafe.Add(ref src, (nint)(uint)16);
        }
        CopyMatchCore8(ref dst, ref src, ref end);
    }

    /// <summary>SIMD match extension, 64-byte stride (Vector512; needs AVX512BW).</summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <param name="start">The start used by this operation.</param>
    /// <param name="limit">The limit used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ExtendMatch512(ref byte a, ref byte b, ref byte start, ref byte limit)
    {
        nint remaining = (nint)Unsafe.ByteOffset(ref a, ref limit);
        while (remaining >= 256)
        {
            var d0 = Vector512.LoadUnsafe(ref a) ^ Vector512.LoadUnsafe(ref b);
            var d1 = Vector512.LoadUnsafe(ref a, 64) ^ Vector512.LoadUnsafe(ref b, 64);
            var d2 = Vector512.LoadUnsafe(ref a, 128) ^ Vector512.LoadUnsafe(ref b, 128);
            var d3 = Vector512.LoadUnsafe(ref a, 192) ^ Vector512.LoadUnsafe(ref b, 192);
            if (!Vector512.EqualsAll((d0 | d1) | (d2 | d3), Vector512<byte>.Zero))
            {
                if (!Vector512.EqualsAll(d0, Vector512<byte>.Zero))
                    return (int)Unsafe.ByteOffset(ref start, ref a)
                        + BitOperations.TrailingZeroCount(~Vector512.Equals(d0, Vector512<byte>.Zero).ExtractMostSignificantBits());
                if (!Vector512.EqualsAll(d1, Vector512<byte>.Zero))
                    return (int)Unsafe.ByteOffset(ref start, ref a) + 64
                        + BitOperations.TrailingZeroCount(~Vector512.Equals(d1, Vector512<byte>.Zero).ExtractMostSignificantBits());
                if (!Vector512.EqualsAll(d2, Vector512<byte>.Zero))
                    return (int)Unsafe.ByteOffset(ref start, ref a) + 128
                        + BitOperations.TrailingZeroCount(~Vector512.Equals(d2, Vector512<byte>.Zero).ExtractMostSignificantBits());
                return (int)Unsafe.ByteOffset(ref start, ref a) + 192
                        + BitOperations.TrailingZeroCount(~Vector512.Equals(d3, Vector512<byte>.Zero).ExtractMostSignificantBits());
            }
            a = ref Unsafe.Add(ref a, (nint)(uint)256);
            b = ref Unsafe.Add(ref b, (nint)(uint)256);
            remaining -= 256;
        }
        while (remaining >= 64)
        {
            ulong mask = Vector512.Equals(
                Vector512.LoadUnsafe(ref a), Vector512.LoadUnsafe(ref b))
                .ExtractMostSignificantBits();
            if (mask != ulong.MaxValue)
                return (int)Unsafe.ByteOffset(ref start, ref a)
                     + (int)BitOperations.TrailingZeroCount(~mask);
            a = ref Unsafe.Add(ref a, (nint)(uint)64);
            b = ref Unsafe.Add(ref b, (nint)(uint)64);
            remaining -= 64;
        }
        return ExtendMatch256(ref a, ref b, ref start, ref limit);
    }

    /// <summary>SIMD match extension, 32-byte stride (Vector256 / AVX2).</summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <param name="start">The start used by this operation.</param>
    /// <param name="limit">The limit used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ExtendMatch256(ref byte a, ref byte b, ref byte start, ref byte limit)
    {
        nint remaining = (nint)Unsafe.ByteOffset(ref a, ref limit);
        while (remaining >= 128)
        {
            var d0 = Vector256.LoadUnsafe(ref a) ^ Vector256.LoadUnsafe(ref b);
            var d1 = Vector256.LoadUnsafe(ref a, 32) ^ Vector256.LoadUnsafe(ref b, 32);
            var d2 = Vector256.LoadUnsafe(ref a, 64) ^ Vector256.LoadUnsafe(ref b, 64);
            var d3 = Vector256.LoadUnsafe(ref a, 96) ^ Vector256.LoadUnsafe(ref b, 96);
            if (!Vector256.EqualsAll((d0 | d1) | (d2 | d3), Vector256<byte>.Zero))
            {
                if (!Vector256.EqualsAll(d0, Vector256<byte>.Zero))
                    return (int)Unsafe.ByteOffset(ref start, ref a)
                        + BitOperations.TrailingZeroCount(~Vector256.Equals(d0, Vector256<byte>.Zero).ExtractMostSignificantBits());
                if (!Vector256.EqualsAll(d1, Vector256<byte>.Zero))
                    return (int)Unsafe.ByteOffset(ref start, ref a) + 32
                        + BitOperations.TrailingZeroCount(~Vector256.Equals(d1, Vector256<byte>.Zero).ExtractMostSignificantBits());
                if (!Vector256.EqualsAll(d2, Vector256<byte>.Zero))
                    return (int)Unsafe.ByteOffset(ref start, ref a) + 64
                        + BitOperations.TrailingZeroCount(~Vector256.Equals(d2, Vector256<byte>.Zero).ExtractMostSignificantBits());
                return (int)Unsafe.ByteOffset(ref start, ref a) + 96
                        + BitOperations.TrailingZeroCount(~Vector256.Equals(d3, Vector256<byte>.Zero).ExtractMostSignificantBits());
            }
            a = ref Unsafe.Add(ref a, (nint)(uint)128);
            b = ref Unsafe.Add(ref b, (nint)(uint)128);
            remaining -= 128;
        }
        while (remaining >= 32)
        {
            uint mask = Vector256.Equals(
                Vector256.LoadUnsafe(ref a), Vector256.LoadUnsafe(ref b))
                .ExtractMostSignificantBits();
            if (mask != uint.MaxValue)
                return (int)Unsafe.ByteOffset(ref start, ref a)
                     + (int)BitOperations.TrailingZeroCount(~mask);
            a = ref Unsafe.Add(ref a, (nint)(uint)32);
            b = ref Unsafe.Add(ref b, (nint)(uint)32);
            remaining -= 32;
        }
        return ExtendMatch128(ref a, ref b, ref start, ref limit);
    }

    /// <summary>SIMD match extension with bounded 128-bit vectors on x86 and ARM64.</summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <param name="start">The start used by this operation.</param>
    /// <param name="limit">The limit used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ExtendMatch128(ref byte a, ref byte b, ref byte start, ref byte limit)
    {
        nint remaining = (nint)Unsafe.ByteOffset(ref a, ref limit);
        while (remaining >= 64)
        {
            var d0 = Vector128.LoadUnsafe(ref a) ^ Vector128.LoadUnsafe(ref b);
            var d1 = Vector128.LoadUnsafe(ref a, 16) ^ Vector128.LoadUnsafe(ref b, 16);
            var d2 = Vector128.LoadUnsafe(ref a, 32) ^ Vector128.LoadUnsafe(ref b, 32);
            var d3 = Vector128.LoadUnsafe(ref a, 48) ^ Vector128.LoadUnsafe(ref b, 48);
            if (!Vector128.EqualsAll((d0 | d1) | (d2 | d3), Vector128<byte>.Zero))
            {
                if (!Vector128.EqualsAll(d0, Vector128<byte>.Zero))
                    return (int)Unsafe.ByteOffset(ref start, ref a)
                        + BitOperations.TrailingZeroCount(~Vector128.Equals(d0, Vector128<byte>.Zero).ExtractMostSignificantBits());
                if (!Vector128.EqualsAll(d1, Vector128<byte>.Zero))
                    return (int)Unsafe.ByteOffset(ref start, ref a) + 16
                        + BitOperations.TrailingZeroCount(~Vector128.Equals(d1, Vector128<byte>.Zero).ExtractMostSignificantBits());
                if (!Vector128.EqualsAll(d2, Vector128<byte>.Zero))
                    return (int)Unsafe.ByteOffset(ref start, ref a) + 32
                        + BitOperations.TrailingZeroCount(~Vector128.Equals(d2, Vector128<byte>.Zero).ExtractMostSignificantBits());
                return (int)Unsafe.ByteOffset(ref start, ref a) + 48
                        + BitOperations.TrailingZeroCount(~Vector128.Equals(d3, Vector128<byte>.Zero).ExtractMostSignificantBits());
            }
            a = ref Unsafe.Add(ref a, (nint)(uint)64);
            b = ref Unsafe.Add(ref b, (nint)(uint)64);
            remaining -= 64;
        }
        while (remaining >= 16)
        {
            // Only the low 16 mask bits are meaningful for a 16-byte vector.
            uint mask = Vector128.Equals(
                Vector128.LoadUnsafe(ref a), Vector128.LoadUnsafe(ref b))
                .ExtractMostSignificantBits();
            if ((mask & 0xFFFF) != 0xFFFF)
                return (int)Unsafe.ByteOffset(ref start, ref a)
                     + (int)BitOperations.TrailingZeroCount(~mask);
            a = ref Unsafe.Add(ref a, (nint)(uint)16);
            b = ref Unsafe.Add(ref b, (nint)(uint)16);
            remaining -= 16;
        }
        return ExtendMatchScalar(ref a, ref b, ref start, ref limit);
    }
#endif

    /// <summary>Scalar 16-byte-unrolled + 8-byte + 1-byte match extension (shared netstandard2.1 path).</summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <param name="start">The start used by this operation.</param>
    /// <param name="limit">The limit used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ExtendMatchScalar(ref byte a, ref byte b, ref byte start, ref byte limit)
    {
        // 16-byte unrolled loop: two ulong XOR per iteration.
        while (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref a, (nint)(uint)16), ref limit))
        {
            ulong d0 = ReadU64(ref a) ^ ReadU64(ref b);
            ulong d1 = ReadU64(ref Unsafe.Add(ref a, (nint)(uint)8)) ^ ReadU64(ref Unsafe.Add(ref b, (nint)(uint)8));
            if ((d0 | d1) != 0)
            {
                if (d0 != 0)
                    return (int)Unsafe.ByteOffset(ref start, ref a) + TrailingZeroBytes(d0);
                return (int)Unsafe.ByteOffset(ref start, ref a) + 8 + TrailingZeroBytes(d1);
            }
            a = ref Unsafe.Add(ref a, (nint)(uint)16);
            b = ref Unsafe.Add(ref b, (nint)(uint)16);
        }
        // 8-byte tail
        if (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref a, (nint)(uint)8), ref limit))
        {
            ulong diff = ReadU64(ref a) ^ ReadU64(ref b);
            if (diff != 0)
                return (int)Unsafe.ByteOffset(ref start, ref a) + TrailingZeroBytes(diff);
            a = ref Unsafe.Add(ref a, (nint)(uint)8);
            b = ref Unsafe.Add(ref b, (nint)(uint)8);
        }
        // byte tail
        while (Unsafe.IsAddressLessThan(ref a, ref limit) && a == b)
        {
            a = ref Unsafe.Add(ref a, (nint)(uint)1);
            b = ref Unsafe.Add(ref b, (nint)(uint)1);
        }
        return (int)Unsafe.ByteOffset(ref start, ref a);
    }

    /// <summary>Performs the extend match step for the current operation.</summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <param name="limit">The limit used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ExtendMatch(ref byte a, ref byte b, ref byte limit)
    {
#if NETSTANDARD2_1
        int maximum = checked((int)Unsafe.ByteOffset(ref a, ref limit));
        if (global::LuminPack.Core.LuminPackBurst.TryCountMatch(ref a, ref b, maximum, out int matched))
            return matched;
#endif
        ref byte start = ref a;

#if NET8_0_OR_GREATER
        // SIMD match extension: compare 64/32/16 bytes per iteration and locate the
        // first differing byte via the comparison mask. Equal bytes set mask bit i;
        // the first mismatch is TrailingZeroCount(~mask) (lowest clear bit).
        // Kept as small non-inlined helpers so ExtendMatch itself stays JIT-inlinable
        // into its hot loop; only long matches ever pay the call. The IsHardwareAccelerated /
        // IsSupported checks are JIT-folded constants (no field reads).
        nint remaining = (nint)Unsafe.ByteOffset(ref a, ref limit);
        if (Vector128.IsHardwareAccelerated && remaining >= 16)
        {
            // Most hash hits end within a word. Reject them before loading a complete vector group.
            ulong difference = ReadU64(ref a) ^ ReadU64(ref b);
            if (difference != 0)
                return TrailingZeroBytes(difference);
        }
        if (Vector512.IsHardwareAccelerated && Avx512BW.IsSupported && remaining >= 64)
            return ExtendMatch512(ref a, ref b, ref start, ref limit);
        if (Vector256.IsHardwareAccelerated && remaining >= 32)
            return ExtendMatch256(ref a, ref b, ref start, ref limit);
        if (Vector128.IsHardwareAccelerated && remaining >= 16)
            return ExtendMatch128(ref a, ref b, ref start, ref limit);
#endif

        return ExtendMatchScalar(ref a, ref b, ref start, ref limit);
    }

    // ══════════════════════════════════════════════════════════════
    //  UTILITY
    // ══════════════════════════════════════════════════════════════

    /// <summary>Reads u32 from the current input.</summary>
    /// <param name="p">The p used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadU32(ref byte p) => Unsafe.ReadUnaligned<uint>(ref p);

    /// <summary>Reads u64 from the current input.</summary>
    /// <param name="p">The p used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadU64(ref byte p)
    {
        if (Unsafe.SizeOf<IntPtr>() == 8)
            return Unsafe.ReadUnaligned<ulong>(ref p);
        ulong value = default;
        LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.As<ulong, byte>(ref value), ref p, 8);
        return value;
    }

    /// <summary>Performs the trailing zero bytes step for the current operation.</summary>
    /// <param name="v">The v used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TrailingZeroBytes(ulong v)
    {
#if NET6_0_OR_GREATER
        return BitOperations.TrailingZeroCount(v) >> 3;
#else
        if (v == 0)
            return 8;
        int n = 0;
        while ((v & 0xFF) == 0)
        {
            v >>= 8;
            n++;
        }
        return n;
#endif
    }

    /// <summary>Validates empty frame before the operation continues.</summary>
    /// <param name="sourceLength">The length of the complete frame.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateEmptyFrame(int sourceLength)
    {
        if (sourceLength != HeaderSize)
            ThrowInvalidData();
    }

    /// <summary>Validates declared output size before the operation continues.</summary>
    /// <param name="source">The complete frame, including its validated header.</param>
    /// <param name="decompressedLength">The decompressed length used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateDeclaredOutputSize(ReadOnlySpan<byte> source, int decompressedLength)
    {
        if (decompressedLength == 0)
        {
            ValidateEmptyFrame(source.Length);
            return;
        }

        int bodyLength = source.Length - HeaderSize;
        if (bodyLength <= 0)
            ThrowInvalidData();
        // Compact lengths permit ratios above 255:1. Before allocating such an
        // output, validate every sequence and its exact expansion without writes.
        if (decompressedLength > (long)bodyLength * byte.MaxValue)
            ValidateHighRatioFrame(source, decompressedLength);
    }

    /// <summary>Checks the exact expansion of a high-ratio frame before allocation or buffer growth.</summary>
    /// <param name="source">The complete frame with a validated nonempty header.</param>
    /// <param name="decompressedLength">The declared output length.</param>
    private static void ValidateHighRatioFrame(ReadOnlySpan<byte> source, int decompressedLength)
    {
        ref byte start = ref MemoryMarshal.GetReference(source);
        ref byte ip = ref Unsafe.Add(ref start, (nint)(uint)HeaderSize);
        ref byte end = ref Unsafe.Add(ref start, (nint)(uint)source.Length);
        int produced = 0;
        while (Unsafe.IsAddressLessThan(ref ip, ref end))
        {
            byte token = ip;
            ip = ref Unsafe.Add(ref ip, (nint)(uint)1);
            int literals = token >> 4;
            if (literals == 15)
                ip = ref ReadExtendedLength(ref ip, ref end, literals, decompressedLength - produced, out literals);
            if ((uint)literals > (uint)(decompressedLength - produced)
                || (uint)literals > (uint)(int)Unsafe.ByteOffset(ref ip, ref end))
                ThrowInvalidData();
            produced += literals;
            ip = ref Unsafe.Add(ref ip, (nint)(uint)literals);
            int match = token & 15;
            if (Unsafe.AreSame(ref ip, ref end))
            {
                if (match != 0 || produced != decompressedLength)
                    ThrowInvalidData();
                return;
            }
            if ((uint)(int)Unsafe.ByteOffset(ref ip, ref end) < OffsetBytes)
                ThrowInvalidData();
            int offset = Unsafe.ReadUnaligned<ushort>(ref ip);
            ip = ref Unsafe.Add(ref ip, (nint)(uint)OffsetBytes);
            if (offset == 0 || offset > produced)
                ThrowInvalidData();
            match += MinMatch;
            if ((token & 15) == 15)
                ip = ref ReadExtendedLength(ref ip, ref end, match, decompressedLength - produced, out match);
            if ((uint)match > (uint)(decompressedLength - produced))
                ThrowInvalidData();
            produced += match;
        }
        ThrowInvalidData();
    }

    /// <summary>Raises the exception for insufficient buffer.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInsufficientBuffer()
        => LuminPackExceptionHelper.ThrowArgumentException("Destination buffer is too small for LuminZ operation.");

    /// <summary>Raises the exception for source too large.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowSourceTooLarge()
        => LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(
            "sourceLength",
            "Source is too large to represent a bounded LuminZ output in a single Span<byte>.");

    /// <summary>Raises the exception for invalid data.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidData()
        => LuminPackExceptionHelper.ThrowInvalidDataException("LuminZ: compressed data is invalid or corrupted.");
}
