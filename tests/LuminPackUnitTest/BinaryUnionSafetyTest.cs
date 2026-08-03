using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPackUnionContracts;

namespace LuminPackUnitTest;

[LuminPackable]
 [LuminPackUnion(1, typeof(StaticDynamicUnionMember))]
public abstract partial class DynamicUnionBase
{
}

[LuminPackable]
public sealed partial class StaticDynamicUnionMember : DynamicUnionBase
{
    public int Value;
}

[LuminPackable]
[LuminPackUnion(7, typeof(FastInterfaceUnionMember))]
public partial interface IFastInterfaceUnion
{
}

[LuminPackable]
public sealed partial class FastInterfaceUnionMember : IFastInterfaceUnion
{
    public int Value;
}

public sealed class RegisteredInterfaceUnionMember : IFastInterfaceUnion
{
    public int Value;
}

[LuminPackable]
[LuminPackWideTag]
[LuminPackUnion(60_000, typeof(WideUnionMember))]
public partial interface IWideUnion
{
}

[LuminPackable]
public sealed partial class WideUnionMember : IWideUnion
{
    public int Value;
}

[LuminPackable]
public partial interface IGenericFastUnion<T>
{
}

[LuminPackable]
public sealed partial class GenericFastUnionMember<T> : IGenericFastUnion<T>
{
    public T Value = default!;
}

[LuminPackable]
public abstract partial class FirstClassUnionRoot
{
}

[LuminPackable]
public abstract partial class SecondClassUnionRoot : FirstClassUnionRoot
{
}

[LuminPackable]
public sealed partial class MultipleClassUnionMember : SecondClassUnionRoot
{
    public int Value;
}

[LuminPackable]
public sealed partial class CrossAssemblyUnionMember : IExternalUnion
{
    public int Value;
}

[LuminPackable]
public sealed partial class CrossAssemblyClassUnionMember : ExternalClassUnionRoot
{
    public int Value;
}

