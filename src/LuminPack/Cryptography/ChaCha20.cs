using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
        {
            a += b; d ^= a; d = Rotl(d, 16);
            c += d; b ^= c; b = Rotl(b, 12);
            a += b; d ^= a; d = Rotl(d, 8);
            c += d; b ^= c; b = Rotl(b, 7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Rotl(uint x, int n) => (x << n) | (x >> (32 - n));

        /// <summary>
        /// Computes one 64-byte keystream block from the given 16-word state (in place), writing
        /// the 64 output bytes little-endian to <paramref name="output"/>. Dispatches to the
        /// fastest available implementation (SSE2 via <see cref="BlockSse2"/>, scalar fallback).
        /// </summary>
        public static unsafe void Block(uint* state, byte* output)
        {
#if NET8_0_OR_GREATER
            if (UseSse2)
            {
                BlockSse2(state, output);
                return;
            }
#endif
            BlockScalar(state, output);
        }

        /// <summary>
        /// Computes two consecutive 64-byte keystream blocks (counters <c>state[12]</c> and
        /// <c>state[12]+1</c>) in one call, writing 128 output bytes to <paramref name="output"/>.
        /// The input <paramref name="state"/> is never modified.
        /// </summary>
        public static unsafe void Block2(uint* state, byte* output)
        {
#if NET8_0_OR_GREATER
            if (UseAvx2)
            {
                BlockAvx2x2(state, output);
                return;
            }
#endif
            // Safe fallback (the AEAD fast path never calls Block2 without AVX2; kept for correctness).
            uint counter = state[12];
            Block(state, output);
            state[12] = counter + 1u;
            Block(state, output + 64);
            state[12] = counter;
        }

        /// <summary>
        /// Computes four consecutive 64-byte keystream blocks (counters <c>state[12]</c> through
        /// <c>state[12]+3</c>) in one call, writing 256 output bytes to <paramref name="output"/>.
        /// The input <paramref name="state"/> is never modified.
        /// </summary>
        public static unsafe void Block4(uint* state, byte* output)
        {
#if NET8_0_OR_GREATER
            if (UseAvx512)
            {
                BlockAvx512x4(state, output);
                return;
            }
#endif
            // Safe fallback (the AEAD fast path never calls Block4 without AVX-512; kept for correctness).
            uint counter = state[12];
            Block2(state, output);
            state[12] = counter + 2u;
            Block2(state, output + 128);
            state[12] = counter;
        }

        /// <summary>
        /// Computes one 64-byte keystream block from the given 16-word state (in place), writing
        /// the 64 output bytes little-endian to <paramref name="output"/>. Scalar 32-bit
        /// implementation (also used for <c>netstandard2.1</c>).
        /// </summary>
        public static unsafe void BlockScalar(uint* state, byte* output)
        {
            uint x0 = state[0], x1 = state[1], x2 = state[2], x3 = state[3];
            uint x4 = state[4], x5 = state[5], x6 = state[6], x7 = state[7];
            uint x8 = state[8], x9 = state[9], x10 = state[10], x11 = state[11];
            uint x12 = state[12], x13 = state[13], x14 = state[14], x15 = state[15];

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

            x0 += state[0]; x1 += state[1]; x2 += state[2]; x3 += state[3];
            x4 += state[4]; x5 += state[5]; x6 += state[6]; x7 += state[7];
            x8 += state[8]; x9 += state[9]; x10 += state[10]; x11 += state[11];
            x12 += state[12]; x13 += state[13]; x14 += state[14]; x15 += state[15];

            WriteUnaligned(ref output[0], x0);
            WriteUnaligned(ref output[4], x1);
            WriteUnaligned(ref output[8], x2);
            WriteUnaligned(ref output[12], x3);
            WriteUnaligned(ref output[16], x4);
            WriteUnaligned(ref output[20], x5);
            WriteUnaligned(ref output[24], x6);
            WriteUnaligned(ref output[28], x7);
            WriteUnaligned(ref output[32], x8);
            WriteUnaligned(ref output[36], x9);
            WriteUnaligned(ref output[40], x10);
            WriteUnaligned(ref output[44], x11);
            WriteUnaligned(ref output[48], x12);
            WriteUnaligned(ref output[52], x13);
            WriteUnaligned(ref output[56], x14);
            WriteUnaligned(ref output[60], x15);
        }

#if NET8_0_OR_GREATER
        private static readonly bool UseAvx512 = Avx512F.IsSupported;
        private static readonly bool UseAvx2 = Avx2.IsSupported;
        private static readonly bool UseSse2 = Sse2.IsSupported;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<uint> Rotl(Vector128<uint> v, byte n)
            => Sse2.Or(Sse2.ShiftLeftLogical(v, n), Sse2.ShiftRightLogical(v, (byte)(32 - n)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void QuarterRound(ref Vector128<uint> a, ref Vector128<uint> b, ref Vector128<uint> c, ref Vector128<uint> d)
        {
            a = Sse2.Add(a, b); d = Sse2.Xor(d, a); d = Rotl(d, 16);
            c = Sse2.Add(c, d); b = Sse2.Xor(b, c); b = Rotl(b, 12);
            a = Sse2.Add(a, b); d = Sse2.Xor(d, a); d = Rotl(d, 8);
            c = Sse2.Add(c, d); b = Sse2.Xor(b, c); b = Rotl(b, 7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> Rotl(Vector256<uint> v, byte n)
            => Avx2.Or(Avx2.ShiftLeftLogical(v, n), Avx2.ShiftRightLogical(v, (byte)(32 - n)));

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
        /// <see cref="Sse2.Shuffle"/> lane rotations (vb: 1, vc: 2, vd: 3).
        /// </summary>
        private static unsafe void BlockSse2(uint* state, byte* output)
        {
            Vector128<uint> va = Sse2.LoadVector128(state);        // x0..x3
            Vector128<uint> vb = Sse2.LoadVector128(state + 4);    // x4..x7
            Vector128<uint> vc = Sse2.LoadVector128(state + 8);    // x8..x11
            Vector128<uint> vd = Sse2.LoadVector128(state + 12);   // x12..x15
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

            Sse2.Store((uint*)output, va);
            Sse2.Store((uint*)(output + 16), vb);
            Sse2.Store((uint*)(output + 32), vc);
            Sse2.Store((uint*)(output + 48), vd);
        }

        /// <summary>
        /// Two-block 2x4-lane AVX2 implementation: the lower 128-bit half carries block A
        /// (counter <c>state[12]</c>), the upper half block B (counter <c>state[12]+1</c>),
        /// producing 128 output bytes. All <see cref="Avx2"/> operations are lane-wise or
        /// per-128-bit-half independent — the two halves never mix, so <c>state</c> is only
        /// read and never modified.
        /// </summary>
        private static unsafe void BlockAvx2x2(uint* state, byte* output)
        {
            Vector256<uint> va = Vector256.Create(state[0], state[1], state[2], state[3],
                                                  state[0], state[1], state[2], state[3]);
            Vector256<uint> vb = Vector256.Create(state[4], state[5], state[6], state[7],
                                                  state[4], state[5], state[6], state[7]);
            Vector256<uint> vc = Vector256.Create(state[8], state[9], state[10], state[11],
                                                  state[8], state[9], state[10], state[11]);
            Vector128<uint> vdLow = Vector128.Create(state[12], state[13], state[14], state[15]);
            Vector128<uint> vdHigh = Vector128.Create(state[12] + 1u, state[13], state[14], state[15]);
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

            Sse2.Store((uint*)output, va.GetLower());              // block A (counter c) -> [0..63]
            Sse2.Store((uint*)(output + 16), vb.GetLower());
            Sse2.Store((uint*)(output + 32), vc.GetLower());
            Sse2.Store((uint*)(output + 48), vd.GetLower());
            Sse2.Store((uint*)(output + 64), va.GetUpper());       // block B (counter c+1) -> [64..127]
            Sse2.Store((uint*)(output + 80), vb.GetUpper());
            Sse2.Store((uint*)(output + 96), vc.GetUpper());
            Sse2.Store((uint*)(output + 112), vd.GetUpper());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<uint> Rotl(Vector512<uint> v, byte n)
            => Avx512F.RotateLeft(v, n);

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
        /// one block (counters <c>state[12]</c> .. <c>state[12]+3</c>), producing 256 output
        /// bytes. Rotations use the single-instruction <c>vprold</c>. <c>Avx512F.Shuffle</c>
        /// (vpshufd) applies independently to each 128-bit quarter, so the lane rotation
        /// transposes work per block exactly like the SSE2/AVX2 versions. All operations are
        /// lane-wise — the four blocks never mix, and <c>state</c> is only read, never modified.
        /// </summary>
        private static unsafe void BlockAvx512x4(uint* state, byte* output)
        {
            Vector128<uint> v0 = Vector128.Create(state[0], state[1], state[2], state[3]);
            Vector128<uint> v4 = Vector128.Create(state[4], state[5], state[6], state[7]);
            Vector128<uint> v8 = Vector128.Create(state[8], state[9], state[10], state[11]);
            Vector128<uint> vd0 = Vector128.Create(state[12], state[13], state[14], state[15]);
            Vector128<uint> vd1 = Vector128.Create(state[12] + 1u, state[13], state[14], state[15]);
            Vector128<uint> vd2 = Vector128.Create(state[12] + 2u, state[13], state[14], state[15]);
            Vector128<uint> vd3 = Vector128.Create(state[12] + 3u, state[13], state[14], state[15]);

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
            Sse2.Store((uint*)output, va0);
            Sse2.Store((uint*)(output + 16), vb0);
            Sse2.Store((uint*)(output + 32), vc0);
            Sse2.Store((uint*)(output + 48), vd0r);
            // block 1 (counter c+1) -> [64..127]
            Sse2.Store((uint*)(output + 64), va1);
            Sse2.Store((uint*)(output + 80), vb1);
            Sse2.Store((uint*)(output + 96), vc1);
            Sse2.Store((uint*)(output + 112), vd1r);
            // block 2 (counter c+2) -> [128..191]
            Sse2.Store((uint*)(output + 128), va2);
            Sse2.Store((uint*)(output + 144), vb2);
            Sse2.Store((uint*)(output + 160), vc2);
            Sse2.Store((uint*)(output + 176), vd2r);
            // block 3 (counter c+3) -> [192..255]
            Sse2.Store((uint*)(output + 192), va3);
            Sse2.Store((uint*)(output + 208), vb3);
            Sse2.Store((uint*)(output + 224), vc3);
            Sse2.Store((uint*)(output + 240), vd3r);
        }
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
        public static unsafe void Initialize(uint* state, byte* key, int keyBytes, byte* nonce, int nonceBytes, uint blockCounter)
        {
            state[0] = C0; state[1] = C1; state[2] = C2; state[3] = C3;

            if (keyBytes == KeyBytes256)
            {
                // 256-bit key: words 4..11 = all 32 key bytes (two 16-byte halves).
                LoadKeyStore(words: state + 4, key: key, offsetBytes: 0);
                LoadKeyStore(words: state + 8, key: key, offsetBytes: 16);
            }
            else if (keyBytes == KeyBytes128)
            {
                // 128-bit key: words 4..7 = key, words 8..11 = key again.
                LoadKeyStore(words: state + 4, key: key, offsetBytes: 0);
                LoadKeyStore(words: state + 8, key: key, offsetBytes: 0);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(keyBytes), "ChaCha20 requires a 16 or 32 byte key.");
            }

            if (nonceBytes == 12)
            {
                state[12] = blockCounter;
                state[13] = ReadUnaligned(ref nonce[0]);
                state[14] = ReadUnaligned(ref nonce[4]);
                state[15] = ReadUnaligned(ref nonce[8]);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(nonceBytes), "ChaCha20 requires a 12 byte nonce.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void LoadKeyStore(uint* words, byte* key, int offsetBytes)
        {
            words[0] = ReadUnaligned(ref key[offsetBytes + 0]);
            words[1] = ReadUnaligned(ref key[offsetBytes + 4]);
            words[2] = ReadUnaligned(ref key[offsetBytes + 8]);
            words[3] = ReadUnaligned(ref key[offsetBytes + 12]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUnaligned(ref byte b) => Unsafe.ReadUnaligned<uint>(ref b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUnaligned(ref byte b, uint value) => Unsafe.WriteUnaligned(ref b, value);
    }
}