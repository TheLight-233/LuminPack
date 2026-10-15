using System.Runtime.CompilerServices;
using LuminPack.Code;

namespace LuminPackBenchmark;

/// <summary>Provides dictionary bucket jit probe functionality.</summary>
internal static class DictionaryBucketJitProbe
{
    private const int Count = 100;
    private const int Iterations = 100_000;

    /// <summary>Measures rebuild int for the configured benchmark input.</summary>
    /// <param name="dictionary">The dictionary used by this operation.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RebuildInt(Dictionary<int, int> dictionary)
    {
        LuminPackMarshal.RebuildDictionaryBuckets(
            LuminPackMarshal.GetDictionaryView(dictionary),
            Count);
    }

    /// <summary>Measures rebuild string for the configured benchmark input.</summary>
    /// <param name="dictionary">The dictionary used by this operation.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RebuildString(Dictionary<string, long> dictionary)
    {
        LuminPackMarshal.RebuildDictionaryBuckets(
            LuminPackMarshal.GetDictionaryView(dictionary),
            Count);
    }

    /// <summary>Measures run for the configured benchmark input.</summary>
    internal static void Run()
    {
        var ints = new Dictionary<int, int>(Count);
        var intView = LuminPackMarshal.GetDictionaryView(ints);
        for (var i = 0; i < Count; i++)
        {
            intView._entries[i].Key = i;
            intView._entries[i].Value = i * 2;
        }

        var strings = new Dictionary<string, long>(Count);
        var stringView = LuminPackMarshal.GetDictionaryView(strings);
        for (var i = 0; i < Count; i++)
        {
            stringView._entries[i].Key = i.ToString();
            stringView._entries[i].Value = i * 3L;
        }

        for (var i = 0; i < Iterations; i++)
        {
            RebuildInt(ints);
            RebuildString(strings);
        }

        Console.WriteLine($"Dictionary bucket JIT probe complete: {ints.Count + strings.Count}");
    }
}
