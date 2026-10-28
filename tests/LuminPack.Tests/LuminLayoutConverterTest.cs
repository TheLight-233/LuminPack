using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that the shuffle kernel of the layout converter agrees with its byte-run loop.</summary>
public class LuminLayoutConverterTest
{
    /// <summary>Describes one synthetic pair of layouts: the data byte offsets of each side and the element widths.</summary>
    /// <param name="Name">A label used in failure text.</param>
    /// <param name="Source">The data byte offsets in the source layout.</param>
    /// <param name="SourceStride">The source element width.</param>
    /// <param name="Destination">The data byte offsets in the destination layout.</param>
    /// <param name="DestinationStride">The destination element width.</param>
    /// <param name="Vectorized">Whether the pair is expected to use the shuffle kernel when hardware supports it.</param>
    public readonly record struct Case(string Name, int[] Source, int SourceStride, int[] Destination, int DestinationStride, bool Vectorized);

    /// <summary>Builds offsets where field runs of the given lengths start at the given offsets.</summary>
    /// <param name="runs">Pairs of (start, length).</param>
    /// <returns>One offset per data byte, in declaration order.</returns>
    private static int[] Offsets(params (int Start, int Length)[] runs)
        => runs.SelectMany(run => Enumerable.Range(run.Start, run.Length)).ToArray();

    /// <summary>The layouts exercised by every test.</summary>
    /// <returns>One theory row per layout pair.</returns>
    public static IEnumerable<object[]> Cases()
    {
        // (byte, long): declared 64-bit order against 32-bit alignment and against the automatic order.
        yield return [new Case("byte+long 16->12", Offsets((0, 1), (8, 8)), 16, Offsets((0, 1), (4, 8)), 12, true)];
        yield return [new Case("byte+long 12->16", Offsets((0, 1), (4, 8)), 12, Offsets((0, 1), (8, 8)), 16, true)];
        yield return [new Case("byte+long declared->auto", Offsets((0, 1), (8, 8)), 16, Offsets((8, 1), (0, 8)), 16, true)];
        // (byte, int): eight-byte elements, two per vector.
        yield return [new Case("byte+int 8->8 swapped", Offsets((0, 1), (4, 4)), 8, Offsets((4, 1), (0, 4)), 8, true)];
        // Several fields with gaps; a mixed odd width.
        yield return [new Case("four runs 13->20", Offsets((0, 2), (2, 1), (4, 4), (9, 4)), 13, Offsets((0, 2), (4, 1), (8, 4), (16, 4)), 20, true)];
        // Wide elements need two vectors on one or both sides.
        yield return [new Case("wide scattered 24->28", Offsets((0, 4), (6, 4), (12, 4), (18, 4)), 24, Offsets((0, 4), (8, 4), (14, 4), (20, 4)), 28, true)];
        yield return [new Case("wide scattered 20->32", Offsets((0, 4), (5, 4), (10, 4), (15, 4)), 20, Offsets((16, 4), (0, 4), (4, 4), (24, 4)), 32, true)];
        // Adjacent runs on both sides merge into two copies, which the byte-run loop already handles in one call each.
        yield return [new Case("two merged runs 24->28", Offsets((0, 4), (4, 4), (8, 8), (16, 8)), 24, Offsets((0, 4), (8, 4), (12, 8), (20, 8)), 28, false)];
        yield return [new Case("narrow to wide 12->24", Offsets((0, 4), (4, 4), (8, 4)), 12, Offsets((0, 4), (8, 4), (16, 4)), 24, true)];
        // One contiguous run, an element wider than two vectors, and a one-run swap stay on byte runs.
        yield return [new Case("single run 8->12", Offsets((0, 8)), 8, Offsets((4, 8)), 12, false)];
        yield return [new Case("huge 40->48", Offsets((0, 8), (16, 8), (24, 8)), 40, Offsets((0, 8), (24, 8), (32, 8)), 48, false)];
    }

    /// <summary>Checks both directions of every layout pair over many element counts, including every remainder.</summary>
    /// <param name="layout">The layout pair.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ShuffleKernelMatchesByteRuns(Case layout)
    {
        bool hardware = Ssse3.IsSupported || AdvSimd.Arm64.IsSupported;
        foreach (bool forward in new[] { true, false })
        {
            var (from, fromStride, to, toStride) = forward
                ? (layout.Source, layout.SourceStride, layout.Destination, layout.DestinationStride)
                : (layout.Destination, layout.DestinationStride, layout.Source, layout.SourceStride);
            var vectors = new LuminLayout(from, fromStride, to, toStride);
            var runs = new LuminLayout(from, fromStride, to, toStride, allowVectors: false);
            Assert.False(runs.UsesVectors);
            if (hardware && layout.Vectorized) Assert.True(vectors.UsesVectors, layout.Name + ": the shuffle kernel was not selected.");
            if (!layout.Vectorized) Assert.False(vectors.UsesVectors, layout.Name + ": the byte-run path was expected.");
            var random = new Random(1234 + from.Length);
            for (int count = 0; count <= 70; count++)
            {
                byte[] source = new byte[Math.Max(1, count * fromStride)];
                random.NextBytes(source);
                // A sentinel after the destination catches stores that run past the last element.
                byte[] expected = new byte[count * toStride + 64], actual = new byte[count * toStride + 64];
                expected.AsSpan(count * toStride).Fill(0x5A);
                actual.AsSpan(count * toStride).Fill(0x5A);
                if (count != 0)
                {
                    runs.Convert(ref source[0], ref expected[0], count);
                    vectors.Convert(ref source[0], ref actual[0], count);
                }
                for (int element = 0; element < count; element++)
                    for (int index = 0; index < to.Length; index++)
                        Assert.True(expected[element * toStride + to[index]] == actual[element * toStride + to[index]],
                            $"{layout.Name} forward={forward} count={count} element={element} byte={index}");
                Assert.True(expected.AsSpan(count * toStride).SequenceEqual(actual.AsSpan(count * toStride)),
                    $"{layout.Name} forward={forward} count={count}: stores ran past the destination.");
            }
        }
    }

    /// <summary>Checks that converting there and back restores every data byte.</summary>
    [Fact]
    public void RoundTripThroughForeignLayoutIsLossless()
    {
        int[] native = Offsets((0, 1), (8, 8)), packed = Offsets((0, 1), (4, 8));
        var down = new LuminLayout(native, 16, packed, 12);
        var up = new LuminLayout(packed, 12, native, 16);
        for (int count = 1; count <= 40; count++)
        {
            byte[] original = new byte[count * 16], foreign = new byte[count * 12], restored = new byte[count * 16];
            new Random(count).NextBytes(original);
            down.Convert(ref original[0], ref foreign[0], count);
            up.Convert(ref foreign[0], ref restored[0], count);
            for (int element = 0; element < count; element++)
                foreach (int offset in native)
                    Assert.Equal(original[element * 16 + offset], restored[element * 16 + offset]);
        }
    }
}
