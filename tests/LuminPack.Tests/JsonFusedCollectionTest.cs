using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the fused JSON element and dictionary entry writers and the fused entry reader against the token paths.</summary>
public class JsonFusedCollectionTest
{
    private static readonly string?[] Strings =
    {
        null, "", "a", "ab", "abc", "abcd", "Title_01_Master", new string('x', 32), new string('y', 33), "quote\"inside", "back\\slash",
        "tab\tline\n", "caf\u00E9", "\u89D2\u8272", "\uD83C\uDF1F", "RECORD_001", "x\u0001y"
    };

    /// <summary>Checks string, int and long arrays and lists write exactly what the unfused enumerable path writes.</summary>
    [Fact]
    public void ElementWritersMatchTheTokenPath()
    {
        for (int length = 0; length <= Strings.Length; length++)
        {
            var strings = Strings.Take(length).ToList();
            string expected = LuminPackSerializer.SerializeJson((IEnumerable<string?>)(strings));
            Assert.Equal(expected, LuminPackSerializer.SerializeJson(strings));
            Assert.Equal(expected, LuminPackSerializer.SerializeJson(strings.ToArray()));
            Assert.Equal(strings, global::LuminPackUnitTest.ConcreteReads.ReadJson<List<string?>>(expected, static (string concreteReadInput, ref List<string?> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        }

        int[] ints = { 0, 1, -1, 9, 10, 9999, 10000, -10000, int.MaxValue, int.MinValue, 123456789 };
        long[] longs = { 0, 7, -7, 99999, 100000, long.MaxValue, long.MinValue, 1234567890123L };
        Assert.Equal(LuminPackSerializer.SerializeJson((IEnumerable<int>)(ints)), LuminPackSerializer.SerializeJson(ints));
        Assert.Equal(LuminPackSerializer.SerializeJson((IEnumerable<int>)(ints)), LuminPackSerializer.SerializeJson(ints.ToList()));
        Assert.Equal(LuminPackSerializer.SerializeJson((IEnumerable<long>)(longs)), LuminPackSerializer.SerializeJson(longs));
        Assert.Equal(LuminPackSerializer.SerializeJson((IEnumerable<long>)(longs)), LuminPackSerializer.SerializeJson(longs.ToList()));
        Assert.Equal(ints, global::LuminPackUnitTest.ConcreteReads.ReadJson<int[]>(LuminPackSerializer.SerializeJson(ints), static (string concreteReadInput, ref int[] concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Assert.Equal(longs, global::LuminPackUnitTest.ConcreteReads.ReadJson<List<long>>(LuminPackSerializer.SerializeJson(longs), static (string concreteReadInput, ref List<long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
    }

    /// <summary>Checks fused dictionary entries are the established pair arrays and read back through either reader path.</summary>
    [Fact]
    public void EntryWritersMatchTheTokenPath()
    {
        var byString = new Dictionary<string, long>();
        var byText = new Dictionary<string, string?>();
        for (int index = 0; index < Strings.Length; index++)
            if (Strings[index] is { } key)
            {
                byString[key] = index * 1000003L - 50;
                byText[key] = Strings[(index + 3) % Strings.Length];
            }
        var byInt = Enumerable.Range(-5, 40).ToDictionary(index => index * 977, index => index * 31);
        var byIntLong = byInt.ToDictionary(pair => pair.Key, pair => (long)pair.Value * int.MaxValue);
        var byIntText = byInt.ToDictionary(pair => pair.Key, pair => Strings[Math.Abs(pair.Key) % Strings.Length]);

        Check(byString, LuminPackSerializer.SerializeJson(byString), Text, value => Number(value));
        Assert.Equal(byString, global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<string, long>>(LuminPackSerializer.SerializeJson(byString), static (string concreteReadInput, ref Dictionary<string, long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Assert.Equal(byString, global::LuminPackUnitTest.ConcreteReads.ReadJson<IDictionary<string, long>>(LuminPackSerializer.SerializeJson(byString), static (string concreteReadInput, ref IDictionary<string, long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Check(byText, LuminPackSerializer.SerializeJson(byText), Text, Text);
        Assert.Equal(byText, global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<string, string?>>(LuminPackSerializer.SerializeJson(byText), static (string concreteReadInput, ref Dictionary<string, string?> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Check(byInt, LuminPackSerializer.SerializeJson(byInt), key => Number(key), value => Number(value));
        Assert.Equal(byInt, global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<int, int>>(LuminPackSerializer.SerializeJson(byInt), static (string concreteReadInput, ref Dictionary<int, int> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Check(byIntLong, LuminPackSerializer.SerializeJson(byIntLong), key => Number(key), value => Number(value));
        Assert.Equal(byIntLong, global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<int, long>>(LuminPackSerializer.SerializeJson(byIntLong), static (string concreteReadInput, ref Dictionary<int, long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Check(byIntText, LuminPackSerializer.SerializeJson(byIntText), key => Number(key), Text);
        Assert.Equal(byIntText, global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<int, string?>>(LuminPackSerializer.SerializeJson(byIntText), static (string concreteReadInput, ref Dictionary<int, string?> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Assert.Equal("[]", LuminPackSerializer.SerializeJson(new Dictionary<string, int>()));

        // <summary>Compares fused output with pair arrays built from single-value token writes.</summary>
        static void Check<TKey, TValue>(Dictionary<TKey, TValue> value, string json, Func<TKey, string> key, Func<TValue, string> item) where TKey : notnull
            => Assert.Equal("[" + string.Join(",", value.Select(pair => "[" + key(pair.Key) + "," + item(pair.Value) + "]")) + "]", json);

        // <summary>Writes one string through the unfused enumerable path and removes its array brackets.</summary>
        static string Text(string? value)
        {
            string array = LuminPackSerializer.SerializeJson((IEnumerable<string?>)(new[] { value }));
            return array.Substring(1, array.Length - 2);
        }

        // <summary>Writes one integer through the unfused enumerable path and removes its array brackets.</summary>
        static string Number(long value)
        {
            string array = LuminPackSerializer.SerializeJson((IEnumerable<long>)(new[] { value }));
            return array.Substring(1, array.Length - 2);
        }
    }

    /// <summary>Checks the fused reader declines unusual token shapes without consuming input, keeping the token path's results.</summary>
    [Fact]
    public void EntryReaderFallsBackForOtherShapes()
    {
        Assert.Equal(new Dictionary<string, long> { ["k"] = 1, ["x"] = -2 },
            global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<string, long>>(" [ [ \"k\" , 1 ] ,\n\t[\"x\",-2]\r\n ] ", static (string concreteReadInput, ref Dictionary<string, long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Assert.Equal(new Dictionary<string, string?> { ["k"] = null },
            global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<string, string?>>("[[\"k\",null]]", static (string concreteReadInput, ref Dictionary<string, string?> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Assert.Equal(new Dictionary<string, long> { ["a\"b\u00E9"] = 5 },
            global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<string, long>>("[[\"a\\\"b\\u00e9\",5]]", static (string concreteReadInput, ref Dictionary<string, long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Assert.Equal(new Dictionary<string, long> { ["k"] = 0 },
            global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<string, long>>("[[\"k\"]]", static (string concreteReadInput, ref Dictionary<string, long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Assert.Equal(new Dictionary<string, long> { ["k"] = 3 },
            global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<string, long>>("[[\"k\",3,4]]", static (string concreteReadInput, ref Dictionary<string, long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        // Extra entry elements were classified without being consumed; each drain now skips them.
        Assert.Equal(new Dictionary<string, long> { ["k"] = 3, ["m"] = 5 },
            global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<string, long>>("[[\"k\",3,\"x\",[1,{\"a\":2}]],[\"m\",5]]", static (string concreteReadInput, ref Dictionary<string, long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Assert.Equal(3, global::LuminPackUnitTest.ConcreteReads.ReadJson<System.Collections.Immutable.ImmutableDictionary<string, long>>("[[\"k\",3,4]]", static (string concreteReadInput, ref System.Collections.Immutable.ImmutableDictionary<string, long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget))["k"]);
        Assert.Equal(3, global::LuminPackUnitTest.ConcreteReads.ReadJson<System.Collections.ObjectModel.ReadOnlyDictionary<string, long>>("[[\"k\",3,4]]", static (string concreteReadInput, ref System.Collections.ObjectModel.ReadOnlyDictionary<string, long> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget))["k"]);
        Assert.ThrowsAny<Exception>(() => global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<int, int>>("[[1,3000000000]]", static (string concreteReadInput, ref Dictionary<int, int> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
        Assert.ThrowsAny<Exception>(() => global::LuminPackUnitTest.ConcreteReads.ReadJson<Dictionary<int, int>>("[[3000000000,1]]", static (string concreteReadInput, ref Dictionary<int, int> concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
    }

    /// <summary>Checks UTF-16 and strict documents keep their own token paths through the fused API.</summary>
    [Fact]
    public void OtherProfilesKeepTheirPaths()
    {
        var value = new FusedJsonHolder
        {
            Names = new List<string?> { "alpha", null, "\u89D2", "quote\"" },
            Counts = new[] { 1, -2, 3 },
            Map = new Dictionary<string, long> { ["first"] = 1, ["second"] = long.MinValue }
        };
        string utf8 = LuminPackSerializer.SerializeJson(value);
        var utf8Copy = global::LuminPackUnitTest.ConcreteReads.ReadJson<FusedJsonHolder>(utf8, static (string concreteReadInput, ref FusedJsonHolder concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget));
        Assert.Equal(value.Names, utf8Copy.Names);
        Assert.Equal(value.Counts, utf8Copy.Counts);
        Assert.Equal(value.Map, utf8Copy.Map);

        string utf16 = LuminPackSerializer.SerializeJson<LuminPackUtf16LengthOptions>((FusedJsonHolder)(value));
        Assert.Equal(utf8, utf16);
        var utf16Copy = global::LuminPackUnitTest.ConcreteReads.ReadJson<FusedJsonHolder>(utf16, static (string concreteReadInput, ref FusedJsonHolder concreteReadTarget) => LuminPackSerializer.DeserializeJson<LuminPackUtf16LengthOptions>(concreteReadInput, ref concreteReadTarget));
        Assert.Equal(value.Map, utf16Copy.Map);

        var strict = new StrictFusedJsonHolder { Map = value.Map };
        var strictCopy = global::LuminPackUnitTest.ConcreteReads.ReadJson<StrictFusedJsonHolder>(LuminPackSerializer.SerializeJson(strict), static (string concreteReadInput, ref StrictFusedJsonHolder concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget));
        Assert.Equal(value.Map, strictCopy.Map);
        Assert.ThrowsAny<Exception>(() => global::LuminPackUnitTest.ConcreteReads.ReadJson<StrictFusedJsonHolder>("{\"Map\":[[\"k\" 1]]}", static (string concreteReadInput, ref StrictFusedJsonHolder concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
    }
}

/// <summary>Holds fused collection shapes for profile checks.</summary>
[LuminPackable]
public partial class FusedJsonHolder
{
    /// <summary>A string list.</summary>
    public List<string?> Names = new();
    /// <summary>An int array.</summary>
    public int[] Counts = Array.Empty<int>();
    /// <summary>A string-keyed dictionary.</summary>
    public Dictionary<string, long> Map = new();
}

/// <summary>Holds a fused dictionary in a strictly validated JSON document.</summary>
[LuminPackable, LuminPackOption(ValidateJsonSyntax = true)]
public partial class StrictFusedJsonHolder
{
    /// <summary>A string-keyed dictionary.</summary>
    public Dictionary<string, long> Map = new();
}
