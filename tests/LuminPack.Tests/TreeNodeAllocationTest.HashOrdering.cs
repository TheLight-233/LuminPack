using System.Reflection;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

public sealed partial class TreeNodeAllocationTest
{
    /// <summary>Matches BCL signed ordering, collision detection and untouched pooled tails for both sort paths.</summary>
    [Fact]
    public void HashOrderingPreservesKeysAndPooledTails()
    {
        var sort = typeof(LuminTreeStorage).GetMethod("SortDistinct", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<long[], int, bool>>();
        var portable = typeof(LuminTreeStorage).GetMethod("SortHashKeys", BindingFlags.Static | BindingFlags.NonPublic)?
            .CreateDelegate<Action<long[], int>>();
#if NETSTANDARD2_1
        Assert.NotNull(portable);
#endif
        var random = new Random(719);
        foreach (int count in Enumerable.Range(0, 65).Concat(new[] { 127, 128, 129, 257, 1023, 4097 }))
        for (int shape = 0; shape < 8; shape++)
        {
            long[] input = HashOrderingInput(count, shape, random);
            long[] expected = (long[])input.Clone();
            Array.Sort(expected, 0, count);
            bool distinct = true;
            for (int i = 1; i < count; i++) distinct &= (int)(expected[i - 1] >> 32) != (int)(expected[i] >> 32);
            long[] actual = (long[])input.Clone();
            Assert.Equal(distinct, sort(actual, count));
            Assert.Equal(expected, actual);
            if (portable is not null)
            {
                input.CopyTo(actual, 0);
                portable(actual, count);
                Assert.Equal(expected, actual);
            }
        }
    }

    /// <summary>Checks repeated unsorted operations after initialization without hiding per-sort heap allocations.</summary>
    [Fact]
    public void WarmHashOrderingDoesNotAllocate()
    {
        var sort = typeof(LuminTreeStorage).GetMethod("SortDistinct", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<long[], int, bool>>();
        long[] keys = HashOrderingInput(257, 1, new Random(719));
        for (int i = 0; i < 64; i++) { Array.Reverse(keys, 0, 257); sort(keys, 257); }
        long best = long.MaxValue;
        for (int sample = 0; sample < 3; sample++)
        {
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++) { Array.Reverse(keys, 0, 257); sort(keys, 257); }
            best = Math.Min(best, GC.GetAllocatedBytesForCurrentThread() - start);
        }
        Assert.Equal(0, best);
    }

    /// <summary>Builds packed signed hashes and original indices, including duplicate hashes and protected tail storage.</summary>
    /// <param name="count">The valid prefix length.</param>
    /// <param name="shape">The hash and index ordering pattern.</param>
    /// <param name="random">The deterministic random source.</param>
    /// <returns>The valid keys followed by sentinel values that must remain unchanged.</returns>
    private static long[] HashOrderingInput(int count, int shape, Random random)
    {
        long[] keys = Enumerable.Repeat(long.MinValue, count + 9).ToArray();
        for (int i = 0; i < count; i++)
        {
            int hash = shape switch
            {
                0 or 7 => random.Next(int.MinValue, int.MaxValue),
                1 => count - i,
                2 => i,
                3 => i % 3,
                6 => (i % 5) switch { 0 => int.MinValue, 1 => int.MaxValue, 2 => -1, 3 => 0, _ => 1 },
                _ => 0
            };
            uint index = shape == 5 ? (uint)(count - i) : shape == 7 ? uint.MaxValue - (uint)i : (uint)i;
            keys[i] = ((long)hash << 32) | index;
        }
        return keys;
    }
}
