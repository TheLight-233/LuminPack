using System.Numerics;
using LuminPack;
using LuminPack.Option;

namespace LuminPackUnitTest;

internal static class JsonReaderSafetyTest
{
    private static readonly LuminPackSerializerOption Utf16 = new()
    {
        StringEncoding = LuminPackStringEncoding.UTF16
    };

    public static void Run(List<string> results)
    {
        RunCase(results, nameof(TopLevelScalarsReadTheirFirstToken), TopLevelScalarsReadTheirFirstToken);
        RunCase(results, nameof(EscapedStringsUnescapeInBothEncodings), EscapedStringsUnescapeInBothEncodings);
        RunCase(results, nameof(EscapedPropertyNamesAndUnknownNestedValues), EscapedPropertyNamesAndUnknownNestedValues);
        RunCase(results, nameof(MalformedStringsAreRejected), MalformedStringsAreRejected);
        RunCase(results, nameof(MalformedNumbersAndTrailingDataAreRejected), MalformedNumbersAndTrailingDataAreRejected);
        RunCase(results, nameof(NumericVectorTypesConsumeEveryArrayToken), NumericVectorTypesConsumeEveryArrayToken);
        RunCase(results, nameof(NestedUnionsConsumeTheirOuterObjectEnd), NestedUnionsConsumeTheirOuterObjectEnd);
        RunCase(results, nameof(CollectionsUseSymmetricJsonShapes), CollectionsUseSymmetricJsonShapes);
        RunCase(results, nameof(MultiDimensionalArraysUseSymmetricNestedArrays), MultiDimensionalArraysUseSymmetricNestedArrays);
    }

    private static void TopLevelScalarsReadTheirFirstToken()
    {
        Assert(LuminPackSerializer.DeserializeJson<int>("-12345") == -12345,
            "A top-level UTF-8 integer was parsed before the first token was read.");
        Assert(LuminPackSerializer.DeserializeJson<bool>("true"),
            "A top-level UTF-8 boolean was parsed before the first token was read.");
        Assert(LuminPackSerializer.DeserializeJson<string>("\"value\"") == "value",
            "A top-level UTF-8 string was parsed before the first token was read.");
        Assert(LuminPackSerializer.DeserializeJson<string?>("null") is null,
            "A top-level UTF-8 null was not recognized.");

        Assert(LuminPackSerializer.DeserializeJson<int>("-54321", Utf16) == -54321,
            "A top-level UTF-16 integer was parsed before the first token was read.");
        Assert(!LuminPackSerializer.DeserializeJson<bool>("false", Utf16),
            "A top-level UTF-16 boolean was parsed before the first token was read.");
        Assert(LuminPackSerializer.DeserializeJson<string>("\"value\"", Utf16) == "value",
            "A top-level UTF-16 string was parsed before the first token was read.");
        Assert(LuminPackSerializer.DeserializeJson<string?>("null", Utf16) is null,
            "A top-level UTF-16 null was not recognized.");
    }

    private static void EscapedStringsUnescapeInBothEncodings()
    {
        const string json = "\"quote=\\\" slash=\\\\ controls=\\b\\f\\n\\r\\t unicode=\\u4F60 emoji=\\uD83D\\uDE00\"";
        const string expected = "quote=\" slash=\\ controls=\b\f\n\r\t unicode=你 emoji=😀";

        Assert(LuminPackSerializer.DeserializeJson<string>(json) == expected,
            "UTF-8 JSON escapes were returned as raw backslash sequences.");
        Assert(LuminPackSerializer.DeserializeJson<string>(json, Utf16) == expected,
            "UTF-16 JSON escapes were returned as raw backslash sequences.");
    }

    private static void EscapedPropertyNamesAndUnknownNestedValues()
    {
        const string escapedName = "{\"V\\u0061l\":42}";
        Assert(LuminPackSerializer.DeserializeJson<A>(escapedName)?.Val == 42,
            "An escaped UTF-8 property name did not match its generated member.");
        Assert(LuminPackSerializer.DeserializeJson<A>(escapedName, Utf16)?.Val == 42,
            "An escaped UTF-16 property name did not match its generated member.");

        const string unknownNested =
            "{\"Unknown\":{\"deep\":[1,{\"text\":\"brace } bracket ] escaped \\\"\"}]},\"Val\":73}";
        Assert(LuminPackSerializer.DeserializeJson<A>(unknownNested)?.Val == 73,
            "Skipping an unknown UTF-8 nested value consumed the following known property.");
        Assert(LuminPackSerializer.DeserializeJson<A>(unknownNested, Utf16)?.Val == 73,
            "Skipping an unknown UTF-16 nested value consumed the following known property.");
    }

