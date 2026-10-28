using System;
using LuminPack.Code;
using LuminPack.Burst;

/// <summary>Shares bounded differential checks between the portable host and actual Unity Burst function pointers.</summary>
public static unsafe class KernelContract
{
    /// <summary>Checks Core copy refusal/fallback, exact tails, every mismatch position and periodic overlapping matches.</summary>
    /// <param name="copy">Complete copy or no-write refusal.</param><param name="match">Equal prefix.</param><param name="xor">Alias-safe XOR.</param><param name="expand">Periodic match expansion.</param>
    public static void Verify(BurstBackend.CopyFunction copy, BurstBackend.CountMatchFunction match, BurstBackend.XorFunction xor,
        BurstBackend.CopyMatchFunction expand)
    {
        var random = new Random(20261025);
        for (int length = 0; length <= 259; length++)
        for (int alignment = 0; alignment < 4; alignment++)
        {
            byte[] input = new byte[length + 64], output = new byte[length + 64], key = new byte[length + 64];
            random.NextBytes(input); random.NextBytes(key);
            for (int i = 0; i < output.Length; i++) output[i] = 0xA5;
            fixed (byte* a = input)
            fixed (byte* b = output)
            fixed (byte* k = key)
            {
                if (copy(b + alignment + 3, a + alignment, (uint)length) == 0)
                    LuminPackMarshal.CopyBlockUnaligned(b + alignment + 3, a + alignment, (uint)length);
                Require(new ReadOnlySpan<byte>(a + alignment, length).SequenceEqual(new ReadOnlySpan<byte>(b + alignment + 3, length)), "Copy bytes differ.");
                for (int mismatch = 0; mismatch < length; mismatch++)
                {
                    b[alignment + 3 + mismatch] ^= 1;
                    Require(match(a + alignment, b + alignment + 3, (uint)length) == mismatch, "Wrong mismatch position.");
                    b[alignment + 3 + mismatch] ^= 1;
                }
                xor(b + alignment + 3, b + alignment + 3, k + alignment, (uint)length);
                for (int i = 0; i < length; i++) Require(b[alignment + 3 + i] == (byte)(a[alignment + i] ^ k[alignment + i]), "In-place XOR differs.");
            }
            for (int i = 0; i < output.Length; i++)
                if (i < alignment + 3 || i >= alignment + 3 + length) Require(output[i] == 0xA5, "Output canary changed.");
        }

        for (int period = 1; period <= 80; period++)
        for (int count = 0; count <= 193; count++)
        {
            byte[] output = new byte[period + count + 13];
            random.NextBytes(output);
            byte[] expected = (byte[])output.Clone();
            for (int i = 0; i < count; i++) expected[period + i] = expected[i];
            fixed (byte* p = output) expand(p + period, (uint)count, (uint)period);
            Require(output.AsSpan().SequenceEqual(expected), "Periodic expansion or tail differs.");
        }

        copy(null, null, 0);
        Require(match(null, null, 0) == 0, "Empty comparison touched memory.");
        xor(null, null, null, 0);
        expand(null, 0, 1);
    }

    /// <summary>Reports a differential failure through the production exception boundary.</summary>
    /// <param name="condition">The required condition.</param><param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) LuminPackExceptionHelper.ThrowInvalidOperationException(message);
    }
}
