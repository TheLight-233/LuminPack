using System.Runtime.CompilerServices;
using LuminPack.Code;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace LuminPack.Cryptography
{
    /// <summary>
    /// ChaCha20 stream cipher core (RFC 8439 / RFC 7539).
    /// <para>
    /// Pure 32-bit word operations, so it is fully compatible with <c>netstandard2.1</c>.
    /// Supports a 256-bit (or 128-bit) key and a 12-byte nonce.
    /// </para>
    /// <para>Internal primitive used by <see cref="LuminChaCha20Poly1305"/>.</para>
    /// </summary>
    internal static class ChaCha20
    {
        private const uint C0 = 0x61707865; // "expa"
        private const uint C1 = 0x3320646e; // "nd 3"
        private const uint C2 = 0x79622d32; // "2-by"
        private const uint C3 = 0x6b206574; // "te k"

        private const int KeyBytes256 = 32;
        private const int KeyBytes128 = 16;

        /// <summary>
        /// Constant-time quarter round over four state words (no data-dependent branches).
        /// </summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        /// <param name="c">The c used by this operation.</param>
        /// <param name="d">The d used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
        {
            a += b; d ^= a; d = Rotl(d, 16);
            c += d; b ^= c; b = Rotl(b, 12);
            a += b; d ^= a; d = Rotl(d, 8);
            c += d; b ^= c; b = Rotl(b, 7);
        }

        /// <summary>Performs the rotl step for the current operation.</summary>
        /// <param name="x">The x used by this operation.</param>
        /// <param name="n">The n used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Rotl(uint x, int n) => (x << n) | (x >> (32 - n));

        /// <summary>
        /// Computes one 64-byte keystream block from the given 16-word state (in place), writing
        /// the 64 output bytes little-endian to <paramref name="output"/>. Dispatches to the
        /// fastest available implementation (SSE2 via <c>BlockSse2</c>, scalar fallback).
        /// </summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        /// <param name="output">The destination receiving the result.</param>
        public static void Block(ref uint state, ref byte output)
        {
#if NETSTANDARD2_1
            if (global::LuminPack.Core.LuminPackBurst.TryChaCha20(ref state, ref output, 1))
                return;
#endif
#if NET8_0_OR_GREATER
            if (UseSse2)
            {
                BlockSse2(ref state, ref output);
                return;
            }
#endif
            BlockScalar(ref state, ref output);
        }

        /// <summary>
        /// Computes two consecutive 64-byte keystream blocks (counters <c>Unsafe.Add(ref state, 12)</c> and
        /// <c>Unsafe.Add(ref state, 12)+1</c>) in one call, writing 128 output bytes to <paramref name="output"/>.
        /// The input <paramref name="state"/> is never modified.
        /// </summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        /// <param name="output">The destination receiving the result.</param>
        public static void Block2(ref uint state, ref byte output)
        {
#if NETSTANDARD2_1
            if (global::LuminPack.Core.LuminPackBurst.TryChaCha20(ref state, ref output, 2))
                return;
#endif
#if NET8_0_OR_GREATER
            if (UseAvx2)
            {
                BlockAvx2x2(ref state, ref output);
                return;
            }
#endif
            // Safe fallback (the AEAD fast path never calls Block2 without AVX2; kept for correctness).
            uint counter = Unsafe.Add(ref state, 12);
            Block(ref state, ref output);
            Unsafe.Add(ref state, 12) = counter + 1u;
            Block(ref state, ref Unsafe.Add(ref output, 64));
            Unsafe.Add(ref state, 12) = counter;
        }

        /// <summary>
        /// Computes four consecutive 64-byte keystream blocks (counters <c>Unsafe.Add(ref state, 12)</c> through
        /// <c>Unsafe.Add(ref state, 12)+3</c>) in one call, writing 256 output bytes to <paramref name="output"/>.
        /// The input <paramref name="state"/> is never modified.
        /// </summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        /// <param name="output">The destination receiving the result.</param>
        public static void Block4(ref uint state, ref byte output)
        {
#if NETSTANDARD2_1
            if (global::LuminPack.Core.LuminPackBurst.TryChaCha20(ref state, ref output, 4))
                return;
#endif
#if NET8_0_OR_GREATER
            if (UseAvx512)
            {
                BlockAvx512x4(ref state, ref output);
                return;
            }
#endif
            // Safe fallback (the AEAD fast path never calls Block4 without AVX-512; kept for correctness).
            uint counter = Unsafe.Add(ref state, 12);
            Block2(ref state, ref output);
            Unsafe.Add(ref state, 12) = counter + 2u;
            Block2(ref state, ref Unsafe.Add(ref output, 128));
            Unsafe.Add(ref state, 12) = counter;
        }

        /// <summary>
        /// Computes one 64-byte keystream block from the given 16-word state (in place), writing
        /// the 64 output bytes little-endian to <paramref name="output"/>. Scalar 32-bit
        /// implementation (also used for <c>netstandard2.1</c>).
        /// </summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        /// <param name="output">The destination receiving the result.</param>
        public static void BlockScalar(ref uint state, ref byte output)
        {
            uint x0 = state, x1 = Unsafe.Add(ref state, 1), x2 = Unsafe.Add(ref state, 2), x3 = Unsafe.Add(ref state, 3);
            uint x4 = Unsafe.Add(ref state, 4), x5 = Unsafe.Add(ref state, 5), x6 = Unsafe.Add(ref state, 6), x7 = Unsafe.Add(ref state, 7);
            uint x8 = Unsafe.Add(ref state, 8), x9 = Unsafe.Add(ref state, 9), x10 = Unsafe.Add(ref state, 10), x11 = Unsafe.Add(ref state, 11);
            uint x12 = Unsafe.Add(ref state, 12), x13 = Unsafe.Add(ref state, 13), x14 = Unsafe.Add(ref state, 14), x15 = Unsafe.Add(ref state, 15);

            for (int i = 0; i < 10; i++)
            {
                QuarterRound(ref x0, ref x4, ref x8, ref x12);
                QuarterRound(ref x1, ref x5, ref x9, ref x13);
                QuarterRound(ref x2, ref x6, ref x10, ref x14);
                QuarterRound(ref x3, ref x7, ref x11, ref x15);
                QuarterRound(ref x0, ref x5, ref x10, ref x15);
                QuarterRound(ref x1, ref x6, ref x11, ref x12);
                QuarterRound(ref x2, ref x7, ref x8, ref x13);
                QuarterRound(ref x3, ref x4, ref x9, ref x14);
            }

            x0 += state;
            x1 += Unsafe.Add(ref state, 1);
            x2 += Unsafe.Add(ref state, 2);
            x3 += Unsafe.Add(ref state, 3);
            x4 += Unsafe.Add(ref state, 4);
            x5 += Unsafe.Add(ref state, 5);
            x6 += Unsafe.Add(ref state, 6);
            x7 += Unsafe.Add(ref state, 7);
            x8 += Unsafe.Add(ref state, 8);
            x9 += Unsafe.Add(ref state, 9);
            x10 += Unsafe.Add(ref state, 10);
            x11 += Unsafe.Add(ref state, 11);
            x12 += Unsafe.Add(ref state, 12);
            x13 += Unsafe.Add(ref state, 13);
            x14 += Unsafe.Add(ref state, 14);
            x15 += Unsafe.Add(ref state, 15);

            WriteUnaligned(ref output, x0);
            WriteUnaligned(ref Unsafe.Add(ref output, 4), x1);
            WriteUnaligned(ref Unsafe.Add(ref output, 8), x2);
            WriteUnaligned(ref Unsafe.Add(ref output, 12), x3);
            WriteUnaligned(ref Unsafe.Add(ref output, 16), x4);
            WriteUnaligned(ref Unsafe.Add(ref output, 20), x5);
            WriteUnaligned(ref Unsafe.Add(ref output, 24), x6);
            WriteUnaligned(ref Unsafe.Add(ref output, 28), x7);
            WriteUnaligned(ref Unsafe.Add(ref output, 32), x8);
            WriteUnaligned(ref Unsafe.Add(ref output, 36), x9);
            WriteUnaligned(ref Unsafe.Add(ref output, 40), x10);
            WriteUnaligned(ref Unsafe.Add(ref output, 44), x11);
            WriteUnaligned(ref Unsafe.Add(ref output, 48), x12);
            WriteUnaligned(ref Unsafe.Add(ref output, 52), x13);
            WriteUnaligned(ref Unsafe.Add(ref output, 56), x14);
            WriteUnaligned(ref Unsafe.Add(ref output, 60), x15);
        }

#if NET8_0_OR_GREATER
        private static readonly bool UseAvx512 = Avx512F.IsSupported;
        private static readonly bool UseAvx2 = Avx2.IsSupported;
        private static readonly bool UseSse2 = Sse2.IsSupported;

        /// <summary>Performs the rotl step for the current operation.</summary>
        /// <param name="v">The v used by this operation.</param>
        /// <param name="n">The n used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<uint> Rotl(Vector128<uint> v, byte n)
            => Sse2.Or(Sse2.ShiftLeftLogical(v, n), Sse2.ShiftRightLogical(v, (byte)(32 - n)));

        /// <summary>Performs the quarter round step for the current operation.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        /// <param name="c">The c used by this operation.</param>
        /// <param name="d">The d used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void QuarterRound(ref Vector128<uint> a, ref Vector128<uint> b, ref Vector128<uint> c, ref Vector128<uint> d)
        {
            a = Sse2.Add(a, b); d = Sse2.Xor(d, a); d = Rotl(d, 16);
            c = Sse2.Add(c, d); b = Sse2.Xor(b, c); b = Rotl(b, 12);
            a = Sse2.Add(a, b); d = Sse2.Xor(d, a); d = Rotl(d, 8);
            c = Sse2.Add(c, d); b = Sse2.Xor(b, c); b = Rotl(b, 7);
        }

        /// <summary>Performs the rotl step for the current operation.</summary>
        /// <param name="v">The v used by this operation.</param>
        /// <param name="n">The n used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> Rotl(Vector256<uint> v, byte n)
            => Avx2.Or(Avx2.ShiftLeftLogical(v, n), Avx2.ShiftRightLogical(v, (byte)(32 - n)));

        /// <summary>Performs the quarter round step for the current operation.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        /// <param name="c">The c used by this operation.</param>
        /// <param name="d">The d used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void QuarterRound(ref Vector256<uint> a, ref Vector256<uint> b, ref Vector256<uint> c, ref Vector256<uint> d)
        {
            a = Avx2.Add(a, b); d = Avx2.Xor(d, a); d = Rotl(d, 16);
            c = Avx2.Add(c, d); b = Avx2.Xor(b, c); b = Rotl(b, 12);
            a = Avx2.Add(a, b); d = Avx2.Xor(d, a); d = Rotl(d, 8);
            c = Avx2.Add(c, d); b = Avx2.Xor(b, c); b = Rotl(b, 7);
        }

        /// <summary>
        /// Single-block 4-lane SSE2 implementation. Each of the four 128-bit lanes holds one
        /// column (or diagonal) quarter round operand group, so a quarter round operates on
        /// four quarter rounds in parallel. Transposes between column and diagonal layouts with
        /// <see cref="Sse2.Shuffle(Vector128{uint}, byte)"/> lane rotations (vb: 1, vc: 2, vd: 3).
        /// </summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        /// <param name="output">The destination receiving the result.</param>
        private static void BlockSse2(ref uint state, ref byte output)
        {
            Vector128<uint> va = Vector128.LoadUnsafe(ref state);        // x0..x3
            Vector128<uint> vb = Vector128.LoadUnsafe(ref state, 4);    // x4..x7
            Vector128<uint> vc = Vector128.LoadUnsafe(ref state, 8);    // x8..x11
            Vector128<uint> vd = Vector128.LoadUnsafe(ref state, 12);   // x12..x15
            Vector128<uint> ova = va, ovb = vb, ovc = vc, ovd = vd;

            for (int i = 0; i < 10; i++)
            {
                QuarterRound(ref va, ref vb, ref vc, ref vd);      // column round (4 lanes in parallel)

                // Transpose to diagonal layout (va stays):
                vb = Sse2.Shuffle(vb, 0x39);                       // rotate lanes left by 1
                vc = Sse2.Shuffle(vc, 0x4E);                       // rotate lanes left by 2
                vd = Sse2.Shuffle(vd, 0x93);                       // rotate lanes left by 3

                QuarterRound(ref va, ref vb, ref vc, ref vd);      // diagonal round

                // Transpose back to column layout:
                vb = Sse2.Shuffle(vb, 0x93);                       // rotate lanes right by 1
                vc = Sse2.Shuffle(vc, 0x4E);                       // rotate lanes right by 2
                vd = Sse2.Shuffle(vd, 0x39);                       // rotate lanes right by 3
            }

            va = Sse2.Add(va, ova);
            vb = Sse2.Add(vb, ovb);
            vc = Sse2.Add(vc, ovc);
            vd = Sse2.Add(vd, ovd);

            va.AsByte().StoreUnsafe(ref output);
            vb.AsByte().StoreUnsafe(ref output, 16);
            vc.AsByte().StoreUnsafe(ref output, 32);
            vd.AsByte().StoreUnsafe(ref output, 48);
        }

        /// <summary>
        /// Two-block 2x4-lane AVX2 implementation: the lower 128-bit half carries block A
        /// (counter <c>Unsafe.Add(ref state, 12)</c>), the upper half block B (counter <c>Unsafe.Add(ref state, 12)+1</c>),
        /// producing 128 output bytes. All <see cref="Avx2"/> operations are lane-wise or
        /// per-128-bit-half independent — the two halves never mix, so <c>state</c> is only
        /// read and never modified.
        /// </summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        /// <param name="output">The destination receiving the result.</param>
        private static void BlockAvx2x2(ref uint state, ref byte output)
        {
            Vector256<uint> va = Vector256.Create(state, Unsafe.Add(ref state, 1), Unsafe.Add(ref state, 2), Unsafe.Add(ref state, 3),
                                                  state, Unsafe.Add(ref state, 1), Unsafe.Add(ref state, 2), Unsafe.Add(ref state, 3));
            Vector256<uint> vb = Vector256.Create(Unsafe.Add(ref state, 4), Unsafe.Add(ref state, 5), Unsafe.Add(ref state, 6), Unsafe.Add(ref state, 7),
                                                  Unsafe.Add(ref state, 4), Unsafe.Add(ref state, 5), Unsafe.Add(ref state, 6), Unsafe.Add(ref state, 7));
            Vector256<uint> vc = Vector256.Create(Unsafe.Add(ref state, 8), Unsafe.Add(ref state, 9), Unsafe.Add(ref state, 10), Unsafe.Add(ref state, 11),
                                                  Unsafe.Add(ref state, 8), Unsafe.Add(ref state, 9), Unsafe.Add(ref state, 10), Unsafe.Add(ref state, 11));
            Vector128<uint> vdLow = Vector128.Create(Unsafe.Add(ref state, 12), Unsafe.Add(ref state, 13), Unsafe.Add(ref state, 14), Unsafe.Add(ref state, 15));
            Vector128<uint> vdHigh = Vector128.Create(Unsafe.Add(ref state, 12) + 1u, Unsafe.Add(ref state, 13), Unsafe.Add(ref state, 14), Unsafe.Add(ref state, 15));
            Vector256<uint> vd = Vector256.Create(vdLow, vdHigh);
            Vector256<uint> ova = va, ovb = vb, ovc = vc, ovd = vd;

            for (int i = 0; i < 10; i++)
            {
                QuarterRound(ref va, ref vb, ref vc, ref vd);      // column round (4 lanes x 2 blocks)

                // Avx2.Shuffle applies independently to each 128-bit half; va stays:
                vb = Avx2.Shuffle(vb, 0x39);
                vc = Avx2.Shuffle(vc, 0x4E);
                vd = Avx2.Shuffle(vd, 0x93);

                QuarterRound(ref va, ref vb, ref vc, ref vd);      // diagonal round

                vb = Avx2.Shuffle(vb, 0x93);
                vc = Avx2.Shuffle(vc, 0x4E);
                vd = Avx2.Shuffle(vd, 0x39);
            }

            va = Avx2.Add(va, ova);
            vb = Avx2.Add(vb, ovb);
            vc = Avx2.Add(vc, ovc);
            vd = Avx2.Add(vd, ovd);

            va.GetLower().AsByte().StoreUnsafe(ref output);              // block A (counter c) -> [0..63]
            vb.GetLower().AsByte().StoreUnsafe(ref output, 16);
            vc.GetLower().AsByte().StoreUnsafe(ref output, 32);
            vd.GetLower().AsByte().StoreUnsafe(ref output, 48);
            va.GetUpper().AsByte().StoreUnsafe(ref output, 64);       // block B (counter c+1) -> [64..127]
            vb.GetUpper().AsByte().StoreUnsafe(ref output, 80);
            vc.GetUpper().AsByte().StoreUnsafe(ref output, 96);
            vd.GetUpper().AsByte().StoreUnsafe(ref output, 112);
        }

        /// <summary>Performs the rotl step for the current operation.</summary>
        /// <param name="v">The v used by this operation.</param>
        /// <param name="n">The n used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<uint> Rotl(Vector512<uint> v, byte n)
            => Avx512F.RotateLeft(v, n);

        /// <summary>Performs the quarter round step for the current operation.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        /// <param name="c">The c used by this operation.</param>
        /// <param name="d">The d used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void QuarterRound(ref Vector512<uint> a, ref Vector512<uint> b, ref Vector512<uint> c, ref Vector512<uint> d)
        {
            a = Avx512F.Add(a, b); d = Avx512F.Xor(d, a); d = Rotl(d, 16);
            c = Avx512F.Add(c, d); b = Avx512F.Xor(b, c); b = Rotl(b, 12);
            a = Avx512F.Add(a, b); d = Avx512F.Xor(d, a); d = Rotl(d, 8);
            c = Avx512F.Add(c, d); b = Avx512F.Xor(b, c); b = Rotl(b, 7);
        }

        /// <summary>
        /// Four-block 4x4-lane AVX-512 implementation: each of the four 512-bit lanes carries
        /// one block (counters <c>Unsafe.Add(ref state, 12)</c> .. <c>Unsafe.Add(ref state, 12)+3</c>), producing 256 output
        /// bytes. Rotations use the single-instruction <c>vprold</c>. <c>Avx512F.Shuffle</c>
        /// (vpshufd) applies independently to each 128-bit quarter, so the lane rotation
        /// transposes work per block exactly like the SSE2/AVX2 versions. All operations are
        /// lane-wise — the four blocks never mix, and <c>state</c> is only read, never modified.
        /// </summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        /// <param name="output">The destination receiving the result.</param>
        private static void BlockAvx512x4(ref uint state, ref byte output)
        {
            Vector128<uint> v0 = Vector128.Create(state, Unsafe.Add(ref state, 1), Unsafe.Add(ref state, 2), Unsafe.Add(ref state, 3));
            Vector128<uint> v4 = Vector128.Create(Unsafe.Add(ref state, 4), Unsafe.Add(ref state, 5), Unsafe.Add(ref state, 6), Unsafe.Add(ref state, 7));
            Vector128<uint> v8 = Vector128.Create(Unsafe.Add(ref state, 8), Unsafe.Add(ref state, 9), Unsafe.Add(ref state, 10), Unsafe.Add(ref state, 11));
            Vector128<uint> vd0 = Vector128.Create(Unsafe.Add(ref state, 12), Unsafe.Add(ref state, 13), Unsafe.Add(ref state, 14), Unsafe.Add(ref state, 15));
            Vector128<uint> vd1 = Vector128.Create(Unsafe.Add(ref state, 12) + 1u, Unsafe.Add(ref state, 13), Unsafe.Add(ref state, 14), Unsafe.Add(ref state, 15));
            Vector128<uint> vd2 = Vector128.Create(Unsafe.Add(ref state, 12) + 2u, Unsafe.Add(ref state, 13), Unsafe.Add(ref state, 14), Unsafe.Add(ref state, 15));
            Vector128<uint> vd3 = Vector128.Create(Unsafe.Add(ref state, 12) + 3u, Unsafe.Add(ref state, 13), Unsafe.Add(ref state, 14), Unsafe.Add(ref state, 15));

            Vector512<uint> va = Vector512.Create(Vector256.Create(v0, v0), Vector256.Create(v0, v0));
            Vector512<uint> vb = Vector512.Create(Vector256.Create(v4, v4), Vector256.Create(v4, v4));
            Vector512<uint> vc = Vector512.Create(Vector256.Create(v8, v8), Vector256.Create(v8, v8));
            Vector512<uint> vd = Vector512.Create(Vector256.Create(vd0, vd1), Vector256.Create(vd2, vd3));
            Vector512<uint> ova = va, ovb = vb, ovc = vc, ovd = vd;

            for (int i = 0; i < 10; i++)
            {
                QuarterRound(ref va, ref vb, ref vc, ref vd);      // column round (4 lanes x 4 blocks)

                // Avx512F.Shuffle applies independently to each 128-bit quarter; va stays:
                vb = Avx512F.Shuffle(vb, 0x39);
                vc = Avx512F.Shuffle(vc, 0x4E);
                vd = Avx512F.Shuffle(vd, 0x93);

                QuarterRound(ref va, ref vb, ref vc, ref vd);      // diagonal round

                vb = Avx512F.Shuffle(vb, 0x93);
                vc = Avx512F.Shuffle(vc, 0x4E);
                vd = Avx512F.Shuffle(vd, 0x39);
            }

            va = Avx512F.Add(va, ova);
            vb = Avx512F.Add(vb, ovb);
            vc = Avx512F.Add(vc, ovc);
            vd = Avx512F.Add(vd, ovd);

            Vector128<uint> va0 = va.GetLower().GetLower(), va1 = va.GetLower().GetUpper(),
                            va2 = va.GetUpper().GetLower(), va3 = va.GetUpper().GetUpper();
            Vector128<uint> vb0 = vb.GetLower().GetLower(), vb1 = vb.GetLower().GetUpper(),
                            vb2 = vb.GetUpper().GetLower(), vb3 = vb.GetUpper().GetUpper();
            Vector128<uint> vc0 = vc.GetLower().GetLower(), vc1 = vc.GetLower().GetUpper(),
                            vc2 = vc.GetUpper().GetLower(), vc3 = vc.GetUpper().GetUpper();
            Vector128<uint> vd0r = vd.GetLower().GetLower(), vd1r = vd.GetLower().GetUpper(),
                            vd2r = vd.GetUpper().GetLower(), vd3r = vd.GetUpper().GetUpper();

            // block 0 (counter c) -> [0..63]
            va0.AsByte().StoreUnsafe(ref output);
            vb0.AsByte().StoreUnsafe(ref output, 16);
            vc0.AsByte().StoreUnsafe(ref output, 32);
            vd0r.AsByte().StoreUnsafe(ref output, 48);
            // block 1 (counter c+1) -> [64..127]
            va1.AsByte().StoreUnsafe(ref output, 64);
            vb1.AsByte().StoreUnsafe(ref output, 80);
            vc1.AsByte().StoreUnsafe(ref output, 96);
            vd1r.AsByte().StoreUnsafe(ref output, 112);
            // block 2 (counter c+2) -> [128..191]
            va2.AsByte().StoreUnsafe(ref output, 128);
            vb2.AsByte().StoreUnsafe(ref output, 144);
            vc2.AsByte().StoreUnsafe(ref output, 160);
            vd2r.AsByte().StoreUnsafe(ref output, 176);
            // block 3 (counter c+3) -> [192..255]
            va3.AsByte().StoreUnsafe(ref output, 192);
            vb3.AsByte().StoreUnsafe(ref output, 208);
            vc3.AsByte().StoreUnsafe(ref output, 224);
            vd3r.AsByte().StoreUnsafe(ref output, 240);
        }
#endif

#if !NET && !NETSTANDARD
        // The Burst source projector parses with its own symbols and retains the native entry ABI.
        /// <summary>Writes one block for the Burst pointer entry.</summary>
        /// <param name="state">Sixteen input words.</param>
        /// <param name="output">The complete output block.</param>
        public static unsafe void Block(uint* state, byte* output) => Block(ref *state, ref *output);

        /// <summary>Writes two blocks for the Burst pointer entry.</summary>
        /// <param name="state">Sixteen input words.</param>
        /// <param name="output">The complete output blocks.</param>
        public static unsafe void Block2(uint* state, byte* output) => Block2(ref *state, ref *output);

        /// <summary>Writes four blocks for the Burst pointer entry.</summary>
        /// <param name="state">Sixteen input words.</param>
        /// <param name="output">The complete output blocks.</param>
        public static unsafe void Block4(uint* state, byte* output) => Block4(ref *state, ref *output);
#endif

        /// <summary>
        /// Initializes the 16-word state.
        /// </summary>
        /// <param name="state">16-word destination.</param>
        /// <param name="key">16 or 32 byte key.</param>
        /// <param name="keyBytes">Key length in bytes (16 or 32).</param>
        /// <param name="nonce">8 or 12 byte nonce.</param>
        /// <param name="nonceBytes">Nonce length in bytes (8 or 12).</param>
        /// <param name="blockCounter">Initial block counter.</param>
        public static void Initialize(ref uint state, ref byte key, int keyBytes, ref byte nonce, int nonceBytes, uint blockCounter)
        {
            state = C0;
            Unsafe.Add(ref state, 1) = C1;
            Unsafe.Add(ref state, 2) = C2;
            Unsafe.Add(ref state, 3) = C3;

            if (keyBytes == KeyBytes256)
            {
                // 256-bit key: words 4..11 = all 32 key bytes (two 16-byte halves).
                LoadKeyStore(words: ref Unsafe.Add(ref state, 4), key: ref key, offsetBytes: 0);
                LoadKeyStore(words: ref Unsafe.Add(ref state, 8), key: ref key, offsetBytes: 16);
            }
            else if (keyBytes == KeyBytes128)
            {
                // 128-bit key: words 4..7 = key, words 8..11 = key again.
                LoadKeyStore(words: ref Unsafe.Add(ref state, 4), key: ref key, offsetBytes: 0);
                LoadKeyStore(words: ref Unsafe.Add(ref state, 8), key: ref key, offsetBytes: 0);
            }
            else
            {
                LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(keyBytes), "ChaCha20 requires a 16 or 32 byte key.");
            }

            if (nonceBytes == 12)
            {
                Unsafe.Add(ref state, 12) = blockCounter;
                Unsafe.Add(ref state, 13) = ReadUnaligned(ref nonce);
                Unsafe.Add(ref state, 14) = ReadUnaligned(ref Unsafe.Add(ref nonce, 4));
                Unsafe.Add(ref state, 15) = ReadUnaligned(ref Unsafe.Add(ref nonce, 8));
            }
            else
            {
                LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(nonceBytes), "ChaCha20 requires a 12 byte nonce.");
            }
        }

        /// <summary>Performs the load key store step for the current operation.</summary>
        /// <param name="words">The words used by this operation.</param>
        /// <param name="key">The key used to identify the entry.</param>
        /// <param name="offsetBytes">The offset bytes used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LoadKeyStore(ref uint words, ref byte key, int offsetBytes)
        {
            words = ReadUnaligned(ref Unsafe.Add(ref key, offsetBytes + 0));
            Unsafe.Add(ref words, 1) = ReadUnaligned(ref Unsafe.Add(ref key, offsetBytes + 4));
            Unsafe.Add(ref words, 2) = ReadUnaligned(ref Unsafe.Add(ref key, offsetBytes + 8));
            Unsafe.Add(ref words, 3) = ReadUnaligned(ref Unsafe.Add(ref key, offsetBytes + 12));
        }

        /// <summary>Reads unaligned from the current input.</summary>
        /// <param name="b">The b used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUnaligned(ref byte b) => Unsafe.ReadUnaligned<uint>(ref b);

        /// <summary>Writes unaligned to the current output.</summary>
        /// <param name="b">The b used by this operation.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUnaligned(ref byte b, uint value) => Unsafe.WriteUnaligned(ref b, value);
    }
}
