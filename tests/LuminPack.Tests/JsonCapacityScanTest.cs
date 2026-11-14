using System.Text;
using System.Text.Json;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;
namespace LuminPackUnitTest;

/// <summary>Checks structural hints independently of generated collection construction.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed unsafe class JsonCapacityScanTest
{
    private delegate ulong EscapeMask(ulong quotes, ulong slashes, int width, ref bool carry);

    /// <summary>Checks every small escape mask and randomized full-width masks against a lane-by-lane state machine.</summary>
    [Fact]
    public void EscapeMasksPreserveParityAtEveryBlockWidth()
    {
        var kernel = typeof(LuminPackJsonReader).GetMethod("RemoveEscapedQuotes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.CreateDelegate<EscapeMask>();
        for (int width = 1; width <= 16; width++)
            for (ulong slashes = 0; slashes < 1UL << width; slashes++)
                CheckEscapeMask(kernel, slashes, width);
        var random = new Random(73481);
        byte[] storage = new byte[8];
        foreach (int width in new[] { 17, 31, 32, 33, 63, 64 })
        {
            ulong valid = ulong.MaxValue >> (64 - width);
            CheckEscapeMask(kernel, valid, width);
            for (int lane = 0; lane < width; lane++)
            {
                CheckEscapeMask(kernel, valid << lane & valid, width);
                CheckEscapeMask(kernel, valid >> lane, width);
            }
            for (int sample = 0; sample < 4096; sample++)
            {
                random.NextBytes(storage);
                CheckEscapeMask(kernel, BitConverter.ToUInt64(storage) & valid, width);
            }
        }
    }

    /// <summary>Compares both incoming escape states with a scalar model of complete valid lanes.</summary>
    /// <param name="kernel">The production mask kernel.</param><param name="slashes">Backslash lanes.</param>
    /// <param name="width">Valid lane count.</param>
    private static void CheckEscapeMask(EscapeMask kernel, ulong slashes, int width)
    {
        ulong quotes = ~slashes & (ulong.MaxValue >> (64 - width));
        for (int incoming = 0; incoming < 2; incoming++)
        {
            bool expectedCarry = incoming != 0;
            ulong expectedQuotes = 0;
            for (int lane = 0; lane < width; lane++)
            {
                ulong bit = 1UL << lane;
                if (expectedCarry)
                    expectedCarry = false;
                else if ((slashes & bit) != 0)
                    expectedCarry = true;
                else
                    expectedQuotes |= bit;
            }
            bool carry = incoming != 0;
            Assert.Equal(expectedQuotes, kernel(quotes, slashes, width, ref carry));
            Assert.Equal(expectedCarry, carry);
        }
    }

    /// <summary>Checks nested values, long strings and quote/backslash runs across all vector and word boundaries.</summary>
    /// <param name="utf16">Whether the document contains UTF-16 code units.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StructuralCountsPreserveReaderAndInput(bool utf16)
    {
        for (int width = 0; width < 144; width++)
        for (int escapes = 0; escapes < 5; escapes++)
        {
            string q = JsonSerializer.Serialize(new string('x', width) + new string('\\', escapes) + "\"\u4E2D\U0001F600,][{}");
            string text = "[" + q + ",[1,{\"k\":" + q + "}],null]";
            byte[] bytes = (utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes(text);
            byte[] original = (byte[])bytes.Clone();
            Assert.Equal(3, Hint(bytes, utf16));
            Assert.Equal(original, bytes);
            string pairs = "[" + new string(' ', 32) + "[" + q + ",null],[1," + q + "]]";
            Assert.Equal(2, Hint((utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes(pairs), utf16, true));
        }
        Assert.Equal(0, Hint((utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes("[ \r\n\t]"), utf16));
    }

    /// <summary>Checks incomplete windows and demonstrates that a structural hint is not a JSON syntax check.</summary>
    /// <param name="utf16">Whether code units are UTF-16.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncompleteAndInvalidWindowsRetainOrdinaryParsing(bool utf16)
    {
        Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
        foreach (string text in new[] { "[", "[1,", "[\"abc", "[[1,2]", "[\"escaped\\\"", "[1}" })
            Assert.Equal(-1, Hint(encoding.GetBytes(text), utf16));
        Assert.Equal(2, Hint(encoding.GetBytes("[1,]"), utf16));
        Assert.Equal(-1, Hint(encoding.GetBytes("null"), utf16));
        if (utf16)
        {
            byte[] odd = encoding.GetBytes("[1]").Concat(new byte[] { 0 }).ToArray();
            Assert.Equal(-1, Hint(odd, true));
        }
        byte[] input = encoding.GetBytes("[1,2]");
        fixed (byte* pin = input)
        {
            ReadOnlySpan<byte> span = input;
            var reader = new LuminPackJsonReader(ref span, null);
            if (utf16) reader.Read<LuminPackUtf16LengthOptions>(); else reader.Read<LuminPackUtf8LengthOptions>();
            Assert.Equal(0, utf16 ? reader.GetArrayCapacityHint<LuminPackUtf16LengthOptions>(int.MaxValue) : reader.GetArrayCapacityHint<LuminPackUtf8LengthOptions>(int.MaxValue));
            Assert.Equal(0, utf16 ? reader.GetArrayCapacityHint<LuminPackUtf16LengthOptions>(-1) : reader.GetArrayCapacityHint<LuminPackUtf8LengthOptions>(-1));
        }
    }

    /// <summary>Checks UTF-16 input does not require an aligned byte-array slice.</summary>
    [Fact]
    public void Utf16HintsAcceptUnalignedCompleteWindows()
    {
        byte[] source = Encoding.Unicode.GetBytes("[" + string.Join(",", Enumerable.Repeat("123", 129)) + "]");
        for (int offset = 0; offset < 8; offset++)
        {
            byte[] carrier = Enumerable.Repeat((byte)0xA5, source.Length + 8).ToArray();
            source.CopyTo(carrier, offset);
            fixed (byte* pin = carrier)
            {
                ReadOnlySpan<byte> input = carrier.AsSpan(offset, source.Length);
                var reader = new LuminPackJsonReader(ref input, null);
                reader.Read<LuminPackUtf16LengthOptions>();
                Assert.Equal(129, reader.GetArrayLengthHint<LuminPackUtf16LengthOptions>());
                Assert.Equal(2, reader.Position);
            }
        }
    }

    /// <summary>Checks warmed scanning allocates no managed storage.</summary>
    /// <param name="utf16">Whether code units are UTF-16.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarmHintsAllocateNothing(bool utf16)
    {
        string text = "[" + string.Join(",", Enumerable.Repeat("[1,\"x\\\\\\\"\u4E2D\"]", 257)) + "]";
        byte[] bytes = (utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes(text);
        for (int i = 0; i < 64; i++) Hint(bytes, utf16, true);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int count = 0;
        for (int i = 0; i < 256; i++) count += Hint(bytes, utf16, true);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(257 * 256, count);
        Assert.Equal(0, allocated);
    }

    /// <summary>Checks state around a single direct hint operation.</summary>
    /// <param name="bytes">Complete document.</param><param name="utf16">Encoding.</param><param name="entries">Flat pairs.</param>
    /// <returns>The hint.</returns>
    private static int Hint(byte[] bytes, bool utf16, bool entries = false)
    {
        fixed (byte* pin = bytes)
        {
            ReadOnlySpan<byte> input = bytes;
            var reader = new LuminPackJsonReader(ref input, null);
            if (utf16) reader.Read<LuminPackUtf16LengthOptions>(); else reader.Read<LuminPackUtf8LengthOptions>();
            int position = reader.Position, depth = reader.Depth;
            var token = reader.CurrentTokenType;
            int count = utf16 ? (entries ? reader.GetEntryArrayCapacityHint<LuminPackUtf16LengthOptions>() : reader.GetArrayLengthHint<LuminPackUtf16LengthOptions>()) :
                (entries ? reader.GetEntryArrayCapacityHint<LuminPackUtf8LengthOptions>() : reader.GetArrayLengthHint<LuminPackUtf8LengthOptions>());
            if (position != reader.Position || depth != reader.Depth || token != reader.CurrentTokenType)
                throw new InvalidOperationException("Structural sizing changed reader state.");
            return count;
        }
    }
}
