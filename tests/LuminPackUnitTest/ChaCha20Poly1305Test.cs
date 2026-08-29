using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using LuminPack.Cryptography;
using Xunit;

namespace LuminPackUnitTest
{
    /// <summary>
    /// ChaCha20-Poly1305 AEAD (RFC 8439) tests.
    /// <para>
    /// Correctness is established two ways: an exact RFC 8439 §2.3.2 ChaCha20 keystream vector,
    /// and a byte-for-byte cross-check of my whole AEAD (ciphertext + tag) against the .NET
    /// BCL <see cref="System.Security.Cryptography.ChaCha20Poly1305"/>. Additional tamper-rejection
    /// and round-trip tests round it out.
    /// </para>
    /// </summary>
    public class ChaCha20Poly1305Test
    {
        private static byte[] FromHex(string hex)
        {
            hex = hex.Replace(" ", "", StringComparison.Ordinal)
                     .Replace("\r", "", StringComparison.Ordinal)
                     .Replace("\n", "", StringComparison.Ordinal);
            var result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return result;
        }

        [Fact]
        public void ChaCha20_Keystream_Rfc8439_2_3_2()
        {
            // RFC 8439 §2.3.2: count=1, 96-bit nonce.
            byte[] key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            byte[] nonce = FromHex("000000090000004a00000000");
            const string expectedHex =
                "10f1e7e4d13b5915500fdd1fa32071c4" +
                "c7d1f4c733c068030422aa9ac3d46c4e" +
                "d2826446079faa0914c2d705d98b02a2" +
                "b5129cd1de164eb9cbd083e8a2503c4e";

            unsafe
            {
                uint* state = stackalloc uint[16];
                byte* block = stackalloc byte[64];
                fixed (byte* k = key)
                fixed (byte* n = nonce)
                {
                    LuminPack.Cryptography.ChaCha20.Initialize(state, k, key.Length, n, nonce.Length, 1u);
                    ChaCha20.Block(state, block);
                }
                var actual = new byte[64];
                System.Runtime.InteropServices.Marshal.Copy((IntPtr)block, actual, 0, 64);
                Assert.Equal(FromHex(expectedHex), actual);
            }
        }

        [Fact]
        public void CrossCheck_AgainstBcl_TwelveByteNonce()
        {
            var rng = new Random(2026);
            var key = new byte[LuminChaCha20Poly1305.KeySize];
            var nonce = new byte[LuminChaCha20Poly1305.NonceSize];
            byte[] aad = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var pt = new byte[257]; // non-16-multiple, exercises final partial Poly1305 block
            rng.NextBytes(key);
            rng.NextBytes(nonce);
            rng.NextBytes(pt);

            var myCt = new byte[pt.Length];
            var myTag = new byte[LuminChaCha20Poly1305.TagSize];
            LuminChaCha20Poly1305.Encrypt(key, nonce, aad, pt, myCt, myTag);

            var bclCt = new byte[pt.Length];
            var bclTag = new byte[LuminChaCha20Poly1305.TagSize];
            using var bcl = new System.Security.Cryptography.ChaCha20Poly1305(key);
            bcl.Encrypt(nonce, pt, bclCt, bclTag, aad);

            Assert.Equal(bclCt, myCt);
            Assert.Equal(bclTag, myTag);
        }

        [Fact]
        public void CrossCheck_AgainstBcl_EmptyPlaintext()
        {
            var rng = new Random(7);
            var key = new byte[LuminChaCha20Poly1305.KeySize];
            var nonce = new byte[LuminChaCha20Poly1305.NonceSize];
            rng.NextBytes(key);
            rng.NextBytes(nonce);
            byte[] aad = { 0x50, 0x51, 0x52, 0x53 };

            var myTag = new byte[LuminChaCha20Poly1305.TagSize];
            LuminChaCha20Poly1305.Encrypt(key, nonce, aad, Array.Empty<byte>(), Array.Empty<byte>(), myTag);

            var bclTag = new byte[LuminChaCha20Poly1305.TagSize];
            using var bcl = new System.Security.Cryptography.ChaCha20Poly1305(key);
            bcl.Encrypt(nonce, Array.Empty<byte>(), Array.Empty<byte>(), bclTag, aad);

            Assert.Equal(bclTag, myTag);
        }

        [Fact]
        public void CrossCheck_AgainstBcl_MultipleOf16()
        {
            var rng = new Random(99);
            var key = new byte[LuminChaCha20Poly1305.KeySize];
            var nonce = new byte[LuminChaCha20Poly1305.NonceSize];
            rng.NextBytes(key);
            rng.NextBytes(nonce);
            var pt = new byte[256]; // exactly 16 * 16: no leftover Poly1305 block
            rng.NextBytes(pt);

            var myCt = new byte[pt.Length];
            var myTag = new byte[LuminChaCha20Poly1305.TagSize];
            LuminChaCha20Poly1305.Encrypt(key, nonce, ReadOnlySpan<byte>.Empty, pt, myCt, myTag);

            var bclCt = new byte[pt.Length];
            var bclTag = new byte[LuminChaCha20Poly1305.TagSize];
            using var bcl = new System.Security.Cryptography.ChaCha20Poly1305(key);
            bcl.Encrypt(nonce, pt, bclCt, bclTag, Array.Empty<byte>());

            Assert.Equal(bclCt, myCt);
            Assert.Equal(bclTag, myTag);
        }

