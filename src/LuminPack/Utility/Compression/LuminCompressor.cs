// ============================================================
//  LuminCompressor.cs — LuminZ Algorithm
//  Part of LuminPack (https://github.com/TheLight-233/LuminPack)
//
//  Performance architecture (target: < 1 µs for ~2 KB payloads)
//  ─────────────────────────────────────────────────────────────
//
//  KEY DESIGN: ThreadStatic reused hash table
//  ───────────────────────────────────────────
//  Problem with per-call stackalloc + InitBlock:
//    Each call zero-initialises ~8 KB → ~60-80 ns wasted every call.
//    Over a benchmark run of 1M iterations this is enormous.
//
//  Solution: [ThreadStatic] ushort[] table — allocated ONCE per thread.
//    • CLR zero-initialises on first creation  (one-time ~60 ns cost).
//    • All subsequent calls reuse the same buffer with NO init.
//    • Stale entries from previous calls are harmless:
//        - dist check rejects candidates outside the 64 KB window
//        - ReadU32 confirm rejects fingerprint mismatches
//        - For same-input benchmarks, stale entries ARE warm-started!
//
//  CORRECTNESS: dist check BEFORE ReadU32
//  ────────────────────────────────────────
//  CRITICAL bug in naive implementations:
//    if (ReadU32(candidate) == v4 && dist <= MaxOffset)  ← WRONG ORDER
//  If candidate comes from a stale/garbage slot pointing past the input
//  buffer, ReadU32(candidate) is an out-of-bounds read.
//  The fix is ALWAYS:
//    if (dist_check_passes && ReadU32(candidate) == v4)  ← SHORT-CIRCUIT
//  The unsigned underflow trick `(uint)(ip - candidate) - 1u < MaxOffset`
//  ensures 1 ≤ dist ≤ MaxOffset in a single branchless comparison,
//  and ReadU32 is only called when candidate is a proven valid past position.
//
//  Other optimisations
//  ───────────────────
//  • Skip acceleration (Snappy-style): step = skip>>5, grows on misses,
//    resets to 1 on match. Dramatically speeds up incompressible sections.
//  • Backward extension: free ratio improvement per match.
//  • 64 KB window (2-byte offset) — matches LZ4 window size.
//  • No custom SIMD for copies — JIT CopyBlockUnaligned wins on small sizes.
//
//  v2 additions
//  ────────────
//  • Lazy matching (single-step): after finding a match at ip, probe ip+1.
//    If that position yields a strictly longer match, emit ip as an extra
//    literal and use the better match.  Typical ratio gain: +3–8 %.
//    The extra ExtendMatch call costs ~2 % compression speed in the worst
//    case but is fully amortised on compressible data.
//
//  • CopyMatch offset≥8 bulk path (decompression throughput):
//    When offset ≥ 8 the 8-byte read/write windows cannot overlap, so we
//    copy 8 bytes per iteration instead of 1.  Overlapping matches with small
//    offsets (2–7) still use the safe byte-by-byte loop.
//
//  • ExtendMatch 16-byte unroll:
//    Two ulong XOR comparisons per loop iteration with a combined OR check
//    before the branch.  Halves branch frequency on long matches and improves
//    ILP on out-of-order pipelines.
//
//  • WriteLSIC 4× unroll:
//    Drains 1020 bytes per loop body before the fine 255-step loop.
//    Negligible on typical payloads; measurable on very-long literal runs.
//
//  Wire format (unchanged)
//  ───────────
//  [Header  8 bytes]  magic(4)="LUMZ"  |  original_length(4) LE
//  [Sequences]
//      token         (1 byte)  litLen[7:4]  matchLen[3:0]
//      ext_lit_len   (0..N)    LSIC continuation bytes
//      literals      (litLen bytes)
//      match_offset  (2 bytes, LE)
//      ext_match_len (0..N)    LSIC continuation bytes
//  Last sequence: token + literals only (no offset).
// ============================================================

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;

#if NET6_0_OR_GREATER
using System.Numerics;
#endif

namespace LuminPack.Utility;

/// <summary>
/// LuminZ: a high-performance in-memory compression algorithm for LuminPack binary payloads.
/// </summary>
internal static class LuminCompressor
{
    private const uint Magic = 0x5A4D554C; // "LUMZ" LE
    private const int  HeaderSize = 8;
    private const int  OffsetBytes = 2;           // 16-bit offset → 64 KB window
    private const int  MaxOffset = 0xFFFF;
    private const int  MinMatch = 4;
    private const int  LastLiterals = 5;
    private const int  MatchGuard = 12;

