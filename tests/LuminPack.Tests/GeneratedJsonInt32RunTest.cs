using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;
using StrictUtf8 = LuminPack.Option.LuminPackOptions<LuminPack.Option.LuminPackUtf8LengthOptions, LuminPack.Option.LuminPackGeneralFormat,
    LuminPack.Option.LuminPackDefaultDepth, LuminPack.Option.LuminPackStrictJson>;
using StrictUtf16 = LuminPack.Option.LuminPackOptions<LuminPack.Option.LuminPackUtf16LengthOptions, LuminPack.Option.LuminPackGeneralFormat,
    LuminPack.Option.LuminPackDefaultDepth, LuminPack.Option.LuminPackStrictJson>;

namespace LuminPackUnitTest;

/// <summary>Exercises generated integer groups at the real object reader boundary.</summary>
public sealed class GeneratedJsonInt32RunTest
{
    /// <summary>Checks both document encodings and recovery after a partially matched group.</summary>
    [Fact]
    public void MatchesOrderedFieldsAndResumesAtTheFirstMiss()
    {
        Verify<StrictUtf8>();
        Verify<StrictUtf16>();

        static void Verify<P>() where P : struct, ILuminPackOptions
        {
            const string ordered = "{\"First\":-2147483648,\"Second\":2147483647,\"Third\":-0,\"Name\":\"unit\",\"Fourth\":4,\"Fifth\":5,\"Sixth\":6}";
            var value = LuminPackSerializer.DeserializeJson<JsonInt32RunRecord, P>(ordered);
            Assert.Equal((int.MinValue, int.MaxValue, 0, "unit", 4, 5, 6), Observe(value));

            const string mixed = "{\"First\":1,\"Second\":2, \"Th\\u0069rd\":3,\"Name\":\"other\",\"Fourth\":4,\"unknown\":[0],\"Sixth\":6,\"Second\":8}";
            var target = value;
            LuminPackSerializer.DeserializeJson<JsonInt32RunRecord, P>(mixed, ref target);
            Assert.Same(value, target);
            Assert.Equal((1, 8, 3, "other", 4, 0, 6), Observe(target));
        }

        static (int, int, int, string?, int, int, int) Observe(JsonInt32RunRecord value) =>
            (value.First, value.Second, value.Third, value.Name, value.Fourth, value.Fifth, value.Sixth);
    }

    /// <summary>Checks that failed fields fall back with their original syntax checks and reuse publication.</summary>
    [Fact]
    public void KeepsStrictFailuresAndCompletedReuseAssignments()
    {
        Check<StrictUtf8>();
        Check<StrictUtf16>();

        static void Check<P>() where P : struct, ILuminPackOptions
        {
            foreach (string suffix in new[] { "2147483648}", "01}", "-}", "1.5}", "1e2}", "true}", "" })
            {
                var record = new JsonInt32RunRecord { First = -1, Second = -2, Third = -3 };
                var initial = record;
                string input = "{\"First\":1,\"Second\":2,\"Third\":" + suffix;
                Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<JsonInt32RunRecord, P>(input, ref record));
                Assert.Same(initial, record);
                Assert.Equal((1, 2, -3), (record.First, record.Second, record.Third));
            }
        }
    }
}

/// <summary>An ordinary record with two integer groups separated by a string value.</summary>
[LuminPackable]
public sealed partial class JsonInt32RunRecord
{
    /// <summary>The first integer.</summary>
    public int First;
    /// <summary>The second integer.</summary>
    public int Second;
    /// <summary>The third integer.</summary>
    public int Third;
    /// <summary>The member between the groups.</summary>
    public string? Name;
    /// <summary>The fourth integer.</summary>
    public int Fourth;
    /// <summary>The fifth integer.</summary>
    public int Fifth;
    /// <summary>The sixth integer.</summary>
    public int Sixth;
}
