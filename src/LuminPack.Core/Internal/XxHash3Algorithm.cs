// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.
// Adapted from dotnet/runtime v10.0.0 System.IO.Hashing XxHash3.cs and XxHashShared.cs.
// Based on xxHash by Yann Collet. See docs/xxhash-LICENSE.txt for both upstream licenses.
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if !NETSTANDARD2_0
using System.Buffers.Binary;
using LuminPack.Code;
#endif
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif

namespace LuminPack.Internal;

/// <summary>Shares the seed-zero XXH3 algorithm between Core and both source generators.</summary>
/// <remarks>Names up to 240 bytes use constants and scalar mixing. Long inputs use the upstream
/// 256/128-bit kernels on supported 64-bit runtimes, including AVX-512 hosts through AVX2;
/// generators, 32-bit and portable runtimes retain the identical scalar algorithm.</remarks>
internal static class XxHash3Algorithm
{
    private const ulong DefaultSecretUInt64_0 = 0xBE4BA423396CFEB8;
    private const ulong DefaultSecretUInt64_1 = 0x1CAD21F72C81017C;
    private const ulong DefaultSecretUInt64_2 = 0xDB979083E96DD4DE;
    private const ulong DefaultSecretUInt64_3 = 0x1F67B3B7A4A44072;
    private const ulong DefaultSecretUInt64_4 = 0x78E5C0CC4EE679CB;
    private const ulong DefaultSecretUInt64_5 = 0x2172FFCC7DD05A82;
    private const ulong DefaultSecretUInt64_6 = 0x8E2443F7744608B8;
    private const ulong DefaultSecretUInt64_7 = 0x4C263A81E69035E0;
    private const ulong DefaultSecretUInt64_8 = 0xCB00C391BB52283C;
    private const ulong DefaultSecretUInt64_9 = 0xA32E531B8B65D088;
    private const ulong DefaultSecretUInt64_10 = 0x4EF90DA297486471;
    private const ulong DefaultSecretUInt64_11 = 0xD8ACDEA946EF1938;
    private const ulong DefaultSecretUInt64_12 = 0x3F349CE33F76FAA8;
    private const ulong DefaultSecretUInt64_13 = 0x1D4F0BC7C7BBDCF9;
    private const ulong DefaultSecretUInt64_14 = 0x3159B4CD4BE0518A;
    private const ulong DefaultSecretUInt64_15 = 0x647378D9C97E9FC8;

    // Cast of DefaultSecret offset by 3 bytes, byte[] => ulong[]
    private const ulong DefaultSecret3UInt64_0 = 0x81017CBE4BA42339;
    private const ulong DefaultSecret3UInt64_1 = 0x6DD4DE1CAD21F72C;
    private const ulong DefaultSecret3UInt64_2 = 0xA44072DB979083E9;
    private const ulong DefaultSecret3UInt64_3 = 0xE679CB1F67B3B7A4;
    private const ulong DefaultSecret3UInt64_4 = 0xD05A8278E5C0CC4E;
    private const ulong DefaultSecret3UInt64_5 = 0x4608B82172FFCC7D;
    private const ulong DefaultSecret3UInt64_6 = 0x9035E08E2443F774;
    private const ulong DefaultSecret3UInt64_7 = 0x52283C4C263A81E6;
    private const ulong DefaultSecret3UInt64_8 = 0x65D088CB00C391BB;
    private const ulong DefaultSecret3UInt64_9 = 0x486471A32E531B8B;
    private const ulong DefaultSecret3UInt64_10 = 0xEF19384EF90DA297;
    private const ulong DefaultSecret3UInt64_11 = 0x76FAA8D8ACDEA946;
    private const ulong DefaultSecret3UInt64_12 = 0xBBDCF93F349CE33F;
    private const ulong DefaultSecret3UInt64_13 = 0xE0518A1D4F0BC7C7;

    private const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
    private const ulong Prime64_2 = 0xC2B2AE3D27D4EB4FUL;
    private const ulong Prime64_3 = 0x165667B19E3779F9UL;
    private const ulong Prime64_4 = 0x85EBCA77C2B2AE63UL;
    private const ulong Prime64_5 = 0x27D4EB2F165667C5UL;

    private const uint Prime32_1 = 0x9E3779B1U;
    private const uint Prime32_2 = 0x85EBCA77U;
    private const uint Prime32_3 = 0xC2B2AE3DU;

    private const int StripeLengthBytes = 64;
    private const int SecretLengthBytes = 192;
    private const int SecretConsumeRateBytes = 8;
    private const int SecretLastAccStartBytes = 7;
    private const int AccumulatorCount = 8;

    /// <summary>Hashes exactly the validated input range with the standard seed-zero XXH3 algorithm.</summary>
    /// <param name="source">The first readable byte; a null reference is valid only for zero length.</param>
    /// <param name="length">The byte count in the validated range.</param>
    /// <returns>The standard XXH3 64-bit value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Compute(ref byte source, uint length)
    {
        if (length <= 16)
            return HashLength0To16(ref source, length);
        if (length <= 128)
            return HashLength17To128(ref source, length);
        if (length <= 240)
            return HashLength129To240(ref source, length);
        return HashLengthOver240(ref source, length);
    }

