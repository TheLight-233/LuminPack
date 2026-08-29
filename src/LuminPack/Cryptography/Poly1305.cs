using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Cryptography
{
    /// <summary>
    /// Poly1305 one-time authenticator (RFC 8439 section 2.5).
    /// <para>
    /// Implemented with a 5 x 26-bit radix and 64-bit multiply-accumulate so it does
    /// NOT rely on <c>UInt128</c> (unavailable on <c>netstandard2.1</c>). All loops run a
    /// fixed number of iterations independent of input data, giving constant-time
    /// behavior with no data-dependent branches.
    /// </para>
    /// <para>This is an internal primitive used by <see cref="LuminChaCha20Poly1305"/>.</para>
    /// </summary>
    internal unsafe struct Poly1305
    {
        private uint _h0, _h1, _h2, _h3, _h4;
        private uint _r0, _r1, _r2, _r3, _r4;
        // s1..s4 = r*5 (precomputed for the mod 2^130-5 reduction during multiply)
        private uint _s1, _s2, _s3, _s4;
        // Second half of the 32-byte poly1305 key, added once at finalize.
        private ulong _keyLo, _keyHi;

        private fixed byte _leftover[16];
        private int _leftoverLength;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Load32(ref byte b) => Unsafe.ReadUnaligned<uint>(ref b);

        /// <summary>
        /// Initializes the authenticator with a 32-byte Poly1305 key
        /// (<c>r</c> = first 16 bytes, <c>s</c> = last 16 bytes).
        /// </summary>
        public void Initialize(byte* key)
        {
            _r0 = Load32(ref key[0]) & 0x3ffffff;
            _r1 = (Load32(ref key[3]) >> 2) & 0x3ffff03;
            _r2 = (Load32(ref key[6]) >> 4) & 0x3ffc0ff;
            _r3 = (Load32(ref key[9]) >> 6) & 0x3f03fff;
            _r4 = (Load32(ref key[12]) >> 8) & 0x00fffff;

            _s1 = _r1 * 5;
            _s2 = _r2 * 5;
            _s3 = _r3 * 5;
            _s4 = _r4 * 5;

            _keyLo = Unsafe.ReadUnaligned<ulong>(ref key[16]);
            _keyHi = Unsafe.ReadUnaligned<ulong>(ref key[24]);

            _h0 = _h1 = _h2 = _h3 = _h4 = 0;
            _leftoverLength = 0;
        }

        /// <summary>Processes a message segment (any length).</summary>
        public void Update(ReadOnlySpan<byte> data)
        {
            fixed (byte* m = &MemoryMarshal.GetReference(data))
            {
                Update(m, data.Length);
            }
        }

        /// <summary>Appends zero padding so the total length processed is a multiple of 16 (AEAD block padding).</summary>
        public void PadTo16(int length) => PadToMultipleOf16(length);

        private void PadToMultipleOf16(int length)
        {
            int rem = length & 15;
            if (rem == 0)
                return;

            Span<byte> zero = stackalloc byte[16 - rem];
            zero.Clear();
            Update(zero);
        }

        /// <summary>Processes an 8-byte little-endian length field (used by ChaCha20-Poly1305 AEAD).</summary>
        public void UpdateLength(ulong length)
        {
            Span<byte> tmp = stackalloc byte[8];
            Unsafe.WriteUnaligned(ref tmp[0], length);
            Update(tmp);
        }

        /// <summary>Finishes and writes the 16-byte tag using constant-time selection.</summary>
        public void Finish(Span<byte> tag)
        {
            // Flush any buffered partial block (1..15 bytes): embed 0x01 padding bit and
            // process WITHOUT the extra 2^128 term (the padding bit lives inside the block).
            if (_leftoverLength > 0)
            {
                fixed (byte* buf = _leftover)
                {
                    buf[_leftoverLength++] = 1;
                    // zero the remaining bytes
                    byte* end = buf + _leftoverLength;
                    byte* stop = buf + 16;
                    while (end < stop)
                        *end++ = 0;

                    Block(buf, 0);
                }
                _leftoverLength = 0;
            }
            else
            {
                // Message length was a multiple of 16: the last full block already carried
                // the 2^128 term. Nothing buffered to flush.
            }

            // Full carry.
            uint c;
            c = _h4 >> 26; _h4 &= 0x3ffffff; _h0 += c * 5;
            c = _h0 >> 26; _h0 &= 0x3ffffff; _h1 += c;
            c = _h1 >> 26; _h1 &= 0x3ffffff; _h2 += c;
            c = _h2 >> 26; _h2 &= 0x3ffffff; _h3 += c;
            c = _h3 >> 26; _h3 &= 0x3ffffff; _h4 += c;

            // Compute h + -p (i.e. h - (2^130-5)).
            uint g0 = _h0 + 5; c = g0 >> 26; g0 &= 0x3ffffff;
            uint g1 = _h1 + c; c = g1 >> 26; g1 &= 0x3ffffff;
            uint g2 = _h2 + c; c = g2 >> 26; g2 &= 0x3ffffff;
            uint g3 = _h3 + c; c = g3 >> 26; g3 &= 0x3ffffff;
            uint g4 = _h4 + c - (1u << 26);

            // Constant-time select: pick (h + -p) if it is < p, otherwise keep h.
            uint mask = (uint)((g4 >> 31) - 1); // 0 if g4 negative (h < p), 0xFFFFFFFF otherwise
            g0 &= mask; g1 &= mask; g2 &= mask; g3 &= mask; g4 &= mask;
            mask = ~mask;
            _h0 = (_h0 & mask) | g0;
            _h1 = (_h1 & mask) | g1;
            _h2 = (_h2 & mask) | g2;
            _h3 = (_h3 & mask) | g3;
            _h4 = (_h4 & mask) | g4;

            // Pack into a 128-bit value (bits 0..127).
            ulong low = (ulong)_h0
                      | ((ulong)_h1 << 26)
                      | ((ulong)(_h2 & 0xFFF) << 52);
            ulong high = (ulong)(_h2 >> 12)
                       | ((ulong)_h3 << 14)
                       | ((ulong)(_h4 & 0xFFFFFF) << 40);

            // Add the second half of the key, modulo 2^128.
            low += _keyLo;
            high += _keyHi + (ulong)(low < _keyLo ? 1 : 0);

            Unsafe.WriteUnaligned(ref tag[0], low);
            Unsafe.WriteUnaligned(ref tag[8], high);
        }

        private void Update(byte* m, int length)
        {
            // Drain any buffered partial block first.
            if (_leftoverLength > 0)
            {
                int take = 16 - _leftoverLength;
                if (length < take)
                    take = length;

                fixed (byte* buf = _leftover)
                {
                    for (int i = 0; i < take; i++)
                        buf[_leftoverLength + i] = m[i];

                    _leftoverLength += take;
                }

                length -= take;
                m += take;

                if (_leftoverLength == 16)
                {
                    fixed (byte* buf = _leftover)
                    {
                        Block(buf, 1u << 24);
                    }
                    _leftoverLength = 0;
                }
                else
                {
                    return; // still buffering
                }
            }

            // Process full 16-byte blocks.
            while (length >= 16)
            {
                Block(m, 1u << 24);
                m += 16;
                length -= 16;
            }

            // Buffer the remaining 1..15 bytes.
            if (length > 0)
            {
                fixed (byte* buf = _leftover)
                {
                    for (int i = 0; i < length; i++)
                        buf[i] = m[i];
                }
                _leftoverLength = length;
            }
        }

        /// <summary>
        /// Processes one 16-byte block.
        /// </summary>
        /// <param name="m">16-byte block.</param>
        /// <param name="hibit">Extra high bit: <c>1&lt;&lt;24</c> for blocks carrying the 2^128 term,
        /// <c>0</c> when the 0x01 padding already lives inside the block buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Block(byte* m, uint hibit)
        {
            uint t0 = Load32(ref m[0]) & 0x3ffffff;
            uint t1 = (Load32(ref m[3]) >> 2) & 0x3ffffff;
            uint t2 = (Load32(ref m[6]) >> 4) & 0x3ffffff;
            uint t3 = (Load32(ref m[9]) >> 6) & 0x3ffffff;
            uint t4 = (Load32(ref m[12]) >> 8) & 0x3ffffff;

            _h0 += t0; _h1 += t1; _h2 += t2; _h3 += t3; _h4 += t4;
            _h4 += hibit;

            // acc = acc * r (mod 2^130 - 5), using r*5 accumulated terms for the reduction.
            ulong d0 = (ulong)_h0 * _r0 + (ulong)_h1 * _s4 + (ulong)_h2 * _s3 + (ulong)_h3 * _s2 + (ulong)_h4 * _s1;
            ulong d1 = (ulong)_h0 * _r1 + (ulong)_h1 * _r0 + (ulong)_h2 * _s4 + (ulong)_h3 * _s3 + (ulong)_h4 * _s2;
            ulong d2 = (ulong)_h0 * _r2 + (ulong)_h1 * _r1 + (ulong)_h2 * _r0 + (ulong)_h3 * _s4 + (ulong)_h4 * _s3;
            ulong d3 = (ulong)_h0 * _r3 + (ulong)_h1 * _r2 + (ulong)_h2 * _r1 + (ulong)_h3 * _r0 + (ulong)_h4 * _s4;
            ulong d4 = (ulong)_h0 * _r4 + (ulong)_h1 * _r3 + (ulong)_h2 * _r2 + (ulong)_h3 * _r1 + (ulong)_h4 * _r0;

            ulong cy;
            cy = d0 >> 26; _h0 = (uint)(d0 & 0x3ffffff); d1 += cy;
            cy = d1 >> 26; _h1 = (uint)(d1 & 0x3ffffff); d2 += cy;
            cy = d2 >> 26; _h2 = (uint)(d2 & 0x3ffffff); d3 += cy;
            cy = d3 >> 26; _h3 = (uint)(d3 & 0x3ffffff); d4 += cy;
            cy = d4 >> 26; _h4 = (uint)(d4 & 0x3ffffff);

            // Final reduction: the carry out of bit 130 is worth 5 at bit 0.
            _h0 += (uint)(cy * 5);
            cy = _h0 >> 26; _h0 &= 0x3ffffff; _h1 += (uint)cy;
        }
    }
}