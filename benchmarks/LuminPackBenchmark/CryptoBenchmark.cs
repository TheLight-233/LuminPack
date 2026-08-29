using System;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack.Cryptography;

namespace LuminPackBenchmark
{
    /// <summary>
    /// ChaCha20-Poly1305 AEAD throughput and allocation, against the .NET BCL
    /// <see cref="ChaCha20Poly1305"/> as reference. Demonstrates the zero-allocation
    /// sync core of <see cref="LuminChaCha20Poly1305"/>.
    /// </summary>
    [HideColumns("StdDev", "RatioSD", "Error")]
    [MinColumn, MaxColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [ShortRunJob(RuntimeMoniker.Net10_0)]
    [MemoryDiagnoser]
    [GcServer]
    [MarkdownExporterAttribute.GitHub]
    public class CryptoBenchmark
    {
        private byte[] _key;
        private byte[] _nonce;
        private byte[] _aad;
        private byte[] _plain;
        private byte[] _cipher;
        private byte[] _recovered;
        private byte[] _tag;

        private ChaCha20Poly1305 _bcl;

        [Params(64, 1024, 65536, 1048576)]
        public int Size;

        [GlobalSetup]
        public void Setup()
        {
            var rng = new Random(1234);
            _key = new byte[LuminChaCha20Poly1305.KeySize];
            _nonce = new byte[LuminChaCha20Poly1305.NonceSize];
            _aad = new byte[16];
            rng.NextBytes(_key);
            rng.NextBytes(_nonce);
            rng.NextBytes(_aad);

            _plain = new byte[Size];
            rng.NextBytes(_plain);
            _cipher = new byte[Size];
            _recovered = new byte[Size];
            _tag = new byte[LuminChaCha20Poly1305.TagSize];

            _bcl = new ChaCha20Poly1305(_key);
            // Produce a valid sealed ciphertext so the Decrypt group runs on real data.
            _bcl.Encrypt(_nonce, _plain, _cipher, _tag, _aad);
        }

        [GlobalCleanup]
        public void Cleanup() => _bcl.Dispose();

        [Benchmark(Baseline = true), BenchmarkCategory("Encrypt")]
        public byte[] LuminPack_Encrypt()
        {
            LuminChaCha20Poly1305.Encrypt(_key, _nonce, _aad, _plain, _cipher, _tag);
            return _cipher;
        }

        [Benchmark, BenchmarkCategory("Encrypt")]
        public byte[] Bcl_Encrypt()
        {
            _bcl.Encrypt(_nonce, _plain, _cipher, _tag, _aad);
            return _cipher;
        }

        [Benchmark(Baseline = true), BenchmarkCategory("Decrypt")]
        public byte[] LuminPack_Decrypt()
        {
            LuminChaCha20Poly1305.Decrypt(_key, _nonce, _aad, _cipher, _tag, _recovered);
            return _recovered;
        }

        [Benchmark, BenchmarkCategory("Decrypt")]
        public byte[] Bcl_Decrypt()
        {
            _bcl.Decrypt(_nonce, _cipher, _tag, _recovered, _aad);
            return _recovered;
        }
    }
}