    /// <summary>Selects the bounded short-name mixer.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="length">The input byte count.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashLength0To16(ref byte source, uint length)
    {
        if (length > 8)
        {
            return HashLength9To16(ref source, length);
        }

        if (length >= 4)
        {
            return HashLength4To8(ref source, length);
        }

        if (length != 0)
        {
            return HashLength1To3(ref source, length);
        }

        const ulong SecretXor = DefaultSecretUInt64_7 ^ DefaultSecretUInt64_8;
        return Avalanche64(SecretXor);
    }

    /// <summary>Mixes all bytes of a one-to-three-byte name.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="length">The input byte count.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashLength1To3(ref byte source, uint length)
    {
        Debug.Assert(length >= 1 && length <= 3);

        // When source.Length == 1, c1 == Unsafe.Add(ref source, 0), c2 == Unsafe.Add(ref source, 0), c3 == Unsafe.Add(ref source, 0)
        // When source.Length == 2, c1 == Unsafe.Add(ref source, 0), c2 == Unsafe.Add(ref source, 1), c3 == Unsafe.Add(ref source, 1)
        // When source.Length == 3, c1 == Unsafe.Add(ref source, 0), c2 == Unsafe.Add(ref source, 1), c3 == Unsafe.Add(ref source, 2)
        byte c1 = source;
        byte c2 = Unsafe.Add(ref source, (nint)(length >> 1));
        byte c3 = Unsafe.Add(ref source, (nint)(length - 1));

        uint combined = ((uint)c1 << 16) | ((uint)c2 << 24) | c3 | (length << 8);

        const uint SecretXor = unchecked((uint)DefaultSecretUInt64_0) ^ (uint)(DefaultSecretUInt64_0 >> 32);
        return Avalanche64(combined ^ SecretXor);
    }

    /// <summary>Mixes the first and last words with the exact length.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="length">The input byte count.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashLength4To8(ref byte source, uint length)
    {
        Debug.Assert(length >= 4 && length <= 8);

        uint inputLow = ReadUInt32LE(ref source);
        uint inputHigh = ReadUInt32LE(ref Unsafe.Add(ref source, (nint)(length - sizeof(uint))));

        const ulong SecretXor = DefaultSecretUInt64_1 ^ DefaultSecretUInt64_2;
        ulong bitflip = SecretXor;
        ulong input64 = inputHigh + (((ulong)inputLow) << 32);

        return Rrmxmx(input64 ^ bitflip, length);
    }

    /// <summary>Folds the first and last words through a full-width product.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="length">The input byte count.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashLength9To16(ref byte source, uint length)
    {
        Debug.Assert(length >= 9 && length <= 16);

        const ulong SecretXorL = DefaultSecretUInt64_3 ^ DefaultSecretUInt64_4;
        const ulong SecretXorR = DefaultSecretUInt64_5 ^ DefaultSecretUInt64_6;
        ulong bitflipLow = SecretXorL;
        ulong bitflipHigh = SecretXorR;

        ulong inputLow = ReadUInt64LE(ref source) ^ bitflipLow;
        ulong inputHigh = ReadUInt64LE(ref Unsafe.Add(ref source, (nint)(length - sizeof(ulong)))) ^ bitflipHigh;

        return Avalanche(
            length +
            ReverseBytes(inputLow) +
            inputHigh +
            Multiply64To128ThenFold(inputLow, inputHigh));
    }

    /// <summary>Mixes distinct positions with compile-time secret constants.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="length">The input byte count.</param>
    /// <returns>The computed value.</returns>
    private static ulong HashLength17To128(ref byte source, uint length)
    {
        Debug.Assert(length >= 17 && length <= 128);

        ulong hash = length * Prime64_1;

        switch ((length - 1) / 32)
        {
            default: // case 3
                hash += Mix16Bytes(ref Unsafe.Add(ref source, 48), DefaultSecretUInt64_12, DefaultSecretUInt64_13);
                hash += Mix16Bytes(ref Unsafe.Add(ref source, (nint)(length - 64)), DefaultSecretUInt64_14, DefaultSecretUInt64_15);
                goto case 2;
            case 2:
                hash += Mix16Bytes(ref Unsafe.Add(ref source, 32), DefaultSecretUInt64_8, DefaultSecretUInt64_9);
                hash += Mix16Bytes(ref Unsafe.Add(ref source, (nint)(length - 48)), DefaultSecretUInt64_10, DefaultSecretUInt64_11);
                goto case 1;
            case 1:
                hash += Mix16Bytes(ref Unsafe.Add(ref source, 16), DefaultSecretUInt64_4, DefaultSecretUInt64_5);
                hash += Mix16Bytes(ref Unsafe.Add(ref source, (nint)(length - 32)), DefaultSecretUInt64_6, DefaultSecretUInt64_7);
                goto case 0;
            case 0:
                hash += Mix16Bytes(ref source, DefaultSecretUInt64_0, DefaultSecretUInt64_1);
                hash += Mix16Bytes(ref Unsafe.Add(ref source, (nint)(length - 16)), DefaultSecretUInt64_2, DefaultSecretUInt64_3);
                break;
        }

        return Avalanche(hash);
    }

