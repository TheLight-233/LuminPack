using System;
using System.Text;
using LuminPack.Code;
namespace LuminPack.Burst.Tests
{
    /// <summary>Checks the shared structural scanner through managed or native entry points.</summary>
    public static unsafe class JsonCapacityKernelContract
    {
        /// <summary>Checks cross-block escapes, nesting, flat pairs, incomplete tails and read-only inputs.</summary>
        /// <param name="utf8">The byte kernel.</param><param name="utf16">The code-unit kernel.</param>
        /// <returns>The checked case count.</returns>
        public static int Verify(BurstBackend.JsonUtf8Function utf8, BurstBackend.JsonUtf16Function utf16)
        {
            int cases = 0;
            for (int width = 0; width < 144; width++)
            for (int slashes = 0; slashes < 5; slashes++)
            {
                string q = "\"" + new string('x', width) + new string('\\', slashes * 2) + "\\\"\u4E2D\U0001F600,][{}\"";
                cases += Check(q + ",[1,{\"k\":" + q + "}],null]", 3, 0, utf8, utf16);
                cases += Check("[" + q + ",null],[1," + q + "]]", 2, 1, utf8, utf16);
            }
            foreach (int length in new[] { 0, 1, 7, 8, 15, 16, 31, 32, 63, 64, 127, 128, 1024 })
            {
                cases += Check(new string(' ', length) + "]", 0, 0, utf8, utf16);
                cases += Check("\"" + new string('x', length), -1, 0, utf8, utf16);
                cases += Check("\"" + new string('x', length) + "\\", -1, 0, utf8, utf16);
            }
            if (utf8(null, 0, 0) != -1 || utf16(null, 0, 0) != -1 || utf8(null, -1, 0) != -1 ||
                utf16(null, -1, 0) != -1 || utf8(null, 0, 2) != -1 || utf16(null, 0, 2) != -1)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Invalid sizing-kernel window or flags.");
            return cases;
        }
        /// <summary>Places a complete window at several alignments with unrelated trailing storage.</summary>
        /// <param name="text">Input after the opening bracket.</param><param name="expected">Expected count.</param>
        /// <param name="flags">Scanning mode.</param><param name="utf8">Byte kernel.</param><param name="utf16">Code-unit kernel.</param>
        /// <returns>The checked alignment count.</returns>
        private static int Check(string text, int expected, uint flags, BurstBackend.JsonUtf8Function utf8, BurstBackend.JsonUtf16Function utf16)
        {
            byte[] source = Encoding.UTF8.GetBytes(text);
            byte[] bytes = new byte[source.Length + 8];
            byte[] wideSource = Encoding.Unicode.GetBytes(text);
            byte[] chars = new byte[wideSource.Length + 8];
            for (int alignment = 0; alignment < 4; alignment++)
            {
                for (int i = 0; i < bytes.Length; i++) bytes[i] = 0xA5;
                for (int i = 0; i < chars.Length; i++) chars[i] = 0xA5;
                source.CopyTo(bytes, alignment); wideSource.CopyTo(chars, alignment);
                fixed (byte* p = bytes) fixed (byte* q = chars)
                {
                    if (utf8(p + alignment, source.Length, flags) != expected ||
                        utf16((ushort*)(q + alignment), text.Length, flags) != expected)
                        LuminPackExceptionHelper.ThrowInvalidOperationException("Structural sizing kernel returned an incorrect count.");
                }
                for (int i = 0; i < bytes.Length; i++)
                    if (bytes[i] != (i >= alignment && i < alignment + source.Length ? source[i - alignment] : 0xA5))
                        LuminPackExceptionHelper.ThrowInvalidOperationException("Sizing kernel changed UTF-8 input.");
                for (int i = 0; i < chars.Length; i++)
                    if (chars[i] != (i >= alignment && i < alignment + wideSource.Length ? wideSource[i - alignment] : 0xA5))
                        LuminPackExceptionHelper.ThrowInvalidOperationException("Sizing kernel changed UTF-16 input.");
            }
            return 8;
        }
    }
}
