using System;

namespace LuminPack.Burst.Tests
{
    /// <summary>Runs the Boolean transfer contract on managed and native Burst entry points.</summary>
    public static unsafe class BooleanKernelContract
    {
        /// <summary>Checks every byte position through all vector tails, including unaligned and exact-alias ranges.</summary>
        /// <param name="copy">The complete Boolean transfer.</param>
        /// <param name="accelerated">Whether a compiled SIMD backend is present.</param>
        public static void Verify(BurstBackend.BooleanFunction copy, bool accelerated)
        {
            for (int length = 0; length <= 257; length++)
            {
                byte[] input = new byte[length + 2];
                byte[] output = new byte[length + 4];
                output.AsSpan().Fill(204);
                for (int i = 0; i < length; i++)
                    input[1 + i] = (byte)(i % 2);
                fixed (byte* source = input, destination = output)
                {
                    bool available = accelerated && length >= 16;
                    Check(copy(source + 1, destination + 3, length) == (available ? 1 : -1), "Boolean result or decline");
                    if (available)
                    {
                        Check(input.AsSpan(1, length).SequenceEqual(output.AsSpan(3, length)), "Canonical copy");
                        for (int i = 0; i < length; i++)
                        {
                            byte saved = input[1 + i];
                            input[1 + i] = (byte)(i % 2 == 0 ? 2 : 128);
                            Check(copy(source + 1, destination + 3, length) == 0, "Invalid byte rejection");
                            Check(input.AsSpan(1, length).SequenceEqual(output.AsSpan(3, length)), "Noncanonical copy");
                            Check(copy(source + 1, source + 1, length) == 0, "Exact alias validation");
                            input[1 + i] = saved;
                        }
                    }
                    else
                    {
                        foreach (byte item in output)
                            Check(item == 204, "Decline leaves output untouched");
                    }
                }
                Check(output[0] == 204 && output[1] == 204 && output[2] == 204 && output[length + 3] == 204,
                    "Destination bounds");
            }
        }

        /// <summary>Reports a violated transfer contract.</summary>
        /// <param name="condition">The expected condition.</param>
        /// <param name="message">The failed condition.</param>
        private static void Check(bool condition, string message)
        {
            if (!condition)
                LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException(message);
        }
    }
}