    private static void MalformedStringsAreRejected()
    {
        AssertThrows<FormatException>(() => LuminPackSerializer.DeserializeJson<string>("\"unterminated"));
        AssertThrows<FormatException>(() => LuminPackSerializer.DeserializeJson<string>("\"\\u12\""));
        AssertThrows<FormatException>(() => LuminPackSerializer.DeserializeJson<string>("\"\\uD800x\""));
        AssertThrows<FormatException>(() => LuminPackSerializer.DeserializeJson<string>("\"\\q\"", Utf16));
    }

    private static void MalformedNumbersAndTrailingDataAreRejected()
    {
        AssertThrows<FormatException>(() => LuminPackSerializer.DeserializeJson<int>("1e+"));
        AssertThrows<FormatException>(() => LuminPackSerializer.DeserializeJson<double>("-."));
        AssertThrows<FormatException>(() => LuminPackSerializer.DeserializeJson<byte>("256"));
        AssertThrows<FormatException>(() => LuminPackSerializer.DeserializeJson<int>("1 2"));
        AssertThrows<FormatException>(() => LuminPackSerializer.DeserializeJson<int>("12x", Utf16));
    }

    private static void NumericVectorTypesConsumeEveryArrayToken()
    {
        RoundTrip(new Vector2(1.25f, -2.5f));
        RoundTrip(new Vector3(1.25f, -2.5f, 3.75f));
        RoundTrip(new Vector4(1.25f, -2.5f, 3.75f, -4.125f));
        RoundTrip(new Quaternion(1.25f, -2.5f, 3.75f, -4.125f));
        RoundTrip(new Matrix3x2(1, 2, 3, 4, 5, 6));
        RoundTrip(new Matrix4x4(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16));
        RoundTrip(new Complex(1.25, -9.5));
        RoundTrip(new Plane(new Vector3(1, 2, 3), 4));
    }

    private static void NestedUnionsConsumeTheirOuterObjectEnd()
    {
        ISerializable value = new Class1
        {
            A = 11,
            B = new DateTime(2026, 7, 28, 12, 34, 56, DateTimeKind.Utc),
            C = Guid.Parse("bfa9ac40-e566-4e0a-93e4-9e2580f4ac11"),
            D = new Struct1
            {
                A = 22,
                B = new DateTime(2025, 6, 27, 1, 2, 3, DateTimeKind.Utc),
                C = Guid.Parse("322c9375-0414-4f8f-ae52-a5720e240715")
            }
        };

        AssertNestedUnion(value, null);
        AssertNestedUnion(value, Utf16);
    }

    private static void AssertNestedUnion(ISerializable value, LuminPackSerializerOption? option)
    {
        var json = LuminPackSerializer.SerializeJson<ISerializable>(value, option);
        var result = LuminPackSerializer.DeserializeJson<ISerializable>(json, option);
        Assert(result is Class1 { A: 11, D: Struct1 { A: 22 } },
            "A nested JSON union left its wrapper end token unread or lost the nested value.");
    }

