using Strict8 = global::LuminPack.Option.LuminPackOptions<global::LuminPack.Option.LuminPackUtf8LengthStrings, global::LuminPack.Option.LuminPackGeneralFormat, global::LuminPack.Option.LuminPackDefaultDepth, global::LuminPack.Option.LuminPackStrictJson>;
using Strict16 = global::LuminPack.Option.LuminPackOptions<global::LuminPack.Option.LuminPackUtf16LengthStrings, global::LuminPack.Option.LuminPackGeneralFormat, global::LuminPack.Option.LuminPackDefaultDepth, global::LuminPack.Option.LuminPackStrictJson>;
using System.Buffers;
using System.Text;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;
namespace LuminPackUnitTest;

/// <summary>Checks private deep lookahead rentals preserve the original reader state.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed unsafe class JsonDeepLookaheadTest
{
    /// <summary>Checks warmed deep scans and exceptional cleanup at different original/temporary capacities.</summary>
    /// <param name="utf16">Whether the complete input contains UTF-16 code units.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeepLookaheadKeepsStateAndReturnsItsPrivateStorage(bool utf16)
    {
        foreach (var pair in new[] { (1, 80), (70, 0), (70, 250), (260, 32) })
        foreach (bool malformed in new[] { false, true })
        {
            string text = new string('[', pair.Item1) + "[" + new string('[', pair.Item2) + "1" +
                new string(']', pair.Item2) + (pair.Item2 == 0 ? ",2" : "") +
                (malformed ? "" : "]" + new string(']', pair.Item1));
            byte[] bytes = (utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes(text);
            fixed (byte* pin = bytes)
            {
                ReadOnlySpan<byte> input = bytes;
                var reader = new LuminPackJsonReader(ref input, null!);
                for (int i = 0; i <= pair.Item1; i++)
                    if (utf16) reader.Read<Strict16>(); else reader.Read<Strict8>();
                int position = reader.Position, depth = reader.Depth;
                byte grammar = reader.GrammarState;
                var token = reader.CurrentTokenType;
                ulong kinds = reader.ObjectContainerBits;
                byte[]? original = reader.DeepContainerKinds;
                byte[]? snapshot = original?.ToArray();
                long best = long.MaxValue;
                for (int iteration = 0; iteration < 24; iteration++)
                {
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    bool failed = false;
                    int count = -1;
                    try { count = utf16 ? reader.CountArrayElements<Strict16>() : reader.CountArrayElements<Strict8>(); }
                    catch (Exception) { failed = true; }
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    Assert.Equal(malformed, failed);
                    if (!malformed) Assert.Equal(pair.Item2 == 0 ? 2 : 1, count);
                    Assert.Equal(position, reader.Position); Assert.Equal(depth, reader.Depth);
                    Assert.Equal(grammar, reader.GrammarState); Assert.Equal(token, reader.CurrentTokenType);
                    Assert.Equal(kinds, reader.ObjectContainerBits);
                    Assert.Same(original, reader.DeepContainerKinds);
                    if (snapshot is not null) Assert.Equal(snapshot, reader.DeepContainerKinds);
                    if (iteration >= 4) best = Math.Min(best, allocated);
                }
                if (!malformed) Assert.Equal(0, best);
            }
        }
    }
    /// <summary>Checks ordinary deep grammar state stays inline through the supported depth window.</summary>
    /// <param name="depth">The nesting depth to exercise.</param>
    /// <param name="utf16">Whether the complete input contains UTF-16 code units.</param>
    [Theory]
    [InlineData(65, false)]
    [InlineData(128, false)]
    [InlineData(192, false)]
    [InlineData(193, false)]
    [InlineData(65, true)]
    [InlineData(128, true)]
    [InlineData(192, true)]
    [InlineData(193, true)]
    public void OrdinaryDeepStateAvoidsAllocationThroughInlineDepth(int depth, bool utf16)
    {
        string text = new string('[', depth) + "1" + new string(']', depth);
        byte[] bytes = (utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes(text);
        fixed (byte* pin = bytes)
        {
            ReadOnlySpan<byte> input = bytes;
            long best = long.MaxValue;
            for (int iteration = 0; iteration < 24; iteration++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var reader = new LuminPackJsonReader(ref input, null!);
                for (int index = 0; index <= depth; index++)
                    if (utf16) reader.Read<Strict16>(); else reader.Read<Strict8>();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (iteration >= 4) best = Math.Min(best, allocated);
            }
            if (depth <= 192) Assert.Equal(0, best);
        }
    }

}
