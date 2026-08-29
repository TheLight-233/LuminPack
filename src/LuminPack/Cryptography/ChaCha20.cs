using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        /// the 64 output bytes little-endian to <paramref name="output"/>.
        /// </summary>
        public static unsafe void Block(uint* state, byte* output)
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