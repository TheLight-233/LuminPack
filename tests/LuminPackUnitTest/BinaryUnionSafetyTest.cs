using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;

namespace LuminPackUnitTest;

[LuminPackable]
 [LuminPackUnion(1, typeof(StaticDynamicUnionMember))]
public abstract class DynamicUnionBase
{
}

[LuminPackable]
public sealed class StaticDynamicUnionMember : DynamicUnionBase
{
}

internal static class BinaryUnionSafetyTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(BoxedStructUnionRoundTripsAndSizeMatches), BoxedStructUnionRoundTripsAndSizeMatches);
        RunCase(results, nameof(DynamicUnionTagZeroAndConflictsAreHandledAtomically), DynamicUnionTagZeroAndConflictsAreHandledAtomically);
        RunCase(results, nameof(DynamicUnionRejectsStaticTagWithoutPublishingType), DynamicUnionRejectsStaticTagWithoutPublishingType);
    }

    private static void BoxedStructUnionRoundTripsAndSizeMatches()
    {
        ISerializable value = new Struct1
        {
            A = 123456789,
            B = new DateTime(2026, 7, 28, 12, 34, 56, DateTimeKind.Utc),
            C = Guid.Parse("8bd49956-c49f-4ba2-a880-c7527fa3db90")
        };

        var rawValue = (Struct1)value;
        var rawSize = LuminPackSerializer.Sizeof(rawValue);
        var rawPayload = LuminPackSerializer.Serialize(rawValue);
        var expectedSize = LuminPackSerializer.Sizeof(value);
        var payload = LuminPackSerializer.Serialize<ISerializable>(value);
        var result = LuminPackSerializer.Deserialize<ISerializable>(payload);

        Assert(expectedSize == payload.Length,
            $"Boxed struct union Sizeof returned {expectedSize}, but serialization wrote {payload.Length} bytes " +
            $"(direct struct: Sizeof {rawSize}, wrote {rawPayload.Length}).");
        Assert(result is Struct1 actual &&
               actual.A == 123456789 &&
               actual.B == new DateTime(2026, 7, 28, 12, 34, 56, DateTimeKind.Utc) &&
               actual.C == Guid.Parse("8bd49956-c49f-4ba2-a880-c7527fa3db90"),
            "A boxed struct union did not survive binary round-trip.");
    }

    private static void DynamicUnionTagZeroAndConflictsAreHandledAtomically()
    {
        global::LuminPack.Generated.LuminPackUnitTest_DynamicUnionBaseParser.Register(
            typeof(string), 0, WriteEmptyUnion, ReadEmptyUnion, WriteJsonEmptyUnion, ReadJsonEmptyUnion);

        var stringMethodTable = LuminPackMarshal.GetMethodTable(typeof(string));
        Assert(global::LuminPack.Generated.LuminPackUnitTest_DynamicUnionBaseParser._unionMap
                   .TryGetValue(stringMethodTable, out var stringEntry) && stringEntry.Tag == 0,
            "Dynamic union tag 0 was not published to the type map.");
        Assert(global::LuminPack.Generated.LuminPackUnitTest_DynamicUnionBaseParser._externalMap
                   .TryGetValue((nint)1, out _),
            "Dynamic union tag 0 was not encoded to a non-zero read-map key.");

        AssertThrowsArgumentException(() =>
            global::LuminPack.Generated.LuminPackUnitTest_DynamicUnionBaseParser.Register(
                typeof(object), 0, WriteEmptyUnion, ReadEmptyUnion, WriteJsonEmptyUnion, ReadJsonEmptyUnion));

        global::LuminPack.Generated.LuminPackUnitTest_DynamicUnionBaseParser.Register(
            typeof(object), 2, WriteEmptyUnion, ReadEmptyUnion, WriteJsonEmptyUnion, ReadJsonEmptyUnion);

        var objectMethodTable = LuminPackMarshal.GetMethodTable(typeof(object));
        Assert(global::LuminPack.Generated.LuminPackUnitTest_DynamicUnionBaseParser._unionMap
                   .TryGetValue(objectMethodTable, out var objectEntry) && objectEntry.Tag == 2,
            "A rejected duplicate tag partially published its type mapping.");
    }

    private static void DynamicUnionRejectsStaticTagWithoutPublishingType()
    {
        AssertThrowsArgumentException(() =>
            global::LuminPack.Generated.LuminPackUnitTest_ISerializableParser.Register(
                typeof(Uri), 0, WriteSerializable, ReadSerializable, WriteJsonSerializable, ReadJsonSerializable));

        global::LuminPack.Generated.LuminPackUnitTest_ISerializableParser.Register(
            typeof(Uri), 60_000, WriteSerializable, ReadSerializable, WriteJsonSerializable, ReadJsonSerializable);

        var methodTable = LuminPackMarshal.GetMethodTable(typeof(Uri));
        Assert(global::LuminPack.Generated.LuminPackUnitTest_ISerializableParser._unionMap
                   .TryGetValue(methodTable, out var entry) && entry.Tag == 60_000,
            "A rejected static-tag collision partially published its type mapping.");
    }

    private static void WriteEmptyUnion(ref LuminPackWriter writer, ref DynamicUnionBase value) { }
    private static void ReadEmptyUnion(ref LuminPackReader reader, ref DynamicUnionBase value) { }
    private static void WriteJsonEmptyUnion(ref LuminPackJsonWriter writer, ref DynamicUnionBase value) { }
    private static void ReadJsonEmptyUnion(ref LuminPackJsonReader reader, ref DynamicUnionBase value) { }
    private static void WriteSerializable(ref LuminPackWriter writer, ref ISerializable value) { }
    private static void ReadSerializable(ref LuminPackReader reader, ref ISerializable value) { }
    private static void WriteJsonSerializable(ref LuminPackJsonWriter writer, ref ISerializable value) { }
    private static void ReadJsonSerializable(ref LuminPackJsonReader reader, ref ISerializable value) { }

    private static void AssertThrowsArgumentException(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException("Expected an ArgumentException.");
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