    /// <summary>Mixes the bounded medium input with the standard overlapping final block.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="length">The input byte count.</param>
    /// <returns>The computed value.</returns>
    private static ulong HashLength129To240(ref byte source, uint length)
    {
        Debug.Assert(length >= 129 && length <= 240);

        ulong hash = length * Prime64_1;

        hash += Mix16Bytes(ref Unsafe.Add(ref source, 0), DefaultSecretUInt64_0, DefaultSecretUInt64_1);
        hash += Mix16Bytes(ref Unsafe.Add(ref source, 16), DefaultSecretUInt64_2, DefaultSecretUInt64_3);
        hash += Mix16Bytes(ref Unsafe.Add(ref source, 32), DefaultSecretUInt64_4, DefaultSecretUInt64_5);
        hash += Mix16Bytes(ref Unsafe.Add(ref source, 48), DefaultSecretUInt64_6, DefaultSecretUInt64_7);
        hash += Mix16Bytes(ref Unsafe.Add(ref source, 64), DefaultSecretUInt64_8, DefaultSecretUInt64_9);
        hash += Mix16Bytes(ref Unsafe.Add(ref source, 80), DefaultSecretUInt64_10, DefaultSecretUInt64_11);
        hash += Mix16Bytes(ref Unsafe.Add(ref source, 96), DefaultSecretUInt64_12, DefaultSecretUInt64_13);
        hash += Mix16Bytes(ref Unsafe.Add(ref source, 112), DefaultSecretUInt64_14, DefaultSecretUInt64_15);

        hash = Avalanche(hash);

        switch ((length - (16 * 8)) / 16)
        {
            default: // case 7
                Debug.Assert((length - 16 * 8) / 16 == 7);
                hash += Mix16Bytes(ref Unsafe.Add(ref source, 224), DefaultSecret3UInt64_12, DefaultSecret3UInt64_13);
                goto case 6;
            case 6:
                hash += Mix16Bytes(ref Unsafe.Add(ref source, 208), DefaultSecret3UInt64_10, DefaultSecret3UInt64_11);
                goto case 5;
            case 5:
                hash += Mix16Bytes(ref Unsafe.Add(ref source, 192), DefaultSecret3UInt64_8, DefaultSecret3UInt64_9);
                goto case 4;
            case 4:
                hash += Mix16Bytes(ref Unsafe.Add(ref source, 176), DefaultSecret3UInt64_6, DefaultSecret3UInt64_7);
                goto case 3;
            case 3:
                hash += Mix16Bytes(ref Unsafe.Add(ref source, 160), DefaultSecret3UInt64_4, DefaultSecret3UInt64_5);
                goto case 2;
            case 2:
                hash += Mix16Bytes(ref Unsafe.Add(ref source, 144), DefaultSecret3UInt64_2, DefaultSecret3UInt64_3);
                goto case 1;
            case 1:
                hash += Mix16Bytes(ref Unsafe.Add(ref source, 128), DefaultSecret3UInt64_0, DefaultSecret3UInt64_1);
                goto case 0;
            case 0:
                hash += Mix16Bytes(ref Unsafe.Add(ref source, (nint)(length - 16)), 0x7378D9C97E9FC831, 0xEBD33483ACC5EA64); // DefaultSecret[119], DefaultSecret[127]
                break;
        }

        return Avalanche(hash);
    }

    /// <summary>Hashes long names without changing the short-name code path.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="length">The input byte count.</param>
    /// <returns>The computed value.</returns>
    private static ulong HashLengthOver240(ref byte source, uint length)
    {
        ref byte secret = ref LongSecret.Bytes[0];
        Span<ulong> accumulators = stackalloc ulong[AccumulatorCount];
        InitializeAccumulators(accumulators);
        HashInternalLoop(accumulators, ref source, length, ref secret);
        return MergeAccumulators(accumulators, ref Unsafe.Add(ref secret, 11), length * Prime64_1);
    }

    /// <summary>Avalanches an unmixed short input.</summary>
    /// <param name="hash">The intermediate hash.</param>
    /// <param name="length">The input byte count.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Rrmxmx(ulong hash, uint length)
    {
        hash ^= ((hash << 49) | (hash >> 15)) ^ ((hash << 24) | (hash >> 40));
        hash *= 0x9FB21C651E98DF25;
        hash ^= (hash >> 35) + length;
        hash *= 0x9FB21C651E98DF25;
        return XorShift(hash, 28);
    }

    /// <summary>Consumes full blocks and the bounded overlapping final stripe.</summary>
    /// <param name="accumulators">The eight initialized lanes.</param>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="length">The input byte count.</param>
    /// <param name="secret">The validated secret range.</param>
    private static void HashInternalLoop(Span<ulong> accumulators, ref byte source, uint length, ref byte secret)
    {
        Debug.Assert(length > 240);

        const int StripesPerBlock = (SecretLengthBytes - StripeLengthBytes) / SecretConsumeRateBytes;
        const int BlockLen = StripeLengthBytes * StripesPerBlock;
        int blocksNum = (int)((length - 1) / BlockLen);

        Accumulate(accumulators, ref source, ref secret, StripesPerBlock, true, blocksNum);
        uint offset = (uint)BlockLen * (uint)blocksNum;

        int stripesNumber = (int)((length - 1 - offset) / StripeLengthBytes);
        Accumulate(accumulators, ref Unsafe.Add(ref source, (nint)(offset)), ref secret, stripesNumber);
        Accumulate512(accumulators, ref Unsafe.Add(ref source, (nint)(length - StripeLengthBytes)), ref Unsafe.Add(ref secret, (nint)((SecretLengthBytes - StripeLengthBytes - SecretLastAccStartBytes))));
    }