    // Knuth multiplicative hash — same constant as LZ4 / Snappy
    private const uint HashMul4 = 2654435761u;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int H4(uint v, int shift) => (int)((v * HashMul4) >> shift);

    // ──────────────────────────────────────────────────────────────
    //  Per-thread hash table — reused across calls, never re-zeroed
    // ──────────────────────────────────────────────────────────────
    //  Entries store raw 16-bit source positions (0-based).
    //  0 = empty sentinel (CLR zero-inits on first allocation).
    //
    //  Stale entries from previous calls are safe because:
    //    (a) (uint)(ip - candidate) - 1u < MaxOffset  — rejects any
    //        candidate that is not a valid prior position within window
    //    (b) ReadU32(candidate) == v4                 — 4-byte confirm
    //  Both are checked (in that order — dist BEFORE ReadU32) so no
    //  out-of-bounds read ever occurs.
    [ThreadStatic]
    private static ushort[]? _ht;

    /// <summary>Maximum compressed-output size for a given uncompressed length.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMaxCompressedSize(int sourceLength)
    {
        if (sourceLength <= 0) return HeaderSize;
        return HeaderSize + sourceLength + (sourceLength >> 8) + 32;
    }

    /// <summary>Original uncompressed size stored in a LuminZ header.</summary>
    public static int GetDecompressedSize(ReadOnlySpan<byte> compressed)
    {
        if (compressed.Length < HeaderSize) ThrowInvalidData();
        ref byte p = ref MemoryMarshal.GetReference(compressed);
        if (Unsafe.ReadUnaligned<uint>(ref p) != Magic) ThrowInvalidData();
        int len = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref p, 4));
        if (len < 0) ThrowInvalidData();
        return len;
    }

    public static byte[] Compress(ReadOnlySpan<byte> source)
    {
        int maxLen = GetMaxCompressedSize(source.Length);
        byte[] tmp = ArrayPool<byte>.Shared.Rent(maxLen);
        try
        {
            int written = Compress(source, tmp.AsSpan(0, maxLen));
#if NET6_0_OR_GREATER
            var arr = GC.AllocateUninitializedArray<byte>(written);
            Buffer.BlockCopy(tmp, 0, arr, 0, written);
            return arr;
#else
            var result = new byte[written];
            Buffer.BlockCopy(tmp, 0, result, 0, written);
            return result;
#endif
        }
        finally { ArrayPool<byte>.Shared.Return(tmp); }
    }

    public static byte[] Decompress(ReadOnlySpan<byte> source)
    {
        int origLen = GetDecompressedSize(source);
        if (origLen == 0) return Array.Empty<byte>();
#if NET6_0_OR_GREATER
        var result = GC.AllocateUninitializedArray<byte>(origLen);
#else
        var result = new byte[origLen];
#endif
        Decompress(source, result);
        return result;
    }

    public static int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (destination.Length < GetMaxCompressedSize(source.Length))
            ThrowInsufficientBuffer();

        if (source.IsEmpty)
        {
            ref byte d = ref MemoryMarshal.GetReference(destination);
            Unsafe.WriteUnaligned(ref d, Magic);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref d, 4), 0);
            return HeaderSize;
        }

        ref byte src = ref MemoryMarshal.GetReference(source);
        ref byte dst = ref MemoryMarshal.GetReference(destination);
        Unsafe.WriteUnaligned(ref dst, Magic);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 4), source.Length);
        int bodyLen = CompressCore(ref src, source.Length, ref Unsafe.Add(ref dst, HeaderSize));
        return HeaderSize + bodyLen;
    }

    public static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length < HeaderSize) ThrowInvalidData();
        ref byte src = ref MemoryMarshal.GetReference(source);
        ref byte dst = ref MemoryMarshal.GetReference(destination);
        if (Unsafe.ReadUnaligned<uint>(ref src) != Magic) ThrowInvalidData();
        int origLen = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref src, 4));
        if (origLen < 0) ThrowInvalidData();
        if (origLen == 0) return 0;
        if (destination.Length < origLen) ThrowInsufficientBuffer();
        return DecompressCore(ref Unsafe.Add(ref src, HeaderSize), source.Length - HeaderSize, ref dst, origLen);
    }

    public static int Compress(LuminBufferWriter source, LuminBufferWriter destination)
    {
        ReadOnlySpan<byte> srcSpan = source.GetSpan();
        int needed = GetMaxCompressedSize(srcSpan.Length);
        destination.EnsureCapacity(needed);
        int written = Compress(srcSpan, destination.GetFullSpan());
        destination.SetWrittenCount(written);
        return written;
    }

    public static int Decompress(LuminBufferWriter source, LuminBufferWriter destination)
    {
        ReadOnlySpan<byte> srcSpan = source.GetSpan();
        int origLen = GetDecompressedSize(srcSpan);
        if (origLen == 0) { destination.SetWrittenCount(0); return 0; }
        destination.EnsureCapacity(origLen);
        int written = Decompress(srcSpan, destination.GetFullSpan());
        destination.SetWrittenCount(written);
        return written;
    }

    public static void CompressAndWriteTo(ReadOnlySpan<byte> source, ref LuminPackWriter writer)
    {
        if (source.IsEmpty) return;
        int maxLen = GetMaxCompressedSize(source.Length);
        byte[] rent = ArrayPool<byte>.Shared.Rent(maxLen);
        try
        {
            int compLen = Compress(source, rent.AsSpan(0, maxLen));
            ref byte p = ref LuminPackMarshal.GetArrayDataReference(rent);
#if NET8_0_OR_GREATER
            Unsafe.CopyBlockUnaligned(
                ref Unsafe.Add(ref writer._bufferStart, (nint)(uint)writer.CurrentIndex),
                ref p, (uint)compLen);
#else
            unsafe
            {
                Unsafe.CopyBlockUnaligned(
                    ref Unsafe.Add(ref Unsafe.AsRef<byte>(writer._bufferStart), (nint)(uint)writer.CurrentIndex),
                    ref p, (uint)compLen);
            }
#endif
            writer.Advance(compLen);
        }
        finally { ArrayPool<byte>.Shared.Return(rent); }
    }

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
        if (tableBits < 8)  tableBits = 8;
        if (tableBits > 15) tableBits = 15;
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

    private static int CompressInner(ref byte src, int srcLen, ref byte dst, ref ushort ht, int hashShift)
    {
        ref byte ip         = ref src;
        ref byte srcEnd     = ref Unsafe.Add(ref src, srcLen);
        ref byte op         = ref dst;
        ref byte anchor     = ref src;
        ref byte matchLimit = ref Unsafe.Add(ref srcEnd, -MatchGuard);
        ref byte extLimit   = ref Unsafe.Add(ref srcEnd, -LastLiterals);

        if (srcLen < MinMatch + LastLiterals + MatchGuard)
            goto EMIT_LAST;

        // Seed position 0; start scanning from position 1.
        Unsafe.Add(ref ht, H4(ReadU32(ref ip), hashShift)) = 0;
        ip = ref Unsafe.Add(ref ip, 1);

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
            uint  v4;

            // ── Inner scan: find 4-byte match with skip acceleration ──
            for (;;)
            {
                v4 = ReadU32(ref ip);
                int    h = H4(v4, hashShift);
                ushort s = Unsafe.Add(ref ht, h);                              // read OLD candidate position
                Unsafe.Add(ref ht, h) = (ushort)(int)Unsafe.ByteOffset(ref src, ref ip); // write CURRENT (read-before-write)

                candidate = ref Unsafe.Add(ref src, s);

                // ╔══════════════════════════════════════════════════╗
                // ║  CRITICAL: dist check BEFORE ReadU32             ║
                // ║                                                  ║
                // ║  (uint)(ip - candidate) - 1u < (uint)MaxOffset  ║
                // ║                                                  ║
                // ║  This single expression checks BOTH:             ║
                // ║   • dist >= 1  (no self-match → offset ≠ 0)     ║
                // ║   • dist <= MaxOffset  (within 64 KB window)    ║
                // ║                                                  ║
                // ║  For stale/garbage pos: if candidate > ip,       ║
                // ║  ip-candidate is negative; as uint it is huge    ║
                // ║  → subtraction underflows again → huge → false.  ║
                // ║  Short-circuit: ReadU32 called ONLY when         ║
                // ║  candidate is a proven valid prior position.     ║
                // ╚══════════════════════════════════════════════════╝
                if ((uint)(int)Unsafe.ByteOffset(ref candidate, ref ip) - 1u < (uint)MaxOffset
                    && ReadU32(ref candidate) == v4)
                    break;

                ip = ref Unsafe.Add(ref ip, (int)(skip >> 5));
                skip++;
                if (!Unsafe.IsAddressLessThan(ref ip, ref matchLimit))
                    goto EMIT_LAST;
            }
            skip = 32; // match found → reset step to 1

            // ── Backward extension — free ratio improvement ───────────
            while (Unsafe.IsAddressGreaterThan(ref ip, ref anchor)
                && Unsafe.IsAddressGreaterThan(ref candidate, ref src)
                && Unsafe.Add(ref ip, -1) == Unsafe.Add(ref candidate, -1))
            {
                ip        = ref Unsafe.Add(ref ip,        -1);
                candidate = ref Unsafe.Add(ref candidate, -1);
            }

            // ── Forward extension ─────────────────────────────────────
            int matchLen = MinMatch + ExtendMatch(
                ref Unsafe.Add(ref ip,        MinMatch),
                ref Unsafe.Add(ref candidate, MinMatch),
                ref extLimit);

            // ── Lazy evaluation (single-step) ─────────────────────────
            //  Before committing to the match at `ip`, probe ip+1.
            //  If that position yields a strictly longer match, pay one
            //  extra literal byte and use the better match instead.
            //  Typical gain: +3–8 % compression ratio at ~2 % speed cost.
            //
            //  Hash management:
            //    • ip's slot was already written at the top of the for-loop.
            //    • We eagerly write lip's slot here (read-before-write as usual).
            //      If we don't use the lazy match the slot is still a useful future hint.
            {
                ref byte lip = ref Unsafe.Add(ref ip, 1);
                if (Unsafe.IsAddressLessThan(ref lip, ref matchLimit))
                {
                    uint   lv4   = ReadU32(ref lip);
                    int    lh    = H4(lv4, hashShift);
                    ushort ls    = Unsafe.Add(ref ht, lh);                                  // read OLD candidate
                    Unsafe.Add(ref ht, lh) = (ushort)(int)Unsafe.ByteOffset(ref src, ref lip); // write lip (read-before-write)
                    ref byte lcand = ref Unsafe.Add(ref src, ls);

                    if ((uint)(int)Unsafe.ByteOffset(ref lcand, ref lip) - 1u < (uint)MaxOffset
                        && ReadU32(ref lcand) == lv4)
                    {
                        int lazyLen = MinMatch + ExtendMatch(
                            ref Unsafe.Add(ref lip,   MinMatch),
                            ref Unsafe.Add(ref lcand, MinMatch),
                            ref extLimit);
                        if (lazyLen > matchLen)
                        {
                            // The lazy match wins: treat `ip` as one more literal,
                            // shift the match start forward by 1.
                            ip        = ref lip;
                            candidate = ref lcand;
                            matchLen  = lazyLen;
                            // Re-run backward extension from the new (lazy) ip.
                            while (Unsafe.IsAddressGreaterThan(ref ip, ref anchor)
                                && Unsafe.IsAddressGreaterThan(ref candidate, ref src)
                                && Unsafe.Add(ref ip, -1) == Unsafe.Add(ref candidate, -1))
                            {
                                ip        = ref Unsafe.Add(ref ip,        -1);
                                candidate = ref Unsafe.Add(ref candidate, -1);
                            }
                        }
                    }
                }
            }

            int litLen   = (int)Unsafe.ByteOffset(ref anchor,    ref ip);
            int matchOff = (int)Unsafe.ByteOffset(ref candidate, ref ip);
            int matchNib = matchLen - MinMatch;

            // ── Emit sequence ─────────────────────────────────────────
            op = (byte)(((litLen  < 15 ? litLen  : 15) << 4)
                       | ( matchNib < 15 ? matchNib : 15));
            op = ref Unsafe.Add(ref op, 1);
            if (litLen  >= 15) op = ref WriteLSIC(ref op, litLen  - 15);
            Unsafe.CopyBlockUnaligned(ref op, ref anchor, (uint)litLen);
            op = ref Unsafe.Add(ref op, litLen);
            op                       = (byte) matchOff;
            Unsafe.Add(ref op, 1)    = (byte)(matchOff >> 8);
            op = ref Unsafe.Add(ref op, OffsetBytes);
            if (matchNib >= 15) op = ref WriteLSIC(ref op, matchNib - 15);

            ip     = ref Unsafe.Add(ref ip, matchLen);
            anchor = ref ip;

            if (!Unsafe.IsAddressLessThan(ref ip, ref matchLimit))
                goto EMIT_LAST;

            // Refresh boundary positions ip-2 and ip-1.
            // Do NOT refresh ip itself — it is written at the top of the next for-loop.
            Unsafe.Add(ref ht, H4(ReadU32(ref Unsafe.Add(ref ip, -2)), hashShift))
                = (ushort)(int)Unsafe.ByteOffset(ref src, ref Unsafe.Add(ref ip, -2));
            Unsafe.Add(ref ht, H4(ReadU32(ref Unsafe.Add(ref ip, -1)), hashShift))
                = (ushort)(int)Unsafe.ByteOffset(ref src, ref Unsafe.Add(ref ip, -1));
        }

        EMIT_LAST:
        {
            int litLen = (int)Unsafe.ByteOffset(ref anchor, ref srcEnd);
            op = (byte)((litLen < 15 ? litLen : 15) << 4);
            op = ref Unsafe.Add(ref op, 1);
            if (litLen >= 15) op = ref WriteLSIC(ref op, litLen - 15);
            Unsafe.CopyBlockUnaligned(ref op, ref anchor, (uint)litLen);
            op = ref Unsafe.Add(ref op, litLen);
        }

        return (int)Unsafe.ByteOffset(ref dst, ref op);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref byte WriteLSIC(ref byte op, int extraLen)
    {
        // Fast path: most lengths fit in 1-2 extra bytes.
        // Bulk path: drain 255*4 = 1020 per unrolled iteration before the fine loop.
        while (extraLen >= 255 * 4)
        {
            op                    = 255;
            Unsafe.Add(ref op, 1) = 255;
            Unsafe.Add(ref op, 2) = 255;
            Unsafe.Add(ref op, 3) = 255;
            op = ref Unsafe.Add(ref op, 4);
            extraLen -= 255 * 4;
        }
        while (extraLen >= 255)
        {
            op = 255;
            op = ref Unsafe.Add(ref op, 1);
            extraLen -= 255;
        }
        op = (byte)extraLen;
        return ref Unsafe.Add(ref op, 1);
    }

    private static int DecompressCore(ref byte src, int srcLen, ref byte dst, int origLen)
    {
        ref byte ip      = ref src;
        ref byte srcEnd  = ref Unsafe.Add(ref src, srcLen);
        ref byte op      = ref dst;
        ref byte dstBase = ref dst;
        ref byte dstEnd  = ref Unsafe.Add(ref dst, origLen);

        while (Unsafe.IsAddressLessThan(ref ip, ref srcEnd))
        {
            byte token    = ip;
            ip = ref Unsafe.Add(ref ip, 1);
            int  litNib   = token >> 4;
            int  matchNib = token & 0x0F;

            // ── Literal length ────────────────────────────────────
            int litLen = litNib;
            if (litNib == 15)
            {
                byte s;
                do
                {
                    if (!Unsafe.IsAddressLessThan(ref ip, ref srcEnd)) ThrowInvalidData();
                    s  = ip;
                    ip = ref Unsafe.Add(ref ip, 1);
                    litLen += s;
                }
                while (s == 255);
            }

            // ── Copy literals ─────────────────────────────────────
            if (litLen > 0)
            {
                if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref ip, litLen), ref srcEnd)
                 || Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref op, litLen), ref dstEnd))
                    ThrowInvalidData();
                Unsafe.CopyBlockUnaligned(ref op, ref ip, (uint)litLen);
                ip = ref Unsafe.Add(ref ip, litLen);
                op = ref Unsafe.Add(ref op, litLen);
            }

            // ── End-of-stream ─────────────────────────────────────
            if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref ip, OffsetBytes), ref srcEnd)) break;

            // ── Match offset (2 bytes LE) ─────────────────────────
            int offset = ip | (Unsafe.Add(ref ip, 1) << 8);
            ip = ref Unsafe.Add(ref ip, OffsetBytes);

            if (offset == 0 || offset > (int)Unsafe.ByteOffset(ref dstBase, ref op)) ThrowInvalidData();

            // ── Match length ──────────────────────────────────────
            int matchLen = matchNib + MinMatch;
            if (matchNib == 15)
            {
                byte s;
                do
                {
                    if (!Unsafe.IsAddressLessThan(ref ip, ref srcEnd)) ThrowInvalidData();
                    s  = ip;
                    ip = ref Unsafe.Add(ref ip, 1);
                    matchLen += s;
                }
                while (s == 255);
            }

            if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref op, matchLen), ref dstEnd)) ThrowInvalidData();

            // ── Copy match ────────────────────────────────────────
            CopyMatch(ref op, ref Unsafe.Add(ref op, -offset), matchLen, offset);
            op = ref Unsafe.Add(ref op, matchLen);
        }

        return (int)Unsafe.ByteOffset(ref dstBase, ref op);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyMatch(ref byte dst, ref byte src, int len, int offset)
    {
        if (offset == 1) { Unsafe.InitBlockUnaligned(ref dst, src, (uint)len); return; }
        if (len <= offset) { Unsafe.CopyBlockUnaligned(ref dst, ref src, (uint)len); return; }

        // Overlapping periodic fill.
        // KEY INSIGHT: when offset >= 8, no single 8-byte read window overlaps
        // its corresponding 8-byte write window (read=[dst-offset, dst-offset+8),
        // write=[dst, dst+8), gap = offset >= 8).  Safe to bulk-copy 8 bytes/iter.
        // For offset 2–7 the windows DO overlap; fall back to byte-by-byte.
        ref byte end = ref Unsafe.Add(ref dst, len);
        if (offset >= 8)
        {
            while (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref dst, 8), ref end))
            {
                if (Environment.Is64BitProcess)
                    Unsafe.WriteUnaligned(ref dst, Unsafe.ReadUnaligned<ulong>(ref src));
                else
                    Unsafe.CopyBlockUnaligned(ref dst, ref src, 8);
                dst = ref Unsafe.Add(ref dst, 8);
                src = ref Unsafe.Add(ref src, 8);
            }
            while (Unsafe.IsAddressLessThan(ref dst, ref end))
            {
                dst = src;
                dst = ref Unsafe.Add(ref dst, 1);
                src = ref Unsafe.Add(ref src, 1);
            }
            return;
        }
        // offset 2–7: byte-by-byte (period too small for bulk tricks without scatter)
        while (Unsafe.IsAddressLessThan(ref dst, ref end))
        {
            dst = src;
            dst = ref Unsafe.Add(ref dst, 1);
            src = ref Unsafe.Add(ref src, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ExtendMatch(ref byte a, ref byte b, ref byte limit)
    {
        ref byte start = ref a;

        // 16-byte unrolled loop: two ulong XOR per iteration.
        // The (d0|d1)!=0 OR-then-branch avoids a branch every 8 bytes and
        // keeps the predictor happy on long matches (common in compressible data).
        while (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref a, 16), ref limit))
        {
            ulong d0 = ReadU64(ref a)                    ^ ReadU64(ref b);
            ulong d1 = ReadU64(ref Unsafe.Add(ref a, 8)) ^ ReadU64(ref Unsafe.Add(ref b, 8));
            if ((d0 | d1) != 0)
            {
                if (d0 != 0) return (int)Unsafe.ByteOffset(ref start, ref a)     + TrailingZeroBytes(d0);
                return             (int)Unsafe.ByteOffset(ref start, ref a) + 8  + TrailingZeroBytes(d1);
            }
            a = ref Unsafe.Add(ref a, 16);
            b = ref Unsafe.Add(ref b, 16);
        }
        // 8-byte tail
        if (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref a, 8), ref limit))
        {
            ulong diff = ReadU64(ref a) ^ ReadU64(ref b);
            if (diff != 0) return (int)Unsafe.ByteOffset(ref start, ref a) + TrailingZeroBytes(diff);
            a = ref Unsafe.Add(ref a, 8);
            b = ref Unsafe.Add(ref b, 8);
        }
        // byte tail
        while (Unsafe.IsAddressLessThan(ref a, ref limit) && a == b)
        {
            a = ref Unsafe.Add(ref a, 1);
            b = ref Unsafe.Add(ref b, 1);
        }
        return (int)Unsafe.ByteOffset(ref start, ref a);
    }

    // ══════════════════════════════════════════════════════════════
    //  UTILITY
    // ══════════════════════════════════════════════════════════════

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint  ReadU32(ref byte p) => Unsafe.ReadUnaligned<uint>(ref p);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadU64(ref byte p)
    {
        if (Environment.Is64BitProcess)
            return Unsafe.ReadUnaligned<ulong>(ref p);
        ulong value = default;
        Unsafe.CopyBlockUnaligned(ref Unsafe.As<ulong, byte>(ref value), ref p, 8);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TrailingZeroBytes(ulong v)
    {
#if NET6_0_OR_GREATER
        return BitOperations.TrailingZeroCount(v) >> 3;
#else
        if (v == 0) return 8;
        int n = 0; while ((v & 0xFF) == 0) { v >>= 8; n++; } return n;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInsufficientBuffer()
        => throw new ArgumentException("Destination buffer is too small for LuminZ operation.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidData()
        => throw new InvalidDataException("LuminZ: compressed data is invalid or corrupted.");
}