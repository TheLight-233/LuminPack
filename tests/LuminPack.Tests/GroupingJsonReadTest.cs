using System.Text;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects JSON grouping property dispatch and publication through LINQ interfaces.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class GroupingJsonReadTest
{
    /// <summary>Checks escaped property names, skipped fields and replacement in both encodings.</summary>
    [Fact]
    public void NamesAndPublication()
    {
        Check<LuminPackUtf8LengthOptions>();
        Check<LuminPackUtf16LengthOptions>();
        Check<LuminPackOptions<LuminPackUtf8LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>>();
        Check<LuminPackOptions<LuminPackUtf16LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>>();
    }

    /// <summary>Detects allocations for fixed grouping property names.</summary>
    [Fact]
    public void NameAllocation()
    {
        CheckAllocations<LuminPackUtf8LengthOptions>();
        CheckAllocations<LuminPackUtf16LengthOptions>();
    }

    /// <summary>Checks how a compiled profile reads and publishes grouping and lookup values.</summary>
    /// <typeparam name="TOptions">The JSON encoding and validation profile.</typeparam>
    private static void Check<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        byte[] source = Encode<TOptions>("{\"Eleme\\u006ets\":[2,3],\"ignored\":{\"nested\":[1]},\"K\\u0065y\":5,\"Key\":7}");
        IGrouping<int, int> old = new LuminPackGrouping<int, int>(9, new[] { 9 });
        IGrouping<int, int> value = old;
        LuminPackSerializer.DeserializeJson<IGrouping<int, int>, TOptions>(source, ref value);
        Assert.NotSame(old, value);
        Assert.Equal(7, value.Key);
        Assert.Equal(new[] { 2, 3 }, value);
        Assert.Equal(new[] { 9 }, old);

#if LUMINPACK_NEXT_GENERATOR
        byte[] invalidValue = Encode<TOptions>("{\"Elements\":[2,3],\"Key\":\"invalid\"}");
        byte[] invalidName = Encode<TOptions>("{\"Elements\":[2,3],true:7}");
        value = old;
        Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.DeserializeJson<IGrouping<int, int>, TOptions>(invalidValue, ref value));
        Assert.Same(old, value);
        Assert.Throws<FormatException>(() => LuminPackSerializer.DeserializeJson<IGrouping<int, int>, TOptions>(invalidName, ref value));
        Assert.Same(old, value);
#endif

        byte[] lookupSource = Encode<TOptions>("[{\"Key\":2,\"Elements\":[3]},{\"Key\":4,\"Elements\":[5]}]");
        ILookup<int, int> lookup = new[] { 9 }.ToLookup(number => number);
        var oldLookup = lookup;
        LuminPackSerializer.DeserializeJson<ILookup<int, int>, TOptions>(lookupSource, ref lookup);
        Assert.NotSame(oldLookup, lookup);
        Assert.Equal(new[] { 2, 4 }, lookup.Select(group => group.Key));
        Assert.Equal(3, Assert.Single(lookup[2]));
        Assert.Equal(5, Assert.Single(lookup[4]));
        Assert.Equal(9, Assert.Single(oldLookup[9]));
    }

    /// <summary>Counts warmed allocations while reading fixed names into an existing grouping slot.</summary>
    /// <typeparam name="TOptions">The JSON encoding and validation profile.</typeparam>
    private static void CheckAllocations<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        byte[] source = Encode<TOptions>("{\"Key\":7,\"Elements\":[]}");
        IGrouping<int, int> value = new LuminPackGrouping<int, int>(9, Array.Empty<int>());
        for (int index = 0; index < 32; index++)
            LuminPackSerializer.DeserializeJson<IGrouping<int, int>, TOptions>(source, ref value);

        const int iterations = 128;
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < iterations; index++)
            LuminPackSerializer.DeserializeJson<IGrouping<int, int>, TOptions>(source, ref value);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;

        // The grouping and its empty element collection account for 64 bytes on x64.
        Assert.InRange(allocated, 0, iterations * 80L);
        Assert.Equal(7, value.Key);
        Assert.Empty(value);
    }

    /// <summary>Encodes JSON text for the selected profile.</summary>
    /// <typeparam name="TOptions">The JSON encoding and validation profile.</typeparam>
    /// <param name="text">The JSON input.</param>
    /// <returns>The encoded JSON bytes.</returns>
    private static byte[] Encode<TOptions>(string text) where TOptions : struct, ILuminPackOptions =>
        (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(text);
}
