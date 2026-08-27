using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Generated;
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
[LuminPackUnion(11, typeof(MixedGenericUnionMember<int>))]
[LuminPackUnion(12, typeof(MixedGenericUnionMember<string>))]
public partial interface IMixedGenericUnion<T>
{
}

[LuminPackable]
public sealed partial class MixedGenericUnionMember<T> : IMixedGenericUnion<T>
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
        RunCase(results, nameof(ClassUnionUsesStaticTagDispatch), ClassUnionUsesStaticTagDispatch);
        RunCase(results, nameof(InterfaceUnionUsesStaticTagDispatch), InterfaceUnionUsesStaticTagDispatch);
        RunCase(results, nameof(KnownTagUsesStaticDeserializeSwitch), KnownTagUsesStaticDeserializeSwitch);
        RunCase(results, nameof(UnlistedUnionMemberIsRejected), UnlistedUnionMemberIsRejected);
        RunCase(results, nameof(CrossAssemblyUnlistedMemberIsRejected), CrossAssemblyUnlistedMemberIsRejected);
        RunCase(results, nameof(RegisteredUnlistedMemberRoundTrips), RegisteredUnlistedMemberRoundTrips);
        RunCase(results, nameof(NullWideGenericAndMultipleRootsRoundTrip), NullWideGenericAndMultipleRootsRoundTrip);
        RunCase(results, nameof(ClosedGenericTagsUseConstantTimeDispatch), ClosedGenericTagsUseConstantTimeDispatch);
        RunCase(results, nameof(DynamicUnionRegistrationIsNotAvailable), DynamicUnionRegistrationIsNotAvailable);
    }

    private static void ClassUnionUsesStaticTagDispatch()
    {
        DynamicUnionBase value = new StaticDynamicUnionMember { Value = 123 };
        var payload = LuminPackSerializer.Serialize(value);
        Assert(payload[0] == 1, "The class union fast path wrote the wrong constant tag.");
        Assert(LuminPackSerializer.Sizeof(value) == payload.Length,
            "The class union static Sizeof path disagrees with Serialize.");

        var result = LuminPackSerializer.Deserialize<DynamicUnionBase>(payload);
        Assert(result is StaticDynamicUnionMember { Value: 123 },
            "The class union fast path did not round-trip through the static tag switch.");
    }

    private static void InterfaceUnionUsesStaticTagDispatch()
    {
        IFastInterfaceUnion value = new FastInterfaceUnionMember { Value = 456 };
        var payload = LuminPackSerializer.Serialize(value);
        var json = LuminPackSerializer.SerializeJson(value);
        Assert(payload[0] == 7, "The interface union fast path wrote the wrong constant tag.");
        Assert(LuminPackSerializer.Sizeof(value) == payload.Length,
            "The interface union static Sizeof path disagrees with Serialize.");
        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(payload) is FastInterfaceUnionMember { Value: 456 },
            "The interface union binary fast path did not round-trip.");
        Assert(LuminPackSerializer.DeserializeJson<IFastInterfaceUnion>(json) is FastInterfaceUnionMember { Value: 456 },
            "The interface union JSON fast path did not round-trip.");
    }

    private static void KnownTagUsesStaticDeserializeSwitch()
    {
        IFastInterfaceUnion value = new FastInterfaceUnionMember { Value = 789 };
        var payload = LuminPackSerializer.Serialize(value);

        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(payload) is FastInterfaceUnionMember { Value: 789 },
            "A known local tag did not deserialize through the generated switch.");
    }

    private static void UnlistedUnionMemberIsRejected()
    {
        IFastInterfaceUnion value = new RegisteredInterfaceUnionMember { Value = 321 };
        AssertThrows(() => LuminPackSerializer.Serialize(value),
            "An unlisted union member was accepted without a generated tag.");
        AssertThrows(() => LuminPackSerializer.SerializeJson(value),
            "An unlisted JSON union member was accepted without a generated tag.");
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

    private static void CrossAssemblyUnlistedMemberIsRejected()
    {
        IExternalUnion value = new CrossAssemblyUnionMember { Value = 654 };
        ExternalClassUnionRoot classValue = new CrossAssemblyClassUnionMember { Value = 655 };
        AssertThrows(() => LuminPackSerializer.Serialize(value),
            "An external interface union member without a generated contract tag was accepted.");
        AssertThrows(() => LuminPackSerializer.Serialize(classValue),
            "An external class union member without a generated contract tag was accepted.");
    }

    private static void RegisteredUnlistedMemberRoundTrips()
    {
        unsafe
        {
            IFastInterfaceUnion.Register<RegisteredInterfaceUnionMember>(100,
                &WriteRegisteredMember, &ReadRegisteredMember, &WriteJsonRegisteredMember, &ReadJsonRegisteredMember);
        }

        Assert(IFastInterfaceUnion.TryGetRegisteredFormatter(100, out var probeEntry) && probeEntry.ReadValue != 0,
            "probe: registered formatter not found by tag after Register.");

        IFastInterfaceUnion value = new RegisteredInterfaceUnionMember { Value = 321 };
        var payload = LuminPackSerializer.Serialize(value);
        Assert(payload[0] == 100, "The registered union member wrote the wrong wire tag.");
        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(payload) is RegisteredInterfaceUnionMember { Value: 321 },
            "The registered union member did not round-trip.");
        var json = LuminPackSerializer.SerializeJson(value);
        Assert(LuminPackSerializer.DeserializeJson<IFastInterfaceUnion>(json) is RegisteredInterfaceUnionMember { Value: 321 },
            "The registered union member did not round-trip through JSON.");
    }

    private static void WriteRegisteredMember(ref LuminPackWriter writer, ref IFastInterfaceUnion value)
    {
        writer.WriteUnionHeader(100);
        writer.WriteValue(LuminPackMarshal.As<IFastInterfaceUnion, RegisteredInterfaceUnionMember>(ref value).Value);
    }

    private static void ReadRegisteredMember(ref LuminPackReader reader, ref IFastInterfaceUnion value)
    {
        var v = new RegisteredInterfaceUnionMember();
        reader.ReadValue(ref v.Value);
        value = LuminPackMarshal.As<RegisteredInterfaceUnionMember, IFastInterfaceUnion>(ref v);
    }

    private static void WriteJsonRegisteredMember(ref LuminPackJsonWriter writer, ref IFastInterfaceUnion value)
    {
        writer.WriteObjectStart();
        if (writer.Option.StringEncoding == LuminPack.Option.LuminPackStringEncoding.UTF8)
            writer.WritePropertyName(LuminPackConstUtf8.TypeU8);
        else
            writer.WritePropertyName(LuminPackConstUtf8.TypeU16);
        writer.WriteInt(100);
        if (writer.Option.StringEncoding == LuminPack.Option.LuminPackStringEncoding.UTF8)
            writer.WritePropertyName(LuminPackConstUtf8.ValueU8);
        else
            writer.WritePropertyName(LuminPackConstUtf8.ValueU16);
        writer.WriteValue(LuminPackMarshal.As<IFastInterfaceUnion, RegisteredInterfaceUnionMember>(ref value).Value);
        writer.WriteObjectEnd();
    }

    private static void ReadJsonRegisteredMember(ref LuminPackJsonReader reader, ref IFastInterfaceUnion value)
    {
        var v = new RegisteredInterfaceUnionMember();
        reader.ReadValue(ref v.Value);
        value = LuminPackMarshal.As<RegisteredInterfaceUnionMember, IFastInterfaceUnion>(ref v);
    }

    private static void ClosedGenericTagsUseConstantTimeDispatch()
    {
        IMixedGenericUnion<int> closed = new MixedGenericUnionMember<int> { Value = 91 };
        IMixedGenericUnion<string> second = new MixedGenericUnionMember<string> { Value = "second" };

        var closedPayload = LuminPackSerializer.Serialize(closed);
        var secondPayload = LuminPackSerializer.Serialize(second);

        Assert(closedPayload[0] == 11,
            "The first explicit closed-generic union member wrote the wrong wire tag.");
        Assert(secondPayload[0] == 12,
            "The second explicit closed-generic union member wrote the wrong wire tag.");
        Assert(LuminPackSerializer.Sizeof(closed) == closedPayload.Length,
            "The first closed-generic union member size did not match its payload.");
        Assert(LuminPackSerializer.Sizeof(second) == secondPayload.Length,
            "The second closed-generic union member size did not match its payload.");
        Assert(LuminPackSerializer.Deserialize<IMixedGenericUnion<int>>(closedPayload) is
                MixedGenericUnionMember<int> { Value: 91 },
            "The first explicit closed-generic union member did not round-trip.");
        Assert(LuminPackSerializer.Deserialize<IMixedGenericUnion<string>>(secondPayload) is
                MixedGenericUnionMember<string> { Value: "second" },
            "The second explicit closed-generic union member did not round-trip.");
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
        var payload = LuminPackSerializer.Serialize(value);
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

    private static void DynamicUnionRegistrationIsNotAvailable()
    {
        DynamicUnionBase value = new StaticDynamicUnionMember { Value = 1 };
        Assert(LuminPackSerializer.Deserialize<DynamicUnionBase>(LuminPackSerializer.Serialize(value)) is StaticDynamicUnionMember,
            "A statically declared union tag did not round-trip without registration.");
    }

    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            return;
        }

        throw new InvalidOperationException(message);
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