    /// <summary>Folds the eight lanes into the final hash.</summary>
    /// <param name="accumulators">The eight initialized lanes.</param>
    /// <param name="secret">The validated secret range.</param>
    /// <param name="start">The length-derived initial value.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MergeAccumulators(Span<ulong> accumulators, ref byte secret, ulong start)
    {
        ulong result64 = start;

        result64 += Multiply64To128ThenFold(accumulators[0] ^ ReadUInt64LE(ref secret), accumulators[1] ^ ReadUInt64LE(ref Unsafe.Add(ref secret, 8)));
        result64 += Multiply64To128ThenFold(accumulators[2] ^ ReadUInt64LE(ref Unsafe.Add(ref secret, 16)), accumulators[3] ^ ReadUInt64LE(ref Unsafe.Add(ref secret, 24)));
        result64 += Multiply64To128ThenFold(accumulators[4] ^ ReadUInt64LE(ref Unsafe.Add(ref secret, 32)), accumulators[5] ^ ReadUInt64LE(ref Unsafe.Add(ref secret, 40)));
        result64 += Multiply64To128ThenFold(accumulators[6] ^ ReadUInt64LE(ref Unsafe.Add(ref secret, 48)), accumulators[7] ^ ReadUInt64LE(ref Unsafe.Add(ref secret, 56)));

        return Avalanche(result64);
    }

    /// <summary>Avalanches an already mixed input.</summary>
    /// <param name="hash">The intermediate hash.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Avalanche(ulong hash)
    {
        hash = XorShift(hash, 37);
        hash *= 0x165667919E3779F9;
        hash = XorShift(hash, 32);
        return hash;
    }

    /// <summary>Computes both halves of a full-width unsigned product.</summary>
    /// <param name="left">The left factor.</param>
    /// <param name="right">The right factor.</param>
    /// <param name="lower">Receives the low half of the product.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Multiply64To128(ulong left, ulong right, out ulong lower)
    {
    #if NET8_0_OR_GREATER
        return Math.BigMul(left, right, out lower);
    #else
        ulong lowerLow = Multiply32To64((uint)left, (uint)right);
        ulong higherLow = Multiply32To64((uint)(left >> 32), (uint)right);
        ulong lowerHigh = Multiply32To64((uint)left, (uint)(right >> 32));
        ulong higherHigh = Multiply32To64((uint)(left >> 32), (uint)(right >> 32));

        ulong cross = (lowerLow >> 32) + (higherLow & 0xFFFFFFFF) + lowerHigh;
        ulong upper = (higherLow >> 32) + (cross >> 32) + higherHigh;
        lower = (cross << 32) | (lowerLow & 0xFFFFFFFF);
        return upper;
    #endif
    }

    /// <summary>XORs the high and low halves of the unsigned product.</summary>
    /// <param name="left">The left factor.</param>
    /// <param name="right">The right factor.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Multiply64To128ThenFold(ulong left, ulong right)
    {
        ulong upper = Multiply64To128(left, right, out ulong lower);
        return lower ^ upper;
    }

    /// <summary>Initializes all eight scalar lanes.</summary>
    /// <param name="accumulators">The eight initialized lanes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InitializeAccumulators(Span<ulong> accumulators)
    {
        accumulators[0] = Prime32_3;
        accumulators[1] = Prime64_1;
        accumulators[2] = Prime64_2;
        accumulators[3] = Prime64_3;
        accumulators[4] = Prime64_4;
        accumulators[5] = Prime32_2;
        accumulators[6] = Prime64_5;
        accumulators[7] = Prime32_1;
    }

    /// <summary>Keeps vector accumulators in registers across complete stripes.</summary>
    /// <param name="accumulators">The eight initialized lanes.</param>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="secret">The validated secret range.</param>
    /// <param name="stripesToProcess">The complete stripe count per block.</param>
    /// <param name="scramble">Whether to scramble after each block.</param>
    /// <param name="blockCount">The number of complete blocks.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Accumulate(Span<ulong> accumulators, ref byte source, ref byte secret, int stripesToProcess, bool scramble = false, int blockCount = 1)
    {
        ref byte secretForAccumulate = ref secret;
        ref byte secretForScramble = ref Unsafe.Add(ref secret, SecretLengthBytes - StripeLengthBytes);

    #if NET8_0_OR_GREATER
        if (IntPtr.Size == 8 && Avx2.IsSupported && BitConverter.IsLittleEndian)
        {
            Vector256<ulong> acc1 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(accumulators));
            Vector256<ulong> acc2 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(accumulators), (nuint)(Vector256<ulong>.Count));