internal static class BinaryUnionSafetyTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(BoxedStructUnionRoundTripsAndSizeMatches), BoxedStructUnionRoundTripsAndSizeMatches);
        RunCase(results, nameof(ClassUnionFastPathDoesNotPopulateUnionMap), ClassUnionFastPathDoesNotPopulateUnionMap);
        RunCase(results, nameof(InterfaceUnionFastPathDoesNotPopulateUnionMap), InterfaceUnionFastPathDoesNotPopulateUnionMap);
        RunCase(results, nameof(KnownTagUsesStaticDeserializeSwitch), KnownTagUsesStaticDeserializeSwitch);
        RunCase(results, nameof(InterfaceDefaultMethodUsesRegisteredFallback), InterfaceDefaultMethodUsesRegisteredFallback);
        RunCase(results, nameof(CrossAssemblyRegistrationUsesRootDefaultFallback), CrossAssemblyRegistrationUsesRootDefaultFallback);
        RunCase(results, nameof(NullWideGenericAndMultipleRootsRoundTrip), NullWideGenericAndMultipleRootsRoundTrip);
        RunCase(results, nameof(DynamicUnionTagZeroAndConflictsAreHandledAtomically), DynamicUnionTagZeroAndConflictsAreHandledAtomically);
        RunCase(results, nameof(DynamicUnionRejectsStaticTagWithoutPublishingType), DynamicUnionRejectsStaticTagWithoutPublishingType);
    }

    private static void ClassUnionFastPathDoesNotPopulateUnionMap()
    {
        DynamicUnionBase value = new StaticDynamicUnionMember { Value = 123 };
        var payload = LuminPackSerializer.Serialize(value);
        var methodTable = LuminPackMarshal.GetMethodTable(value);

        Assert(!global::LuminPack.Generated.LuminPackUnitTest_DynamicUnionBaseParser._unionMap
                .TryGetValue(methodTable, out _),
            "A local class union member was inserted into UnionMap.");
        Assert(payload[0] == 1, "The class union fast path wrote the wrong constant tag.");
        Assert(LuminPackSerializer.Sizeof(value) == payload.Length,
            "The class union virtual CalculateOffset path disagrees with Serialize.");

        var result = LuminPackSerializer.Deserialize<DynamicUnionBase>(payload);
        Assert(result is StaticDynamicUnionMember { Value: 123 },
            "The class union fast path did not round-trip through the static tag switch.");
    }

    private static void InterfaceUnionFastPathDoesNotPopulateUnionMap()
    {
        IFastInterfaceUnion value = new FastInterfaceUnionMember { Value = 456 };
        var payload = LuminPackSerializer.Serialize(value);
        var json = LuminPackSerializer.SerializeJson(value);
        var methodTable = LuminPackMarshal.GetMethodTable(value);

        Assert(!global::LuminPack.Generated.LuminPackUnitTest_IFastInterfaceUnionParser._unionMap
                .TryGetValue(methodTable, out _),
            "A local interface union member was inserted into UnionMap.");
        Assert(payload[0] == 7, "The interface union fast path wrote the wrong constant tag.");
        Assert(LuminPackSerializer.Sizeof(value) == payload.Length,
            "The interface union explicit CalculateOffset path disagrees with Serialize.");
        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(payload) is FastInterfaceUnionMember { Value: 456 },
            "The interface union binary fast path did not round-trip.");
        Assert(LuminPackSerializer.DeserializeJson<IFastInterfaceUnion>(json) is FastInterfaceUnionMember { Value: 456 },
            "The interface union JSON fast path did not round-trip.");
    }

    private static void KnownTagUsesStaticDeserializeSwitch()
    {
        IFastInterfaceUnion value = new FastInterfaceUnionMember { Value = 789 };
        var payload = LuminPackSerializer.Serialize(value);

        Assert(!global::LuminPack.Generated.LuminPackUnitTest_IFastInterfaceUnionParser._externalMap
                .TryGetValue((nint)8, out _),
            "A local tag was unexpectedly published to the registered-reader map.");
        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(payload) is FastInterfaceUnionMember { Value: 789 },
            "A known local tag did not deserialize through the generated switch.");
    }

    private static void InterfaceDefaultMethodUsesRegisteredFallback()
    {
        global::LuminPack.Generated.LuminPackUnitTest_IFastInterfaceUnionParser.Register(
            typeof(RegisteredInterfaceUnionMember),
            99,
            WriteRegisteredInterfaceUnion,
            ReadRegisteredInterfaceUnion,
            WriteJsonRegisteredInterfaceUnion,
            ReadJsonRegisteredInterfaceUnion);

        IFastInterfaceUnion value = new RegisteredInterfaceUnionMember { Value = 321 };
        var payload = LuminPackSerializer.Serialize(value);
        var json = LuminPackSerializer.SerializeJson(value);

        Assert(payload[0] == 99, "The interface default method did not use the registered writer.");
        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(payload) is RegisteredInterfaceUnionMember { Value: 321 },
            "An unknown/local-default binary tag did not use the registered reader fallback.");
        Assert(LuminPackSerializer.DeserializeJson<IFastInterfaceUnion>(json) is RegisteredInterfaceUnionMember { Value: 321 },
            "An unknown/local-default JSON tag did not use the registered reader fallback.");
    }

    private static void NullWideGenericAndMultipleRootsRoundTrip()
    {
        IFastInterfaceUnion? nullValue = null;
        var nullPayload = LuminPackSerializer.Serialize(nullValue);
        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(nullPayload) is null,
            "A null interface union did not round-trip.");

        IWideUnion wide = new WideUnionMember { Value = 42 };
        var widePayload = LuminPackSerializer.Serialize(wide);
        Assert(widePayload[0] == LuminPack.Code.LuminPackCode.WideTag,
            "The wide-tag virtual path did not preserve the wide union header.");
        Assert(LuminPackSerializer.Deserialize<IWideUnion>(widePayload) is WideUnionMember { Value: 42 },
            "A wide-tag union did not round-trip.");

        IGenericFastUnion<int> generic = new GenericFastUnionMember<int> { Value = 73 };
        var genericPayload = LuminPackSerializer.Serialize(generic);
        Assert(LuminPackSerializer.Deserialize<IGenericFastUnion<int>>(genericPayload) is GenericFastUnionMember<int> { Value: 73 },
            "A generic interface union did not round-trip.");

        var member = new MultipleClassUnionMember { Value = 88 };
        FirstClassUnionRoot first = member;
        SecondClassUnionRoot second = member;
        var firstPayload = LuminPackSerializer.Serialize(first);
        var secondPayload = LuminPackSerializer.Serialize(second);
        Assert(LuminPackSerializer.Deserialize<FirstClassUnionRoot>(firstPayload) is MultipleClassUnionMember { Value: 88 },
            "The first independent class-union virtual slot failed.");
        Assert(LuminPackSerializer.Deserialize<SecondClassUnionRoot>(secondPayload) is MultipleClassUnionMember { Value: 88 },
            "The second independent class-union virtual slot failed.");
    }

    private static void CrossAssemblyRegistrationUsesRootDefaultFallback()
    {
        global::LuminPack.Generated.LuminPackUnionContracts_IExternalUnionParser.Register(
            typeof(CrossAssemblyUnionMember),
            123,
            WriteCrossAssemblyUnion,
            ReadCrossAssemblyUnion,
            WriteJsonCrossAssemblyUnion,
            ReadJsonCrossAssemblyUnion);
        global::LuminPack.Generated.LuminPackUnionContracts_ExternalClassUnionRootParser.Register(
            typeof(CrossAssemblyClassUnionMember),
            124,
            WriteCrossAssemblyClassUnion,
            ReadCrossAssemblyClassUnion,
            WriteJsonCrossAssemblyClassUnion,
            ReadJsonCrossAssemblyClassUnion);

        IExternalUnion value = new CrossAssemblyUnionMember { Value = 654 };
        var payload = LuminPackSerializer.Serialize(value);
        var json = LuminPackSerializer.SerializeJson(value);

        Assert(payload[0] == 123, "The cross-assembly default interface method did not use the registered writer.");
        Assert(LuminPackSerializer.Deserialize<IExternalUnion>(payload) is CrossAssemblyUnionMember { Value: 654 },
            "The cross-assembly registered binary reader did not run from switch default.");
        Assert(LuminPackSerializer.DeserializeJson<IExternalUnion>(json) is CrossAssemblyUnionMember { Value: 654 },
            "The cross-assembly registered JSON reader did not run from switch default.");

        ExternalClassUnionRoot classValue = new CrossAssemblyClassUnionMember { Value = 655 };
        var classPayload = LuminPackSerializer.Serialize(classValue);
        var classJson = LuminPackSerializer.SerializeJson(classValue);
        Assert(classPayload[0] == 124, "The cross-assembly class virtual default did not use the registered writer.");
        Assert(LuminPackSerializer.Deserialize<ExternalClassUnionRoot>(classPayload) is CrossAssemblyClassUnionMember { Value: 655 },
            "The cross-assembly class registered binary reader did not run from switch default.");
        Assert(LuminPackSerializer.DeserializeJson<ExternalClassUnionRoot>(classJson) is CrossAssemblyClassUnionMember { Value: 655 },
            "The cross-assembly class registered JSON reader did not run from switch default.");
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

    private static void WriteRegisteredInterfaceUnion(ref LuminPackWriter writer, ref IFastInterfaceUnion value)
    {
        writer.WriteUnionHeader(99);
        var memberValue = ((RegisteredInterfaceUnionMember)value).Value;
        writer.WriteValue(memberValue);
    }

    private static void ReadRegisteredInterfaceUnion(ref LuminPackReader reader, ref IFastInterfaceUnion value)
    {
        var memberValue = 0;
        reader.ReadValue(ref memberValue);
        value = new RegisteredInterfaceUnionMember { Value = memberValue };
    }

    private static void WriteJsonRegisteredInterfaceUnion(ref LuminPackJsonWriter writer, ref IFastInterfaceUnion value)
    {
        writer.WriteObjectStart();
        writer.WritePropertyName("$type");
        writer.WriteInt(99);
        writer.WritePropertyName("$value");
        var memberValue = ((RegisteredInterfaceUnionMember)value).Value;
        writer.WriteValue(ref memberValue);
        writer.WriteObjectEnd();
    }

    private static void ReadJsonRegisteredInterfaceUnion(ref LuminPackJsonReader reader, ref IFastInterfaceUnion value)
    {
        var memberValue = 0;
        reader.ReadValue(ref memberValue);
        value = new RegisteredInterfaceUnionMember { Value = memberValue };
    }

    private static void WriteCrossAssemblyUnion(ref LuminPackWriter writer, ref IExternalUnion value)
    {
        writer.WriteUnionHeader(123);
        var memberValue = (CrossAssemblyUnionMember)value;
        writer.WritePolymorphismValue(memberValue);
    }

    private static void ReadCrossAssemblyUnion(ref LuminPackReader reader, ref IExternalUnion value)
    {
        CrossAssemblyUnionMember memberValue = null!;
        reader.ReadPolymorphismValue(ref memberValue);
        value = memberValue;
    }

    private static void WriteJsonCrossAssemblyUnion(ref LuminPackJsonWriter writer, ref IExternalUnion value)
    {
        writer.WriteObjectStart();
        writer.WritePropertyName("$type");
        writer.WriteInt(123);
        writer.WritePropertyName("$value");
        var memberValue = (CrossAssemblyUnionMember)value;
        writer.WriteValue(ref memberValue);
        writer.WriteObjectEnd();
    }

    private static void ReadJsonCrossAssemblyUnion(ref LuminPackJsonReader reader, ref IExternalUnion value)
    {
        CrossAssemblyUnionMember memberValue = null!;
        reader.ReadValue(ref memberValue);
        value = memberValue;
    }

    private static void WriteCrossAssemblyClassUnion(ref LuminPackWriter writer, ref ExternalClassUnionRoot value)
    {
        writer.WriteUnionHeader(124);
        var memberValue = (CrossAssemblyClassUnionMember)value;
        writer.WritePolymorphismValue(memberValue);
    }

    private static void ReadCrossAssemblyClassUnion(ref LuminPackReader reader, ref ExternalClassUnionRoot value)
    {
        CrossAssemblyClassUnionMember memberValue = null!;
        reader.ReadPolymorphismValue(ref memberValue);
        value = memberValue;
    }

    private static void WriteJsonCrossAssemblyClassUnion(ref LuminPackJsonWriter writer, ref ExternalClassUnionRoot value)
    {
        writer.WriteObjectStart();
        writer.WritePropertyName("$type");
        writer.WriteInt(124);
        writer.WritePropertyName("$value");
        var memberValue = (CrossAssemblyClassUnionMember)value;
        writer.WriteValue(ref memberValue);
        writer.WriteObjectEnd();
    }

    private static void ReadJsonCrossAssemblyClassUnion(ref LuminPackJsonReader reader, ref ExternalClassUnionRoot value)
    {
        CrossAssemblyClassUnionMember memberValue = null!;
        reader.ReadValue(ref memberValue);
        value = memberValue;
    }

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