    private static void CollectionsUseSymmetricJsonShapes()
    {
        var dictionary = new Dictionary<string, List<int>>
        {
            ["one"] = [1, 2, 3],
            ["two"] = [4, 5]
        };
        var dictionaryResult = RoundTrip(dictionary);
        Assert(dictionaryResult is not null &&
               dictionaryResult.Count == dictionary.Count &&
               dictionaryResult.All(item => item.Value.SequenceEqual(dictionary[item.Key])),
            "Dictionary JSON writer and reader used different shapes.");

        var sortedList = new SortedList<string, string>
        {
            ["a\"b"] = "line\nvalue",
            ["plain"] = "text"
        };
        var sortedResult = RoundTrip(sortedList, Utf16);
        Assert(sortedResult is not null && sortedResult.SequenceEqual(sortedList),
            "UTF-16 SortedList JSON property names or values did not round-trip.");

        var strings = new List<string?> { "first", null, "escaped\nvalue" };
        var stringResult = RoundTrip(strings);
        Assert(stringResult is not null && stringResult.SequenceEqual(strings),
            "A JSON list containing null or escaped strings did not round-trip.");

        IGrouping<string, int> grouping = new[] { 1, 2, 3 }.GroupBy(static _ => "key").Single();
        var groupingResult = RoundTrip(grouping);
        Assert(groupingResult is not null && groupingResult.Key == "key" && groupingResult.SequenceEqual(grouping),
            "IGrouping JSON writer and reader used different object protocols.");
        var groupingUtf16Result = RoundTrip(grouping, Utf16);
        Assert(groupingUtf16Result is not null &&
               groupingUtf16Result.Key == "key" && groupingUtf16Result.SequenceEqual(grouping),
            "UTF-16 IGrouping JSON property names were encoded with the UTF-8 protocol.");
    }

    private static void MultiDimensionalArraysUseSymmetricNestedArrays()
    {
        int[,] twoDimensional =
        {
            { 1, 2, 3 },
            { 4, 5, 6 }
        };
        var twoResult = RoundTrip(twoDimensional);
        Assert(twoResult is not null && twoResult.Cast<int>().SequenceEqual(twoDimensional.Cast<int>()),
            "Two-dimensional JSON array elements were not read from the nested rows.");

        string[,,] threeDimensional =
        {
            {
                { "a", "b" },
                { "c", "escaped\nvalue" }
            },
            {
                { "d", "e" },
                { "f", "g" }
            }
        };
        var threeResult = RoundTrip(threeDimensional, Utf16);
        Assert(threeResult is not null &&
               threeResult.Cast<string>().SequenceEqual(threeDimensional.Cast<string>()),
            "UTF-16 three-dimensional JSON array elements were not read from the nested planes.");

        var emptySecondDimension = new int[2, 0];
        var emptySecondResult = RoundTrip(emptySecondDimension);
        Assert(emptySecondResult is not null &&
               emptySecondResult.GetLength(0) == 2 && emptySecondResult.GetLength(1) == 0,
            "A rectangular JSON array with an empty second dimension lost its first dimension.");

        var emptyThirdDimension = new int[1, 2, 0];
        var emptyThirdResult = RoundTrip(emptyThirdDimension, Utf16);
        Assert(emptyThirdResult is not null &&
               emptyThirdResult.GetLength(0) == 1 &&
               emptyThirdResult.GetLength(1) == 2 &&
               emptyThirdResult.GetLength(2) == 0,
            "A rectangular JSON array with an empty third dimension lost its outer dimensions.");

        AssertThrows<FormatException>(() =>
            LuminPackSerializer.DeserializeJson<int[,]>("[[1,2],[3]]"));
        AssertThrows<FormatException>(() =>
            LuminPackSerializer.DeserializeJson<int[,,]>("[[[1],[2]],[[3,4],[5]]]", Utf16));
        AssertThrows<FormatException>(() =>
            LuminPackSerializer.DeserializeJson<int[,,,]>("[[[[1,2],[3]]]]"));

        var fourDimensional = new int[1, 1, 1, 2];
        fourDimensional[0, 0, 0, 0] = 10;
        fourDimensional[0, 0, 0, 1] = 20;
        var fourResult = RoundTrip(fourDimensional);
        Assert(fourResult is not null &&
               fourResult.GetLength(3) == 2 &&
               fourResult[0, 0, 0, 0] == 10 && fourResult[0, 0, 0, 1] == 20,
            "Four-dimensional JSON array protocol was not symmetric.");
    }

    private static T? RoundTrip<T>(T value, LuminPackSerializerOption? option = null)
    {
        var json = LuminPackSerializer.SerializeJson(value, option);
        var result = LuminPackSerializer.DeserializeJson<T>(json, option);
        if (typeof(T).IsValueType)
        {
            Assert(EqualityComparer<T>.Default.Equals(value, result),
                $"{typeof(T).Name} JSON round-trip changed its value.");
        }
        return result;
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
    }

    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
