using System;
using LuminPack.Code;
namespace LuminPack.Burst.Tests
{
/// <summary>Validates compact-record native kernels independently of framing and allocation.</summary>
public static unsafe class Latin1KernelContract
{
    /// <summary>Checks full ranges, non-ASCII Latin-1, invalid code units, mismatches, overlap tails and canaries.</summary>
    /// <param name="check">Classification.</param><param name="narrow">Narrowing.</param><param name="widen">Widening.</param><param name="equal">Equality.</param>
    /// <returns>The checked operation count.</returns>
    public static int Verify(BurstBackend.IsLatin1Function check, BurstBackend.NarrowLatin1Function narrow, BurstBackend.WidenLatin1Function widen, BurstBackend.Latin1EqualFunction equal)
    {
        int cases = 0;
        var random = new Random(20261026);
        for (int length = 0; length <= 513; length++)
        for (int alignment = 0; alignment < 4; alignment++)
        {
            char[] source = new char[length + 8], expanded = new char[length + 8];
            byte[] output = new byte[length + 8];
            for (int i = 0; i < source.Length; i++) { source[i] = (char)random.Next(256); expanded[i] = '\uA5A5'; }
            for (int i = 0; i < output.Length; i++) output[i] = 0xA5;
            fixed (char* p = source)
            fixed (char* q = expanded)
            fixed (byte* bytes = output)
            {
                ushort* text = (ushort*)(p + alignment);
                Require(check(text, length) == 1, "Representable text rejected.");
                Require(narrow(text, bytes + alignment, length) == 1, "Latin1 narrowing failed.");
                widen(bytes + alignment, (ushort*)(q + alignment), length);
                for (int i = 0; i < length; i++) Require(output[i + alignment] == source[i + alignment] && expanded[i + alignment] == source[i + alignment], "Latin1 payload differs: length=" + length + " alignment=" + alignment + " index=" + i + " byte=" + output[i + alignment] + " char=" + (int)expanded[i + alignment] + " expected=" + (int)source[i + alignment]);
                Require(equal(bytes + alignment, text, length) == 1, "Equal Latin1 text rejected.");
                for (int mismatch = 0; mismatch < length; mismatch++)
                {
                    source[alignment + mismatch] ^= (char)0x80;
                    Require(equal(bytes + alignment, text, length) == 0, "Latin1 mismatch accepted.");
                    source[alignment + mismatch] ^= (char)0x80;
                    cases++;
                }
                for (int invalid = 0; invalid < length; invalid += Math.Max(1, length / 17))
                {
                    char saved = source[alignment + invalid];
                    source[alignment + invalid] = invalid % 2 == 0 ? '\u0100' : '\uD800';
                    Require(check(text, length) == 0, "Unrepresentable code unit accepted.");
                    Require(equal(bytes + alignment, text, length) == 0, "Unrepresentable candidate matched Latin1.");
                    Require(narrow(text, bytes + alignment, length) == 0, "Invalid Latin1 narrowing accepted.");
                    source[alignment + invalid] = saved;
                    cases++;
                }
            }
            for (int i = 0; i < output.Length; i++)
                if (i < alignment || i >= alignment + length) Require(output[i] == 0xA5 && expanded[i] == '\uA5A5', "Latin1 canary changed.");
            cases++;
        }
        Require(check(null, 0) == 1 && narrow(null, null, 0) == 1 && equal(null, null, 0) == 1, "Empty Latin1 range failed.");
        widen(null, null, 0);
        return cases;
    }
    /// <summary>Reports a precise native contract failure.</summary><param name="condition">Required condition.</param><param name="message">Failure.</param>
    private static void Require(bool condition, string message) { if (!condition) LuminPackExceptionHelper.ThrowInvalidOperationException(message); }
}
}