        [Fact]
        public void Rejects_TamperedTag()
        {
            byte[] key = new byte[LuminChaCha20Poly1305.KeySize];
            byte[] nonce = new byte[LuminChaCha20Poly1305.NonceSize];
            var rng = new Random(1);
            rng.NextBytes(key);
            rng.NextBytes(nonce);
            byte[] aad = { 9, 8, 7, 6 };
            byte[] pt = new byte[64];
            rng.NextBytes(pt);
            byte[] ct = new byte[pt.Length];
            byte[] tag = new byte[LuminChaCha20Poly1305.TagSize];
            LuminChaCha20Poly1305.Encrypt(key, nonce, aad, pt, ct, tag);

            var wrongTag = (byte[])tag.Clone();
            wrongTag[0] ^= 0xff;
            var recovered = new byte[pt.Length];
            Assert.False(LuminChaCha20Poly1305.Decrypt(key, nonce, aad, ct, wrongTag, recovered));
        }

        [Fact]
        public void Rejects_TamperedCiphertext()
        {
            byte[] key = new byte[LuminChaCha20Poly1305.KeySize];
            byte[] nonce = new byte[LuminChaCha20Poly1305.NonceSize];
            var rng = new Random(2);
            rng.NextBytes(key);
            rng.NextBytes(nonce);
            byte[] aad = { 1, 2, 3 };
            byte[] pt = new byte[80];
            rng.NextBytes(pt);
            byte[] ct = new byte[pt.Length];
            byte[] tag = new byte[LuminChaCha20Poly1305.TagSize];
            LuminChaCha20Poly1305.Encrypt(key, nonce, aad, pt, ct, tag);

            var tampered = (byte[])ct.Clone();
            tampered[5] ^= 0x01;
            var recovered = new byte[pt.Length];
            Assert.False(LuminChaCha20Poly1305.Decrypt(key, nonce, aad, tampered, tag, recovered));
        }

        [Fact]
        public void Rejects_WrongAad()
        {
            byte[] key = new byte[LuminChaCha20Poly1305.KeySize];
            byte[] nonce = new byte[LuminChaCha20Poly1305.NonceSize];
            var rng = new Random(3);
            rng.NextBytes(key);
            rng.NextBytes(nonce);
            byte[] pt = new byte[32];
            rng.NextBytes(pt);
            byte[] ct = new byte[pt.Length];
            byte[] tag = new byte[LuminChaCha20Poly1305.TagSize];
            LuminChaCha20Poly1305.Encrypt(key, nonce, new byte[] { 1, 2, 3 }, pt, ct, tag);

            var recovered = new byte[pt.Length];
            Assert.False(LuminChaCha20Poly1305.Decrypt(key, nonce, new byte[] { 1, 2, 3, 4 }, ct, tag, recovered));
        }

        [Fact]
        public void RoundTrip_TwelveByteNonce_RandomPayload()
        {
            var rng = new Random(42);
            var key = new byte[LuminChaCha20Poly1305.KeySize];
            var nonce = new byte[LuminChaCha20Poly1305.NonceSize];
            byte[] aad = { 1, 2, 3, 4, 5 };
            var pt = new byte[4097]; // deliberately non-16-multiple
            rng.NextBytes(key);
            rng.NextBytes(nonce);
            rng.NextBytes(pt);

            var ct = new byte[pt.Length];
            var tag = new byte[LuminChaCha20Poly1305.TagSize];
            LuminChaCha20Poly1305.Encrypt(key, nonce, aad, pt, ct, tag);

            var recovered = new byte[pt.Length];
            Assert.True(LuminChaCha20Poly1305.Decrypt(key, nonce, aad, ct, tag, recovered));
            Assert.Equal(pt, recovered);
        }

        [Fact]
        public async Task AsyncStream_EncryptDecrypt_RoundTrip()
        {
            var rng = new Random(7);
            var key = new byte[LuminChaCha20Poly1305.KeySize];
            var nonce = new byte[LuminChaCha20Poly1305.NonceSize];
            rng.NextBytes(key);
            rng.NextBytes(nonce);
            byte[] aad = { 9, 8, 7, 6 };
            var pt = new byte[100_000];
            rng.NextBytes(pt);

            using var src = new MemoryStream(pt);
            using var sealedStream = new MemoryStream();
            await LuminChaCha20Poly1305.EncryptAsync(src, sealedStream, key, nonce, aad);

            sealedStream.Position = 0;
            using var dest = new MemoryStream();
            bool ok = await LuminChaCha20Poly1305.DecryptAsync(sealedStream, dest, key, nonce, aad);

            Assert.True(ok);
            Assert.Equal(pt, dest.ToArray());
        }
    }
}