            for (int j = 0; j < blockCount; j++)
            {
                secret = ref secretForAccumulate;
                for (int i = 0; i < stripesToProcess; i++)
                {
                    Vector256<uint> secretVal = Vector256.LoadUnsafe(ref Unsafe.As<byte, uint>(ref secret));
                    acc1 = Accumulate256(acc1, ref source, secretVal);
                    source = ref Unsafe.Add(ref source, Vector256<byte>.Count);

                    secretVal = Vector256.LoadUnsafe(ref Unsafe.As<byte, uint>(ref secret), (nuint)(Vector256<uint>.Count));
                    acc2 = Accumulate256(acc2, ref source, secretVal);
                    source = ref Unsafe.Add(ref source, Vector256<byte>.Count);

                    secret = ref Unsafe.Add(ref secret, SecretConsumeRateBytes);
                }

                if (scramble)
                {
                    acc1 = ScrambleAccumulator256(acc1, Vector256.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref secretForScramble)));
                    acc2 = ScrambleAccumulator256(acc2, Vector256.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref secretForScramble), (nuint)(Vector256<ulong>.Count)));
                }
            }

            Vector256.StoreUnsafe(acc1, ref MemoryMarshal.GetReference(accumulators));
            Vector256.StoreUnsafe(acc2, ref MemoryMarshal.GetReference(accumulators), (nuint)(Vector256<ulong>.Count));
        }
        else if (IntPtr.Size == 8 && (Sse2.IsSupported || AdvSimd.Arm64.IsSupported) && BitConverter.IsLittleEndian)
        {
            Vector128<ulong> acc1 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(accumulators));
            Vector128<ulong> acc2 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(accumulators), (nuint)(Vector128<ulong>.Count));
            Vector128<ulong> acc3 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(accumulators), (nuint)(Vector128<ulong>.Count * 2));
            Vector128<ulong> acc4 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(accumulators), (nuint)(Vector128<ulong>.Count * 3));

            for (int j = 0; j < blockCount; j++)
            {
                secret = ref secretForAccumulate;
                for (int i = 0; i < stripesToProcess; i++)
                {
                    Vector128<uint> secretVal = Vector128.LoadUnsafe(ref Unsafe.As<byte, uint>(ref secret));
                    acc1 = Accumulate128(acc1, ref source, secretVal);
                    source = ref Unsafe.Add(ref source, Vector128<byte>.Count);

                    secretVal = Vector128.LoadUnsafe(ref Unsafe.As<byte, uint>(ref secret), (nuint)(Vector128<uint>.Count));
                    acc2 = Accumulate128(acc2, ref source, secretVal);
                    source = ref Unsafe.Add(ref source, Vector128<byte>.Count);

                    secretVal = Vector128.LoadUnsafe(ref Unsafe.As<byte, uint>(ref secret), (nuint)(Vector128<uint>.Count * 2));
                    acc3 = Accumulate128(acc3, ref source, secretVal);
                    source = ref Unsafe.Add(ref source, Vector128<byte>.Count);

                    secretVal = Vector128.LoadUnsafe(ref Unsafe.As<byte, uint>(ref secret), (nuint)(Vector128<uint>.Count * 3));
                    acc4 = Accumulate128(acc4, ref source, secretVal);
                    source = ref Unsafe.Add(ref source, Vector128<byte>.Count);

                    secret = ref Unsafe.Add(ref secret, SecretConsumeRateBytes);
                }

                if (scramble)
                {
                    acc1 = ScrambleAccumulator128(acc1, Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref secretForScramble)));
                    acc2 = ScrambleAccumulator128(acc2, Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref secretForScramble), (nuint)(Vector128<ulong>.Count)));
                    acc3 = ScrambleAccumulator128(acc3, Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref secretForScramble), (nuint)(Vector128<ulong>.Count * 2)));
                    acc4 = ScrambleAccumulator128(acc4, Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref secretForScramble), (nuint)(Vector128<ulong>.Count * 3)));
                }
            }

            Vector128.StoreUnsafe(acc1, ref MemoryMarshal.GetReference(accumulators));
            Vector128.StoreUnsafe(acc2, ref MemoryMarshal.GetReference(accumulators), (nuint)(Vector128<ulong>.Count));
            Vector128.StoreUnsafe(acc3, ref MemoryMarshal.GetReference(accumulators), (nuint)(Vector128<ulong>.Count * 2));
            Vector128.StoreUnsafe(acc4, ref MemoryMarshal.GetReference(accumulators), (nuint)(Vector128<ulong>.Count * 3));
        }
        else
    #endif
        {
            for (int j = 0; j < blockCount; j++)
            {
                for (int i = 0; i < stripesToProcess; i++)
                {
                    Accumulate512Inlined(accumulators, ref source, ref Unsafe.Add(ref secret, i * SecretConsumeRateBytes));
                    source = ref Unsafe.Add(ref source, StripeLengthBytes);
                }

                if (scramble)
                {
                    ScrambleAccumulators(accumulators, ref secretForScramble);
                }
            }
        }
    }

    /// <summary>Consumes one 64-byte stripe, independent of SIMD register width.</summary>
    /// <param name="accumulators">The eight initialized lanes.</param>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="secret">The validated secret range.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Accumulate512(Span<ulong> accumulators, ref byte source, ref byte secret)
    {
        Accumulate512Inlined(accumulators, ref source, ref secret);
    }

    /// <summary>Accumulates one validated stripe using the available instruction set.</summary>
    /// <param name="accumulators">The eight initialized lanes.</param>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="secret">The validated secret range.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Accumulate512Inlined(Span<ulong> accumulators, ref byte source, ref byte secret)
    {
    #if NET8_0_OR_GREATER
        if (IntPtr.Size == 8 && Avx2.IsSupported && BitConverter.IsLittleEndian)
        {
            for (int i = 0; i < AccumulatorCount / Vector256<ulong>.Count; i++)
            {
                Vector256<ulong> accVec = Accumulate256(Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(accumulators)), ref source, Vector256.LoadUnsafe(ref Unsafe.As<byte, uint>(ref secret)));
                Vector256.StoreUnsafe(accVec, ref MemoryMarshal.GetReference(accumulators));

                accumulators = accumulators.Slice(Vector256<ulong>.Count);
                secret = ref Unsafe.Add(ref secret, Vector256<byte>.Count);
                source = ref Unsafe.Add(ref source, Vector256<byte>.Count);
            }
        }
        else if (IntPtr.Size == 8 && (Sse2.IsSupported || AdvSimd.Arm64.IsSupported) && BitConverter.IsLittleEndian)
        {
            for (int i = 0; i < AccumulatorCount / Vector128<ulong>.Count; i++)
            {
                Vector128<ulong> accVec = Accumulate128(Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(accumulators)), ref source, Vector128.LoadUnsafe(ref Unsafe.As<byte, uint>(ref secret)));
                Vector128.StoreUnsafe(accVec, ref MemoryMarshal.GetReference(accumulators));

                accumulators = accumulators.Slice(Vector128<ulong>.Count);
                secret = ref Unsafe.Add(ref secret, Vector128<byte>.Count);
                source = ref Unsafe.Add(ref source, Vector128<byte>.Count);
            }
        }
        else
    #endif
        {
            for (int i = 0; i < AccumulatorCount; i++)
            {
                ulong sourceVal = ReadUInt64LE(ref Unsafe.Add(ref source, 8 * i));
                ulong sourceKey = sourceVal ^ ReadUInt64LE(ref Unsafe.Add(ref secret, i * 8));

                accumulators[i ^ 1] += sourceVal; // swap adjacent lanes
                accumulators[i] += Multiply32To64((uint)sourceKey, (uint)(sourceKey >> 32));
            }
        }
    }

    /// <summary>Scrambles all lanes after a complete block.</summary>
    /// <param name="accumulators">The eight initialized lanes.</param>
    /// <param name="secret">The validated secret range.</param>
    private static void ScrambleAccumulators(Span<ulong> accumulators, ref byte secret)
    {
    #if NET8_0_OR_GREATER
        if (IntPtr.Size == 8 && Avx2.IsSupported && BitConverter.IsLittleEndian)
        {
            for (int i = 0; i < AccumulatorCount / Vector256<ulong>.Count; i++)
            {
                Vector256<ulong> accVec = ScrambleAccumulator256(Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(accumulators)), Vector256.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref secret)));
                Vector256.StoreUnsafe(accVec, ref MemoryMarshal.GetReference(accumulators));

                accumulators = accumulators.Slice(Vector256<ulong>.Count);
                secret = ref Unsafe.Add(ref secret, Vector256<byte>.Count);
            }
        }
        else if (IntPtr.Size == 8 && (Sse2.IsSupported || AdvSimd.Arm64.IsSupported) && BitConverter.IsLittleEndian)
        {
            for (int i = 0; i < AccumulatorCount / Vector128<ulong>.Count; i++)
            {
                Vector128<ulong> accVec = ScrambleAccumulator128(Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(accumulators)), Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref secret)));
                Vector128.StoreUnsafe(accVec, ref MemoryMarshal.GetReference(accumulators));

                accumulators = accumulators.Slice(Vector128<ulong>.Count);
                secret = ref Unsafe.Add(ref secret, Vector128<byte>.Count);
            }
        }
        else
    #endif
        {
            for (int i = 0; i < AccumulatorCount; i++)
            {
                ulong xorShift = XorShift(accumulators[i], 47);
                ulong xorWithKey = xorShift ^ ReadUInt64LE(ref secret);
                accumulators[i] = xorWithKey * Prime32_1;

                secret = ref Unsafe.Add(ref secret, sizeof(ulong));
            }
        }
    }

    #if NET8_0_OR_GREATER
    /// <summary>Accumulates four lanes using AVX2.</summary>
    /// <param name="accVec">The current accumulator lanes.</param>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="secret">The validated secret range.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> Accumulate256(Vector256<ulong> accVec, ref byte source, Vector256<uint> secret)
    {
        Vector256<uint> sourceVec = Vector256.LoadUnsafe(ref Unsafe.As<byte, uint>(ref source));
        Vector256<uint> sourceKey = sourceVec ^ secret;

        Vector256<uint> sourceKeyLow = Vector256.Shuffle(sourceKey, Vector256.Create(1u, 0, 3, 0, 5, 0, 7, 0));
        Vector256<uint> sourceSwap = Vector256.Shuffle(sourceVec, Vector256.Create(2u, 3, 0, 1, 6, 7, 4, 5));
        Vector256<ulong> sum = accVec + sourceSwap.AsUInt64();
        Vector256<ulong> product = Avx2.IsSupported ?
            Avx2.Multiply(sourceKey, sourceKeyLow) :
            (sourceKey & Vector256.Create(~0u, 0u, ~0u, 0u, ~0u, 0u, ~0u, 0u)).AsUInt64() * (sourceKeyLow & Vector256.Create(~0u, 0u, ~0u, 0u, ~0u, 0u, ~0u, 0u)).AsUInt64();

        accVec = product + sum;
        return accVec;
    }
    #endif

    #if NET8_0_OR_GREATER
    /// <summary>Accumulates two lanes using SSE2 or ARM64 AdvSimd.</summary>
    /// <param name="accVec">The current accumulator lanes.</param>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="secret">The validated secret range.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> Accumulate128(Vector128<ulong> accVec, ref byte source, Vector128<uint> secret)
    {
        Vector128<uint> sourceVec = Vector128.LoadUnsafe(ref Unsafe.As<byte, uint>(ref source));
        Vector128<uint> sourceKey = sourceVec ^ secret;

        Vector128<uint> sourceSwap = Vector128.Shuffle(sourceVec, Vector128.Create(2u, 3, 0, 1));
        Vector128<ulong> sum = accVec + sourceSwap.AsUInt64();

        Vector128<ulong> product = MultiplyWideningLower(sourceKey);
        accVec = product + sum;
        return accVec;
    }
    #endif

    #if NET8_0_OR_GREATER
    /// <summary>Multiplies the low and high 32-bit halves of two lanes.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> MultiplyWideningLower(Vector128<uint> source)
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            Vector64<uint> sourceLow = Vector128.Shuffle(source, Vector128.Create(0u, 2, 0, 0)).GetLower();
            Vector64<uint> sourceHigh = Vector128.Shuffle(source, Vector128.Create(1u, 3, 0, 0)).GetLower();
            return AdvSimd.MultiplyWideningLower(sourceLow, sourceHigh);
        }
        else
        {
            Vector128<uint> sourceLow = Vector128.Shuffle(source, Vector128.Create(1u, 0, 3, 0));
            return Sse2.IsSupported ?
                Sse2.Multiply(source, sourceLow) :
                (source & Vector128.Create(~0u, 0u, ~0u, 0u)).AsUInt64() * (sourceLow & Vector128.Create(~0u, 0u, ~0u, 0u)).AsUInt64();
        }
    }
    #endif

    #if NET8_0_OR_GREATER
    /// <summary>Scrambles four AVX2 lanes.</summary>
    /// <param name="accVec">The current accumulator lanes.</param>
    /// <param name="secret">The validated secret range.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> ScrambleAccumulator256(Vector256<ulong> accVec, Vector256<ulong> secret)
    {
        Vector256<ulong> xorShift = accVec ^ Vector256.ShiftRightLogical(accVec, 47);
        Vector256<ulong> xorWithKey = xorShift ^ secret;
        accVec = xorWithKey * Vector256.Create((ulong)Prime32_1);
        return accVec;
    }
    #endif

    #if NET8_0_OR_GREATER
    /// <summary>Scrambles two SSE2 or ARM64 AdvSimd lanes.</summary>
    /// <param name="accVec">The current accumulator lanes.</param>
    /// <param name="secret">The validated secret range.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> ScrambleAccumulator128(Vector128<ulong> accVec, Vector128<ulong> secret)
    {
        Vector128<ulong> xorShift = accVec ^ Vector128.ShiftRightLogical(accVec, 47);
        Vector128<ulong> xorWithKey = xorShift ^ secret;
        accVec = xorWithKey * Vector128.Create((ulong)Prime32_1);
        return accVec;
    }
    #endif

    /// <summary>Mixes two input words with their position-specific secrets.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <param name="secretLow">The first constant secret word.</param>
    /// <param name="secretHigh">The second constant secret word.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix16Bytes(ref byte source, ulong secretLow, ulong secretHigh) =>
        Multiply64To128ThenFold(ReadUInt64LE(ref source) ^ secretLow, ReadUInt64LE(ref Unsafe.Add(ref source, 8)) ^ secretHigh);

    /// <summary>Computes an unsigned widening product.</summary>
    /// <param name="v1">The first factor.</param>
    /// <param name="v2">The second factor.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Multiply32To64(uint v1, uint v2) => (ulong)v1 * v2;

    /// <summary>Combines a word with its logical right shift.</summary>
    /// <param name="value">The input word.</param>
    /// <param name="shift">The logical shift distance.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong XorShift(ulong value, int shift) => value ^ (value >> shift);

    /// <summary>Applies the XXH64 finalizer required for zero-to-three-byte inputs.</summary>
    /// <param name="hash">The intermediate hash.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Avalanche64(ulong hash)
    {
        hash ^= hash >> 33;
        hash *= Prime64_2;
        hash ^= hash >> 29;
        hash *= Prime64_3;
        return hash ^ (hash >> 32);
    }

    /// <summary>Loads a validated unaligned four-byte little-endian word.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadUInt32LE(ref byte source)
    {
        uint value = Unsafe.ReadUnaligned<uint>(ref source);
    #if NETSTANDARD2_0
        if (!BitConverter.IsLittleEndian)
            value = (value >> 24) | ((value >> 8) & 0x0000FF00u) | ((value << 8) & 0x00FF0000u) | (value << 24);
        return value;
    #else
        return BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
    #endif
    }

    /// <summary>Loads a validated word while retaining the 32-bit block-copy contract.</summary>
    /// <param name="source">The complete validated input range.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadUInt64LE(ref byte source)
    {
    #if NETSTANDARD2_0
        return ReadUInt32LE(ref source) | ((ulong)ReadUInt32LE(ref Unsafe.Add(ref source, 4)) << 32);
    #else
        ulong value;
        if (IntPtr.Size == 8)
            value = Unsafe.ReadUnaligned<ulong>(ref source);
        else
        {
            value = 0;
            LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.As<ulong, byte>(ref value), ref source, 8);
        }
        return BitConverter.IsLittleEndian ? value : ReverseBytes(value);
    #endif
    }

    /// <summary>Reverses the bytes of one unsigned word.</summary>
    /// <param name="value">The input word.</param>
    /// <returns>The computed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReverseBytes(ulong value)
    {
    #if NETSTANDARD2_0
        value = ((value & 0x00FF00FF00FF00FFUL) << 8) | ((value >> 8) & 0x00FF00FF00FF00FFUL);
        value = ((value & 0x0000FFFF0000FFFFUL) << 16) | ((value >> 16) & 0x0000FFFF0000FFFFUL);
        return (value << 32) | (value >> 32);
    #else
        return BinaryPrimitives.ReverseEndianness(value);
    #endif
    }

    /// <summary>Initializes secret storage only when a long name is hashed.</summary>
    private static class LongSecret
    {
        internal static readonly byte[] Bytes =
        {
            0xb8, 0xfe, 0x6c, 0x39, 0x23, 0xa4, 0x4b, 0xbe, // DefaultSecretUInt64_0
            0x7c, 0x01, 0x81, 0x2c, 0xf7, 0x21, 0xad, 0x1c, // DefaultSecretUInt64_1
            0xde, 0xd4, 0x6d, 0xe9, 0x83, 0x90, 0x97, 0xdb, // DefaultSecretUInt64_2
            0x72, 0x40, 0xa4, 0xa4, 0xb7, 0xb3, 0x67, 0x1f, // DefaultSecretUInt64_3
            0xcb, 0x79, 0xe6, 0x4e, 0xcc, 0xc0, 0xe5, 0x78, // DefaultSecretUInt64_4
            0x82, 0x5a, 0xd0, 0x7d, 0xcc, 0xff, 0x72, 0x21, // DefaultSecretUInt64_5
            0xb8, 0x08, 0x46, 0x74, 0xf7, 0x43, 0x24, 0x8e, // DefaultSecretUInt64_6
            0xe0, 0x35, 0x90, 0xe6, 0x81, 0x3a, 0x26, 0x4c, // DefaultSecretUInt64_7
            0x3c, 0x28, 0x52, 0xbb, 0x91, 0xc3, 0x00, 0xcb, // DefaultSecretUInt64_8
            0x88, 0xd0, 0x65, 0x8b, 0x1b, 0x53, 0x2e, 0xa3, // DefaultSecretUInt64_9
            0x71, 0x64, 0x48, 0x97, 0xa2, 0x0d, 0xf9, 0x4e, // DefaultSecretUInt64_10
            0x38, 0x19, 0xef, 0x46, 0xa9, 0xde, 0xac, 0xd8, // DefaultSecretUInt64_11
            0xa8, 0xfa, 0x76, 0x3f, 0xe3, 0x9c, 0x34, 0x3f, // DefaultSecretUInt64_12
            0xf9, 0xdc, 0xbb, 0xc7, 0xc7, 0x0b, 0x4f, 0x1d, // DefaultSecretUInt64_13
            0x8a, 0x51, 0xe0, 0x4b, 0xcd, 0xb4, 0x59, 0x31, // DefaultSecretUInt64_14
            0xc8, 0x9f, 0x7e, 0xc9, 0xd9, 0x78, 0x73, 0x64, // DefaultSecretUInt64_15
            0xea, 0xc5, 0xac, 0x83, 0x34, 0xd3, 0xeb, 0xc3, // DefaultSecretUInt64_16
            0xc5, 0x81, 0xa0, 0xff, 0xfa, 0x13, 0x63, 0xeb, // DefaultSecretUInt64_17
            0x17, 0x0d, 0xdd, 0x51, 0xb7, 0xf0, 0xda, 0x49, // DefaultSecretUInt64_18
            0xd3, 0x16, 0x55, 0x26, 0x29, 0xd4, 0x68, 0x9e, // DefaultSecretUInt64_19
            0x2b, 0x16, 0xbe, 0x58, 0x7d, 0x47, 0xa1, 0xfc, // DefaultSecretUInt64_20
            0x8f, 0xf8, 0xb8, 0xd1, 0x7a, 0xd0, 0x31, 0xce, // DefaultSecretUInt64_21
            0x45, 0xcb, 0x3a, 0x8f, 0x95, 0x16, 0x04, 0x28, // DefaultSecretUInt64_22
            0xaf, 0xd7, 0xfb, 0xca, 0xbb, 0x4b, 0x40, 0x7e, // DefaultSecretUInt64_23

        };
    }
}
