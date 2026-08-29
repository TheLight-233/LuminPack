using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace LuminPack.Cryptography
{
    /// <summary>
    /// ChaCha20-Poly1305 AEAD (RFC 8439) — authenticated encryption suitable for
    /// LuminPack binary payloads (save files, network payloads).
    /// <para>
    /// This component is isolated from the LuminPack serialization pipeline and lives in its
    /// own namespace. Its sync core is zero-allocation and runs in constant time (no
    /// data-dependent branches, constant-time tag comparison).
    /// </para>
    /// <para>
    /// A 12-byte random nonce must NEVER be reused with the same key. For high volume use a
    /// fresh key per session, or a monotonic counter-based nonce, and bind the nonce to the
    /// ciphertext (it is not authenticated).
    /// </para>
    /// </summary>
    public static class LuminChaCha20Poly1305
    {
        public const int KeySize = 32;        // 256-bit
        public const int KeySize128 = 16;     // 128-bit (ChaCha20-128)
        public const int NonceSize = 12;      // RFC 8439
        public const int TagSize = 16;        // 128-bit tag

        private const int BlockSize = 64;

        /// <summary>
        /// Encrypts <paramref name="plaintext"/> with Authenticated Encryption, producing
        /// <paramref name="ciphertext"/> (same length as plaintext) and a 16-byte
        /// <paramref name="tag"/>.
        /// </summary>
        public static unsafe void Encrypt(
            ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> aad,
            ReadOnlySpan<byte> plaintext,
            Span<byte> ciphertext,
            Span<byte> tag)
        {
            ValidateKey(key.Length);
            ValidateNonce(nonce.Length);
            EnsureLength(ciphertext.Length == plaintext.Length,
                "ciphertext must be the same length as plaintext.");
            EnsureLength(tag.Length >= TagSize, "tag buffer must be at least 16 bytes.");

            fixed (byte* keyPtr = &AsRef(key))
            fixed (byte* noncePtr = &AsRef(nonce))
            fixed (byte* ptPtr = &AsRef(plaintext))
            fixed (byte* ctPtr = &AsRef(ciphertext))
            {
                uint* state = stackalloc uint[16];
                byte* block = stackalloc byte[BlockSize];

                // Derive the one-time Poly1305 key from keystream block 0.
                ChaCha20.Initialize(state, keyPtr, key.Length, noncePtr, nonce.Length, 0);
                ChaCha20.Block(state, block);

                Poly1305 poly = default;
                poly.Initialize(block);

                // Authenticate the AAD (padded) first.
                poly.Update(aad);
                poly.PadTo16(aad.Length);

                // Encrypt using keystream counters 1, 2, 3, ...
                uint counter = 1;
                int length = plaintext.Length;
                int pos = 0;
                while (pos < length)
                {
                    int take = length - pos;
                    if (take > BlockSize)
                        take = BlockSize;

                    state[12] = counter++;
                    ChaCha20.Block(state, block);

                    for (int i = 0; i < take; i++)
                        ctPtr[pos + i] = (byte)(ptPtr[pos + i] ^ block[i]);

                    pos += take;
                }

                // Authenticate ciphertext, then the little-endian length fields.
                poly.Update(ciphertext);
                poly.PadTo16(ciphertext.Length);
                poly.UpdateLength((ulong)aad.Length);
                poly.UpdateLength((ulong)ciphertext.Length);

                poly.Finish(tag);
            }
        }

        /// <summary>
        /// Decrypts and authenticates. Returns <see langword="true"/> on success and writes
        /// plaintext into <paramref name="plaintext"/>; returns <see langword="false"/> if the
        /// tag does not match, in which case the plaintext buffer is left unmodified.
        /// </summary>
        public static unsafe bool Decrypt(
            ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> aad,
            ReadOnlySpan<byte> ciphertext,
            ReadOnlySpan<byte> tag,
            Span<byte> plaintext)
        {
            ValidateKey(key.Length);
            ValidateNonce(nonce.Length);
            EnsureLength(plaintext.Length == ciphertext.Length,
                "plaintext must be the same length as ciphertext.");
            EnsureLength(tag.Length >= TagSize, "tag must be at least 16 bytes.");

            // Always compute the MAC before decrypting so no plaintext-dependent branch runs.
            Span<byte> computed = stackalloc byte[TagSize];

            fixed (byte* keyPtr = &AsRef(key))
            fixed (byte* noncePtr = &AsRef(nonce))
            fixed (byte* ctPtr = &AsRef(ciphertext))
            {
                uint* state = stackalloc uint[16];
                byte* block = stackalloc byte[BlockSize];

                ChaCha20.Initialize(state, keyPtr, key.Length, noncePtr, nonce.Length, 0);
                ChaCha20.Block(state, block);

                Poly1305 poly = default;
                poly.Initialize(block);

                poly.Update(aad);
                poly.PadTo16(aad.Length);

                poly.Update(ciphertext);
                poly.PadTo16(ciphertext.Length);
                poly.UpdateLength((ulong)aad.Length);
                poly.UpdateLength((ulong)ciphertext.Length);

                poly.Finish(computed);
            }

            if (!ConstantTimeEquals(computed, tag))
                return false;

            // Tag verified — reveal the plaintext.
            fixed (byte* keyPtr = &AsRef(key))
            fixed (byte* noncePtr = &AsRef(nonce))
            fixed (byte* ctPtr = &AsRef(ciphertext))
            fixed (byte* ptPtr = &AsRef(plaintext))
            {
                uint* state = stackalloc uint[16];
                byte* block = stackalloc byte[BlockSize];

                ChaCha20.Initialize(state, keyPtr, key.Length, noncePtr, nonce.Length, 0);

                uint counter = 1;
                int length = ciphertext.Length;
                int pos = 0;
                while (pos < length)
                {
                    int take = length - pos;
                    if (take > BlockSize)
                        take = BlockSize;

                    state[12] = counter++;
                    ChaCha20.Block(state, block);

                    for (int i = 0; i < take; i++)
                        ptPtr[pos + i] = (byte)(ctPtr[pos + i] ^ block[i]);

                    pos += take;
                }
            }

            return true;
        }

        /// <summary>
        /// Async helper: reads the whole <paramref name="source"/> stream, encrypts it and
        /// writes <c>[ciphertext][16-byte tag]</c> to <paramref name="destination"/>.
        /// </summary>
        public static async ValueTask EncryptAsync(
            Stream source,
            Stream destination,
            ReadOnlyMemory<byte> key,
            ReadOnlyMemory<byte> nonce,
            ReadOnlyMemory<byte> aad,
            CancellationToken cancellationToken = default)
        {
            byte[] plaintext = await ReadAllAsync(source, cancellationToken).ConfigureAwait(false);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagSize];

            Encrypt(key.Span, nonce.Span, aad.Span, plaintext, ciphertext, tag);

            await destination.WriteAsync(ciphertext, cancellationToken).ConfigureAwait(false);
            await destination.WriteAsync(tag, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Async helper: reads <c>[ciphertext][16-byte tag]</c> from <paramref name="source"/>,
        /// authenticates &amp; decrypts it and writes plaintext to <paramref name="destination"/>.
        /// Returns <see langword="true"/> on success.
        /// </summary>
        public static async ValueTask<bool> DecryptAsync(
            Stream source,
            Stream destination,
            ReadOnlyMemory<byte> key,
            ReadOnlyMemory<byte> nonce,
            ReadOnlyMemory<byte> aad,
            CancellationToken cancellationToken = default)
        {
            byte[] sealedData = await ReadAllAsync(source, cancellationToken).ConfigureAwait(false);
            if (sealedData.Length < TagSize)
                return false;

            int cipherLength = sealedData.Length - TagSize;
            byte[] plaintext = new byte[cipherLength];

            bool ok = Decrypt(
                key.Span,
                nonce.Span,
                aad.Span,
                new ReadOnlySpan<byte>(sealedData, 0, cipherLength),
                new ReadOnlySpan<byte>(sealedData, cipherLength, TagSize),
                plaintext);

            if (!ok)
            {
                CryptographicOperationsZeroize(plaintext);
                return false;
            }

            await destination.WriteAsync(plaintext, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private static async ValueTask<byte[]> ReadAllAsync(Stream source, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                ms.Write(buffer, 0, read);
            return ms.ToArray();
        }

        private static void ValidateKey(int length)
        {
            EnsureLength(length == KeySize || length == KeySize128,
                "Key must be 16 or 32 bytes.");
        }

        private static void ValidateNonce(int length)
        {
            EnsureLength(length == NonceSize, "Nonce must be 12 bytes.");
        }

        private static void EnsureLength(bool condition, string message)
        {
            if (!condition)
                throw new ArgumentException(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ref byte AsRef(ReadOnlySpan<byte> span)
            => ref System.Runtime.InteropServices.MemoryMarshal.GetReference(span);

        /// <summary>Constant-time byte comparison (no early exit on mismatch).</summary>
        private static bool ConstantTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            uint diff = unchecked((uint)(left.Length ^ right.Length));
            int count = left.Length < right.Length ? left.Length : right.Length;
            for (int i = 0; i < count; i++)
                diff |= (uint)(left[i] ^ right[i]);
            return diff == 0;
        }

        /// <summary>Best-effort scrubbing of a temporary plaintext buffer on failed authentication.</summary>
        private static void CryptographicOperationsZeroize(byte[] data)
        {
            if (data != null)
            {
                Array.Clear(data, 0, data.Length);
            }
        }
    }
}